using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.CanonicalInspectionUpload;

public class CanonicalInspectionUploadCommandHandler : IRequestHandler<CanonicalInspectionUploadCommand, UploadInspectionImageResult>
{
    private readonly ISender _mediator;
    private readonly IMissionRepository _missionRepository;
    private readonly ITowerRepository _towerRepository;

    public CanonicalInspectionUploadCommandHandler(
        ISender mediator,
        IMissionRepository missionRepository,
        ITowerRepository towerRepository)
    {
        _mediator = mediator;
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
    }

    public async Task<UploadInspectionImageResult> Handle(CanonicalInspectionUploadCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
            throw new NotFoundException("Mission", request.MissionId);

        Guid? resolvedTowerId = request.TowerId;

        // Auto-extract EXIF if GPS missing
        double? lat = request.Latitude;
        double? lon = request.Longitude;
        DateTime capturedAt = request.CapturedAt;

        if ((!lat.HasValue || !lon.HasValue || capturedAt == default) && request.FileStream != null && request.FileStream.CanSeek)
        {
            var initialPosition = request.FileStream.Position;
            var exif = ExifMetadataExtractor.Extract(request.FileStream);
            request.FileStream.Position = initialPosition;

            lat ??= exif.Latitude;
            lon ??= exif.Longitude;
            if (capturedAt == default && exif.CapturedAt.HasValue)
            {
                capturedAt = exif.CapturedAt.Value;
            }
        }

        if (capturedAt == default)
        {
            capturedAt = DateTime.UtcNow;
        }

        // Ảnh không chỉ định cột: dùng cột đầu tiên của nhiệm vụ nếu có; video hành lang có thể không gắn cột.
        if (!resolvedTowerId.HasValue || resolvedTowerId.Value == Guid.Empty)
        {
            resolvedTowerId = mission.MissionTargets.OrderBy(t => t.Sequence).FirstOrDefault()?.TowerId;
        }

        var uploadCommand = new UploadInspectionImageCommand
        {
            MissionId = request.MissionId,
            TowerId = resolvedTowerId,
            CapturedAt = capturedAt,
            Latitude = lat,
            Longitude = lon,
            FileStream = request.FileStream ?? Stream.Null,
            FileName = request.FileName,
            ContentType = request.ContentType
        };

        return await _mediator.Send(uploadCommand, cancellationToken);
    }
}
