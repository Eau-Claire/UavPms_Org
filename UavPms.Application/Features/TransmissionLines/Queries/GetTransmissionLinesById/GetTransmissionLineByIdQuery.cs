using MediatR;
using UavPms.Application.Features.TransmissionLines.DTOs;

namespace UavPms.Application.Features.TransmissionLines.Queries.GetTransmissionLinesById;

public record GetTransmissionLineByIdQuery(Guid Id) : IRequest<TransmissionLineDto>;