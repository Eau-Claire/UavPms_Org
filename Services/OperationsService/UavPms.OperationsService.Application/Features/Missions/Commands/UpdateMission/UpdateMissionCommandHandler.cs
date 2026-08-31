using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.UpdateMission;

public class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRegionAssignmentRepository _assignments;
    private readonly ICurrentUserServices _currentUser;

    public UpdateMissionCommandHandler(
        IMissionRepository missionRepository,
        IUserRepository userRepository,
        IUavRepository uavRepository,
        IUnitOfWork unitOfWork,
        IUserRegionAssignmentRepository assignments,
        ICurrentUserServices currentUser)
    {
        _missionRepository = missionRepository;
        _userRepository = userRepository;
        _uavRepository = uavRepository;
        _unitOfWork = unitOfWork;
        _assignments = assignments;
        _currentUser = currentUser;
    }
    
    public async Task<MissionDto> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        var misison = await _missionRepository.GetByIdAsync(request.Id);
        if (misison == null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        if (_currentUser.Roles?.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase) != true &&
            !await _assignments.ExistsAsync(_currentUser.UserId, misison.RegionId, cancellationToken))
            throw new BusinessRuleException("Requesting user is not assigned to the mission Region.");

        var inspector = await _userRepository.GetByIdAsync(request.InspectorId);
        if (inspector == null)
        {
            throw new NotFoundException("User", request.InspectorId);
        }

        if (!await _assignments.ExistsAsync(request.InspectorId, misison.RegionId, cancellationToken))
            throw new BusinessRuleException("Inspector is not assigned to the mission Region.");

        var uav = await _uavRepository.GetByIdAsync(request.UavId);
        if (uav == null)
        {
            throw new NotFoundException("Uav", request.UavId);
        }
        
        misison.Title = request.Title;
        misison.InspectorId = request.InspectorId;
        misison.UavId = uav.Id;
        misison.ScheduledStartAt = request.ScheduledStartAt;
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
            RegionId = misison.RegionId,
            InspectorId = misison.InspectorId,
            InspectorEmail = inspector.Email,
            UavId = misison.UavId,
            TargetTowerIds = (misison.MissionTargets ?? new List<MissionTarget>()).OrderBy(x => x.Sequence).Select(x => x.TowerId).ToArray(),
            Status = misison.Status.ToString(),
            Description = misison.Description,
            ManagerId = misison.ManagerId,
            ManagerEmail = manager?.Email ?? string.Empty,
            ScheduledStartAt = misison.ScheduledStartAt,
            StartedAt = misison.StartedAt,
            EndedAt = misison.EndedAt,
            CreatedAt = misison.CreatedAt,
            UpdatedAt = misison.UpdatedAt
        };
    }
}
