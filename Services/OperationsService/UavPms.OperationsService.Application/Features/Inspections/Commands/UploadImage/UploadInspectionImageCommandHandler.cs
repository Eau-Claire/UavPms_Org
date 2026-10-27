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
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Domain.Enums;
using System.Text.Json;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandHandler
    : IRequestHandler<UploadInspectionImageCommand, UploadInspectionImageResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Tower> _towerRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<MissionTarget> _missionTargetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;
    private readonly IGenericRepository<MissionAssignment>? _assignmentRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IMissionRealtimeNotifier? _notifier;
    private readonly ILogger<UploadInspectionImageCommandHandler> _logger;

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Tower> towerRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<MissionTarget> missionTargetRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        IGenericRepository<OutboxMessage> outboxRepository,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger,
        IGenericRepository<MissionAssignment>? assignmentRepository = null,
        IMissionRealtimeNotifier? notifier = null)
    {
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
        _mediaRepository = mediaRepository;
        _missionTargetRepository = missionTargetRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _outboxRepository = outboxRepository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
        _notifier = notifier;
        _logger = logger;
    }

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Tower> towerRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<MissionTarget> missionTargetRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger)
        : this(missionRepository, towerRepository, mediaRepository, missionTargetRepository, unitOfWork,
            fileStorageService, eventPublisher, null!, currentUser, logger, null, null) { }

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

        // 1b. State Machine Guard: Không cho phép nạp dữ liệu kiểm tra khi nhiệm vụ chưa thực thi hoặc bị hủy
        if (mission.Status is MissionStatus.Draft or MissionStatus.PendingAcceptance)
        {
            throw new BusinessRuleException("MISSION_NOT_EXECUTING_OR_COMPLETED", "Cannot upload inspection media before the mission is executing or completed.");
        }

        if (mission.Status == MissionStatus.Cancelled)
        {
            throw new BusinessRuleException("MISSION_CANCELLED", "Cannot upload inspection media for cancelled mission.");
        }

        // 1c. Kiểm tra cột điện (không bắt buộc: video hành lang có thể đi qua nhiều cột)
        if (request.TowerId.HasValue)
        {
            var tower = await _towerRepository.GetByIdAsync(request.TowerId.Value, track: false);
            if (tower == null)
            {
                throw new KeyNotFoundException($"Tower with ID '{request.TowerId}' was not found.");
            }
        }

        // 2. Kiểm tra quyền: chỉ Inspector được giao mới có quyền upload
        var currentUserId = _currentUser.UserId;
        var isAssigned = mission.PrimaryInspectorId() == currentUserId;
        if (!isAssigned && _assignmentRepository != null)
        {
            var activeAssignments = await _assignmentRepository.FindAsync(
                a => a.MissionId == request.MissionId &&
                     a.UserId == currentUserId &&
                     a.Status == MissionAssignmentStatus.Active,
                track: false);
            isAssigned = activeAssignments.Count > 0;
        }

        if (!isAssigned)
        {
            throw new ForbiddenException("You are not assigned to this mission.");
        }

        var targets = request.TowerId.HasValue
            ? await _missionTargetRepository.FindAsync(
                target => target.MissionId == request.MissionId && target.TowerId == request.TowerId.Value,
                track: false)
            : null;
        if (targets != null && targets.Count == 0)
        {
            throw new BusinessRuleException("The tower is not included in the mission inspection scope.");
        }

        // 2b. Auto-extract EXIF GPS & timestamp if missing
        double? effectiveLat = request.Latitude;
        double? effectiveLng = request.Longitude;
        DateTime effectiveCapturedAt = request.CapturedAt;

        if (!effectiveLat.HasValue || !effectiveLng.HasValue || effectiveCapturedAt == default)
        {
            var exif = UavPms.OperationsService.Application.Common.Utilities.ExifMetadataExtractor.Extract(request.FileStream);
            effectiveLat ??= exif.Latitude;
            effectiveLng ??= exif.Longitude;
            if (effectiveCapturedAt == default && exif.CapturedAt.HasValue)
            {
                effectiveCapturedAt = exif.CapturedAt.Value;
            }
        }
        if (effectiveCapturedAt == default) effectiveCapturedAt = DateTime.UtcNow;

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
            TowerId = request.TowerId,
            UploadedBy = currentUserId,
            CaptureLocation = effectiveLat.HasValue && effectiveLng.HasValue
                ? new Point(effectiveLng.Value, effectiveLat.Value) { SRID = 4326 }
                : null,
            MediaType = mediaType,
            FileUrl = fileUrl,
            ValidationStatus = "Pending",
            CapturedAt = effectiveCapturedAt,
            CreatedBy = currentUserId
        };

        var integrationEvent = new InspectionMediaUploadedEvent
        {
            EventId = Guid.NewGuid(),
            MediaId = media.Id,
            MissionId = media.MissionId,
            TowerId = request.TowerId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            UploadedBy = currentUserId,
            CapturedAt = media.CapturedAt,
            Latitude = effectiveLat,
            Longitude = effectiveLng
        };

        await _mediaRepository.AddAsync(media);
        if (_outboxRepository != null)
        {
            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = integrationEvent.EventId,
                MessageType = nameof(InspectionMediaUploadedEvent),
                Payload = JsonSerializer.Serialize(integrationEvent),
                OccurredAt = integrationEvent.OccurredAt
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

        if (_notifier != null)
        {
            try
            {
                await _notifier.NotifyAsync(new MissionLifecycleEventDto
                {
                    MissionId = request.MissionId.ToString(),
                    Type = "MEDIA_UPLOADED",
                    Status = media.MediaType,
                    Reason = media.FileUrl,
                    Timestamp = DateTime.UtcNow
                }, cancellationToken);
            }
            catch
            {
                // Non-blocking realtime event
            }
        }

        _logger.LogInformation(
            "Inspection image uploaded: MediaId={MediaId}, MissionId={MissionId}, Url={FileUrl}",
            media.Id, media.MissionId, media.FileUrl);

        return new UploadInspectionImageResult
        {
            MediaId = media.Id,
            MissionId = media.MissionId,
            TowerId = media.TowerId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            CapturedAt = media.CapturedAt
        };
    }
}
