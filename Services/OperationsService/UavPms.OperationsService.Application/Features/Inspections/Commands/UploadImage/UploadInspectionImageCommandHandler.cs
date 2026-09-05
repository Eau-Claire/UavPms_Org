using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Domain.Contracts;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using System.Text.Json;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandHandler
    : IRequestHandler<UploadInspectionImageCommand, UploadInspectionImageResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Asset> _assetRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<MissionTarget> _missionTargetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<UploadInspectionImageCommandHandler> _logger;

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Asset> assetRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<MissionTarget> missionTargetRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        IGenericRepository<OutboxMessage> outboxRepository,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _assetRepository = assetRepository;
        _mediaRepository = mediaRepository;
        _missionTargetRepository = missionTargetRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _outboxRepository = outboxRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Asset> assetRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<MissionTarget> missionTargetRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger)
        : this(missionRepository, assetRepository, mediaRepository, missionTargetRepository, unitOfWork,
            fileStorageService, eventPublisher, null!, currentUser, logger) { }

    public async Task<UploadInspectionImageResult> Handle(
        UploadInspectionImageCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra Mission tồn tại
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, track: false);
        if (mission == null)
        {
            throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        }

        // 1b. Kiểm tra Asset tồn tại
        var asset = await _assetRepository.GetByIdAsync(request.AssetId, track: false);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset with ID '{request.AssetId}' was not found.");
        }

        // 2. Kiểm tra quyền: chỉ Inspector được giao mới có quyền upload
        var currentUserId = _currentUser.UserId;
        if (mission.InspectorId != currentUserId)
        {
            throw new ForbiddenException("You are not assigned to this mission.");
        }

        var targets = await _missionTargetRepository.FindAsync(
            target => target.MissionId == request.MissionId && target.AssetId == request.AssetId,
            track: false);
        if (targets.Count == 0)
        {
            throw new BusinessRuleException("The asset is not included in the mission inspection scope.");
        }

        // 3. Lưu file ảnh vào hệ thống
        string fileUrl;
        try
        {
            fileUrl = await _fileStorageService.SaveImageAsync(request.FileStream, request.FileName, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InfrastructureOperationException("STORAGE_FAILURE", "Failed to store inspection media.", ex);
        }

        // 4. Xác định loại media từ content type
        var mediaType = request.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? "Video"
            : "Image";

        // 5. Tạo bản ghi InspectionMedia
        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            AssetId = request.AssetId,
            UploadedBy = currentUserId,
            CaptureLocation = request.Latitude.HasValue
                ? new Point(request.Longitude!.Value, request.Latitude.Value) { SRID = 4326 }
                : null,
            MediaType = mediaType,
            FileUrl = fileUrl,
            AiSource = string.Empty,
            ValidationStatus = "Pending",
            CapturedAt = request.CapturedAt,
            CreatedBy = currentUserId
        };

        var integrationEvent = new InspectionMediaUploadedEvent
        {
            EventId = Guid.NewGuid(),
            MediaId = media.Id,
            MissionId = media.MissionId,
            AssetId = request.AssetId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            UploadedBy = currentUserId,
            CapturedAt = media.CapturedAt,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        await _mediaRepository.AddAsync(media);
        if (_outboxRepository != null)
        {
            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = integrationEvent.EventId,
                MessageType = nameof(InspectionMediaUploadedEvent),
                Payload = JsonSerializer.Serialize(integrationEvent),
                OccurredAt = integrationEvent.OccurredAt,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            });
        }
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InfrastructureOperationException("DATABASE_FAILURE", "Failed to persist inspection media.", ex);
        }

        if (_outboxRepository == null)
            await _eventPublisher.PublishAsync(integrationEvent);

        _logger.LogInformation(
            "Inspection image uploaded: MediaId={MediaId}, MissionId={MissionId}, Url={FileUrl}",
            media.Id, media.MissionId, media.FileUrl);

        return new UploadInspectionImageResult
        {
            MediaId = media.Id,
            MissionId = media.MissionId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            CapturedAt = media.CapturedAt
        };
    }
}
