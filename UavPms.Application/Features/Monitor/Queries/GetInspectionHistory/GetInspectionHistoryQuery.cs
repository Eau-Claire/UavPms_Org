using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.Application.Features.Monitor.DTOs;
using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Monitor.Queries.GetInspectionHistory;

public record GetInspectionHistoryQuery(
    Guid? MissionId,
    bool? IsDefect,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize
) : IRequest<InspectionHistoryResponse>;

public record InspectionHistoryDto(
    Guid InspectionId,
    Guid MissionId,
    string MissionTitle,
    string ImageUrl,
    bool IsDefect,
    string DefectType,
    DateTime DetectedAt
);

public record InspectionHistoryResponse(
    List<InspectionHistoryDto> Items,
    PaginationMetaData Pagination
);
