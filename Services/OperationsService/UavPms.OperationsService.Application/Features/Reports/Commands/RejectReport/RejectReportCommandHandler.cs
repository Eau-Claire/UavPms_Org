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
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;

public class RejectReportCommandHandler : IRequestHandler<RejectReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public RejectReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _currentUserServices = currentUserServices;
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

        bool isAdmin = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        bool isManager = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin && !isManager)
        {
            throw new ForbiddenException("Chỉ cấp quản lý hoặc quản trị viên mới có quyền từ chối báo cáo.");
        }

        if (isManager && !isAdmin)
        {
            var canManage = await _reportRepository.CanUserManageReportAsync(_currentUserServices.UserId, report);
            if (!canManage)
            {
                throw new ForbiddenException("Bạn không có quyền từ chối báo cáo ngoài khu vực quản lý.");
            }
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

        return target.ToDetailDto(await _reportRepository.CountAnomaliesForReportAsync(target));
    }
}
