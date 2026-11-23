using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Alerts.DTOs;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Bottom-Up Modular Integration Tests:
/// Combines small, focused pairs/triads of modules step-by-step from foundation (Level 1) up to cross-module
/// workflows (Level 4), verifying exact State Machine transitions, SystemSettings rules, and DB persistence.
/// Runs against real PostgreSQL/PostGIS when Docker is available (e.g. CI), and falls back to a shared InMemory
/// store when Docker is unavailable locally so the integration logic is always executed.
/// </summary>
[Collection(PostgresCollection.Name)]
public class BottomUpModularIntegrationTests
{
    private readonly PostgresFixture _pg;

    public BottomUpModularIntegrationTests(PostgresFixture pg) => _pg = pg;

    private sealed class TestDatabaseScope : IAsyncDisposable
    {
        private readonly PostgresFixture _pg;
        private readonly string _inMemoryDbName = $"bottom-up-it-{Guid.NewGuid():N}";

        public bool IsPostgres => _pg.Available;

        public TestDatabaseScope(PostgresFixture pg) => _pg = pg;

        public ApplicationDbContext CreateContext(ICurrentUserServices? user = null)
        {
            if (_pg.Available)
            {
                return _pg.CreateContext(user);
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_inMemoryDbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new ApplicationDbContext(options, user ?? Mock.Of<ICurrentUserServices>());
        }

        public async Task<Guid> SeedUserAsync(string tag, string role, Guid? regionId = null)
        {
            await using var db = CreateContext();
            Guid userId;
            if (_pg.Available)
            {
                userId = await Mf01Seed.InsertUserAsync(db, tag, role);
            }
            else
            {
                userId = Guid.NewGuid();
                var roleEntity = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == role);
                if (roleEntity == null)
                {
                    roleEntity = new Role { Id = db.Roles.Local.Count + 10 + Math.Abs(role.GetHashCode() % 1000), RoleName = role, Description = role };
                    db.Roles.Add(roleEntity);
                }

                var user = new User
                {
                    Id = userId,
                    FullName = $"{role} {tag}",
                    Email = $"{role.ToLowerInvariant()}-{userId:N}@{tag}.it.test",
                    Phone = string.Empty,
                    Status = "Active",
                    IsEmailVerified = true
                };
                db.Users.Add(user);
                db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleEntity.Id, Role = roleEntity, User = user });
                await db.SaveChangesAsync();
            }

            if (regionId.HasValue)
            {
                db.UserGeographicScopes.Add(new UserGeographicScope { UserId = userId, RegionId = regionId.Value });
                await db.SaveChangesAsync();
            }

            return userId;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ICurrentUserServices UserContext(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        mock.SetupGet(u => u.Username).Returns(role);
        return mock.Object;
    }

    // =========================================================================================================
    // LEVEL 1 (MF01): SystemSettingService + DroneTechnicalInspectionService + PreMissionAssessmentService
    // =========================================================================================================
    [Fact]
    public async Task Level1_Mf01_TechnicalInspectionAndSystemSettings_ShouldUnlockAssessmentReadiness()
    {
        await using var scope = new TestDatabaseScope(_pg);
        var tag = Mf01Seed.Tag();

        Guid regionId, tower1Id, tower2Id, droneId;
        await using (var db = scope.CreateContext())
        {
            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
            var t1 = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}-1", Geom = new Point(105.85, 21.03) { SRID = 4326 } };
            var t2 = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}-2", Geom = new Point(105.851, 21.031) { SRID = 4326 } };
            var drone = new Uav
            {
                Id = Guid.NewGuid(),
                UavCode = $"UAV-{tag}",
                Name = "M300",
                Model = "DJI M300",
                SerialNumber = $"SN-{tag}",
                AssignedRegionId = region.Id,
                OperationalStatus = DroneOperationalStatus.Available,
                TechnicalHealth = TechnicalHealth.Unknown, // Not yet inspected!
                FlightEnduranceMinutes = 45,
                OperationalRangeKm = 12,
                BatteryLevel = 92
            };
            regionId = region.Id;
            tower1Id = t1.Id;
            tower2Id = t2.Id;
            droneId = drone.Id;

