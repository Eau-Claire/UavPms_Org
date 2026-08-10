using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
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
        misison.Status = misison.Status = Enum.TryParse<MissionStatus>(request.Status, true, out var parsedStatus) ? parsedStatus : misison.Status;
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
            AssignedToEmail = assignedUser.Email,
            DroneCode = misison.DroneCode,
            Status = misison.Status.ToString(),
            Description = misison.Description,
            ManagerId = misison.ManagerId,
            ManagerEmail = manager?.Email ?? string.Empty,
            CreatedAt = misison.CreatedAt,
            UpdatedAt = misison.UpdatedAt
        };
    }
}