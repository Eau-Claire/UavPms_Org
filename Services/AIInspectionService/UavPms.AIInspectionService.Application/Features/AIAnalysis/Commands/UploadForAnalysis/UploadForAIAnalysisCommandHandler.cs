using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using UavPms.AIInspectionService.Domain.Contracts;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadForAnalysis;

/// <summary>
/// Handler xử lý upload nhiều ảnh/video cho AI phân tích ad-hoc.
/// Mỗi file: Save → DB insert → Publish event.
/// Tất cả file trong 1 request chia sẻ cùng analysisType và notes.
/// </summary>
public class UploadForAIAnalysisCommandHandler
    : IRequestHandler<UploadForAIAnalysisCommand, List<AIAnalysisUploadResult>>
{
    private readonly IGenericRepository<AIAnalysisRequest> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserServices _currentUser;
    private readonly ILogger<UploadForAIAnalysisCommandHandler> _logger;

    public UploadForAIAnalysisCommandHandler(
        IGenericRepository<AIAnalysisRequest> repository,
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorageService,
        IEventPublisher eventPublisher,
        ICurrentUserServices currentUser,
        ILogger<UploadForAIAnalysisCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
        _eventPublisher = eventPublisher;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<List<AIAnalysisUploadResult>> Handle(
        UploadForAIAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId;
        var results = new List<AIAnalysisUploadResult>();
        var createdRequests = new List<AIAnalysisRequest>();

        // 1. Lưu từng file và tạo bản ghi AIAnalysisRequest
        foreach (var fileItem in request.Files)
        {
            var fileUrl = await _fileStorageService.SaveImageAsync(fileItem.FileStream, fileItem.FileName);

            var mediaType = fileItem.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                ? "Video"
                : "Image";

            var analysisRequest = new AIAnalysisRequest
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

            await _repository.AddAsync(analysisRequest);
            createdRequests.Add(analysisRequest);
        }

        // 2. Lưu tất cả bản ghi trong 1 transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AI analysis batch created: {Count} file(s), AnalysisType={AnalysisType}, UploadedBy={UserId}",
            createdRequests.Count, request.AnalysisType, currentUserId);

        // 3. Publish event cho từng file lên RabbitMQ
        foreach (var analysisRequest in createdRequests)
        {
            await _eventPublisher.PublishAsync(new AIAnalysisRequestedEvent
            {
                RequestId = analysisRequest.Id,
                FileUrl = analysisRequest.FileUrl,
                MediaType = analysisRequest.MediaType,
                AnalysisType = analysisRequest.AnalysisType.ToString(),
                Notes = analysisRequest.Notes,
                UploadedBy = currentUserId,
                RequestedAt = analysisRequest.CreatedAt
            });

            _logger.LogInformation(
                "Published AIAnalysisRequestedEvent: RequestId={RequestId}, MediaType={MediaType}",
                analysisRequest.Id, analysisRequest.MediaType);

            results.Add(new AIAnalysisUploadResult
            {
                Id = analysisRequest.Id,
                FileUrl = analysisRequest.FileUrl,
                MediaType = analysisRequest.MediaType,
                AnalysisType = analysisRequest.AnalysisType,
                Status = analysisRequest.Status,
                CreatedAt = analysisRequest.CreatedAt
            });
        }

        return results;
    }
}
