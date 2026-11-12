using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetMissionAiDetections;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Events;
using UavPms.Shared.Contracts.Media;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadStandaloneAnalysis;

public sealed class UploadStandaloneAnalysisCommandHandler
    : IRequestHandler<UploadStandaloneAnalysisCommand, UploadStandaloneAnalysisResult>
{
    private static readonly string[] VideoExtensions = [".mp4", ".avi", ".mov", ".webm"];

    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<Tower> _towerRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRun> _runRepository;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadStandaloneAnalysisCommandHandler> _logger;

    [ActivatorUtilitiesConstructor]
    public UploadStandaloneAnalysisCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<Tower> towerRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRun> runRepository,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork,
        ILogger<UploadStandaloneAnalysisCommandHandler> logger,
        IGenericRepository<OutboxMessage>? outboxRepository = null)
    {
        _missionRepository = missionRepository;
        _towerRepository = towerRepository;
        _mediaRepository = mediaRepository;
        _runRepository = runRepository;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _outboxRepository = outboxRepository;
    }

    public async Task<UploadStandaloneAnalysisResult> Handle(
        UploadStandaloneAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        var validFiles = (request.Files ?? Array.Empty<UploadedFileInput>())
            .Where(f => f != null && f.Stream != null && f.Length > 0 && !string.IsNullOrWhiteSpace(f.FileName))
            .ToList();

        if (validFiles.Count == 0)
        {
            throw new BusinessRuleException("FILES_REQUIRED", "At least one non-empty media file is required for AI analysis.");
        }

        if (request.TowerId.HasValue && request.TowerId.Value != Guid.Empty)
        {
            var tower = await _towerRepository.GetByIdAsync(request.TowerId.Value, false, cancellationToken);
            if (tower == null || tower.IsDeleted)
            {
                throw new NotFoundException(nameof(Tower), request.TowerId.Value);
            }
        }

        var currentUserId = _currentUser.UserId;
        var mission = await ResolveMissionAsync(request.MissionId, currentUserId, request.Notes, cancellationToken);

        var parsedAnalysisType = Enum.TryParse<AnalysisType>(request.AnalysisType, true, out var parsed)
            ? parsed
            : AnalysisType.DefectDetection;
        var modelName = string.IsNullOrWhiteSpace(request.PreferredModel)
            ? "SERVER"
            : request.PreferredModel.Trim();

        var batchId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var createdRuns = new List<UploadedMediaRunItemDto>(validFiles.Count);
        var pendingEvents = new List<AIAnalysisRequestedEvent>(validFiles.Count);

        foreach (var file in validFiles)
        {
            string? checksum = null;
            if (file.Stream.CanSeek)
            {
                file.Stream.Position = 0;
                var hashBytes = await SHA256.HashDataAsync(file.Stream, cancellationToken);
                checksum = Convert.ToHexStringLower(hashBytes);
                file.Stream.Position = 0;
            }

            InspectionMedia? media = null;
            if (!string.IsNullOrWhiteSpace(checksum))
            {
                var existingMedia = await _mediaRepository.FindAsync(m => m.Checksum == checksum && !m.IsDeleted);
                media = existingMedia?.FirstOrDefault();
            }

            if (media == null)
            {
                var metadata = MediaMetadataExtractor.Extract(file.Stream, file.ContentType, file.FileName);
                var fileUrl = await _fileStorageService.SaveImageAsync(file.Stream, file.FileName);
                var mediaType = DetermineMediaType(file.ContentType, file.FileName);

                media = new InspectionMedia
                {
                    Id = Guid.NewGuid(),
                    MissionId = mission.Id,
                    TowerId = request.TowerId,
                    UploadedBy = currentUserId == Guid.Empty ? null : currentUserId,
                    MediaType = mediaType,
                    FileUrl = fileUrl,
                    Checksum = checksum,
                    FileSizeBytes = file.Length,
                    WidthPx = metadata.WidthPx,
                    HeightPx = metadata.HeightPx,
                    DurationSeconds = metadata.DurationSeconds,
                    Fps = metadata.Fps,
                    ValidationStatus = "Pending",
                    CapturedAt = now,
                    CreatedAt = now,
                    CreatedBy = currentUserId == Guid.Empty ? null : currentUserId
                };

                await _mediaRepository.AddAsync(media);
            }

            var activeDuplicate = (await _runRepository.FindAsync(r =>
                r.MediaId == media.Id &&
                r.AnalysisType == parsedAnalysisType &&
                r.ModelName == modelName &&
                (r.Status == AIAnalysisStatus.Pending || r.Status == AIAnalysisStatus.Processing), false))?.FirstOrDefault();

            var run = activeDuplicate;
            if (run == null)
            {
                run = new AIAnalysisRun
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    MediaId = media.Id,
                    ExecutionLocation = AIAnalysisRun.Cloud,
                    RequestedBy = currentUserId == Guid.Empty ? null : currentUserId,
                    AnalysisType = parsedAnalysisType,
                    ModelName = modelName,
                    Status = AIAnalysisStatus.Pending,
                    CreatedAt = now,
                    CreatedBy = currentUserId == Guid.Empty ? null : currentUserId
                };

                await _runRepository.AddAsync(run);

                var workerEvent = new AIAnalysisRequestedEvent
                {
                    RequestId = run.Id,
                    FileUrl = media.FileUrl,
                    MediaType = media.MediaType,
                    AnalysisType = run.AnalysisType.ToString(),
                    Notes = request.Notes,
                    UploadedBy = currentUserId,
                    RequestedAt = now,
                    MediaId = media.Id,
                    MissionId = media.MissionId,
                    AssetId = media.TowerId,
                    TowerId = media.TowerId,
                    PreferredModel = run.ModelName
                };

                if (_outboxRepository != null)
                {
                    await _outboxRepository.AddAsync(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        MessageType = nameof(AIAnalysisRequestedEvent),
                        Payload = JsonSerializer.Serialize(workerEvent),
                        OccurredAt = now
                    });
                }
                else
                {
                    pendingEvents.Add(workerEvent);
                }
            }

            createdRuns.Add(new UploadedMediaRunItemDto
            {
                RunId = run.Id,
                MediaId = media.Id,
                FileName = file.FileName,
                FileUrl = media.FileUrl,
                MediaType = media.MediaType,
                Status = run.Status.ToString()
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var evt in pendingEvents)
        {
            await _eventPublisher.PublishAsync(evt);
        }

        _logger.LogInformation(
            "Uploaded {FileCount} file(s) to MinIO and queued AI analysis batch {BatchId} under mission {MissionId}",
            createdRuns.Count, batchId, mission.Id);

        return new UploadStandaloneAnalysisResult
        {
            Id = batchId.ToString(),
            BatchId = batchId,
            MissionId = mission.Id,
            TowerId = request.TowerId,
            Status = AIAnalysisStatus.Pending.ToString(),
            AnalysisType = parsedAnalysisType.ToString(),
            ModelName = modelName,
            Notes = request.Notes ?? string.Empty,
            CreatedAt = now.ToString("o"),
            FilesCount = createdRuns.Count,
            Runs = createdRuns,
            Detections = Array.Empty<MissionAiDetectionDto>()
        };
    }

    private async Task<Mission> ResolveMissionAsync(
        Guid? requestedMissionId,
        Guid currentUserId,
        string? notes,
        CancellationToken ct)
    {
        if (requestedMissionId.HasValue && requestedMissionId.Value != Guid.Empty)
        {
            var specifiedMission = await _missionRepository.GetByIdAsync(requestedMissionId.Value, false, ct);
            if (specifiedMission == null || specifiedMission.IsDeleted)
            {
                throw new NotFoundException(nameof(Mission), requestedMissionId.Value);
            }
            return specifiedMission;
        }

        if (currentUserId != Guid.Empty)
        {
            var userMissions = await _missionRepository.FindAsync(m => m.ManagerId == currentUserId && !m.IsDeleted);
            var latestUserMission = userMissions?
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();
            if (latestUserMission != null)
            {
                return latestUserMission;
            }
        }

        var allMissions = await _missionRepository.GetAllAsync();
        var latestMission = allMissions?
            .Where(m => !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();
        if (latestMission != null)
        {
            return latestMission;
        }

        if (currentUserId == Guid.Empty)
        {
            throw new BusinessRuleException("MISSION_REQUIRED", "A valid missionId is required to upload inspection media.");
        }

        var now = DateTime.UtcNow;
        var adHocMission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = $"STD-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
            Title = string.IsNullOrWhiteSpace(notes) ? "AI Analysis Upload" : notes.Trim(),
            ManagerId = currentUserId,
            Status = "InProgress",
            MissionType = "AdHoc",
            StartedAt = now,
            CreatedAt = now,
            CreatedBy = currentUserId
        };
        await _missionRepository.AddAsync(adHocMission);
        return adHocMission;
    }

    private static string DetermineMediaType(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "Video";
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return VideoExtensions.Contains(ext) ? "Video" : "Image";
    }
}
