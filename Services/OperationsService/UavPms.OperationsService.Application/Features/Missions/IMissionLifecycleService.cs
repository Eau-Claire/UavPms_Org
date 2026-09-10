using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Missions;

public record Mf01CreateMission(string Title, Guid RegionId, MissionType MissionType, Guid? ScheduleId,
    string? TriggerReason, DateTime PlannedStart, DateTime PlannedEnd, string? Description);
public record Mf01Assignment(Guid UserId, string AssignmentRole);
public record Mf01Handover(Guid DroneId, Guid ReceivedBy, string Condition, bool Accepted);

public interface IMissionLifecycleService
{
    Task<Mission> CreateAsync(Mf01CreateMission request, CancellationToken ct);
    Task<IReadOnlyList<Asset>> ResolveScopeAsync(Guid missionId, string boundaryWkt, CancellationToken ct);
    Task ConfirmAssetsAsync(Guid missionId, string boundaryWkt, IReadOnlyCollection<Guid> assetIds, CancellationToken ct);
    Task<MissionAssignment> AssignAsync(Guid missionId, Mf01Assignment request, CancellationToken ct);
    Task RemoveAssignmentAsync(Guid missionId, Guid assignmentId, CancellationToken ct);
    Task AssignDroneAsync(Guid missionId, Guid droneId, CancellationToken ct);
    Task<DroneHandover> ConfirmHandoverAsync(Guid missionId, Mf01Handover request, CancellationToken ct);
    Task<MissionCheckIn> CheckInAsync(Guid missionId, CancellationToken ct);
    Task StartAsync(Guid missionId, CancellationToken ct);
    Task CompleteAsync(Guid missionId, CancellationToken ct);
    Task CancelAsync(Guid missionId, CancellationToken ct);
}
