using MediatR;

namespace UavPms.Application.Features.Towers.Commands.DeleteTower;

public record DeleteTowerCommand(Guid Id) : IRequest;