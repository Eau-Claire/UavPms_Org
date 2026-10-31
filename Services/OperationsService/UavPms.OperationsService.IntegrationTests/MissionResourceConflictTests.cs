using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UavPms.OperationsService.API.Middlewares;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// BR-19 booking conflicts and UAV usage on real PostgreSQL: the exclusion constraints are the final guard, so a race
/// they decide must come back as 409 with a business code, never 500.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MissionResourceConflictTests
{
    private readonly PostgresFixture _pg;

    public MissionResourceConflictTests(PostgresFixture pg) => _pg = pg;

    private static DateTime Hour(int hoursFromNow) => DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour + hoursFromNow);

    [Fact]
    public async Task ConcurrentCreates_ForTheSameInspectorAndUav_OneWinsAndTheOtherGets409()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var people = new[] { new MissionPersonnelAssignmentRequest(inspector, "INSPECTOR", true) };
        var a1 = await w.AddAssessmentAsync(Hour(2), Hour(5), new[] { inspector }, new[] { drone });
        var a2 = await w.AddAssessmentAsync(Hour(3), Hour(6), new[] { inspector }, new[] { drone });

        var results = await Task.WhenAll(
            Capture(() => w.CreateMissionAsync(MissionWorld.Request(a1, people, new[] { drone }))),
            Capture(() => w.CreateMissionAsync(MissionWorld.Request(a2, people, new[] { drone }))));

        results.Count(r => r.Mission != null).Should().Be(1);
        var failure = results.Single(r => r.Error != null).Error;
        failure.Should().BeOfType<BusinessRuleException>().Which.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
        (await Status(failure!)).Should().Be(HttpStatusCode.Conflict);

        var active = await w.Read(db => db.ResourceBookings.CountAsync(b => b.DroneId == drone && b.Status == ResourceBookingStatus.Active));
        active.Should().Be(1);
    }

    [Fact]
    public async Task ExclusionConstraintViolation_IsReturnedAs409ResourceBookingConflict()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var mission = await w.CreateMissionAsync(Hour(2), Hour(5), inspector, drone);

        // Bypass the application checks: only the database constraint can refuse this row.
        DbUpdateException? raw = null;
        try
        {
            await w.Write(db =>
            {
                db.ResourceBookings.Add(new ResourceBooking { MissionId = mission.Id, DroneId = drone, StartAt = Hour(4), EndAt = Hour(7), Status = ResourceBookingStatus.Active });
                return Task.CompletedTask;
            });
        }
        catch (DbUpdateException ex) { raw = ex; }

        raw.Should().NotBeNull();
        var (status, body) = await Handle(raw!);
        status.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("RESOURCE_BOOKING_CONFLICT");

        // Unique violation on assignments: same user twice Active in one mission.
        DbUpdateException? duplicate = null;
        try
        {
            await w.Write(db =>
            {
                db.MissionAssignments.Add(new MissionAssignment { MissionId = mission.Id, UserId = inspector, AssignmentRole = "INSPECTOR", AssignedById = w.ManagerId });
                return Task.CompletedTask;
            });
        }
        catch (DbUpdateException ex) { duplicate = ex; }

        duplicate.Should().NotBeNull();
        (status, body) = await Handle(duplicate!);
        status.Should().Be(HttpStatusCode.Conflict);
        body.Should().Contain("DUPLICATE_ASSIGNMENT");
    }

    [Fact]
    public async Task TouchingWindows_AreNotAConflict_ButOverlapsAre()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();

        await w.CreateMissionAsync(Hour(2), Hour(4), inspector, drone);
        var second = await w.CreateMissionAsync(Hour(4), Hour(6), inspector, drone);
        second.Status.Should().Be(MissionStatus.PendingAcceptance);

        var overlap = () => w.CreateMissionAsync(Hour(5), Hour(7), inspector, drone);
        (await overlap.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
    }

    [Fact]
    public async Task CreateRequest_WithDuplicatesSeveralUavsOrNoRequiredMember_IsRejected()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var drone2 = await w.AddDroneAsync();
        var assessment = await w.AddAssessmentAsync(Hour(2), Hour(4), new[] { inspector }, new[] { drone, drone2 });
        var one = new[] { new MissionPersonnelAssignmentRequest(inspector, "INSPECTOR", true) };

        await Expect("DUPLICATE_PERSONNEL", () => w.CreateMissionAsync(MissionWorld.Request(assessment,
            new[] { new MissionPersonnelAssignmentRequest(inspector, "INSPECTOR", true), new MissionPersonnelAssignmentRequest(inspector, "ANALYST", false) }, new[] { drone })));
        await Expect("DUPLICATE_DRONE", () => w.CreateMissionAsync(MissionWorld.Request(assessment, one, new[] { drone, drone })));
        await Expect("MULTIPLE_DRONES_NOT_SUPPORTED", () => w.CreateMissionAsync(MissionWorld.Request(assessment, one, new[] { drone, drone2 })));
        await Expect("REQUIRED_MEMBER_REQUIRED", () => w.CreateMissionAsync(MissionWorld.Request(assessment,
            new[] { new MissionPersonnelAssignmentRequest(inspector, "INSPECTOR", false) }, new[] { drone })));
        await Expect("INVALID_CONFIRMATION_DEADLINE", () => w.CreateMissionAsync(MissionWorld.Request(assessment, one, new[] { drone }, confirmationDeadline: Hour(3))));

        // Nothing was booked by the refused requests; a valid one books exactly one person and one UAV.
        var mission = await w.CreateMissionAsync(MissionWorld.Request(assessment, one, new[] { drone }));
        var bookings = await w.BookingsAsync(mission.Id);
        bookings.Should().HaveCount(2).And.OnlyContain(b => b.Status == ResourceBookingStatus.Active);
        bookings.Count(b => b.DroneId == drone).Should().Be(1);

        // BR-31: the default confirmation deadline is in the future and not after the planned start.
        var saved = await w.LoadMissionAsync(mission.Id);
        saved.ConfirmationDeadline.Should().NotBeNull();
        saved.ConfirmationDeadline!.Value.Should().BeAfter(DateTime.UtcNow).And.BeOnOrBefore(Hour(2));
        saved.ManagerInstructions.Should().Be("Bring spare batteries");
    }

    [Fact]
    public async Task CreateMission_RevalidatesPeopleAndUav_InsteadOfTrustingTheSnapshot()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var outOfScope = await w.AddUserAsync(inScope: false);
        var otherManager = await w.AddUserAsync(UserRoles.Manager);
        var drone = await w.AddDroneAsync();
        var unknownHealth = await w.AddDroneAsync(TechnicalHealth.Unknown);
        var assessment = await w.AddAssessmentAsync(Hour(2), Hour(4), new[] { inspector, outOfScope, otherManager }, new[] { drone, unknownHealth });
        MissionPersonnelAssignmentRequest[] Person(Guid id) => new[] { new MissionPersonnelAssignmentRequest(id, "INSPECTOR", true) };

        await Expect("ASSIGNEE_OUT_OF_SCOPE", () => w.CreateMissionAsync(MissionWorld.Request(assessment, Person(outOfScope), new[] { drone })));
        await Expect("ASSIGNEE_ROLE_NOT_ELIGIBLE", () => w.CreateMissionAsync(MissionWorld.Request(assessment, Person(otherManager), new[] { drone })));
        await Expect("DRONE_HEALTH_UNKNOWN", () => w.CreateMissionAsync(MissionWorld.Request(assessment, Person(inspector), new[] { unknownHealth })));

        // Deactivated after the evaluation.
        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Users" SET "Status" = 'Inactive' WHERE "Id" = {inspector}"""));
        await Expect("ASSIGNEE_INACTIVE", () => w.CreateMissionAsync(MissionWorld.Request(assessment, Person(inspector), new[] { drone })));
    }

    [Fact]
    public async Task IdempotencyKey_ReplaysTheSameRequest_AndRefusesADifferentOne()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var assessment = await w.AddAssessmentAsync(Hour(2), Hour(4), new[] { inspector }, new[] { drone });
        var key = Guid.NewGuid().ToString();
        var request = MissionWorld.Request(assessment, new[] { new MissionPersonnelAssignmentRequest(inspector, "INSPECTOR", true) }, new[] { drone }, key);

        var results = await Task.WhenAll(Capture(() => w.CreateMissionAsync(request)), Capture(() => w.CreateMissionAsync(request)));
        results.Should().OnlyContain(r => r.Error == null);
        results.Select(r => r.Mission!.Id).Distinct().Should().HaveCount(1);
        (await w.Read(db => db.Missions.CountAsync(m => m.IdempotencyKey == key))).Should().Be(1);

        await Expect("IDEMPOTENCY_KEY_REUSED", () => w.CreateMissionAsync(request with { Title = "Another mission" }));
        var otherManager = await w.AddUserAsync(UserRoles.Manager);
        var byOther = () => w.WithAssessments(otherManager, UserRoles.Manager, s => s.CreateMissionFromAssessmentAsync(request, default));
        (await byOther.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task OneUav_CannotFlyTwoMissions_AndCompletingOneKeepsItFlyingForTheOther()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspectorA = await w.AddUserAsync();
        var inspectorB = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var a = await w.CreateMissionAsync(Hour(-1), Hour(1), inspectorA, drone);
        var b = await w.CreateMissionAsync(Hour(1), Hour(3), inspectorB, drone);
        await w.AcceptAllAsync(a);
        await w.AcceptAllAsync(b);

        await w.AsInspector(inspectorA, s => s.StartAsync(a.Id, default));
        (await w.DroneAsync(drone)).FlightStatus.Should().Be(DroneStatus.Flying);

        var startB = () => w.AsInspector(inspectorB, s => s.StartAsync(b.Id, default));
        (await startB.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("DRONE_IN_USE");

        // Even if data says B flies the same UAV (legacy rows), completing A must not ground it.
        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Missions" SET "Status" = 'InProgress', "StartedAt" = now() WHERE "Id" = {b.Id}"""));
        await w.AsInspector(inspectorA, s => s.CompleteAsync(a.Id, default));
        (await w.DroneAsync(drone)).FlightStatus.Should().Be(DroneStatus.Flying);
        (await w.BookingsAsync(a.Id)).Should().OnlyContain(x => x.Status == ResourceBookingStatus.Released);

        await w.AsInspector(inspectorB, s => s.CompleteAsync(b.Id, default));
        (await w.DroneAsync(drone)).FlightStatus.Should().Be(DroneStatus.Idle);
    }

    [Fact]
    public async Task Start_IsRefusedBeforeTheWindow_ForAnUnusableUav_OrBeforeAcceptance()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var early = await w.CreateMissionAsync(Hour(5), Hour(7), inspector, drone);
        var inspector2 = await w.AddUserAsync();
        var drone2 = await w.AddDroneAsync();
        var now = await w.CreateMissionAsync(Hour(0), Hour(2), inspector2, drone2);

        // BR-30: nobody accepted yet. Even a manager cannot start it.
        var managerStart = () => w.AsManager(s => s.StartAsync(now.Id, default));
        (await managerStart.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().BeOneOf("INVALID_MISSION_STATE", "REQUIRED_ACCEPTANCE_PENDING");

        await w.AcceptAllAsync(early);
        var tooEarly = () => w.AsInspector(inspector, s => s.StartAsync(early.Id, default));
        (await tooEarly.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("MISSION_START_TOO_EARLY");

        await w.AcceptAllAsync(now);
        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "UAVs" SET "OperationalStatus" = 'Maintenance' WHERE "Id" = {drone2}"""));
        var maintenance = () => w.AsInspector(inspector2, s => s.StartAsync(now.Id, default));
        (await maintenance.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("DRONE_NOT_AVAILABLE");

        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "UAVs" SET "OperationalStatus" = 'Available', "TechnicalHealth" = 'Critical' WHERE "Id" = {drone2}"""));
        (await maintenance.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("DRONE_CRITICAL_HEALTH");

        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "UAVs" SET "TechnicalHealth" = 'Healthy' WHERE "Id" = {drone2}"""));
        await w.AsInspector(inspector2, s => s.StartAsync(now.Id, default));
        (await w.LoadMissionAsync(now.Id)).Status.Should().Be(MissionStatus.InProgress);
    }

    [Fact]
    public async Task CancelAfterStartAndSuspend_ReleasesBookingsUavHandoverAndAssessment()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var mission = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, drone);
        await w.AcceptAllAsync(mission);
        await w.AsInspector(inspector, s => s.StartAsync(mission.Id, default));
        await w.AsManager(s => s.SuspendMissionAsync(mission.Id, "Weather", default));

        var inProgressCancel = await w.LoadMissionAsync(mission.Id);
        inProgressCancel.Status.Should().Be(MissionStatus.Suspended);
        await w.AsManager(s => s.CancelMissionAsync(mission.Id, "Storm", default));

        var cancelled = await w.LoadMissionAsync(mission.Id);
        cancelled.Status.Should().Be(MissionStatus.Cancelled);
        cancelled.PreMissionAssessmentId.Should().BeNull();
        cancelled.DroneHandovers.Should().OnlyContain(h => h.ReturnedAt != null);
        (await w.BookingsAsync(mission.Id)).Should().OnlyContain(b => b.Status == ResourceBookingStatus.Cancelled);
        (await w.DroneAsync(drone)).FlightStatus.Should().Be(DroneStatus.Idle);
        var assessmentStatus = await w.Read(db => db.PreMissionAssessments.Where(a => a.Id == mission.PreMissionAssessmentId).Select(a => a.Status).SingleAsync());
        assessmentStatus.Should().Be(PreMissionAssessmentStatus.Ready);

        // The UAV and the inspector are free again for the same window.
        var again = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, drone);
        again.Status.Should().Be(MissionStatus.PendingAcceptance);
    }

    [Fact]
    public async Task DeletingADraftMission_CancelsItsBookings_AndLeakedBookingsAre409NotDbErrors()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();

        var draftId = Guid.NewGuid();
        await w.Write(db =>
        {
            db.Missions.Add(new Mission { Id = draftId, MissionCode = $"DRAFT-{w.Tag}", Title = "Draft", ManagerId = w.ManagerId, RegionId = w.RegionId, UavId = drone, Status = MissionStatus.Draft, PlannedStart = Hour(2), PlannedEnd = Hour(4) });
            db.ResourceBookings.Add(new ResourceBooking { MissionId = draftId, DroneId = drone, StartAt = Hour(2), EndAt = Hour(4) });
            return Task.CompletedTask;
        });

        var current = MissionWorld.Current(w.ManagerId, UserRoles.Manager);
        await using (var db = _pg.CreateContext(current))
        {
            await new DeleteMissionCommandHandler(new UnitOfWork(db), new MissionRepository(db), current).Handle(new DeleteMissionCommand(draftId), default);
        }
        (await w.Read(db => db.ResourceBookings.Where(b => b.MissionId == draftId).Select(b => b.Status).SingleAsync())).Should().Be(ResourceBookingStatus.Cancelled);
        var mission = await w.CreateMissionAsync(Hour(2), Hour(4), inspector, drone);
        mission.Status.Should().Be(MissionStatus.PendingAcceptance);

        // A booking left Active by a mission deleted before this fix is still seen by the application check.
        var drone2 = await w.AddDroneAsync();
        var leakedId = Guid.NewGuid();
        await w.Write(db =>
        {
            db.Missions.Add(new Mission { Id = leakedId, MissionCode = $"LEAK-{w.Tag}", Title = "Leaked", ManagerId = w.ManagerId, RegionId = w.RegionId, Status = MissionStatus.Draft, IsDeleted = true });
            db.ResourceBookings.Add(new ResourceBooking { MissionId = leakedId, DroneId = drone2, StartAt = Hour(2), EndAt = Hour(4) });
            return Task.CompletedTask;
        });
        var inspector2 = await w.AddUserAsync();
        var leaked = () => w.CreateMissionAsync(Hour(3), Hour(5), inspector2, drone2);
        var error = (await leaked.Should().ThrowAsync<BusinessRuleException>()).Which;
        error.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
        error.Message.Should().Contain($"LEAK-{w.Tag}");

        // The conflict-check endpoint sees it too.
        var check = await w.AsManager(s => s.CheckResourceConflictsAsync(
            new CheckResourceConflictsRequest(Hour(3), Hour(5), DroneIds: new[] { drone2 }), default));
        check.HasConflict.Should().BeTrue();
        check.Conflicts.Single().ConflictingMissionCode.Should().Be($"LEAK-{w.Tag}");
    }

    [Fact]
    public async Task ChangeUav_CancelsEveryOtherUavBookingOfTheMission()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var drone1 = await w.AddDroneAsync();
        var drone2 = await w.AddDroneAsync();
        var drone3 = await w.AddDroneAsync();
        var mission = await w.CreateMissionAsync(Hour(2), Hour(4), inspector, drone1);
        // A second Active UAV booking, as the old multi-drone create left behind.
        await w.Write(db =>
        {
            db.ResourceBookings.Add(new ResourceBooking { MissionId = mission.Id, DroneId = drone2, StartAt = Hour(2), EndAt = Hour(4) });
            return Task.CompletedTask;
        });

        await w.AsManager(s => s.AssignDroneAsync(mission.Id, drone3, default));

        var drones = (await w.BookingsAsync(mission.Id)).Where(b => b.DroneId != null).ToList();
        drones.Where(b => b.Status == ResourceBookingStatus.Active).Select(b => b.DroneId).Should().Equal(drone3);
        drones.Where(b => b.DroneId != drone3).Should().OnlyContain(b => b.Status == ResourceBookingStatus.Cancelled);
        (await w.LoadMissionAsync(mission.Id)).UavId.Should().Be(drone3);

        // drone1 and drone2 are free for another mission in the same window; a Critical or booked UAV is refused.
        var inspector2 = await w.AddUserAsync();
        var other = await w.CreateMissionAsync(Hour(2), Hour(4), inspector2, drone2);
        var booked = () => w.AsManager(s => s.AssignDroneAsync(other.Id, drone3, default));
        (await booked.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
        var critical = await w.AddDroneAsync(TechnicalHealth.Critical);
        var bad = () => w.AsManager(s => s.AssignDroneAsync(other.Id, critical, default));
        (await bad.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("DRONE_CRITICAL_HEALTH");
        await w.AsManager(s => s.AssignDroneAsync(other.Id, drone1, default));
    }

    private static async Task<(Mission? Mission, Exception? Error)> Capture(Func<Task<Mission>> create)
    {
        try { return (await create(), null); }
        catch (Exception ex) { return (null, ex); }
    }

    private static async Task Expect(string code, Func<Task> act) =>
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be(code);

    private static async Task<HttpStatusCode> Status(Exception exception) => (await Handle(exception)).Status;

    private static async Task<(HttpStatusCode Status, string Body)> Handle(Exception exception)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, default);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return ((HttpStatusCode)context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }
}
