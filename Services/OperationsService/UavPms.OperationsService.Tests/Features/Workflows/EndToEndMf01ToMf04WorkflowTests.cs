using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.DeliverDroneHandover;
using UavPms.OperationsService.Application.Features.FieldExecution.Commands.FieldCheckIn;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.CompleteMaintenanceTicket;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.ReviewDetectedAnomaly;
using UavPms.OperationsService.Application.Features.Maintenance.Commands.UploadMaintenanceProof;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.Commands.AssignMissionMember;
using UavPms.OperationsService.Application.Features.Missions.Commands.DispatchMission;
using UavPms.OperationsService.Application.Features.Missions.Commands.LockResourceBooking;
using UavPms.OperationsService.Application.Features.Missions.Commands.RespondAssignment;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Workflows;

/// <summary>
/// GIAI ĐOẠN 5: SMOKE TEST KỊCH BẢN XUYÊN SUỐT (MF01 -> MF04)
/// Kịch bản E2E hoàn chỉnh:
/// 1. Manager đánh giá tiền khả thi & sẵn sàng tài nguyên (MF01)
/// 2. Tạo nhiệm vụ, phân công phi công, đặt lịch tài nguyên, chấp thuận & Dispatch (MF02)
/// 3. Phi công check-in GPS < 200m, nhận bàn giao drone trước bay, thực hiện bay và nạp media (MF03)
/// 4. AI phát hiện lỗi vỡ sứ, Analyst duyệt Critical, phát cảnh báo khẩn, tạo phiếu bảo dưỡng, nạp ảnh nghiệm thu & đóng phiếu (MF04)
/// </summary>
public class EndToEndMf01ToMf04WorkflowTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role, string username = "TestUser")
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role, UserRoles.AllAuthenticatedRoles, UserRoles.SystemAdmin });
        mock.SetupGet(u => u.Username).Returns(username);
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("E2E_Workflow_" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task SmokeTest_CompleteLifecycle_FromMf01ThroughMf04_SucceedsEndToEnd()
    {
        // =========================================================================
        // SETUP: Cấu hình cơ sở dữ liệu và người dùng hệ thống
        // =========================================================================
        var managerId = Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var analystId = Guid.NewGuid();
        var technicianId = Guid.NewGuid();

        var currentUserMock = CreateUserMock(managerId, UserRoles.Manager, "manager_hoang");
        await using var db = CreateContext(currentUserMock.Object);

        // Khởi tạo người dùng
        db.Users.AddRange(
            new User { Id = managerId, FullName = "Nguyễn Văn Hoàng (Manager)", Status = "Active" },
            new User { Id = pilotId, FullName = "Trần Đình Phi (Pilot)", Status = "Active" },
            new User { Id = analystId, FullName = "Lê Thị Lan (Analyst)", Status = "Active" },
            new User { Id = technicianId, FullName = "Phạm Văn Sửa (Technician)", Status = "Active" }
        );

        // Khởi tạo khu vực và cột điện mục tiêu (T-220kV tại tọa độ 10.8231, 106.6297)
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-HCM", Name = "TP. Hồ Chí Minh" };
        db.Regions.Add(region);

        var tower = new Tower
        {
            Id = Guid.NewGuid(),
            TowerCode = "TOWER-HCM-101",
            RegionId = region.Id,
            Geom = new Point(106.6297, 10.8231) { SRID = 4326 }
        };
        db.Towers.Add(tower);

        // Khởi tạo UAV và biên bản kiểm định kỹ thuật còn hạn (Passed)
        var uav = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "DJI-M300-01",
            Name = "Matrice 300 RTK",
            Model = "Matrice 300 RTK",
            FlightStatus = DroneStatus.Idle,
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy,
            AssignedRegionId = region.Id
        };
        db.Uavs.Add(uav);

        var inspection = new DroneTechnicalInspection
        {
            Id = Guid.NewGuid(),
            DroneId = uav.Id,
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            ValidUntil = DateTime.UtcNow.AddDays(28),
            TechnicianId = technicianId
        };
        db.DroneTechnicalInspections.Add(inspection);
        uav.LastTechnicalInspectionId = inspection.Id;
        await db.SaveChangesAsync();

        // =========================================================================
        // BƯỚC 1: MF01 - ĐÁNH GIÁ TIỀN KHẢ THI (PRE-MISSION ASSESSMENT)
        // =========================================================================
        var assessment = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready,
            CreatedAt = DateTime.UtcNow
        };
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel
        {
            AssessmentId = assessment.Id,
            UserId = pilotId,
            EligibilityStatus = ResourceEligibilityStatus.Eligible,
            AvailabilityStatus = ResourceAvailabilityStatus.Available
        });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone
        {
            AssessmentId = assessment.Id,
            DroneId = uav.Id,
            OperationalAvailabilityStatus = ResourceAvailabilityStatus.Available,
            TechnicalEligibilityStatus = ResourceEligibilityStatus.Eligible,
            Suitability = UavScopeSuitability.Suitable
        });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        // Xác nhận assessment ở trạng thái Ready
        assessment.Status.Should().Be(PreMissionAssessmentStatus.Ready);

        // =========================================================================
        // BƯỚC 2: MF02 - ĐIỀU PHỐI, ĐẶT LỊCH TÀI NGUYÊN & DISPATCH NHIỆM VỤ
        // =========================================================================
        var plannedStart = DateTime.UtcNow.AddHours(2);
        var plannedEnd = DateTime.UtcNow.AddHours(5);

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MSN-2026-MF02-01",
            Title = "Kiểm tra định kỳ tuyến đường dây 220kV Cột 101",
            RegionId = region.Id,
            ManagerId = managerId,
            UavId = uav.Id,
            PlannedStart = plannedStart,
            PlannedEnd = plannedEnd,
            Status = MissionStatus.Draft,
            PreMissionAssessmentId = assessment.Id,
            Version = 1
        };
        mission.MissionTargets.Add(new MissionTarget
        {
            MissionId = mission.Id,
            TowerId = tower.Id,
            Sequence = 1
        });
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        // 2.1. Phân công vai trò phi công
        var missionRepo = new MissionRepository(db);
        var uow = new UnitOfWork(db);
        var lifecycleService = new MissionLifecycleService(db, currentUserMock.Object);
        var assignHandler = new AssignMissionMemberCommandHandler(lifecycleService);

        var assignResult = await assignHandler.Handle(
            new AssignMissionMemberCommand(mission.Id, pilotId, "Pilot"), CancellationToken.None);
        assignResult.Should().NotBeNull();
        assignResult.UserId.Should().Be(pilotId);

        // 2.2. Phi công phản hồi chấp thuận (Accept assignment)
        var pilotUserMock = CreateUserMock(pilotId, UserRoles.Inspector, "pilot_phi");
        var pilotLifecycleService = new MissionLifecycleService(db, pilotUserMock.Object);
        var respondHandler = new RespondAssignmentCommandHandler(pilotLifecycleService);

        var respondResult = await respondHandler.Handle(
            new RespondAssignmentCommand(mission.Id, true, "Sẵn sàng nhận bay."), CancellationToken.None);
        respondResult.Should().NotBeNull();
        respondResult.ResponseStatus.Should().Be("Accepted");

        // 2.3. Khóa lịch tài nguyên (Anti-double-booking)
        var bookingRepo = new GenericRepository<ResourceBooking>(db);
        var lockBookingHandler = new LockResourceBookingCommandHandler(bookingRepo, uow);

        var bookingResult = await lockBookingHandler.Handle(
            new LockResourceBookingCommand(mission.Id, pilotId, uav.Id, plannedStart, plannedEnd),
            CancellationToken.None);
        bookingResult.Should().NotBeNull();
        bookingResult.Status.Should().Be("Active");

        // 2.4. Dispatch nhiệm vụ sang Assigned
        var uavRepo = new UavRepository(db);
        var inspectionRepo = new GenericRepository<DroneTechnicalInspection>(db);
        var dispatchHandler = new DispatchMissionCommandHandler(
            missionRepo, uavRepo, inspectionRepo, bookingRepo, uow);

        var dispatchResult = await dispatchHandler.Handle(
            new DispatchMissionCommand(mission.Id), CancellationToken.None);
        dispatchResult.Status.Should().Be("Assigned");

        var updatedMission = await db.Missions.FindAsync(mission.Id);
        updatedMission!.Status.Should().Be(MissionStatus.Assigned);

        // =========================================================================
        // BƯỚC 3: MF03 - BÀN GIAO DRONE, CHECK-IN HIỆN TRƯỜNG & NẠP MEDIA
        // =========================================================================
        var accessFilter = new GeographicAccessFilter(db, pilotUserMock.Object);
        var towerRepo = new TowerRepository(db, accessFilter);
        var checkInRepo = new GenericRepository<MissionCheckIn>(db);

        // 3.1. Phi công Check-in GPS (< 200m từ cột T-101)
        var checkInHandler = new FieldCheckInCommandHandler(
            missionRepo, towerRepo, checkInRepo, pilotUserMock.Object, uow);

        // Tọa độ phi công cách cột ~50m (10.8234, 106.6299)
        var checkInResult = await checkInHandler.Handle(
            new FieldCheckInCommand(mission.Id, 10.8234, 106.6299), CancellationToken.None);
        checkInResult.Status.Should().Be("CheckedIn");

        // 3.2. Bàn giao Drone trước bay (Pre-flight handover checklist JSONB)
        var handoverRepo = new GenericRepository<DroneHandover>(db);
        var deliverHandoverHandler = new DeliverDroneHandoverCommandHandler(
            missionRepo, uavRepo, handoverRepo, pilotUserMock.Object, uow);

        var checklistJson = "{\"battery\": 98, \"propellers\": \"Intact\", \"camera\": \"Calibrated\", \"compass\": \"Calibrated\"}";
        var handoverResult = await deliverHandoverHandler.Handle(
            new DeliverDroneHandoverCommand(mission.Id, uav.Id, pilotId, "PreFlightCheck", checklistJson),
            CancellationToken.None);
        handoverResult.Status.Should().Be("Accepted");

        // Chuyển trạng thái mission sang InProgress
        updatedMission.Status = MissionStatus.InProgress;
        await db.SaveChangesAsync();

        // 3.3. Nạp ảnh kiểm tra bay (InspectionMedia) liên kết với tower.Id
        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            TowerId = tower.Id,
            MediaType = "Image",
            FileUrl = "https://minio.evn.vn/inspections/flight-2026/img_tower101_01.jpg",
            CapturedAt = DateTime.UtcNow,
            CaptureLocation = new Point(106.6297, 10.8231) { SRID = 4326 }
        };
        db.InspectionMedia.Add(media);
        await db.SaveChangesAsync();

        media.TowerId.Should().Be(tower.Id);

        // =========================================================================
        // BƯỚC 4: MF04 - THẨM ĐỊNH LỖI, CẢNH BÁO KHẨN CẤP & BẢO TRÌ SỬA CHỮA
        // =========================================================================
        // 4.1. AI phát hiện bất thường (Vỡ bát sứ cách điện - Insulator Defect)
        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            TowerId = tower.Id,
            CategoryId = 1,
            ConfidenceScore = 0.94,
            BoundingBox = "{\"x\": 120, \"y\": 240, \"width\": 80, \"height\": 95}",
            ValidationStatus = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DetectedAnomalies.Add(anomaly);
        await db.SaveChangesAsync();

        // 4.2. Analyst thẩm định lỗi mức Critical có phản hồi JSONB
        var analystMock = CreateUserMock(analystId, UserRoles.Analyst, "analyst_lan");
        var anomalyRepo = new AnomalyRepository(db);
        var alertRepo = new GenericRepository<EmergencyAlert>(db);
        var ticketRepo = new GenericRepository<MaintenanceTicket>(db);

        var reviewHandler = new ReviewDetectedAnomalyCommandHandler(
            anomalyRepo, missionRepo, towerRepo, alertRepo, ticketRepo, analystMock.Object, uow);

        var analystFeedback = "{\"rootCause\": \"Sét đánh gây vỡ bát sứ số 3\", \"riskLevel\": \"Critical\"}";
        var reviewResult = await reviewHandler.Handle(
            new ReviewDetectedAnomalyCommand(mission.Id, anomaly.Id, "Confirmed", "Nguy cơ phóng điện cực kỳ cao", analystFeedback, "Critical"),
            CancellationToken.None);

        reviewResult.Status.Should().Be("Confirmed");

        // Phát sinh cảnh báo khẩn cấp; cột điện của cảnh báo suy ra qua Anomaly.TowerId
        var emergencyAlert = await db.EmergencyAlerts.FirstOrDefaultAsync(a => a.Anomaly!.TowerId == tower.Id);
        emergencyAlert.Should().NotBeNull();
        emergencyAlert!.Priority.Should().Be("Emergency");

        // 4.3. Quản lý tạo phiếu bảo trì sửa chữa cột điện
        var userRepo = new UserRepository(db);
        var createTicketHandler = new CreateMaintenanceTicketCommandHandler(
            ticketRepo, towerRepo, anomalyRepo, userRepo, currentUserMock.Object, uow);

        var ticketResult = await createTicketHandler.Handle(
            new CreateMaintenanceTicketCommand(
                anomaly.Id,
                technicianId,
                "Emergency",
                "Sửa chữa thay thế chuỗi bát sứ cách điện bị vỡ tại cột T-101"),
            CancellationToken.None);

        ticketResult.Should().NotBeNull();
        ticketResult.Status.Should().Be("Open");
        ticketResult.TowerId.Should().Be(tower.Id);

        // 4.4. Thợ kỹ thuật tải ảnh minh chứng sau sửa chữa (ProofType = "After")
        var proofRepo = new GenericRepository<MaintenanceProof>(db);
        var fileStorageMock = new Mock<IFileStorageService>();
        fileStorageMock
            .Setup(f => f.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.evn.vn/maintenance/proof-tower101-after.jpg");

        var techMock = CreateUserMock(technicianId, UserRoles.Technician, "tech_sua");
        var uploadProofHandler = new UploadMaintenanceProofCommandHandler(
            ticketRepo, proofRepo, fileStorageMock.Object, techMock.Object, uow);

        var dummyStream = new MemoryStream(Encoding.UTF8.GetBytes("fake_jpg_content"));
        var formFile = new FormFile(dummyStream, 0, dummyStream.Length, "file", "repair_proof_after.jpg");

        var proofResult = await uploadProofHandler.Handle(
            new UploadMaintenanceProofCommand(
                ticketResult.Id,
                "After",
                formFile,
                "Đã thay thế chuỗi bát sứ mới 220kV, đo điện trở cách điện đạt chuẩn."),
            CancellationToken.None);

        proofResult.Should().NotBeNull();
        proofResult.ProofType.Should().Be("After");

        // 4.5. Đóng phiếu bảo trì
        var completeTicketHandler = new CompleteMaintenanceTicketCommandHandler(ticketRepo, proofRepo, uow);

        var completedTicket = await completeTicketHandler.Handle(
            new CompleteMaintenanceTicketCommand(ticketResult.Id, "Nghiệm thu hoàn tất đạt tiêu chuẩn an toàn."),
            CancellationToken.None);

        completedTicket.Status.Should().Be("Closed");

        // =========================================================================
        // KIỂM TRA ĐÍCH CUỐI: phiếu bảo trì của cột đã đóng (cột suy ra qua Anomaly.TowerId)
        // =========================================================================
        var finalTicket = await db.MaintenanceTickets
            .Include(t => t.Anomaly)
            .FirstAsync(t => t.Id == ticketResult.Id);
        finalTicket.Status.Should().Be(TicketStatus.Closed);
        finalTicket.ResolvedAt.Should().NotBeNull();
        finalTicket.Anomaly!.TowerId.Should().Be(tower.Id);
    }
}
