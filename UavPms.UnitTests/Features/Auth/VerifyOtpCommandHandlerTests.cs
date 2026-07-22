using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Auth.Commands.VerifyOtp;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using RefreshTokenEntity = UavPms.Core.Entities.RefreshToken;

namespace UavPms.UnitTests.Features.Auth;

public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IOtpService> _otpServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IGenericRepository<RefreshTokenEntity>> _refreshTokenRepositoryMock;
    private readonly Mock<IGenericRepository<TrustedDevice>> _trustedDeviceRepositoryMock;
    private readonly VerifyOtpCommandHandler _handler;
    public VerifyOtpCommandHandlerTests()
    {
        _otpServiceMock = new Mock<IOtpService>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _jwtProviderMock = new Mock<IJwtProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _configurationMock = new Mock<IConfiguration>();
        _refreshTokenRepositoryMock = new Mock<IGenericRepository<RefreshTokenEntity>>();
        _trustedDeviceRepositoryMock = new Mock<IGenericRepository<TrustedDevice>>();
        _configurationMock.Setup(c => c["Jwt:ExpiryMinutes"]).Returns("60");
        _handler = new VerifyOtpCommandHandler(
            _otpServiceMock.Object,
            _userRepositoryMock.Object,
            _jwtProviderMock.Object,
            _unitOfWorkMock.Object,
            _configurationMock.Object,
            _refreshTokenRepositoryMock.Object,
            _trustedDeviceRepositoryMock.Object
        );
    }
    
    #region Helper
    private static User CreateActiveUser(string email = "user@test.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Username = "testuser",
            FullName = "Test User",
            Status = "Active",
            IsEmailVerified = true,
            UserRoles = new List<UserRole>
            {
                new UserRole { Role = new Role { Id = 1, RoleName = "Operator" } }
            }
        };
    }
    #endregion

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenOtpIsInvalid()
    {
        var command = new VerifyOtpCommand("user@test.com", "000000", OtpPurpose.Login, "UserAgent");
        _otpServiceMock.Setup(o =>
                o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((false, "Invalid OTP code"));
        
        Func<Task> act =  async () => await _handler.Handle(command, CancellationToken.None);
        
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage($"Invalid OTP code");
    }

    [Fact]
    public async Task Handle_ShouldUseEmail_WhenLoginIdentifierMatchesEmailAndUsernameOfDifferentUsers()
    {
        var accountA = CreateActiveUser("uselessliem@gmail.com");
        accountA.Username = "an3439201@gmail.com";
        var accountB = CreateActiveUser("testing@123gmail.com");
        accountB.Username = "uselessliem@gmail.com";
        var command = new VerifyOtpCommand("uselessliem@gmail.com", "123456", OtpPurpose.Login, "UserAgent");

        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(accountA);
        _userRepositoryMock.Setup(u => u.GetByUsernameWithRolesAsync(command.Email))
            .ReturnsAsync(accountB);
        _otpServiceMock.Setup(o =>
                o.VerifyOtpAsync(accountA.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _jwtProviderMock.Setup(j =>
            j.GenerateAccessToken(accountA, It.IsAny<IList<string>>())).Returns("access-token");
        _jwtProviderMock.Setup(j =>
            j.GenerateRefreshToken()).Returns("refresh-token");
        _unitOfWorkMock.Setup(u =>
                u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AuthResult.Should().NotBeNull();
        result.AuthResult!.User.Should().NotBeNull();
        result.AuthResult.User!.Email.Should().Be("uselessliem@gmail.com");
        _otpServiceMock.Verify(o =>
            o.VerifyOtpAsync("uselessliem@gmail.com", command.Code, OtpPurpose.Login), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnAuthResult_WhenOtpValidForLogin()
    {
        var user = CreateActiveUser();
        var command = new VerifyOtpCommand(user.Email, "123456",  OtpPurpose.Login, "UserAgent");

        _otpServiceMock.Setup(o =>
                o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _userRepositoryMock.Setup(u =>
             u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        _jwtProviderMock.Setup(j =>
            j.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("access-token");
        _jwtProviderMock.Setup(j => 
            j.GenerateRefreshToken()).Returns("refresh-token");
        _unitOfWorkMock.Setup(u => 
            u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AuthResult.Should().NotBeNull();
        result.AuthResult!.AccessToken.Should().Be("access-token");
        result.AuthResult!.RefreshToken.Should().Be("refresh-token");
        result.AuthResult!.User.Should().NotBeNull();
        result.AuthResult!.DeviceTrustToken.Should().NotBeNullOrEmpty();
        
        _refreshTokenRepositoryMock.Verify(r => r.AddAsync(It.IsAny<RefreshTokenEntity>()), Times.Once);
        _trustedDeviceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<TrustedDevice>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowNotFoundException_WhenUserDoesNotExist()
    {
        var command = new VerifyOtpCommand("user@test.com", "123456", OtpPurpose.Login, "UserAgent");   
        _otpServiceMock.Setup(o =>
            o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync((User?)null);
        _userRepositoryMock.Setup(u => u.GetByUsernameWithRolesAsync(command.Email))
            .ReturnsAsync((User?)null);
        
        Func<Task> act =  async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenUserIsInactive()
    {
        var user = CreateActiveUser();
        user.Status = "Inactive";
        var command = new VerifyOtpCommand(user.Email, "123456", OtpPurpose.Login, "UserAgent");

        _otpServiceMock.Setup(o =>
                o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        
        Func<Task> act =  async () => await _handler.Handle(command, CancellationToken.None);
        
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage($"User account is not active.");
    }

    [Fact]
    public async Task Handle_ShouldActivateUser_WhenEmailVerificationPurpose()
    {
        var user = CreateActiveUser();
        user.IsEmailVerified =false;
        user.Status = "Pending";
        var command = new VerifyOtpCommand(user.Email, "123456", OtpPurpose.EmailVerification, "UserAgent");
        
        _otpServiceMock.Setup(o =>
            o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        _jwtProviderMock.Setup(j =>
            j.GenerateAccessToken(user, It.IsAny<IList<string>>()))
            .Returns("access-token");
        _jwtProviderMock.Setup(j =>
            j.GenerateRefreshToken()).Returns("refresh-token");
        _unitOfWorkMock.Setup(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        user.IsEmailVerified.Should().BeTrue();
        user.Status.Should().Be("Active");
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
        result.AuthResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnVerificationToken_WhenForgotPurposePurpose()
    {
        var command = new VerifyOtpCommand("user@test.com", "123456", OtpPurpose.ForgotPassword, "UserAgent");
        
        _otpServiceMock.Setup(o => 
            o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _otpServiceMock.Setup(o =>
            o.SaveVerificationTokenAsync(It.IsAny<string>(), command.Email, It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        result.Success.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
        result.AuthResult.Should().BeNull();

        _otpServiceMock.Verify(o =>
            o.SaveVerificationTokenAsync(It.IsAny<string>(), command.Email, TimeSpan.FromMinutes(10)), Times.Once);
    }
    
    [Fact]
    public async Task Handle_ShouldReturnsStepUpToken_WhenChangePasswordPurpose()
    {
       var user = CreateActiveUser();
       var command = new VerifyOtpCommand(user.Email, "123456", OtpPurpose.ChangePassword, "UserAgent"); 
       
       _otpServiceMock.Setup(o =>
           o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
           .ReturnsAsync((true, "OK"));
       _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email)).
           ReturnsAsync(user);
       _jwtProviderMock.Setup(j =>
           j.GenerateStepUpToken(user, "ChangePassword"))
           .Returns("step-up-token");
       _otpServiceMock.Setup(o =>
           o.SaveStepUpTokenAsync(user.Id.ToString(), "ChangePassword", "step-up-token", TimeSpan.FromMinutes(5)))
           .Returns(Task.CompletedTask);
       
       var result = await _handler.Handle(command, CancellationToken.None);
       
       result.Success.Should().BeTrue();
       result.Token.Should().Be("step-up-token");
       result.AuthResult.Should().BeNull();

       _jwtProviderMock.Verify(j =>
           j.GenerateStepUpToken(user, "ChangePassword"), Times.Once);
       _otpServiceMock.Verify(o =>
           o.SaveStepUpTokenAsync(user.Id.ToString(), "ChangePassword", "step-up-token", TimeSpan.FromMinutes(5)), Times.Once);
    }
}
