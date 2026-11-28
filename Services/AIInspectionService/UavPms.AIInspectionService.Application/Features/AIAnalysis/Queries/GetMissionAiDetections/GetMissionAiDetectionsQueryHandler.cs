using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Common.Security;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public class GetMissionAiDetectionsQueryHandler
    : IRequestHandler<GetMissionAiDetectionsQuery, IReadOnlyList<MissionAiDetectionMediaDto>>
{
    private readonly IInspectionMediaRepository _inspectionMediaRepository;
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IMissionAccessPolicy _accessPolicy;

    public GetMissionAiDetectionsQueryHandler(
        IInspectionMediaRepository inspectionMediaRepository,
        IGenericRepository<Mission> missionRepository,
        IMissionAccessPolicy accessPolicy)
    {
        _inspectionMediaRepository = inspectionMediaRepository;
        _missionRepository = missionRepository;
        _accessPolicy = accessPolicy;
    }

    public async Task<IReadOnlyList<MissionAiDetectionMediaDto>> Handle(
        GetMissionAiDetectionsQuery request,
        CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, false, cancellationToken)
            ?? throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        // Defect của nhiệm vụ chỉ người tham gia (và Manager/Analyst của vùng) được xem (spec §4).
        if (!await _accessPolicy.CanViewAsync(mission))
            throw new ForbiddenException("MISSION_DEFECT_ACCESS_DENIED");

        var mediaList = await _inspectionMediaRepository.GetByMissionIdWithDetailsAsync(request.MissionId);

        return mediaList
            .Select(media =>
            {
                var activeAnomalies = media.DetectedAnomalies
                    .Where(anomaly => anomaly.SupersededById == null)
                    .OrderBy(anomaly => anomaly.FrameTimeSeconds ?? double.MaxValue)
                    .ThenBy(anomaly => anomaly.FrameIndex ?? int.MaxValue)
                    .ThenByDescending(anomaly => anomaly.ConfidenceScore)
                    .ToList();

                return (media, activeAnomalies);
            })
            .Where(item => item.activeAnomalies.Count > 0)
            .Select(item =>
            {
                var (media, orderedDetections) = item;
                return new MissionAiDetectionMediaDto
                {
                    MediaId = media.Id,
                    MissionId = media.MissionId,
                    TowerId = media.TowerId,
                    MediaType = media.MediaType,
                    FileUrl = media.FileUrl,
                    AiSource = MissionAiDetectionMapper.ResolveMediaAiSource(media),
                    ValidationStatus = media.ValidationStatus,
                    CapturedAt = media.CapturedAt,
                    CreatedAt = media.CreatedAt,
                    DetectionCount = orderedDetections.Count,
                    VideoMetadata = MissionAiDetectionMapper.MapVideoMetadata(media),
                    Detections = orderedDetections
                        .Select(MissionAiDetectionMapper.MapDetection)
                        .ToList()
                };
            })
            .ToList();
    }

}
