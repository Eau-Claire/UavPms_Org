using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Missions.Commands.UpdateMission;

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

        var assignedUser = await _userRepository.GetByIdAsync(request.AssignedToUserId);
        if (assignedUser == null)
        {
            throw new NotFoundException("User", request.AssignedToUserId);
        }

        var uav = await _uavRepository.GetByUavCodeAsync(request.DroneCode);
        if (uav == null)
        {
            uav = new Uav
            {
                Id = Guid.NewGuid(),
                UavCode = request.DroneCode,
                Model = "Standard",
                Status = "Active",
                BatteryLevel = 100,
                CreatedAt = DateTime.Now,
            };
            await _uavRepository.AddAsync(uav);
        }
        
        misison.Title = request.Title;
        misison.RouteData = request.RouteData;
        misison.AssignedToUserId = request.AssignedToUserId;
        misison.InspectorId = request.AssignedToUserId;
        misison.DroneCode = request.DroneCode;
        misison.UavId = uav.Id;
        misison.Status = request.Status;
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
            AssignedToUsername = assignedUser.Username,
            DroneCode = misison.DroneCode,
            Status = misison.Status,
            Description = misison.Description,
            ManagerId = misison.ManagerId,
            ManagerUsername = manager?.Username ?? string.Empty,
            CreatedAt = misison.CreatedAt,
            UpdatedAt = misison.UpdatedAt
        };
    }
}