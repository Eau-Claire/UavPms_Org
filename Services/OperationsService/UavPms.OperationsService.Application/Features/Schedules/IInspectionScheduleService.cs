using System;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Schedules.DTOs;

namespace UavPms.OperationsService.Application.Features.Schedules;

public interface IInspectionScheduleService
{
    Task<PaginatedInspectionSchedulesResponse> GetPagedAsync(
        int page,
        int pageSize,
        Guid? regionId,
        bool? isActive,
        string? search,
        CancellationToken ct);

    Task<InspectionScheduleDto> GetByIdAsync(Guid id, CancellationToken ct);

    Task<InspectionScheduleDto> CreateAsync(
        Guid regionId,
        string name,
        string recurrenceRule,
        TimeOnly plannedTime,
        bool isActive,
        CancellationToken ct);

    Task<InspectionScheduleDto> UpdateAsync(
        Guid id,
        Guid regionId,
        string name,
        string recurrenceRule,
        TimeOnly plannedTime,
        bool isActive,
        CancellationToken ct);

    Task<InspectionScheduleDto> ToggleActiveAsync(Guid id, bool? isActive, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    Task<TriggerScheduleResultDto> TriggerAsync(Guid id, CancellationToken ct);
}

