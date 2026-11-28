using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.API.Hubs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BE-REQ-101 Gate 5: chạy pipeline ASP.NET Core thật (Controller, Service, JWT Bearer thật) của OperationsService.
/// Chỉ thay lớp lưu trữ bằng EF Core InMemory (tên DB riêng cho từng factory) để test chạy được khi máy không có Docker.
/// Lấy kiểu public TelemetryHub của assembly API làm điểm vào vì <c>Program</c> là internal; factory chỉ dùng assembly của nó.
/// </summary>
public sealed class Be101ApiFactory : WebApplicationFactory<TelemetryHub>
{
    public const string JwtSecret = "be101-gate5-signing-key-0123456789-abcdef";
    public const string JwtIssuer = "UavPms";
    public const string JwtAudience = "UavPmsClient";

    private readonly string _databaseName = $"be101-gate5-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Mqtt:Enabled"] = "false",
                ["RunMigrations"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Gỡ toàn bộ cấu hình Npgsql của DbContext rồi đăng ký lại bằng InMemory.
            var dbContextDescriptors = services
                .Where(d => d.ServiceType == typeof(ApplicationDbContext) ||
                            (d.ServiceType.IsGenericType &&
                             d.ServiceType.GetGenericArguments().Contains(typeof(ApplicationDbContext))))
                .ToList();
            foreach (var descriptor in dbContextDescriptors)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

            // Job nền (MQTT, outbox, hết hạn assessment...) không thuộc phạm vi Gate 5 và làm host dừng khi DB là InMemory.
            services.RemoveAll<IHostedService>();
        });
    }
}

