using System;
using MediatR;
using UavPms.Application.Features.Regions.DTOs;

namespace UavPms.Application.Features.Regions.Queries.GetRegionById;

public record GetRegionByIdQuery(Guid Id) : IRequest<RegionDto>;
