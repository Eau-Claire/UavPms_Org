using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IGenericRepository<AIAnalysisRun> _runRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeExistingMediaCommandHandler> _logger;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;

    [ActivatorUtilitiesConstructor]
    public AnalyzeExistingMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRun> runRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        IGenericRepository<OutboxMessage> outboxRepository,
        ILogger<AnalyzeExistingMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _runRepository = runRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public AnalyzeExistingMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRun> runRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeExistingMediaCommandHandler> logger)
        : this(missionRepository, mediaRepository, runRepository, unitOfWork, eventPublisher,
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

        // 4. Tạo bản ghi AIAnalysisRun (Cloud). Media không gắn tower vẫn được phân tích (TowerId = null).
        var run = new AIAnalysisRun
        {
            Id = Guid.NewGuid(),
            MediaId = media.Id,
            ExecutionLocation = AIAnalysisRun.Cloud,
            RequestedBy = currentUserId == Guid.Empty ? null : currentUserId,
            ModelName = string.IsNullOrWhiteSpace(request.PreferredModel) ? "SERVER" : request.PreferredModel.Trim(),
            AnalysisType = request.AnalysisType,
            Status = AIAnalysisStatus.Pending,
            CreatedBy = currentUserId == Guid.Empty ? null : currentUserId
        };
        await _runRepository.AddAsync(run);

        var workerEvent = new AIAnalysisRequestedEvent
        {
            RequestId = run.Id, FileUrl = media.FileUrl, MediaType = media.MediaType,
            AnalysisType = run.AnalysisType.ToString(), Notes = request.Notes,
            UploadedBy = currentUserId, RequestedAt = run.CreatedAt, MediaId = media.Id,
            MissionId = media.MissionId, AssetId = media.TowerId, TowerId = media.TowerId,
            PreferredModel = run.ModelName
        };
        if (_outboxRepository != null)
        {
            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(), MessageType = nameof(AIAnalysisRequestedEvent),
                Payload = JsonSerializer.Serialize(workerEvent), OccurredAt = DateTime.UtcNow
            });
        }

        // 5. Lưu xuống DB
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created AI analysis run for existing media: RunId={RunId}, MediaId={MediaId}, MissionId={MissionId}",
            run.Id, media.Id, request.MissionId);

        // 6. Phát sự kiện AIAnalysisRequestedEvent lên RabbitMQ để Python consumer xử lý
        if (_outboxRepository == null)
            await _eventPublisher.PublishAsync(workerEvent);

        return new AIAnalysisReanalysisResult
        {
            Id = run.Id,
            MediaId = media.Id,
            MissionId = media.MissionId,
            TowerId = media.TowerId,
            ExecutionLocation = run.ExecutionLocation,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            AnalysisType = run.AnalysisType,
            Status = run.Status,
            CreatedAt = run.CreatedAt
        };
    }
}
