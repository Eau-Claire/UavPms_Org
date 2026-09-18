using System;

namespace UavPms.OperationsService.Application.Features.Reports.DTOs;

public record ReportNestedEntityDto(Guid Id, string Name);

public record ReportCreatorDto(Guid Id, string FullName);

public record ReportDto(
    Guid Id,
    string Code,
    string Title,
    string Type,
    string Status,
    DateTime CreatedAt,
    ReportNestedEntityDto? TransmissionLine,
    ReportNestedEntityDto? Substation,
    ReportCreatorDto? Creator,
    int DefectCount,
    long? FileSizePdf,
    long? FileSizeExcel
);
