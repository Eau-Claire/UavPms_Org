using FluentAssertions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionLifecycleTests
{
    [Fact]
    public void CannotStartUnlessReady()
    {
        var mission = new Mission { Status = MissionStatus.Preparing };
        mission.Invoking(x => x.Start()).Should().Throw<InvalidOperationException>().WithMessage("MISSION_NOT_READY");
    }

    [Fact]
    public void ReadyMissionStartsOnlyOnce()
    {
        var mission = new Mission { Status = MissionStatus.Ready };
        mission.Start();
        mission.Status.Should().Be(MissionStatus.InProgress);
        mission.Invoking(x => x.Start()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadinessRequiresAssignmentsCheckInsDroneHandoverAndAssets()
    {
        var user = Guid.NewGuid(); var drone = Guid.NewGuid();
        var mission = new Mission { Status = MissionStatus.Assigned, UavId = drone };
        mission.Assignments.Add(new MissionAssignment { UserId = user });
        mission.MissionTargets.Add(new MissionTarget { AssetId = Guid.NewGuid() });
        mission.RecalculateReadiness().Should().BeFalse();
        mission.CheckIns.Add(new MissionCheckIn { UserId = user });
        mission.DroneHandovers.Add(new DroneHandover { DroneId = drone, Status = DroneHandoverStatus.Rejected });
        mission.RecalculateReadiness().Should().BeFalse();
        mission.DroneHandovers.Single().Status = DroneHandoverStatus.Accepted;
        mission.RecalculateReadiness().Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Ready);
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
