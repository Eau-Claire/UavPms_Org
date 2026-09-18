using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;

public class RejectReportCommandHandler : IRequestHandler<RejectReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;

    public RejectReportCommandHandler(IUnitOfWork unitOfWork, IReportRepository reportRepository)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
    }

    public async Task<ReportDetailDto> Handle(RejectReportCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Lý do từ chối không được để trống.");
        }

        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        if (report.Status != ReportStatus.Pending)
        {
            throw new InvalidOperationException($"Chỉ có thể từ chối báo cáo ở trạng thái Chờ duyệt (Pending). Trạng thái hiện tại: {report.Status}.");
        }

        report.Status = ReportStatus.Draft;
        report.RejectionReason = request.Reason.Trim();
        report.UpdatedAt = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detailedReport = await _reportRepository.GetReportByIdWithDetailsAsync(report.Id);
        var target = detailedReport ?? report;

        return new ReportDetailDto(
            target.Id,
            target.Code,
            target.Title,
            target.Type.ToString().ToLowerInvariant(),
            target.Status.ToString().ToLowerInvariant(),
            target.Description,
            target.CreatedAt,
            target.UpdatedAt,
            target.TransmissionLine != null ? new ReportNestedEntityDto(target.TransmissionLine.Id, target.TransmissionLine.LineName) : null,
            target.Substation != null ? new ReportNestedEntityDto(target.Substation.Id, target.Substation.SubstationName) : null,
            target.CreatedByUser != null ? new ReportCreatorDto(target.CreatedByUser.Id, target.CreatedByUser.FullName) : null,
            target.DefectCount,
            target.PdfFileSize,
            target.ExcelFileSize,
            target.PdfFileUrl,
            target.ExcelFileUrl,
            target.DateFrom,
            target.DateTo,
            target.RejectionReason,
            target.ApprovedBy != null ? new ReportCreatorDto(target.ApprovedBy.Id, target.ApprovedBy.FullName) : null,
            target.ApprovedAt,
            target.ReportMissions.Select(rm => new ReportMissionSummaryDto(
                rm.MissionId,
                rm.Mission?.MissionCode ?? string.Empty,
                rm.Mission?.Title ?? string.Empty,
                rm.Mission?.Status.ToString().ToLowerInvariant() ?? string.Empty,
                rm.Mission?.ScheduledStartAt
            )).ToList()
        );
    }
}
