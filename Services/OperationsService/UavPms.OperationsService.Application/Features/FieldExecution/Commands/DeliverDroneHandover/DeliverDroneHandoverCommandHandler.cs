using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.DeliverDroneHandover;

public class DeliverDroneHandoverCommandHandler : IRequestHandler<DeliverDroneHandoverCommand, DroneHandoverResponseDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IGenericRepository<DroneHandover> _handoverRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeliverDroneHandoverCommandHandler(
        IMissionRepository missionRepository,
        IUavRepository uavRepository,
        IGenericRepository<DroneHandover> handoverRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _missionRepository = missionRepository;
        _uavRepository = uavRepository;
        _handoverRepository = handoverRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<DroneHandoverResponseDto> Handle(DeliverDroneHandoverCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
            throw new NotFoundException("Mission", request.MissionId);

        if (mission.UavId != request.DroneId)
            throw new BusinessRuleException("DRONE_NOT_ASSIGNED", "Specified drone is not assigned to this mission.");

        var drone = await _uavRepository.GetByIdAsync(request.DroneId);
        if (drone == null)
            throw new NotFoundException("Drone", request.DroneId);

        var existingHandovers = await _handoverRepository.FindAsync(
            h => h.MissionId == request.MissionId && h.DroneId == request.DroneId && h.ReturnedAt == null,
            track: false);

        if (existingHandovers.Count > 0)
            throw new BusinessRuleException("DUPLICATE_HANDOVER", "An active handover for this drone already exists.");

        var handover = new DroneHandover
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            DroneId = request.DroneId,
            HandedOverBy = _currentUser.UserId,
            ReceivedBy = request.ReceivedBy,
            ReceivedAt = DateTime.UtcNow,
            Condition = request.Condition,
            InitialChecklist = !string.IsNullOrWhiteSpace(request.InitialChecklistJson) ? request.InitialChecklistJson : "{}",
            Status = request.Accepted ? DroneHandoverStatus.Accepted : DroneHandoverStatus.Rejected,
            CreatedAt = DateTime.UtcNow
        };

        if (request.Accepted)
        {
            drone.OperationalStatus = DroneOperationalStatus.InUse;
            drone.FlightStatus = DroneStatus.Flying;
            await _uavRepository.UpdateAsync(drone);
        }

        await _handoverRepository.AddAsync(handover);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DroneHandoverResponseDto
        {
            Id = handover.Id,
            MissionId = handover.MissionId,
            DroneId = handover.DroneId,
            HandedOverBy = handover.HandedOverBy,
            ReceivedBy = handover.ReceivedBy,
            Condition = handover.Condition,
            Status = handover.Status.ToString(),
            ReceivedAt = handover.ReceivedAt,
            ReturnedAt = handover.ReturnedAt,
            CreatedAt = handover.CreatedAt
        };
    }
}
