using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// MF02/MF03 state rules (BR-05 Table 59, BR-30, BR-32, BR-35 – BR-38) on real PostgreSQL: responses, changes and
/// field steps are refused in the wrong state instead of silently moving the mission.
/// </summary>
[Collection(PostgresCollection.Name)]
public class MissionStateGuardTests
{
    private readonly PostgresFixture _pg;

    public MissionStateGuardTests(PostgresFixture pg) => _pg = pg;

    private static DateTime Hour(int hoursFromNow) => DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour + hoursFromNow);

    [Fact]
    public async Task AcceptAndPostpone_AreRefusedOnCancelledPostponedAndStartedMissions()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);

        // Cancelled.
        var i1 = await w.AddUserAsync();
        var cancelled = await w.CreateMissionAsync(Hour(1), Hour(3), i1, await w.AddDroneAsync());
        await w.AsManager(s => s.CancelMissionAsync(cancelled.Id, "No longer needed", default));
        await Expect("INVALID_MISSION_STATE", () => w.AsInspector(i1, s => s.AcceptAssignmentAsync(cancelled.Id, default)));
        await Expect("INVALID_MISSION_STATE", () => w.AsInspector(i1, s => s.PostponeAssignmentAsync(cancelled.Id, "Sick", default)));
        await Expect("INVALID_MISSION_STATE", () => w.AsInspector(i1, s => s.ConfirmMissionAsync(cancelled.Id, null, default)));

        // Postponed by its required member: Table 59 only allows Cancelled next.
        var i2 = await w.AddUserAsync();
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var postponed = await w.CreateMissionAsync(Hour(1), Hour(3), i2, await w.AddDroneAsync(), new MissionPersonnelAssignmentRequest(analyst, "ANALYST", false));
        await w.As(analyst, UserRoles.Analyst, s => s.PostponeAssignmentAsync(postponed.Id, "Other duty", default));
        var afterOptional = await w.LoadMissionAsync(postponed.Id);
        afterOptional.Status.Should().Be(MissionStatus.PendingAcceptance);
        afterOptional.PostponeReason.Should().BeNull("an optional member only answers for themselves");

        await w.AsInspector(i2, s => s.PostponeAssignmentAsync(postponed.Id, "Equipment missing", default));
        var mission = await w.LoadMissionAsync(postponed.Id);
        mission.Status.Should().Be(MissionStatus.Postponed);
        mission.PostponeReason.Should().Be("Equipment missing");
        await Expect("INVALID_MISSION_STATE", () => w.As(analyst, UserRoles.Analyst, s => s.AcceptAssignmentAsync(postponed.Id, default)));

        // In progress, then completed.
        var i3 = await w.AddUserAsync();
        var running = await w.CreateMissionAsync(Hour(0), Hour(2), i3, await w.AddDroneAsync());
        await w.AcceptAllAsync(running);
        await w.AsInspector(i3, s => s.StartAsync(running.Id, default));
        await Expect("INVALID_MISSION_STATE", () => w.AsInspector(i3, s => s.PostponeAssignmentAsync(running.Id, "Too late", default)));
        await w.AsInspector(i3, s => s.CompleteAsync(running.Id, default));
        await Expect("INVALID_MISSION_STATE", () => w.AsInspector(i3, s => s.PostponeAssignmentAsync(running.Id, "Too late", default)));
        (await w.LoadMissionAsync(running.Id)).Status.Should().Be(MissionStatus.Completed);
    }

    [Fact]
    public async Task Confirm_IsOnlyTheCallersAcceptance_AndMissionPostponeIsAManagerAction()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var outsider = await w.AddUserAsync();
        var mission = await w.CreateMissionAsync(Hour(1), Hour(3), inspector, await w.AddDroneAsync(), new MissionPersonnelAssignmentRequest(analyst, "ANALYST", false));

        await w.As(analyst, UserRoles.Analyst, s => s.ConfirmMissionAsync(mission.Id, "Ready", default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.PendingAcceptance, "a non-required member cannot confirm the mission");

        var notMember = () => w.AsInspector(outsider, s => s.ConfirmMissionAsync(mission.Id, null, default));
        (await notMember.Should().ThrowAsync<ForbiddenException>()).Which.Message.Should().Contain("USER_NOT_ASSIGNED");

        var inspectorPostpone = () => w.AsInspector(inspector, s => s.PostponeMissionAsync(mission.Id, "I want to", default));
        await inspectorPostpone.Should().ThrowAsync<ForbiddenException>();

        await w.AsInspector(inspector, s => s.ConfirmMissionAsync(mission.Id, "Ready", default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Assigned);

        await w.AsManager(s => s.PostponeMissionAsync(mission.Id, "Line maintenance", default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Postponed);
        var notified = await w.Read(db => db.Notifications.Where(n => n.ReferenceId == mission.Id && n.Type == "MISSION_POSTPONED").Select(n => n.UserId).ToListAsync());
        notified.Should().BeEquivalentTo(new[] { inspector, analyst });
        await Expect("INVALID_MISSION_STATE", () => w.AsManager(s => s.PostponeMissionAsync(mission.Id, "Again", default)));
    }

    [Fact]
    public async Task ChangesAfterStartAndSuspend_AreRefused()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var spare = await w.AddUserAsync();
        var spareDrone = await w.AddDroneAsync();
        var mission = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, await w.AddDroneAsync());
        await w.AcceptAllAsync(mission);
        await w.AsInspector(inspector, s => s.StartAsync(mission.Id, default));
        await w.AsManager(s => s.SuspendMissionAsync(mission.Id, "Wind", default));
        var assignmentId = (await w.LoadMissionAsync(mission.Id)).Assignments.Single().Id;

        const string code = "MISSION_IMMUTABLE_AFTER_START";
        await Expect(code, () => w.AsManager(s => s.AssignAsync(mission.Id, new Mf01Assignment(spare, "INSPECTOR"), default)));
        await Expect(code, () => w.AsManager(s => s.RemoveAssignmentAsync(mission.Id, assignmentId, default)));
        await Expect(code, () => w.AsManager(s => s.ReassignAsync(mission.Id, assignmentId, spare, null, "Sick", default)));
        await Expect(code, () => w.AsManager(s => s.AssignDroneAsync(mission.Id, spareDrone, default)));
        await Expect(code, () => w.AsManager(s => s.ConfirmAssetsAsync(mission.Id, "", w.TowerIds, default)));
        await Expect(code, () => w.AsManager(s => s.UpdatePriorityAsync(mission.Id, new UpdateMissionPriorityRequest(MissionPriority.High), default)));

        await w.AsManager(s => s.ResumeMissionAsync(mission.Id, null, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.InProgress);
    }

    [Fact]
    public async Task NewRequiredMember_ReturnsTheMissionToPendingAcceptance_AndResumeRechecksAcceptance()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var technician = await w.AddUserAsync(UserRoles.Technician);
        var analyst2 = await w.AddUserAsync(UserRoles.Analyst);
        var mission = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, await w.AddDroneAsync());
        await w.AcceptAllAsync(mission);
        await w.AsInspector(inspector, s => s.CheckInAsync(mission.Id, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Preparing);

        // An optional member does not block anything.
        await w.AsManager(s => s.AssignAsync(mission.Id, new Mf01Assignment(technician, "TECHNICIAN", IsRequired: false), default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Preparing);

        // A required one does, and the mission cannot start until they accept.
        await w.AsManager(s => s.AssignAsync(mission.Id, new Mf01Assignment(analyst, "ANALYST"), default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.PendingAcceptance);
        await Expect("INVALID_MISSION_STATE", () => w.AsManager(s => s.StartAsync(mission.Id, default)));
        await w.As(analyst, UserRoles.Analyst, s => s.AcceptAssignmentAsync(mission.Id, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Preparing, "the earlier check-in still counts");

        // Suspended before the start, a reassignment is checked again on resume.
        await w.AsManager(s => s.SuspendMissionAsync(mission.Id, "Paperwork", default));
        var analystAssignment = (await w.LoadMissionAsync(mission.Id)).Assignments.Single(a => a.UserId == analyst && a.Status == MissionAssignmentStatus.Active);
        await w.AsManager(s => s.ReassignAsync(mission.Id, analystAssignment.Id, analyst2, null, "Swap", default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Suspended);
        await w.AsManager(s => s.ResumeMissionAsync(mission.Id, null, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.PendingAcceptance);

        // Removing the pending required member completes the acceptance; the last required one cannot be removed.
        var pending = (await w.LoadMissionAsync(mission.Id)).Assignments.Single(a => a.UserId == analyst2 && a.Status == MissionAssignmentStatus.Active);
        await w.AsManager(s => s.RemoveAssignmentAsync(mission.Id, pending.Id, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Preparing);
        var last = (await w.LoadMissionAsync(mission.Id)).Assignments.Single(a => a.UserId == inspector && a.Status == MissionAssignmentStatus.Active);
        await Expect("REQUIRED_MEMBER_REQUIRED", () => w.AsManager(s => s.RemoveAssignmentAsync(mission.Id, last.Id, default)));
        (await w.BookingsAsync(mission.Id)).Single(b => b.UserId == analyst2).Status.Should().Be(ResourceBookingStatus.Cancelled);
    }

    [Fact]
    public async Task Reassign_ChecksTheTargetAndTheNewUser_AndHandsTheSeatOver()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var replacement = await w.AddUserAsync();
        var manager2 = await w.AddUserAsync(UserRoles.Manager);
        var outOfScope = await w.AddUserAsync(inScope: false);
        var busy = await w.AddUserAsync();
        var mission = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, await w.AddDroneAsync(), new MissionPersonnelAssignmentRequest(analyst, "ANALYST", true));
        await w.CreateMissionAsync(Hour(1), Hour(3), busy, await w.AddDroneAsync());
        await w.AcceptAllAsync(mission);
        await w.AsInspector(inspector, s => s.CheckInAsync(mission.Id, default));
        var inspectorSeat = (await w.LoadMissionAsync(mission.Id)).Assignments.Single(a => a.UserId == inspector);

        var unknown = () => w.AsManager(s => s.ReassignAsync(mission.Id, Guid.NewGuid(), replacement, null, null, default));
        await unknown.Should().ThrowAsync<NotFoundException>();
        await Expect("ASSIGNEE_ALREADY_ASSIGNED", () => w.AsManager(s => s.ReassignAsync(mission.Id, inspectorSeat.Id, analyst, null, null, default)));
        await Expect("ASSIGNEE_ROLE_NOT_ELIGIBLE", () => w.AsManager(s => s.ReassignAsync(mission.Id, inspectorSeat.Id, manager2, null, null, default)));
        await Expect("ASSIGNEE_OUT_OF_SCOPE", () => w.AsManager(s => s.ReassignAsync(mission.Id, inspectorSeat.Id, outOfScope, null, null, default)));
        await Expect("RESOURCE_BOOKING_CONFLICT", () => w.AsManager(s => s.ReassignAsync(mission.Id, inspectorSeat.Id, busy, null, null, default)));
        (await w.LoadMissionAsync(mission.Id)).Assignments.Count(a => a.Status == MissionAssignmentStatus.Active).Should().Be(2, "refused reassignments add nobody");

        var seat = await w.AsManager(s => s.ReassignAsync(mission.Id, inspectorSeat.Id, replacement, null, "Sick", default));
        seat.UserId.Should().Be(replacement);
        seat.ResponseStatus.Should().Be(MissionAssignmentResponse.Pending);

        var after = await w.LoadMissionAsync(mission.Id);
        after.Status.Should().Be(MissionStatus.PendingAcceptance);
        var old = after.Assignments.Single(a => a.Id == inspectorSeat.Id);
        old.Status.Should().Be(MissionAssignmentStatus.Revoked);
        old.ResponseStatus.Should().Be(MissionAssignmentResponse.Replaced);
        after.CheckIns.Single(c => c.UserId == inspector).Status.Should().Be(MissionCheckInStatus.Revoked);
        var bookings = await w.BookingsAsync(mission.Id);
        bookings.Single(b => b.UserId == inspector).Status.Should().Be(ResourceBookingStatus.Released);
        bookings.Single(b => b.UserId == replacement).Status.Should().Be(ResourceBookingStatus.Active);
        var notifications = await w.Read(db => db.Notifications.Where(n => n.ReferenceId == mission.Id).Select(n => new { n.UserId, n.Type }).ToListAsync());
        notifications.Should().ContainEquivalentOf(new { UserId = replacement, Type = "MISSION_ASSIGNED" });
        notifications.Should().ContainEquivalentOf(new { UserId = inspector, Type = "MISSION_ASSIGNMENT_REMOVED" });

        // The replaced inspector can come back later and check in again (one check-in row per member and mission).
        var replacementSeat = after.Assignments.Single(a => a.UserId == replacement && a.Status == MissionAssignmentStatus.Active);
        await w.AsManager(s => s.ReassignAsync(mission.Id, replacementSeat.Id, inspector, null, "Recovered", default));
        await w.AsInspector(inspector, s => s.AcceptAssignmentAsync(mission.Id, default));
        await w.AsInspector(inspector, s => s.CheckInAsync(mission.Id, default));
        (await w.LoadMissionAsync(mission.Id)).CheckIns.Single(c => c.UserId == inspector).Status.Should().Be(MissionCheckInStatus.CheckedIn);
    }

    [Fact]
    public async Task SuspendAndResume_WithoutAnInspector_NotifyEachMemberOnce()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var drone = await w.AddDroneAsync();
        var assessment = await w.AddAssessmentAsync(Hour(1), Hour(3), new[] { analyst }, new[] { drone });
        var mission = await w.CreateMissionAsync(MissionWorld.Request(assessment, new[] { new MissionPersonnelAssignmentRequest(analyst, "ANALYST", true) }, new[] { drone }));
        await w.As(analyst, UserRoles.Analyst, s => s.AcceptAssignmentAsync(mission.Id, default));

        await w.AsManager(s => s.SuspendMissionAsync(mission.Id, "Check", default));
        await w.AsManager(s => s.ResumeMissionAsync(mission.Id, null, default));

        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Assigned);
        var types = await w.Read(db => db.Notifications.Where(n => n.ReferenceId == mission.Id && n.UserId == analyst).Select(n => n.Type).ToListAsync());
        types.Count(t => t == "MISSION_SUSPENDED").Should().Be(1);
        types.Count(t => t == "MISSION_RESUMED").Should().Be(1);
        (await w.Read(db => db.Notifications.AnyAsync(n => n.ReferenceId == mission.Id && n.UserId == Guid.Empty))).Should().BeFalse();
    }

    [Fact]
    public async Task Handover_RejectedIsClosed_BlocksStartUntilANewHandover_AndIsOnePerUav()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var analyst = await w.AddUserAsync(UserRoles.Analyst);
        var outsider = await w.AddUserAsync();
        var drone = await w.AddDroneAsync();
        var mission = await w.CreateMissionAsync(Hour(0), Hour(2), inspector, drone, new MissionPersonnelAssignmentRequest(analyst, "ANALYST", false));
        await w.AcceptAllAsync(mission);

        var byOutsider = () => w.AsInspector(outsider, s => s.ConfirmHandoverAsync(mission.Id, new Mf01Handover(drone, inspector, "OK", true), default));
        await byOutsider.Should().ThrowAsync<ForbiddenException>();

        var rejected = await w.As(inspector, UserRoles.Inspector, s => s.ConfirmHandoverAsync(mission.Id, new Mf01Handover(drone, inspector, "Cracked propeller", false), default));
        rejected.ReturnedAt.Should().NotBeNull();
        await Expect("HANDOVER_REJECTED", () => w.AsInspector(inspector, s => s.StartAsync(mission.Id, default)));

        await w.AsInspector(inspector, s => s.ConfirmHandoverAsync(mission.Id, new Mf01Handover(drone, inspector, "Propeller replaced", true), default));
        await Expect("DUPLICATE_HANDOVER", () => w.AsInspector(inspector, s => s.ConfirmHandoverAsync(mission.Id, new Mf01Handover(drone, inspector, "Again", true), default)));

        // A pending optional member does not keep the mission from Ready (BR-37).
        await w.AsInspector(inspector, s => s.CheckInAsync(mission.Id, default));
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.Ready);

        // The same UAV booked later by another mission cannot be handed over while this handover is open.
        var inspector2 = await w.AddUserAsync();
        var next = await w.CreateMissionAsync(Hour(2), Hour(4), inspector2, drone);
        await w.AcceptAllAsync(next);
        await Expect("DUPLICATE_HANDOVER", () => w.AsInspector(inspector2, s => s.ConfirmHandoverAsync(next.Id, new Mf01Handover(drone, inspector2, "OK", true), default)));

        await w.AsInspector(inspector, s => s.StartAsync(mission.Id, default));
        (await w.LoadMissionAsync(mission.Id)).DroneHandovers.Count(h => h.ReturnedAt == null).Should().Be(1, "start reuses the open handover");
    }

    [Fact]
    public async Task DeactivatedManager_CannotActWithAValidToken()
    {
        if (!_pg.Available) return;
        var w = await MissionWorld.CreateAsync(_pg);
        var inspector = await w.AddUserAsync();
        var mission = await w.CreateMissionAsync(Hour(1), Hour(3), inspector, await w.AddDroneAsync());
        await w.Write(db => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Users" SET "Status" = 'Inactive' WHERE "Id" = {w.ManagerId}"""));

        var cancel = () => w.AsManager(s => s.CancelMissionAsync(mission.Id, null, default));
        (await cancel.Should().ThrowAsync<ForbiddenException>()).Which.Message.Should().Contain("ACTIVE_USER_REQUIRED");
        (await w.LoadMissionAsync(mission.Id)).Status.Should().Be(MissionStatus.PendingAcceptance);
    }

    private static async Task Expect(string code, Func<Task> act) =>
        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be(code);
}
