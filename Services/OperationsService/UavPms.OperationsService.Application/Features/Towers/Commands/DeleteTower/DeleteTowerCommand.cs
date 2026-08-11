using MediatR;

namespace UavPms.OperationsService.Application.Features.Towers.Commands.DeleteTower;

public record DeleteTowerCommand(Guid Id) : IRequest;