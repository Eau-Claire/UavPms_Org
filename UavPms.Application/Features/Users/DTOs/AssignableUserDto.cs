using System;

namespace UavPms.Application.Features.Users.DTOs;

public class AssignableUserDto{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
}