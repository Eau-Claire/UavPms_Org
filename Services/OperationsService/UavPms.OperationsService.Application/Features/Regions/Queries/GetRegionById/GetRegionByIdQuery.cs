using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Regions.DTOs;

namespace UavPms.OperationsService.Application.Features.Regions.Queries.GetRegionById;

public record GetRegionByIdQuery(Guid Id) : IRequest<RegionDto>;
