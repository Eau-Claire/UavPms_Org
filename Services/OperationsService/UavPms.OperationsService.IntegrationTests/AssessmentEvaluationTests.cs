using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using static UavPms.OperationsService.IntegrationTests.Mf01Seed;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// MF01 readiness assessment on real PostgreSQL: create/evaluate rules (BR-14 – BR-22), idempotency, read-only
/// resource readiness, expiry and access. Candidates are built from every user and UAV in the shared database, so
/// each test asserts only on the rows it seeded.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AssessmentEvaluationTests
{
    private readonly PostgresFixture _pg;

    public AssessmentEvaluationTests(PostgresFixture pg) => _pg = pg;

    private static DateTime Start => DateTime.UtcNow.AddHours(1);

    [Fact]
    public async Task Evaluate_ByAdmin_CandidateScopeComesFromCandidateRows_AndInactiveStaffGetReasons()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid adminId, outsiderId, inactiveId, unverifiedId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            adminId = await InsertUserAsync(db, site.Tag, UserRoles.SystemAdmin);
            outsiderId = await InsertUserAsync(db, site.Tag, UserRoles.Inspector);          // active, no scope row
            inactiveId = await InsertUserAsync(db, site.Tag, UserRoles.Analyst, status: "Inactive");
            unverifiedId = await InsertUserAsync(db, site.Tag, UserRoles.Inspector, verified: false);
            await AddScopeAsync(db, inactiveId, site.RegionId);
            await AddScopeAsync(db, unverifiedId, site.RegionId);
        }

        var admin = Current(adminId, UserRoles.SystemAdmin);
        PreMissionAssessment assessment;
        await using (var db = _pg.CreateContext(admin))
        {
            assessment = await new PreMissionAssessmentService(db, admin)
                .CreateAsync(site.RegionId, Start, Start.AddHours(2), site.TowerIds, CancellationToken.None);
        }

        await using (var db = _pg.CreateContext())
        {
            var candidates = await db.PreMissionAssessmentPersonnel.Where(p => p.AssessmentId == assessment.Id).ToListAsync();

            // BR-18: the evaluating admin's role does not put an out-of-scope field user in scope.
            var outsider = candidates.Single(p => p.UserId == outsiderId);
            outsider.IsEligible.Should().BeFalse();
            outsider.ReasonCode.Should().Be("OUTSIDE_MANAGEMENT_SCOPE");

            candidates.Single(p => p.UserId == site.InspectorId).IsEligible.Should().BeTrue();

            // BR-18 / #19: inactive or unverified staff of the region are listed with USER_INACTIVE.
            candidates.Single(p => p.UserId == inactiveId).ReasonCode.Should().Be("USER_INACTIVE");
            candidates.Single(p => p.UserId == unverifiedId).ReasonCode.Should().Be("USER_INACTIVE");

            // The admin and the manager are never candidates.
            candidates.Should().NotContain(p => p.UserId == adminId || p.UserId == site.ManagerId);
        }
    }

    [Fact]
    public async Task Evaluate_UsesNewestCompletedInspection_AndRequiresAValidOne()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid validDrone, newerFailedDrone, expiredDrone, neverInspectedDrone;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            validDrone = await AddDroneAsync(db, site.Tag, "OK", inspections: ValidPassed());
            // #2: an older Passed inspection is still valid, but a newer one Failed.
            newerFailedDrone = await AddDroneAsync(db, site.Tag, "FAILED", inspections: new[]
            {
                ValidPassed(daysAgo: 2),
                (DroneTechnicalInspectionStatus.Failed, TechnicalHealth.Critical, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddDays(7))
            });
            // #10: inspected months ago, stored health still Healthy.
            expiredDrone = await AddDroneAsync(db, site.Tag, "OLD", inspections:
                (DroneTechnicalInspectionStatus.Passed, TechnicalHealth.Healthy, DateTime.UtcNow.AddDays(-90), DateTime.UtcNow.AddDays(-83)));
            neverInspectedDrone = await AddDroneAsync(db, site.Tag, "NEW");
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        PreMissionAssessment assessment;
        await using (var db = _pg.CreateContext(manager))
        {
            assessment = await new PreMissionAssessmentService(db, manager)
                .CreateAsync(site.RegionId, Start, Start.AddHours(2), site.TowerIds, CancellationToken.None);
        }

        assessment.Status.Should().Be(PreMissionAssessmentStatus.Ready);

        await using (var db = _pg.CreateContext())
        {
            var drones = await db.PreMissionAssessmentDrones.Where(d => d.AssessmentId == assessment.Id).ToListAsync();

            var ok = drones.Single(d => d.DroneId == validDrone);
            ok.IsEligible.Should().BeTrue();
            ok.ReasonCode.Should().BeNull();

            var failed = drones.Single(d => d.DroneId == newerFailedDrone);
            failed.IsEligible.Should().BeFalse();
            failed.TechnicalHealth.Should().Be(TechnicalHealth.Critical);
            failed.ReasonCode.Should().Be("TECHNICAL_HEALTH_CRITICAL");

            foreach (var id in new[] { expiredDrone, neverInspectedDrone })
            {
                var d = drones.Single(x => x.DroneId == id);
                d.IsEligible.Should().BeFalse();
                d.TechnicalEligibilityStatus.Should().Be(ResourceEligibilityStatus.RequiresInspection);
                d.ReasonCode.Should().Be("NO_VALID_TECHNICAL_INSPECTION");
            }
        }
    }

    [Fact]
    public async Task Evaluate_ScopeSuitability_UsesTowerGeometryAndRange_AndEligibleEndpointAgrees()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid oneFlight, split, tooShort, noEndurance, maintenance;
        await using (var db = _pg.CreateContext())
        {
            // 3 towers 0.135° of latitude apart: a corridor of ~30 km.
            site = await SeedSiteAsync(db, towerCount: 3, spacingDeg: 0.135);
            oneFlight = await AddDroneAsync(db, site.Tag, "LONG", enduranceMinutes: 120, rangeKm: 40, inspections: ValidPassed());
            split = await AddDroneAsync(db, site.Tag, "SPLIT", enduranceMinutes: 120, rangeKm: 12, inspections: ValidPassed());   // range: 3 flights
            tooShort = await AddDroneAsync(db, site.Tag, "SHORT", enduranceMinutes: 120, rangeKm: 5, inspections: ValidPassed()); // range: 6 flights
            noEndurance = await AddDroneAsync(db, site.Tag, "ZERO", enduranceMinutes: 0, rangeKm: 40, inspections: ValidPassed());
            maintenance = await AddDroneAsync(db, site.Tag, "MAINT", status: DroneOperationalStatus.Maintenance, enduranceMinutes: 120, rangeKm: 40, inspections: ValidPassed());
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        PreMissionAssessment assessment;
        await using (var db = _pg.CreateContext(manager))
        {
            assessment = await new PreMissionAssessmentService(db, manager)
                .CreateAsync(site.RegionId, Start, Start.AddHours(2), site.TowerIds, CancellationToken.None);
        }

        await using (var db = _pg.CreateContext(manager))
        {
            var drones = await db.PreMissionAssessmentDrones.Where(d => d.AssessmentId == assessment.Id).ToListAsync();

            var a = drones.Single(d => d.DroneId == oneFlight);
            a.Suitability.Should().Be(UavScopeSuitability.Suitable);
            a.EstimatedFlightsNeeded.Should().Be(1);
            a.IsEligible.Should().BeTrue();

            // #13: a ~30 km corridor with a 12 km range needs 3 flights even though one flight has enough endurance.
            var b = drones.Single(d => d.DroneId == split);
            b.Suitability.Should().Be(UavScopeSuitability.SuitableWithSplit);
            b.EstimatedFlightsNeeded.Should().Be(3);
            b.IsEligible.Should().BeTrue();

            var c = drones.Single(d => d.DroneId == tooShort);
            c.Suitability.Should().Be(UavScopeSuitability.Unsuitable);
            c.ReasonCode.Should().Be("SCOPE_EXCEEDS_CAPABILITY");

            // Endurance 0 is not silently replaced by 30 minutes.
            var z = drones.Single(d => d.DroneId == noEndurance);
            z.Suitability.Should().Be(UavScopeSuitability.Unsuitable);
            z.IsEligible.Should().BeFalse();

            // Availability is a separate criterion: the scope itself is suitable.
            var m = drones.Single(d => d.DroneId == maintenance);
            m.Suitability.Should().Be(UavScopeSuitability.Suitable);
            m.ReasonCode.Should().Be("DRONE_NOT_AVAILABLE");
            m.IsEligible.Should().BeFalse();

            // GET /drones/eligible applies the same evaluator.
            var uavService = new UavManagementService(db, manager, new DroneTechnicalInspectionService(db, manager));
            var eligible = await uavService.GetEligibleDronesForAssessmentAsync(assessment.Id);
            var seeded = new[] { oneFlight, split, tooShort, noEndurance, maintenance };
            eligible.Select(e => e.DroneId).Where(seeded.Contains).Should().BeEquivalentTo(new[] { oneFlight, split });
            eligible.Single(e => e.DroneId == split).Suitability.Should().Be("SUITABLE_WITH_SPLIT");
        }
    }

    [Fact]
    public async Task Create_NonUtcTimes_AreNormalisedToUtc()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        await using (var db = _pg.CreateContext())
            site = await SeedSiteAsync(db);

        var manager = Current(site.ManagerId, UserRoles.Manager);
        var utcStart = DateTime.UtcNow.AddHours(2);
        utcStart = utcStart.AddTicks(-(utcStart.Ticks % TimeSpan.TicksPerSecond));

        await using var ctx = _pg.CreateContext(manager);
        var service = new PreMissionAssessmentService(ctx, manager);

        // Unspecified kind (JSON without offset) is taken as UTC; Npgsql no longer rejects it with a 500.
        var unspecified = await service.CreateAsync(site.RegionId,
            DateTime.SpecifyKind(utcStart, DateTimeKind.Unspecified),
            DateTime.SpecifyKind(utcStart.AddHours(2), DateTimeKind.Unspecified),
            site.TowerIds, CancellationToken.None);
        unspecified.PlannedStart.Should().Be(utcStart);

        // Local kind (JSON with an offset) is converted.
        var local = await service.CreateAsync(site.RegionId,
            utcStart.ToLocalTime(), utcStart.AddHours(2).ToLocalTime(), site.TowerIds, CancellationToken.None);
        local.PlannedStart.Should().Be(utcStart);
        local.PlannedStart.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Create_InvalidScope_IsRejected()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid noGeomTower;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            var tower = new Tower { Id = Guid.NewGuid(), RegionId = site.RegionId, TowerCode = $"T-{site.Tag}-NOGEOM" };
            db.Towers.Add(tower);
            await db.SaveChangesAsync();
            noGeomTower = tower.Id;
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        await using var ctx = _pg.CreateContext(manager);
        var service = new PreMissionAssessmentService(ctx, manager);

        // BR-15: a tower without coordinates fails even without a boundary.
        var noGeom = () => service.CreateAsync(site.RegionId, Start, Start.AddHours(2), new[] { site.TowerIds[0], noGeomTower }, CancellationToken.None);
        await noGeom.Should().ThrowAsync<BusinessRuleException>().WithMessage("TOWER_MISSING_GEOLOCATION*");

        // BR-14 / #22: a missing tower list is a 400, not a NullReferenceException.
        var nullTowers = () => service.CreateAsync(site.RegionId, Start, Start.AddHours(2), null!, CancellationToken.None);
        await nullTowers.Should().ThrowAsync<BusinessRuleException>().WithMessage("INVALID_PROPOSED_SCOPE*");

        var unknownRegion = () => service.CreateAsync(Guid.NewGuid(), Start, Start.AddHours(2), site.TowerIds, CancellationToken.None);
        await unknownRegion.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Create_IdempotencyKey_IsScopedToManagerAndPayload_AndRecoversStuckEvaluation()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid otherManagerId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            otherManagerId = await InsertUserAsync(db, site.Tag, UserRoles.Manager);
            await AddScopeAsync(db, otherManagerId, site.RegionId);
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        var start = Start;
        var key = $"idem-{Guid.NewGuid():N}";

        Guid firstId;
        await using (var db = _pg.CreateContext(manager))
        {
            var service = new PreMissionAssessmentService(db, manager);
            firstId = (await service.CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds, null, key, CancellationToken.None)).Id;
        }

        await using (var db = _pg.CreateContext(manager))
        {
            var service = new PreMissionAssessmentService(db, manager);
            (await service.CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds, null, key, CancellationToken.None)).Id
                .Should().Be(firstId);

            var otherWindow = () => service.CreateAsync(site.RegionId, start, start.AddHours(3), site.TowerIds, null, key, CancellationToken.None);
            await otherWindow.Should().ThrowAsync<BusinessRuleException>().WithMessage("IDEMPOTENCY_KEY_REUSED*");

            var otherTowers = () => service.CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds.Take(1).ToList(), null, key, CancellationToken.None);
            await otherTowers.Should().ThrowAsync<BusinessRuleException>().WithMessage("IDEMPOTENCY_KEY_REUSED*");
        }

        var other = Current(otherManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(other))
        {
            var sameKeyOtherManager = () => new PreMissionAssessmentService(db, other)
                .CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds, null, key, CancellationToken.None);
            await sameKeyOtherManager.Should().ThrowAsync<BusinessRuleException>().WithMessage("IDEMPOTENCY_KEY_REUSED*");
        }

        // A first request whose evaluation failed left the assessment in Evaluating: the retry evaluates it.
        var stuckKey = $"idem-{Guid.NewGuid():N}";
        Guid stuckId;
        await using (var db = _pg.CreateContext())
        {
            var stuck = new PreMissionAssessment
            {
                ManagerId = site.ManagerId, RegionId = site.RegionId, PlannedStart = start, PlannedEnd = start.AddHours(2),
                Status = PreMissionAssessmentStatus.Evaluating, IdempotencyKey = stuckKey, EvaluationPolicyVersion = "v2.0",
            };
            stuck.Towers = site.TowerIds.Select((t, i) => new PreMissionAssessmentTower { AssessmentId = stuck.Id, TowerId = t, Sequence = i + 1 }).ToList();
            db.PreMissionAssessments.Add(stuck);
            await db.SaveChangesAsync();
            stuckId = stuck.Id;
        }

        await using (var db = _pg.CreateContext(manager))
        {
            var replay = await new PreMissionAssessmentService(db, manager)
                .CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds, null, stuckKey, CancellationToken.None);
            replay.Id.Should().Be(stuckId);
            replay.Status.Should().BeOneOf(PreMissionAssessmentStatus.Ready, PreMissionAssessmentStatus.NotReady);
            replay.ValidUntil.Should().NotBeNull();
        }

        // Concurrent submissions with one key create one assessment; the unique index race is handled.
        var raceKey = $"idem-{Guid.NewGuid():N}";
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
        {
            await using var db = _pg.CreateContext(manager);
            return (await new PreMissionAssessmentService(db, manager)
                .CreateAsync(site.RegionId, start, start.AddHours(2), site.TowerIds, null, raceKey, CancellationToken.None)).Id;
        }));
        results.Distinct().Should().HaveCount(1);
        await using (var db = _pg.CreateContext())
            (await db.PreMissionAssessments.CountAsync(a => a.IdempotencyKey == raceKey)).Should().Be(1);
    }

    [Fact]
    public async Task ResourceReadiness_IsReadOnly()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid droneId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            droneId = await AddDroneAsync(db, site.Tag, "RR", inspections: ValidPassed());
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        PreMissionAssessment created;
        await using (var db = _pg.CreateContext(manager))
            created = await new PreMissionAssessmentService(db, manager).CreateAsync(site.RegionId, Start, Start.AddHours(2), site.TowerIds, CancellationToken.None);
        created.Status.Should().Be(PreMissionAssessmentStatus.Ready);

        // The UAV becomes unavailable after the evaluation: only a re-evaluation may change the stored result.
        await using (var db = _pg.CreateContext())
        {
            var drone = await db.Uavs.SingleAsync(d => d.Id == droneId);
            drone.OperationalStatus = DroneOperationalStatus.Maintenance;
            await db.SaveChangesAsync();
        }

        await using (var db = _pg.CreateContext(manager))
        {
            var handler = new EvaluateResourceReadinessQueryHandler(new PreMissionAssessmentService(db, manager));
            var readiness = await handler.Handle(new EvaluateResourceReadinessQuery(created.Id), CancellationToken.None);
            readiness.IsReady.Should().BeTrue();
            readiness.DroneCandidates.Single(d => d.DroneId == droneId).IsEligible.Should().BeTrue();
        }

        await using (var db = _pg.CreateContext())
        {
            var stored = await db.PreMissionAssessments.SingleAsync(a => a.Id == created.Id);
            stored.Version.Should().Be(created.Version);
            stored.ValidUntil.Should().BeCloseTo(created.ValidUntil!.Value, TimeSpan.FromMilliseconds(1));
            stored.Status.Should().Be(PreMissionAssessmentStatus.Ready);
        }
    }

    [Fact]
    public async Task Cancel_ExpiredOrCancelled_IsRejected_AndExpiryIsAudited()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid expiredId, openId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            expiredId = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.Ready, validUntil: DateTime.UtcNow.AddMinutes(-1));
            openId = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.NotReady, validUntil: DateTime.UtcNow.AddHours(1));
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(manager))
        {
            var service = new PreMissionAssessmentService(db, manager);
            var cancelExpired = () => service.CancelAsync(expiredId, null, CancellationToken.None);
            await cancelExpired.Should().ThrowAsync<BusinessRuleException>().WithMessage("ASSESSMENT_EXPIRED*");

            (await service.CancelAsync(openId, "test", CancellationToken.None)).Status.Should().Be(PreMissionAssessmentStatus.Cancelled);
            var cancelTwice = () => service.CancelAsync(openId, null, CancellationToken.None);
            await cancelTwice.Should().ThrowAsync<BusinessRuleException>().WithMessage("ASSESSMENT_CANCELLED*");
        }

        await using (var db = _pg.CreateContext())
        {
            (await db.PreMissionAssessments.SingleAsync(a => a.Id == expiredId)).Status.Should().Be(PreMissionAssessmentStatus.Expired);
            (await db.AuditLogs.CountAsync(a => a.RecordId == expiredId && a.ActionType == "ASSESSMENT_EXPIRED")).Should().Be(1);
        }
    }

    [Fact]
    public async Task List_AppliesExpiryBeforeStatusFilter_AndAuditsIt()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid lapsedNotReady, lapsedReady, liveNotReady;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            lapsedNotReady = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.NotReady, validUntil: DateTime.UtcNow.AddMinutes(-1));
            lapsedReady = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.Ready, validUntil: DateTime.UtcNow.AddMinutes(-1));
            liveNotReady = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.NotReady, validUntil: DateTime.UtcNow.AddHours(1));
        }

        var manager = Current(site.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(manager))
        {
            var service = new PreMissionAssessmentService(db, manager);
            var notReady = await service.ListAsync("NOT_READY", CancellationToken.None);
            notReady.Should().Contain(a => a.Id == liveNotReady);
            notReady.Should().NotContain(a => a.Id == lapsedNotReady);
            notReady.Should().OnlyContain(a => a.Status == PreMissionAssessmentStatus.NotReady);

            (await service.ListAsync("READY", CancellationToken.None)).Should().NotContain(a => a.Id == lapsedReady);
            (await service.ListAsync("EXPIRED", CancellationToken.None)).Select(a => a.Id).Should().Contain(new[] { lapsedNotReady, lapsedReady });
        }

        await using (var db = _pg.CreateContext())
        {
            (await db.AuditLogs.CountAsync(a => (a.RecordId == lapsedNotReady || a.RecordId == lapsedReady) && a.ActionType == "ASSESSMENT_EXPIRED"))
                .Should().Be(2);
        }
    }

    [Fact]
    public async Task WriteActions_RequireCurrentRegionScope_ReadsStayOpenToFieldStaff()
    {
        if (!_pg.Available) return;

        Mf01Seed.Site site;
        Guid assessmentId;
        await using (var db = _pg.CreateContext())
        {
            site = await SeedSiteAsync(db);
            assessmentId = await AddAssessmentAsync(db, site, PreMissionAssessmentStatus.NotReady, validUntil: DateTime.UtcNow.AddHours(1));
            // The creator loses the region scope.
            await db.UserGeographicScopes.Where(s => s.UserId == site.ManagerId).ExecuteDeleteAsync();
        }

        var creator = Current(site.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(creator))
        {
            var service = new PreMissionAssessmentService(db, creator);
            var evaluate = () => service.EvaluateAsync(assessmentId, CancellationToken.None);
            await evaluate.Should().ThrowAsync<ForbiddenException>().WithMessage("REGION_MANAGEMENT_SCOPE_REQUIRED");
            var cancel = () => service.CancelAsync(assessmentId, null, CancellationToken.None);
            await cancel.Should().ThrowAsync<ForbiddenException>().WithMessage("REGION_MANAGEMENT_SCOPE_REQUIRED");

            (await service.GetAsync(assessmentId, CancellationToken.None)).Id.Should().Be(assessmentId);
        }

        var technician = Current(site.TechnicianId, UserRoles.MaintenanceTechnician);
        await using (var db = _pg.CreateContext(technician))
        {
            var service = new PreMissionAssessmentService(db, technician);
            (await service.GetAsync(assessmentId, CancellationToken.None)).Id.Should().Be(assessmentId);
            var evaluate = () => service.EvaluateAsync(assessmentId, CancellationToken.None);
            await evaluate.Should().ThrowAsync<ForbiddenException>();
        }
    }

    private static async Task<Guid> AddAssessmentAsync(
        Infrastructure.Persistence.ApplicationDbContext db, Mf01Seed.Site site, PreMissionAssessmentStatus status, DateTime validUntil)
    {
        var start = DateTime.UtcNow.AddHours(2);
        var assessment = new PreMissionAssessment
        {
            ManagerId = site.ManagerId,
            RegionId = site.RegionId,
            PlannedStart = start,
            PlannedEnd = start.AddHours(2),
            Status = status,
            SiteFeasibilityStatus = ReadinessCheckStatus.Passed,
            ValidUntil = validUntil,
            EvaluationPolicyVersion = "v2.0",
        };
        assessment.Towers = site.TowerIds.Select((t, i) => new PreMissionAssessmentTower { AssessmentId = assessment.Id, TowerId = t, Sequence = i + 1 }).ToList();
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();
        return assessment.Id;
    }
}
