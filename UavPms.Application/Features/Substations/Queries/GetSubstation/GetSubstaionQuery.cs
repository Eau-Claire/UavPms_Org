using MediatR;
using UavPms.Application.Features.Substations.DTOs;

namespace UavPms.Application.Features.Substations.Queries.GetSubstation;

public record GetSubstaionQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? RegionAssetId = null,
    string? SearchTerm = null
) : IRequest<PaginatedSubstationsResponse>;