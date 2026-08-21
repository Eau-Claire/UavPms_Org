using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UavPms.IdentityService.Infrastructure.Services;

namespace UavPms.IdentityService.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public async Task SendOtpEmailAsync_ShouldCallBrevoTransactionalEndpoint()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{\"messageId\":\"test-id\"}");
        var service = CreateService(handler);

        await service.SendOtpEmailAsync("pilot@example.com", "123456", DateTime.UtcNow.AddMinutes(3));

        handler.Method.Should().Be(HttpMethod.Post);
        handler.RequestUri.Should().Be(new Uri("https://api.brevo.com/v3/smtp/email"));
        handler.ApiKey.Should().Be("test-api-key");
        handler.Body.Should().Contain("pilot@example.com");
        handler.Body.Should().Contain("123456");
        handler.Body.Should().Contain("verified@example.com");
    }

    [Fact]
    public async Task SendEmailAsync_ShouldExposeBrevoErrorDetails()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, "{\"message\":\"Key not found\"}");
        var service = CreateService(handler);

        var action = () => service.SendEmailAsync("pilot@example.com", "Subject", "Body");

        var exception = await action.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        exception.Which.Message.Should().Contain("Key not found");
    }

    [Fact]
    public async Task SendEmailAsync_ShouldRejectMissingApiKeyBeforeSending()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created, "{}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brevo:FromEmail"] = "verified@example.com"
            })
            .Build();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var service = new EmailService(httpClient, configuration);

        var action = () => service.SendEmailAsync("pilot@example.com", "Subject", "Body");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Brevo:ApiKey is not configured.");
        handler.RequestCount.Should().Be(0);
    }

    private static EmailService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Brevo:ApiKey"] = "test-api-key",
                ["Brevo:FromEmail"] = "verified@example.com",
                ["Brevo:FromName"] = "UavPms Tests"
            })
            .Build();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };

        return new EmailService(httpClient, configuration);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public int RequestCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
