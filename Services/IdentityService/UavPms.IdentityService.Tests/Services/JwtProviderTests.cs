using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Infrastructure.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace UavPms.IdentityService.Tests.Services;

public class JwtProviderTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly JwtProvider _jwtProvider;
    private const string SecretKey = "super_secret_key_123_456_789_0123_345";
    private const string Issuer = "UavPms_Issuer";
    private const string Audience = "UavPms_Audience";

    public JwtProviderTests()
    {
        _configMock = new Mock<IConfiguration>();
        _configMock.Setup(c => c["Jwt:SecretKey"]).Returns(SecretKey);
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns(Issuer);
        _configMock.Setup(c => c["Jwt:Audience"]).Returns(Audience);
        _configMock.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");

        _jwtProvider = new JwtProvider(_configMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken_WithCorrectClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "testuseremail@gmail.com",
            FullName = "testuserfullname",
        };

        var roles = new List<string> { "Admin", "Operator" };

        var tokenString = _jwtProvider.GenerateAccessToken(user, roles);

        tokenString.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        jwtToken.Should().NotBeNull();
        jwtToken.Issuer.Should().Be(Issuer);
        jwtToken.Audiences.Should().Contain(Audience);

        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == "fullname" && c.Value == user.FullName);

        var roleClaims = jwtToken.Claims.Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);
        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        var tokenString1 = _jwtProvider.GenerateRefreshToken();
        var tokenString2 = _jwtProvider.GenerateRefreshToken();
        tokenString1.Should().NotBe(tokenString2);
        tokenString1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidToken_WithStepUpClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
        };
        var purpose = "ChangePassword";

        var tokenString = _jwtProvider.GenerateStepUpToken(user, purpose);

        tokenString.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        jwtToken.Should().NotBeNull();
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == "step_up_purpose" && c.Value == purpose);
        jwtToken.Claims.Should().Contain(c => c.Type == "step_up_verified_at");
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectExpiration()
    {
        // arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@user.com",
            FullName = "testuserfullname",
        };

        var roles = new List<string> { "Admin", "Operator" };

        // act
        var tokenString = _jwtProvider.GenerateAccessToken(user, roles);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        // assert
        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
        jwtToken.Should().NotBeNull();
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    // Test 5: Thiếu config SecretKey -> throw  InvalidOperationException
    [Fact]
    public void GenerateAccessToken_ShouldThrowException_WhenSecretKeyMissing()
    {
        // arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:SecretKey"]).Returns((string?)null);
        var provider = new JwtProvider(configMock.Object);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@user.com",
            FullName = "testuserfullname",
        };

        // act
        Action act = () => provider.GenerateAccessToken(user, new List<string> { "Admin", "Operator" });

        // assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Jwt:SecretKey is not configured.");
    }

    // test 6: SteupUp token expire in 5 minutes
    [Fact]
    public void GenerateStepUpToken_ShouldExpiredInFiveMinutes()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
        };

        var tokenString = _jwtProvider.GenerateStepUpToken(user, "ChangePassword");
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        var expectedExpiry = DateTime.UtcNow.AddMinutes(5);
        jwtToken.Should().NotBeNull();
        jwtToken.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    // test 7: Token uses HmacSha256 algorithm
    [Fact]
    public void GenerateAccessToken_ShouldUseHmacSha256()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FullName = "testuserfullname",
        };

        var tokenString = _jwtProvider.GenerateAccessToken(user, new List<string> { "Admin", "Operator" });
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        jwtToken.Header.Alg.Should().Be("HS256");
    }

    // Test 8: Refresh token is valid Base64, with suitable length 
    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidBase64String()
    {
        var token = _jwtProvider.GenerateRefreshToken();
        
        token.Should().NotBeNullOrEmpty();
        
        var bytes = Convert.FromBase64String(token);
        bytes.Length.Should().Be(32);
    }

    // Test 9 : Token is tampered -> validation fail
    [Fact]
    public void ValidateToken_ShouldFail_WhenTokenIsTampered()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FullName = "testuserfullname",
        };
        var tokenString = _jwtProvider.GenerateAccessToken(user, new List<string> { "Admin", "Operator" });
        
        // tamper: change characters in signature
        var parts = tokenString.Split('.');
        var tamperedSignature = parts[2][..^1] + (parts[2][^1] == 'A' ? 'B' : 'A');
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";
        
        var handler = new JwtSecurityTokenHandler();
        var validataionParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        
        Action act = () => handler.ValidateToken(tamperedToken, validataionParams, out _);
        act.Should().Throw<SecurityTokenException>();
    }
}