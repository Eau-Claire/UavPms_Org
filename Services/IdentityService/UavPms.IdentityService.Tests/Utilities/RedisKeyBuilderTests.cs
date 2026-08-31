using FluentAssertions;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Infrastructure.Services.Helpers;
using Xunit;

namespace UavPms.IdentityService.Tests.Utilities;

public class RedisKeyBuilderTests
{
    [Fact]
    public void OtpKey_ShouldFormatCorrectlyWithoutSpaces()
    {
        var key = RedisKeyBuilder.OtpKey(OtpPurpose.Login, "user@test.com");
        key.Should().Be("otp:login:user@test.com");
    }

    [Fact]
    public void AttemptsKey_ShouldFormatCorrectly()
    {
        var key = RedisKeyBuilder.AttemptsKey(OtpPurpose.Login, "user@test.com");
        key.Should().Be("otp:login:user@test.com:attempts");
    }

    [Fact]
    public void OtpKeys_ShouldNormalizeEmailCasingAndWhitespace()
    {
        RedisKeyBuilder.OtpKey(OtpPurpose.Login, "  User@Test.COM  ")
            .Should().Be("otp:login:user@test.com");

        RedisKeyBuilder.AttemptsKey(OtpPurpose.ForgotPassword, "  User@Test.COM  ")
            .Should().Be("otp:forgotpassword:user@test.com:attempts");
    }

    [Fact]
    public void VerificationTokenKey_ShouldFormatCorrectly()
    {
        var key = RedisKeyBuilder.VerificationTokenKey("hash123");
        key.Should().Be("verification-token:hash123");
    }

    [Fact]
    public void StepUpKey_ShouldFormatCorrectly()
    {
        var key = RedisKeyBuilder.StepUpKey("user-id-1", "ChangePassword");
        key.Should().Be("step-up:user-id-1:changepassword");
    }
}
