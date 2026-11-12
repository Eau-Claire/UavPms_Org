using System;
using System.Collections.Generic;
using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Schedules.DTOs;

public class InspectionScheduleDto
{
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RecurrenceRule { get; set; } = string.Empty;
    public TimeOnly PlannedTime { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record PaginatedInspectionSchedulesResponse(
    List<InspectionScheduleDto> Items,
    PaginationMetaData Pagination);

public record TriggerScheduleResultDto(
    Guid ScheduleId,
    Guid AssessmentId,
    string AssessmentStatus,
    int TowerCount,
    DateTime PlannedStart,
    DateTime PlannedEnd,
    DateTime TriggeredAt);

