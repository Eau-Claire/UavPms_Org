using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;
using System.Text.Json;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;

public class AnalyzeExistingMediaCommandHandler
    : IRequestHandler<AnalyzeExistingMediaCommand, AIAnalysisReanalysisResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeExistingMediaCommandHandler> _logger;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;

    public AnalyzeExistingMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        IGenericRepository<OutboxMessage> outboxRepository,
        ILogger<AnalyzeExistingMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _aiRequestRepository = aiRequestRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public AnalyzeExistingMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeExistingMediaCommandHandler> logger)
        : this(missionRepository, mediaRepository, aiRequestRepository, unitOfWork, eventPublisher,
            currentUser, null!, logger) { }

    public async Task<AIAnalysisReanalysisResult> Handle(
        AnalyzeExistingMediaCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;

        // 1. Kiểm tra Mission tồn tại
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, track: false, cancellationToken);
        if (mission == null)
        {
            throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        }

        if ((_currentUser.Roles ?? Array.Empty<string>()).Contains(UavPms.Shared.Contracts.Constants.UserRoles.Manager) &&
            mission.ManagerId != currentUserId)
        {
            throw new ForbiddenException("Managers may only request reanalysis for missions they manage.");
        }

        // 2. Kiểm tra InspectionMedia tồn tại
        var media = await _mediaRepository.GetByIdAsync(request.MediaId, track: false, cancellationToken);
        if (media == null)
        {
            throw new KeyNotFoundException($"InspectionMedia with ID '{request.MediaId}' was not found.");
        }

        // 3. Kiểm tra xem media có thuộc đúng mission hay không
        if (media.MissionId != request.MissionId)
        {
            throw new BusinessRuleException("The specified inspection media does not belong to the selected mission.");
        }

        if (!media.AssetId.HasValue || media.AssetId == Guid.Empty)
        {
            throw new BusinessRuleException("Mission inspection media must be associated with an asset before reanalysis.");
        }

        // 4. Tạo bản ghi AIAnalysisRequest
        var aiRequest = new AIAnalysisRequest
        {
            Id = Guid.NewGuid(),
            UploadedBy = currentUserId,
            MediaId = media.Id,
            MissionId = request.MissionId,
            AssetId = media.AssetId,
            ModelName = string.IsNullOrWhiteSpace(request.PreferredModel) ? "SERVER" : request.PreferredModel.Trim(),
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            AnalysisType = request.AnalysisType,
            Notes = request.Notes,
            Status = AIAnalysisStatus.Pending,
            CreatedBy = currentUserId
        };
        await _aiRequestRepository.AddAsync(aiRequest);

        var workerEvent = new AIAnalysisRequestedEvent
        {
            RequestId = aiRequest.Id, FileUrl = aiRequest.FileUrl, MediaType = aiRequest.MediaType,
            AnalysisType = aiRequest.AnalysisType.ToString(), Notes = aiRequest.Notes,
            UploadedBy = currentUserId, RequestedAt = aiRequest.CreatedAt, MediaId = media.Id,
            MissionId = request.MissionId, AssetId = media.AssetId, PreferredModel = aiRequest.ModelName
        };
        if (_outboxRepository != null)
        {
            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(), MessageType = nameof(AIAnalysisRequestedEvent),
                Payload = JsonSerializer.Serialize(workerEvent), OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = currentUserId
            });
        }

        // 5. Lưu xuống DB
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created AI analysis request for existing media: RequestId={RequestId}, MediaId={MediaId}, MissionId={MissionId}",
            aiRequest.Id, media.Id, request.MissionId);

        // 6. Phát sự kiện AIAnalysisRequestedEvent lên RabbitMQ để Python consumer xử lý
        if (_outboxRepository == null)
            await _eventPublisher.PublishAsync(workerEvent);

        return new AIAnalysisReanalysisResult
        {
            Id = aiRequest.Id,
            MediaId = media.Id,
            MissionId = media.MissionId,
            AssetId = media.AssetId.Value,
            FileUrl = aiRequest.FileUrl,
            MediaType = aiRequest.MediaType,
            AnalysisType = aiRequest.AnalysisType,
            Status = aiRequest.Status,
            CreatedAt = aiRequest.CreatedAt
        };
    }
}
