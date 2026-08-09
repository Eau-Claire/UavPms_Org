using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.IdentityService.Application.Features.Auth.Commands.Login;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Tests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<UavPms.IdentityService.Application.Common.Interfaces.IUserTokenService> _userTokenServiceMock;
    private readonly Mock<IOtpService> _otpServiceMock;
    private readonly Mock<IGenericRepository<TrustedDevice>> _trustedDeviceRepositoryMock;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<LoginCommandHandler>> _loggerMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _userTokenServiceMock = new Mock<UavPms.IdentityService.Application.Common.Interfaces.IUserTokenService>();
        _otpServiceMock = new Mock<IOtpService>();
        _trustedDeviceRepositoryMock = new Mock<IGenericRepository<TrustedDevice>>();
        _loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<LoginCommandHandler>>();

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _userTokenServiceMock.Object,
            _otpServiceMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _loggerMock.Object
        );
    }

    #region  Helper: Create User template with roles

    private static User CreateActivateUser(string email = "user@test.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = "Test User",
            PasswordHash = "hashed_password",
            Status = UserStatus.Active,
            IsEmailVerified = true,
            UserRoles = new List<UserRole>
            {
                new UserRole
                {
                    Role = new Role { Id = 1, RoleName = "Operator" }
                }
            }
        };
    }

    #endregion

    // Test 1: User không tồn tại -> trả về UnauthorizedAccessException
    [Fact]
    public async Task Hanlde_ShouldThrowUnauthorizedException_WhenUserDoesNotExist()
    {
        var command = new LoginCommand("nonexistuser@gmail.com", "password123", null, "UserAgent");
        _userRepositoryMock.Setup(r => 
            r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
        _passwordHasherMock.Setup(p =>
            p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        
        // act 
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        //assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().
            WithMessage("Invalid credentials");
    }

    // Test 2: User status = Inactive -> UnauthorizedAccessException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedException_WhenUserIsInactive()
    {
        // arrange
        var command = new LoginCommand("inactiveuser@gmail.com", "password123", null, "UserAgent");
        var user = new User
        {
            Email = command.Email,
            Status = UserStatus.Inactive,
            IsEmailVerified = true,
        };
        _userRepositoryMock.Setup(r => 
            r.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p =>
            p.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        
        //act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }

    // Test 3: Password sai -> UnauthorizedAccessException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedException_WhenPasswordIsIncorrect()
    {
        // arrange 
        var user = CreateActivateUser();
        var command = new LoginCommand(user.Email, "wrongpassword", null, "UserAgent");
        
        _userRepositoryMock.Setup(r =>
            r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p =>
            p.Verify(user.PasswordHash, command.Password)).Returns(false);
        
        // act 
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }
    
    // Test 4: Email chưa verify _> UnauthorizedAccessException
    [Fact]
    public async Task Handle_ShouldThrowUnauthorizedException_WhenEmailIsNotVerified()
    {
        // arrange
        var user = CreateActivateUser();
        user.IsEmailVerified = false;
        var command = new LoginCommand(user.Email, "correctPassword", null, "UserAgent");
        
        _userRepositoryMock.Setup(r =>
            r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p =>
            p.Verify(user.PasswordHash, command.Password)).Returns(true);
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Email not verified");
    }
    
    // Test 5: Login thành công KHÔNG có trusted device -> Trả về OTP required
    [Fact]
    public async Task Handle_ShouldReturnOtpRequired_WhenNoTrustedDevices()
    {
        // arrange
        var user = CreateActivateUser();
        var command = new LoginCommand(user.Email, "password123", null, "UserAgent");
        
        _userRepositoryMock.Setup(r =>
            r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p =>
            p.Verify(user.PasswordHash, command.Password)).Returns(true);
        _otpServiceMock.Setup(o =>
            o.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login, false))
            .ReturnsAsync((true, "OTP sent"));
        
        // act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // assert
        result.Should().NotBeNull();
        result.OtpRequired.Should().BeTrue();
        result.Email.Should().Be(user.Email);
        result.AccessToken.Should().BeNull();
        result.RefreshToken.Should().BeNull();
        
        // Verify interactions
        _userTokenServiceMock.Verify(s =>
            s.IssueTokensAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test 6: OTP gửi thất bại -> Throw Exception
    [Fact]
    public async Task Handle_ShouldThrowException_WhenOtpSendFails()
    {
        var user = CreateActivateUser();
        var command = new LoginCommand(user.Email, "password123", null, "UserAgent");
        
        _userRepositoryMock.Setup(r =>
            r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);
        _passwordHasherMock.Setup(p =>
            p.Verify(user.PasswordHash, command.Password)).Returns(true);
        _otpServiceMock.Setup(o =>
            o.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login, false))
            .ReturnsAsync((false, "Rate limit exceeded"));
        
        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);
        
        // assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Rate limit exceeded");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldThrowUnauthorizedAccessException_WhenUserPasswordHashIsNullOrEmpty(string? emptyPasswordHash)
    {
        // arrange
        var user = CreateActivateUser();
        user.PasswordHash = emptyPasswordHash!;
        var command = new LoginCommand(user.Email, "password123", null, "UserAgent");

        _userRepositoryMock.Setup(r => r.GetByEmailWithRolesAsync(command.Email)).ReturnsAsync(user);

        // act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid credentials");
    }
}

