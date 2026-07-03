using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UavPms.Application.Features.Monitor.DTOs;
using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Monitor.Queries.GetRecentDefects;

public record GetRecentDefectsQuery(int Page, int PageSize) : IRequest<RecentDefectsResponse>;

public record RecentDefectDto(
    Guid InspectionId,
    Guid MissionId,
    string MissionTitle,
    string ImageUrl,
    string DefectType,
    DateTime DetectedAt
);

public record RecentDefectsResponse(
    List<RecentDefectDto> Items,
    PaginationMetaData Pagination
);
