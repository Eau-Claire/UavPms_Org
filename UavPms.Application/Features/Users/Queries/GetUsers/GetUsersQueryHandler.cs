using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Application.Features.Users.DTOs;
using UavPms.Core.Interfaces.Repositories;
namespace UavPms.Application.Features.Users.Queries.GetUsers;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedUsersResponse>
{
    private readonly IUserRepository _userRepository;

    public GetUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PaginatedUsersResponse> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalItems) =
            await _userRepository.GetUsersPagedAsync(request.Page, request.PageSize, request.Search);

        var userDtos = users.Select(u => new UserDetailDto
        {
            Id = u.Id,
            CreatedAt = u.CreatedAt,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            Roles = u.UserRoles.Select(ur => ur.Role!.RoleName).ToList(),
            Status = u.Status,
            Username = u.Username,
        });

        var totalPages = (int)Math.Ceiling((double)totalItems / request.PageSize);
        var metaData = new PaginationMetaData(request.Page,request.PageSize, totalItems, totalPages);
        
        return new PaginatedUsersResponse(userDtos.ToList(), metaData);
    }
}