/// <summary>Dữ liệu nền + HTTP client có JWT thật cho từng test; mỗi world có DB InMemory riêng.</summary>
public sealed class Be101World : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Be101ApiFactory Factory { get; } = new();

    public sealed record RegionSeed(Guid RegionId, List<Guid> TowerIds);

    public HttpClient Client(Guid userId, params string[] roles)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userId, roles));
        return client;
    }

    public static string Token(Guid userId, params string[] roles)
    {
        var claims = new List<System.Security.Claims.Claim> { new("sub", userId.ToString()) };
        claims.AddRange(roles.Select(role => new System.Security.Claims.Claim("role", role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Be101ApiFactory.JwtSecret)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            Be101ApiFactory.JwtIssuer,
            Be101ApiFactory.JwtAudience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public Task WithDbAsync(Func<ApplicationDbContext, Task> action) =>
        WithDbAsync<int>(async db =>
        {
            await action(db);
            return 0;
        });

    public Task<RegionSeed> SeedRegionAsync() =>
        WithDbAsync(async db =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var region = new Region { Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
            var towers = Enumerable.Range(0, 2)
                .Select(i => new Tower
                {
                    RegionId = region.Id,
                    TowerCode = $"T-{tag}-{i}",
                    Geom = new Point(105.85, 21.03 + i * 0.001) { SRID = 4326 }
                })
                .ToList();
            db.Regions.Add(region);
            db.Towers.AddRange(towers);
            await db.SaveChangesAsync();
            return new RegionSeed(region.Id, towers.Select(t => t.Id).ToList());
        });

    /// <summary>Người dùng hoạt động trong DB (đủ role) và scope theo các vùng truyền vào; SystemAdmin thường không có scope.</summary>
    public Task<Guid> SeedUserAsync(string[] roles, params Guid[] regionIds) =>
        WithDbAsync(async db =>
        {
            var user = new User
            {
                FullName = $"{roles[0]} {Guid.NewGuid():N}"[..20],
                Email = $"{Guid.NewGuid():N}@gate5.test",
                Phone = string.Empty,
                Status = "Active",
                IsEmailVerified = true
            };
            db.Users.Add(user);
            foreach (var roleName in roles)
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName)
                    ?? db.Roles.Add(new Role { RoleName = roleName, Description = roleName }).Entity;
                await db.SaveChangesAsync();
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
            foreach (var regionId in regionIds)
                db.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Id, RegionId = regionId });
            await db.SaveChangesAsync();
            return user.Id;
        });

    /// <summary>
    /// Drone gắn trực tiếp vào vùng qua AssignedRegionId. <paramref name="health"/> = Healthy tạo thêm một phiếu Passed còn hạn
    /// (drone "Ready"); các trạng thái khác chỉ lưu health trên Uav (drone "MaintenanceRequired").
    /// </summary>
    public Task<Guid> SeedDroneAsync(Guid? regionId, TechnicalHealth health = TechnicalHealth.Unknown)
    {
        return WithDbAsync(async db =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var drone = new Uav
            {
                UavCode = $"UAV-{tag}",
                Name = $"Drone {tag}",
                Model = "M300",
                SerialNumber = $"SN-{tag}",
                AssignedRegionId = regionId,
                OperationalStatus = DroneOperationalStatus.Available,
                TechnicalHealth = health,
                BatteryLevel = 90,
                FlightEnduranceMinutes = 40,
                OperationalRangeKm = 10
            };
            db.Uavs.Add(drone);

            if (health != TechnicalHealth.Unknown)
            {
                var inspection = new DroneTechnicalInspection
                {
                    DroneId = drone.Id,
                    Status = health == TechnicalHealth.Critical
                        ? DroneTechnicalInspectionStatus.Failed
                        : DroneTechnicalInspectionStatus.Passed,
                    Health = health,
                    SourceType = "manual",
                    StartedAt = DateTime.UtcNow.AddHours(-2),
                    CompletedAt = DateTime.UtcNow.AddHours(-2),
                    ValidUntil = DateTime.UtcNow.AddDays(6),
                    PolicyVersion = "v2.0"
                };
                db.DroneTechnicalInspections.Add(inspection);
                drone.LastTechnicalInspectionId = inspection.Id;
            }

            await db.SaveChangesAsync();
            return drone.Id;
        });
    }

    public Task SeedInspectionAsync(Guid droneId, DroneTechnicalInspectionStatus status, TechnicalHealth health, DateTime completedAtUtc) =>
        WithDbAsync(async db =>
        {
            db.DroneTechnicalInspections.Add(new DroneTechnicalInspection
            {
                DroneId = droneId,
                Status = status,
                Health = health,
                SourceType = "manual",
                StartedAt = completedAtUtc,
                CompletedAt = completedAtUtc,
                ValidUntil = completedAtUtc.AddDays(7),
                PolicyVersion = "v2.0"
            });
            await db.SaveChangesAsync();
        });

    public Task SeedFlightControllerBindingAsync(Guid droneId, string uid, DateTime pairedAtUtc, bool active) =>
        WithDbAsync(async db =>
        {
            db.DroneFlightControllers.Add(new DroneFlightController
            {
                DroneId = droneId,
                FlightControllerUid = uid,
                PairedAt = pairedAtUtc,
                UnpairedAt = active ? null : pairedAtUtc.AddHours(1)
            });
            await db.SaveChangesAsync();
        });

    public Task SetSettingAsync(string key, string value) =>
        WithDbAsync(async db =>
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, Category = "Assessment", DataType = "Int" });
            await db.SaveChangesAsync();
        });

    /// <summary>Payload kiểm tra do Technician nhập tay: pin + GPS có số đo thật, kèm ghi chú.</summary>
    public static object[] ManualMetrics(decimal batteryPercent = 88m, bool batteryPassed = true) => new object[]
    {
        new { metricCode = "BATTERY", subsystem = "battery", numericValue = batteryPercent, unit = "%", passed = batteryPassed, isRequired = true, critical = false, severity = "Low" },
        new { metricCode = "GPS", subsystem = "navigation", boolValue = true, passed = true, isRequired = true, critical = false, severity = "Low" }
    };

    public static object ManualInspection(string conclusion = "Passed", string sourceType = "manual", string? notes = "Đo tại hiện trường", string? flightControllerUid = null, decimal batteryPercent = 88m, bool batteryPassed = true) => new
    {
        sourceType,
        conclusion,
        notes,
        flightControllerUid,
        metrics = ManualMetrics(batteryPercent, batteryPassed)
    };

    public static object Mf01Payload(
        RegionSeed region,
        int? inspectorCount = 1,
        int? analystCount = 1,
        int? technicianCount = 1,
        IEnumerable<Guid>? droneIds = null,
        string? idempotencyKey = null) => new Dictionary<string, object?>
        {
            ["regionId"] = region.RegionId,
            ["plannedStart"] = DateTime.UtcNow.AddDays(1),
            ["plannedEnd"] = DateTime.UtcNow.AddDays(1).AddHours(4),
            ["towerIds"] = region.TowerIds,
            ["inspectorCount"] = inspectorCount,
            ["analystCount"] = analystCount,
            ["technicianCount"] = technicianCount,
            ["droneIds"] = droneIds?.ToArray(),
            ["idempotencyKey"] = idempotencyKey
        };

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    public static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object? body) =>
        client.PostAsJsonAsync(url, body ?? new { }, Json);

    public void Dispose() => Factory.Dispose();
}
