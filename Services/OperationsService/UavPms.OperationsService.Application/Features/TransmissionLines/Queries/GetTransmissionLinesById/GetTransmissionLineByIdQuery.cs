using MediatR;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLinesById;

public record GetTransmissionLineByIdQuery(Guid Id) : IRequest<TransmissionLineDto>;