using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Assessments;

public interface IPreMissionAssessmentService
{
    Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> towerIds,
        CancellationToken ct);

    Task<PreMissionAssessment> CreateAsync(
        Guid regionId,
        DateTime plannedStart,
        DateTime plannedEnd,
        IReadOnlyCollection<Guid> towerIds,
        string? boundaryWkt,
        string? idempotencyKey,
        CancellationToken ct);

    Task<PreMissionAssessment> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PreMissionAssessment>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<PreMissionAssessment>> ListAsync(string? status, CancellationToken ct);
    Task<PreMissionAssessment> EvaluateAsync(Guid id, CancellationToken ct);
    Task<Mission> CreateMissionFromAssessmentAsync(CreateMissionFromAssessmentRequest request, CancellationToken ct);
    Task<Mission> CreateMissionAsync(Guid assessmentId, string title, Guid inspectorId, Guid droneId, CancellationToken ct);
    Task<PreMissionAssessment> MarkCompletedAsync(Guid assessmentId, Guid? missionId, CancellationToken ct);
    Task<PreMissionAssessment> CancelAsync(Guid id, string? reason, CancellationToken ct);
}
