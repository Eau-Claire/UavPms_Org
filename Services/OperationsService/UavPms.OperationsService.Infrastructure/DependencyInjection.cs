using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Messaging;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Services;

namespace UavPms.OperationsService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
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

        return services;
    }
}