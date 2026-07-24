using MediatR;
using UavPms.IdentityService.Application.Features.Users.DTOs;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery(int Page, int PageSize, string? Search) : IRequest<PaginatedUsersResponse>;