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

namespace UavPms.AIInspectionService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
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

        // Đăng ký Unit of Work và Generic Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        
        // Đăng ký các Repositories quản lý phân cấp lưới điện & tài sản
        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<ISubstationRepository, SubstationRepository>();
        services.AddScoped<ITransmissionLineRepository, TransmissionLineRepository>();
        
        // Đăng ký các Repositories đặc thù 
        services.AddScoped<ITowerRepository, TowerRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IAnomalyRepository, AnomalyRepository>();
        services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        services.AddScoped<IUavRepository, UavRepository>();
        
        // Đăng ký Password Hasher và JWT Provider
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();

        // Đăng ký Notification Repository
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Đăng ký Monitor Repository
        services.AddScoped<IMonitorRepository, MonitorRepository>();

        // Đăng ký Audit Log Repository
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Đăng ký Inspection Media Repository
        services.AddScoped<IInspectionMediaRepository, InspectionMediaRepository>();

        // Đăng ký HttpContextAccessor và CurrentUserServices
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserServices, CurrentUserServices>();

        // Đăng ký các Dịch vụ OTP, Email (SendGrid) và Event Publisher
        services.AddMemoryCache();
        services.AddScoped<IEmailService, EmailService>();

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

        services.AddScoped<IOtpService, RedisOtpService>();

        // Đăng ký File Storage Service (Tự động chọn Supabase nếu có ApiKey, ngược lại dùng Local)
        var supabaseKey = configuration["Supabase:ApiKey"];
        if (!string.IsNullOrEmpty(supabaseKey) && !supabaseKey.Contains("YOUR_SUPABASE_SERVICE_ROLE_KEY"))
        {
            services.AddScoped<IFileStorageService, SupabaseFileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
        }

        // Đăng ký OTP Purpose Handlers (Strategy pattern)
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.LoginOtpHandler>();
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.ForgotPasswordOtpHandler>();
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.EmailVerificationOtpHandler>();
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.ChangeEmailOtpHandler>();
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.ChangePasswordOtpHandler>();
        services.AddScoped<IOtpPurposeHandler, UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers.DeleteAccountOtpHandler>();
        
        return services;
    }
}
