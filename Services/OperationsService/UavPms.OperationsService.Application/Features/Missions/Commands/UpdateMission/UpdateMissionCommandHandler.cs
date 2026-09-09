using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;

public class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        IUnitOfWork unitOfWork)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<MissionDto> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        var misison = await _missionRepository.GetByIdAsync(request.Id);
        if (misison == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        if (misison.Status is MissionStatus.InProgress or MissionStatus.Completed or MissionStatus.Cancelled)
            throw new BusinessRuleException("MISSION_IMMUTABLE_AFTER_START");
        
        misison.Title = request.Title;
        misison.RouteData = request.RouteData;
        // Assignment, drone and lifecycle state have dedicated MF01 actions. The
        // generic update endpoint never trusts a client-submitted status.
        misison.Description = request.Description ?? string.Empty;
        misison.UpdatedAt = DateTime.UtcNow;
        
        await _missionRepository.UpdateAsync(misison);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var manager = misison.ManagerId != Guid.Empty
            ? await _userRepository.GetByIdAsync(misison.ManagerId)
            : null;

        return new MissionDto
        {
            Id = misison.Id,
            MissionCode = misison.MissionCode,
            Title = misison.Title,
            RouteData = misison.RouteData,
            AssignedToUserId = misison.AssignedToUserId,
            AssignedToEmail = misison.Inspector?.Email ?? string.Empty,
            DroneCode = misison.Uav?.UavCode ?? string.Empty,
            Status = misison.Status.ToString(),
            Description = misison.Description,
            ManagerId = misison.ManagerId,
            ManagerEmail = manager?.Email ?? string.Empty,
            CreatedAt = misison.CreatedAt,
            UpdatedAt = misison.UpdatedAt
        };
    }
}
