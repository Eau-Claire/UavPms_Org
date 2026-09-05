using FluentAssertions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public sealed class ProcessAiAnalysisResultValidationTests
{
    private readonly ProcessAiAnalysisResultCommandValidator _validator = new();

    [Fact]
    public void CompletedCallback_ShouldAllowZeroDetections()
    {
        _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Callback_ShouldRejectConfidenceOutsideNormalizedRange(double confidence)
    {
        var command = ValidCommand();
        command.Detections = [ValidDetection()];
        command.Detections[0].Confidence = confidence;
        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Callback_ShouldRejectBoundingBoxThatCrossesMediaExtent()
    {
        var command = ValidCommand();
        command.Detections = [ValidDetection()];
        command.Detections[0].BoundingBox = new BoundingBoxDto { X = .8, Y = .2, Width = .3, Height = .4 };
        _validator.Validate(command).IsValid.Should().BeFalse();
    }

    private static ProcessAiAnalysisResultCommand ValidCommand() => new()
    {
        RequestId = Guid.NewGuid(), MediaId = Guid.NewGuid(), MissionId = Guid.NewGuid(), AssetId = Guid.NewGuid(),
        Status = "Completed", ModelName = "server", CompletedAt = DateTime.UtcNow, Detections = []
    };

    private static DetectionDto ValidDetection() => new()
    {
        CategoryCode = "CORROSION", Confidence = .9,
        BoundingBox = new BoundingBoxDto { X = .1, Y = .2, Width = .3, Height = .4 }
    };
}
