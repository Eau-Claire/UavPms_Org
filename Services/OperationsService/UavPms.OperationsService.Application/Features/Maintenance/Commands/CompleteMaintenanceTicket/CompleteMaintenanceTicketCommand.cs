using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CompleteMaintenanceTicket;

public record CompleteMaintenanceTicketCommand(
    Guid TicketId,
    string? Notes = null
) : IRequest<MaintenanceTicketDto>;
