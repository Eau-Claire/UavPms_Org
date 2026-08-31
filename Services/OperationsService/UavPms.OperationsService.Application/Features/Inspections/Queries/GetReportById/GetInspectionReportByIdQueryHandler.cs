using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;

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
            TowerId = media.TowerId,
            Latitude = media.CaptureLocation?.Y,
            Longitude = media.CaptureLocation?.X,
            MediaType = media.MediaType,
            FileUrl = media.FileUrl,
            AiSource = media.AiSource,
            ValidationStatus = media.ValidationStatus,
            CapturedAt = media.CapturedAt,
            CreatedAt = media.CreatedAt,
            DetectedAnomalies = (media.DetectedAnomalies ?? new List<DetectedAnomaly>()).Select(a => new DetectedAnomalyDto
            {
                Id = a.Id,
                MediaId = a.MediaId,
                TowerId = a.TowerId,
                ComponentId = a.ComponentId,
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
