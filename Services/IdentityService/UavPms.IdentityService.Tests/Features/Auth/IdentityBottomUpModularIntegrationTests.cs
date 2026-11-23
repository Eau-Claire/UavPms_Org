using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UavPms.IdentityService.Application.Common.Options;
using UavPms.IdentityService.Application.Common.Services;
using UavPms.IdentityService.Application.Features.Auth.Commands.Login;
using UavPms.IdentityService.Application.Features.Auth.Commands.RefreshToken;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp;
using UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;
using UavPms.IdentityService.Application.Features.Users.Commands.CreateUser;
using UavPms.IdentityService.Application.Features.Users.Commands.UpdateUser;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.Infrastructure.Persistence;
using UavPms.IdentityService.Infrastructure.Repositories;
using UavPms.IdentityService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;
using RefreshTokenEntity = UavPms.IdentityService.Domain.Entities.RefreshToken;

namespace UavPms.IdentityService.Tests.Features.Auth;

/// <summary>
/// Bottom-Up Modular Integration Tests for IdentityService:
/// Combines CreateUserCommandHandler, BCryptPasswordHasher, LoginCommandHandler, VerifyOtpCommandHandler
/// (LoginOtpStrategy), UserTokenService, JwtProvider, SystemSettingService, RefreshTokenCommandHandler,
/// and UpdateUserCommandHandler against a shared EF Core database context.
/// </summary>
public class IdentityBottomUpModularIntegrationTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"identity-bottom-up-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }

    [Fact]
    public async Task Pair1And2_CreateUser_LoginWithOtp_DeviceTrustBypass_RefreshTokenRotationReuseDetection_AndDeactivation()
    {
        await using var db = CreateContext();

        // Seed canonical roles
        db.Roles.AddRange(
            new Role { Id = 1, RoleName = UserRoles.SystemAdmin, Description = "System Administrator" },
            new Role { Id = 2, RoleName = UserRoles.Manager, Description = "Manager" },
            new Role { Id = 3, RoleName = UserRoles.Inspector, Description = "Inspector" },
            new Role { Id = 4, RoleName = UserRoles.Analyst, Description = "Analyst" },
            new Role { Id = 5, RoleName = UserRoles.Technician, Description = "Technician" });
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(db);
        var roleRepo = new GenericRepository<Role>(db);
        var refreshTokenRepo = new GenericRepository<RefreshTokenEntity>(db);
        var trustedDeviceRepo = new GenericRepository<TrustedDevice>(db);
        var uow = new UnitOfWork(db);
        var passwordHasher = new BCryptPasswordHasher();

        // Configure custom token expiries via SystemSettingService
        var settingService = new SystemSettingService(db, NullLogger<SystemSettingService>.Instance);
        settingService.InvalidateCache();
        await settingService.UpdateAsync(SettingKeys.AuthAccessTokenExpiryMinutes, "45");
        await settingService.UpdateAsync(SettingKeys.AuthRefreshTokenExpiryDays, "14");
        await settingService.UpdateAsync(SettingKeys.AuthDeviceTrustDays, "60");

        var jwtOptions = Options.Create(new JwtOptions
        {
            SecretKey = "uavpms_bottom_up_integration_test_secret_key_2026!",
            Issuer = "UavPms.IdentityService",
            Audience = "UavPms.Client",
            ExpiryMinutes = 45
        });
        var jwtProvider = new JwtProvider(jwtOptions);
        var userTokenService = new UserTokenService(jwtProvider, refreshTokenRepo, uow, jwtOptions, settingService);

        var otpServiceMock = new Mock<IOtpService>();
        otpServiceMock
            .Setup(o => o.GenerateAndSendOtpAsync(It.IsAny<string>(), OtpPurpose.Login, false))
            .ReturnsAsync((true, "OTP sent"));
        otpServiceMock
            .Setup(o => o.VerifyOtpAsync(It.IsAny<string>(), "654321", OtpPurpose.Login))
            .ReturnsAsync((true, "Verified"));

        // 1. CreateUserCommandHandler creates Inspector user with BCrypt password hash and canonical role
        var createUserHandler = new CreateUserCommandHandler(userRepo, roleRepo, passwordHasher, uow);
        var userId = await createUserHandler.Handle(new CreateUserCommand(
            Email: "Inspector.One@UavPms.vn",
            FullName: "Nguyễn Văn Kiểm Tra",
            Phone: "0901234567",
            Roles: new List<string> { UserRoles.Inspector },
            Password: "Inspector@2026!"), CancellationToken.None);

        var createdUser = await db.Users.SingleAsync(u => u.Id == userId);
        createdUser.Email.Should().Be("inspector.one@uavpms.vn");
        createdUser.IsEmailVerified = true; // Mark email verified for login flow
        await db.SaveChangesAsync();

        // 2. First Login without DeviceTrustToken -> requires OTP
        var loginHandler = new LoginCommandHandler(
            userRepo,
            passwordHasher,
            userTokenService,
            otpServiceMock.Object,
            trustedDeviceRepo,
            NullLogger<LoginCommandHandler>.Instance);

        var firstLogin = await loginHandler.Handle(
            new LoginCommand("Inspector.One@UavPms.vn", "Inspector@2026!", null, "Chrome/130 Windows"),
            CancellationToken.None);

        firstLogin.OtpRequired.Should().BeTrue();
        firstLogin.AccessToken.Should().BeNull();

        // 3. VerifyOtpCommandHandler (LoginOtpStrategy) -> issues JWT, RefreshToken (14 days), and TrustedDevice (60 days)
        var strategies = new IOtpVerificationStrategy[]
        {
            new LoginOtpStrategy(userRepo, uow, userTokenService, trustedDeviceRepo, settingService),
            new ForgotPasswordOtpStrategy(otpServiceMock.Object, settingService),
            new StepUpOtpStrategy(jwtProvider, otpServiceMock.Object, settingService)
        };
        var verifyOtpHandler = new VerifyOtpCommandHandler(
            otpServiceMock.Object,
            userRepo,
            new OtpVerificationStrategyResolver(strategies));

        var verifyResult = await verifyOtpHandler.Handle(
            new VerifyOtpCommand("inspector.one@uavpms.vn", "654321", OtpPurpose.Login, "Chrome/130 Windows"),
            CancellationToken.None);

        verifyResult.Success.Should().BeTrue();
        verifyResult.AuthResult.Should().NotBeNull();
        verifyResult.AuthResult!.ExpiresIn.Should().Be(45 * 60);
        verifyResult.AuthResult.DeviceTrustToken.Should().NotBeNullOrWhiteSpace();

        var firstRefreshToken = verifyResult.AuthResult.RefreshToken!;
        var deviceTrustToken = verifyResult.AuthResult.DeviceTrustToken!;

        var parsedJwt = new JwtSecurityTokenHandler().ReadJwtToken(verifyResult.AuthResult.AccessToken!);
        parsedJwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId.ToString());
        parsedJwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == UserRoles.Inspector);

        var storedDevice = await db.TrustedDevices.SingleAsync(d => d.UserId == userId);
        storedDevice.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(60), TimeSpan.FromMinutes(2));

        // 4. Second Login with valid DeviceTrustToken -> bypasses OTP and rotates active RefreshToken (Single Active Session)
        var secondLogin = await loginHandler.Handle(
            new LoginCommand("inspector.one@uavpms.vn", "Inspector@2026!", deviceTrustToken, "Chrome/130 Windows"),
            CancellationToken.None);

        secondLogin.OtpRequired.Should().BeFalse();
        secondLogin.Success.Should().BeTrue();
        secondLogin.RefreshToken.Should().NotBeNullOrWhiteSpace();
        secondLogin.RefreshToken.Should().NotBe(firstRefreshToken);

        var secondRefreshToken = secondLogin.RefreshToken!;

        // 5. RefreshTokenCommandHandler rotates secondRefreshToken -> thirdRefreshToken; reusing revoked secondRefreshToken triggers security breach
        var refreshHandler = new RefreshTokenCommandHandler(refreshTokenRepo, userRepo, userTokenService, uow);
        var rotatedAuth = await refreshHandler.Handle(
            new RefreshTokenCommand(secondRefreshToken, "Chrome/130 Windows"),
            CancellationToken.None);

        rotatedAuth.Success.Should().BeTrue();
        var thirdRefreshToken = rotatedAuth.RefreshToken!;
        thirdRefreshToken.Should().NotBe(secondRefreshToken);

        // Reuse of already-revoked secondRefreshToken -> revokes all active tokens (including thirdRefreshToken) and throws UnauthorizedAccessException
        var reuseAct = () => refreshHandler.Handle(
            new RefreshTokenCommand(secondRefreshToken, "AttackerAgent"),
            CancellationToken.None);
        await reuseAct.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Revoked refresh token reused*");

        (await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).CountAsync()).Should().Be(0);

        // 6. UpdateUserCommandHandler deactivates user -> subsequent Login is rejected
        var updateUserHandler = new UpdateUserCommandHandler(userRepo, roleRepo, uow);
        await updateUserHandler.Handle(new UpdateUserCommand(
            Id: userId,
            Email: "inspector.one@uavpms.vn",
            FullName: "Nguyễn Văn Kiểm Tra",
            Phone: "0901234567",
            Status: "Inactive",
            Roles: new List<string> { UserRoles.Inspector }), CancellationToken.None);

        var inactiveLoginAct = () => loginHandler.Handle(
            new LoginCommand("inspector.one@uavpms.vn", "Inspector@2026!", deviceTrustToken, "Chrome/130 Windows"),
            CancellationToken.None);
        await inactiveLoginAct.Should().ThrowAsync<UnauthorizedAccessException>();

        // Restore default SystemSettings
        await settingService.UpdateAsync(SettingKeys.AuthAccessTokenExpiryMinutes, "60");
        await settingService.UpdateAsync(SettingKeys.AuthRefreshTokenExpiryDays, "7");
        await settingService.UpdateAsync(SettingKeys.AuthDeviceTrustDays, "30");
    }
}
