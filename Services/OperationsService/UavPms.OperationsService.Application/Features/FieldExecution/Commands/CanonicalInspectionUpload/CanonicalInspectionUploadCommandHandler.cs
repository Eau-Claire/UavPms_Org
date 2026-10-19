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
    private readonly IAssetRepository _assetRepository;

    public CanonicalInspectionUploadCommandHandler(
        ISender mediator,
        IMissionRepository missionRepository,
        ITowerRepository towerRepository,
        IAssetRepository assetRepository)
    {
        _mediator = mediator;
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
        _assetRepository = assetRepository;
    }

    public async Task<UploadInspectionImageResult> Handle(CanonicalInspectionUploadCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
            throw new NotFoundException("Mission", request.MissionId);

        Guid resolvedAssetId = request.AssetId ?? Guid.Empty;
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

        // If AssetId is not provided but TowerId is
        if (resolvedAssetId == Guid.Empty && resolvedTowerId.HasValue && resolvedTowerId.Value != Guid.Empty)
        {
            var assets = await _assetRepository.FindAsync(a => a.TowerId == resolvedTowerId.Value, track: false);
            if (assets.Count > 0)
            {
                resolvedAssetId = assets[0].Id;
            }
            else
            {
                resolvedAssetId = resolvedTowerId.Value;
            }
        }
        else if (resolvedAssetId == Guid.Empty)
        {
            // Pick first target from mission
            var firstTarget = mission.MissionTargets.FirstOrDefault();
            if (firstTarget != null && firstTarget.AssetId != Guid.Empty)
            {
                resolvedAssetId = firstTarget.AssetId;
            }
        }

        if (resolvedAssetId == Guid.Empty)
        {
            throw new BusinessRuleException("TARGET_REQUIRED", "No target tower or asset found for media upload.");
        }

        var uploadCommand = new UploadInspectionImageCommand
        {
            MissionId = request.MissionId,
            AssetId = resolvedAssetId,
            TowerId = resolvedTowerId,
            CapturedAt = capturedAt,
            Latitude = lat,
            Longitude = lon,
            FileStream = request.FileStream,
            FileName = request.FileName,
            ContentType = request.ContentType
        };

        return await _mediator.Send(uploadCommand, cancellationToken);
    }
}
