using FluentAssertions;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Infrastructure.Messaging;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Infrastructure.Messaging;

public class MockDetectionGeneratorTests
{
    private static readonly List<DefectCategory> Categories =
    [
        new() { Id = 1, CategoryCode = "CORROSION", CategoryName = "Ăn mòn" },
        new() { Id = 2, CategoryCode = "CRACK", CategoryName = "Nứt vỡ", IsEmergencyClass = true },
        new() { Id = 3, CategoryCode = "BIRD_NEST", CategoryName = "Tổ chim" },
        new() { Id = 4, CategoryCode = "LOOSE_BOLT", CategoryName = "Bu lông lỏng" },
    ];

    [Fact]
    public void Generate_SameMedia_ReturnsSameDetections()
    {
        var mediaId = Guid.NewGuid();

        var first = MockDetectionGenerator.Generate(mediaId, false, Categories, new MockDetectionOptions());
        var second = MockDetectionGenerator.Generate(mediaId, false, Categories, new MockDetectionOptions());

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public void Generate_ReturnsDistinctCategoriesWithNormalizedBoxesInsideTheImage()
    {
        for (var i = 0; i < 200; i++)
        {
            var detections = MockDetectionGenerator.Generate(Guid.NewGuid(), false, Categories, new MockDetectionOptions());

            detections.Should().HaveCountGreaterThan(0).And.HaveCountLessThanOrEqualTo(3);
            detections.Select(d => d.CategoryCode).Should().OnlyHaveUniqueItems();
            detections.Should().AllSatisfy(d =>
            {
                d.Confidence.Should().BeInRange(0.6, 0.97);
                d.BoundingBox.X.Should().BeGreaterThanOrEqualTo(0);
                d.BoundingBox.Y.Should().BeGreaterThanOrEqualTo(0);
                (d.BoundingBox.X + d.BoundingBox.Width).Should().BeLessThanOrEqualTo(1);
                (d.BoundingBox.Y + d.BoundingBox.Height).Should().BeLessThanOrEqualTo(1);
                d.FrameIndex.Should().BeNull();
            });
        }
    }

    [Fact]
    public void Generate_RaisesEmergencyCategoryForAboutTheConfiguredShareOfMedia()
    {
        var withEmergency = Enumerable.Range(0, 1000)
            .Count(_ => MockDetectionGenerator.Generate(Guid.NewGuid(), false, Categories, new MockDetectionOptions(EmergencyRate: 0.2))
                .Any(d => d.CategoryCode == "CRACK"));

        withEmergency.Should().BeInRange(120, 280);
    }

    [Fact]
    public void Generate_Video_SetsIncreasingFrames()
    {
        var detections = MockDetectionGenerator.Generate(Guid.NewGuid(), true, Categories, new MockDetectionOptions(MaxDetections: 3));

        detections.Should().AllSatisfy(d => d.FrameIndex.Should().NotBeNull());
        detections.Select(d => d.FrameIndex).Should().BeInAscendingOrder();
        detections.Should().AllSatisfy(d => d.TimestampMs.Should().Be(d.FrameIndex * 1000 / 30));
    }

    [Fact]
    public void Generate_ConfiguredCategory_ReturnsSingleDetectionWithConfiguredConfidence()
    {
        var detections = MockDetectionGenerator.Generate(Guid.NewGuid(), false, Categories, new MockDetectionOptions("bird_nest", 0.9));

        detections.Should().ContainSingle().Which.Should().Match<Application.Features.AIAnalysis.Commands.ProcessCallbackResults.DetectionDto>(
            d => d.CategoryCode == "BIRD_NEST" && d.Confidence == 0.9);
    }

    [Fact]
    public void Generate_NoCategories_ReturnsEmpty()
    {
        MockDetectionGenerator.Generate(Guid.NewGuid(), false, [], new MockDetectionOptions()).Should().BeEmpty();
    }
}
