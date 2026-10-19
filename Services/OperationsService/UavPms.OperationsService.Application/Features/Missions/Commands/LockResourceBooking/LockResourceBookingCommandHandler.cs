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

namespace UavPms.OperationsService.Application.Features.Missions.Commands.LockResourceBooking;

public class LockResourceBookingCommandHandler : IRequestHandler<LockResourceBookingCommand, ResourceBookingDto>
{
    private readonly IGenericRepository<ResourceBooking> _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LockResourceBookingCommandHandler(
        IGenericRepository<ResourceBooking> bookingRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResourceBookingDto> Handle(LockResourceBookingCommand request, CancellationToken cancellationToken)
    {
        if (request.EndAt <= request.StartAt)
        {
            throw new BusinessRuleException("INVALID_BOOKING_TIME", "End time must be greater than start time.");
        }

        if (!request.UserId.HasValue && !request.DroneId.HasValue)
        {
            throw new BusinessRuleException("RESOURCE_REQUIRED", "Either UserId or DroneId must be provided for booking.");
        }

        // Anti-double-booking check: (StartAt < ExistingEndAt && EndAt > ExistingStartAt)
        if (request.UserId.HasValue)
        {
            var conflictingUserBookings = await _bookingRepository.FindAsync(
                b => b.UserId == request.UserId.Value &&
                     b.Status == ResourceBookingStatus.Active &&
                     b.MissionId != request.MissionId &&
                     request.StartAt < b.EndAt &&
                     request.EndAt > b.StartAt,
                track: false);

            if (conflictingUserBookings.Count > 0)
            {
                var conflict = conflictingUserBookings.First();
                throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                    $"User {request.UserId} has conflicting booking for mission {conflict.MissionId} ({conflict.StartAt:yyyy-MM-dd HH:mm} - {conflict.EndAt:yyyy-MM-dd HH:mm}).");
            }
        }

        if (request.DroneId.HasValue)
        {
            var conflictingDroneBookings = await _bookingRepository.FindAsync(
                b => b.DroneId == request.DroneId.Value &&
                     b.Status == ResourceBookingStatus.Active &&
                     b.MissionId != request.MissionId &&
                     request.StartAt < b.EndAt &&
                     request.EndAt > b.StartAt,
                track: false);

            if (conflictingDroneBookings.Count > 0)
            {
                var conflict = conflictingDroneBookings.First();
                throw new BusinessRuleException("RESOURCE_BOOKING_CONFLICT",
                    $"Drone {request.DroneId} has conflicting booking for mission {conflict.MissionId} ({conflict.StartAt:yyyy-MM-dd HH:mm} - {conflict.EndAt:yyyy-MM-dd HH:mm}).");
            }
        }

        var booking = new ResourceBooking
        {
            Id = Guid.NewGuid(),
            MissionId = request.MissionId,
            UserId = request.UserId,
            DroneId = request.DroneId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Status = ResourceBookingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResourceBookingDto(
            booking.Id,
            booking.MissionId,
            booking.UserId,
            booking.DroneId,
            booking.StartAt,
            booking.EndAt,
            booking.Status.ToString(),
            booking.CreatedAt
        );
    }
}
