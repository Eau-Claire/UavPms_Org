using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(IConfiguration configuration)
    {
        var apiKey = configuration["SendGrid:ApiKey"] ?? string.Empty;
        _client = new SendGridClient(apiKey);
        _fromEmail = configuration["SendGrid:FromEmail"] ?? "no-reply@uavpms.com";
        _fromName = configuration["SendGrid:FromName"] ?? "UavPms System";
    }

    public async Task SendOtpEmailAsync(string email, string code, DateTime expiryTime)
    {
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(email);
        var subject = "Your OTP Verification Code";
        var plainTextContent = $"Your OTP code is: {code}. It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).";
        var htmlContent = $"<p>Your OTP code is: <strong>{code}</strong></p><p>It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).</p>";
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await _client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode}");
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string token, DateTime expiryTime)
    {
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(email);
        var subject = "Password Reset Request";
        var plainTextContent = $"You requested a password reset. Please use the following token to call the reset password API:\n\n{token}\n\nThis token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.";
        var htmlContent = $"<p>You requested a password reset.</p><p>Please use the following token to call the reset password API:</p><p><strong>{token}</strong></p><p>This token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.</p>";
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await _client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode}");
        }
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, body, body);
        var response = await _client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"SendGrid returned status code {response.StatusCode} for sending email to {toEmail}");
        }
    }
}
