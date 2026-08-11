using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using UavPms.IdentityService.Application.Common.Options;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Services;

namespace UavPms.IdentityService.Infrastructure.Services;

public class JwtProvider : IJwtProvider
{
    private readonly JwtOptions _jwtOptions;

    public JwtProvider(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateAccessToken(User user, IList<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("fullname", user.FullName),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        
        var token = new JwtSecurityToken(
        issuer: _jwtOptions.Issuer,
        audience: _jwtOptions.Audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
        signingCredentials: credentials);
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string GenerateStepUpToken(User user, string purpose)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var claims = new List<Claim>{
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("step_up_verified_at", now),
            new Claim("step_up_purpose", purpose)
        };

        var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
