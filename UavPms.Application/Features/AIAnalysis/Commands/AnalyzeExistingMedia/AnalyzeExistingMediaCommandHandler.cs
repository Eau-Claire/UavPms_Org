using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.AIAnalysis.Commands.UploadForAnalysis;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.AIAnalysis.Commands.AnalyzeExistingMedia;

public class AnalyzeExistingMediaCommandHandler
    : IRequestHandler<AnalyzeExistingMediaCommand, AIAnalysisUploadResult>
{
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<AIAnalysisRequest> _aiRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<AnalyzeExistingMediaCommandHandler> _logger;

    public AnalyzeExistingMediaCommandHandler(
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<AIAnalysisRequest> aiRequestRepository,
        IUnitOfWork unitOfWork,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<AnalyzeExistingMediaCommandHandler> logger)
    {
        _missionRepository = missionRepository;
        _mediaRepository = mediaRepository;
        _aiRequestRepository = aiRequestRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AIAnalysisUploadResult> Handle(
        AnalyzeExistingMediaCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;

        // 1. Kiểm tra Mission tồn tại
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, track: false);
        if (mission == null)
        {
            throw new KeyNotFoundException($"Mission with ID '{request.MissionId}' was not found.");
        }

        // 2. Kiểm tra InspectionMedia tồn tại
        var media = await _mediaRepository.GetByIdAsync(request.MediaId, track: false);
        if (media == null)
        {
            throw new KeyNotFoundException($"InspectionMedia with ID '{request.MediaId}' was not found.");
        }

        // 3. Kiểm tra xem media có thuộc đúng mission hay không
        if (media.MissionId != request.MissionId)
        {
            throw new BusinessRuleException("The specified inspection media does not belong to the selected mission.");
        }

        // 4. Tạo bản ghi AIAnalysisRequest
        var aiRequest = new AIAnalysisRequest
        {
            Id = Guid.NewGuid(),
            UploadedBy = currentUserId,
            MediaId = media.Id,
            MissionId = request.MissionId,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            AnalysisType = request.AnalysisType,
            Notes = request.Notes,
            Status = AIAnalysisStatus.Pending,
            CreatedBy = currentUserId
        };
        await _aiRequestRepository.AddAsync(aiRequest);

        // 5. Lưu xuống DB
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created AI analysis request for existing media: RequestId={RequestId}, MediaId={MediaId}, MissionId={MissionId}",
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
            AssetId = media.AssetId,
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
