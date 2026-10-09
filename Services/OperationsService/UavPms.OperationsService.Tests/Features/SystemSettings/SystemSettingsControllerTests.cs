using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.Shared.Contracts.Interfaces;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.Tests.Features.SystemSettings;

public class SystemSettingsControllerTests
{
    private readonly Mock<ISystemSettingService> _settingServiceMock = new();
    private readonly SystemSettingsController _controller;

    public SystemSettingsControllerTests()
    {
        _controller = new SystemSettingsController(_settingServiceMock.Object);
    }

    [Fact]
    public async Task GetAllSettings_ShouldReturnOkWithSettingsList()
    {
        // Arrange
        var mockSettings = new List<SystemSettingDto>
        {
            new(Guid.NewGuid(), "KEY_1", "VAL_1", "Desc 1", "CAT", "string", DateTime.UtcNow),
            new(Guid.NewGuid(), "KEY_2", "VAL_2", "Desc 2", "CAT", "string", DateTime.UtcNow)
        };
        _settingServiceMock.Setup(s => s.GetAllAsync("CAT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSettings);

        // Act
        var result = await _controller.GetAllSettings("CAT", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().BeEquivalentTo(mockSettings);
    }

    [Fact]
    public async Task GetSettingByKey_WhenFound_ShouldReturnOkWithSetting()
    {
        // Arrange
        var mockSetting = new SystemSettingDto(Guid.NewGuid(), "KEY_1", "VAL_1", "Desc", "CAT", "string", DateTime.UtcNow);
        _settingServiceMock.Setup(s => s.GetByKeyAsync("KEY_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSetting);

        // Act
        var result = await _controller.GetSettingByKey("KEY_1", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(mockSetting);
    }

    [Fact]
    public async Task GetSettingByKey_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _settingServiceMock.Setup(s => s.GetByKeyAsync("UNKNOWN", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemSettingDto?)null);

        // Act
        var result = await _controller.GetSettingByKey("UNKNOWN", CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var apiResponse = notFoundResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.ErrorCode.Should().Be("SETTING_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateSetting_WhenValid_ShouldCallServiceAndReturnOk()
    {
        // Arrange
        var updatedSetting = new SystemSettingDto(Guid.NewGuid(), "KEY_1", "NEW_VAL", "Desc", "CAT", "string", DateTime.UtcNow);
        _settingServiceMock.Setup(s => s.UpdateAsync("KEY_1", "NEW_VAL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedSetting);

        // Act
        var result = await _controller.UpdateSetting("KEY_1", new UpdateSystemSettingRequest("NEW_VAL"), CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().Be(updatedSetting);
    }

    [Fact]
    public async Task UpdateSetting_WhenEmptyValue_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.UpdateSetting("KEY_1", new UpdateSystemSettingRequest("   "), CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.ErrorCode.Should().Be("INVALID_VALUE");
    }

    [Fact]
    public void ReloadCache_ShouldCallInvalidateCacheAndReturnOk()
    {
        // Act
        var result = _controller.ReloadCache();

        // Assert
        _settingServiceMock.Verify(s => s.InvalidateCache(), Times.Once);
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue();
    }
}
