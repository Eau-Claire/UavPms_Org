using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Events;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;

public class CreateMaintenanceTicketCommandHandler : IRequestHandler<CreateMaintenanceTicketCommand, MaintenanceTicketDto>
{
    private readonly IGenericRepository<MaintenanceTicket> _ticketRepository;
    private readonly ITowerRepository _towerRepository;
    private readonly IAnomalyRepository _anomalyRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IGenericRepository<OutboxMessage>? _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMaintenanceTicketCommandHandler(
        IGenericRepository<MaintenanceTicket> ticketRepository,
        ITowerRepository towerRepository,
        IAnomalyRepository anomalyRepository,
        IUserRepository userRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork,
        IGenericRepository<OutboxMessage>? outboxRepository = null)
    {
        _ticketRepository = ticketRepository;
        _towerRepository = towerRepository;
        _anomalyRepository = anomalyRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _outboxRepository = outboxRepository;
    }

    public async Task<MaintenanceTicketDto> Handle(CreateMaintenanceTicketCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new BusinessRuleException("DESCRIPTION_REQUIRED", "Description is required.");

        TicketPriority priority;
        if (string.Equals(request.Priority, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Priority, "Emergency", StringComparison.OrdinalIgnoreCase))
        {
            priority = TicketPriority.Emergency;
        }
        else if (!Enum.TryParse(request.Priority, true, out priority))
        {
            priority = TicketPriority.Medium;
        }

        var tech = await _userRepository.GetByIdAsync(request.TechnicianId);
        if (tech == null)
            throw new NotFoundException("Technician", request.TechnicianId);

        var anomaly = await _anomalyRepository.GetByIdAsync(request.AnomalyId);
        if (anomaly == null)
            throw new NotFoundException("DetectedAnomaly", request.AnomalyId);

        // Cột điện cần sửa suy ra từ bất thường (anomaly.TowerId), không lưu trên phiếu.
        Guid? towerId = anomaly.TowerId;

        var ticket = new MaintenanceTicket
        {
            Id = Guid.NewGuid(),
            TicketCode = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetInt32(1000, 10000)}",
            AnomalyId = request.AnomalyId,
            ManagerId = _currentUser.UserId != Guid.Empty ? _currentUser.UserId : Guid.NewGuid(),
            TechnicianId = request.TechnicianId,
            Status = TicketStatus.Open,
            Priority = priority,
            Description = request.Description,
            DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(7),
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _ticketRepository.AddAsync(ticket);

        if (_outboxRepository != null)
        {
            var ticketAssignedEvent = new MaintenanceTicketAssignedEvent
            {
                EventId = Guid.NewGuid(),
                TicketId = ticket.Id,
                TicketCode = ticket.TicketCode,
                Title = $"Phiếu bảo trì {ticket.TicketCode}",
                TowerId = towerId ?? Guid.Empty,
                AssignedToUserId = request.TechnicianId,
                Priority = ticket.Priority.ToString(),
                AnomalyDescription = ticket.Description,
                AssignedAt = ticket.AssignedAt ?? DateTime.UtcNow
            };

            await _outboxRepository.AddAsync(new OutboxMessage
            {
                Id = ticketAssignedEvent.EventId,
                MessageType = nameof(MaintenanceTicketAssignedEvent),
                Payload = System.Text.Json.JsonSerializer.Serialize(ticketAssignedEvent),
                OccurredAt = ticketAssignedEvent.AssignedAt
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MaintenanceTicketDto
        {
            Id = ticket.Id,
            TicketCode = ticket.TicketCode,
            AnomalyId = ticket.AnomalyId,
            TowerId = towerId,
            ManagerId = ticket.ManagerId,
            TechnicianId = ticket.TechnicianId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            Description = ticket.Description,
            DueDate = ticket.DueDate,
            AssignedAt = ticket.AssignedAt,
            CreatedAt = ticket.CreatedAt
        };
    }
}
