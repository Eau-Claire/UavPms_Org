using System;
using System.Collections.Generic;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record ReportMissionSummaryDto(
    Guid Id,
    string MissionCode,
    string Title,
    string Status,
    DateTime? ScheduledStartAt
);

public record ReportDetailDto(
    Guid Id,
    string Code,
    string Title,
    string Type,
    string Status,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    ReportNestedEntityDto? TransmissionLine,
    ReportNestedEntityDto? Substation,
    ReportCreatorDto? Creator,
    int DefectCount,
    long? FileSizePdf,
    long? FileSizeExcel,
    string? PdfFileUrl,
    string? ExcelFileUrl,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? RejectionReason,
    ReportCreatorDto? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    List<ReportMissionSummaryDto> Missions
);
