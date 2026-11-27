using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.NotificationService.Tests.Features.Notifications;

public class NotificationOwnershipGate5Tests
{
    private const string Root = "/api/v1/notifications";
    private const string KeyHeader = "X-Internal-Key";
    private static readonly string[] ListRoutes = [Root, Root + "/history", Root + "/unread-count"];
    private static readonly HttpMethod[] ItemMethods = [HttpMethod.Get, HttpMethod.Put, HttpMethod.Delete];

    [Theory]
    [InlineData(UserRoles.Inspector, "sub")]
    [InlineData(UserRoles.Manager, "sub")]
    [InlineData(UserRoles.SystemAdmin, "sub")]
    [InlineData(UserRoles.AdminAndManager, "sub")]
    [InlineData(UserRoles.Inspector, "uid")]
    [InlineData(UserRoles.Manager, "uid")]
    [InlineData(UserRoles.SystemAdmin, "uid")]
    [InlineData(UserRoles.AdminAndManager, "uid")]
    [Trait("Gate5", "P0-4")]
    public async Task P04A_EnforcesOwnerForEveryRoute(string roles, string claimType)
    {
        using var app = new NotificationApp();
        using var client = app.CreateClient();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var token = app.SignToken(ownerId.ToString(), roles, claimType);
        var owned = await sender.Send(new CreateNotificationCommand(ownerId, "OwnershipCheck", null, null, "Owner A", "Ownership check"));
        var other = await sender.Send(new CreateNotificationCommand(otherId, "OwnershipCheck", null, null, "Owner B", "Ownership check"));
        await sender.Send(new CreateNotificationCommand(otherId, "OwnershipCheck", null, null, "Owner B", "Second unread"));
        var missingId = Guid.NewGuid();

        foreach (var route in ListRoutes)
        {
            foreach (var query in new[] { "", $"?userId={ownerId}" })
            {
                using var response = await SendAsync(client, HttpMethod.Get, route + query, token, HttpStatusCode.OK);
                using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var data = payload.RootElement.GetProperty("data");
                if (route.EndsWith("/unread-count", StringComparison.Ordinal))
                {
                    Assert.Equal(1, data.GetProperty("unreadCount").GetInt32());
                    continue;
                }
                Assert.Equal(1, data.GetProperty("totalCount").GetInt32());
                var item = Assert.Single(data.GetProperty("items").EnumerateArray());
                Assert.Equal(owned.Id, item.GetProperty("id").GetGuid());
                Assert.Equal(ownerId, item.GetProperty("userId").GetGuid());
            }

            using var forbidden = await SendAsync(client, HttpMethod.Get, $"{route}?userId={otherId}", token, HttpStatusCode.Forbidden);
            using var malformed = await SendAsync(client, HttpMethod.Get, route + "?userId=invalid", token, HttpStatusCode.BadRequest);
            using var empty = await SendAsync(client, HttpMethod.Get, route + "?userId=", token, HttpStatusCode.BadRequest);
        }

        await AssertUnauthorizedAsync(client, ownerId, owned.Id,
            [null, app.SignToken(null, roles), app.SignToken("invalid", roles)]);
        Assert.Null((await ReadAsync(db, owned.Id))?.ReadAt);
        foreach (var method in ItemMethods)
        {
            using var foreign = await SendAsync(client, method, ItemRoute(method, other.Id), token, HttpStatusCode.NotFound);
            using var missing = await SendAsync(client, method, ItemRoute(method, missingId), token, HttpStatusCode.NotFound);
            Assert.True(await foreign.Content.ReadAsStringAsync() == await missing.Content.ReadAsStringAsync(),
                "Foreign and missing notifications must return the same response.");
            var unchanged = await ReadAsync(db, other.Id);
            Assert.NotNull(unchanged);
            Assert.Equal(other.UserId, unchanged.UserId);
            Assert.Equal(other.ReadAt, unchanged.ReadAt);
            using var own = await SendAsync(client, method, ItemRoute(method, owned.Id), token, HttpStatusCode.OK);
            if (method == HttpMethod.Get)
            {
                using var payload = JsonDocument.Parse(await own.Content.ReadAsStringAsync());
                Assert.Equal(owned.Id, payload.RootElement.GetProperty("data").GetProperty("id").GetGuid());
            }
            if (method == HttpMethod.Put)
            {
                Assert.NotNull((await ReadAsync(db, owned.Id))?.ReadAt);
            }
        }
        Assert.Null(await ReadAsync(db, owned.Id));
    }

