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
    [Theory]
    [InlineData("an3439201@gmail.com")]
    [InlineData("AN3439201@GMAIL.COM")]
    [InlineData(" An3439201@Gmail.com ")]
    public async Task GetByEmailWithRolesAsync_ShouldMatchEmailCaseInsensitively(string lookupEmail)
    {
        await using var context = CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "An3439201@gmail.com",
            FullName = "Case Test User",
            UserRoles = new List<UserRole>
            {
                new()
                {
                    Role = new Role { Id = 1, RoleName = "Operator" }
                }
            }
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var repository = new UserRepository(context);

        var result = await repository.GetByEmailWithRolesAsync(lookupEmail);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.UserRoles.Should().ContainSingle();
        result.UserRoles.Single().Role!.RoleName.Should().Be("Operator");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }
}
