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

    [Theory]
    [InlineData(" User@Test.com ", "otp:login:user@test.com")]
    [InlineData("USER@TEST.COM", "otp:login:user@test.com")]
    [InlineData("user@test.com", "otp:login:user@test.com")]
    public void OtpKey_ShouldNormalizeEmail(string email, string expectedKey)
    {
        var key = RedisKeyBuilder.OtpKey(OtpPurpose.Login, email);

        key.Should().Be(expectedKey);
    }

    [Fact]
    public void AttemptsKey_ShouldFormatCorrectly()
    {
        var key = RedisKeyBuilder.AttemptsKey(OtpPurpose.Login, "user@test.com");
        key.Should().Be("otp:login:user@test.com:attempts");
    }

    [Theory]
    [InlineData(" User@Test.com ", "otp:login:user@test.com:attempts")]
    [InlineData("USER@TEST.COM", "otp:login:user@test.com:attempts")]
    [InlineData("user@test.com", "otp:login:user@test.com:attempts")]
    public void AttemptsKey_ShouldNormalizeEmail(string email, string expectedKey)
    {
        var key = RedisKeyBuilder.AttemptsKey(OtpPurpose.Login, email);

        key.Should().Be(expectedKey);
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
