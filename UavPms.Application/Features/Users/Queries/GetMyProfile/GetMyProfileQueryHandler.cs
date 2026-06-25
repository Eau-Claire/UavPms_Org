using MediatR;
using UavPms.Application.Features.Auth.DTOs;
using UavPms.Application.Features.Users.Queries.GetMyProfile;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Users.Queries.GetMyProfile;

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, AuthUserDto>
{
    private readonly IUserRepository _userRepository;

    public GetMyProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    
    public async Task<AuthUserDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(request.UserId);
        if (user == null || user.Status == "Inactive")
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        return new AuthUserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            FullName = user.FullName,
            Roles = user.UserRoles.Select(ur => ur.Role!.RoleName).ToList(),
        };
    }
}