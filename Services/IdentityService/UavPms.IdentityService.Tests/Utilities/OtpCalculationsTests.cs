using System;
using FluentAssertions;
using UavPms.IdentityService.Infrastructure.Services.Helpers;
using Xunit;

namespace UavPms.IdentityService.Tests.Utilities;

public class OtpCalculationsTests
{
    [Fact]
    public void CalculateCooldown_ShouldReturnActive_WhenTtlIsGreaterThan150Seconds()
    {
        var ttl = TimeSpan.FromSeconds(170); // 10s elapsed
        var (isCooldownActive, remainingSeconds) = OtpCalculations.CalculateCooldown(ttl);

        isCooldownActive.Should().BeTrue();
        remainingSeconds.Should().Be(20);
    }

    [Fact]
    public void CalculateCooldown_ShouldReturnInactive_WhenTtlIsLessThan150Seconds()
    {
        var ttl = TimeSpan.FromSeconds(140); // 40s elapsed
        var (isCooldownActive, remainingSeconds) = OtpCalculations.CalculateCooldown(ttl);

        isCooldownActive.Should().BeFalse();
        remainingSeconds.Should().Be(0);
    }

    [Fact]
    public void EvaluateAttempts_ShouldFlagExceeded_WhenAttemptsReachOrExceed5()
    {
        var result1 = OtpCalculations.EvaluateAttempts(3);
        result1.Exceeded.Should().BeFalse();
        result1.RemainingAttempts.Should().Be(2);

        var result2 = OtpCalculations.EvaluateAttempts(5);
        result2.Exceeded.Should().BeTrue();
        result2.RemainingAttempts.Should().Be(0);
    }
}
