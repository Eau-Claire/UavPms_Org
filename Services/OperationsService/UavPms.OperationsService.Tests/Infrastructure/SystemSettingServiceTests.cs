using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Infrastructure;

public class SystemSettingServiceTests
{
    private static ApplicationDbContext CreateContext()
    {
        var userMock = new Mock<ICurrentUserServices>();
        userMock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, userMock.Object);
    }

    [Fact]
    public async Task GetValues_WhenKeyDoesNotExist_ShouldReturnDefaultValue()
    {
        // Arrange
        await using var db = CreateContext();
        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act
        var stringVal = await service.GetStringAsync("NON_EXISTENT_STR", "default_str");
        var intVal = await service.GetIntAsync("NON_EXISTENT_INT", 42);
        var doubleVal = await service.GetDoubleAsync("NON_EXISTENT_DBL", 3.14);
        var boolVal = await service.GetBoolAsync("NON_EXISTENT_BOOL", true);

        // Assert
        stringVal.Should().Be("default_str");
        intVal.Should().Be(42);
        doubleVal.Should().Be(3.14);
        boolVal.Should().BeTrue();
    }

    [Fact]
    public async Task GetValues_WhenKeyExistsInDb_ShouldReturnParsedDbValue()
    {
        // Arrange
        await using var db = CreateContext();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "TEST_STR", Value = "configured_value", Category = "TEST" },
            new SystemSetting { Key = "TEST_INT", Value = "120", Category = "TEST" },
            new SystemSetting { Key = "TEST_DBL", Value = "99.5", Category = "TEST" },
            new SystemSetting { Key = "TEST_BOOL", Value = "false", Category = "TEST" }
        );
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act & Assert
        (await service.GetStringAsync("TEST_STR", "def")).Should().Be("configured_value");
        (await service.GetIntAsync("TEST_INT", 0)).Should().Be(120);
        (await service.GetDoubleAsync("TEST_DBL", 0.0)).Should().Be(99.5);
        (await service.GetBoolAsync("TEST_BOOL", true)).Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateCache_ShouldForceReloadFromDatabase()
    {
        // Arrange
        await using var db = CreateContext();
        var setting = new SystemSetting { Key = "CACHE_KEY", Value = "initial_val", Category = "TEST" };
        db.SystemSettings.Add(setting);
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // First call populates cache
        var val1 = await service.GetStringAsync("CACHE_KEY", "def");
        val1.Should().Be("initial_val");

        // Directly modify in DB behind the scenes
        setting.Value = "modified_val";
        await db.SaveChangesAsync();

        // Second call without invalidation still reads cache
        var cachedVal = await service.GetStringAsync("CACHE_KEY", "def");
        cachedVal.Should().Be("initial_val");

        // Invalidate cache
        service.InvalidateCache();

        // Third call reads new DB value
        var refreshedVal = await service.GetStringAsync("CACHE_KEY", "def");
        refreshedVal.Should().Be("modified_val");
    }

    [Fact]
    public async Task UpdateAsync_WhenSettingExists_ShouldUpdateDbAndInvalidateCache()
    {
        // Arrange
        await using var db = CreateContext();
        db.SystemSettings.Add(new SystemSetting { Key = SettingKeys.DroneCriticalBatteryThreshold, Value = "30", Category = "DRONE" });
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act
        var result = await service.UpdateAsync(SettingKeys.DroneCriticalBatteryThreshold, "25");

        // Assert
        result.Value.Should().Be("25");

        var updatedSetting = await db.SystemSettings.FirstAsync(s => s.Key == SettingKeys.DroneCriticalBatteryThreshold);
        updatedSetting.Value.Should().Be("25");

        // Memory cache should immediately reflect updated value
        var cachedValue = await service.GetDoubleAsync(SettingKeys.DroneCriticalBatteryThreshold, 30.0);
        cachedValue.Should().Be(25.0);
    }

    [Fact]
    public async Task GetDroneThresholdSettingsAsync_ShouldReturnStronglyTypedDto()
    {
        // Arrange
        await using var db = CreateContext();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = SettingKeys.DroneCriticalBatteryThreshold, Value = "35", Category = "DRONE" },
            new SystemSetting { Key = SettingKeys.DroneWarningBatteryThreshold, Value = "55", Category = "DRONE" },
            new SystemSetting { Key = SettingKeys.DroneMinRfLinkThreshold, Value = "60", Category = "DRONE" },
            new SystemSetting { Key = SettingKeys.DroneInspectionValidityDays, Value = "14", Category = "DRONE" }
        );
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act
        var dto = await service.GetDroneThresholdSettingsAsync();

        // Assert
        dto.CriticalBatteryThreshold.Should().Be(35.0);
        dto.WarningBatteryThreshold.Should().Be(55.0);
        dto.MinRfLinkThreshold.Should().Be(60.0);
        dto.InspectionValidityDays.Should().Be(14);
    }

    [Fact]
    public async Task GetAiEvaluationSettingsAsync_ShouldReturnDynamicAiThresholds()
    {
        // Arrange
        await using var db = CreateContext();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = SettingKeys.AiEmergencyConfidenceThreshold, Value = "0.85", Category = "AI" },
            new SystemSetting { Key = SettingKeys.AiHighScoreThreshold, Value = "80", Category = "AI" }
        );
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act
        var dto = await service.GetAiEvaluationSettingsAsync();

        // Assert
        dto.EmergencyConfidenceThreshold.Should().Be(0.85);
        dto.HighScoreThreshold.Should().Be(80);
        dto.CriticalScoreThreshold.Should().Be(90); // Default fallback
    }

    [Fact]
    public async Task GetAllAsync_WithCategoryFilter_ShouldReturnOnlyMatchingSettings()
    {
        // Arrange
        await using var db = CreateContext();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "OTP_1", Value = "5", Category = "OTP" },
            new SystemSetting { Key = "OTP_2", Value = "60", Category = "OTP" },
            new SystemSetting { Key = "AUTH_1", Value = "7", Category = "AUTH" }
        );
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SystemSettingService>>();
        var service = new SystemSettingService(db, loggerMock.Object);

        // Act
        var otpSettings = await service.GetAllAsync("OTP");
        var allSettings = await service.GetAllAsync();

        // Assert
        otpSettings.Should().HaveCount(2);
        otpSettings.All(s => s.Category == "OTP").Should().BeTrue();
        allSettings.Should().HaveCount(3);
    }
}
