using System.Data.Common;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using UavPms.NotificationService.Application;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure;
using UavPms.NotificationService.Infrastructure.Messaging;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.NotificationService.Api.Hubs;
using UavPms.NotificationService.Api.Jobs;
using UavPms.NotificationService.Api.Middlewares;
using UavPms.NotificationService.Api.Services;
using UavPms.NotificationService.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
});

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.WriteTo.Console());

builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;
})
.AddXmlSerializerFormatters()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationConnectionRegistry, NotificationConnectionRegistry>();
builder.Services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();

builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ??
                        throw new InvalidOperationException("Jwt:SecretKey is not configured in appsettings.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

if (!string.IsNullOrEmpty(builder.Configuration["RabbitMQ:HostName"]))
{
    builder.Services.AddHostedService<MissionCreatedConsumer>();
    builder.Services.AddHostedService<DefectDetectedConsumer>();
}

var normalizedHangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection");
if (string.IsNullOrWhiteSpace(normalizedHangfireConnection))
{
    normalizedHangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
}

normalizedHangfireConnection = NormalizeHangfireConnectionString(
    normalizedHangfireConnection,
    builder.Configuration.GetValue<int?>("Hangfire:MinimumPoolSize") ?? 5);

if (!string.IsNullOrWhiteSpace(normalizedHangfireConnection))
{
    builder.Services.AddHangfire(config =>
    {
        config.UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(normalizedHangfireConnection),
            new PostgreSqlStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15)
            });
    });

    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 1;
        options.SchedulePollingInterval = TimeSpan.FromSeconds(30);
    });
}
else
{
    Log.Warning("Hangfire is disabled because neither HangfireConnection nor DefaultConnection is configured.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:5194", "https://seppms.vercel.app", "https://uavpms.ddns.net" };

        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Configuration.GetValue<bool>("RunMigrations"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    await DatabaseSeeder.SeedAsync(dbContext);
}
else
{
    Log.Information("Database migration and seeding skipped. Set RunMigrations=true to enable it for a dedicated migration run.");
}

if (!string.IsNullOrWhiteSpace(normalizedHangfireConnection))
{
    UavPms.NotificationService.Api.HangfireExtensions.HangfireDashboardCustomizer.ConfigureCustomPages();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new UavPms.NotificationService.Api.HangfireExtensions.HangfireBasicAuthorizationFilter() }
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

if (!string.IsNullOrWhiteSpace(normalizedHangfireConnection))
{
    RecurringJob.AddOrUpdate<CleanupJob>("auto-cleanup-job", job => job.Execute(), Cron.Weekly);
    RecurringJob.AddOrUpdate<DailySummaryJob>("daily-summary-job", job => job.Execute(), Cron.Daily);
    RecurringJob.AddOrUpdate<PushNotificationsJob>("push-notifications-sync", job => job.Execute(), Cron.Minutely);
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { service = "notification", status = "healthy" }));
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications").RequireAuthorization();
app.Run();

static string? NormalizeHangfireConnectionString(string? connectionString, int minimumPoolSize)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    var builder = new DbConnectionStringBuilder
    {
        ConnectionString = connectionString
    };

    var configuredPoolSize = TryGetIntConnectionValue(builder, "Maximum Pool Size");
    if (configuredPoolSize == null || configuredPoolSize < minimumPoolSize)
    {
        builder["Maximum Pool Size"] = minimumPoolSize;
    }

    return builder.ConnectionString;
}

static int? TryGetIntConnectionValue(DbConnectionStringBuilder builder, string key)
{
    foreach (string existingKey in builder.Keys)
    {
        if (!string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (builder[existingKey] is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(builder[existingKey]?.ToString(), out var parsedValue))
        {
            return parsedValue;
        }
    }

    return null;
}
