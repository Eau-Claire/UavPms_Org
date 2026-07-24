using MediatR;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Commands.DeleteTransmissionLine;

public record DeleteTransmissionLineCommand(Guid Id) : IRequest;