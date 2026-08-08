using System;
using System.Collections.Generic;

namespace UavPms.IdentityService.Application.Features.Auth.DTOs;
public class AuthUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string? FullName { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
}

