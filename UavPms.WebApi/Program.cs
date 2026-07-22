using Microsoft.Extensions.FileProviders;
using Serilog;
using UavPms.Infrastructure;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Repositories;
using UavPms.Infrastructure.Messaging;
using UavPms.WebApi.Jobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using UavPms.Infrastructure.Persistence;
using UavPms.Application;
using UavPms.WebApi.Middlewares;
using UavPms.WebApi.Hubs;
using UavPms.WebApi.Services;
using UavPms.Core.Interfaces.Services;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using UavPms.WebApi.Swagger;
using Microsoft.Extensions.Options; 
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
});

// Cấu hình Serilog in ra Console   
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.WriteTo.Console(); // Viết log ra màn hình Terminal
});

// ĐĂNG KÝ SERVICES VÀO DI CONTAINER
// ĐĂNG KÝ CONTROLLER
builder.Services.AddControllers(options =>
{   
    options.RespectBrowserAcceptHeader = true; // Tôn trọng header Accept từ client gửi lên
    options.ReturnHttpNotAcceptable = true; // Trả về lỗi 406 Not Acceptable nếu định dạng yêu cầu không hỗ trợ
})
.AddXmlSerializerFormatters() // Thêm định dạng chuyển đổi dữ liệu định dạng XML
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationConnectionRegistry, NotificationConnectionRegistry>();
builder.Services.AddScoped<IRealtimeNotificationService, RealtimeNotificationService>();

// Cấu hình API Versioning 
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

// đăng ký cầu hình xắc thực JWT Bearer
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

// Đăng ký dịch vụ cấu hình Swagger tự động theo phiên bản
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

//Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// RabbitMQ Consumers (chỉ khởi động khi có cấu hình RabbitMQ)
if (!string.IsNullOrEmpty(builder.Configuration["RabbitMQ:HostName"]))
{
    builder.Services.AddHostedService<MissionCreatedConsumer>();
    builder.Services.AddHostedService<DefectDetectedConsumer>();

    if (builder.Configuration.GetValue<bool>("MockAI:Enabled"))
    {
        builder.Services.AddHostedService<MockAIAnalysisConsumer>();
    }
}

// Hangfire - Background Job Processing
builder.Services.AddHangfire(config =>
{
    var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection");
    if (string.IsNullOrWhiteSpace(hangfireConnection))
    {
        hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    }

    hangfireConnection = NormalizeHangfireConnectionString(
        hangfireConnection,
        builder.Configuration.GetValue<int?>("Hangfire:MinimumPoolSize") ?? 5);

    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(hangfireConnection),
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

// CẤU HÌNH CORS POLICY 
builder.Services.AddCors(options =>
{
    options.AddPolicy($"AllowFrontend", policy =>
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

        policy.WithOrigins(allowedOrigins) // Chỉ cho phép URL của Frontend truy cập
            .AllowAnyMethod()                       // Cho phép mọi method  
            .AllowAnyHeader()                       // Cho phép mọi header  
            .AllowCredentials();                    // Rất quan trọng: Bắt buộc phải có để chạy SignalR sau này
    });
});

// XÂY DỰNG ỨNG DỤNG VÀ CẤU HÌNH MIDDLEWARE PIPELINE    
var app = builder.Build();

app.UseForwardedHeaders();

// Global Exception Handler
app.UseExceptionHandler();

// Run database migrations/seeding only when explicitly enabled.
// Production web containers should not migrate on every restart because this can exhaust hosted DB pooler sessions.
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

// Cấu hình Hangfire Dashboard và Custom Pages cho tất cả môi trường
UavPms.WebApi.HangfireExtensions.HangfireDashboardCustomizer.ConfigureCustomPages();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new UavPms.WebApi.HangfireExtensions.HangfireBasicAuthorizationFilter() }
});

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

// Đăng ký các Hangfire Recurring Jobs
RecurringJob.AddOrUpdate<CleanupJob>(
    "auto-cleanup-job",
    job => job.Execute(),
    Cron.Weekly);

RecurringJob.AddOrUpdate<DailySummaryJob>(
    "daily-summary-job",
    job => job.Execute(),
    Cron.Daily);

RecurringJob.AddOrUpdate<PushNotificationsJob>(
    "push-notifications-sync",
    job => job.Execute(),
    Cron.Minutely);

app.UseHttpsRedirection();

// Kích hoạt Middleware CORS (Bắt buộc phải đặt trước authorization)
app.UseCors("AllowFrontend");

// Cấu hình Middleware để phục vụ file tĩnh (Ảnh bằng chứng)
var rawPath = builder.Configuration["FileStorage:AlertImagesPath"] 
    ?? "uav_storage/images";

var imagePath = Path.IsPathRooted(rawPath)
    ? rawPath
    : Path.Combine(builder.Environment.ContentRootPath, rawPath);

try
{
    if (!Directory.Exists(imagePath))
    {
        Directory.CreateDirectory(imagePath);
    }
}
catch (Exception)
{
    // Fallback về thư mục cục bộ của ứng dụng nếu không có quyền truy cập
    imagePath = Path.Combine(builder.Environment.ContentRootPath, "uav_storage", "images");
    if (!Directory.Exists(imagePath))
    {
        Directory.CreateDirectory(imagePath);
    }
}

// Cấu hình map thư mục vật lý ra đường dẫn web
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagePath),
    RequestPath = "/images"
});

app.UseAuthentication();
app.UseAuthorization();
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
