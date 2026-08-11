using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UavPms.IdentityService.Application.Common.Behaviors;
using UavPms.IdentityService.Application.Common.Interfaces;
using UavPms.IdentityService.Application.Common.Options;
using UavPms.IdentityService.Application.Common.Services;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

namespace UavPms.IdentityService.Application;

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
        
        // Đăng ký UserTokenService
        services.AddScoped<IUserTokenService, UserTokenService>();

        // Đăng ký các Strategies cho OTP Verification
        services.AddScoped<IOtpVerificationStrategy, LoginOtpStrategy>();
        services.AddScoped<IOtpVerificationStrategy, ForgotPasswordOtpStrategy>();
        services.AddScoped<IOtpVerificationStrategy, StepUpOtpStrategy>();
        services.AddScoped<OtpVerificationStrategyResolver>();
        
        return services;
    }
}