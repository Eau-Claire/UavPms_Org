using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;

public class GetReportByIdQueryHandler : IRequestHandler<GetReportByIdQuery, ReportDetailDto>
{
    private readonly IReportRepository _reportRepository;

    public GetReportByIdQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<ReportDetailDto> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        return new ReportDetailDto(
            report.Id,
            report.Code,
            report.Title,
            report.Type.ToString().ToLowerInvariant(),
            report.Status.ToString().ToLowerInvariant(),
            report.Description,
            report.CreatedAt,
            report.UpdatedAt,
            report.TransmissionLine != null ? new ReportNestedEntityDto(report.TransmissionLine.Id, report.TransmissionLine.LineName) : null,
            report.Substation != null ? new ReportNestedEntityDto(report.Substation.Id, report.Substation.SubstationName) : null,
            report.CreatedByUser != null ? new ReportCreatorDto(report.CreatedByUser.Id, report.CreatedByUser.FullName) : null,
            report.DefectCount,
            report.PdfFileSize,
            report.ExcelFileSize,
            report.PdfFileUrl,
            report.ExcelFileUrl,
            report.DateFrom,
            report.DateTo,
            report.RejectionReason,
            report.ApprovedBy != null ? new ReportCreatorDto(report.ApprovedBy.Id, report.ApprovedBy.FullName) : null,
            report.ApprovedAt,
            report.ReportMissions.Select(rm => new ReportMissionSummaryDto(
                rm.MissionId,
                rm.Mission?.MissionCode ?? string.Empty,
                rm.Mission?.Title ?? string.Empty,
                rm.Mission?.Status.ToString().ToLowerInvariant() ?? string.Empty,
                rm.Mission?.ScheduledStartAt
            )).ToList()
        );
    }
}
