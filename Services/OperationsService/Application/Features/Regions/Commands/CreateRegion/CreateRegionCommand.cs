using MediatR;
using UavPms.OperationsService.Application.Features.Regions.DTOs;

namespace UavPms.OperationsService.Application.Features.Regions.Commands.CreateRegion;

public record CreateRegionCommand(string RegionName) : IRequest<RegionDto>;
