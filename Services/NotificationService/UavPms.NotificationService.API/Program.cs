using System.Data.Common;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using UavPms.NotificationService.Application;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Infrastructure;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.NotificationService.Infrastructure.Messaging; 
using UavPms.NotificationService.API.Hubs;
using UavPms.NotificationService.API.Jobs;
using UavPms.NotificationService.API.Middlewares;
using UavPms.NotificationService.API.Services;
using UavPms.NotificationService.API.Swagger;

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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

if (!string.IsNullOrEmpty(builder.Configuration["RabbitMQ:HostName"]))
{
    builder.Services.AddHostedService<MissionCreatedConsumer>();
    builder.Services.AddHostedService<DefectDetectedConsumer>();
    builder.Services.AddHostedService<NotificationPushConsumer>();
    builder.Services.AddHostedService<AIAnalysisStatusChangedConsumer>();
}

// Register Hangfire background jobs in DI so Hangfire JobActivator can resolve them
builder.Services.AddTransient<CleanupJob>();
builder.Services.AddTransient<DailySummaryJob>();
builder.Services.AddTransient<PushNotificationsJob>();
builder.Services.AddTransient<ScheduledNotificationJob>();

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
    Log.Warning("RunMigrations is ignored by NotificationService. OperationsService is the only migration owner for the shared database.");
}

if (!string.IsNullOrWhiteSpace(normalizedHangfireConnection))
{
    UavPms.NotificationService.API.HangfireExtensions.HangfireDashboardCustomizer.ConfigureCustomPages();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new UavPms.NotificationService.API.HangfireExtensions.HangfireBasicAuthorizationFilter() }
    });
}

// Keep service-level OpenAPI available behind the gateway in Docker as well.
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
    try
    {
        RecurringJob.AddOrUpdate<CleanupJob>("auto-cleanup-job", job => job.Execute(), Cron.Weekly);
        RecurringJob.AddOrUpdate<DailySummaryJob>("daily-summary-job", job => job.Execute(), Cron.Daily);
        RecurringJob.AddOrUpdate<PushNotificationsJob>("push-notifications-sync", job => job.Execute(), Cron.Minutely);
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception,
            "Hangfire recurring-job registration failed. The API will remain available while Hangfire retries storage access.");
    }
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
