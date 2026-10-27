using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;

public class UpdateReportCommandHandler : IRequestHandler<UpdateReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly IRegionRepository _regionRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public UpdateReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        IRegionRepository regionRepository,
        IMissionRepository missionRepository,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _regionRepository = regionRepository;
        _missionRepository = missionRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<ReportDetailDto> Handle(UpdateReportCommand request, CancellationToken cancellationToken)
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
                    throw new ForbiddenException("Quản lý không có quyền chỉnh sửa báo cáo ngoài khu vực quản lý.");
                }
            }
            else if (report.CreatedBy.HasValue && report.CreatedBy.Value != _currentUserServices.UserId)
            {
                throw new ForbiddenException("Bạn chỉ có thể chỉnh sửa báo cáo do chính mình tạo ra.");
            }
        }

        if (report.Status != ReportStatus.Draft)
        {
            throw new InvalidOperationException($"Chỉ có thể cập nhật báo cáo ở trạng thái Bản nháp (Draft). Trạng thái hiện tại: {report.Status}.");
        }

        if (request.RegionId.HasValue)
        {
            var region = await _regionRepository.GetByIdAsync(request.RegionId.Value);
            if (region == null || region.IsDeleted)
            {
                throw new NotFoundException("Region", request.RegionId.Value);
            }
        }

        report.Title = request.Title.Trim();
        report.Description = request.Description;
        report.RegionId = request.RegionId;
        report.DateFrom = request.DateFrom;
        report.DateTo = request.DateTo;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : null;

        if (request.MissionIds != null)
        {
            report.ReportMissions.Clear();
            var distinctMissionIds = request.MissionIds.Distinct().ToList();
            foreach (var missionId in distinctMissionIds)
            {
                var mission = await _missionRepository.GetByIdAsync(missionId);
                if (mission != null && !mission.IsDeleted)
                {
                    report.ReportMissions.Add(new ReportMission
                    {
                        ReportId = report.Id,
                        MissionId = mission.Id,
                        AddedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

        await _reportRepository.UpdateAsync(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detailedReport = await _reportRepository.GetReportByIdWithDetailsAsync(report.Id);
        var target = detailedReport ?? report;

        return target.ToDetailDto(await _reportRepository.CountAnomaliesForReportAsync(target));
    }
}
