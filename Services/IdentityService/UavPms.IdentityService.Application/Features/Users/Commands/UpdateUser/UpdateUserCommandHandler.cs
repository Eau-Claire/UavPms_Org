using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;

namespace UavPms.IdentityService.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IGenericRepository<Role> roleRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(request.Id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }
        
        var existingEmail = await _userRepository.GetByEmailWithRolesAsync(request.Email);
        if (existingEmail != null && existingEmail.Id != user.Id)
        {
            throw new ArgumentException("Email is already in use");
        }
        
        user.Email = request.Email;
        user.FullName = request.FullName;
        user.Phone = request.Phone;
        user.Status = Enum.TryParse<UavPms.IdentityService.Domain.Enums.UserStatus>(request.Status, true, out var parsedStatus) ? parsedStatus : UavPms.IdentityService.Domain.Enums.UserStatus.Active;
        
        user.UserRoles.Clear();
        var rolesInDb = await _roleRepository.FindAsync(r => request.Roles.Contains(r.RoleName));
        user.UserRoles = rolesInDb.Select(role => new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = DateTime.UtcNow
        }).ToList();
        
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return true;
    }
}