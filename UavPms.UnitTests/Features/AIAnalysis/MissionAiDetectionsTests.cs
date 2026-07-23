using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.UnitTests.Features.AIAnalysis;

public class MissionAiDetectionsTests
{
    [Fact]
    public async Task GetMissionAiDetections_ShouldReturnOnlyMediaWithParsedDetections()
    {
        var missionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var repositoryMock = new Mock<IInspectionMediaRepository>();
        repositoryMock.Setup(r => r.GetByMissionIdWithDetailsAsync(missionId))
            .ReturnsAsync(new List<InspectionMedia>
            {
                new()
                {
                    Id = mediaId,
                    MissionId = missionId,
                    MediaType = "Image",
                    FileUrl = "https://storage/pole.jpg",
                    AiSource = "RF-DETR",
                    ValidationStatus = "PendingReview",
                    DetectedAnomalies = new List<DetectedAnomaly>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            MediaId = mediaId,
                            BoundingBox = """{ "x": 0.1, "y": 0.2, "width": 0.3, "height": 0.4 }""",
                            AiDetectionId = "ai-det-001",
                            FrameIndex = 360,
                            Timestamp = 12.03,
                            ImageUrl = "https://storage/frame-360.jpg",
                            CropUrl = "https://storage/crop-360.jpg",
                            Gps = """{ "lat": 10.762622, "lng": 106.660172 }""",
                            TowerId = "tower-42",
                            VideoDuration = 132.5,
                            VideoFps = 30,
                            VideoWidth = 1920,
                            VideoHeight = 1080,
                            ConfidenceScore = 0.91,
                            ValidationStatus = "Pending",
                            AiSource = "RF-DETR",
                            Category = new DefectCategory
                            {
                                CategoryCode = "CORROSION",
                                CategoryName = "Corrosion",
                                Description = "Rust/corrosion detected",
                                SeverityWeight = 0.8,
                                IsEmergencyClass = true
                            }
                        }
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    MissionId = missionId,
                    MediaType = "Image",
                    FileUrl = "https://storage/empty.jpg"
                }
            });

        var handler = new GetMissionAiDetectionsQueryHandler(repositoryMock.Object);

        var result = await handler.Handle(new GetMissionAiDetectionsQuery(missionId), CancellationToken.None);

        result.Should().HaveCount(1);
        var media = result[0];
        media.MediaId.Should().Be(mediaId);
        media.DetectionCount.Should().Be(1);
        media.VideoMetadata.Should().NotBeNull();
        media.VideoMetadata!.Duration.Should().Be(132.5);
        media.VideoMetadata.Fps.Should().Be(30);
        media.VideoMetadata.Width.Should().Be(1920);
        media.VideoMetadata.Height.Should().Be(1080);
        media.Detections[0].CategoryCode.Should().Be("CORROSION");
        var detection = media.Detections[0];
        detection.AiDetectionId.Should().Be("ai-det-001");
        detection.Class.Should().Be("Corrosion");
        detection.Confidence.Should().Be(0.91);
        detection.FrameIndex.Should().Be(360);
        detection.Timestamp.Should().Be(12.03);
        detection.ImageUrl.Should().Be("https://storage/frame-360.jpg");
        detection.CropUrl.Should().Be("https://storage/crop-360.jpg");
        detection.Gps.Should().NotBeNull();
        detection.Gps!.Lat.Should().Be(10.762622);
        detection.Gps.Lng.Should().Be(106.660172);
        detection.TowerId.Should().Be("tower-42");
        detection.BoundingBox.Should().NotBeNull();
        var boundingBox = detection.BoundingBox!;
        boundingBox.X.Should().Be(0.1);
        boundingBox.Y.Should().Be(0.2);
        boundingBox.Width.Should().Be(0.3);
        boundingBox.Height.Should().Be(0.4);
        detection.IsEmergencyClass.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewMissionAiDetection_ShouldPersistDecisionNotesAndAnalyst()
    {
        var missionId = Guid.NewGuid();
        var detectionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var analystId = Guid.NewGuid();
        var anomaly = new DetectedAnomaly
        {
            Id = detectionId,
            MediaId = mediaId,
            Media = new InspectionMedia { Id = mediaId, MissionId = missionId },
            BoundingBox = """{ "x1": 10, "y1": 20, "x2": 30, "y2": 50 }""",
            ConfidenceScore = 0.77,
            ValidationStatus = "Pending",
            AiSource = "RF-DETR",
            Category = new DefectCategory { CategoryCode = "BROKEN_INSULATOR", CategoryName = "Broken insulator" }
        };

        var anomalyRepoMock = new Mock<IAnomalyRepository>();
        anomalyRepoMock.Setup(r => r.GetByIdWithDetailAsync(detectionId)).ReturnsAsync(anomaly);
        anomalyRepoMock.Setup(r => r.UpdateAsync(It.IsAny<DetectedAnomaly>())).Returns(Task.CompletedTask);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUserMock = new Mock<ICurrentUserServices>();
        currentUserMock.Setup(c => c.UserId).Returns(analystId);

        var handler = new ReviewMissionAiDetectionCommandHandler(
            anomalyRepoMock.Object,
            unitOfWorkMock.Object,
            currentUserMock.Object);

        var result = await handler.Handle(new ReviewMissionAiDetectionCommand
        {
            MissionId = missionId,
            DetectionId = detectionId,
            Decision = "Rejected",
            Notes = "False positive: shadow on tower"
        }, CancellationToken.None);

        anomaly.ValidationStatus.Should().Be("Rejected");
        anomaly.AnalystNotes.Should().Be("False positive: shadow on tower");
        anomaly.AnalystId.Should().Be(analystId);
        anomaly.ValidatedAt.Should().NotBeNull();
        result.ValidationStatus.Should().Be("Rejected");
        result.AnalystNotes.Should().Be("False positive: shadow on tower");
        result.BoundingBox.Should().NotBeNull();
        result.BoundingBox!.X.Should().Be(10);
        result.BoundingBox.Width.Should().Be(20);

        anomalyRepoMock.Verify(r => r.UpdateAsync(anomaly), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
