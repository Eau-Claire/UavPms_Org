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

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.StartUploadedMediaAnalysis;

public sealed class StartUploadedMediaAnalysisCommandHandler
    : IRequestHandler<StartUploadedMediaAnalysisCommand, Guid>
{
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRun> _requestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<StartUploadedMediaAnalysisCommandHandler> _logger;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;

    [ActivatorUtilitiesConstructor]
    public StartUploadedMediaAnalysisCommandHandler(
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRun> requestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher publisher,
        IGenericRepository<OutboxMessage> outboxRepository,
        ILogger<StartUploadedMediaAnalysisCommandHandler> logger)
    {
        _mediaRepository = mediaRepository;
        _requestRepository = requestRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public StartUploadedMediaAnalysisCommandHandler(
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRun> requestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher publisher,
        ILogger<StartUploadedMediaAnalysisCommandHandler> logger)
        : this(mediaRepository, requestRepository, unitOfWork, publisher, null!, logger) { }

    public async Task<Guid> Handle(StartUploadedMediaAnalysisCommand command, CancellationToken cancellationToken)
    {
        var upload = command.Upload;
        var media = await _mediaRepository.GetByIdAsync(upload.MediaId, false, cancellationToken)
            ?? throw new NotFoundException(nameof(InspectionMedia), upload.MediaId);

        // TowerId is nullable (corridor media); it only has to match when the event carries one.
        if (media.MissionId != upload.MissionId ||
            (upload.TowerId.HasValue && media.TowerId != upload.TowerId) ||
            media.UploadedBy != upload.UploadedBy || string.IsNullOrWhiteSpace(media.FileUrl))
        {
            throw new BusinessRuleException("Inspection media event does not match the persisted media.");
        }

        var prior = (await _requestRepository.FindAsync(
            item => item.SourceEventId == upload.EventId, track: true)).SingleOrDefault();

        var analysisType = Enum.TryParse<AnalysisType>(upload.AnalysisType, true, out var parsed)
            ? parsed
            : AnalysisType.General;
        var model = string.IsNullOrWhiteSpace(upload.PreferredModel) ? "SERVER" : upload.PreferredModel.Trim();

        var run = prior;
        if (run == null)
        {
            var activeDuplicate = (await _requestRepository.FindAsync(item =>
                item.MediaId == media.Id && item.AnalysisType == analysisType &&
                item.ModelName == model &&
                (item.Status == AIAnalysisStatus.Pending || item.Status == AIAnalysisStatus.Processing), false)).FirstOrDefault();
            if (activeDuplicate != null)
                return activeDuplicate.Id;

            run = new AIAnalysisRun
            {
                Id = Guid.NewGuid(),
                SourceEventId = upload.EventId,
                MediaId = media.Id,
                ExecutionLocation = AIAnalysisRun.Cloud,
                RequestedBy = upload.UploadedBy == Guid.Empty ? null : upload.UploadedBy,
                AnalysisType = analysisType,
                ModelName = model,
                Status = AIAnalysisStatus.Pending,
                CreatedBy = upload.UploadedBy == Guid.Empty ? null : upload.UploadedBy
            };
            await _requestRepository.AddAsync(run);
            if (_outboxRepository != null)
            {
                var workerEvent = ToWorkerEvent(run, media);
                await _outboxRepository.AddAsync(new OutboxMessage
                {
                    Id = Guid.NewGuid(), MessageType = nameof(AIAnalysisRequestedEvent),
                    Payload = JsonSerializer.Serialize(workerEvent), OccurredAt = DateTime.UtcNow
                });
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (_outboxRepository == null)
            await _publisher.PublishAsync(ToWorkerEvent(run, media));
        _logger.LogInformation("Queued authoritative inspection media {MediaId} as AI run {RunId}", media.Id, run.Id);
        return run.Id;
    }

    /// <summary>Worker payload keeps its legacy JSON shape; file/media/mission come from the media row.</summary>
    private static AIAnalysisRequestedEvent ToWorkerEvent(AIAnalysisRun run, InspectionMedia media) => new()
    {
        RequestId = run.Id,
        FileUrl = media.FileUrl,
        MediaType = media.MediaType,
        AnalysisType = run.AnalysisType.ToString(),
        UploadedBy = run.RequestedBy ?? Guid.Empty,
        RequestedAt = run.CreatedAt,
        MediaId = run.MediaId,
        MissionId = media.MissionId,
        AssetId = media.TowerId,
        TowerId = media.TowerId,
        PreferredModel = run.ModelName
    };
}
