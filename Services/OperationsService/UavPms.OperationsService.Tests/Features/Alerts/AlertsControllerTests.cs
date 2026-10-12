using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UavPms.OperationsService.API.Controllers;
using UavPms.OperationsService.Application.Features.Alerts;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Alerts;

public class AlertsControllerTests
{
    private readonly Mock<IEmergencyAlertService> _alertServiceMock = new();
    private readonly Mock<ICurrentUserServices> _currentUserMock = new();
    private readonly AlertsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public AlertsControllerTests()
    {
        _currentUserMock.SetupGet(u => u.UserId).Returns(_currentUserId);
        _controller = new AlertsController(_alertServiceMock.Object, _currentUserMock.Object);
    }

    [Fact]
    public async Task GetActiveAlerts_ShouldReturnOkWithListOfActiveAlerts()
    {
        // Arrange
        var mockAlerts = new List<ActiveEmergencyAlertDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AnomalyId = Guid.NewGuid(),
                AssetCode = "TOW-N1-05",
                AlertType = "Wildfire",
                Priority = "Critical",
                Status = "Active"
            }
        };
        _alertServiceMock.Setup(s => s.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockAlerts);

        // Act
        var result = await _controller.GetActiveAlerts(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(mockAlerts);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnOkWithPaginatedAlerts()
    {
        // Arrange
        var paginatedResponse = new PaginatedAlertsResponse
        {
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            Items = new List<EmergencyAlertHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AssetCode = "TOW-N1-05",
                    AlertType = "Wildfire",
                    Priority = "Critical",
                    Status = "Confirmed"
                }
            }
        };
        _alertServiceMock.Setup(s => s.GetAlertHistoryAsync("Confirmed", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetAlerts("Confirmed", 1, 10, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(paginatedResponse);
    }

    [Fact]
    public async Task GetAlertById_WhenFound_ShouldReturnOkWithDetail()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var mockDetail = new EmergencyAlertDetailDto
        {
            Id = alertId,
            AssetCode = "TOW-N1-05",
            Status = "Active",
            Priority = "Critical"
        };
        _alertServiceMock.Setup(s => s.GetAlertByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDetail);

        // Act
        var result = await _controller.GetAlertById(alertId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(mockDetail);
    }

    [Fact]
    public async Task GetAlertById_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        _alertServiceMock.Setup(s => s.GetAlertByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmergencyAlertDetailDto?)null);

        // Act
        var result = await _controller.GetAlertById(alertId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReviewAlert_WhenSuccess_ShouldReturnNoContent()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new ReviewEmergencyAlertRequest { Status = "Confirmed", Notes = "Reviewed by Analyst" };
        _alertServiceMock.Setup(s => s.ReviewAlertAsync(alertId, _currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReviewAlert(alertId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ReviewAlert_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new ReviewEmergencyAlertRequest { Status = "Confirmed" };
        _alertServiceMock.Setup(s => s.ReviewAlertAsync(alertId, _currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ReviewAlert(alertId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task EscalateAlert_WhenSuccess_ShouldReturnCreated()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var targetManagerId = Guid.NewGuid();
        var request = new EscalateEmergencyAlertRequest { EscalatedToUserId = targetManagerId, Reason = "Critical risk" };
        var mockEscalation = new AlertEscalationDto
        {
            Id = Guid.NewGuid(),
            EscalatedBy = _currentUserId,
            EscalatedTo = targetManagerId,
            Reason = "Critical risk",
            EscalatedAt = DateTime.UtcNow
        };
        _alertServiceMock.Setup(s => s.EscalateAlertAsync(alertId, _currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockEscalation);

        // Act
        var result = await _controller.EscalateAlert(alertId, request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(mockEscalation);
    }

    [Fact]
    public async Task EscalateAlert_WhenAlertNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var request = new EscalateEmergencyAlertRequest { EscalatedToUserId = Guid.NewGuid(), Reason = "Reason" };
        _alertServiceMock.Setup(s => s.EscalateAlertAsync(alertId, _currentUserId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Alert not found"));

        // Act
        var result = await _controller.EscalateAlert(alertId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TriggerAlert_ShouldReturnCreated()
    {
        // Arrange
        var request = new TriggerEmergencyAlertRequest
        {
            TowerCode = "TOW-N1-05",
            AlertType = "Wildfire / Flashover",
            Priority = "Critical"
        };
        var mockResult = new EmergencyAlertDetailDto
        {
            Id = Guid.NewGuid(),
            AssetCode = "TOW-N1-05",
            Status = "Active"
        };
        _alertServiceMock.Setup(s => s.TriggerAlertAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _controller.TriggerAlert(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeEquivalentTo(mockResult);
    }
}
