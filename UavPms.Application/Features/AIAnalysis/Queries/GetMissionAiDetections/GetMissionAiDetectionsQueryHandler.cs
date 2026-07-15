using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;

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
            .Select(media => new MissionAiDetectionMediaDto
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
                Detections = media.DetectedAnomalies
                    .OrderByDescending(anomaly => anomaly.ConfidenceScore)
                    .Select(MissionAiDetectionMapper.MapDetection)
                    .ToList()
            })
            .ToList();
    }

}