            db.AddRange(region, t1, t2, drone);
            await db.SaveChangesAsync();
        }

        var managerId = await scope.SeedUserAsync(tag, UserRoles.Manager, regionId);
        await scope.SeedUserAsync(tag, UserRoles.Inspector, regionId);
        await scope.SeedUserAsync(tag, UserRoles.Analyst, regionId);
        var technicianId = await scope.SeedUserAsync(tag, UserRoles.Technician, regionId);

        // 1. Configure SystemSettings: Assessment:ValidityHours = 168 (7 days) & Drone:InspectionValidityDays = 7
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var settingService = new SystemSettingService(db, NullLogger<SystemSettingService>.Instance);
            await settingService.UpdateAsync(SettingKeys.AssessmentValidityHours, "168");
            await settingService.UpdateAsync(SettingKeys.DroneInspectionValidityDays, "7");
        }

        // 2. Manager creates PreMissionAssessment before drone is inspected -> Status must be NotReady
        var plannedStart = DateTime.UtcNow.AddHours(2);
        var plannedEnd = plannedStart.AddHours(4);
        Guid assessmentId;
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var assessmentService = new PreMissionAssessmentService(db, UserContext(managerId, UserRoles.Manager));
            var assessment = await assessmentService.CreateAsync(
                regionId, plannedStart, plannedEnd, new[] { tower1Id, tower2Id }, CancellationToken.None);

            assessmentId = assessment.Id;
            assessment.Status.Should().Be(PreMissionAssessmentStatus.NotReady);
            assessment.ValidUntil.Should().BeCloseTo(DateTime.UtcNow.AddHours(168), TimeSpan.FromMinutes(2));
        }

        // 3. Technician submits passing BIST technical inspection -> UAV health becomes Healthy (valid for 7 days)
        await using (var db = scope.CreateContext(UserContext(technicianId, UserRoles.Technician)))
        {
            var settingService = new SystemSettingService(db, NullLogger<SystemSettingService>.Instance);
            var inspectionService = new DroneTechnicalInspectionService(
                db, UserContext(technicianId, UserRoles.Technician), settingService);

            var inspection = await inspectionService.SubmitInspectionAsync(new DroneInspectionSubmitRequest(
                droneId,
                "BIST pre-flight check passed",
                new List<DroneMetricSubmitDto>
                {
                    new("BATTERY_HEALTH", "Power", 92m, Passed: true, Critical: true, Severity: "High"),
                    new("RF_LINK_QUALITY", "Comms", 88m, Passed: true, Critical: true, Severity: "High")
                },
                PreMissionAssessmentId: assessmentId), CancellationToken.None);

            inspection.Status.Should().Be(DroneTechnicalInspectionStatus.Passed);
            inspection.Health.Should().Be(TechnicalHealth.Healthy);
            inspection.ValidUntil.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(2));
        }

        // 4. Manager re-evaluates PreMissionAssessment -> Status transitions NotReady -> Ready
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var assessmentService = new PreMissionAssessmentService(db, UserContext(managerId, UserRoles.Manager));
            var evaluated = await assessmentService.EvaluateAsync(assessmentId, CancellationToken.None);

            evaluated.Status.Should().Be(PreMissionAssessmentStatus.Ready);
            evaluated.OverallTechnicalHealth.Should().Be(TechnicalHealth.Healthy);
            evaluated.DroneCandidates.Should().Contain(d => d.DroneId == droneId && d.IsEligible);
        }
    }

    // =========================================================================================================
    // LEVEL 2 (MF01 -> MF02): PreMissionAssessmentService + MissionLifecycleService + ResourceBookings
    // =========================================================================================================
    [Fact]
    public async Task Level2_Mf01ToMf02_CreateMissionFromAssessment_AndMemberAcceptance_ShouldTransitionToConfirmed()
    {
        await using var scope = new TestDatabaseScope(_pg);
        var tag = Mf01Seed.Tag();

        Guid regionId, towerId, droneId;
        await using (var db = scope.CreateContext())
        {
            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
            var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}-1", Geom = new Point(105.85, 21.03) { SRID = 4326 } };
            var drone = new Uav
            {
                Id = Guid.NewGuid(),
                UavCode = $"UAV-{tag}",
                Name = "M300",
                Model = "DJI M300",
                SerialNumber = $"SN-{tag}",
                AssignedRegionId = region.Id,
                OperationalStatus = DroneOperationalStatus.Available,
                TechnicalHealth = TechnicalHealth.Healthy,
                FlightEnduranceMinutes = 45,
                OperationalRangeKm = 12,
                BatteryLevel = 95
            };
            regionId = region.Id;
            towerId = tower.Id;
            droneId = drone.Id;

            db.AddRange(region, tower, drone);
            db.DroneTechnicalInspections.Add(new DroneTechnicalInspection
            {
                Id = Guid.NewGuid(),
                DroneId = drone.Id,
                Status = DroneTechnicalInspectionStatus.Passed,
                Health = TechnicalHealth.Healthy,
                StartedAt = DateTime.UtcNow.AddHours(-1),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                ValidUntil = DateTime.UtcNow.AddDays(7),
                PolicyVersion = "v2.0"
            });
            await db.SaveChangesAsync();
        }

        var managerId = await scope.SeedUserAsync(tag, UserRoles.Manager, regionId);
        var inspectorId = await scope.SeedUserAsync(tag, UserRoles.Inspector, regionId);
        var analystId = await scope.SeedUserAsync(tag, UserRoles.Analyst, regionId);
        await scope.SeedUserAsync(tag, UserRoles.Technician, regionId);

        // 1. Create Ready Assessment
        var plannedStart = DateTime.UtcNow.AddHours(1);
        var plannedEnd = plannedStart.AddHours(3);
        Guid assessmentId;
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var assessmentService = new PreMissionAssessmentService(db, UserContext(managerId, UserRoles.Manager));
            var assessment = await assessmentService.CreateAsync(
                regionId, plannedStart, plannedEnd, new[] { towerId }, CancellationToken.None);
            assessment.Status.Should().Be(PreMissionAssessmentStatus.Ready);
            assessmentId = assessment.Id;
        }

        // 2. Create Mission from Assessment -> Assessment becomes Completed, Mission is PendingAcceptance, ResourceBookings are Active
        Guid missionId;
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var assessmentService = new PreMissionAssessmentService(db, UserContext(managerId, UserRoles.Manager));
            var mission = await assessmentService.CreateMissionFromAssessmentAsync(
                new CreateMissionFromAssessmentRequest(
                    assessmentId,
                    $"Mission {tag}",
                    "Modular integration test mission",
                    new List<MissionPersonnelAssignmentRequest>
                    {
                        new(inspectorId, "INSPECTOR", true),
                        new(analystId, "ANALYST", true)
                    },
                    new List<Guid> { droneId }),
                CancellationToken.None);

            missionId = mission.Id;
            mission.Status.Should().Be(MissionStatus.PendingAcceptance);
        }

        await using (var db = scope.CreateContext())
        {
            var storedAssessment = await db.PreMissionAssessments.SingleAsync(a => a.Id == assessmentId);
            storedAssessment.Status.Should().Be(PreMissionAssessmentStatus.Completed);

            var activeBookings = await db.ResourceBookings
                .Where(b => b.MissionId == missionId && b.Status == ResourceBookingStatus.Active)
                .ToListAsync();
            activeBookings.Should().HaveCount(3); // Inspector, Analyst, Drone
        }

        // 3. Inspector accepts -> still PendingAcceptance because Analyst is also required
        await using (var db = scope.CreateContext(UserContext(inspectorId, UserRoles.Inspector)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(inspectorId, UserRoles.Inspector));
            await lifecycle.AcceptAssignmentAsync(missionId, CancellationToken.None);
        }

        await using (var db = scope.CreateContext())
        {
            (await db.Missions.SingleAsync(m => m.Id == missionId)).Status.Should().Be(MissionStatus.PendingAcceptance);
        }

        // 4. Analyst accepts -> all required members accepted -> Mission transitions PendingAcceptance -> Confirmed
        await using (var db = scope.CreateContext(UserContext(analystId, UserRoles.Analyst)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(analystId, UserRoles.Analyst));
            await lifecycle.AcceptAssignmentAsync(missionId, CancellationToken.None);
        }

        await using (var db = scope.CreateContext())
        {
            var confirmedMission = await db.Missions.SingleAsync(m => m.Id == missionId);
            confirmedMission.Status.Should().Be(MissionStatus.Confirmed);
            confirmedMission.AcceptedAt.Should().NotBeNull();
        }
    }

    // =========================================================================================================
    // LEVEL 3 (MF03 + Telemetry): CheckIn Geofence + Start + Periodic DroneTelemetry + Complete FlightLog
    // =========================================================================================================
    [Fact]
    public async Task Level3_Mf03_GeofenceCheckIn_Start_TelemetryIngestion_AndComplete_ShouldAggregateFlightLogAndReleaseResources()
    {
        await using var scope = new TestDatabaseScope(_pg);
        var tag = Mf01Seed.Tag();

        Guid regionId, towerId, droneId, missionId;
        var droneCode = $"UAV-{tag}";
        await using (var db = scope.CreateContext())
        {
            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
            var tower = new Tower
            {
                Id = Guid.NewGuid(),
                RegionId = region.Id,
                TowerCode = $"T-{tag}-1",
                Geom = new Point(106.7055, 10.7770) { SRID = 4326 }
            };
            var drone = new Uav
            {
                Id = Guid.NewGuid(),
                UavCode = droneCode,
                Name = "M300",
                Model = "DJI M300",
                SerialNumber = $"SN-{tag}",
                AssignedRegionId = region.Id,
                OperationalStatus = DroneOperationalStatus.Available,
                TechnicalHealth = TechnicalHealth.Healthy,
                FlightStatus = DroneStatus.Idle,
                BatteryLevel = 100
            };
            regionId = region.Id;
            towerId = tower.Id;
            droneId = drone.Id;
            db.AddRange(region, tower, drone);
            await db.SaveChangesAsync();
        }

        var managerId = await scope.SeedUserAsync(tag, UserRoles.Manager, regionId);
        var inspectorId = await scope.SeedUserAsync(tag, UserRoles.Inspector, regionId);

        await using (var db = scope.CreateContext())
        {
            var mission = new Mission
            {
                Id = Guid.NewGuid(),
                MissionCode = $"MS-{tag}",
                Title = $"Flight {tag}",
                ManagerId = managerId,
                RegionId = regionId,
                UavId = droneId,
                Status = MissionStatus.Confirmed,
                PlannedStart = DateTime.UtcNow.AddMinutes(10),
                PlannedEnd = DateTime.UtcNow.AddHours(2)
            };
            mission.MissionTargets.Add(new MissionTarget { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = towerId, Sequence = 1 });
            mission.Assignments.Add(new MissionAssignment
            {
                Id = Guid.NewGuid(),
                MissionId = mission.Id,
                UserId = inspectorId,
                AssignmentRole = "INSPECTOR",
                IsRequired = true,
                Status = MissionAssignmentStatus.Active,
                ResponseStatus = MissionAssignmentResponse.Accepted
            });
            db.Missions.Add(mission);
            db.ResourceBookings.Add(new ResourceBooking
            {
                Id = Guid.NewGuid(),
                MissionId = mission.Id,
                DroneId = droneId,
                StartAt = mission.PlannedStart.Value,
                EndAt = mission.PlannedEnd.Value,
                Status = ResourceBookingStatus.Active
            });
            missionId = mission.Id;
            await db.SaveChangesAsync();
        }

        // 1. Check-in outside geofence (1.5 km away) -> rejected by Mission:CheckInGeofenceRadiusMeters (default 200m)
        await using (var db = scope.CreateContext(UserContext(inspectorId, UserRoles.Inspector)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(inspectorId, UserRoles.Inspector));
            var farCheckIn = () => lifecycle.CheckInAsync(missionId, 10.7900, 106.7200, CancellationToken.None);
            (await farCheckIn.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("CHECKIN_OUTSIDE_GEOFENCE");

            // Check-in within 200m -> Mission transitions Confirmed -> Ready
            await lifecycle.CheckInAsync(missionId, 10.7771, 106.7056, CancellationToken.None);
        }

        // 2. Start Mission -> Ready -> InProgress, UAV FlightStatus -> Flying
        await using (var db = scope.CreateContext(UserContext(inspectorId, UserRoles.Inspector)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(inspectorId, UserRoles.Inspector));
            await lifecycle.StartAsync(missionId, CancellationToken.None);
        }

        // 3. Ingest periodic MQTT telemetry frames while mission is InProgress
        var liveState = new InMemoryDroneLiveStateService();
        await using (var db = scope.CreateContext(UserContext(inspectorId, UserRoles.Inspector)))
        {
            var telemetryService = new DroneTelemetryService(
                db,
                UserContext(inspectorId, UserRoles.Inspector),
                liveState,
                Options.Create(new MqttOptions()),
                NullLogger<DroneTelemetryService>.Instance);

            var frame1 = $$"""
            {
              "drone_id": "{{droneCode}}",
              "timestamp": "2026-11-23T06:00:00.000Z",
              "flight_mode": "AUTO",
              "armed": true,
              "lat": 10.7770,
              "lon": 106.7055,
              "alt_relative_m": 30.0,
              "battery_remaining_pct": 96
            }
            """;
            var frame2 = $$"""
            {
              "drone_id": "{{droneCode}}",
              "timestamp": "2026-11-23T06:03:00.000Z",
              "flight_mode": "AUTO",
              "armed": true,
              "lat": 10.7775,
              "lon": 106.7060,
              "alt_relative_m": 54.5,
              "battery_remaining_pct": 81
            }
            """;

            await telemetryService.IngestAsync($"drone/{droneCode}/telemetry", frame1);
            await telemetryService.IngestAsync($"drone/{droneCode}/telemetry", frame2);
        }

        // 4. Complete Mission -> InProgress -> Completed, UAV -> Idle, Booking -> Released, MissionFlightLog auto-created
        await using (var db = scope.CreateContext(UserContext(inspectorId, UserRoles.Inspector)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(inspectorId, UserRoles.Inspector));
            await lifecycle.CompleteAsync(missionId, CancellationToken.None);
        }

        await using (var db = scope.CreateContext())
        {
            var completedMission = await db.Missions.SingleAsync(m => m.Id == missionId);
            completedMission.Status.Should().Be(MissionStatus.Completed);

            var uav = await db.Uavs.SingleAsync(d => d.Id == droneId);
            uav.FlightStatus.Should().Be(DroneStatus.Idle);
            uav.BatteryLevel.Should().Be(81);

            var booking = await db.ResourceBookings.SingleAsync(b => b.MissionId == missionId);
            booking.Status.Should().Be(ResourceBookingStatus.Released);

            var flightLog = await db.MissionFlightLogs.SingleAsync(l => l.MissionId == missionId);
            flightLog.MinBatteryRecorded.Should().Be(81);
            flightLog.MaxAltitudeM.Should().Be(54.5);
            flightLog.FlightDurationSeconds.Should().Be(180);
        }
    }

    // =========================================================================================================
    // LEVEL 4 (MF03 -> MF04): AI Detection Review + EmergencyAlertService + MaintenanceTicketService + SLA Settings
    // =========================================================================================================
    [Fact]
    public async Task Level4_Mf03ToMf04_ConfirmEmergencyAnomaly_ShouldCreateOpenAlertAndSlaTicket_AndCloseResolvesAnomaly()
    {
        await using var scope = new TestDatabaseScope(_pg);
        var tag = Mf01Seed.Tag();

        Guid regionId, towerId, missionId, anomalyId;
        await using (var db = scope.CreateContext())
        {
            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = $"Region {tag}", Type = "Province" };
            var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}-1", Geom = new Point(105.85, 21.03) { SRID = 4326 } };
            regionId = region.Id;
            towerId = tower.Id;
            db.AddRange(region, tower);
            await db.SaveChangesAsync();
        }

        var managerId = await scope.SeedUserAsync(tag, UserRoles.Manager, regionId);
        var analystId = await scope.SeedUserAsync(tag, UserRoles.Analyst, regionId);
        var technicianId = await scope.SeedUserAsync(tag, UserRoles.Technician, regionId);

        await using (var db = scope.CreateContext())
        {
            // Configure custom Emergency SLA = 2 days in SystemSettings
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = SettingKeys.MaintenanceEmergencySlaDays,
                Value = "2",
                Category = "MAINTENANCE",
                DataType = "int"
            });

            var category = new DefectCategory
            {
                CategoryCode = $"WF-{tag}",
                CategoryName = "Wildfire / Flashover",
                Description = "Critical flashover hazard",
                SeverityWeight = 5.0,
                IsEmergencyClass = true
            };
            var mission = new Mission
            {
                Id = Guid.NewGuid(),
                MissionCode = $"MS-{tag}",
                Title = $"Inspection {tag}",
                ManagerId = managerId,
                RegionId = regionId,
                Status = MissionStatus.Completed
            };
            mission.Assignments.Add(new MissionAssignment
            {
                Id = Guid.NewGuid(),
                MissionId = mission.Id,
                UserId = analystId,
                AssignmentRole = "ANALYST",
                Status = MissionAssignmentStatus.Active,
                ResponseStatus = MissionAssignmentResponse.Accepted
            });
            var media = new InspectionMedia
            {
                Id = Guid.NewGuid(),
                MissionId = mission.Id,
                TowerId = towerId,
                MediaType = "Image",
                FileUrl = $"/images/{tag}.jpg",
                ValidationStatus = "PendingReview",
                CapturedAt = DateTime.UtcNow
            };
            var run = new AIAnalysisRun
            {
                Id = Guid.NewGuid(),
                MediaId = media.Id,
                ExecutionLocation = AIAnalysisRun.Cloud,
                ModelName = "RF-DETR",
                Status = AIAnalysisStatus.Completed
            };
            db.AddRange(category, mission, media, run);
            await db.SaveChangesAsync();

            var anomaly = new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                MediaId = media.Id,
                AnalysisRunId = run.Id,
                TowerId = towerId,
                CategoryId = category.Id,
                BoundingBox = """{"x":0.1,"y":0.1,"width":0.2,"height":0.2}""",
                ConfidenceScore = 0.96,
                ValidationStatus = "InReview"
            };
            db.DetectedAnomalies.Add(anomaly);
            await db.SaveChangesAsync();

            missionId = mission.Id;
            anomalyId = anomaly.Id;
        }

        // 1. Assigned Analyst confirms the emergency anomaly -> creates Open EmergencyAlert + Open MaintenanceTicket with 2-day SLA
        Guid ticketId, alertId;
        await using (var db = scope.CreateContext(UserContext(analystId, UserRoles.Analyst)))
        {
            var lifecycle = new MissionLifecycleService(db, UserContext(analystId, UserRoles.Analyst));
            var reviewResult = await lifecycle.ReviewDetectionAsync(
                missionId,
                anomalyId,
                new ReviewDetectionRequest
                {
                    Status = "Confirmed",
                    OverrideSeverity = "Critical",
                    ReviewNotes = "Confirmed severe flashover risk on insulator"
                },
                CancellationToken.None);

            reviewResult.Status.Should().Be("Approved");
            Guid.TryParse(reviewResult.CreatedTaskId, out ticketId).Should().BeTrue();
            Guid.TryParse(reviewResult.EmergencyAlertId, out alertId).Should().BeTrue();
        }

        await using (var db = scope.CreateContext())
        {
            var alert = await db.EmergencyAlerts.SingleAsync(a => a.Id == alertId);
            alert.Status.Should().Be("Open");

            var ticket = await db.MaintenanceTickets.SingleAsync(t => t.Id == ticketId);
            ticket.Status.Should().Be(TicketStatus.Open);
            ticket.Priority.Should().Be(TicketPriority.Emergency);
            ticket.DueDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(2), TimeSpan.FromMinutes(2));
        }

        // 2. Manager reviews EmergencyAlert (Open -> Acknowledged) and assigns MaintenanceTicket to Technician (Open -> Assigned)
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var alertService = new EmergencyAlertService(db, Mock.Of<IEventPublisher>(), NullLogger<EmergencyAlertService>.Instance);
            var reviewed = await alertService.ReviewAlertAsync(
                alertId,
                managerId,
                new ReviewEmergencyAlertRequest { Status = "Acknowledged", Notes = "Dispatched technician immediately" },
                CancellationToken.None);
            reviewed.Should().BeTrue();

            var ticketService = new MaintenanceTicketService(db, UserContext(managerId, UserRoles.Manager));
            var assignedDto = await ticketService.AssignTechnicianAsync(
                ticketId,
                new AssignTechnicianRequest { TechnicianId = technicianId });
            assignedDto.Status.Should().Be("Assigned");
        }

        // 3. Technician starts repair (Assigned -> InProgress), uploads proof, and requests verification (InProgress -> PendingVerification)
        await using (var db = scope.CreateContext(UserContext(technicianId, UserRoles.Technician)))
        {
            var ticketService = new MaintenanceTicketService(db, UserContext(technicianId, UserRoles.Technician));
            (await ticketService.UpdateTicketStatusAsync(ticketId, "InProgress")).Status.Should().Be("InProgress");

            db.MaintenanceProofs.Add(new MaintenanceProof
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                UploadedBy = technicianId,
                ProofType = "After",
                FileUrl = $"uav-media/proof-{tag}.jpg",
                TechnicianNotes = "Replaced damaged insulator"
            });
            await db.SaveChangesAsync();

            (await ticketService.UpdateTicketStatusAsync(ticketId, "PendingVerification")).Status.Should().Be("PendingVerification");
        }

        // 4. Manager verifies & closes ticket (PendingVerification -> Closed) -> Anomaly automatically transitions Confirmed -> Resolved
        await using (var db = scope.CreateContext(UserContext(managerId, UserRoles.Manager)))
        {
            var ticketService = new MaintenanceTicketService(db, UserContext(managerId, UserRoles.Manager));
            var closedDto = await ticketService.CloseTicketAsync(ticketId);
            closedDto.Status.Should().Be("Closed");
        }

        await using (var db = scope.CreateContext())
        {
            var resolvedAnomaly = await db.DetectedAnomalies.SingleAsync(a => a.Id == anomalyId);
            resolvedAnomaly.ValidationStatus.Should().Be("Resolved");

            var closedAlert = await db.EmergencyAlerts.SingleAsync(a => a.Id == alertId);
            closedAlert.Status.Should().Be("Acknowledged");
        }
    }
}
