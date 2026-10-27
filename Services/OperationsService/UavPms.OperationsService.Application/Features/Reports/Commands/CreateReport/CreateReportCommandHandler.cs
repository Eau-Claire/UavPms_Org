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
    private readonly IRegionRepository _regionRepository;
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices _currentUserServices;

    public CreateReportCommandHandler(
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

    public async Task<ReportDetailDto> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        if (request.RegionId.HasValue)
        {
            var region = await _regionRepository.GetByIdAsync(request.RegionId.Value);
            if (region == null || region.IsDeleted)
            {
                throw new NotFoundException("Region", request.RegionId.Value);
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
            RegionId = request.RegionId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : null
        };

        if (request.MissionIds != null && request.MissionIds.Any())
        {
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

        await _reportRepository.AddAsync(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var detailedReport = await _reportRepository.GetReportByIdWithDetailsAsync(report.Id);
        var target = detailedReport ?? report;

        return target.ToDetailDto(await _reportRepository.CountAnomaliesForReportAsync(target));
    }
}
