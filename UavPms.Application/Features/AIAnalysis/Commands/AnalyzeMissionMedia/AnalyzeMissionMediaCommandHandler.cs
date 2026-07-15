using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;

public class AnalyzeMissionMediaCommandHandler
    : IRequestHandler<AnalyzeMissionMediaCommand, AIAnalysisBatchUploadResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeMissionMediaCommandHandler> _logger;

    public AnalyzeMissionMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeMissionMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _aiRequestRepository = aiRequestRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AIAnalysisBatchUploadResult> Handle(
        AnalyzeMissionMediaCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var batchId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // 1. Kiểm tra Mission tồn tại
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, track: false);
        if (mission == null)
        {
            throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        }

        var allowedTypes = new[]
        {
            "image/jpeg", "image/png", "image/webp", "image/tiff",
            "video/mp4", "video/x-msvideo", "video/quicktime", "video/webm"
        };

        var requestIds = new List<Guid>();
        int totalFiles = request.Files.Count;
        int acceptedFiles = 0;
        int rejectedFiles = 0;

        var itemsToPublish = new List<(AIAnalysisRequest Req, Guid MediaId)>();

        foreach (var fileDto in request.Files)
        {
            if (fileDto.Stream == null || fileDto.Stream.Length == 0)
            {
                rejectedFiles++;
                continue;
            }

            var contentType = fileDto.ContentType.ToLower();
            if (Array.IndexOf(allowedTypes, contentType) < 0)
            {
                rejectedFiles++;
                _logger.LogWarning("File '{FileName}' has unsupported content type '{ContentType}' and was rejected.", fileDto.FileName, fileDto.ContentType);
                continue;
            }

            try
            {
                // 2. Lưu file ảnh/video vào hệ thống
                var fileUrl = await _fileStorageService.SaveImageAsync(fileDto.Stream, fileDto.FileName);

                var mediaType = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                    ? "Video"
                    : "Image";

                var mediaId = Guid.NewGuid();
                var aiRequestId = Guid.NewGuid();

                // 3. Tạo bản ghi InspectionMedia để lưu kết quả phân tích AI sau này
                var media = new InspectionMedia
                {
                    Id = mediaId,
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

                // 4. Tạo bản ghi AIAnalysisRequest
                var aiRequest = new AIAnalysisRequest
                {
                    Id = aiRequestId,
                    UploadedBy = currentUserId,
                    FileUrl = fileUrl,
                    MediaType = mediaType,
                    AnalysisType = request.AnalysisType,
                    Notes = request.Notes,
                    Status = AIAnalysisStatus.Pending,
                    BatchId = batchId,
                    CreatedBy = currentUserId
                };
                await _aiRequestRepository.AddAsync(aiRequest);

                requestIds.Add(aiRequestId);
                acceptedFiles++;

                itemsToPublish.Add((aiRequest, mediaId));
            }
            catch (Exception ex)
            {
                rejectedFiles++;
                _logger.LogError(ex, "Error processing file '{FileName}' in batch upload.", fileDto.FileName);
            }
        }

        // Save everything to DB
        if (acceptedFiles > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish RabbitMQ messages for each successfully saved media item
            foreach (var (aiRequest, mediaId) in itemsToPublish)
            {
                _logger.LogInformation(
                    "Created mission AI analysis request in batch: BatchId={BatchId}, RequestId={RequestId}, MediaId={MediaId}, MissionId={MissionId}",
                    batchId, aiRequest.Id, mediaId, request.MissionId);

                await _eventPublisher.PublishAsync(new AIAnalysisRequestedEvent
                {
                    RequestId = aiRequest.Id,
                    FileUrl = aiRequest.FileUrl,
                    MediaType = aiRequest.MediaType,
                    AnalysisType = aiRequest.AnalysisType.ToString(),
                    Notes = aiRequest.Notes,
                    UploadedBy = currentUserId,
                    RequestedAt = aiRequest.CreatedAt,
                    MediaId = mediaId,
                    MissionId = request.MissionId,
                    AssetId = null,
                    PreferredModel = request.PreferredModel
                });
            }
        }

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
