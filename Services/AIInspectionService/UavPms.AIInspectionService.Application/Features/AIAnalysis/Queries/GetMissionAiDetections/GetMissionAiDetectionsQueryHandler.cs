using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.AIInspectionService.Application.Common.Exceptions;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public class GetMissionAiDetectionsQueryHandler
    : IRequestHandler<GetMissionAiDetectionsQuery, IReadOnlyList<MissionAiDetectionMediaDto>>
{
    private readonly IInspectionMediaRepository _inspectionMediaRepository;
    private readonly IGenericRepository<UavPms.AIInspectionService.Domain.Entities.Mission> _missionRepository;
    private readonly ICurrentUserServices _currentUser;

    public GetMissionAiDetectionsQueryHandler(
        IInspectionMediaRepository inspectionMediaRepository,
        IGenericRepository<UavPms.AIInspectionService.Domain.Entities.Mission> missionRepository,
        ICurrentUserServices currentUser)
    {
        _inspectionMediaRepository = inspectionMediaRepository;
        _missionRepository = missionRepository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MissionAiDetectionMediaDto>> Handle(
        GetMissionAiDetectionsQuery request,
        CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, false, cancellationToken)
            ?? throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        if (roles.Contains(UserRoles.Inspector) && mission.InspectorId != _currentUser.UserId)
            throw new ForbiddenException("You may only view detections for missions assigned to you.");
        if (roles.Contains(UserRoles.Manager) && mission.ManagerId != _currentUser.UserId)
            throw new ForbiddenException("You may only view detections for missions you manage.");

        var mediaList = await _inspectionMediaRepository.GetByMissionIdWithDetailsAsync(request.MissionId);

        return mediaList
            .Where(media => media.DetectedAnomalies.Count > 0)
            .Select(media =>
            {
                var orderedDetections = media.DetectedAnomalies
                    .OrderBy(anomaly => anomaly.Timestamp ?? double.MaxValue)
                    .ThenBy(anomaly => anomaly.FrameIndex ?? int.MaxValue)
                    .ThenByDescending(anomaly => anomaly.ConfidenceScore)
                    .ToList();

                return new MissionAiDetectionMediaDto
                {
                    MediaId = media.Id,
                    MissionId = media.MissionId,
                    AssetId = media.AssetId,
                    MediaType = media.MediaType,
                    FileUrl = media.FileUrl,
                    AiSource = media.AiSource,
                    ValidationStatus = media.ValidationStatus,
                    CapturedAt = media.CapturedAt,
                    CreatedAt = media.CreatedAt,
                    DetectionCount = media.DetectedAnomalies.Count,
                    VideoMetadata = orderedDetections
                        .Select(MissionAiDetectionMapper.MapVideoMetadata)
                        .FirstOrDefault(metadata => metadata != null),
                    Detections = orderedDetections
                        .Select(MissionAiDetectionMapper.MapDetection)
                        .ToList()
                };
            })
            .ToList();
    }

}
