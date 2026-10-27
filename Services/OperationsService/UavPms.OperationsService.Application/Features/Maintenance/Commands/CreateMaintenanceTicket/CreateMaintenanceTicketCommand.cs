using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;

public record CreateMaintenanceTicketCommand(
    Guid AnomalyId,
    Guid TechnicianId,
    string Priority,
    string Description,
    DateTime? DueDate = null
) : IRequest<MaintenanceTicketDto>;
