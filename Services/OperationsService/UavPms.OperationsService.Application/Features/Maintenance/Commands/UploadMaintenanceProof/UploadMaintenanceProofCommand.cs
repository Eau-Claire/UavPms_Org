using System;
using MediatR;
using Microsoft.AspNetCore.Http;
using UavPms.OperationsService.Application.Features.Maintenance.DTOs;

namespace UavPms.OperationsService.Application.Features.Maintenance.Commands.UploadMaintenanceProof;

public record UploadMaintenanceProofCommand(
    Guid TicketId,
    string ProofType,
    IFormFile File,
    string? TechnicianNotes = null
) : IRequest<MaintenanceProofDto>;
