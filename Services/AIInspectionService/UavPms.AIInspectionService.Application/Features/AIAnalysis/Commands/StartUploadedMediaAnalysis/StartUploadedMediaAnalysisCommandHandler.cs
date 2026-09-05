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
    private readonly IGenericRepository<AIAnalysisRequest> _requestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<StartUploadedMediaAnalysisCommandHandler> _logger;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;

    [ActivatorUtilitiesConstructor]
    public StartUploadedMediaAnalysisCommandHandler(
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> requestRepository,
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
        IGenericRepository<AIAnalysisRequest> requestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher publisher,
        ILogger<StartUploadedMediaAnalysisCommandHandler> logger)
        : this(mediaRepository, requestRepository, unitOfWork, publisher, null!, logger) { }

    public async Task<Guid> Handle(StartUploadedMediaAnalysisCommand command, CancellationToken cancellationToken)
    {
        var upload = command.Upload;
        var media = await _mediaRepository.GetByIdAsync(upload.MediaId, false, cancellationToken)
            ?? throw new NotFoundException(nameof(InspectionMedia), upload.MediaId);

        if (media.MissionId != upload.MissionId || media.AssetId != upload.AssetId ||
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

        var request = prior;
        if (request == null)
        {
            var activeDuplicate = (await _requestRepository.FindAsync(item =>
                item.MediaId == media.Id && item.AnalysisType == analysisType &&
                item.ModelName == model &&
                (item.Status == AIAnalysisStatus.Pending || item.Status == AIAnalysisStatus.Processing), false)).FirstOrDefault();
            if (activeDuplicate != null)
                return activeDuplicate.Id;

            request = new AIAnalysisRequest
            {
                Id = Guid.NewGuid(),
                SourceEventId = upload.EventId,
                UploadedBy = upload.UploadedBy,
                MediaId = media.Id,
                MissionId = media.MissionId,
                AssetId = media.AssetId,
                FileUrl = media.FileUrl,
                MediaType = media.MediaType,
                AnalysisType = analysisType,
                ModelName = model,
                Status = AIAnalysisStatus.Pending,
                CreatedBy = upload.UploadedBy
            };
            await _requestRepository.AddAsync(request);
            if (_outboxRepository != null)
            {
                var workerEvent = ToWorkerEvent(request, media.AssetId!.Value);
                await _outboxRepository.AddAsync(new OutboxMessage
                {
                    Id = Guid.NewGuid(), MessageType = nameof(AIAnalysisRequestedEvent),
                    Payload = JsonSerializer.Serialize(workerEvent), OccurredAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow, CreatedBy = upload.UploadedBy
                });
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (_outboxRepository == null)
            await _publisher.PublishAsync(ToWorkerEvent(request, media.AssetId!.Value));
        _logger.LogInformation("Queued authoritative inspection media {MediaId} as AI request {RequestId}", media.Id, request.Id);
        return request.Id;
    }

    private static AIAnalysisRequestedEvent ToWorkerEvent(AIAnalysisRequest request, Guid assetId) => new()
    {
        RequestId = request.Id,
        FileUrl = request.FileUrl,
        MediaType = request.MediaType,
        AnalysisType = request.AnalysisType.ToString(),
        UploadedBy = request.UploadedBy,
        RequestedAt = request.CreatedAt,
        MediaId = request.MediaId,
        MissionId = request.MissionId,
        AssetId = assetId,
        PreferredModel = request.ModelName
    };
}
