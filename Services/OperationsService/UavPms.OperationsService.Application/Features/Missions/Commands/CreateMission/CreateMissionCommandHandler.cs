using MediatR;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IRegionRepository _regionRepository;
    private readonly IUserRegionAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;
    private readonly IEventPublisher _eventPublisher;

    public CreateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        ITowerRepository towerRepository,
        IRegionRepository regionRepository,
        IUserRegionAssignmentRepository assignmentRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUserServices,
        IEventPublisher eventPublisher)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _towerRepository = towerRepository;
        _regionRepository = regionRepository;
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
        _currentUserServices = currentUserServices;
        _eventPublisher = eventPublisher;
    }
    
    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        if (await _regionRepository.GetByIdAsync(request.RegionId, false) == null)
            throw new NotFoundException("Region", request.RegionId);

        var inspector = await _userRepository.GetByIdAsync(request.InspectorId);
        if (inspector == null)
        {
            throw new NotFoundException("User", request.InspectorId);
        }

        var uav = await _uavRepository.GetByIdAsync(request.UavId);
        if (uav == null)
        {
            throw new NotFoundException("Uav", request.UavId);
        }

        if (request.TargetTowerIds.Count != request.TargetTowerIds.Distinct().Count())
            throw new BusinessRuleException("A tower can only be selected once per mission.");

        var managerId = _currentUserServices.UserId;
        var requiredUsers = new[] { managerId, request.InspectorId }.Where(x => x != Guid.Empty).Distinct().ToArray();
        var assignedUsers = await _assignmentRepository.GetAssignedUserIdsAsync(request.RegionId, requiredUsers, cancellationToken);
        var isSystemAdmin = _currentUserServices.Roles?.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) == true;
        if (!isSystemAdmin && !assignedUsers.Contains(managerId))
            throw new BusinessRuleException("Requesting user is not assigned to the mission Region.");
        if (!assignedUsers.Contains(request.InspectorId))
            throw new BusinessRuleException("Inspector is not assigned to the mission Region.");

        var towerRegions = await _towerRepository.GetRegionIdsByTowerIdsAsync(request.TargetTowerIds.Distinct().ToArray(), cancellationToken);
        if (towerRegions.Count != request.TargetTowerIds.Count)
            throw new BusinessRuleException("One or more target towers do not exist.");
        if (towerRegions.Values.Any(regionId => regionId != request.RegionId))
            throw new BusinessRuleException("One or more target towers are outside the mission Region.");

        var missionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = missionCode,
            Title = request.Title,
            Status = Enum.TryParse<MissionStatus>(request.Status, true, out var parsedStatus) ? parsedStatus : MissionStatus.Pending,
            Description = request.Description ?? string.Empty,
            ManagerId = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : Guid.Empty,
            RegionId = request.RegionId,
            InspectorId = request.InspectorId,
            UavId = uav.Id,
            ScheduledStartAt = request.ScheduledStartAt,
            CreatedAt = DateTime.UtcNow,
        };

        mission.MissionTargets = request.TargetTowerIds.Select((towerId, index) => new MissionTarget
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            TowerId = towerId,
            Sequence = index + 1,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        }).ToList();
        
        await _missionRepository.AddAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var createdEvent = new MissionCreatedEvent
        {
            MissionId = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = string.Empty,
            AssignedToUserId = mission.InspectorId,
            ManagerId = mission.ManagerId,
            DroneCode = uav.UavCode,
            Status = mission.Status.ToString(),
            Description = mission.Description,
            CreatedAt = mission.CreatedAt,
        };

        await _eventPublisher.PublishAsync(createdEvent);

        return new MissionDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RegionId = mission.RegionId,
            InspectorId = mission.InspectorId,
            InspectorEmail = inspector.Email,
            UavId = mission.UavId,
            TargetTowerIds = (mission.MissionTargets ?? new List<MissionTarget>()).OrderBy(x => x.Sequence).Select(x => x.TowerId).ToArray(),
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = _currentUserServices.Email ?? string.Empty,
            ScheduledStartAt = mission.ScheduledStartAt,
            StartedAt = mission.StartedAt,
            EndedAt = mission.EndedAt,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt,
        };
    }
}
