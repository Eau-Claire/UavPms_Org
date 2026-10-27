using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.AIInspectionService.Application.Common.Security;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.AIInspectionService.Application.Common.Exceptions;

namespace UavPms.AIInspectionService.Application.Features.AIAnalysis.Queries.GetAnalysisById;

/// <summary>
/// Handler lấy chi tiết một AI analysis run theo ID.
/// </summary>
public class GetAIAnalysisByIdQueryHandler
    : IRequestHandler<GetAIAnalysisByIdQuery, AIAnalysisDetailResult>
{
    private readonly IGenericRepository<AIAnalysisRun> _repository;
    private readonly IGenericRepository<InspectionMedia> _mediaRepository;
    private readonly IGenericRepository<Mission> _missionRepository;
    private readonly IGenericRepository<MissionAssignment> _assignmentRepository;
    private readonly ICurrentUserServices _currentUser;

    public GetAIAnalysisByIdQueryHandler(
        IGenericRepository<AIAnalysisRun> repository,
        IGenericRepository<InspectionMedia> mediaRepository,
        IGenericRepository<Mission> missionRepository,
        IGenericRepository<MissionAssignment> assignmentRepository,
        ICurrentUserServices currentUser)
    {
        _repository = repository;
        _mediaRepository = mediaRepository;
        _missionRepository = missionRepository;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
    }

    public async Task<AIAnalysisDetailResult> Handle(
        GetAIAnalysisByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, track: false, cancellationToken)
            ?? throw new KeyNotFoundException($"AI analysis run with ID '{request.Id}' was not found.");

        var media = await _mediaRepository.GetByIdAsync(entity.MediaId, false, cancellationToken)
            ?? throw new KeyNotFoundException($"InspectionMedia with ID '{entity.MediaId}' was not found.");

        var mission = await _missionRepository.GetByIdAsync(media.MissionId, false, cancellationToken)
            ?? throw new KeyNotFoundException($"Mission with ID '{media.MissionId}' was not found.");
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        if (roles.Contains(UserRoles.Inspector) &&
            !await MissionAssignmentAccess.IsAssignedFieldOperatorAsync(_assignmentRepository, mission.Id, _currentUser.UserId))
            throw new ForbiddenException("You may only view AI analyses for missions assigned to you.");
        if (roles.Contains(UserRoles.Manager) && mission.ManagerId != _currentUser.UserId)
            throw new ForbiddenException("You may only view AI analyses for missions you manage.");

        return new AIAnalysisDetailResult
        {
            Id = entity.Id,
            UploadedBy = entity.RequestedBy,
            RequestedBy = entity.RequestedBy,
            MediaId = entity.MediaId,
            MissionId = media.MissionId,
            TowerId = media.TowerId,
            UavId = entity.UavId,
            ExecutionLocation = entity.ExecutionLocation,
            ModelName = entity.ModelName,
            ModelVersion = entity.ModelVersion,
            FileUrl = media.FileUrl,
            MediaType = media.MediaType,
            AnalysisType = entity.AnalysisType,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt
        };
    }
}
