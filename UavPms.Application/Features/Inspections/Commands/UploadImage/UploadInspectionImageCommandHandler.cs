using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandHandler
    : IRequestHandler<UploadInspectionImageCommand, UploadInspectionImageResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Asset> _assetRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<UploadInspectionImageCommandHandler> _logger;

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Asset> assetRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _assetRepository = assetRepository;
        _mediaRepository = mediaRepository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _logger = logger;
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

        // 1b. Kiểm tra Asset tồn tại
        var asset = await _assetRepository.GetByIdAsync(request.AssetId, track: false);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset with ID '{request.AssetId}' was not found.");
        }

        // 2. Kiểm tra quyền: chỉ Inspector được giao mới có quyền upload
        var currentUserId = _currentUser.UserId;
        if (mission.InspectorId != currentUserId)
        {
            throw new UnauthorizedAccessException("You are not assigned to this mission.");
        }

        // 3. Lưu file ảnh vào hệ thống
        var fileUrl = await _fileStorageService.SaveImageAsync(request.FileStream, request.FileName);

        // 4. Xác định loại media từ content type
        var mediaType = request.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? "Video"
            : "Image";

        // 5. Tạo bản ghi InspectionMedia
        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            AssetId = request.AssetId,
            MediaType = mediaType,
            FileUrl = fileUrl,
            AiSource = string.Empty,
            ValidationStatus = "Pending",
            CapturedAt = request.CapturedAt,
            CreatedBy = currentUserId
        };

        await _mediaRepository.AddAsync(media);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Inspection image uploaded: MediaId={MediaId}, MissionId={MissionId}, Url={FileUrl}",
            media.Id, media.MissionId, media.FileUrl);

        // 6. Phát sự kiện ImageUploaded lên RabbitMQ
        await _eventPublisher.PublishAsync(new ImageUploadedEvent
        {
            MediaId = media.Id,
            MissionId = media.MissionId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            UploadedBy = currentUserId,
            UploadedAt = media.CapturedAt
        });

        return new UploadInspectionImageResult
        {
            MediaId = media.Id,
            MissionId = media.MissionId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            CapturedAt = media.CapturedAt
        };
    }
}
