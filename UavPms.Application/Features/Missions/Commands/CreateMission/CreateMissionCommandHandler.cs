using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Missions.DTOs;
using UavPms.Core.Contracts;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Missions.Commands.CreateMission;

public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;
    private readonly IEventPublisher _eventPublisher;

    public CreateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUserServices,
        IEventPublisher eventPublisher)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _unitOfWork = unitOfWork;
        _currentUserServices = currentUserServices;
        _eventPublisher = eventPublisher;
    }
    
    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        var assignedUser = await _userRepository.GetByIdAsync(request.AssignedToUserId);
        if (assignedUser == null)
        {
            throw new NotFoundException("User",  request.AssignedToUserId);
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
                CreatedAt = DateTime.UtcNow,
            };
            await _uavRepository.AddAsync(uav);
        }

        var missionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = missionCode,
            Title = request.Title,
            RouteData = request.RouteData,
            AssignedToUserId = request.AssignedToUserId,
            DroneCode = request.DroneCode,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Pending" : request.Status,
            Description = request.Description ?? string.Empty,
            ManagerId = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : Guid.Empty,
            InspectorId = request.AssignedToUserId,
            UavId = uav.Id,
            CreatedAt = DateTime.UtcNow,
        };
        
        await _missionRepository.AddAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdEvent = new MissionCreatedEvent
        {
            MissionId = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = mission.RouteData,
            AssignedToUserId = mission.AssignedToUserId,
            ManagerId = mission.ManagerId,
            DroneCode = mission.DroneCode,
            Status = mission.Status,
            Description = mission.Description,
            CreatedAt = mission.CreatedAt,
        };

        await _eventPublisher.PublishAsync(createdEvent);

        return new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = mission.RouteData,
            AssignedToUserId = mission.AssignedToUserId,
            AssignedToUsername = assignedUser.Username,
            DroneCode = mission.DroneCode,
            Status = mission.Status,
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerUsername = _currentUserServices.UserName ?? string.Empty,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt,
        };
    }
}