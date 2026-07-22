using MediatR;
using UavPms.Application.Features.Regions.DTOs;

namespace UavPms.Application.Features.Regions.Commands.CreateRegion;

public record CreateRegionCommand(string RegionName) : IRequest<RegionDto>;
