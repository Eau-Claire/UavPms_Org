using System;
using System.Threading.Tasks;

namespace UavPms.AIInspectionService.Domain.Interfaces.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string code, DateTime expiryTime);
    Task SendPasswordResetEmailAsync(string email, string token, DateTime expiryTime);
    Task SendEmailAsync(string toEmail, string subject, string body);
}
