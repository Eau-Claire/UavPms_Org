using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportStatistics;

public class GetReportStatisticsQueryHandler : IRequestHandler<GetReportStatisticsQuery, ReportStatisticsDto>
{
    private readonly IReportRepository _reportRepository;

    public GetReportStatisticsQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<ReportStatisticsDto> Handle(GetReportStatisticsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From;
        var to = request.To;

        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Period))
        {
            switch (request.Period.Trim().ToLowerInvariant())
            {
                case "month":
                    from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                    to = now;
                    break;
                case "quarter":
                    int currentQuarter = (now.Month - 1) / 3 + 1;
                    int startMonth = (currentQuarter - 1) * 3 + 1;
                    from = new DateTimeOffset(now.Year, startMonth, 1, 0, 0, 0, TimeSpan.Zero);
                    to = now;
                    break;
                case "year":
                    from = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    to = now;
                    break;
            }
        }

        var (total, byType, byStatus) = await _reportRepository.GetReportStatisticsAsync(from, to);

        return new ReportStatisticsDto(total, byType, byStatus);
    }
}
