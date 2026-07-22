using FluentAssertions;
using Moq;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Services.OtpHandlers;
using Xunit;

namespace UavPms.UnitTests.Services;

public class LoginOtpHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly LoginOtpHandler _handler;

    public LoginOtpHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new LoginOtpHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task ValidatePreconditionAsync_ShouldReturnResolvedEmail_WhenUserFoundByUsername()
    {
        // Account B: Username = uselessliem@gmail.com, Email = testing@123gmail.com
        var userB = new User
        {
            Id = Guid.NewGuid(),
            Username = "uselessliem@gmail.com",
            Email = "testing@123gmail.com",
            Status = "Active"
        };

        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r => r.GetByUsernameWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync(userB);

        var result = await _handler.ValidatePreconditionAsync("uselessliem@gmail.com", null);

        result.IsValid.Should().BeTrue();
        result.ResolvedEmail.Should().Be("testing@123gmail.com");
    }

    [Fact]
    public async Task ValidatePreconditionAsync_ShouldReturnResolvedEmail_WhenUserFoundByEmail()
    {
        var userA = new User
        {
            Id = Guid.NewGuid(),
            Username = "an3439201@gmail.com",
            Email = "uselessliem@gmail.com",
            Status = "Active"
        };

        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync(userA);

        var result = await _handler.ValidatePreconditionAsync("uselessliem@gmail.com", null);

        result.IsValid.Should().BeTrue();
        result.ResolvedEmail.Should().Be("uselessliem@gmail.com");
    }

    [Fact]
    public async Task ValidatePreconditionAsync_ShouldFail_WhenIdentifierMatchesEmailAndUsernameOfDifferentUsers()
    {
        var accountA = new User
        {
            Id = Guid.NewGuid(),
            Username = "an3439201@gmail.com",
            Email = "uselessliem@gmail.com",
            Status = "Active"
        };
        var accountB = new User
        {
            Id = Guid.NewGuid(),
            Username = "uselessliem@gmail.com",
            Email = "testing@123gmail.com",
            Status = "Active"
        };

        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync(accountA);
        _userRepositoryMock.Setup(r => r.GetByUsernameWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync(accountB);

        var result = await _handler.ValidatePreconditionAsync("uselessliem@gmail.com", null);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("Ambiguous login identifier.");
    }
}
