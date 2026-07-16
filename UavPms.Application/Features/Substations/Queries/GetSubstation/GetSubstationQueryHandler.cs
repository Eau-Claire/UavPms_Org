using MediatR;
using Microsoft.EntityFrameworkCore;
using UavPms.Application.Features.Substations.DTOs;

namespace UavPms.Application.Features.Substations.Queries.GetSubstation;

public class GetSubstationQueryHandler : IRequestHandler<GetSubstaionQuery, PaginatedSubstationsResponse>
{
    private readonly ApplicationDbContext _context;
    public Task<PaginatedSubstationsResponse> Handle(GetSubstaionQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}