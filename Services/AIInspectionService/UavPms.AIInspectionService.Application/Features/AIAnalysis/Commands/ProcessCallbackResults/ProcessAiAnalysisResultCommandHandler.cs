using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using UavPms.Shared.Contracts.Events;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Interfaces;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.ProcessCallbackResults;

/// <summary>
/// Cloud AI callback lifecycle for an <see cref="AIAnalysisRun"/>:
/// Pending → Processing (StartedAt) → Completed/Failed (CompletedAt, ErrorMessage).
/// </summary>
public class ProcessAiAnalysisResultCommandHandler
    : IRequestHandler<ProcessAiAnalysisResultCommand, AiAnalysisCallbackResponseDto>
{
    private const int Wgs84Srid = 4326;
    private static readonly GeometryFactory Wgs84Factory = new(new PrecisionModel(), Wgs84Srid);
    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IGenericRepository<AIAnalysisRun> _runRepo;
    private readonly IInspectionMediaRepository _mediaRepo;
    private readonly IGenericRepository<DefectCategory> _defectCategoryRepo;
    private readonly IAnomalyRepository _anomalyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInspectionEvaluationClient _inspectionEvaluationClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IGenericRepository<OutboxMessage> _outboxRepository;
    private readonly ILogger<ProcessAiAnalysisResultCommandHandler> _logger;

    public ProcessAiAnalysisResultCommandHandler(
        IGenericRepository<AIAnalysisRun> runRepo,
        IInspectionMediaRepository mediaRepo,
        IGenericRepository<DefectCategory> defectCategoryRepo,
        IAnomalyRepository anomalyRepo,
        IUnitOfWork unitOfWork,
        IInspectionEvaluationClient inspectionEvaluationClient,
        IEventPublisher eventPublisher,
        IGenericRepository<OutboxMessage> outboxRepository,
        ILogger<ProcessAiAnalysisResultCommandHandler> logger)
    {
        _runRepo = runRepo;
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
        _logger.LogInformation("Processing AI analysis result for RunId={RunId}, Status={Status}",
            request.RequestId, request.Status);

        // 1. Check the AIAnalysisRun exists
        var run = await _runRepo.GetByIdAsync(request.RequestId, track: true, cancellationToken);
        if (run == null)
        {
            _logger.LogWarning("AIAnalysisRun not found: RunId={RunId}", request.RequestId);
            throw new NotFoundException(nameof(AIAnalysisRun), request.RequestId);
        }

        // Idempotent check
        if (run.Status is AIAnalysisStatus.Completed or AIAnalysisStatus.Failed)
        {
            _logger.LogInformation("AIAnalysisRun {RunId} is already in terminal state {Status}. Skipping duplicate processing.",
                request.RequestId, run.Status);
            return new AiAnalysisCallbackResponseDto
            {
                RequestId = run.Id,
                Status = run.Status.ToString(),
                SavedDetections = 0,
                CreatedAlerts = 0,
                ProcessedAt = DateTime.UtcNow
            };
        }

        if (run.MediaId != request.MediaId)
            throw new BusinessRuleException("AI callback identifiers do not match the stored analysis run.");

        var media = await _mediaRepo.GetByIdWithDetailsAsync(run.MediaId)
            ?? throw new NotFoundException(nameof(InspectionMedia), run.MediaId);
        if (request.MissionId.HasValue && media.MissionId != request.MissionId)
            throw new BusinessRuleException("AI callback identifiers do not match the stored inspection media.");

        if ("Processing".Equals(request.Status, StringComparison.OrdinalIgnoreCase))
        {
            if (run.Status == AIAnalysisStatus.Pending)
            {
                run.Status = AIAnalysisStatus.Processing;
                run.StartedAt ??= DateTime.UtcNow;
                run.UpdatedAt = DateTime.UtcNow;
                await _runRepo.UpdateAsync(run);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return new AiAnalysisCallbackResponseDto
            {
                RequestId = run.Id,
                Status = run.Status.ToString(),
                ProcessedAt = DateTime.UtcNow
            };
        }

        if (run.Status != AIAnalysisStatus.Processing)
            throw new BusinessRuleException("AI analysis must enter Processing before reaching a terminal state.");

        var savedDetections = 0;
        var createdAlerts = 0;
        var eventsToPublish = new List<object>();

        try
        {
            if ("Completed".Equals(request.Status, StringComparison.OrdinalIgnoreCase))
            {
                var detections = request.Detections ?? [];

                // Edge results on the same media that a cloud result may supersede.
                var edgeCandidates = detections.Count == 0
                    ? new List<DetectedAnomaly>()
                    : (await _anomalyRepo.FindAsync(a =>
                          a.MediaId == media.Id &&
                          a.SupersededById == null &&
                          a.AnalysisRunId != null &&
                          a.AnalysisRunId != run.Id &&
                          a.AnalysisRun!.ExecutionLocation == AIAnalysisRun.Edge, track: true)
                       ?? Array.Empty<DetectedAnomaly>()).ToList();

                foreach (var detection in detections)
                {
                    // Map category code to category ID
                    var categoryList = await _defectCategoryRepo.FindAsync(c => c.CategoryCode == detection.CategoryCode);
                    var category = categoryList?.FirstOrDefault();
                    if (category == null)
                    {
                        _logger.LogWarning("DefectCategory not found for CategoryCode={CategoryCode}", detection.CategoryCode);
                        throw new BusinessRuleException($"DefectCategory with CategoryCode '{detection.CategoryCode}' was not found.");
                    }

                    var bboxJson = JsonSerializer.Serialize(new
                    {
                        x = detection.BoundingBox.X,
                        y = detection.BoundingBox.Y,
                        width = detection.BoundingBox.Width,
                        height = detection.BoundingBox.Height
                    }, CamelCase);

                    var frameTime = detection.Timestamp
                        ?? (detection.TimestampMs.HasValue
                            ? Math.Round(detection.TimestampMs.Value / 1000d, 3)
                            : null);

                    var anomaly = new DetectedAnomaly
                    {
                        Id = Guid.NewGuid(),
                        MediaId = media.Id,
                        AnalysisRunId = run.Id,
                        CategoryId = category.Id,
                        BoundingBox = bboxJson,
                        AiDetectionId = string.IsNullOrWhiteSpace(detection.Id) ? null : detection.Id.Trim(),
                        FrameIndex = detection.FrameIndex,
                        FrameTimeSeconds = frameTime,
                        ImageUrl = string.IsNullOrWhiteSpace(detection.ImageUrl)
                            ? media.FileUrl
                            : detection.ImageUrl.Trim(),
                        CropUrl = string.IsNullOrWhiteSpace(detection.CropUrl)
                            ? null
                            : detection.CropUrl.Trim(),
                        Location = ToPoint(detection.Gps),
                        TowerId = ResolveTowerId(detection.TowerId, media.TowerId),
                        ConfidenceScore = Math.Round(detection.Confidence, 3),
                        ValidationStatus = "Pending",
                        AnalystNotes = string.Empty,
                        ValidatedAt = null,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _anomalyRepo.AddAsync(anomaly);
                    savedDetections++;

                    // Cloud result replaces a matching (same media + same category) edge result.
                    foreach (var edge in edgeCandidates.Where(e => e.CategoryId == category.Id).ToList())
                    {
                        edge.SupersededById = anomaly.Id;
                        edge.UpdatedAt = DateTime.UtcNow;
                        await _anomalyRepo.UpdateAsync(edge);
                        edgeCandidates.Remove(edge);
                    }

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

                    anomaly.AnalystNotes = string.IsNullOrWhiteSpace(evaluation?.Reason)
                        ? anomaly.AnalystNotes
                        : $"AI evaluation: severity={evaluation.Severity}, risk={evaluation.RiskLevel}, score={evaluation.PriorityScore}. {evaluation.Reason}";

                    await QueueEventAsync(new AiDetectionCompletedEvent
                    {
                        EventId = Guid.NewGuid(),
                        RequestId = run.Id,
                        InspectionId = media.MissionId,
                        MissionId = media.MissionId,
                        MediaId = media.Id,
                        TowerId = anomaly.TowerId,
                        AnomalyId = anomaly.Id,
                        CategoryCode = detection.CategoryCode,
                        DefectType = category.CategoryName,
                        Confidence = Math.Round(detection.Confidence, 3),
                        Severity = evaluation?.Severity ?? "Medium",
                        IsEmergencyClass = category.IsEmergencyClass,
                        IsDefect = true,
                        ImageUrl = anomaly.ImageUrl ?? media.FileUrl,
                        CropUrl = anomaly.CropUrl,
                        DetectedAt = anomaly.CreatedAt
                    }, eventsToPublish);
                }

                // Update InspectionMedia: review status + video metadata (previously stored on each anomaly).
                media.ValidationStatus = "PendingReview";
                if (request.VideoMetadata != null)
                {
                    media.DurationSeconds = request.VideoMetadata.Duration ?? media.DurationSeconds;
                    media.Fps = request.VideoMetadata.Fps ?? media.Fps;
                    media.WidthPx = request.VideoMetadata.Width ?? media.WidthPx;
                    media.HeightPx = request.VideoMetadata.Height ?? media.HeightPx;
                }
                media.UpdatedAt = DateTime.UtcNow;
                await _mediaRepo.UpdateAsync(media);

                run.Status = AIAnalysisStatus.Completed;
                run.ErrorMessage = null;
                if (!string.IsNullOrWhiteSpace(request.ModelName))
                    run.ModelName = Truncate(request.ModelName.Trim(), 100);
                if (!string.IsNullOrWhiteSpace(request.ModelVersion))
                    run.ModelVersion = Truncate(request.ModelVersion.Trim(), 50);
            }
            else // Failed
            {
                run.Status = AIAnalysisStatus.Failed;
                run.ErrorMessage = string.IsNullOrWhiteSpace(request.ErrorCode)
                    ? request.ErrorMessage
                    : $"[{request.ErrorCode}] {request.ErrorMessage}";
            }

            run.StartedAt ??= DateTime.UtcNow;
            run.CompletedAt = request.CompletedAt;
            run.UpdatedAt = DateTime.UtcNow;
            await _runRepo.UpdateAsync(run);

            if (run.RequestedBy.HasValue && run.RequestedBy.Value != Guid.Empty)
            {
                await QueueEventAsync(new AIAnalysisStatusChangedEvent
                {
                    UserId = run.RequestedBy.Value,
                    RequestId = run.Id,
                    BatchId = run.BatchId,
                    MissionId = media.MissionId,
                    MediaId = run.MediaId,
                    MediaType = media.MediaType,
                    Status = run.Status.ToString(),
                    SavedDetections = savedDetections,
                    CreatedAlerts = createdAlerts,
                    ErrorCode = request.ErrorCode,
                    ErrorMessage = request.ErrorMessage,
                    CreatedAt = run.CreatedAt,
                    CompletedAt = run.CompletedAt
                }, eventsToPublish);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while processing AI analysis result.");
            throw;
        }

        _logger.LogInformation("Successfully processed AI analysis result. RunId={RunId}, Status={Status}",
            request.RequestId, run.Status.ToString());

        return new AiAnalysisCallbackResponseDto
        {
            RequestId = request.RequestId,
            Status = run.Status.ToString(),
            SavedDetections = savedDetections,
            CreatedAlerts = createdAlerts,
            ProcessedAt = DateTime.UtcNow
        };
    }

    /// <summary>Detection gps → geometry(Point,4326) (X = longitude, Y = latitude).</summary>
    internal static Point? ToPoint(GpsDto? gps)
    {
        if (gps?.Lat is not double lat || gps.Lng is not double lng) return null;
        return Wgs84Factory.CreatePoint(new Coordinate(lng, lat));
    }

    /// <summary>The worker may report a tower id; it must be a Guid, otherwise fall back to the media tower.</summary>
    private static Guid? ResolveTowerId(string? detectionTowerId, Guid? mediaTowerId)
        => !string.IsNullOrWhiteSpace(detectionTowerId) && Guid.TryParse(detectionTowerId.Trim(), out var parsed)
            ? parsed
            : mediaTowerId;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private async Task QueueEventAsync(object integrationEvent, ICollection<object> fallback)
    {
        await _outboxRepository.AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = integrationEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAt = DateTime.UtcNow
        });
    }
}
