using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Inspections.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Inspections.Queries.GetReportById;

public class GetInspectionReportByIdQueryHandler
    : IRequestHandler<GetInspectionReportByIdQuery, InspectionReportDto>
{
    private readonly IInspectionMediaRepository _inspectionMediaRepository;

    public GetInspectionReportByIdQueryHandler(IInspectionMediaRepository inspectionMediaRepository)
    {
        _inspectionMediaRepository = inspectionMediaRepository;
    }

    public async Task<InspectionReportDto> Handle(
        GetInspectionReportByIdQuery request,
        CancellationToken cancellationToken)
    {
        var media = await _inspectionMediaRepository.GetByIdWithDetailsAsync(request.Id);
        if (media == null)
        {
            throw new NotFoundException("InspectionMedia", request.Id);
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
