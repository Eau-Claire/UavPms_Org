using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var useLocalDownstreams = builder.Configuration.GetValue<bool>("Gateway:UseLocalDownstreams");

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.WriteTo.Console();
});

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

if (builder.Environment.IsDevelopment() && (!isRunningInContainer || useLocalDownstreams))
{
    builder.Configuration.AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCors", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? new[]
            {
                "http://localhost:3000",
                "http://localhost:5173",
                "https://seppms.vercel.app",
                "https://uavpms.ddns.net"
            };

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("gateway", new OpenApiInfo
    {
        Title = "UAV PMS API Gateway",
        Version = "gateway",
        Description = "Unified Swagger entrypoint for the UAV PMS distributed services."
    });
});
builder.Services.AddHttpClient();
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseHealthChecks("/health");
app.UseWebSockets();
app.UseCors("GatewayCors");

// Swagger is part of the deployed API contract and must also be available
// when the gateway runs in the production Docker profile.
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/swagger/index.html");
            return;
        }

        await next();
    });

    var swaggerTargets = isRunningInContainer && !useLocalDownstreams
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["identity"] = builder.Configuration["SwaggerServices:IdentityUrl"] ?? "http://identityservice:8080/swagger/v1/swagger.json",
            ["operations"] = builder.Configuration["SwaggerServices:OperationsUrl"] ?? "http://operationsservice:8080/swagger/v1/swagger.json",
            ["ai-inspection"] = builder.Configuration["SwaggerServices:AIInspectionUrl"] ?? "http://aiinspectionservice:8080/swagger/v1/swagger.json",
            ["notifications"] = builder.Configuration["SwaggerServices:NotificationsUrl"] ?? "http://notificationservice:8080/swagger/v1/swagger.json"
        }
        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
            ["identity"] = builder.Configuration["SwaggerServices:IdentityUrl"] ?? "http://localhost:5101/swagger/v1/swagger.json",
            ["operations"] = builder.Configuration["SwaggerServices:OperationsUrl"] ?? "http://localhost:5102/swagger/v1/swagger.json",
            ["ai-inspection"] = builder.Configuration["SwaggerServices:AIInspectionUrl"] ?? "http://localhost:5103/swagger/v1/swagger.json",
            ["notifications"] = builder.Configuration["SwaggerServices:NotificationsUrl"] ?? "http://localhost:5104/swagger/v1/swagger.json"
        };

    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/swagger/services", out var remainingPath))
        {
            await next();
            return;
        }

        var segments = remainingPath.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            ?? Array.Empty<string>();

        if (segments.Length != 3 || segments[1] != "v1" || segments[2] != "swagger.json")
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var service = segments[0];
        if (!swaggerTargets.TryGetValue(service, out var targetUrl))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { message = $"Unknown Swagger service '{service}'." });
            return;
        }

        var httpClientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        using var response = await httpClientFactory.CreateClient().GetAsync(targetUrl, context.RequestAborted);
        var content = await response.Content.ReadAsStringAsync(context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await context.Response.WriteAsync(content, context.RequestAborted);
    });

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "UAV PMS APIs";
        options.SwaggerEndpoint("/swagger/gateway/swagger.json", "API Gateway");
        options.SwaggerEndpoint("/swagger/services/identity/v1/swagger.json", "Identity Service");
        options.SwaggerEndpoint("/swagger/services/operations/v1/swagger.json", "Operations Service");
        options.SwaggerEndpoint("/swagger/services/ai-inspection/v1/swagger.json", "AI Inspection Service");
        options.SwaggerEndpoint("/swagger/services/notifications/v1/swagger.json", "Notification Service");
    });
}

await app.UseOcelot();

app.Run();
