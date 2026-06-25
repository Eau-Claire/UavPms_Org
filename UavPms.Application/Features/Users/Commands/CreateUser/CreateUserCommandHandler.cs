using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IGenericRepository<Role> roleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork
    )
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var existingEmail = await _userRepository.GetByEmailWithRolesAsync(request.Email);
        if (existingEmail != null)
        {
            throw new ArgumentException("Email already exists.");
        }

        var existingUsername = await _userRepository.GetByUsernameWithRolesAsync(request.Username);
        if(existingUsername != null)
        {
            throw new ArgumentException("Username already exists");
        }

        var rolesInDb = await _roleRepository.FindAsync(r => request.Roles.Contains(r.RoleName));
        
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            Phone = request.Phone,
            CreatedAt = DateTime.Now,
            Status = "Active"
        };

        user.UserRoles = rolesInDb.Select(role => new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            AssignedAt = DateTime.Now
        }).ToList();

        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}