using MediatR;

namespace UavPms.Application.Features.TransmissionLines.Commands.DeleteTransmissionLine;

public record DeleteTransmissionLineCommand(Guid Id) : IRequest;