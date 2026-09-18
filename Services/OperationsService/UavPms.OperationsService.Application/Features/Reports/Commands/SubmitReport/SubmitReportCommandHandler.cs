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
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.SubmitReport;

public class SubmitReportCommandHandler : IRequestHandler<SubmitReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public SubmitReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportDetailDto> Handle(SubmitReportCommand request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        bool isAdmin = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        bool isManager = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            if (isManager)
            {
                bool canManage = await _reportRepository.CanUserManageReportAsync(_currentUserServices.UserId, report);
                if (!canManage)
                {
                    throw new ForbiddenException("Quản lý không có quyền gửi duyệt báo cáo ngoài khu vực quản lý.");
                }
            }
            else if (report.CreatedBy.HasValue && report.CreatedBy.Value != _currentUserServices.UserId)
            {
                throw new ForbiddenException("Bạn không có quyền gửi duyệt báo cáo do người khác tạo.");
            }
        }

        if (report.Status != ReportStatus.Draft)
        {
            throw new InvalidOperationException($"Chỉ có thể gửi duyệt báo cáo ở trạng thái Bản nháp (Draft). Trạng thái hiện tại: {report.Status}.");
        }

        report.Status = ReportStatus.Pending;
        report.UpdatedAt = DateTime.UtcNow;

        await _reportRepository.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
