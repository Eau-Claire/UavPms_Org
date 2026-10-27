using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportsPaged;

public class GetReportsPagedQueryHandler : IRequestHandler<GetReportsPagedQuery, PaginatedReportsResponse>
{
    private readonly IReportRepository _reportRepository;

    public GetReportsPagedQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<PaginatedReportsResponse> Handle(GetReportsPagedQuery request, CancellationToken cancellationToken)
    {
        int page = request.Page < 1 ? 1 : request.Page;
        int pageSize = request.PageSize <= 0 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        ReportType? reportType = null;
        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<ReportType>(request.Type, true, out var parsedType))
        {
            reportType = parsedType;
        }

        ReportStatus? reportStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReportStatus>(request.Status, true, out var parsedStatus))
        {
            reportStatus = parsedStatus;
        }

        var (items, totalCount) = await _reportRepository.GetReportsPagedAsync(
            page,
            pageSize,
            reportType,
            reportStatus,
            request.RegionId,
            request.Search,
            request.SortBy,
            request.SortOrder
        );

        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var dtos = new List<ReportDto>(items.Count);
        foreach (var r in items)
        {
            dtos.Add(r.ToDto(await _reportRepository.CountAnomaliesForReportAsync(r)));
        }

        var pagination = new PaginationMetaData(page, pageSize, totalCount, totalPages);

        return new PaginatedReportsResponse(dtos, pagination);
    }
}
