using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Infrastructure.Services;

public class EmailService : IEmailService
{
    private const string SendEmailPath = "v3/smtp/email";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public EmailService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Brevo:ApiKey"];
        _fromEmail = configuration["Brevo:FromEmail"] ?? string.Empty;
        _fromName = configuration["Brevo:FromName"] ?? "UavPms System";
    }

    public Task SendOtpEmailAsync(string email, string code, DateTime expiryTime)
    {
        var encodedCode = WebUtility.HtmlEncode(code);
        var subject = "Your OTP Verification Code";
        var plainTextContent = $"Your OTP code is: {code}. It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).";
        var htmlContent = $"<p>Your OTP code is: <strong>{encodedCode}</strong></p><p>It will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC (within 3 minutes).</p>";

        return SendTransactionalEmailAsync(email, subject, plainTextContent, htmlContent);
    }

    public Task SendPasswordResetEmailAsync(string email, string token, DateTime expiryTime)
    {
        var encodedToken = WebUtility.HtmlEncode(token);
        var subject = "Password Reset Request";
        var plainTextContent = $"You requested a password reset. Please use the following token to call the reset password API:\n\n{token}\n\nThis token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.";
        var htmlContent = $"<p>You requested a password reset.</p><p>Please use the following token to call the reset password API:</p><p><strong>{encodedToken}</strong></p><p>This token will expire at {expiryTime:yyyy-MM-dd HH:mm:ss} UTC.</p>";

        return SendTransactionalEmailAsync(email, subject, plainTextContent, htmlContent);
    }

    public Task SendEmailAsync(string toEmail, string subject, string body)
    {
        return SendTransactionalEmailAsync(toEmail, subject, body, body);
    }

    private async Task SendTransactionalEmailAsync(
        string toEmail,
        string subject,
        string textContent,
        string htmlContent)
    {
        ValidateConfiguration();

        var payload = new
        {
            sender = new { email = _fromEmail, name = _fromName },
            to = new[] { new { email = toEmail } },
            subject,
            textContent,
            htmlContent
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEmailPath)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", _apiKey);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Brevo returned status code {(int)response.StatusCode} ({response.StatusCode}): {responseBody}",
            inner: null,
            response.StatusCode);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Brevo:ApiKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_fromEmail))
        {
            throw new InvalidOperationException("Brevo:FromEmail is not configured.");
        }
    }
}
