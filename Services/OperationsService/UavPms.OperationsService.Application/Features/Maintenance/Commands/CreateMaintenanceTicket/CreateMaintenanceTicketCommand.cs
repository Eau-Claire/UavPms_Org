using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.CreateMaintenanceTicket;

public record CreateMaintenanceTicketCommand(
    Guid AnomalyId,
    Guid TechnicianId,
    string Priority,
    string Description,
    Guid? TowerId = null,
    Guid? AssetId = null,
    DateTime? DueDate = null
) : IRequest<MaintenanceTicketDto>;
