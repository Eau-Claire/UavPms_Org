using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;

public class DeleteMissionCommandHandler : IRequestHandler<DeleteMissionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMissionRepository _missionRepository;
    private readonly ICurrentUserServices? _currentUserServices;

    public DeleteMissionCommandHandler(
        IUnitOfWork unitOfWork,
        IMissionRepository missionRepository,
        ICurrentUserServices? currentUserServices = null)
    {
        _unitOfWork = unitOfWork;
        _missionRepository = missionRepository;
        _currentUserServices = currentUserServices;
    }
    
    public async Task Handle(DeleteMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id);
        if (mission == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        if (mission.Status != MissionStatus.Draft)
        {
            throw new BusinessRuleException("INVALID_MISSION_STATE", $"Cannot delete mission with status {mission.Status}. Only Draft missions can be deleted.");
        }

        if (_currentUserServices is { IsAuthenticated: true } && _currentUserServices.UserId != Guid.Empty)
        {
            var isGlobal = _currentUserServices.Roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase);
            var canManage = await _missionRepository.UserCanManageAsync(request.Id, _currentUserServices.UserId, isGlobal, cancellationToken);
            if (!canManage)
            {
                throw new ForbiddenException("REGION_MANAGEMENT_SCOPE_REQUIRED");
            }
        }
        
        await _missionRepository.DeleteAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}