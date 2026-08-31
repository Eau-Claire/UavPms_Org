using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;

public class DeleteMissionCommandHandler : IRequestHandler<DeleteMissionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRegionAssignmentRepository _assignments;
    private readonly ICurrentUserServices _currentUser;

    public DeleteMissionCommandHandler(IUnitOfWork unitOfWork, IMissionRepository missionRepository, IUserRegionAssignmentRepository assignments, ICurrentUserServices currentUser)
    {
        _unitOfWork = unitOfWork;
        _missionRepository = missionRepository;
        _assignments = assignments;
        _currentUser = currentUser;
    }
    
    public async Task Handle(DeleteMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id);
        if (mission == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }
        if (_currentUser.Roles?.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) != true &&
            !await _assignments.ExistsAsync(_currentUser.UserId, mission.RegionId, cancellationToken))
            throw new BusinessRuleException("Requesting user is not assigned to the mission Region.");
        
        await _missionRepository.DeleteAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
