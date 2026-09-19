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
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private readonly ISubstationRepository _substationRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public UpdateReportCommandHandler(
        IUnitOfWork unitOfWork,
        IReportRepository reportRepository,
        ITransmissionLineRepository transmissionLineRepository,
        ISubstationRepository substationRepository,
        IMissionRepository missionRepository,
        ICurrentUserServices currentUserServices)
    {
        _unitOfWork = unitOfWork;
        _reportRepository = reportRepository;
        _transmissionLineRepository = transmissionLineRepository;
        _substationRepository = substationRepository;
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

        if (request.TransmissionLineId.HasValue)
        {
            var line = await _transmissionLineRepository.GetByIdAsync(request.TransmissionLineId.Value);
            if (line == null || line.IsDeleted)
            {
                throw new NotFoundException("TransmissionLine", request.TransmissionLineId.Value);
            }
        }

        if (request.SubstationId.HasValue)
        {
            var substation = await _substationRepository.GetByIdAsync(request.SubstationId.Value);
            if (substation == null || substation.IsDeleted)
            {
                throw new NotFoundException("Substation", request.SubstationId.Value);
            }
        }

        report.Title = request.Title.Trim();
        report.Description = request.Description;
        report.TransmissionLineId = request.TransmissionLineId;
        report.SubstationId = request.SubstationId;
        report.DateFrom = request.DateFrom;
        report.DateTo = request.DateTo;
        report.UpdatedAt = DateTime.UtcNow;
        report.UpdatedBy = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : null;

        if (request.MissionIds != null)
        {
            report.ReportMissions.Clear();
            var distinctMissionIds = request.MissionIds.Distinct().ToList();
            report.DefectCount = await _reportRepository.CountAnomaliesByMissionIdsAsync(distinctMissionIds);
            foreach (var missionId in distinctMissionIds)
            {
                var mission = await _missionRepository.GetByIdAsync(missionId);
                if (mission != null && !mission.IsDeleted)
                {
                    report.ReportMissions.Add(new ReportMission
                    {
                        ReportId = report.Id,
                        MissionId = mission.Id,
                        AssignedAt = DateTimeOffset.UtcNow
                    });
                }
            }
        }

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
