using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.WebApi.Filters;
using UavPms.Core.Contracts;
using MediatR;
using UavPms.Application.Features.Users.Queries.GetMyProfile;

using Asp.Versioning;
using UavPms.Application.Features.Users.Commands.CreateUser;
using UavPms.Application.Features.Users.Commands.SuspendUser;
using UavPms.Application.Features.Users.Commands.UpdateUser;
using UavPms.Application.Features.Users.Queries.GetUserById;
using UavPms.Application.Features.Users.Queries.GetUsers;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Authorize] // Require standard JWT authentication first
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly ISender _mediator;

    public UserController(
        IUserRepository userRepository, 
        IPasswordHasher passwordHasher, 
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        ISender mediator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _mediator = mediator;
    }

    [HttpGet("users")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        if (pageSize <= 0 || pageSize <= 0)
        {
            return BadRequest(new ApiResponse(false, "Page and PageSize must be a positive integer."));
        }

        if (pageSize > 100)
        {
            return BadRequest(new ApiResponse(false, "Page size must be less than 100 characters."));
        }

        var result = await _mediator.Send(new GetUsersQuery(page, pageSize, search));
        return Ok(new ApiResponse(true, "Users retrieved successfully.", result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        try
        {
            var userId = await _mediator.Send(command);
            return Ok(new ApiResponse(true, "User created successfully.", new {Id = userId}));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse(false, ex.Message));
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequestDto request)
    {
        try
        {
            var command = new UpdateUserCommand(id, request.Email, request.FullName, request.Phone, request.Status, request.Roles);
            await _mediator.Send(command);
            return Ok(new ApiResponse(true, "User successfully updated successfully."));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new ApiResponse(false, e.Message));
        }
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = "SystemAdmin")]
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
    [RequireStepUp("ChangePassword")] // Requires Step-Up Token with purpose "ChangePassword"
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

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate step-up token on Redis immediately (single-use)
        await _otpService.DeleteStepUpTokenAsync(userIdString, "ChangePassword");

        return Ok(new ApiResponse(true, "Password changed successfully using Step-Up authentication."));
    }
}

public record ChangePasswordRequest(string NewPassword);
public record UpdateUserRequestDto(
    string Email,
    string FullName,
    string Phone,
    string Status,
    List<string> Roles
);
