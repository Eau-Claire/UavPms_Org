using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

public class GetMissionAiDetectionsQueryHandler
    : IRequestHandler<GetMissionAiDetectionsQuery, IReadOnlyList<MissionAiDetectionMediaDto>>
{
    private readonly IInspectionMediaRepository _inspectionMediaRepository;

    public GetMissionAiDetectionsQueryHandler(IInspectionMediaRepository inspectionMediaRepository)
    {
        _inspectionMediaRepository = inspectionMediaRepository;
    }

    public async Task<IReadOnlyList<MissionAiDetectionMediaDto>> Handle(
        GetMissionAiDetectionsQuery request,
        CancellationToken cancellationToken)
    {
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
