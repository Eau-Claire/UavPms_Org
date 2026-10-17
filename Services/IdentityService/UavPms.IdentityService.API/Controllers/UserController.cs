using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.API.Filters;
using MediatR;
using UavPms.IdentityService.Application.Features.Users.DTOs;
using UavPms.IdentityService.Application.Features.Users.Queries.GetMyProfile;
using UavPms.IdentityService.Application.Features.Users.Queries.GetAssignableUsers;
using Asp.Versioning;
using UavPms.IdentityService.Application.Features.Users.Commands.CreateUser;
using UavPms.IdentityService.Application.Features.Users.Commands.SuspendUser;
using UavPms.IdentityService.Application.Features.Users.Commands.UpdateUser;
using UavPms.IdentityService.Application.Features.Users.Queries.GetUserById;
using UavPms.IdentityService.Application.Features.Users.Queries.GetUsers;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.IdentityService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Authorize] // Require standard JWT authentication first
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<Role> _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly ISender _mediator;

    public UserController(
        IUserRepository userRepository, 
        IGenericRepository<Role> roleRepository,
        IPasswordHasher passwordHasher, 
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        ISender mediator)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (page <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Page and PageSize must be positive integers."));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "PageSize must be less than or equal to 100."));
        }

        var result = await _mediator.Send(new GetUsersQuery(page, pageSize, search));
        return Ok(new ApiResponse(true, "Users retrieved successfully.", result));
    }

    [HttpGet("assignable")]
    [Authorize(Roles = UserRoles.AdminAndManager)]
    public async Task<IActionResult> GetAssignableUsers()
    {
        var result = await _mediator.Send(new GetAssignableUsersQuery());
        return Ok(new ApiResponse(true, "Assignable users retrieved successfully.", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        try
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return Ok(new ApiResponse(true, "User retrieved successfully.", result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse(false, ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        try
        {
            var userId = await _mediator.Send(command);
            var createdUser = new UserDetailDto
            {
                Id = userId,
                Email = command.Email,
                FullName = command.FullName,
                Phone = command.Phone,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                Roles = command.Roles ?? new List<string> { "SystemAdmin" }
            };
            return Ok(new ApiResponse(true, "User created successfully.", new { Id = userId, user = createdUser }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request)
    {
        try
        {
            var command = new UpdateUserCommand(id, request.Email, request.FullName, request.Phone, request.Status, request.Roles);
            await _mediator.Send(command);
            return Ok(new ApiResponse(true, "User updated successfully."));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse(false, e.Message));
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> PatchUser(Guid id, [FromBody] PatchUserRequestDto request)
    {
        try
        {
            var user = await _userRepository.GetByIdWithRolesAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse(false, "User not found"));
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                if (Enum.TryParse<UavPms.IdentityService.Domain.Enums.UserStatus>(request.Status, true, out var parsedStatus))
                {
                    user.Status = parsedStatus;
                }
            }

            if (!string.IsNullOrEmpty(request.FullName))
            {
                user.FullName = request.FullName;
            }

            if (!string.IsNullOrEmpty(request.Phone))
            {
                user.Phone = request.Phone;
            }

            var rolesToUpdate = request.Roles ?? (string.IsNullOrEmpty(request.Role) ? null : new List<string> { request.Role });
            if (rolesToUpdate != null && rolesToUpdate.Count > 0)
            {
                var normalizedRoles = rolesToUpdate.Select(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "SystemAdmin" : r).ToList();
                var rolesInDb = await _roleRepository.FindAsync(r => normalizedRoles.Contains(r.RoleName));
                if (rolesInDb.Count > 0)
                {
                    user.UserRoles.Clear();
                    user.UserRoles = rolesInDb.Select(role => new UavPms.IdentityService.Domain.Entities.UserRole
                    {
                        UserId = user.Id,
                        RoleId = role.Id,
                        AssignedAt = DateTime.UtcNow
                    }).ToList();
                }
            }

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            var updatedUserDto = new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Status = user.Status.ToString(),
                CreatedAt = user.CreatedAt,
                Roles = user.UserRoles.Select(ur => ur.Role?.RoleName ?? "SystemAdmin").ToList()
            };

            return Ok(new ApiResponse(true, "User updated successfully.", updatedUserDto));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            await _mediator.Send(new SuspendUserCommand(id));
            return Ok(new ApiResponse(true, "User suspended successfully"));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse(false, e.Message));
        }
    }

    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> AdminResetPassword(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id, track: true);
        if (user == null)
        {
            return NotFound(new ApiResponse(false, "User not found"));
        }

        var tempPassword = $"Pms@{Guid.NewGuid().ToString("N")[..8]}!";
        user.PasswordHash = _passwordHasher.Hash(tempPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Temporary password generated successfully.", new
        {
            username = user.Email,
            temporaryPassword = tempPassword
        }));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = UserRoles.AdminOnly)]
    public async Task<IActionResult> SuspendUser(Guid id)
    {
        try
        {
            await _mediator.Send(new SuspendUserCommand(id));
            return Ok(new ApiResponse(true, "User suspended successfully"));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse(false, e.Message));
        }
    }

    [HttpGet("me")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userIdString =  User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new ApiResponse(false, "Invalid user token."));
        }
        
        var result = await _mediator.Send(new GetMyProfileQuery(userId));
        
        return Ok(new ApiResponse(true, "Profile retrieved successfully.", result));
    }

    [HttpPost("change-password")]
    [Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new ApiResponse(false, "New password is required."));
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new ApiResponse(false, "Invalid user token."));
        }

        var user = await _userRepository.GetByIdAsync(userId, track: true);
        if (user == null)
        {
            return NotFound(new ApiResponse(false, "User not found."));
        }

        if (Request.Headers.TryGetValue("X-StepUp-Token", out var tokenValues) && !string.IsNullOrEmpty(tokenValues.FirstOrDefault()))
        {
            var savedToken = await _otpService.GetStepUpTokenAsync(userIdString, "ChangePassword");
            if (savedToken == null || savedToken != tokenValues.FirstOrDefault())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse(false, "Step-Up token has expired, been used, or is invalid."));
            }
            await _otpService.DeleteStepUpTokenAsync(userIdString, "ChangePassword");
        }
        else if (!string.IsNullOrEmpty(request.OldPassword))
        {
            if (!_passwordHasher.Verify(request.OldPassword, user.PasswordHash))
            {
                return BadRequest(new ApiResponse(false, "Mật khẩu hiện tại không chính xác."));
            }
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new ApiResponse(true, "Password changed successfully."));
    }
}

public record ChangePasswordRequest(string? OldPassword, string NewPassword);
public record UpdateUserRequestDto(
    string Email,
    string FullName,
    string Phone,
    string Status,
    List<string> Roles
);
public record PatchUserRequestDto(
    string? Email = null,
    string? FullName = null,
    string? Phone = null,
    string? Status = null,
    string? Role = null,
    List<string>? Roles = null
);
