using System.Text.Json;
using MediatR;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;

[Obsolete("Direct mission creation via CreateMissionCommandHandler is deprecated. Missions must be created through PreMissionAssessment workflow.")]
public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;
    private readonly IEventPublisher _eventPublisher;
    private readonly INotificationRepository? _notificationRepository;

    public CreateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        ITowerRepository towerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUserServices,
        IEventPublisher eventPublisher,
        INotificationRepository? notificationRepository = null)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _towerRepository = towerRepository;
        _unitOfWork = unitOfWork;
        _currentUserServices = currentUserServices;
        _eventPublisher = eventPublisher;
        _notificationRepository = notificationRepository;
    }
    
    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        var inspectorId = request.InspectorId ?? request.AssignedToUserId;
        if (inspectorId == Guid.Empty)
        {
            throw new BusinessRuleException("Inspector is required");
        }

        var targetTowerIds = request.TargetTowerIds ?? Array.Empty<Guid>();
        if (targetTowerIds.Count == 0)
        {
            throw new BusinessRuleException("MISSION_TARGET_REQUIRED");
        }

        if (targetTowerIds.Count != targetTowerIds.Distinct().Count())
        {
            throw new BusinessRuleException("Duplicate target towers are not allowed.");
        }

        var assignedUser = await _userRepository.GetByIdWithRolesAsync(inspectorId);
        if (assignedUser == null)
        {
            throw new NotFoundException("User", inspectorId);
        }

        if (!assignedUser.UserRoles.Any(userRole =>
                string.Equals(userRole.Role?.RoleName, UserRoles.Inspector, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException("Inspector is not eligible for mission assignment.");
        }

        var uav = request.UavId.HasValue
            ? await _uavRepository.GetByIdAsync(request.UavId.Value)
            : await _uavRepository.GetByUavCodeAsync(request.DroneCode ?? string.Empty);
        if (uav == null)
        {
            throw new NotFoundException("Drone", request.UavId?.ToString() ?? request.DroneCode ?? string.Empty);
        }

        if (uav.FlightStatus != DroneStatus.Idle)
        {
            throw new BusinessRuleException("Drone is not available for assignment.");
        }

        var targetAssets = await _towerRepository.FindAsync(t => targetTowerIds.Contains(t.Id));
        if (targetAssets.Count != targetTowerIds.Count)
        {
            throw new NotFoundException("Tower", "TOWER_NOT_FOUND");
        }

        var targetAssetsById = targetAssets.ToDictionary(tower => tower.Id);
        var orderedTargetAssets = targetTowerIds.Select(towerId => targetAssetsById[towerId]).ToList();

        var missionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmss}";

        if (request.Priority == MissionPriority.Emergency)
        {
            if (string.IsNullOrWhiteSpace(request.EmergencyReason) || request.EmergencyReason.Trim().Length < 10)
            {
                throw new BusinessRuleException("EMERGENCY_REASON_REQUIRED", "EmergencyReason is required (min 10 characters) when Priority is EMERGENCY.");
            }
        }

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = missionCode,
            Title = request.Title,
            RouteData = request.RouteData ?? string.Empty,
            AssignedToUserId = inspectorId,
            DroneCode = uav.UavCode,
            Status = MissionStatus.Draft,
            Priority = request.Priority,
            Objective = request.Objective,
            TriggerReason = request.EmergencyReason,
            IsImmediate = request.IsImmediate,
            Description = request.Description ?? string.Empty,
            ManagerId = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : Guid.Empty,
            UavId = uav.Id,
            PlannedStart = request.ScheduledStartAt,
            CreatedAt = DateTime.UtcNow,
        };

        mission.MissionTargets = orderedTargetAssets.Select((tower, index) => new MissionTarget
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            TowerId = tower.Id,
            Sequence = index + 1,
            InspectionStatus = MissionTargetInspectionStatus.Pending
        }).ToList();
        
        mission.Assignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = inspectorId,
            AssignmentRole = UserRoles.Inspector,
            Status = MissionAssignmentStatus.Active,
            ResponseStatus = MissionAssignmentResponse.Pending,
            IsRequired = true,
            AssignedById = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : Guid.Empty,
            AssignedAt = DateTime.UtcNow
        });

        await _missionRepository.SetPriorityDefectsAsync(mission, request.PriorityDefects, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _missionRepository.AddAsync(mission);
            if (_notificationRepository != null)
            {
                var notif = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = inspectorId,
                    Title = $"[MF02 ĐIỀU PHỐI] Yêu cầu xác nhận nhiệm vụ: {mission.MissionCode}",
                    Body = $"Bạn được phân công làm {UserRoles.Inspector} trong nhiệm vụ \"{mission.Title}\".",
                    Type = "MISSION_DISPATCH",
                    ReferenceType = "MISSION",
                    ReferenceId = mission.Id,
                    SentAt = DateTime.UtcNow
                };
                await _notificationRepository.AddAsync(notif);
            }
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var createdEvent = new MissionCreatedEvent
        {
            MissionId = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            RouteData = string.Empty,
            AssignedToUserId = inspectorId,
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
            RouteData = string.Empty,
            AssignedToUserId = inspectorId,
            AssignedToEmail = assignedUser.Email,
            DroneCode = uav.UavCode,
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = _currentUserServices.Email ?? string.Empty,
            ScheduledStartAt = mission.PlannedStart,
            InspectorId = inspectorId,
            InspectorEmail = assignedUser.Email,
            UavId = uav.Id,
            Priority = mission.Priority.ToString(),
            Objective = mission.Objective.ToString(),
            PriorityDefects = mission.PriorityDefectCodes(),
            EmergencyReason = mission.TriggerReason,
            IsImmediate = mission.IsImmediate,
            Targets = mission.MissionTargets.OrderBy(x => x.Sequence).Select(target =>
            {
                var tower = orderedTargetAssets.First(a => a.Id == target.TowerId);
                return new MissionTargetDto
                {
                    TowerId = tower.Id,
                    TowerCode = tower.TowerCode,
                    Sequence = target.Sequence,
                    InspectionStatus = target.InspectionStatus.ToString(),
                    Latitude = tower.Geom?.Coordinate.Y,
                    Longitude = tower.Geom?.Coordinate.X,
                    RegionId = tower.RegionId
                };
            }).ToList(),
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt,
        };
    }
}
