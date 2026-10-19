using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.DispatchMission;

public class DispatchMissionCommandHandler : IRequestHandler<DispatchMissionCommand, MissionOperationResultDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IGenericRepository<DroneTechnicalInspection> _inspectionRepository;
    private readonly IGenericRepository<ResourceBooking> _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMissionRealtimeNotifier? _notifier;

    public DispatchMissionCommandHandler(
        IMissionRepository missionRepository,
        IUavRepository uavRepository,
        IGenericRepository<DroneTechnicalInspection> inspectionRepository,
        IGenericRepository<ResourceBooking> bookingRepository,
        IUnitOfWork unitOfWork,
        IMissionRealtimeNotifier? notifier = null)
    {
        _missionRepository = missionRepository;
        _uavRepository = uavRepository;
        _inspectionRepository = inspectionRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task<MissionOperationResultDto> Handle(DispatchMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
        {
            throw new NotFoundException("Mission", request.MissionId);
        }

        // 1. Mission targets prerequisite check
        if (mission.MissionTargets == null || mission.MissionTargets.Count == 0)
        {
            throw new BusinessRuleException("MISSION_TARGETS_REQUIRED", "Cannot dispatch mission without target towers/assets.");
        }

        // 2. UAV prerequisite check
        if (!mission.UavId.HasValue || mission.UavId == Guid.Empty)
        {
            throw new BusinessRuleException("MISSION_UAV_REQUIRED", "Cannot dispatch mission without an assigned UAV.");
        }

        var drone = await _uavRepository.GetByIdAsync(mission.UavId.Value);
        if (drone == null)
        {
            throw new NotFoundException("Drone", mission.UavId.Value);
        }

        var validInspections = await _inspectionRepository.FindAsync(
            i => i.DroneId == drone.Id &&
                 !i.IsDeleted &&
                 i.Status == DroneTechnicalInspectionStatus.Passed &&
                 i.ValidUntil > DateTime.UtcNow,
            track: false);

        if (validInspections.Count == 0 && drone.TechnicalHealth == TechnicalHealth.Critical)
        {
            throw new BusinessRuleException("UAV_TECHNICAL_INSPECTION_EXPIRED", "The assigned UAV does not have a valid passed technical inspection.");
        }

        // 3. Personnel acceptance prerequisite check
        var requiredAssignments = mission.Assignments.Where(a => a.IsRequired && a.Status == MissionAssignmentStatus.Active).ToList();
        var unacceptedAssignments = requiredAssignments.Where(a => a.ResponseStatus != MissionAssignmentResponse.Accepted).ToList();
        if (unacceptedAssignments.Count > 0)
        {
            var unacceptedUserIds = string.Join(", ", unacceptedAssignments.Select(a => a.UserId.ToString()));
            throw new BusinessRuleException("PERSONNEL_NOT_ACCEPTED", $"Cannot dispatch mission. Required members have not accepted assignment: {unacceptedUserIds}");
        }

        // 4. Lock active resource bookings for drone and members if not already locked
        var startAt = mission.PlannedStart ?? DateTime.UtcNow;
        var endAt = mission.PlannedEnd ?? startAt.AddHours(2);

        var existingDroneBooking = await _bookingRepository.FindAsync(
            b => b.MissionId == mission.Id && b.DroneId == drone.Id && b.Status == ResourceBookingStatus.Active,
            track: false);

        if (existingDroneBooking.Count == 0)
        {
            await _bookingRepository.AddAsync(new ResourceBooking
            {
                Id = Guid.NewGuid(),
                MissionId = mission.Id,
                DroneId = drone.Id,
                StartAt = startAt,
                EndAt = endAt,
                Status = ResourceBookingStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        }

        foreach (var assignment in requiredAssignments)
        {
            var existingUserBooking = await _bookingRepository.FindAsync(
                b => b.MissionId == mission.Id && b.UserId == assignment.UserId && b.Status == ResourceBookingStatus.Active,
                track: false);

            if (existingUserBooking.Count == 0)
            {
                await _bookingRepository.AddAsync(new ResourceBooking
                {
                    Id = Guid.NewGuid(),
                    MissionId = mission.Id,
                    UserId = assignment.UserId,
                    StartAt = startAt,
                    EndAt = endAt,
                    Status = ResourceBookingStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // 5. Transition status to Assigned
        mission.Status = MissionStatus.Assigned;
        mission.Version++;
        mission.UpdatedAt = DateTime.UtcNow;

        await _missionRepository.UpdateAsync(mission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_notifier != null)
        {
            await _notifier.NotifyAsync(new MissionLifecycleEventDto
            {
                MissionId = mission.Id.ToString(),
                Type = "MISSION_DISPATCHED",
                Status = mission.Status.ToString(),
                Reason = "Mission successfully dispatched after validating prerequisites.",
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
        }

        return new MissionOperationResultDto
        {
            Id = mission.Id,
            MissionCode = mission.MissionCode,
            Title = mission.Title,
            Status = mission.Status.ToString(),
            Priority = mission.Priority.ToString(),
            RegionId = mission.RegionId,
            InspectorId = mission.InspectorId,
            UavId = mission.UavId,
            ManagerId = mission.ManagerId,
            PlannedStart = mission.PlannedStart,
            PlannedEnd = mission.PlannedEnd,
            ConfirmationDeadline = mission.ConfirmationDeadline,
            AcceptedAt = mission.AcceptedAt,
            StartedAt = mission.StartedAt,
            EndedAt = mission.EndedAt,
            PostponedAt = mission.PostponedAt,
            ManagerInstructions = mission.ManagerInstructions,
            Version = mission.Version,
            CreatedAt = mission.CreatedAt,
            UpdatedAt = mission.UpdatedAt
        };
    }
}
