using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Application.Features.Assessments;

public interface IDroneTechnicalInspectionService
{
    Task<DroneTechnicalInspection> SubmitInspectionAsync(DroneInspectionSubmitRequest request, CancellationToken ct);
    Task<DroneTechnicalInspection?> GetLatestInspectionAsync(Guid droneId, CancellationToken ct);
}
