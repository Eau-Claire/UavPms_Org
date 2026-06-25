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
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using UavPms.WebApi.Swagger;
using Microsoft.Extensions.Options; 
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
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
}

// Hangfire - Background Job Processing
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")),
        new PostgreSqlStorageOptions
        {
            PrepareSchemaIfNecessary = true
        });
});

builder.Services.AddHangfireServer();

// CẤU HÌNH CORS POLICY 
builder.Services.AddCors(options =>
{
    options.AddPolicy($"AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://seppms.vercel.app") // Chỉ cho phép URL của Frontend truy cập
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

// Tự động chạy Migration và Seed dữ liệu khi khởi động ứng dụng
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
    await DatabaseSeeder.SeedAsync(dbContext);
}

// Cấu hình Hangfire Dashboard và Custom Pages cho tất cả môi trường
UavPms.WebApi.HangfireExtensions.HangfireDashboardCustomizer.ConfigureCustomPages();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new UavPms.WebApi.HangfireExtensions.AllowAllDashboardAuthorizationFilter() }
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
app.Run();  