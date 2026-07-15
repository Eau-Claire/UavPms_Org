using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeMissionMedia;

public class AnalyzeMissionMediaCommandHandler
    : IRequestHandler<AnalyzeMissionMediaCommand, AIAnalysisUploadResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Asset> _assetRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeMissionMediaCommandHandler> _logger;

    public AnalyzeMissionMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Asset> assetRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeMissionMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _assetRepository = assetRepository;
        _mediaRepository = mediaRepository;
        _aiRequestRepository = aiRequestRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AIAnalysisUploadResult> Handle(
        AnalyzeMissionMediaCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;

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

        // 2. Lưu file ảnh/video vào hệ thống
        var fileUrl = await _fileStorageService.SaveImageAsync(request.FileStream, request.FileName);

        var mediaType = request.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? "Video"
            : "Image";

        // 3. Tạo bản ghi InspectionMedia để lưu kết quả phân tích AI sau này
        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            AssetId = request.AssetId,
            MediaType = mediaType,
            FileUrl = fileUrl,
            AiSource = request.PreferredModel,
            ValidationStatus = "Pending",
            CapturedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };
        await _mediaRepository.AddAsync(media);

        // 4. Tạo bản ghi AIAnalysisRequest (ad-hoc/job request)
        var aiRequest = new AIAnalysisRequest
        {
            Id = Guid.NewGuid(),
            UploadedBy = currentUserId,
            FileUrl = fileUrl,
            MediaType = mediaType,
            AnalysisType = request.AnalysisType,
            Notes = request.Notes,
            Status = AIAnalysisStatus.Pending,
            CreatedBy = currentUserId
        };
        await _aiRequestRepository.AddAsync(aiRequest);

        // 5. Lưu xuống DB
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created mission AI analysis request: RequestId={RequestId}, MediaId={MediaId}, MissionId={MissionId}",
            aiRequest.Id, media.Id, request.MissionId);

        // 6. Phát sự kiện AIAnalysisRequestedEvent lên RabbitMQ để Python consumer xử lý
        await _eventPublisher.PublishAsync(new AIAnalysisRequestedEvent
        {
            RequestId = aiRequest.Id,
            FileUrl = aiRequest.FileUrl,
            MediaType = aiRequest.MediaType,
            AnalysisType = aiRequest.AnalysisType.ToString(),
            Notes = aiRequest.Notes,
            UploadedBy = currentUserId,
            RequestedAt = aiRequest.CreatedAt,
            MediaId = media.Id,
            MissionId = request.MissionId,
            AssetId = request.AssetId,
            PreferredModel = request.PreferredModel
        });

        return new AIAnalysisUploadResult
        {
            Id = aiRequest.Id,
            FileUrl = aiRequest.FileUrl,
            MediaType = aiRequest.MediaType,
            AnalysisType = aiRequest.AnalysisType,
            Status = aiRequest.Status,
            CreatedAt = aiRequest.CreatedAt
        };
    }
}
