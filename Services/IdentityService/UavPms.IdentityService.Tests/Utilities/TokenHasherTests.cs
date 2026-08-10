using FluentAssertions;
using UavPms.IdentityService.Application.Common.Utilities;
using Xunit;

namespace UavPms.IdentityService.Tests.Utilities;

public class TokenHasherTests
{
    [Fact]
    public void Hash_ShouldReturnEmptyString_WhenInputIsNullOrEmpty()
    {
        TokenHasher.Hash(null!).Should().BeEmpty();
        TokenHasher.Hash(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Hash_ShouldReturnConsistentSha256Base64_WhenInputIsValid()
    {
        var input = "test-token-123";
        var hash1 = TokenHasher.Hash(input);
        var hash2 = TokenHasher.Hash(input);

        hash1.Should().NotBeNullOrEmpty();
        hash1.Should().Be(hash2);
    }
}
