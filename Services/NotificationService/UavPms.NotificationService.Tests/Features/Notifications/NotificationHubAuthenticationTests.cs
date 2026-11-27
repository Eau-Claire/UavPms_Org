using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.API.Services;

namespace UavPms.NotificationService.Tests.Features.Notifications;

public class NotificationHubAuthenticationTests
{
    private const string Issuer = "notification-tests", Audience = "notification-clients";
    private const string Secret = "notification-gate5-secret-key-32-bytes";
    [Fact]
    [Trait("Gate5", "P0-3")]
    public async Task Negotiate_WithoutValidJwt_ShouldReturnUnauthorized()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        var missingTokenResponse = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, missingTokenResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateExpiredToken());
        var expiredTokenResponse = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredTokenResponse.StatusCode);
    }
    private static TestServer CreateServer()
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<INotificationConnectionRegistry, NotificationConnectionRegistry>();
                services.AddSignalR();
                services.AddAuthorization();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                    options.TokenValidationParameters = CreateTokenValidationParameters());
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization());
            }));
    }
    private static string CreateExpiredToken()
    {
        var credentials = new SigningCredentials(CreateTokenValidationParameters().IssuerSigningKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience, expires: DateTime.UtcNow.AddMinutes(-1), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private static TokenValidationParameters CreateTokenValidationParameters() => new()
    {
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = Issuer, ValidAudience = Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), ClockSkew = TimeSpan.Zero
    };
}
