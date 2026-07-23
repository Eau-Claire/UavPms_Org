using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.WriteTo.Console();
});

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

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
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseHealthChecks("/health");
app.UseWebSockets();
app.UseCors("GatewayCors");

await app.UseOcelot();

app.Run();
