using MediatR;

namespace UavPms.Application.Features.Substations.Commands.DeleteSubstation;

public record DeleteSubstationCommand(Guid Id) : IRequest;