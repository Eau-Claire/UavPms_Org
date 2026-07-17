using System;
using MediatR;
using UavPms.Application.Features.Regions.DTOs;

namespace UavPms.Application.Features.Regions.Commands.UpdateRegion;

public record UpdateRegionCommand(Guid Id, string RegionName) : IRequest<RegionDto>;
