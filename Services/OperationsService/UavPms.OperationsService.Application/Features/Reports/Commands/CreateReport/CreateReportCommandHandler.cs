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

namespace UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;

public class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, ReportDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReportRepository _reportRepository;
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private readonly ISubstationRepository _substationRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public CreateReportCommandHandler(
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

    public async Task<ReportDetailDto> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
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

        if (!Enum.TryParse<ReportType>(request.Type, true, out var reportType))
        {
            throw new BusinessRuleException($"Loại báo cáo '{request.Type}' không hợp lệ.");
        }

        var nextCode = await _reportRepository.GetNextReportCodeAsync(DateTime.UtcNow.Year);

        var report = new Report
        {
            Id = Guid.NewGuid(),
            Code = nextCode,
            Title = request.Title.Trim(),
            Type = reportType,
            Status = ReportStatus.Draft,
            Description = request.Description,
            TransmissionLineId = request.TransmissionLineId,
            SubstationId = request.SubstationId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            DefectCount = 0,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : null
        };

        int defectCount = 0;
        if (request.MissionIds != null && request.MissionIds.Any())
        {
            var distinctMissionIds = request.MissionIds.Distinct().ToList();
            defectCount = await _reportRepository.CountAnomaliesByMissionIdsAsync(distinctMissionIds);
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

        report.DefectCount = defectCount;

        await _reportRepository.AddAsync(report);
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
