using UavPms.IdentityService.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace UavPms.IdentityService.Tests.Services;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher;

    public  BCryptPasswordHasherTests()
    {
        _hasher = new BCryptPasswordHasher();
    }

    [Fact]
    public void Hash_ShouldNotReturnPlainText()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void Hash_ShouldProduceDifferentHashes_ForSamePassword()
    {
        var password = "SuperSecretPassword123!@#";
        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);
        
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordMatches()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        var result = _hasher.Verify(hash, password);
        result.Should().BeTrue();
    }

    [Fact]
    public void Verity_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        var result = _hasher.Verify(hash, "wrongPassword123!@#");
        result.Should().BeFalse();
    }

    [Fact]
    public void Hash_ShouldReturnHashedPassword_WhenGivenPlanPassword()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenPasswordMatchesHash()
    {
        var password = "SuperSecretPassword123!@#";
        var hash = _hasher.Hash(password);
        
        var result = _hasher.Verify(hash, password);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenPasswordDoesNotMatchHash()
    {
        var password = "SuperSecretPassword123!@#";
        var wrongPassword = "WrongPassword123!@#";
        var hash = _hasher.Hash(password);
        
        var result = _hasher.Verify(hash, wrongPassword);
        
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid_hash_string")]
    public void Verify_ShouldReturnFalse_WhenPasswordHashIsNullOrEmptyOrInvalid(string? invalidHash)
    {
        var result = _hasher.Verify(invalidHash!, "SomePassword123!");

        result.Should().BeFalse();
    }
}