using MediatR;
using UavPms.Application.Features.Users.DTOs;

namespace UavPms.Application.Features.Users.Queries.GetUsers;

public record GetUsersQuery(int Page, int PageSize, string? Search) : IRequest<PaginatedUsersResponse>;