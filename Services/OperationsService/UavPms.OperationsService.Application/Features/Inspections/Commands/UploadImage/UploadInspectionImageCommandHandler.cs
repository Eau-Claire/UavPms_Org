using System;
using System.Linq;
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
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Domain.Enums;
using System.Text.Json;
using UavPms.Shared.Contracts.Constants;

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
    private readonly IGenericRepository<UserGeographicScope>? _scopeRepository;
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
        IMissionRealtimeNotifier? notifier = null,
        IGenericRepository<UserGeographicScope>? scopeRepository = null)
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
        _scopeRepository = scopeRepository;
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

    private async Task<bool> CanUploadAsync(Mission mission, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        bool HasRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);

        if (HasRole(UserRoles.SystemAdmin))
        {
            return true;
        }

        if (HasRole(UserRoles.Manager))
        {
            if (mission.ManagerId == userId)
            {
                return true;
            }

            if (mission.RegionId.HasValue && _scopeRepository != null)
            {
                var scopes = await _scopeRepository.FindAsync(
                    s => s.UserId == userId && s.RegionId == mission.RegionId.Value,
                    track: false);
                if (scopes.Count > 0)
                {
                    return true;
                }
            }
        }

        if (!HasRole(UserRoles.Inspector))
        {
            return false;
        }

        if (mission.PrimaryInspectorId() == userId)
        {
            return true;
        }

        if (_assignmentRepository == null)
        {
            return false;
        }

        var activeAssignments = await _assignmentRepository.FindAsync(
            a => a.MissionId == mission.Id &&
                 a.UserId == userId &&
                 a.Status == MissionAssignmentStatus.Active,
            track: false);
        return activeAssignments.Count > 0;
    }

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

        // 2. Quyền upload (SRS bảng phân quyền SCR-13, BR-02, BR-40):
        //    - SystemAdmin: mọi nhiệm vụ;
        //    - Manager: nhiệm vụ mình phụ trách hoặc thuộc khu vực trong phạm vi quản lý;
        //    - Inspector: thành viên đang được giao của nhiệm vụ.
        //    Analyst và Technician chỉ xem nhiệm vụ, kể cả khi là thành viên.
        if (!await CanUploadAsync(mission, cancellationToken))
        {
            throw new ForbiddenException("You are not assigned to this mission.");
        }
        var currentUserId = _currentUser.UserId;

        var targets = request.TowerId.HasValue
            ? await _missionTargetRepository.FindAsync(
                target => target.MissionId == request.MissionId && target.TowerId == request.TowerId.Value,
                track: false)
            : null;
        if (targets != null && targets.Count == 0)
        {
            throw new BusinessRuleException("The tower is not included in the mission inspection scope.");
        }

        // 2a. Checksum: the same file uploaded twice (e.g. an edge sync retry) returns the existing media.
        var hasContent = request.FileStream is { CanSeek: true } && request.FileStream.Length > 0;
        var checksum = hasContent ? MediaMetadataExtractor.ComputeSha256(request.FileStream) : null;
        if (checksum != null)
        {
            var duplicate = await FindByChecksumAsync(checksum, request.MissionId);
            if (duplicate != null)
            {
                _logger.LogInformation("Duplicate inspection media upload: Checksum={Checksum}, MediaId={MediaId}", checksum, duplicate.Id);
                return UploadInspectionImageResult.From(duplicate, isDuplicate: true);
            }
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
        var metadata = MediaMetadataExtractor.Extract(request.FileStream, request.ContentType);

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
            CreatedBy = currentUserId,
            Checksum = checksum,
            FileSizeBytes = hasContent ? request.FileStream.Length : null,
            WidthPx = metadata.WidthPx,
            HeightPx = metadata.HeightPx,
            DurationSeconds = metadata.DurationSeconds,
            Fps = metadata.Fps
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
            // A concurrent upload of the same file won the unique checksum index: return its media.
            var concurrent = checksum != null ? await FindByChecksumAsync(checksum, request.MissionId) : null;
            if (concurrent != null)
            {
                return UploadInspectionImageResult.From(concurrent, isDuplicate: true);
            }
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

        return UploadInspectionImageResult.From(media);
    }

    /// <summary>
    /// Existing media with this checksum. The checksum is unique across all missions, so the same file
    /// already stored for another mission is a conflict rather than a duplicate to return.
    /// </summary>
    private async Task<InspectionMedia?> FindByChecksumAsync(string checksum, Guid missionId)
    {
        var existing = (await _mediaRepository.FindAsync(m => m.Checksum == checksum, track: false))?.FirstOrDefault();
        if (existing != null && existing.MissionId != missionId)
        {
            throw new BusinessRuleException("DUPLICATE_MEDIA",
                $"This file was already uploaded to another mission (media {existing.Id}, mission {existing.MissionId}).");
        }
        return existing;
    }
}
