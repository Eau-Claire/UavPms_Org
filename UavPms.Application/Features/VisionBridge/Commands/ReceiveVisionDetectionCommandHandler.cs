using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Application.Features.VisionBridge.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.Application.Features.Notifications.Commands.CreateNotification;

namespace UavPms.Application.Features.VisionBridge.Commands;

public class ReceiveVisionDetectionCommandHandler
    : IRequestHandler<ReceiveVisionDetectionCommand, VisionDetectionResultDto>
{
    private readonly IGenericRepository<Uav> _uavRepository;
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<DetectedAnomaly> _anomalyRepository;
    private readonly IGenericRepository<DefectCategory> _defectCategoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISender _mediator;
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
        ISender mediator,
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
        _mediator = mediator;
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

        try
        {
            var admins = await _userRepository.GetUsersByRoleAsync("SystemAdmin");
            var managers = await _userRepository.GetUsersByRoleAsync("Manager");

            var usersToNotify = admins.Concat(managers)
                .DistinctBy(u => u.Id)
                .ToList();

            foreach (var user in usersToNotify)
            {
                await _mediator.Send(new CreateNotificationCommand(
                    user.Id,
                    "CriticalAlert",
                    "DetectedAnomaly",
                    anomaly.Id,
                    "⚠️ Phát hiện bất thường từ Edge Camera",
                    $"Hệ thống giám sát biên (Edge Device) phát hiện hành vi bất thường '{detection.ClassName}' (độ tin cậy: {detection.Confidence:P1}) thuộc nhiệm vụ {activeMission.MissionCode}."
                ), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification for anomaly {AnomalyId}", anomaly.Id);
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
