using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;

public class GetInspectionReportByIdQueryHandler
    : IRequestHandler<GetInspectionReportByIdQuery, InspectionReportDto>
{
    private readonly IInspectionMediaRepository _inspectionMediaRepository;
    private readonly IMissionRepository? _missionRepository;
    private readonly ICurrentUserServices? _currentUser;

    public GetInspectionReportByIdQueryHandler(
        IInspectionMediaRepository inspectionMediaRepository,
        IMissionRepository? missionRepository = null,
        ICurrentUserServices? currentUser = null)
    {
        _inspectionMediaRepository = inspectionMediaRepository;
        _missionRepository = missionRepository;
        _currentUser = currentUser;
    }

    public async Task<InspectionReportDto> Handle(
        GetInspectionReportByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _inspectionMediaRepository.ExistsAsync(request.Id))
        {
            throw new NotFoundException("InspectionMedia", request.Id);
        }

        var media = await _inspectionMediaRepository.GetByIdWithDetailsAsync(request.Id);
        if (media == null)
        {
            throw new NotFoundException("InspectionMedia", request.Id);
        }

        if (_currentUser is { IsAuthenticated: true } && _missionRepository != null)
        {
            var isGlobal = _currentUser.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
            var canAccess = await _missionRepository.UserCanAccessAsync(
                media.MissionId,
                _currentUser.UserId,
                isGlobal,
                cancellationToken);

            if (!canAccess)
            {
                throw new ForbiddenException("MISSION_ACCESS_DENIED");
            }
        }

        return new InspectionReportDto
        {
            Id = media.Id,
            MissionId = media.MissionId,
            AssetId = media.AssetId,
            MediaType = media.MediaType,
            FileUrl = media.FileUrl,
            AiSource = media.AiSource,
            ValidationStatus = media.ValidationStatus,
            CapturedAt = media.CapturedAt,
            CreatedAt = media.CreatedAt,
            DetectedAnomalies = media.DetectedAnomalies.Select(a => new DetectedAnomalyDto
            {
                Id = a.Id,
                MediaId = a.MediaId,
                AssetId = a.AssetId,
                CategoryName = a.Category?.CategoryName ?? string.Empty,
                DefectType = a.Category?.CategoryCode ?? string.Empty,
                ConfidenceScore = a.ConfidenceScore,
                ValidationStatus = a.ValidationStatus,
                AiSource = a.AiSource,
                BoundingBox = a.BoundingBox,
                ValidatedAt = a.ValidatedAt,
                CreatedAt = a.CreatedAt
            }).ToList()
        };
    }
}
