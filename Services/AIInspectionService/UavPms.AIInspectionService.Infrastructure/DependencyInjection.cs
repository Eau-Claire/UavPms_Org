using UavPms.Shared.Contracts.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grpc.Net.Client;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Infrastructure.Grpc;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Messaging;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Services;
using UavPms.Grpc.InspectionEvaluation;
using UavPms.AIInspectionService.Application.Common.Options;
using UavPms.Shared.Contracts.Interfaces;

namespace UavPms.AIInspectionService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Đăng ký Strongly-Typed Options với Validation & ValidateOnStart
        services.AddOptions<PythonAIOptions>()
            .Bind(configuration.GetSection(PythonAIOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RabbitMQOptions>()
            .Bind(configuration.GetSection(RabbitMQOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<AIAnalysisResultMessagingOptions>(configuration.GetSection(AIAnalysisResultMessagingOptions.SectionName));

        // Truyền Connection String vào cấu hình UseNpgsql
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => {
                    o.UseNetTopologySuite();
                    o.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        
        // Đăng ký RabbitMQ Connection (conditional - fallback to NoOp)
        var rabbitHost = configuration["RabbitMQ:HostName"];
        if (!string.IsNullOrEmpty(rabbitHost))
        {
            services.AddSingleton<RabbitMqConnection>();
            services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
        }
        else
        {
            services.AddScoped<IEventPublisher, NoOpEventPublisher>();
        }

        var inspectionEvaluationUrl = configuration["GrpcServices:InspectionEvaluationUrl"]
            ?? "http://inspectionevaluationservice:8080";
        services.AddSingleton(_ => GrpcChannel.ForAddress(inspectionEvaluationUrl));
        services.AddSingleton(sp =>
            new InspectionEvaluation.InspectionEvaluationClient(sp.GetRequiredService<GrpcChannel>()));
        services.AddScoped<IInspectionEvaluationClient, GrpcInspectionEvaluationClient>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();

        // Đăng ký Unit of Work và Generic Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAnomalyRepository, AnomalyRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IUavRepository, UavRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IInspectionMediaRepository, InspectionMediaRepository>();

        // Đăng ký HttpContextAccessor và CurrentUserServices
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserServices, CurrentUserServices>();

        // Đăng ký các Dịch vụ OTP, Email (SendGrid) và Event Publisher
        services.AddMemoryCache();

        // Register Redis ConnectionMultiplexer as Singleton
        var redisConnectionString = configuration["Redis:ConnectionString"];
        var redisPassword = configuration["Redis:Password"];
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            var configOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
            if (!string.IsNullOrEmpty(redisPassword))
            {
                configOptions.Password = redisPassword;
            }
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(StackExchange.Redis.ConnectionMultiplexer.Connect(configOptions));
        }

        // Media lives in MinIO; URL columns store bucket/object keys rebuilt from the public base on read (plan item 2.8).
        services.AddOptions<MinioOptions>().Bind(configuration.GetSection(MinioOptions.SectionName));
        var minio = configuration.GetSection(MinioOptions.SectionName).Get<MinioOptions>() ?? new MinioOptions();
        StorageKeys.Configure(minio.IsConfigured || !string.IsNullOrWhiteSpace(minio.PublicBaseUrl) ? minio.ResolvedPublicBaseUrl : null);

        // Đăng ký File Storage Service (MinIO khi đã cấu hình Minio:Endpoint/AccessKey/SecretKey, ngược lại dùng Local)
        if (minio.IsConfigured)
        {
            services.AddSingleton(_ => MinioFileStorageService.CreateClient(minio));
            services.AddScoped<IFileStorageService, MinioFileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }
      
        return services;
    }
}
