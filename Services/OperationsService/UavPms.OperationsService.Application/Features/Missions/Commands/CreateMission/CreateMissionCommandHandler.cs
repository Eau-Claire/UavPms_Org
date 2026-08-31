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

public class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserServices _currentUserServices;
    private readonly IEventPublisher _eventPublisher;

    public CreateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        IAssetRepository assetRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserServices currentUserServices,
        IEventPublisher eventPublisher)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _assetRepository = assetRepository;
        _unitOfWork = unitOfWork;
        _currentUserServices = currentUserServices;
        _eventPublisher = eventPublisher;
    }
    
    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        var inspectorId = request.InspectorId ?? request.AssignedToUserId;
        if (inspectorId == Guid.Empty)
        {
            throw new BusinessRuleException("Inspector is required");
        }

        var targetAssetIds = request.TargetAssetIds ?? Array.Empty<Guid>();
        if (targetAssetIds.Count == 0)
        {
            throw new BusinessRuleException("MISSION_TARGET_REQUIRED");
        }

        if (targetAssetIds.Count != targetAssetIds.Distinct().Count())
        {
            throw new BusinessRuleException("Duplicate target assets are not allowed.");
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

        if (uav.Status != DroneStatus.Idle)
        {
            throw new BusinessRuleException("Drone is not available for assignment.");
        }

        var targetAssets = await _assetRepository.GetAssetsByIdsAsync(targetAssetIds, cancellationToken);
        if (targetAssets.Count != targetAssetIds.Count)
        {
            throw new NotFoundException("Asset", "ASSET_NOT_FOUND");
        }

        if (targetAssets.Any(asset => asset.Status is not ("Active" or "Operational")))
        {
            throw new BusinessRuleException("ASSET_NOT_AVAILABLE");
        }

        if (targetAssets.Any(asset => asset.TowerId == Guid.Empty))
        {
            throw new BusinessRuleException("ASSET_TOWER_REQUIRED");
        }

        var targetAssetsById = targetAssets.ToDictionary(asset => asset.Id);
        var orderedTargetAssets = targetAssetIds.Select(assetId => targetAssetsById[assetId]).ToList();
        if (orderedTargetAssets.Select(asset => asset.TowerId).Distinct().Count() != orderedTargetAssets.Count)
        {
            throw new BusinessRuleException("Duplicate target towers are not allowed.");
        }

        var missionCode = $"MS-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = missionCode,
            Title = request.Title,
            RouteData = request.RouteData ?? string.Empty,
            AssignedToUserId = inspectorId,
            DroneCode = uav.UavCode,
            Status = Enum.TryParse<MissionStatus>(request.Status, true, out var parsedStatus) ? parsedStatus : MissionStatus.Pending,
            Description = request.Description ?? string.Empty,
            ManagerId = _currentUserServices.UserId != Guid.Empty ? _currentUserServices.UserId : Guid.Empty,
            InspectorId = inspectorId,
            UavId = uav.Id,
            ScheduledStartAt = request.ScheduledStartAt,
            CreatedAt = DateTime.UtcNow,
        };

        mission.MissionTargets = orderedTargetAssets.Select((asset, index) => new MissionTarget
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            TowerId = asset.TowerId,
            Sequence = index + 1,
            Status = "Pending"
        }).ToList();
        
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _missionRepository.AddAsync(mission);
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
            RouteData = string.Empty,
            AssignedToUserId = mission.InspectorId,
            AssignedToEmail = assignedUser.Email,
            DroneCode = uav.UavCode,
            Status = mission.Status.ToString(),
            Description = mission.Description,
            ManagerId = mission.ManagerId,
            ManagerEmail = _currentUserServices.Email ?? string.Empty,
            ScheduledStartAt = mission.ScheduledStartAt,
            InspectorId = inspectorId,
            InspectorEmail = assignedUser.Email,
            UavId = uav.Id,
            Targets = mission.MissionTargets.OrderBy(x => x.Sequence).Select(target =>
            {
                var asset = orderedTargetAssets.First(a => a.TowerId == target.TowerId);
                return new MissionTargetDto
                {
                    TowerId = target.TowerId,
                    TowerCode = asset.Tower?.TowerCode ?? string.Empty,
                    AssetId = asset.Id,
                    AssetCode = asset.AssetCode,
                    Sequence = target.Sequence,
                    InspectionStatus = target.Status,
                    Latitude = asset.Tower?.Geom?.Coordinate.Y,
                    Longitude = asset.Tower?.Geom?.Coordinate.X
                };
            }).ToList(),
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt,
        };
    }
}
