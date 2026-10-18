using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Drones.DTOs;

namespace UavPms.OperationsService.Application.Features.Drones;

public interface IUavManagementService
{
    Task<IReadOnlyList<UavDetailsDto>> GetDronesAsync(
        string? search = null,
        string? operationalStatus = null,
        string? technicalHealth = null,
        Guid? regionId = null,
        bool? availableOnly = null,
        CancellationToken ct = default);

    Task<UavDetailsDto> GetDroneByIdAsync(Guid droneId, CancellationToken ct = default);

    Task<UavDetailsDto> CreateDroneAsync(CreateUavRequest request, CancellationToken ct = default);

    Task<UavDetailsDto> UpdateDroneAsync(Guid droneId, UpdateUavRequest request, CancellationToken ct = default);

    Task<bool> DeleteDroneAsync(Guid droneId, CancellationToken ct = default);

    Task<IReadOnlyList<DroneTechnicalInspectionSummaryDto>> GetTechnicalInspectionsForDroneAsync(
        Guid droneId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<DroneTechnicalInspectionSummaryDto> SubmitBasicTechnicalCheckAsync(
        Guid droneId,
        BasicTechnicalCheckRequest request,
        CancellationToken ct = default);

    Task<GisDroneEstimationResult> EstimateGisScopeDronesAsync(
        GisDroneEstimationRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<GisDroneCapabilityItemDto>> GetEligibleDronesForAssessmentAsync(
        Guid assessmentId,
        CancellationToken ct = default);
}
