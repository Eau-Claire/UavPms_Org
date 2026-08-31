using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Domain.Contracts;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Application.Common.Utilities;

namespace UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;

public class UploadInspectionImageCommandHandler
    : IRequestHandler<UploadInspectionImageCommand, UploadInspectionImageResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Tower> _towerRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<UploadInspectionImageCommandHandler> _logger;

    public UploadInspectionImageCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Tower> towerRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<UploadInspectionImageCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
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

        // 1b. Kiểm tra AssetComponent tồn tại
        var tower = await _towerRepository.GetByIdAsync(request.TowerId, track: false);
        if (tower == null)
        {
            throw new KeyNotFoundException($"Tower with ID '{request.TowerId}' was not found.");
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
            TowerId = request.TowerId,
            MediaType = mediaType,
            FileUrl = fileUrl,
            AiSource = string.Empty,
            ValidationStatus = "Pending",
            CapturedAt = request.CapturedAt,
            CaptureLocation = request.Latitude.HasValue && request.Longitude.HasValue
                ? SpatialGeometryFactory.CreatePoint(request.Longitude.Value, request.Latitude.Value)
                : null,
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
