using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;

public class AnalyzeMissionMediaCommandHandler
    : IRequestHandler<AnalyzeMissionMediaCommand, AIAnalysisBatchUploadResult>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/tiff",
        "video/mp4", "video/x-msvideo", "video/quicktime", "video/webm"
    };

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeMissionMediaCommandHandler> _logger;

    public AnalyzeMissionMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        IRealtimeNotificationService realtimeNotificationService,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeMissionMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _aiRequestRepository = aiRequestRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _realtimeNotificationService = realtimeNotificationService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AIAnalysisBatchUploadResult> Handle(
        AnalyzeMissionMediaCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, track: false);
        if (mission == null)
        {
            throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        }

        var batchId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var totalFiles = request.Files?.Count ?? 0;
        var acceptedFiles = 0;
        var rejectedFiles = 0;
        var requestIds = new List<Guid>();
        var publishItems = new List<(AIAnalysisRequest Request, Guid MediaId)>();

        foreach (var file in request.Files ?? new List<FileDataDto>())
        {
            var safeFileName = Path.GetFileName(file.FileName);
            var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;

            if (file.Stream == null || (file.Stream.CanSeek && file.Stream.Length == 0))
            {
                rejectedFiles++;
                _logger.LogWarning("Rejected empty mission AI analysis file: MissionId={MissionId}, FileName={FileName}", request.MissionId, safeFileName);
                continue;
            }

            if (!AllowedContentTypes.Contains(contentType))
            {
                rejectedFiles++;
                _logger.LogWarning(
                    "Rejected unsupported mission AI analysis file: MissionId={MissionId}, FileName={FileName}, ContentType={ContentType}",
                    request.MissionId, safeFileName, contentType);
                continue;
            }

            try
            {
                if (file.Stream.CanSeek)
                {
                    file.Stream.Position = 0;
                }

                var fileUrl = await _fileStorageService.SaveImageAsync(file.Stream, safeFileName);
                var mediaType = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "Video" : "Image";

                var media = new InspectionMedia
                {
                    Id = Guid.NewGuid(),
                    MissionId = request.MissionId,
                    AssetId = null,
                    MediaType = mediaType,
                    FileUrl = fileUrl,
                    AiSource = request.PreferredModel,
                    ValidationStatus = "Pending",
                    CapturedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                };
                await _mediaRepository.AddAsync(media);

                var aiRequest = new AIAnalysisRequest
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    UploadedBy = currentUserId,
                    MediaId = media.Id,
                    MissionId = request.MissionId,
                    FileUrl = fileUrl,
                    MediaType = mediaType,
                    AnalysisType = request.AnalysisType,
                    Notes = request.Notes,
                    Status = AIAnalysisStatus.Pending,
                    CreatedBy = currentUserId
                };
                await _aiRequestRepository.AddAsync(aiRequest);

                acceptedFiles++;
                requestIds.Add(aiRequest.Id);
                publishItems.Add((aiRequest, media.Id));
            }
            catch (Exception ex)
            {
                rejectedFiles++;
                _logger.LogError(
                    ex,
                    "Failed to create mission AI analysis item: MissionId={MissionId}, FileName={FileName}",
                    request.MissionId, safeFileName);
            }
        }

        if (acceptedFiles > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var item in publishItems)
            {
                await _eventPublisher.PublishAsync(new AIAnalysisRequestedEvent
                {
                    RequestId = item.Request.Id,
                    FileUrl = item.Request.FileUrl,
                    MediaType = item.Request.MediaType,
                    AnalysisType = item.Request.AnalysisType.ToString(),
                    Notes = item.Request.Notes,
                    UploadedBy = currentUserId,
                    RequestedAt = item.Request.CreatedAt,
                    MediaId = item.MediaId,
                    MissionId = request.MissionId,
                    AssetId = null,
                    PreferredModel = request.PreferredModel
                });

                await _realtimeNotificationService.SendAiAnalysisStatusToUserAsync(
                    currentUserId,
                    new AIAnalysisStatusChangedEvent
                    {
                        RequestId = item.Request.Id,
                        BatchId = batchId,
                        MissionId = request.MissionId,
                        MediaId = item.MediaId,
                        MediaType = item.Request.MediaType,
                        Status = item.Request.Status.ToString(),
                        CreatedAt = item.Request.CreatedAt
                    },
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Created mission AI analysis batch: BatchId={BatchId}, MissionId={MissionId}, TotalFiles={TotalFiles}, AcceptedFiles={AcceptedFiles}, RejectedFiles={RejectedFiles}",
            batchId, request.MissionId, totalFiles, acceptedFiles, rejectedFiles);

        return new AIAnalysisBatchUploadResult
        {
            BatchId = batchId,
            TotalFiles = totalFiles,
            AcceptedFiles = acceptedFiles,
            RejectedFiles = rejectedFiles,
            RequestIds = requestIds,
            CreatedAt = createdAt
        };
    }
}
