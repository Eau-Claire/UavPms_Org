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
    private readonly IUnitOfWork _unitOfWork;

    public CompleteMaintenanceTicketCommandHandler(
        IGenericRepository<MaintenanceTicket> ticketRepository,
        IGenericRepository<MaintenanceProof> proofRepository,
        IUnitOfWork unitOfWork)
    {
        _ticketRepository = ticketRepository;
        _proofRepository = proofRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceTicketDto> Handle(CompleteMaintenanceTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
        if (ticket == null)
            throw new NotFoundException("MaintenanceTicket", request.TicketId);

        if (ticket.Status == TicketStatus.Closed)
            throw new BusinessRuleException("TICKET_ALREADY_CLOSED", "Maintenance ticket is already closed.");

        // Verification check: ensure repair proof photo exists
        var proofs = await _proofRepository.FindAsync(p => p.TicketId == request.TicketId, track: false);
        if (proofs.Count == 0)
        {
            throw new BusinessRuleException("PROOF_REQUIRED", "Cannot complete maintenance ticket without repair proof photo.");
        }

        ticket.Status = TicketStatus.Closed;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _ticketRepository.UpdateAsync(ticket);
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
