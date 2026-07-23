using MediatR;
using UavPms.OperationsService.Application.Features.Substations.DTOs;

namespace UavPms.OperationsService.Application.Features.Substations.Queries.GetSubstation;

public record GetSubstaionQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? RegionAssetId = null,
    string? SearchTerm = null
) : IRequest<PaginatedSubstationsResponse>;