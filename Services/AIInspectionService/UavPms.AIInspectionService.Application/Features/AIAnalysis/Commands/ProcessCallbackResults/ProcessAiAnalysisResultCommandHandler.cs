using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Shared.Contracts.Events;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Application.Common.Utilities;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInspectionEvaluationClient _inspectionEvaluationClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IGenericRepository<OutboxMessage> _outboxRepository;
    private readonly ILogger<ProcessAiAnalysisResultCommandHandler> _logger;

    public ProcessAiAnalysisResultCommandHandler(
        IGenericRepository<AIAnalysisRequest> aiRequestRepo,
        IInspectionMediaRepository mediaRepo,
        IGenericRepository<DefectCategory> defectCategoryRepo,
        IAnomalyRepository anomalyRepo,
        IUnitOfWork unitOfWork,
        IInspectionEvaluationClient inspectionEvaluationClient,
        IEventPublisher eventPublisher,
        IGenericRepository<OutboxMessage> outboxRepository,
        ILogger<ProcessAiAnalysisResultCommandHandler> logger)
    {
        _aiRequestRepo = aiRequestRepo;
        _mediaRepo = mediaRepo;
        _defectCategoryRepo = defectCategoryRepo;
        _anomalyRepo = anomalyRepo;
        _unitOfWork = unitOfWork;
        _inspectionEvaluationClient = inspectionEvaluationClient;
        _eventPublisher = eventPublisher;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<AiAnalysisCallbackResponseDto> Handle(
        ProcessAiAnalysisResultCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing AI analysis result for RequestId={RequestId}, Status={Status}", 
            request.RequestId, request.Status);

        // 1. Check if AIAnalysisRequest exists
        var aiRequest = await _aiRequestRepo.GetByIdAsync(request.RequestId, track: true, cancellationToken);
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

        if (aiRequest.MediaId != request.MediaId || aiRequest.MissionId != request.MissionId ||
            aiRequest.AssetId != request.AssetId)
            throw new BusinessRuleException("AI callback identifiers do not match the stored analysis request.");

        var media = await _mediaRepo.GetByIdWithDetailsAsync(request.MediaId!.Value)
            ?? throw new NotFoundException(nameof(InspectionMedia), request.MediaId.Value);
        if (!media.AssetId.HasValue || media.AssetId == Guid.Empty || media.AssetId != request.AssetId ||
            media.MissionId != request.MissionId)
            throw new BusinessRuleException("AI callback identifiers do not match the stored inspection media.");

        if ("Processing".Equals(request.Status, StringComparison.OrdinalIgnoreCase))
        {
            if (aiRequest.Status == AIAnalysisStatus.Pending)
            {
                aiRequest.Status = AIAnalysisStatus.Processing;
                aiRequest.UpdatedAt = DateTime.UtcNow;
                await _aiRequestRepo.UpdateAsync(aiRequest);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return new AiAnalysisCallbackResponseDto
            {
                RequestId = aiRequest.Id,
                Status = aiRequest.Status.ToString(),
                ProcessedAt = DateTime.UtcNow
            };
        }

        if (aiRequest.Status != AIAnalysisStatus.Processing)
            throw new BusinessRuleException("AI analysis must enter Processing before reaching a terminal state.");

        var savedDetections = 0;
        var createdAlerts = 0;
        var eventsToPublish = new List<object>();

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
                            AssetId = media.AssetId.Value,
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

            if (aiRequest!.UploadedBy != Guid.Empty)
            {
                await QueueEventAsync(new AIAnalysisStatusChangedEvent
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
                }, eventsToPublish);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while processing AI analysis result.");
            throw;
        }
        foreach (var integrationEvent in eventsToPublish)
            await PublishFallbackAsync(integrationEvent);

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

    private async Task QueueEventAsync(object integrationEvent, ICollection<object> fallback)
    {
        await _outboxRepository.AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = integrationEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
    }

    private Task PublishFallbackAsync(object integrationEvent) => integrationEvent switch
    {
        DefectDetectedEvent defect => _eventPublisher.PublishAsync(defect),
        AIAnalysisStatusChangedEvent status => _eventPublisher.PublishAsync(status),
        _ => throw new InvalidOperationException($"Unsupported integration event {integrationEvent.GetType().Name}.")
    };
}
