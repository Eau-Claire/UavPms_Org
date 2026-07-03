using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Missions.Commands.DeleteMission;

public class DeleteMissionCommandHandler : IRequestHandler<DeleteMissionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMissionRepository _missionRepository;

    public DeleteMissionCommandHandler(IUnitOfWork unitOfWork, IMissionRepository missionRepository)
    {
        _unitOfWork = unitOfWork;
        _missionRepository = missionRepository;
    }
    
    public async Task Handle(DeleteMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id);
        if (mission == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }
        
        await _missionRepository.DeleteAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}