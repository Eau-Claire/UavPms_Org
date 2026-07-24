using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UavPms.AIInspectionService.Application.Common.Behaviors;

namespace UavPms.AIInspectionService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services.AddApplicationServices();
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Đăng ký toàn bộ các FluentValidation Validators trong Assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Đăng ký MediatR và cấu hình các pipeline behaviors chạy ngầm
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            
            // Đăng ký tự động ghi log
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            
            // Đăng ký tự động validate
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        
        return services;
    }
}