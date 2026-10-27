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

namespace UavPms.OperationsService.Application.Features.FieldExecution.Commands.ReturnDroneHandover;

public class ReturnDroneHandoverCommandHandler : IRequestHandler<ReturnDroneHandoverCommand, DroneHandoverResponseDto>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IUavRepository _uavRepository;
    private readonly IGenericRepository<DroneHandover> _handoverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnDroneHandoverCommandHandler(
        IMissionRepository missionRepository,
        IUavRepository uavRepository,
        IGenericRepository<DroneHandover> handoverRepository,
        IUnitOfWork unitOfWork)
    {
        _missionRepository = missionRepository;
        _uavRepository = uavRepository;
        _handoverRepository = handoverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DroneHandoverResponseDto> Handle(ReturnDroneHandoverCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId);
        if (mission == null)
            throw new NotFoundException("Mission", request.MissionId);

        var handovers = await _handoverRepository.FindAsync(
            h => h.MissionId == request.MissionId &&
                 h.DroneId == request.DroneId &&
                 h.ReturnedAt == null &&
                 h.Status == DroneHandoverStatus.Accepted,
            track: true);

        var handover = handovers.FirstOrDefault();
        if (handover == null)
            throw new NotFoundException("Active drone handover not found for this mission.");

        handover.ReturnedAt = DateTime.UtcNow;
        handover.ReturnChecklist = !string.IsNullOrWhiteSpace(request.ReturnChecklistJson) ? request.ReturnChecklistJson : "{}";
        if (!string.IsNullOrWhiteSpace(request.Condition))
        {
            handover.Condition = string.IsNullOrWhiteSpace(handover.Condition)
                ? $"Return: {request.Condition}"
                : $"{handover.Condition} | Return: {request.Condition}";
        }

        var drone = await _uavRepository.GetByIdAsync(request.DroneId);
        if (drone != null)
        {
            drone.OperationalStatus = DroneOperationalStatus.Available;
            drone.FlightStatus = DroneStatus.Idle;
            drone.UpdatedAt = DateTime.UtcNow;
            await _uavRepository.UpdateAsync(drone);
        }

        await _handoverRepository.UpdateAsync(handover);
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
