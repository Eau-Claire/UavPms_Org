using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionLinearWorkflowTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role, string username = "TestUser")
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        mock.SetupGet(u => u.Username).Returns(username);
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task GetMissionDetectionsAsync_WhenNoMediaOrAnomalies_ShouldReturnEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUserMock(userId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = userId, FullName = "Lê Quang Minh", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DET-01",
            Title = "Mission New - No AI Results",
            ManagerId = userId,
            Status = MissionStatus.PendingAcceptance
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Act
        var result = await service.GetMissionDetectionsAsync(mission.Id, null, null, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMissionDetectionsAsync_WithAnomalies_ShouldMapAndFilterCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUserMock(userId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = userId, FullName = "Lê Quang Minh", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DET-02",
            Title = "Mission Executed",
            ManagerId = userId,
            Status = MissionStatus.Completed
        };
        db.Missions.Add(mission);

        var media1 = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            MediaType = "Video",
            FileUrl = "https://minio.evn.vn/media/flight-video.mp4"
        };
        var media2 = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            MediaType = "Image",
            FileUrl = "https://minio.evn.vn/media/crop-01.jpg"
        };
        db.InspectionMedia.AddRange(media1, media2);

        var categoryCrack = new DefectCategory
        {
            Id = 1,
            CategoryCode = "DEF-INS-CRACK",
            CategoryName = "Bát sứ nứt vỡ",
            SeverityWeight = 5.0,
            IsEmergencyClass = true,
            Description = "Vết nứt bề mặt đĩa sứ"
        };
        var categoryRust = new DefectCategory
        {
            Id = 2,
            CategoryCode = "DEF-RUST",
            CategoryName = "Gỉ sét xà néo",
            SeverityWeight = 2.0,
            IsEmergencyClass = false,
            Description = "Vết gỉ bề mặt"
        };
        db.DefectCategories.AddRange(categoryCrack, categoryRust);

        var tower = new Tower { Id = Guid.NewGuid(), TowerCode = "Cột 042 (Néo)" };
        db.Towers.Add(tower);

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            TowerId = tower.Id,
            Tower = tower,
            AssetCode = "INS-220KV-042",
            CurrentHealthScore = 90.0,
            RiskLevel = "Low"
        };
        db.Assets.Add(asset);

        var anomaly1 = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media1.Id,
            CategoryId = categoryCrack.Id,
            Category = categoryCrack,
            AssetId = asset.Id,
            Asset = asset,
            ConfidenceScore = 0.945,
            ValidationStatus = "Confirmed",
            BoundingBox = "{\"x\": 36.5, \"y\": 28.2, \"width\": 24.0, \"height\": 30.5}",
            Timestamp = 74.5,
            FrameIndex = 2235,
            CropUrl = "https://minio.evn.vn/media/crop-crack.jpg"
        };
        var anomaly2 = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media2.Id,
            CategoryId = categoryRust.Id,
            Category = categoryRust,
            AssetId = asset.Id,
            Asset = asset,
            ConfidenceScore = 0.88,
            ValidationStatus = "Rejected",
            BoundingBox = "{\"x\": 10.0, \"y\": 15.0, \"width\": 20.0, \"height\": 25.0}"
        };
        db.DetectedAnomalies.AddRange(anomaly1, anomaly2);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Act 1: Get all
        var all = await service.GetMissionDetectionsAsync(mission.Id, null, null, null, CancellationToken.None);
        all.Should().HaveCount(2);

        var det1 = all.First(d => d.Id == anomaly1.Id.ToString());
        det1.Title.Should().Be("Bát sứ nứt vỡ");
        det1.Confidence.Should().Be(94.5);
        det1.CategoryCode.Should().Be("DEF-INS-CRACK");
        det1.IsEmergency.Should().BeTrue();
        det1.Status.Should().Be("Approved");
        det1.BoundingBox.Should().NotBeNull();
        det1.BoundingBox!.X.Should().Be(36.5);
        det1.BoundingBox.Width.Should().Be(24.0);
        det1.TimestampLabel.Should().Be("01:14"); // 74.5s = 1m 14s

        // Act 2: Filter by status "Approved"
        var approvedOnly = await service.GetMissionDetectionsAsync(mission.Id, "Approved", null, null, CancellationToken.None);
        approvedOnly.Should().HaveCount(1);
        approvedOnly[0].Id.Should().Be(anomaly1.Id.ToString());

        // Act 3: Filter by mediaType "Image"
        var imageOnly = await service.GetMissionDetectionsAsync(mission.Id, null, "Image", null, CancellationToken.None);
        imageOnly.Should().HaveCount(1);
        imageOnly[0].Id.Should().Be(anomaly2.Id.ToString());

        // Act 4: Filter by isEmergency true
        var emergencyOnly = await service.GetMissionDetectionsAsync(mission.Id, null, null, true, CancellationToken.None);
        emergencyOnly.Should().HaveCount(1);
        emergencyOnly[0].CategoryCode.Should().Be("DEF-INS-CRACK");
    }

    [Fact]
    public async Task ReviewDetectionAsync_WhenApproved_ShouldDeductHealthScoreAndCreateMaintenanceTicket()
    {
        // Arrange
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(analystId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = analystId, FullName = "Lê Quang Minh", Status = "Active" });
        db.Users.Add(new User { Id = managerId, FullName = "Trần Quản Lý", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-REV-01",
            Title = "Mission AI Review",
            ManagerId = managerId,
            Status = MissionStatus.Completed
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = analystId,
            AssignmentRole = "ANALYST",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted
        });
        db.Missions.Add(mission);

        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            MediaType = "Image",
            FileUrl = "https://minio.evn.vn/media/img1.jpg"
        };
        db.InspectionMedia.Add(media);

        var category = new DefectCategory
        {
            Id = 10,
            CategoryCode = "DEF-INS-BROKEN",
            CategoryName = "Vỡ đĩa sứ",
            SeverityWeight = 5.0,
            IsEmergencyClass = true
        };
        db.DefectCategories.Add(category);

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "INS-001",
            CurrentHealthScore = 95.0,
            RiskLevel = "Low"
        };
        db.Assets.Add(asset);

        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            CategoryId = category.Id,
            Category = category,
            AssetId = asset.Id,
            Asset = asset,
            ValidationStatus = "Pending"
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var request = new ReviewDetectionRequest(
            Status: "Approved",
            ReviewNotes: "Xác nhận nứt vỡ nguy hiểm cần thay thế.",
            OverrideSeverity: "Critical Risk");

        // Act
        var result = await service.ReviewDetectionAsync(mission.Id, anomaly.Id, request, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Approved");
        result.MaintenanceTaskId.Should().NotBeNullOrEmpty();
        result.NewAssetHealthScore.Should().BeLessThan(95.0);

        // Kiểm tra anomaly trong DB
        var updatedAnomaly = await db.DetectedAnomalies.FindAsync(anomaly.Id);
        updatedAnomaly!.ValidationStatus.Should().Be("Confirmed");
        updatedAnomaly.AnalystId.Should().Be(analystId);
        updatedAnomaly.AnalystNotes.Should().Be("Xác nhận nứt vỡ nguy hiểm cần thay thế.");

        // Kiểm tra asset health history trong DB
        var history = await db.AssetHealthHistories.FirstOrDefaultAsync(h => h.AssetId == asset.Id);
        history.Should().NotBeNull();
        history!.HealthScore.Should().Be(result.NewAssetHealthScore!.Value);

        // Kiểm tra MaintenanceTicket được tự động sinh
        var ticket = await db.MaintenanceTickets.FirstOrDefaultAsync(t => t.AnomalyId == anomaly.Id);
        ticket.Should().NotBeNull();
        ticket!.Status.Should().Be(TicketStatus.Open);
        ticket.Priority.Should().Be(TicketPriority.Emergency);
    }

    [Fact]
    public async Task ReviewDetectionAsync_WhenRejected_ShouldNotDeductHealthScoreAndNotCreateTicket()
    {
        // Arrange
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(analystId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = analystId, FullName = "Lê Quang Minh", Status = "Active" });
        db.Users.Add(new User { Id = managerId, FullName = "Trần Quản Lý", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-REV-02",
            Title = "Mission AI Review Reject",
            ManagerId = managerId,
            Status = MissionStatus.Completed
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = analystId,
            AssignmentRole = "ANALYST",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted
        });
        db.Missions.Add(mission);

        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            MediaType = "Image",
            FileUrl = "https://minio.evn.vn/media/img2.jpg"
        };
        db.InspectionMedia.Add(media);

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            AssetCode = "INS-002",
            CurrentHealthScore = 90.0,
            RiskLevel = "Low"
        };
        db.Assets.Add(asset);

        var category = new DefectCategory
        {
            Id = 20,
            CategoryCode = "DEF-INS-GLARE",
            CategoryName = "Lóa sáng",
            SeverityWeight = 1.0,
            IsEmergencyClass = false
        };
        db.DefectCategories.Add(category);

        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            Media = media,
            CategoryId = category.Id,
            Category = category,
            AssetId = asset.Id,
            Asset = asset,
            ValidationStatus = "Pending"
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        var request = new ReviewDetectionRequest(
            Status: "Rejected",
            ReviewNotes: "Ảnh bị lóa sáng do góc chụp mặt trời, không phải khuyết tật.");

        // Act
        var result = await service.ReviewDetectionAsync(mission.Id, anomaly.Id, request, CancellationToken.None);

        // Assert
        result.Status.Should().Be("Rejected");
        result.MaintenanceTaskId.Should().BeNull();

        var updatedAsset = await db.Assets.FindAsync(asset.Id);
        updatedAsset!.CurrentHealthScore.Should().Be(90.0);

        var ticketCount = await db.MaintenanceTickets.CountAsync(t => t.AnomalyId == anomaly.Id);
        ticketCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMissionMaintenanceTasksAsync_WhenApprovedAnomaliesExist_ShouldReturnTasks()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = CreateUserMock(userId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = userId, FullName = "Lê Quang Minh", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-MAINT-01",
            Title = "Mission Maintenance Check",
            ManagerId = userId,
            Status = MissionStatus.Completed
        };
        db.Missions.Add(mission);

        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            MediaType = "Image"
        };
        db.InspectionMedia.Add(media);

        var tower = new Tower { Id = Guid.NewGuid(), TowerCode = "Cột 042" };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, AssetCode = "INS-220KV-042-PHA-B" };
        db.Towers.Add(tower);
        db.Assets.Add(asset);

        var category = new DefectCategory
        {
            Id = 5,
            CategoryCode = "DEF-INS-CRACK",
            CategoryName = "Bát sứ nứt vỡ",
            Description = "Bát sứ số 4 chuỗi néo bị nứt vỡ"
        };
        db.DefectCategories.Add(category);

        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            Media = media,
            CategoryId = category.Id,
            Category = category,
            AssetId = asset.Id,
            Asset = asset,
            ValidationStatus = "Confirmed"
        };
        db.DetectedAnomalies.Add(anomaly);

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = "MT-20260925-001",
            AnomalyId = anomaly.Id,
            Anomaly = anomaly,
            AssetId = asset.Id,
            Asset = asset,
            Priority = TicketPriority.Emergency,
            Status = TicketStatus.Open,
            Description = "Thay thế khẩn cấp bát sứ nứt vỡ chuỗi néo pha B"
        };
        db.MaintenanceTickets.Add(ticket);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Act
        var result = await service.GetMissionMaintenanceTasksAsync(mission.Id, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].DetectionId.Should().Be(anomaly.Id.ToString());
        result[0].Priority.Should().Be("Urgent");
        result[0].Status.Should().Be("Pending");
        result[0].TowerCode.Should().Be("Cột 042");
        result[0].AssetCode.Should().Be("INS-220KV-042-PHA-B");
    }

    [Fact]
    public async Task AddActivityAsync_And_GetActivitiesAsync_ShouldRecordAndRetrieveActivities()
    {
        // Arrange
        var inspectorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(inspectorId, UserRoles.Inspector, "Nguyễn Văn Bay");
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = inspectorId, FullName = "Nguyễn Văn Bay", Status = "Active" });
        db.Users.Add(new User { Id = managerId, FullName = "Trần Quản Lý", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-ACT-01",
            Title = "Mission Activity Stream",
            ManagerId = managerId,
            InspectorId = inspectorId,
            Status = MissionStatus.Assigned
        };
        mission.Assignments.Add(new MissionAssignment
        {
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, user.Object, notifierMock.Object);

        var request = new CreateMissionActivityRequest(
            Content: "Thời tiết hiện trường tại cột 42 gió cấp 3, tầm nhìn tốt, sẵn sàng cất cánh.",
            SenderRole: "INSPECTOR");

        // Act 1: Add Activity
        var created = await service.AddActivityAsync(mission.Id, request, CancellationToken.None);

        // Assert 1
        created.Content.Should().Be("Thời tiết hiện trường tại cột 42 gió cấp 3, tầm nhìn tốt, sẵn sàng cất cánh.");
        created.SenderRole.Should().Be("INSPECTOR");
        created.SenderName.Should().Be("Nguyễn Văn Bay");
        notifierMock.Verify(n => n.NotifyAsync(It.Is<MissionLifecycleEventDto>(e => e.Type == "COMMUNICATION"), It.IsAny<CancellationToken>()), Times.Once);

        // Act 2: Get Activities
        var list = await service.GetActivitiesAsync(mission.Id, CancellationToken.None);
        list.Should().HaveCount(1);
        list[0].Content.Should().Be(created.Content);
        list[0].SenderUserId.Should().Be(inspectorId.ToString());
    }

    [Fact]
    public async Task GetAssignmentsOverviewAsync_ShouldCalculateRequiredAndConfirmedCounts()
    {
        // Arrange
        var managerId = Guid.NewGuid();
        var userPilot = new User { Id = Guid.NewGuid(), FullName = "Phạm Văn Hùng", Status = "Active", Email = "pilot_hung@evn.vn" };
        var userAnalyst = new User { Id = Guid.NewGuid(), FullName = "Lê Quang Minh", Status = "Active", Email = "analyst_minh@evn.vn" };
        var userTech = new User { Id = Guid.NewGuid(), FullName = "Trần Anh Tuấn", Status = "Active", Email = "tech_tuan@evn.vn" };

        var user = CreateUserMock(managerId, UserRoles.Manager, "manager_duc");
        await using var db = CreateContext(user.Object);

        db.Users.AddRange(userPilot, userAnalyst, userTech, new User { Id = managerId, FullName = "Quản lý", Status = "Active" });

        var deadline = DateTime.UtcNow.AddHours(12);
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-ASSIGN-01",
            Title = "Mission Multi-Role Matrix",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance,
            ConfirmationDeadline = deadline
        };

        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = userPilot.Id,
            User = userPilot,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted,
            IsRequired = true,
            AssignedAt = DateTime.UtcNow.AddHours(-2),
            RespondedAt = DateTime.UtcNow.AddHours(-1)
        });

        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = userAnalyst.Id,
            User = userAnalyst,
            AssignmentRole = "ANALYST",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true,
            AssignedAt = DateTime.UtcNow.AddHours(-2)
        });

        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = userTech.Id,
            User = userTech,
            AssignmentRole = "TECHNICIAN",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true,
            AssignedAt = DateTime.UtcNow.AddHours(-2)
        });

        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new MissionLifecycleService(db, user.Object);

        // Act
        var overview = await service.GetAssignmentsOverviewAsync(mission.Id, CancellationToken.None);

        // Assert
        overview.MissionId.Should().Be(mission.Id.ToString());
        overview.TotalRequiredCount.Should().Be(3);
        overview.ConfirmedCount.Should().Be(1);
        overview.AllConfirmed.Should().BeFalse();
        overview.ConfirmationDeadline.Should().Be(deadline);
        overview.Assignments.Should().HaveCount(3);

        var pilotItem = overview.Assignments.First(a => a.UserId == userPilot.Id.ToString());
        pilotItem.UserFullName.Should().Be("Phạm Văn Hùng");
        pilotItem.ResponseStatus.Should().Be("Accepted");
        pilotItem.AssignmentRole.Should().Be("INSPECTOR");
    }

    [Fact]
    public async Task AcceptAssignmentAsync_WhenAllRequiredRolesAccepted_ShouldAutoConfirmMissionAndEmitRealtimeEvent()
    {
        // Arrange
        var pilotId = Guid.NewGuid();
        var analystId = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var userAnalystMock = CreateUserMock(analystId, UserRoles.Analyst, "analyst_minh");
        await using var db = CreateContext(userAnalystMock.Object);

        db.Users.Add(new User { Id = pilotId, FullName = "Phi công", Status = "Active" });
        db.Users.Add(new User { Id = analystId, FullName = "Chuyên viên phân tích", Status = "Active" });
        db.Users.Add(new User { Id = managerId, FullName = "Quản lý", Status = "Active" });

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-AUTO-CONFIRM-01",
            Title = "Mission Multi-Role Auto Confirm",
            ManagerId = managerId,
            Status = MissionStatus.PendingAcceptance
        };

        // Pilot đã accept từ trước
        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = pilotId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Accepted,
            IsRequired = true
        });

        // Analyst đang pending, chuẩn bị accept
        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = analystId,
            AssignmentRole = "ANALYST",
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true
        });

        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var notifierMock = new Mock<IMissionRealtimeNotifier>();
        var service = new MissionLifecycleService(db, userAnalystMock.Object, notifierMock.Object);

        // Act: Analyst chấp nhận phân công cuối cùng
        var assignmentResult = await service.AcceptAssignmentAsync(mission.Id, CancellationToken.None);

        // Assert
        assignmentResult.ResponseStatus.Should().Be(MissionAssignmentResponse.Accepted);

        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(MissionStatus.Assigned); // Tự động chuyển sang Assigned (CONFIRMED)
        updatedMission.AcceptedAt.Should().NotBeNull();

        // Kiểm tra realtime event CONFIRMED được broadcast
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<MissionLifecycleEventDto>(e => e.MissionId == mission.Id.ToString() && e.Type == "CONFIRMED"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
