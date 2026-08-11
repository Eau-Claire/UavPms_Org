using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using UavPms.NotificationService.Application.Common.Options;
using UavPms.NotificationService.Domain.Interfaces.Services;

namespace UavPms.NotificationService.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly SendGridOptions _options;
    public EmailService(IOptions<SendGridOptions> options)
    {
        _options = options.Value;
    }
    private ISendGridClient CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("SendGrid:ApiKey is not configured.");
        }
        return new SendGridClient(_options.ApiKey);
    }

    public async Task SendOtpEmailAsync(string email, string code, DateTime expiryTime)
    {
        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(email);
        var subject = "Your OTP Verification Code";
        var plainTextContent = $"Your OTP code is: {code}. It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).";
        var htmlContent = $"<p>Your OTP code is: <strong>{code}</strong></p><p>It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).</p>";
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await CreateClient().SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode}");
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string token, DateTime expiryTime)
    {
        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(email);
        var subject = "Password Reset Request";
        var plainTextContent = $"You requested a password reset. Please use the following token to call the reset password API:\n\n{token}\n\nThis token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.";
        var htmlContent = $"<p>You requested a password reset.</p><p>Please use the following token to call the reset password API:</p><p><strong>{token}</strong></p><p>This token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.</p>";
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await CreateClient().SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode}");
        }
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, body, body);
        var response = await CreateClient().SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode} for sending email to {toEmail}");
        }
    }
}
