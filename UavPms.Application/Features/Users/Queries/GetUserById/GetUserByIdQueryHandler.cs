using MediatR;
using UavPms.Application.Features.Users.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    
    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    
    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(request.Id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        
        return new UserDetailDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            Roles = user.UserRoles.Select(ur => ur.Role!.RoleName).ToList()
        };
    }
}