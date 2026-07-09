using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Application.Features.Monitor.DTOs;
using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Monitor.Queries.GetRecentDefects;

public class GetRecentDefectsQueryHandler : IRequestHandler<GetRecentDefectsQuery, RecentDefectsResponse>
{
    private readonly IMonitorRepository _monitorRepository;

    public GetRecentDefectsQueryHandler(IMonitorRepository monitorRepository)
    {
        _monitorRepository = monitorRepository;
    }

    public async Task<RecentDefectsResponse> Handle(GetRecentDefectsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _monitorRepository.GetRecentDefectsAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = items.Select(x => new RecentDefectDto(
            x.InspectionId,
            x.MissionId,
            x.MissionTitle,
            x.ImageUrl,
            x.DefectType,
            x.DetectedAt
            )).ToList();

        var totalPages = request.PageSize > 0 ? 
        ((int)Math.Ceiling((double)totalCount / request.PageSize)) : 0;

        var pagination = new PaginationMetaData(
            request.Page,
            request.PageSize,
            totalCount,
            totalPages
        );

        return new RecentDefectsResponse(dtos, pagination);
    }
}