    [Fact]
    [Trait("Gate5", "P0-4")]
    public async Task P04B_PublicWriteEndpointsAreGone()
    {
        using var app = new NotificationApp();
        using var client = app.CreateClient();
        var admin = app.SignToken(Guid.NewGuid().ToString(), UserRoles.SystemAdmin);
        foreach (var path in new[] { Root, Root + "/enqueue-email", Root + "/schedule" })
        {
            foreach (var token in new[] { null, admin })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
                if (token != null)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                using var response = await client.SendAsync(request);
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Gate5", "P0-4")]
    public async Task P04B_InternalEndpointsRequireConfiguredKey(bool keyConfigured)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var app = new NotificationApp(keyConfigured ? key : "");
        using var client = app.CreateClient();
        var user = app.SignToken(Guid.NewGuid().ToString(), UserRoles.Inspector);
        var admin = app.SignToken(Guid.NewGuid().ToString(), UserRoles.SystemAdmin);
        var eventId = Guid.NewGuid();
        foreach (var route in new[] { "mission-event", "assessment-event" })
        {
            var path = $"{Root}/realtime/{route}";
            await AssertInternalStatusAsync(client, path, eventId, null, null, HttpStatusCode.Unauthorized);
            await AssertInternalStatusAsync(client, path, eventId, user, null, HttpStatusCode.Unauthorized);
            await AssertInternalStatusAsync(client, path, eventId, admin, null, HttpStatusCode.Unauthorized);
            await AssertInternalStatusAsync(client, path, eventId, admin, "wrong-key", HttpStatusCode.Unauthorized);
            await AssertInternalStatusAsync(client, path, eventId, null, "unrelated-test-key", HttpStatusCode.Unauthorized);
            var expected = keyConfigured ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
            await AssertInternalStatusAsync(client, path, eventId, null, key, expected);
        }

        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.DoesNotContain("/notifications/realtime", swagger, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertInternalStatusAsync(
        HttpClient client, string path, Guid eventId, string? token, string? key, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { missionId = eventId, assessmentId = eventId })
        };
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (key != null)
        {
            request.Headers.Add(KeyHeader, key);
        }
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task AssertUnauthorizedAsync(
        HttpClient client, Guid ownerId, Guid notificationId, string?[] tokens)
    {
        foreach (var token in tokens)
        {
            foreach (var route in ListRoutes)
            {
                using var response = await SendAsync(client, HttpMethod.Get, $"{route}?userId={ownerId}", token, HttpStatusCode.Unauthorized);
            }
            foreach (var method in ItemMethods)
            {
                using var response = await SendAsync(client, method, $"{ItemRoute(method, notificationId)}?userId={ownerId}", token, HttpStatusCode.Unauthorized);
            }
        }
    }

    private static string ItemRoute(HttpMethod method, Guid id) =>
        method == HttpMethod.Put ? $"{Root}/{id}/read" : $"{Root}/{id}";

    private static Task<Notification?> ReadAsync(ApplicationDbContext db, Guid id) =>
        db.Notifications.AsNoTracking().SingleOrDefaultAsync(notification => notification.Id == id);

    private sealed class NotificationApp(string sharedKey = "") : WebApplicationFactory<Program>
    {
        private const string Issuer = "notification-gate5";
        private const string Audience = "notification-gate5-client";
        private readonly string _signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Cấu hình host trước khi Program đọc connection string và đăng ký Hangfire.
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "",
                ["ConnectionStrings:HangfireConnection"] = "",
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:SecretKey"] = _signingKey,
                ["InternalServices:SharedKey"] = sharedKey,
                ["AIService:ServiceKey"] = "unrelated-test-key",
                ["SendGrid:ApiKey"] = "unused-by-http-tests",
                ["SendGrid:FromEmail"] = "gate5@example.test"
            }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }

        public string SignToken(string? userId, string roles, string claimType = "sub")
        {
            var claims = roles.Split(',').Select(role => new Claim(ClaimTypes.Role, role)).ToList();
            if (userId != null)
            {
                claims.Add(new Claim(claimType, userId));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
            var token = new JwtSecurityToken(Issuer, Audience, claims, expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string route, string? token, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(method, route);
        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        return response;
    }
}
