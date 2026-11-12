using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadStandaloneAnalysis;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Application.Features.VisionBridge.Commands;
using UavPms.AIInspectionService.Application.Features.VisionBridge.DTOs;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Events;
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
        var runId = Guid.NewGuid();
        var towerId = Guid.NewGuid();

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
            TowerId = towerId,
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

        context.AIAnalysisRuns.Add(new AIAnalysisRun
        {
            Id = runId,
            RequestedBy = Guid.NewGuid(),
            MediaId = mediaId,
            ExecutionLocation = AIAnalysisRun.Cloud,
            AnalysisType = AnalysisType.DefectDetection,
            Status = AIAnalysisStatus.Processing,
            StartedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var callbackHandler = CreateCallbackHandler(context);

        var callbackResult = await callbackHandler.Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = runId,
            MediaId = mediaId,
            MissionId = missionId,
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
                    TowerId = "tower-42", // not a Guid -> falls back to the media tower
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

        var storedRun = await context.AIAnalysisRuns.SingleAsync(r => r.Id == runId);
        storedRun.Status.Should().Be(AIAnalysisStatus.Completed);
        storedRun.ModelName.Should().Be("RF-DETR");
        storedRun.ModelVersion.Should().Be("v1.0");
        storedRun.CompletedAt.Should().Be(new DateTime(2026, 7, 21, 9, 5, 0, DateTimeKind.Utc));

        var storedAnomaly = await context.DetectedAnomalies.SingleAsync();
        storedAnomaly.AnalysisRunId.Should().Be(runId);
        storedAnomaly.Location.Should().NotBeNull();
        storedAnomaly.Location!.SRID.Should().Be(4326);
        storedAnomaly.Location.X.Should().Be(106.660172);
        storedAnomaly.Location.Y.Should().Be(10.762622);

        var queryHandler = new GetMissionAiDetectionsQueryHandler(
            new InspectionMediaRepository(context),
            new GenericRepository<Mission>(context),
            new GenericRepository<MissionAssignment>(context),
            Mock.Of<ICurrentUserServices>());
        var mediaResults = await queryHandler.Handle(
            new GetMissionAiDetectionsQuery(missionId),
            CancellationToken.None);

        mediaResults.Should().HaveCount(1);
        var media = mediaResults[0];
        media.MediaType.Should().Be("Video");
        media.TowerId.Should().Be(towerId);
        media.AiSource.Should().Be("RF-DETR");
        media.VideoMetadata.Should().NotBeNull();
        media.VideoMetadata!.Duration.Should().Be(132.5);
        media.VideoMetadata.Fps.Should().Be(30);
        media.VideoMetadata.Width.Should().Be(1920);
        media.VideoMetadata.Height.Should().Be(1080);

        media.Detections.Should().HaveCount(1);
        var detection = media.Detections[0];
        detection.AiDetectionId.Should().Be("ai-det-360");
        detection.AnalysisRunId.Should().Be(runId);
        detection.AiSource.Should().Be("RF-DETR");
        detection.Class.Should().Be("Broken Insulator");
        detection.Confidence.Should().Be(0.94);
        detection.FrameIndex.Should().Be(360);
        detection.Timestamp.Should().Be(12.03);
        detection.ImageUrl.Should().Be("https://storage.local/missions/mis-ai-001/frames/360.jpg");
        detection.CropUrl.Should().Be("https://storage.local/missions/mis-ai-001/crops/ai-det-360.jpg");
        detection.Gps.Should().NotBeNull();
        detection.Gps!.Lat.Should().Be(10.762622);
        detection.Gps.Lng.Should().Be(106.660172);
        detection.TowerId.Should().Be(towerId);
        detection.BoundingBox.Should().NotBeNull();
        detection.BoundingBox!.X.Should().Be(0.1);
    }

    [Fact]
    public async Task EdgeDetection_ShouldCreateCompletedEdgeRun_AndCloudResultShouldSupersedeIt()
    {
        await using var context = CreateContext();
        var uavId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        context.Uavs.Add(new Uav { Id = uavId, UavCode = "UAV-EDGE-01", FlightStatus = "Flying" });
        context.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MIS-EDGE-001",
            Title = "Edge inspection",
            Status = "Executing",
            ManagerId = Guid.NewGuid(),
            UavId = uavId
        });
        context.DefectCategories.Add(new DefectCategory
        {
            Id = 2001,
            CategoryCode = "CI",
            CategoryName = "Broken Insulator",
            Description = "Broken or cracked insulator"
        });
        await context.SaveChangesAsync();

        var edgeHandler = new ReceiveVisionDetectionCommandHandler(
            new GenericRepository<Uav>(context),
            new GenericRepository<Mission>(context),
            new GenericRepository<InspectionMedia>(context),
            new GenericRepository<DetectedAnomaly>(context),
            new GenericRepository<AIAnalysisRun>(context),
            new GenericRepository<MissionAssignment>(context),
            new GenericRepository<DefectCategory>(context),
            new UserRepository(context),
            new NotificationRepository(context),
            Mock.Of<IEventPublisher>(),
            new UnitOfWork(context),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReceiveVisionDetectionCommandHandler>>());

        var edgeResult = await edgeHandler.Handle(new ReceiveVisionDetectionCommand
        {
            Detection = new VisionDetectionDto
            {
                DroneId = "UAV-EDGE-01",
                ClassName = "CI",
                Confidence = 0.81,
                Timestamp = DateTime.UtcNow,
                Latitude = 21.0285,
                Longitude = 105.8542,
                TrackId = 17,
                BoundingBox = new[] { 10, 20, 110, 220 }
            }
        }, CancellationToken.None);

        edgeResult.Success.Should().BeTrue();

        var edgeRun = await context.AIAnalysisRuns.SingleAsync();
        edgeRun.ExecutionLocation.Should().Be(AIAnalysisRun.Edge);
        edgeRun.Status.Should().Be(AIAnalysisStatus.Completed);
        edgeRun.UavId.Should().Be(uavId);
        edgeRun.RequestedBy.Should().BeNull();
        edgeRun.ModelName.Should().Be(ReceiveVisionDetectionCommandHandler.EdgeModelName);
        edgeRun.StartedAt.Should().NotBeNull();
        edgeRun.CompletedAt.Should().NotBeNull();

        var edgeAnomaly = await context.DetectedAnomalies.SingleAsync();
        edgeAnomaly.AnalysisRunId.Should().Be(edgeRun.Id);
        edgeAnomaly.AiDetectionId.Should().Be("17");
        edgeAnomaly.AnalystNotes.Should().NotContain("Track ID");
        edgeAnomaly.Location.Should().NotBeNull();
        edgeAnomaly.Location!.Y.Should().Be(21.0285);
        edgeAnomaly.SupersededById.Should().BeNull();

        var media = await context.InspectionMedia.SingleAsync();
        media.TowerId.Should().BeNull();

        // Cloud analysis of the same media detects the same category -> supersedes the edge anomaly.
        var cloudRunId = Guid.NewGuid();
        context.AIAnalysisRuns.Add(new AIAnalysisRun
        {
            Id = cloudRunId,
            MediaId = media.Id,
            ExecutionLocation = AIAnalysisRun.Cloud,
            ModelName = "SERVER",
            Status = AIAnalysisStatus.Processing,
            StartedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var callbackResult = await CreateCallbackHandler(context).Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = cloudRunId,
            MediaId = media.Id,
            MissionId = missionId,
            Status = "Completed",
            ModelName = "RF-DETR",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "cloud-1",
                    CategoryCode = "CI",
                    Confidence = 0.97,
                    BoundingBox = new BoundingBoxDto { X = 0.1, Y = 0.1, Width = 0.2, Height = 0.2 }
                }
            },
            CompletedAt = DateTime.UtcNow
        }, CancellationToken.None);

        callbackResult.SavedDetections.Should().Be(1);
        var cloudAnomaly = await context.DetectedAnomalies.SingleAsync(a => a.AnalysisRunId == cloudRunId);
        var reloadedEdge = await context.DetectedAnomalies.SingleAsync(a => a.Id == edgeAnomaly.Id);
        reloadedEdge.SupersededById.Should().Be(cloudAnomaly.Id);
        cloudAnomaly.SupersededById.Should().BeNull();
    }

    [Fact]
    public async Task EmergencyDefectDetection_ShouldAutoCreateActiveEmergencyAlert_InCloudAndEdgeFlows()
    {
        await using var context = CreateContext();
        var uavId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        context.Uavs.Add(new Uav { Id = uavId, UavCode = "UAV-EMG-01", FlightStatus = "Flying" });
        context.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MIS-EMG-001",
            Title = "Emergency defect inspection",
            Status = "Executing",
            ManagerId = Guid.NewGuid(),
            UavId = uavId
        });
        context.InspectionMedia.Add(new InspectionMedia
        {
            Id = mediaId,
            MissionId = missionId,
            MediaType = "Image",
            FileUrl = "https://storage.local/missions/mis-emg-001/image.jpg",
            CapturedAt = DateTime.UtcNow
        });
        context.DefectCategories.Add(new DefectCategory
        {
            Id = 3001,
            CategoryCode = "WF",
            CategoryName = "Wildfire / Flashover",
            Description = "Fire or flashover near transmission line",
            SeverityWeight = 5.0,
            IsEmergencyClass = true
        });
        context.AIAnalysisRuns.Add(new AIAnalysisRun
        {
            Id = runId,
            RequestedBy = Guid.NewGuid(),
            MediaId = mediaId,
            ExecutionLocation = AIAnalysisRun.Cloud,
            AnalysisType = AnalysisType.DefectDetection,
            Status = AIAnalysisStatus.Processing,
            StartedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var callbackResult = await CreateCallbackHandler(context).Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = runId,
            MediaId = mediaId,
            MissionId = missionId,
            Status = "Completed",
            ModelName = "RF-DETR",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "emg-det-1",
                    CategoryCode = "WF",
                    ClassName = "Wildfire / Flashover",
                    Confidence = 0.96,
                    BoundingBox = new BoundingBoxDto { X = 0.2, Y = 0.2, Width = 0.3, Height = 0.3 }
                }
            },
            CompletedAt = DateTime.UtcNow
        }, CancellationToken.None);

        callbackResult.SavedDetections.Should().Be(1);
        callbackResult.CreatedAlerts.Should().Be(1);

        var cloudAnomaly = await context.DetectedAnomalies.SingleAsync(a => a.AnalysisRunId == runId);
        var cloudAlert = await context.EmergencyAlerts.SingleAsync(a => a.AnomalyId == cloudAnomaly.Id);
        cloudAlert.Status.Should().Be("Active");
        cloudAlert.Priority.Should().Be("Critical");

        // Edge emergency detection should also auto-create an active EmergencyAlert
        var edgeHandler = new ReceiveVisionDetectionCommandHandler(
            new GenericRepository<Uav>(context),
            new GenericRepository<Mission>(context),
            new GenericRepository<InspectionMedia>(context),
            new GenericRepository<DetectedAnomaly>(context),
            new GenericRepository<AIAnalysisRun>(context),
            new GenericRepository<MissionAssignment>(context),
            new GenericRepository<DefectCategory>(context),
            new UserRepository(context),
            new NotificationRepository(context),
            Mock.Of<IEventPublisher>(),
            new UnitOfWork(context),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReceiveVisionDetectionCommandHandler>>(),
            new GenericRepository<EmergencyAlert>(context));

        var edgeResult = await edgeHandler.Handle(new ReceiveVisionDetectionCommand
        {
            Detection = new VisionDetectionDto
            {
                DroneId = "UAV-EMG-01",
                ClassName = "WF",
                Confidence = 0.91,
                Timestamp = DateTime.UtcNow,
                Latitude = 21.0285,
                Longitude = 105.8542,
                TrackId = 99,
                BoundingBox = new[] { 10, 20, 110, 220 }
            }
        }, CancellationToken.None);

        edgeResult.Success.Should().BeTrue();
        var edgeAlert = await context.EmergencyAlerts.SingleAsync(a => a.AnomalyId == edgeResult.RecordId);
        edgeAlert.Status.Should().Be("Active");
        edgeAlert.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task UploadStandaloneAnalysis_ShouldSaveToMinioAndQueueOutboxJob_WithoutMockDetections()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserServices>();
        currentUser.Setup(u => u.UserId).Returns(userId);

        var fileStorage = new Mock<IFileStorageService>();
        fileStorage
            .Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string fileName) => $"https://minio.local/uavpms-ai/images/2026/11/12/{fileName}");

        var handler = new UploadStandaloneAnalysisCommandHandler(
            new GenericRepository<Mission>(context),
            new GenericRepository<Tower>(context),
            new GenericRepository<InspectionMedia>(context),
            new GenericRepository<AIAnalysisRun>(context),
            fileStorage.Object,
            Mock.Of<IEventPublisher>(),
            currentUser.Object,
            new UnitOfWork(context),
            Mock.Of<ILogger<UploadStandaloneAnalysisCommandHandler>>(),
            new GenericRepository<OutboxMessage>(context));

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("real-image-binary-payload"));
        var result = await handler.Handle(new UploadStandaloneAnalysisCommand
        {
            Files = new[]
            {
                new UploadedFileInput
                {
                    Stream = stream,
                    FileName = "insulator_tower_01.jpg",
                    ContentType = "image/jpeg",
                    Length = stream.Length
                }
            },
            AnalysisType = "DefectDetection",
            PreferredModel = "RF-DETR",
            Notes = "Upload thật lên MinIO"
        }, CancellationToken.None);

        result.Status.Should().Be(AIAnalysisStatus.Pending.ToString());
        result.FilesCount.Should().Be(1);
        result.Detections.Should().BeEmpty();
        result.Runs.Should().ContainSingle();
        result.Runs[0].FileUrl.Should().Be("https://minio.local/uavpms-ai/images/2026/11/12/insulator_tower_01.jpg");

        fileStorage.Verify(s => s.SaveImageAsync(It.IsAny<Stream>(), "insulator_tower_01.jpg"), Times.Once);

        var savedMedia = await context.InspectionMedia.SingleAsync();
        savedMedia.FileUrl.Should().Be("https://minio.local/uavpms-ai/images/2026/11/12/insulator_tower_01.jpg");
        savedMedia.Checksum.Should().NotBeNullOrWhiteSpace();

        var savedRun = await context.AIAnalysisRuns.SingleAsync();
        savedRun.MediaId.Should().Be(savedMedia.Id);
        savedRun.Status.Should().Be(AIAnalysisStatus.Pending);
        savedRun.ModelName.Should().Be("RF-DETR");

        var outboxMsg = await context.OutboxMessages.SingleAsync();
        outboxMsg.MessageType.Should().Be(nameof(AIAnalysisRequestedEvent));
        outboxMsg.Payload.Should().Contain(savedRun.Id.ToString());
    }

    private static ProcessAiAnalysisResultCommandHandler CreateCallbackHandler(ApplicationDbContext context)
        => new(
            new GenericRepository<AIAnalysisRun>(context),
            new InspectionMediaRepository(context),
            new GenericRepository<DefectCategory>(context),
            new AnomalyRepository(context),
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
            new GenericRepository<OutboxMessage>(context),
            Mock.Of<ILogger<ProcessAiAnalysisResultCommandHandler>>(),
            new UavPms.AIInspectionService.Infrastructure.Services.SystemSettingService(
                context, Mock.Of<ILogger<UavPms.AIInspectionService.Infrastructure.Services.SystemSettingService>>()),
            new GenericRepository<EmergencyAlert>(context));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var currentUser = new Mock<ICurrentUserServices>();
        return new ApplicationDbContext(options, currentUser.Object);
    }

    [Fact]
    public async Task ReceiveVisionDetection_ShouldMatchInProgressMission_MapAiLabel_AndQueueOutbox()
    {
        await using var context = CreateContext();
        var uavId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        context.Uavs.Add(new Uav { Id = uavId, UavCode = "UAV-INPROG-01" });
        context.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MSN-INPROG-01",
            Title = "InProgress Mission",
            Status = "InProgress",
            UavId = uavId,
            ManagerId = managerId
        });
        context.DefectCategories.Add(new DefectCategory
        {
            Id = 3001,
            CategoryCode = "BROKEN_INSULATOR",
            CategoryName = "Broken Insulator",
            IsEmergencyClass = true
        });
        await context.SaveChangesAsync();

        var settingServiceMock = new Mock<UavPms.Shared.Contracts.Interfaces.ISystemSettingService>();
        settingServiceMock
            .Setup(s => s.GetByKeyAsync("AiLabel:insulator_crack", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UavPms.Shared.Contracts.Settings.SystemSettingDto(
                Guid.NewGuid(),
                "AiLabel:insulator_crack",
                "BROKEN_INSULATOR",
                "String",
                "AI",
                null,
                null));

        var evalMock = new Mock<IInspectionEvaluationClient>();
        evalMock
            .Setup(e => e.EvaluateAsync(It.IsAny<DetectionEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetectionEvaluationResult("Critical", "High", 95, true, "Severe crack"));

        var outboxRepo = new GenericRepository<OutboxMessage>(context);

        var handler = new ReceiveVisionDetectionCommandHandler(
            new GenericRepository<Uav>(context),
            new GenericRepository<Mission>(context),
            new GenericRepository<InspectionMedia>(context),
            new GenericRepository<DetectedAnomaly>(context),
            new GenericRepository<AIAnalysisRun>(context),
            new GenericRepository<MissionAssignment>(context),
            new GenericRepository<DefectCategory>(context),
            new UserRepository(context),
            new NotificationRepository(context),
            Mock.Of<IEventPublisher>(),
            new UnitOfWork(context),
            Mock.Of<IFileStorageService>(),
            Mock.Of<ILogger<ReceiveVisionDetectionCommandHandler>>(),
            new GenericRepository<EmergencyAlert>(context),
            settingServiceMock.Object,
            evalMock.Object,
            outboxRepo);

        var result = await handler.Handle(new ReceiveVisionDetectionCommand
        {
            Detection = new VisionDetectionDto
            {
                DroneId = "UAV-INPROG-01",
                ClassName = "insulator_crack",
                Confidence = 0.91,
                Timestamp = DateTime.UtcNow,
                Latitude = 21.0285,
                Longitude = 105.8542,
                TrackId = 42
            }
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        var anomaly = await context.DetectedAnomalies.SingleAsync();
        anomaly.CategoryId.Should().Be(3001);
        anomaly.AnalystNotes.Should().Contain("Severe crack");

        var alert = await context.EmergencyAlerts.SingleAsync();
        alert.Status.Should().Be("Active");

        var outbox = await context.OutboxMessages.ToListAsync();
        outbox.Should().Contain(m => m.MessageType == nameof(AiDetectionCompletedEvent));
    }
}
