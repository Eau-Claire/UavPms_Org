using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

public class AiInspectionPipelineTests
{
    [Fact]
    public async Task AiCallbackToMissionDetectionsPipeline_ShouldExposeVideoTimelineMetadata()
    {
        await using var context = CreateContext();
        var missionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var assetId = Guid.NewGuid();

        context.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MIS-AI-001",
            Title = "AI inspection pipeline",
            Status = "InProgress",
            ManagerId = Guid.NewGuid()
        });

        context.InspectionMedia.Add(new InspectionMedia
        {
            Id = mediaId,
            MissionId = missionId,
            AssetId = assetId,
            MediaType = "Video",
            FileUrl = "https://storage.local/missions/mis-ai-001/video.mp4",
            CapturedAt = new DateTime(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc)
        });

        context.DefectCategories.Add(new DefectCategory
        {
            Id = 1001,
            CategoryCode = "CI",
            CategoryName = "Broken Insulator",
            Description = "Broken or cracked insulator",
            SeverityWeight = 0.95,
            IsEmergencyClass = false
        });

        context.AIAnalysisRequests.Add(new AIAnalysisRequest
        {
            Id = requestId,
            UploadedBy = Guid.NewGuid(),
            MediaId = mediaId,
            MissionId = missionId,
            AssetId = assetId,
            FileUrl = "https://storage.local/missions/mis-ai-001/video.mp4",
            MediaType = "Video",
            AnalysisType = AnalysisType.DefectDetection,
            Status = AIAnalysisStatus.Processing
        });

        await context.SaveChangesAsync();

        var aiRequestRepo = new GenericRepository<AIAnalysisRequest>(context);
        var mediaRepo = new InspectionMediaRepository(context);
        var defectCategoryRepo = new GenericRepository<DefectCategory>(context);
        var anomalyRepo = new AnomalyRepository(context);
        var callbackHandler = new ProcessAiAnalysisResultCommandHandler(
            aiRequestRepo,
            mediaRepo,
            defectCategoryRepo,
            anomalyRepo,
            new UnitOfWork(context),
            Mock.Of<IInspectionEvaluationClient>(c => c.EvaluateAsync(
                It.IsAny<DetectionEvaluationRequest>(),
                It.IsAny<CancellationToken>()) == Task.FromResult(new DetectionEvaluationResult(
                    "Medium",
                    "PlannedReview",
                    70,
                    false,
                    "Unit test evaluation"))),
            Mock.Of<IEventPublisher>(),
            Mock.Of<ILogger<ProcessAiAnalysisResultCommandHandler>>());

        var callbackResult = await callbackHandler.Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = requestId,
            MediaId = mediaId,
            MissionId = missionId,
            AssetId = assetId,
            Status = "Completed",
            ModelName = "RF-DETR",
            ModelVersion = "v1.0",
            ProcessingTimeMs = 1500,
            VideoMetadata = new VideoMetadataDto
            {
                Duration = 132.5,
                Fps = 30,
                Width = 1920,
                Height = 1080
            },
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "ai-det-360",
                    CategoryCode = "CI",
                    ClassName = "Broken Insulator",
                    Confidence = 0.94,
                    FrameIndex = 360,
                    Timestamp = 12.03,
                    ImageUrl = "https://storage.local/missions/mis-ai-001/frames/360.jpg",
                    CropUrl = "https://storage.local/missions/mis-ai-001/crops/ai-det-360.jpg",
                    Gps = new GpsDto { Lat = 10.762622, Lng = 106.660172 },
                    TowerId = "tower-42",
                    AssetId = assetId,
                    BoundingBox = new BoundingBoxDto
                    {
                        X = 0.1,
                        Y = 0.2,
                        Width = 0.3,
                        Height = 0.4
                    }
                }
            },
            CompletedAt = new DateTime(2026, 7, 21, 9, 5, 0, DateTimeKind.Utc)
        }, CancellationToken.None);

        callbackResult.SavedDetections.Should().Be(1);

        var queryHandler = new GetMissionAiDetectionsQueryHandler(
            mediaRepo,
            new GenericRepository<Mission>(context),
            Mock.Of<ICurrentUserServices>());
        var mediaResults = await queryHandler.Handle(
            new GetMissionAiDetectionsQuery(missionId),
            CancellationToken.None);

        mediaResults.Should().HaveCount(1);
        var media = mediaResults[0];
        media.MediaType.Should().Be("Video");
        media.VideoMetadata.Should().NotBeNull();
        media.VideoMetadata!.Duration.Should().Be(132.5);
        media.VideoMetadata.Fps.Should().Be(30);
        media.VideoMetadata.Width.Should().Be(1920);
        media.VideoMetadata.Height.Should().Be(1080);

        media.Detections.Should().HaveCount(1);
        var detection = media.Detections[0];
        detection.AiDetectionId.Should().Be("ai-det-360");
        detection.Class.Should().Be("Broken Insulator");
        detection.Confidence.Should().Be(0.94);
        detection.FrameIndex.Should().Be(360);
        detection.Timestamp.Should().Be(12.03);
        detection.ImageUrl.Should().Be("https://storage.local/missions/mis-ai-001/frames/360.jpg");
        detection.CropUrl.Should().Be("https://storage.local/missions/mis-ai-001/crops/ai-det-360.jpg");
        detection.Gps.Should().NotBeNull();
        detection.Gps!.Lat.Should().Be(10.762622);
        detection.Gps.Lng.Should().Be(106.660172);
        detection.TowerId.Should().Be("tower-42");
        detection.AssetId.Should().Be(assetId);
        detection.BoundingBox.Should().NotBeNull();
        detection.BoundingBox!.X.Should().Be(0.1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new Mock<ICurrentUserServices>();
        return new ApplicationDbContext(options, currentUser.Object);
    }
}
