using System;
using FluentAssertions;
using UavPms.AIInspectionService.Application.Common.Utilities;
using UavPms.AIInspectionService.Domain.Entities;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class DetectionMatcherTests
{
    private static DetectedAnomaly Edge(string? box, double? frameTime = null)
        => new() { Id = Guid.NewGuid(), BoundingBox = box ?? string.Empty, FrameTimeSeconds = frameTime };

    private static DetectedAnomaly Cloud(string? box, double? frameTime = null)
        => new() { Id = Guid.NewGuid(), BoundingBox = box ?? string.Empty, FrameTimeSeconds = frameTime };

    [Fact]
    public void SameUnits_OverlappingBoxes_Match()
    {
        // edge (10,20)-(110,220) vs cloud (20,30)-(120,230): IoU = 17100 / 22900 ≈ 0.75
        var edge = Edge("""{"x1":10,"y1":20,"x2":110,"y2":220}""");
        var cloud = Cloud("""{"x":20,"y":30,"width":100,"height":200}""");

        DetectionMatcher.IsSameDefect(edge, cloud, null, null).Should().BeTrue();
    }

    [Fact]
    public void SameUnits_DisjointBoxes_DoNotMatch()
    {
        var edge = Edge("""{"x1":10,"y1":20,"x2":110,"y2":220}""");
        var cloud = Cloud("""{"x":500,"y":500,"width":100,"height":100}""");

        DetectionMatcher.IsSameDefect(edge, cloud, null, null).Should().BeFalse();
    }

    [Fact]
    public void NormalizedCloudBox_IsScaledToPixelsWithMediaSize()
    {
        // 1000x1000 frame: cloud (0.1,0.2,0.1,0.2) -> (100,200)-(200,400)
        var edge = Edge("""{"x1":100,"y1":200,"x2":200,"y2":400}""");
        var sameSpot = Cloud("""{"x":0.1,"y":0.2,"width":0.1,"height":0.2}""");
        var otherSpot = Cloud("""{"x":0.7,"y":0.7,"width":0.1,"height":0.2}""");

        DetectionMatcher.IsSameDefect(edge, sameSpot, 1000, 1000).Should().BeTrue();
        DetectionMatcher.IsSameDefect(edge, otherSpot, 1000, 1000).Should().BeFalse();
    }

    [Fact]
    public void DifferentUnitsWithoutMediaSize_FallsBackToCategoryRule()
    {
        var edge = Edge("""{"x1":700,"y1":700,"x2":800,"y2":800}""");
        var cloud = Cloud("""{"x":0.1,"y":0.1,"width":0.1,"height":0.1}""");

        DetectionMatcher.IsSameDefect(edge, cloud, null, null).Should().BeTrue();
    }

    [Fact]
    public void MissingBox_FallsBackToCategoryRule()
    {
        DetectionMatcher.IsSameDefect(Edge(null), Cloud("""{"x":1,"y":1,"width":2,"height":2}"""), null, null).Should().BeTrue();
    }

    [Fact]
    public void VideoFramesMoreThanOneSecondApart_DoNotMatch_EvenWithSameBox()
    {
        const string box = """{"x":10,"y":10,"width":50,"height":50}""";

        DetectionMatcher.IsSameDefect(Edge(box, 12.0), Cloud(box, 12.8), null, null).Should().BeTrue();
        DetectionMatcher.IsSameDefect(Edge(box, 12.0), Cloud(box, 14.5), null, null).Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"X":1,"Y":2,"Width":3,"Height":4}""", 1, 2, 3, 4)]
    [InlineData("""{"x1":"1","y1":"2","x2":"4","y2":"6"}""", 1, 2, 3, 4)]
    public void TryParse_AcceptsBothFormats_CaseInsensitive(string json, double x, double y, double width, double height)
    {
        DetectionMatcher.TryParse(json, out var box).Should().BeTrue();
        box.Should().Be(new DetectionBox(x, y, width, height));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[1,2,3,4]")]
    [InlineData("not json")]
    [InlineData("""{"x":1}""")]
    public void TryParse_RejectsUnknownShapes(string json)
    {
        DetectionMatcher.TryParse(json, out _).Should().BeFalse();
    }
}
