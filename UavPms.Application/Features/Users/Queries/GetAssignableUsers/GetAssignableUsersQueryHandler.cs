using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.Application.Features.Users.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Users.Queries.GetAssignableUsers;

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
            .Where(u => u.Status == "Active")
            .Select(u => new AssignableUserDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email
            })
            .ToList();
            
        return result;
    }
}
