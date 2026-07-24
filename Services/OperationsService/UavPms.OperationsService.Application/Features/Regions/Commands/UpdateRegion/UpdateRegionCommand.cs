using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Regions.DTOs;

namespace UavPms.OperationsService.Application.Features.Regions.Commands.UpdateRegion;

public record UpdateRegionCommand(Guid Id, string RegionName) : IRequest<RegionDto>;
