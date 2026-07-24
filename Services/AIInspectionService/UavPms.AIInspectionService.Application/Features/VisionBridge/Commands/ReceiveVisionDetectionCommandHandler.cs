using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Application.Features.VisionBridge.DTOs;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;

namespace UavPms.AIInspectionService.Application.Features.VisionBridge.Commands;

public class ReceiveVisionDetectionCommandHandler
    : IRequestHandler<ReceiveVisionDetectionCommand, VisionDetectionResultDto>
{
    private readonly IGenericRepository<Uav> _uavRepository;
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<DetectedAnomaly> _anomalyRepository;
    private readonly IGenericRepository<DefectCategory> _defectCategoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IEmailService _emailService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ReceiveVisionDetectionCommandHandler> _logger;

    public ReceiveVisionDetectionCommandHandler(
        IGenericRepository<Uav> uavRepository,
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<DetectedAnomaly> anomalyRepository,
        IGenericRepository<DefectCategory> defectCategoryRepository,
        IUserRepository userRepository,
        INotificationRepository notificationRepository,
        IEmailService emailService,
        IEventPublisher eventPublisher,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        ILogger<ReceiveVisionDetectionCommandHandler> logger)
    {
        _uavRepository = uavRepository;
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _anomalyRepository = anomalyRepository;
        _defectCategoryRepository = defectCategoryRepository;
        _userRepository = userRepository;
        _notificationRepository = notificationRepository;
        _emailService = emailService;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<VisionDetectionResultDto> Handle(
        ReceiveVisionDetectionCommand request,
        CancellationToken cancellationToken)
    {
        var detection = request.Detection;
        var receivedAt = DateTime.UtcNow;

        _logger.LogWarning(
            "VISION DETECTION: Drone={DroneId}, Class={ClassName}, Confidence={Confidence:P1}",
            detection.DroneId, detection.ClassName, detection.Confidence);

        var uavs = await _uavRepository.FindAsync(u => u.UavCode == detection.DroneId);
        var uav = uavs.FirstOrDefault();

        if (uav == null && Guid.TryParse(detection.DroneId, out var uavGuid))
        {
            uav = await _uavRepository.GetByIdAsync(uavGuid);
        }

        if (uav == null)
        {
            _logger.LogWarning("UAV not found: {DroneId}", detection.DroneId);
            return new VisionDetectionResultDto
            {
                Success = false,
                Message = $"UAV not registered: {detection.DroneId}",
                ReceivedAt = receivedAt
            };
        }

        var missions = await _missionRepository.FindAsync(m => m.UavId == uav.Id && m.Status == "Executing");
        var activeMission = missions.FirstOrDefault();

        if (activeMission == null)
        {
            _logger.LogWarning("No active executing mission found for UAV: {UavCode}", uav.UavCode);
            return new VisionDetectionResultDto
            {
                Success = false,
                Message = $"No active mission executing for UAV: {uav.UavCode}",
                ReceivedAt = receivedAt
            };
        }

        string? savedImageUrl = null;
        if (request.EvidenceImageStream != null && !string.IsNullOrEmpty(request.EvidenceFileName))
        {
            try
            {
                savedImageUrl = await _fileStorageService.SaveImageAsync(
                    request.EvidenceImageStream, request.EvidenceFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save evidence image");
            }
        }

        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = activeMission.Id,
            AssetId = Guid.Empty,
            MediaType = "Image",
            FileUrl = savedImageUrl ?? string.Empty,
            AiSource = "VisionEdge",
            ValidationStatus = "Pending",
            CapturedAt = DateTime.SpecifyKind(detection.Timestamp, DateTimeKind.Utc)
        };

        await _mediaRepository.AddAsync(media);

        var boundingBoxJson = detection.BoundingBox != null
            ? JsonSerializer.Serialize(new
            {
                x1 = detection.BoundingBox.Length > 0 ? detection.BoundingBox[0] : 0,
                y1 = detection.BoundingBox.Length > 1 ? detection.BoundingBox[1] : 0,
                x2 = detection.BoundingBox.Length > 2 ? detection.BoundingBox[2] : 0,
                y2 = detection.BoundingBox.Length > 3 ? detection.BoundingBox[3] : 0,
            })
            : "{}";

        var categories = await _defectCategoryRepository.FindAsync(c => c.CategoryCode == detection.ClassName);
        var category = categories.FirstOrDefault();
        var categoryId = category?.Id ?? 1;

        var anomaly = new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            AssetId = Guid.Empty,
            CategoryId = categoryId,
            BoundingBox = boundingBoxJson,
            ConfidenceScore = detection.Confidence,
            ValidationStatus = "Pending",
            AiSource = "VisionEdge",
            AnalystNotes = $"Track ID: {detection.TrackId}"
        };

        await _anomalyRepository.AddAsync(anomaly);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Detection linked: Mission={MissionCode}, AnomalyId={AnomalyId}",
            activeMission.MissionCode, anomaly.Id);

        // Xác định khuyết tật nguy hiểm (Critical Anomaly)
        bool isCritical = false;
        if (category != null && category.IsEmergencyClass)
        {
            isCritical = true;
        }
        else if (detection.ClassName.Contains("Anomaly", StringComparison.OrdinalIgnoreCase) || 
                 detection.ClassName.Contains("Critical", StringComparison.OrdinalIgnoreCase) || 
                 detection.ClassName.Contains("Emergency", StringComparison.OrdinalIgnoreCase))
        {
            isCritical = true;
        }

        try
        {
            // Lấy thông tin những người tham gia trực tiếp vào nhiệm vụ (AssignedToUser, Manager, Inspector)
            var participantIds = new List<Guid>
            {
                activeMission.AssignedToUserId,
                activeMission.ManagerId,
                activeMission.InspectorId
            }
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

            var usersToNotify = new List<User>();
            foreach (var pId in participantIds)
            {
                var user = await _userRepository.GetByIdAsync(pId);
                if (user != null)
                {
                    usersToNotify.Add(user);
                }
            }

            string title = isCritical ? "🚨 CẢNH BÁO KHẨN CẤP: Phát hiện khuyết tật nguy hiểm!" : "⚠️ Phát hiện bất thường từ Edge Camera";
            string body = isCritical 
                ? $"Hệ thống giám sát biên phát hiện khuyết tật/hành vi NGUY HIỂM '{detection.ClassName}' (độ tin cậy: {detection.Confidence:P1}) tại tọa độ ({detection.Latitude:F6}, {detection.Longitude:F6}) thuộc nhiệm vụ {activeMission.MissionCode}. Cần xử lý khẩn cấp!"
                : $"Hệ thống giám sát biên (Edge Device) phát hiện hành vi bất thường '{detection.ClassName}' (độ tin cậy: {detection.Confidence:P1}) thuộc nhiệm vụ {activeMission.MissionCode}.";

            foreach (var user in usersToNotify)
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Type = isCritical ? "CriticalAlert" : "InfoAlert",
                    ReferenceType = "DetectedAnomaly",
                    ReferenceId = anomaly.Id,
                    Title = title,
                    Body = body,
                    IsRead = false,
                    SentAt = DateTime.UtcNow
                };

                await _notificationRepository.AddAsync(notification);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _eventPublisher.PublishAsync(new NotificationPushEvent
                {
                    UserId = notification.UserId,
                    NotificationId = notification.Id,
                    Type = notification.Type,
                    Title = notification.Title,
                    Body = notification.Body,
                    ReferenceType = notification.ReferenceType,
                    ReferenceId = notification.ReferenceId,
                    IsRead = notification.IsRead,
                    SentAt = notification.SentAt
                });

                // Nếu là khuyết tật nguy hiểm (Critical), push email lập tức cho người tham gia!
                if (isCritical && !string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(user.Email, title, body);
                        
                        notification.IsPushed = true;
                        notification.PushedAt = DateTime.UtcNow;
                        await _notificationRepository.UpdateAsync(notification);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed to immediately push notification email to {Email}", user.Email);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification/push for anomaly {AnomalyId}", anomaly.Id);
        }

        return new VisionDetectionResultDto
        {
            Success = true,
            Message = $"Detection linked to mission: {activeMission.MissionCode}",
            RecordId = anomaly.Id,
            ReceivedAt = receivedAt
        };
    }
}
