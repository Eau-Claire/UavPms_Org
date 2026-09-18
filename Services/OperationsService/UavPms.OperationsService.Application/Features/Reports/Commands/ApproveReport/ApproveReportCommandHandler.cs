using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.ApproveReport;

public class ApproveReportCommandHandler : IRequestHandler<ApproveReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public ApproveReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportDetailDto> Handle(ApproveReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        if (report.Status != ReportStatus.Pending)
        {
            throw new InvalidOperationException($"Chỉ có thể phê duyệt báo cáo ở trạng thái Chờ duyệt (Pending). Trạng thái hiện tại: {report.Status}.");
        }

        report.Status = ReportStatus.Approved;
        report.ApprovedById = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : null;
        report.ApprovedAt = DateTimeOffset.UtcNow;
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
