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

namespace UavPms.OperationsService.Application.Features.Reports.Queries.GetReportById;

public class GetReportByIdQueryHandler : IRequestHandler<GetReportByIdQuery, ReportDetailDto>
{
    private readonly IReportRepository _reportRepository;
    private readonly ICurrentUserServices? _currentUserServices;

    public GetReportByIdQueryHandler(
        IReportRepository reportRepository,
        ICurrentUserServices? currentUserServices = null)
    {
        _reportRepository = reportRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportDetailDto> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
    {
        var report = await _reportRepository.GetReportByIdWithDetailsAsync(request.Id);
        if (report == null || report.IsDeleted)
        {
            throw new NotFoundException("Report", request.Id);
        }

        if (_currentUserServices != null && _currentUserServices.IsAuthenticated)
        {
            bool isAdmin = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
            bool isManager = _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));

            if (!isAdmin)
            {
                if (isManager)
                {
                    bool canManage = await _reportRepository.CanUserManageReportAsync(_currentUserServices.UserId, report);
                    if (!canManage)
                    {
                        throw new ForbiddenException("Quản lý không có quyền xem báo cáo ngoài khu vực quản lý.");
                    }
                }
                else if (report.Status == ReportStatus.Draft && report.CreatedBy.HasValue && report.CreatedBy.Value != _currentUserServices.UserId)
                {
                    throw new ForbiddenException("Bạn chỉ có thể xem báo cáo nháp do chính mình tạo ra.");
                }
            }
        }

        return report.ToDetailDto(await _reportRepository.CountAnomaliesForReportAsync(report));
    }
}
