using MediatR;

namespace UavPms.OperationsService.Application.Features.Substations.Commands.DeleteSubstation;

public record DeleteSubstationCommand(Guid Id) : IRequest;