using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UavPms.ApiGateway.Tests;

public sealed class GatewayRouteSecurityTests
{
    private const string ProductionEnvironment = "Production";
    private const string DevelopmentEnvironment = "Development";
    private const string TestBearerToken = "gate5-route-check";

    private static readonly string[] ClosedPaths =
    [
        "/ai-service",
        "/ai-service/",
        "/ai-service/health",
        "/ai-service/predict",
        "/hangfire",
        "/hangfire/",
        "/hangfire/jobs"
    ];

    private static readonly HttpMethod[] PreviouslyMappedMethods =
    [
        HttpMethod.Get,
        HttpMethod.Post,
        HttpMethod.Put,
        HttpMethod.Patch,
        HttpMethod.Delete,
        HttpMethod.Options
    ];

    public static TheoryData<string> GatewayEnvironments => new()
    {
        ProductionEnvironment,
        DevelopmentEnvironment
    };

    [Theory]
    [MemberData(nameof(GatewayEnvironments))]
    [Trait("Gate5", "GW")]
    public async Task Removed_public_routes_return_not_found_without_forwarding(string environment)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();

        foreach (var path in ClosedPaths)
        {
            foreach (var method in PreviouslyMappedMethods)
            {
                await AssertNotFoundAsync(client, method, path, includeAuthorization: false);
                await AssertNotFoundAsync(client, method, path, includeAuthorization: true);
            }
        }
    }

    [Theory]
    [MemberData(nameof(GatewayEnvironments))]
    [Trait("Gate5", "GW")]
    public void Required_public_routes_remain_in_loaded_configuration(string environment)
    {
        using var factory = CreateFactory(environment);
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var routes = configuration.GetSection("Routes").GetChildren().ToArray();

        Assert.Contains(routes, route =>
            route["UpstreamPathTemplate"] == "/api/v{version}/ai-analysis/{everything}" &&
            route["DownstreamScheme"] == "http");
        Assert.Contains(routes, route =>
            route["UpstreamPathTemplate"] == "/hubs/notifications/{everything}" &&
            route["DownstreamScheme"] == "ws" &&
            route.GetSection("UpstreamHttpMethod").Get<string[]>()!.Contains("POST"));
        Assert.Contains(routes, route =>
            route["UpstreamPathTemplate"] == "/hubs/notifications" &&
            route["DownstreamScheme"] == "ws");
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
        });
    }

    private static async Task AssertNotFoundAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        bool includeAuthorization)
    {
        using var request = new HttpRequestMessage(method, path);
        if (includeAuthorization)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestBearerToken);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
