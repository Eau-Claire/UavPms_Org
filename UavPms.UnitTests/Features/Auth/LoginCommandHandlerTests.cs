using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.Application.Features.Auth.Commands.Login;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;

namespace UavPms.UnitTests.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly Mock<IGenericRepository<RefreshToken>> _refreshTokenRepositoryMock;
    private readonly Mock<IOtpService> _otpServiceMock;
    private readonly Mock<IGenericRepository<TrustedDevice>> _trustedDeviceRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtProviderMock = new Mock<IJwtProvider>();
        _refreshTokenRepositoryMock = new Mock<IGenericRepository<RefreshToken>>();
        _otpServiceMock = new Mock<IOtpService>();
        _trustedDeviceRepositoryMock = new Mock<IGenericRepository<TrustedDevice>>();
        _configurationMock = new Mock<IConfiguration>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _configurationMock.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");
        
        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _jwtProviderMock.Object,
            _refreshTokenRepositoryMock.Object,
            _otpServiceMock.Object,
            _trustedDeviceRepositoryMock.Object,
            _configurationMock.Object,
            _unitOfWorkMock.Object
        );
    }

    #region  Helper: Create User template with roles

    private static User CreateActivateUser(string email = "user@test.com", string username = "testuser")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = "Test User",
            Username = username,
            PasswordHash = "hashed_password",
            Status = "Active",
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
        _userRepositoryMock.Setup(r => 
            r.GetByUsernameWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
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
            Status = "Inactive",
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
        _refreshTokenRepositoryMock.Verify(r =>
            r.AddAsync(It.IsAny<RefreshToken>()), Times.Never);
        _unitOfWorkMock.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    
    // Test 7: Login bằng Username (không phải email) cũng phải hoạt động
    [Fact]
    public async Task Handle_ShouldFindUserByUsername_WhenEmailLookupReturnsNull()
    {
        var user = CreateActivateUser();
        var command = new LoginCommand(user.Email, "password123", null, "UserAgent");
        
        // email lookup trả null, username trả usre
        _userRepositoryMock.Setup(r =>
            r.GetByUsernameWithRolesAsync(command.Email)).ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(r =>
            r.GetByUsernameWithRolesAsync(command.Email)).ReturnsAsync(user);
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
        _userRepositoryMock.Verify(r => 
            r.GetByUsernameWithRolesAsync(command.Email), Times.Once);
    }
    
    // Test 8: OTP gửi thất bại -> Throw Exception
    [Fact]
    public async Task Handle_ShouldThrowException_WhenOtpSendFails()
    {
        var user = CreateActivateUser();
        var command = new LoginCommand(user.Email, "password123", null, "UserAgent");
        
        _userRepositoryMock.Setup(r =>
            r.GetByUsernameWithRolesAsync(command.Email)).ReturnsAsync(user);
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
}