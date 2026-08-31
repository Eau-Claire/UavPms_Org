using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.Infrastructure.Persistence;
using UavPms.IdentityService.Infrastructure.Repositories;

namespace UavPms.IdentityService.Tests.Infrastructure.Repositories;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetByEmailWithRolesAsync_ShouldMatchEmailCaseInsensitively()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "An3439201@gmail.com",
            FullName = "Case Test",
            Phone = "0000000000"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var result = await repository.GetByEmailWithRolesAsync("  AN3439201@GMAIL.COM ");

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }
}
