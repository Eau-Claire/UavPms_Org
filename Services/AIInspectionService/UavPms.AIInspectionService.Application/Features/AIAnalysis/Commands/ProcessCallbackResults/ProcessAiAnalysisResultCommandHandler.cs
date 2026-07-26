using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Shared.Contracts.Events;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

public class ProcessAiAnalysisResultCommandHandler
    : IRequestHandler<ProcessAiAnalysisResultCommand, AiAnalysisCallbackResponseDto>
{
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepo;
    private readonly IInspectionMediaRepository _mediaRepo;
    private readonly IGenericRepository<DefectCategory> _defectCategoryRepo;
    private readonly IAnomalyRepository _anomalyRepo;
    private readonly IGenericRepository<EmergencyAlert> _emergencyAlertRepo;
    private readonly INotificationRepository _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInspectionEvaluationClient _inspectionEvaluationClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ProcessAiAnalysisResultCommandHandler> _logger;

    public ProcessAiAnalysisResultCommandHandler(
        IGenericRepository<AIAnalysisRequest> aiRequestRepo,
        IInspectionMediaRepository mediaRepo,
        IGenericRepository<DefectCategory> defectCategoryRepo,
        IAnomalyRepository anomalyRepo,
        IGenericRepository<EmergencyAlert> emergencyAlertRepo,
        INotificationRepository notificationRepo,
        IUnitOfWork unitOfWork,
        IInspectionEvaluationClient inspectionEvaluationClient,
        IEventPublisher eventPublisher,
        ILogger<ProcessAiAnalysisResultCommandHandler> logger)
    {
        _aiRequestRepo = aiRequestRepo;
        _mediaRepo = mediaRepo;
        _defectCategoryRepo = defectCategoryRepo;
        _anomalyRepo = anomalyRepo;
        _emergencyAlertRepo = emergencyAlertRepo;
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
        _inspectionEvaluationClient = inspectionEvaluationClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<AiAnalysisCallbackResponseDto> Handle(
        ProcessAiAnalysisResultCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing AI analysis result for RequestId={RequestId}, Status={Status}", 
            request.RequestId, request.Status);

        // 1. Check if AIAnalysisRequest exists
        var aiRequest = await _aiRequestRepo.GetByIdAsync(request.RequestId, track: true);
        if (aiRequest == null)
        {
            _logger.LogWarning("AIAnalysisRequest not found: RequestId={RequestId}", request.RequestId);
            throw new NotFoundException(nameof(AIAnalysisRequest), request.RequestId);
        }

        // Idempotent check
        if (aiRequest.Status is AIAnalysisStatus.Completed or AIAnalysisStatus.Failed)
        {
            _logger.LogInformation("AIAnalysisRequest {RequestId} is already in terminal state {Status}. Skipping duplicate processing.",
                request.RequestId, aiRequest.Status);
            return new AiAnalysisCallbackResponseDto
            {
                RequestId = aiRequest.Id,
                Status = aiRequest.Status.ToString(),
                SavedDetections = 0,
                CreatedAlerts = 0,
                ProcessedAt = DateTime.UtcNow
            };
        }

        // 2. Resolve InspectionMedia from callback MediaId, or fallback to the persisted AIAnalysisRequest link.
        var resolvedMediaId = request.MediaId.HasValue && request.MediaId.Value != Guid.Empty
            ? request.MediaId
            : aiRequest.MediaId;

        InspectionMedia? media = null;
        if (resolvedMediaId != null && resolvedMediaId.Value != Guid.Empty)
        {
            media = await _mediaRepo.GetByIdWithDetailsAsync(resolvedMediaId.Value);
            if (media == null)
            {
                _logger.LogWarning(
                    "InspectionMedia not found while processing AI result: RequestId={RequestId}, MediaId={MediaId}",
                    request.RequestId, resolvedMediaId);
                throw new NotFoundException(nameof(InspectionMedia), resolvedMediaId.Value);
            }
        }
        else if (!string.IsNullOrWhiteSpace(aiRequest.FileUrl))
        {
            var matchingMedia = await _mediaRepo.FindAsync(m => m.FileUrl == aiRequest.FileUrl, track: false);
            var mediaByFileUrl = matchingMedia.FirstOrDefault();
            if (mediaByFileUrl != null)
            {
                media = await _mediaRepo.GetByIdWithDetailsAsync(mediaByFileUrl.Id);
                if (media != null)
                {
                    aiRequest.MediaId = media.Id;
                    aiRequest.MissionId = media.MissionId;

                    _logger.LogInformation(
                        "Resolved AI result media by file URL fallback: RequestId={RequestId}, MediaId={MediaId}",
                        request.RequestId, media.Id);
                }
            }
        }

        var savedDetections = 0;
        var createdAlerts = 0;
        var notificationsToPush = new List<Notification>();

        // Save Changes
        try
        {
            if ("Completed".Equals(request.Status, StringComparison.OrdinalIgnoreCase))
            {
                // 3. Process detections if media is present
                if (media != null && request.Detections != null)
                {
                    foreach (var detection in request.Detections)
                    {
                        // Map category code to category ID
                        var categoryList = await _defectCategoryRepo.FindAsync(c => c.CategoryCode == detection.CategoryCode);
                        var category = categoryList.FirstOrDefault();
                        if (category == null)
                        {
                            _logger.LogWarning("DefectCategory not found for CategoryCode={CategoryCode}", detection.CategoryCode);
                            throw new BusinessRuleException($"DefectCategory with CategoryCode '{detection.CategoryCode}' was not found.");
                        }

                        // Serialize bounding box
                        var bboxJson = JsonSerializer.Serialize(new
                        {
                            x = detection.BoundingBox.X,
                            y = detection.BoundingBox.Y,
                            width = detection.BoundingBox.Width,
                            height = detection.BoundingBox.Height
                        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                        // 4. Create DetectedAnomaly
                        var timestamp = detection.Timestamp
                            ?? (detection.TimestampMs.HasValue
                                ? Math.Round(detection.TimestampMs.Value / 1000d, 3)
                                : null);
                        var gpsJson = detection.Gps == null
                            ? null
                            : JsonSerializer.Serialize(detection.Gps, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });

                        var anomaly = new DetectedAnomaly
                        {
                            Id = Guid.NewGuid(),
                            MediaId = media.Id,
                            AssetId = detection.AssetId ?? media.AssetId,
                            CategoryId = category.Id,
                            BoundingBox = bboxJson,
                            AiDetectionId = string.IsNullOrWhiteSpace(detection.Id) ? null : detection.Id.Trim(),
                            FrameIndex = detection.FrameIndex,
                            Timestamp = timestamp,
                            ImageUrl = string.IsNullOrWhiteSpace(detection.ImageUrl)
                                ? media.FileUrl
                                : detection.ImageUrl.Trim(),
                            CropUrl = string.IsNullOrWhiteSpace(detection.CropUrl)
                                ? null
                                : detection.CropUrl.Trim(),
                            Gps = gpsJson,
                            TowerId = string.IsNullOrWhiteSpace(detection.TowerId)
                                ? null
                                : detection.TowerId.Trim(),
                            VideoDuration = request.VideoMetadata?.Duration,
                            VideoFps = request.VideoMetadata?.Fps,
                            VideoWidth = request.VideoMetadata?.Width,
                            VideoHeight = request.VideoMetadata?.Height,
                            ConfidenceScore = Math.Round(detection.Confidence, 3),
                            ValidationStatus = "Pending",
                            AiSource = request.ModelName ?? "UnknownModel",
                            AnalystNotes = string.Empty,
                            ValidatedAt = null,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _anomalyRepo.AddAsync(anomaly);
                        savedDetections++;

                        var evaluation = await _inspectionEvaluationClient.EvaluateAsync(
                            new DetectionEvaluationRequest(
                                detection.CategoryCode,
                                category.CategoryName,
                                detection.Confidence,
                                category.IsEmergencyClass,
                                media.MissionId,
                                media.Id,
                                detection.Id),
                            cancellationToken);

                        anomaly.AnalystNotes = string.IsNullOrWhiteSpace(evaluation.Reason)
                            ? anomaly.AnalystNotes
                            : $"AI evaluation: severity={evaluation.Severity}, risk={evaluation.RiskLevel}, score={evaluation.PriorityScore}. {evaluation.Reason}";

                        // 5. Check if it's an emergency alert
                        if (evaluation.RequiresImmediateAlert)
                        {
                            var latencySeconds = (int)Math.Max(0, (DateTime.UtcNow - request.CompletedAt).TotalSeconds);

                            var alert = new EmergencyAlert
                            {
                                Id = Guid.NewGuid(),
                                AnomalyId = anomaly.Id,
                                AssetId = media.AssetId,
                                MissionId = media.MissionId,
                                Status = "Open",
                                Priority = "Critical",
                                TriggeredAt = DateTime.UtcNow,
                                DeliveryLatencySeconds = latencySeconds
                            };

                            await _emergencyAlertRepo.AddAsync(alert);
                            createdAlerts++;

                            await _eventPublisher.PublishAsync(new DefectDetectedEvent
                            {
                                InspectionId = media.MissionId,
                                RecordId = anomaly.Id,
                                MissionId = media.MissionId,
                                ImageUrl = anomaly.ImageUrl ?? media.FileUrl,
                                IsDefect = true,
                                DefectType = $"{category.CategoryName} ({evaluation.Severity})",
                                DetectedAt = DateTime.UtcNow
                            });

                            // 6. Send Notification to Mission Manager
                            if (media.Mission != null)
                            {
                                var managerId = media.Mission.ManagerId;
                                if (managerId != Guid.Empty)
                                {
                                    var notification = new Notification
                                    {
                                        Id = Guid.NewGuid(),
                                        UserId = managerId,
                                        Type = "CriticalAlert",
                                        ReferenceType = "EmergencyAlert",
                                        ReferenceId = alert.Id,
                                        Title = "⚠️ Cảnh báo khẩn cấp: Phát hiện sự cố nghiêm trọng",
                                        Body = $"Phát hiện khuyết tật khẩn cấp '{category.CategoryName}' ({detection.CategoryCode}) với độ tin cậy {detection.Confidence:P1} tại thiết bị.",
                                        IsRead = false,
                                        SentAt = DateTime.UtcNow
                                    };

                                    await _notificationRepo.AddAsync(notification);
                                    notificationsToPush.Add(notification);
                                }
                            }
                        }
                    }
                }

                // 7. Update InspectionMedia if present
                if (media != null)
                {
                    media.AiSource = request.ModelName ?? "UnknownModel";
                    media.ValidationStatus = "PendingReview";
                    media.UpdatedAt = DateTime.UtcNow;
                    await _mediaRepo.UpdateAsync(media);
                }

                // 8. Update AIAnalysisRequest if present
                if (aiRequest != null)
                {
                    aiRequest.Status = AIAnalysisStatus.Completed;
                    aiRequest.Result = JsonSerializer.Serialize(new
                    {
                        modelName = request.ModelName,
                        modelVersion = request.ModelVersion,
                        processingTimeMs = request.ProcessingTimeMs,
                        savedDetectionsCount = savedDetections,
                        completedAt = request.CompletedAt,
                        videoMetadata = request.VideoMetadata,
                        rawResult = request.RawResult
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                }
            }
            else // Failed
            {
                // 9. Handle Failed Status if present
                if (aiRequest != null)
                {
                    aiRequest.Status = AIAnalysisStatus.Failed;
                    aiRequest.Result = JsonSerializer.Serialize(new
                    {
                        errorCode = request.ErrorCode,
                        errorMessage = request.ErrorMessage,
                        completedAt = request.CompletedAt
                    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                }
            }

            if (aiRequest != null)
            {
                aiRequest.CompletedAt = request.CompletedAt;
                aiRequest.UpdatedAt = DateTime.UtcNow;
                await _aiRequestRepo.UpdateAsync(aiRequest);
            }

            // Save Changes
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while processing AI analysis result.");
            throw;
        }



        foreach (var notification in notificationsToPush)
        {
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
        }

        if (aiRequest != null && aiRequest.UploadedBy != Guid.Empty)
        {
            await _eventPublisher.PublishAsync(
                new UavPms.Shared.Contracts.Events.AIAnalysisStatusChangedEvent
                {
                    UserId = aiRequest.UploadedBy,
                    RequestId = aiRequest.Id,
                    BatchId = aiRequest.BatchId,
                    MissionId = aiRequest.MissionId,
                    MediaId = aiRequest.MediaId,
                    MediaType = aiRequest.MediaType,
                    Status = aiRequest.Status.ToString(),
                    SavedDetections = savedDetections,
                    CreatedAlerts = createdAlerts,
                    ErrorCode = request.ErrorCode,
                    ErrorMessage = request.ErrorMessage,
                    CreatedAt = aiRequest.CreatedAt,
                    CompletedAt = aiRequest.CompletedAt
                });
        }

        _logger.LogInformation("Successfully processed AI analysis result. RequestId={RequestId}, Status={Status}", 
            request.RequestId, aiRequest?.Status.ToString() ?? "Completed");

        return new AiAnalysisCallbackResponseDto
        {
            RequestId = request.RequestId,
            Status = aiRequest?.Status.ToString() ?? "Completed",
            SavedDetections = savedDetections,
            CreatedAlerts = createdAlerts,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
