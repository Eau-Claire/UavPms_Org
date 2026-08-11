using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UavPms.Grpc.InspectionEvaluation;
using UavPms.InspectionEvaluationService.Application.Common.Options;
using UavPms.InspectionEvaluationService.Infrastructure.Services;
using Xunit;

namespace UavPms.InspectionEvaluationService.Tests;

public class InspectionEvaluationGrpcServiceTests
{
    private readonly Mock<ILogger<InspectionEvaluationGrpcService>> _loggerMock;
    private readonly InspectionEvaluationGrpcService _service;

    public InspectionEvaluationGrpcServiceTests()
    {
        _loggerMock = new Mock<ILogger<InspectionEvaluationGrpcService>>();
        var options = Options.Create(new EvaluationThresholdOptions());
        _service = new InspectionEvaluationGrpcService(options, _loggerMock.Object);
    }

    [Fact]
    public async Task EvaluateDetection_EmergencyClassWithHighConfidence_ReturnsCriticalSeverityAndImmediateAction()
    {
        // Arrange
        var request = new EvaluateDetectionRequest
        {
            DetectionId = "det-1",
            CategoryCode = "FIRE_01",
            Confidence = 0.95f,
            IsEmergencyClass = true
        };

        // Act
        var response = await _service.EvaluateDetection(request, null!);

        // Assert
        response.Should().NotBeNull();
        response.Severity.Should().Be("Critical");
        response.RiskLevel.Should().Be("ImmediateAction");
        response.PriorityScore.Should().Be(97);
        response.RequiresImmediateAlert.Should().BeTrue();
        response.Reason.Should().Contain("Emergency category FIRE_01 exceeded confidence threshold");
    }

    [Fact]
    public async Task EvaluateDetection_NonEmergencyClassWithLowConfidence_ReturnsLowSeverityAndMonitor()
    {
        // Arrange
        var request = new EvaluateDetectionRequest
        {
            DetectionId = "det-2",
            CategoryCode = "RUST_01",
            Confidence = 0.30f,
            IsEmergencyClass = false
        };

        // Act
        var response = await _service.EvaluateDetection(request, null!);

        // Assert
        response.Should().NotBeNull();
        response.Severity.Should().Be("Low");
        response.RiskLevel.Should().Be("Monitor");
        response.PriorityScore.Should().Be(17);
        response.RequiresImmediateAlert.Should().BeFalse();
        response.Reason.Should().Contain("Category RUST_01 evaluated with confidence");
    }
}
