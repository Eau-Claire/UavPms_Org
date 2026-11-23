using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ReviewMissionAiDetection;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Application.Features.Anomalies;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using UavPms.AIInspectionService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

/// <summary>
/// Bottom-Up Modular Integration Tests for AIInspectionService:
/// - Pair A: AnalyzeExistingMedia + ProcessAiAnalysisResult + SystemSettingService (AiLabel mapping) + Evaluation Engine
/// - Pair B: Cloud Reanalysis Superseding + AssignAnomalyTower + ReviewMissionAiDetection + SystemSettings SLA
/// </summary>
public class AiBottomUpModularIntegrationTests
{
    private static ApplicationDbContext CreateContext(ICurrentUserServices? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-bottom-up-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, currentUser ?? Mock.Of<ICurrentUserServices>());
    }

    private static ICurrentUserServices UserContext(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock.Object;
    }

    // =========================================================================================================
    // PAIR A: AnalyzeExistingMediaCommandHandler + ProcessAiAnalysisResultCommandHandler + SystemSettingService
    // =========================================================================================================
    [Fact]
    public async Task PairA_QueueExistingMedia_AndCallbackWithSystemSettingLabelMapping_ShouldCreateAnomalyAlertAndOutbox()
    {
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var towerId = Guid.NewGuid();

        var userCtx = UserContext(analystId, UserRoles.Analyst);
        await using var db = CreateContext(userCtx);

        // Seed SystemSetting mapping raw label "raw_flashover" -> "WF"
        var settingService = new SystemSettingService(db, NullLogger<SystemSettingService>.Instance);
        settingService.InvalidateCache();
        await settingService.UpdateAsync("AiLabel:raw_flashover", "WF");

        db.DefectCategories.Add(new DefectCategory
        {
            Id = 5001,
            CategoryCode = "WF",
            CategoryName = "Wildfire / Flashover",
            Description = "Critical flashover hazard",
            SeverityWeight = 5.0,
            IsEmergencyClass = true
        });
        db.Towers.Add(new Tower { Id = towerId, TowerCode = "T-500KV-01" });
        db.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MS-AI-PAIR-A",
            Title = "500kV Corridor Inspection",
            ManagerId = managerId,
            Status = "Completed"
        });
        db.MissionAssignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            UserId = analystId,
            AssignmentRole = "ANALYST",
            Status = "Active"
        });
        db.InspectionMedia.Add(new InspectionMedia
        {
            Id = mediaId,
            MissionId = missionId,
            TowerId = towerId,
            MediaType = "Image",
            FileUrl = "https://minio.local/uav-media/tower-01.jpg",
            CapturedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // 1. A different Manager who does not manage this mission is blocked by ForbiddenException
        var otherManagerHandler = new AnalyzeExistingMediaCommandHandler(
            new GenericRepository<Mission>(db),
            new InspectionMediaRepository(db),
            new GenericRepository<AIAnalysisRun>(db),
            new UnitOfWork(db),
            Mock.Of<IEventPublisher>(),
            UserContext(otherManagerId, UserRoles.Manager),
            new GenericRepository<OutboxMessage>(db),
            NullLogger<AnalyzeExistingMediaCommandHandler>.Instance);

        var forbiddenAct = () => otherManagerHandler.Handle(new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = mediaId,
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR"
        }, CancellationToken.None);
        await forbiddenAct.Should().ThrowAsync<ForbiddenException>();

        // 2. Assigned Analyst requests Cloud AIAnalysisRun -> persisted as Queued and creates AIAnalysisRequestedEvent in Outbox
        var analyzeHandler = new AnalyzeExistingMediaCommandHandler(
            new GenericRepository<Mission>(db),
            new InspectionMediaRepository(db),
            new GenericRepository<AIAnalysisRun>(db),
            new UnitOfWork(db),
            Mock.Of<IEventPublisher>(),
            userCtx,
            new GenericRepository<OutboxMessage>(db),
            NullLogger<AnalyzeExistingMediaCommandHandler>.Instance);

        var queued = await analyzeHandler.Handle(new AnalyzeExistingMediaCommand
        {
            MissionId = missionId,
            MediaId = mediaId,
            AnalysisType = AnalysisType.DefectDetection,
            PreferredModel = "RF-DETR"
        }, CancellationToken.None);

        queued.Status.Should().Be(AIAnalysisStatus.Queued);
        var runId = queued.Id;

        var outboxRequested = await db.OutboxMessages.SingleAsync(m => m.MessageType == nameof(AIAnalysisRequestedEvent));
        outboxRequested.Payload.Should().Contain(runId.ToString());

        // 3. AI Worker finishes and callback invokes ProcessAiAnalysisResultCommandHandler with raw label "raw_flashover"
        var evalClient = new Mock<IInspectionEvaluationClient>();
        evalClient
            .Setup(c => c.EvaluateAsync(It.IsAny<DetectionEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DetectionEvaluationResult(
                "Critical",
                "ImmediateDispatch",
                98,
                true,
                "High-confidence flashover on 500kV insulator"));

        var callbackHandler = new ProcessAiAnalysisResultCommandHandler(
            new GenericRepository<AIAnalysisRun>(db),
            new InspectionMediaRepository(db),
            new GenericRepository<DefectCategory>(db),
            new AnomalyRepository(db),
            new UnitOfWork(db),
            evalClient.Object,
            Mock.Of<IEventPublisher>(),
            new GenericRepository<OutboxMessage>(db),
            NullLogger<ProcessAiAnalysisResultCommandHandler>.Instance,
            settingService,
            new GenericRepository<EmergencyAlert>(db));

        var callbackResult = await callbackHandler.Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = runId,
            MediaId = mediaId,
            MissionId = missionId,
            Status = "Completed",
            ModelName = "RF-DETR",
            ModelVersion = "v2.1",
            ProcessingTimeMs = 840,
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "det-flashover-1",
                    CategoryCode = "raw_flashover", // Mapped via SystemSettingService ("AiLabel:raw_flashover" -> "WF")
                    Confidence = 0.94,
                    BoundingBox = new BoundingBoxDto { X = 0.15, Y = 0.25, Width = 0.30, Height = 0.35 }
                }
            },
            CompletedAt = DateTime.UtcNow
        }, CancellationToken.None);

        callbackResult.SavedDetections.Should().Be(1);
        callbackResult.CreatedAlerts.Should().Be(1);

        var savedRun = await db.AIAnalysisRuns.SingleAsync(r => r.Id == runId);
        savedRun.Status.Should().Be(AIAnalysisStatus.Completed);

        var anomaly = await db.DetectedAnomalies.SingleAsync(a => a.AnalysisRunId == runId);
        anomaly.CategoryId.Should().Be(5001);
        anomaly.ValidationStatus.Should().Be(DetectedAnomaly.StatusInReview);
        anomaly.SupersededById.Should().BeNull();

        var alert = await db.EmergencyAlerts.SingleAsync(a => a.AnomalyId == anomaly.Id);
        alert.Status.Should().Be("Open");
        alert.Priority.Should().Be("Critical");
    }

    // =========================================================================================================
    // PAIR B: Cloud Reanalysis Superseding + AssignAnomalyTower + ReviewMissionAiDetection + SLA SystemSettings
    // =========================================================================================================
    [Fact]
    public async Task PairB_CloudReanalysisSuperseding_TowerAssignment_AndAnalystReview_ShouldEnforceSupersededGuardsAndCreateSlaTicket()
    {
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var run1Id = Guid.NewGuid();
        var run2Id = Guid.NewGuid();

        var userCtx = UserContext(analystId, UserRoles.Analyst);
        await using var db = CreateContext(userCtx);

        var settingService = new SystemSettingService(db, NullLogger<SystemSettingService>.Instance);
        settingService.InvalidateCache();
        await settingService.UpdateAsync(SettingKeys.MaintenanceEmergencySlaDays, "3");

        db.DefectCategories.Add(new DefectCategory
        {
            Id = 6001,
            CategoryCode = "WF",
            CategoryName = "Wildfire / Flashover",
            Description = "Critical flashover hazard",
            SeverityWeight = 5.0,
            IsEmergencyClass = true
        });
        db.Towers.Add(new Tower
        {
            Id = towerId,
            RegionId = regionId,
            TowerCode = "T-500KV-99"
        });
        db.Missions.Add(new Mission
        {
            Id = missionId,
            MissionCode = "MS-AI-PAIR-B",
            Title = "Reanalysis & Review Pipeline",
            ManagerId = managerId,
            Status = "Completed"
        });
        db.MissionAssignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            UserId = analystId,
            AssignmentRole = "ANALYST",
            Status = "Active"
        });
        // Media initially has NO TowerId so we can test TOWER_REQUIRED and AssignAnomalyTowerCommandHandler
        db.InspectionMedia.Add(new InspectionMedia
        {
            Id = mediaId,
            MissionId = missionId,
            TowerId = null,
            MediaType = "Image",
            FileUrl = "https://minio.local/uav-media/unassigned-tower.jpg",
            CapturedAt = DateTime.UtcNow
        });
        db.AIAnalysisRuns.AddRange(
            new AIAnalysisRun
            {
                Id = run1Id,
                RequestedBy = analystId,
                MediaId = mediaId,
                ExecutionLocation = AIAnalysisRun.Cloud,
                AnalysisType = AnalysisType.DefectDetection,
                Status = AIAnalysisStatus.Processing,
                StartedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new AIAnalysisRun
            {
                Id = run2Id,
                RequestedBy = analystId,
                MediaId = mediaId,
                ExecutionLocation = AIAnalysisRun.Cloud,
                AnalysisType = AnalysisType.DefectDetection,
                Status = AIAnalysisStatus.Processing,
                StartedAt = DateTime.UtcNow.AddMinutes(-2)
            });
        await db.SaveChangesAsync();

        var callbackHandler = new ProcessAiAnalysisResultCommandHandler(
            new GenericRepository<AIAnalysisRun>(db),
            new InspectionMediaRepository(db),
            new GenericRepository<DefectCategory>(db),
            new AnomalyRepository(db),
            new UnitOfWork(db),
            Mock.Of<IInspectionEvaluationClient>(c => c.EvaluateAsync(
                It.IsAny<DetectionEvaluationRequest>(),
                It.IsAny<CancellationToken>()) == Task.FromResult(new DetectionEvaluationResult(
                    "Critical", "ImmediateDispatch", 95, true, "Critical flashover"))),
            Mock.Of<IEventPublisher>(),
            new GenericRepository<OutboxMessage>(db),
            NullLogger<ProcessAiAnalysisResultCommandHandler>.Instance,
            settingService,
            new GenericRepository<EmergencyAlert>(db));

        // 1. Run 1 completes -> creates run1Anomaly (InReview) + Open EmergencyAlert
        await callbackHandler.Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = run1Id,
            MediaId = mediaId,
            MissionId = missionId,
            Status = "Completed",
            ModelName = "RF-DETR",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "r1-det-1",
                    CategoryCode = "WF",
                    Confidence = 0.89,
                    BoundingBox = new BoundingBoxDto { X = 0.2, Y = 0.2, Width = 0.3, Height = 0.3 }
                }
            },
            CompletedAt = DateTime.UtcNow.AddMinutes(-9)
        }, CancellationToken.None);

        var run1Anomaly = await db.DetectedAnomalies.SingleAsync(a => a.AnalysisRunId == run1Id);

        // 2. Run 2 (Cloud Reanalysis) completes with overlapping box (IoU = 1.0 >= 0.5) -> supersedes run1Anomaly and transfers Open EmergencyAlert
        await callbackHandler.Handle(new ProcessAiAnalysisResultCommand
        {
            RequestId = run2Id,
            MediaId = mediaId,
            MissionId = missionId,
            Status = "Completed",
            ModelName = "RF-DETR",
            Detections = new List<DetectionDto>
            {
                new()
                {
                    Id = "r2-det-1",
                    CategoryCode = "WF",
                    Confidence = 0.97,
                    BoundingBox = new BoundingBoxDto { X = 0.2, Y = 0.2, Width = 0.3, Height = 0.3 }
                }
            },
            CompletedAt = DateTime.UtcNow.AddMinutes(-1)
        }, CancellationToken.None);

        var run2Anomaly = await db.DetectedAnomalies.SingleAsync(a => a.AnalysisRunId == run2Id);
        (await db.DetectedAnomalies.SingleAsync(a => a.Id == run1Anomaly.Id)).SupersededById.Should().Be(run2Anomaly.Id);

        var activeAlerts = await db.EmergencyAlerts.ToListAsync();
        activeAlerts.Should().ContainSingle();
        activeAlerts[0].AnomalyId.Should().Be(run2Anomaly.Id);

        // 3. GetMissionAiDetectionsQueryHandler returns ONLY the active run2Anomaly
        var detectionsQueryHandler = new GetMissionAiDetectionsQueryHandler(
            new InspectionMediaRepository(db),
            new GenericRepository<Mission>(db),
            new GenericRepository<MissionAssignment>(db),
            userCtx);
        var detectionsList = await detectionsQueryHandler.Handle(new GetMissionAiDetectionsQuery(missionId), CancellationToken.None);
        detectionsList.Should().ContainSingle();
        detectionsList[0].Detections.Select(d => d.Id).Should().Equal(run2Anomaly.Id);

        // 4. Attempting to assign tower or review the superseded run1Anomaly is blocked with ANOMALY_SUPERSEDED
        var assignTowerHandler = new AssignAnomalyTowerCommandHandler(
            new AnomalyRepository(db),
            new GenericRepository<Tower>(db),
            new UnitOfWork(db));

        var reviewHandler = new ReviewMissionAiDetectionCommandHandler(
            new AnomalyRepository(db),
            new UnitOfWork(db),
            userCtx,
            new GenericRepository<OutboxMessage>(db),
            Mock.Of<IEventPublisher>(),
            new GenericRepository<EmergencyAlert>(db),
            new GenericRepository<MaintenanceTicket>(db),
            new GenericRepository<Mission>(db),
            settingService);

        var assignSupersededAct = () => assignTowerHandler.Handle(
            new AssignAnomalyTowerCommand(run1Anomaly.Id, towerId), CancellationToken.None);
        (await assignSupersededAct.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("ANOMALY_SUPERSEDED");

        var reviewSupersededAct = () => reviewHandler.Handle(new ReviewMissionAiDetectionCommand
        {
            MissionId = missionId,
            DetectionId = run1Anomaly.Id,
            Decision = "Confirmed"
        }, CancellationToken.None);
        (await reviewSupersededAct.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("ANOMALY_SUPERSEDED");

        // 5. Attempting to confirm run2Anomaly before a tower is assigned -> rejected with TOWER_REQUIRED
        var confirmWithoutTowerAct = () => reviewHandler.Handle(new ReviewMissionAiDetectionCommand
        {
            MissionId = missionId,
            DetectionId = run2Anomaly.Id,
            Decision = "Confirmed"
        }, CancellationToken.None);
        (await confirmWithoutTowerAct.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("TOWER_REQUIRED");

        // 6. Analyst assigns tower to run2Anomaly via AssignAnomalyTowerCommandHandler, then confirms it -> creates MaintenanceTicket with 3-day SLA
        var assignedResult = await assignTowerHandler.Handle(
            new AssignAnomalyTowerCommand(run2Anomaly.Id, towerId), CancellationToken.None);
        assignedResult.TowerId.Should().Be(towerId);

        var confirmedDto = await reviewHandler.Handle(new ReviewMissionAiDetectionCommand
        {
            MissionId = missionId,
            DetectionId = run2Anomaly.Id,
            Decision = "Confirmed",
            Notes = "Confirmed flashover after reanalysis",
            OverrideSeverity = "Critical"
        }, CancellationToken.None);

        confirmedDto.ValidationStatus.Should().Be("Confirmed");
        confirmedDto.TowerId.Should().Be(towerId);
        confirmedDto.EmergencyAlertId.Should().Be(activeAlerts[0].Id.ToString());
        Guid.TryParse(confirmedDto.MaintenanceTaskId, out var ticketId).Should().BeTrue();

        var ticket = await db.MaintenanceTickets.SingleAsync(t => t.Id == ticketId);
        ticket.AnomalyId.Should().Be(run2Anomaly.Id);
        ticket.ManagerId.Should().Be(managerId);
        ticket.Status.Should().Be(MaintenanceTicket.StatusOpen);
        ticket.Priority.Should().Be(MaintenanceTicket.PriorityEmergency);
        ticket.DueDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(3), TimeSpan.FromMinutes(2));

        await settingService.UpdateAsync(SettingKeys.MaintenanceEmergencySlaDays, "1");
    }
}
