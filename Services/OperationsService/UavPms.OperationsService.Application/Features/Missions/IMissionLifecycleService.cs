using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Application.Features.Missions;

public record Mf01CreateMission(string Title, Guid RegionId, MissionType MissionType, Guid? ScheduleId,
    string? TriggerReason, DateTime PlannedStart, DateTime PlannedEnd, string? Description,
    DateTime? ConfirmationDeadline = null, string? ManagerInstructions = null, Guid? AssignedToUserId = null,
    Guid? DroneId = null);
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
    Task<MissionAssignment> AcceptAssignmentAsync(Guid missionId, CancellationToken ct);
    Task<MissionAssignment> PostponeAssignmentAsync(Guid missionId, string reason, CancellationToken ct);
    Task StartAsync(Guid missionId, CancellationToken ct);
    Task CompleteAsync(Guid missionId, CancellationToken ct);
    Task CancelAsync(Guid missionId, CancellationToken ct);

    // MF02 Realtime Lifecycle Methods
    Task<Mission> ConfirmMissionAsync(Guid missionId, string? reason, CancellationToken ct);
    Task<Mission> SuspendMissionAsync(Guid missionId, string reason, CancellationToken ct);
    Task<Mission> ResumeMissionAsync(Guid missionId, string? reason, CancellationToken ct);
    Task<Mission> PostponeMissionAsync(Guid missionId, string reason, CancellationToken ct);
    Task<Mission> CancelMissionAsync(Guid missionId, string? reason, CancellationToken ct);
    Task RemindMissionAsync(Guid missionId, string? reason, CancellationToken ct);
    Task<MissionCommunicationLogDto> AddCommunicationAsync(Guid missionId, string message, CancellationToken ct);
    Task<IReadOnlyList<MissionCommunicationLogDto>> GetCommunicationsAsync(Guid missionId, CancellationToken ct);

    // MF02 Linear Workflow & Extended APIs
    Task<IReadOnlyList<UavPms.OperationsService.Application.Features.Missions.DTOs.MissionDetectionDto>> GetMissionDetectionsAsync(Guid missionId, string? status, string? mediaType, bool? isEmergency, CancellationToken ct);
    Task<UavPms.OperationsService.Application.Features.Missions.DTOs.ReviewDetectionResultDto> ReviewDetectionAsync(Guid missionId, Guid detectionId, UavPms.OperationsService.Application.Features.Missions.DTOs.ReviewDetectionRequest request, CancellationToken ct);
    Task<IReadOnlyList<UavPms.OperationsService.Application.Features.Missions.DTOs.MissionMaintenanceTaskDto>> GetMissionMaintenanceTasksAsync(Guid missionId, CancellationToken ct);
    Task<IReadOnlyList<UavPms.OperationsService.Application.Features.Missions.DTOs.MissionActivityDto>> GetActivitiesAsync(Guid missionId, CancellationToken ct);
    Task<UavPms.OperationsService.Application.Features.Missions.DTOs.MissionActivityDto> AddActivityAsync(Guid missionId, UavPms.OperationsService.Application.Features.Missions.DTOs.CreateMissionActivityRequest request, CancellationToken ct);
    Task<UavPms.OperationsService.Application.Features.Missions.DTOs.MissionAssignmentsOverviewDto> GetAssignmentsOverviewAsync(Guid missionId, CancellationToken ct);
}

