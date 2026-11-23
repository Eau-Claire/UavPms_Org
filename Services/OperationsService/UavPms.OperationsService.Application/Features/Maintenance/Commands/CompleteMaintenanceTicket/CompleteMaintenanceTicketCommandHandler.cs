using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CompleteMaintenanceTicket;

public class CompleteMaintenanceTicketCommandHandler : IRequestHandler<CompleteMaintenanceTicketCommand, MaintenanceTicketDto>
{
    private readonly IGenericRepository<MaintenanceTicket> _ticketRepository;
    private readonly IGenericRepository<MaintenanceProof> _proofRepository;
    private readonly IGenericRepository<DetectedAnomaly>? _anomalyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteMaintenanceTicketCommandHandler(
        IGenericRepository<MaintenanceTicket> ticketRepository,
        IGenericRepository<MaintenanceProof> proofRepository,
        IUnitOfWork unitOfWork,
        IGenericRepository<DetectedAnomaly>? anomalyRepository = null)
    {
        _ticketRepository = ticketRepository;
        _proofRepository = proofRepository;
        _unitOfWork = unitOfWork;
        _anomalyRepository = anomalyRepository;
    }

    public async Task<MaintenanceTicketDto> Handle(CompleteMaintenanceTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket == null)
            throw new NotFoundException("MaintenanceTicket", request.TicketId);

        if (ticket.Status == TicketStatus.Closed)
            throw new BusinessRuleException("TICKET_ALREADY_CLOSED", "Maintenance ticket is already closed.");

        if (ticket.Status != TicketStatus.PendingVerification)
            throw new BusinessRuleException("TICKET_NOT_PENDING_VERIFICATION", $"Cannot close maintenance ticket from status '{ticket.Status}'. Must be in PendingVerification.");

        // Verification check: ensure repair proof photo exists
        var proofs = await _proofRepository.FindAsync(p => p.TicketId == request.TicketId, track: false);
        if (proofs.Count == 0)
        {
            throw new BusinessRuleException("PROOF_REQUIRED", "Cannot complete maintenance ticket without repair proof photo.");
        }

        var now = DateTime.UtcNow;
        ticket.Status = TicketStatus.Closed;
        ticket.ResolvedAt = now;
        ticket.UpdatedAt = now;

        await _ticketRepository.UpdateAsync(ticket);

        if (_anomalyRepository != null && ticket.AnomalyId != Guid.Empty)
        {
            var anomaly = await _anomalyRepository.GetByIdAsync(ticket.AnomalyId);
            if (anomaly != null && !string.Equals(anomaly.ValidationStatus, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                anomaly.ValidationStatus = "Resolved";
                anomaly.UpdatedAt = now;
                await _anomalyRepository.UpdateAsync(anomaly);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MaintenanceTicketDto
        {
            Id = ticket.Id,
            TicketCode = ticket.TicketCode,
            AnomalyId = ticket.AnomalyId,
            ManagerId = ticket.ManagerId,
            TechnicianId = ticket.TechnicianId,
            Status = ticket.Status.ToString(),
            Priority = ticket.Priority.ToString(),
            Description = ticket.Description,
            DueDate = ticket.DueDate,
            AssignedAt = ticket.AssignedAt,
            StartedAt = ticket.StartedAt,
            ResolvedAt = ticket.ResolvedAt,
            CreatedAt = ticket.CreatedAt
        };
    }
}
