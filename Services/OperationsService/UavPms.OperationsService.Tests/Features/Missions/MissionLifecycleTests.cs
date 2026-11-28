using FluentAssertions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionLifecycleTests
{
    [Fact]
    public void CannotStartIfPending()
    {
        var mission = new Mission { Status = MissionStatus.Draft };
        mission.Invoking(x => x.Start()).Should().Throw<InvalidOperationException>().WithMessage("*Mission must be Ready, Assigned, or Preparing*");
    }

    [Fact]
    public void ReadyMissionStartsOnlyOnce()
    {
        var mission = new Mission { Status = MissionStatus.Ready };
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.Start();
        mission.Status.Should().Be(MissionStatus.InProgress);
        mission.Invoking(x => x.Start()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CannotStartWhileARequiredMemberHasNotAccepted()
    {
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = Guid.NewGuid() };
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), ResponseStatus = MissionAssignmentResponse.Pending });
        mission.Invoking(x => x.Start()).Should().Throw<InvalidOperationException>().WithMessage("*required member*");
    }

    [Fact]
    public void NewRequiredMemberSendsAssignedMissionBackToPendingAcceptance()
    {
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = Guid.NewGuid() };
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), ResponseStatus = MissionAssignmentResponse.Pending });
        mission.RecalculateReadiness().Should().BeFalse();
        mission.Status.Should().Be(MissionStatus.PendingAcceptance);
    }

    [Fact]
    public void PendingOptionalMemberDoesNotBlockReadiness()
    {
        var inspector = Guid.NewGuid(); var drone = Guid.NewGuid();
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = drone };
        mission.Assignments.Add(new MissionAssignment { UserId = inspector, AssignmentRole = MissionAssignment.InspectorRole, ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), IsRequired = false, ResponseStatus = MissionAssignmentResponse.Pending });
        mission.MissionTargets.Add(new MissionTarget { TowerId = Guid.NewGuid() });
        mission.CheckIns.Add(new MissionCheckIn { UserId = inspector });
        mission.RecalculateReadiness().Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Ready);
    }

    [Fact]
    public void ResumeBeforeStartReturnsToPendingAcceptanceWhenARequiredMemberHasNotAccepted()
    {
        var mission = new Mission { Status = MissionStatus.Suspended, UavId = Guid.NewGuid() };
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), ResponseStatus = MissionAssignmentResponse.Pending });
        mission.Resume();
        mission.Status.Should().Be(MissionStatus.PendingAcceptance);
    }

    [Fact]
    public void ReadinessRequiresAssignmentsCheckInsAndAssets()
    {
        var user = Guid.NewGuid(); var drone = Guid.NewGuid();
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = drone };
        mission.Assignments.Add(new MissionAssignment { UserId = user, AssignmentRole = MissionAssignment.InspectorRole, ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.MissionTargets.Add(new MissionTarget { TowerId = Guid.NewGuid() });
        mission.RecalculateReadiness().Should().BeFalse();
        mission.CheckIns.Add(new MissionCheckIn { UserId = user });
        mission.RecalculateReadiness().Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Ready);
    }

    [Fact]
    public void OnlyTheInspectorChecksIn_AnalystAndTechnicianDoNot()
    {
        // Spec §4: chỉ Inspector (người bay) check-in tại cột số 1; Analyst/Technician làm việc tại văn phòng.
        var inspector = Guid.NewGuid();
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = Guid.NewGuid() };
        foreach (var (userId, role) in new[] { (inspector, MissionAssignment.InspectorRole), (Guid.NewGuid(), MissionAssignment.AnalystRole), (Guid.NewGuid(), MissionAssignment.TechnicianRole) })
            mission.Assignments.Add(new MissionAssignment { UserId = userId, AssignmentRole = role, ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.MissionTargets.Add(new MissionTarget { TowerId = Guid.NewGuid() });

        mission.RecalculateReadiness().Should().BeFalse();
        mission.CheckIns.Add(new MissionCheckIn { UserId = inspector });
        mission.RecalculateReadiness().Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Ready);
    }

    [Fact]
    public void MissionWithoutInspector_IsNeverReady()
    {
        var mission = new Mission { Status = MissionStatus.Confirmed, UavId = Guid.NewGuid() };
        mission.Assignments.Add(new MissionAssignment { UserId = Guid.NewGuid(), AssignmentRole = MissionAssignment.AnalystRole, ResponseStatus = MissionAssignmentResponse.Accepted });
        mission.MissionTargets.Add(new MissionTarget { TowerId = Guid.NewGuid() });

        mission.RecalculateReadiness().Should().BeFalse();
        mission.Status.Should().Be(MissionStatus.Confirmed);
    }

    [Fact]
    public void InProgressCompletesAndCannotNormallyCancel()
    {
        var mission = new Mission { Status = MissionStatus.InProgress };
        mission.Invoking(x => x.Cancel()).Should().Throw<InvalidOperationException>();
        mission.Complete();
        mission.Status.Should().Be(MissionStatus.Completed);
        mission.EndedAt.Should().NotBeNull();
    }
}
