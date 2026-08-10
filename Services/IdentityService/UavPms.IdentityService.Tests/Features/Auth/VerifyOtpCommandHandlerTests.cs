using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using RefreshTokenEntity = UavPms.IdentityService.Domain.Entities.RefreshToken;


namespace UavPms.IdentityService.Tests.Features.Auth;

public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IOtpService> _otpServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.OtpVerificationStrategyResolver _strategyResolver;
    private readonly Mock<UavPms.IdentityService.Application.Common.Interfaces.IUserTokenService> _userTokenServiceMock;
    private readonly Mock<IJwtProvider> _jwtProviderMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<TrustedDevice>> _trustedDeviceRepositoryMock;
    private readonly VerifyOtpCommandHandler _handler;

    public VerifyOtpCommandHandlerTests()
    {
        _otpServiceMock = new Mock<IOtpService>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _userTokenServiceMock = new Mock<UavPms.IdentityService.Application.Common.Interfaces.IUserTokenService>();
        _jwtProviderMock = new Mock<IJwtProvider>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _trustedDeviceRepositoryMock = new Mock<IGenericRepository<TrustedDevice>>();

        var strategies = new List<UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.IOtpVerificationStrategy>
        {
            new UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.LoginOtpStrategy(_userRepositoryMock.Object, _unitOfWorkMock.Object, _userTokenServiceMock.Object, _trustedDeviceRepositoryMock.Object),
            new UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.ForgotPasswordOtpStrategy(_otpServiceMock.Object),
            new UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.StepUpOtpStrategy(_jwtProviderMock.Object, _otpServiceMock.Object)
        };
        _strategyResolver = new UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies.OtpVerificationStrategyResolver(strategies);

        _handler = new VerifyOtpCommandHandler(
            _otpServiceMock.Object,
            _userRepositoryMock.Object,
            _strategyResolver
        );
    }
    
    #region Helper
    private static User CreateActiveUser(string email = "user@test.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = "Test User",
            Status = UserStatus.Active,
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
        var user = CreateActiveUser("user@test.com");
        var command = new VerifyOtpCommand("user@test.com", "000000", OtpPurpose.Login, "UserAgent");
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        _otpServiceMock.Setup(o =>
                o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((false, "Invalid OTP code"));
        
        Func<Task> act =  async () => await _handler.Handle(command, CancellationToken.None);
        
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage($"Invalid OTP code");
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
        _userTokenServiceMock.Setup(s =>
            s.IssueTokensAsync(user, command.UserAgent, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UavPms.IdentityService.Application.Features.Auth.DTOs.AuthResultDto.SuccessResult(
                "access-token", 
                "refresh-token", 
                3600, 
                new UavPms.IdentityService.Application.Features.Auth.DTOs.AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Roles = new List<string> { "Operator" }
                }, 
                "trust-token"));

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
        
        Func<Task> act =  async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleException_WhenUserIsInactive()
    {
        var user = CreateActiveUser();
        user.Status = UserStatus.Inactive;
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
        user.Status = UserStatus.Pending;
        var command = new VerifyOtpCommand(user.Email, "123456", OtpPurpose.EmailVerification, "UserAgent");
        
        _otpServiceMock.Setup(o =>
            o.VerifyOtpAsync(command.Email, command.Code, command.OtpPurpose))
            .ReturnsAsync((true, "OK"));
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        _userTokenServiceMock.Setup(s =>
            s.IssueTokensAsync(user, command.UserAgent, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UavPms.IdentityService.Application.Features.Auth.DTOs.AuthResultDto.SuccessResult(
                "access-token", 
                "refresh-token", 
                3600, 
                new UavPms.IdentityService.Application.Features.Auth.DTOs.AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Roles = new List<string> { "Operator" }
                }, 
                "trust-token"));
        _unitOfWorkMock.Setup(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        
        var result = await _handler.Handle(command, CancellationToken.None);
        
        user.IsEmailVerified.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        _userRepositoryMock.Verify(r => r.UpdateAsync(user), Times.Once);
        result.AuthResult.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnVerificationToken_WhenForgotPurposePurpose()
    {
        var user = CreateActiveUser("user@test.com");
        var command = new VerifyOtpCommand("user@test.com", "123456", OtpPurpose.ForgotPassword, "UserAgent");
        _userRepositoryMock.Setup(u => u.GetByEmailWithRolesAsync(command.Email))
            .ReturnsAsync(user);
        
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

