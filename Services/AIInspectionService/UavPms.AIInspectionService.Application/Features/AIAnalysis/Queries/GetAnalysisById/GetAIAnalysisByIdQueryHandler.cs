using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.AIInspectionService.Application.Common.Exceptions;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// Handler lấy chi tiết kết quả phân tích AI theo ID.
/// </summary>
public class GetAIAnalysisByIdQueryHandler
    : IRequestHandler<GetAIAnalysisByIdQuery, AIAnalysisDetailResult>
{
    private readonly IGenericRepository<AIAnalysisRequest> _repository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IGenericRepository<Mission> _missionRepository;

    public GetAIAnalysisByIdQueryHandler(
        IGenericRepository<AIAnalysisRequest> repository,
        IGenericRepository<Mission> missionRepository,
        ICurrentUserServices currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
        _missionRepository = missionRepository;
    }

    public async Task<AIAnalysisDetailResult> Handle(
        GetAIAnalysisByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, track: false, cancellationToken);
        if (entity == null)
        {
            throw new KeyNotFoundException($"AI analysis request with ID '{request.Id}' was not found.");
        }

        if (entity.MissionId.HasValue)
        {
            var mission = await _missionRepository.GetByIdAsync(entity.MissionId.Value, false, cancellationToken)
                ?? throw new KeyNotFoundException($"Mission with ID '{entity.MissionId}' was not found.");
            var roles = _currentUser.Roles ?? Array.Empty<string>();
            if (roles.Contains(UserRoles.Inspector) &&
                (entity.UploadedBy != _currentUser.UserId || mission.InspectorId != _currentUser.UserId))
                throw new ForbiddenException("You may only view AI analyses for missions assigned to you.");
            if (roles.Contains(UserRoles.Manager) && mission.ManagerId != _currentUser.UserId)
                throw new ForbiddenException("You may only view AI analyses for missions you manage.");
        }

        return new AIAnalysisDetailResult
        {
            Id = entity.Id,
            UploadedBy = entity.UploadedBy,
            MediaId = entity.MediaId,
            MissionId = entity.MissionId,
            AssetId = entity.AssetId,
            ModelName = entity.ModelName,
            FileUrl = entity.FileUrl,
            MediaType = entity.MediaType,
            AnalysisType = entity.AnalysisType,
            Notes = entity.Notes,
            Status = entity.Status,
            Result = entity.Result,
            CreatedAt = entity.CreatedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
