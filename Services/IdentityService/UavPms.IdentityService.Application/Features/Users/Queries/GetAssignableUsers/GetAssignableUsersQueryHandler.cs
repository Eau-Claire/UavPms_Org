using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.IdentityService.Application.Features.Users.DTOs;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetAssignableUsers;

public class GetAssignableUsersQueryHandler : IRequestHandler<GetAssignableUsersQuery, List<AssignableUserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetAssignableUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    
    }

    public async Task<List<AssignableUserDto>> Handle(GetAssignableUsersQuery request, CancellationToken cancellationToken)
    {
        var inspectors = await _userRepository.GetUsersByRoleAsync("Inspector");

        var result = inspectors
            .Where(u => u.IsActive())
            .Select(u => new AssignableUserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email
            })
            .ToList();
            
        return result;
    }
}
