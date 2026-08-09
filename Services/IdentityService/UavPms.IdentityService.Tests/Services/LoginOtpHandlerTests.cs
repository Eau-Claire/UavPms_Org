using FluentAssertions;
using Moq;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Infrastructure.Services.OtpHandlers;
using Xunit;

namespace UavPms.IdentityService.Tests.Services;

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
    public async Task ValidatePreconditionAsync_ShouldReturnResolvedEmail_WhenUserFoundByEmail()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "uselessliem@gmail.com",
            Status = UserStatus.Active
        };

        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync("uselessliem@gmail.com"))
            .ReturnsAsync(user);

        var result = await _handler.ValidatePreconditionAsync("uselessliem@gmail.com", null);

        result.IsValid.Should().BeTrue();
        result.ResolvedEmail.Should().Be("uselessliem@gmail.com");
    }

    [Fact]
    public async Task ValidatePreconditionAsync_ShouldReturnFailure_WhenUserNotFound()
    {
        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync("notfound@gmail.com"))
            .ReturnsAsync((User?)null);

        var result = await _handler.ValidatePreconditionAsync("notfound@gmail.com", null);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("User not found or inactive.");
    }
}

