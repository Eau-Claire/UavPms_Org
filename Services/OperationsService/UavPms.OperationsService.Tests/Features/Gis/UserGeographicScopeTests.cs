using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;
using UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Gis;

public class UserGeographicScopeTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, params string[] roles)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(roles);
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    private static SetUserGeographicScopesCommandHandler CreateSetHandler(ApplicationDbContext db, ICurrentUserServices user)
        => new(new UserRepository(db), new GenericRepository<UserGeographicScope>(db), new GenericRepository<Region>(db), user, new UnitOfWork(db));

    private static GetUserGeographicScopesQueryHandler CreateGetHandler(ApplicationDbContext db, ICurrentUserServices user)
        => new(new UserRepository(db), new GenericRepository<UserGeographicScope>(db), new GenericRepository<Region>(db), user);

    private static Region NewRegion(string code) => new() { Id = Guid.NewGuid(), Code = code, Name = code, Type = "Province" };

    [Fact]
    public async Task SetScopes_ReplacesSet_SoftDeletesRemovedAndAddsNew()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "inspector@x.vn", FullName = "Inspector" };
        var (a, b, c) = (NewRegion("A"), NewRegion("B"), NewRegion("C"));
        db.Users.Add(user);
        db.Regions.AddRange(a, b, c);
        db.UserGeographicScopes.AddRange(
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = b.Id });
        await db.SaveChangesAsync();

        var result = await CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, new[] { b.Id, c.Id, c.Id }), CancellationToken.None);

        result.Regions.Select(r => r.Code).Should().Equal("B", "C");
        var rows = await db.UserGeographicScopes.IgnoreQueryFilters().Where(s => s.UserId == user.Id).ToListAsync();
        rows.Should().HaveCount(3);
        rows.Single(s => s.RegionId == a.Id).IsDeleted.Should().BeTrue();
        rows.Where(s => !s.IsDeleted).Select(s => s.RegionId).Should().BeEquivalentTo(new[] { b.Id, c.Id });
    }

    [Fact]
    public async Task SetScopes_EmptyList_RemovesAll()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "u@x.vn" };
        var a = NewRegion("A");
        db.Users.Add(user);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id });
        await db.SaveChangesAsync();

        var result = await CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, Array.Empty<Guid>()), CancellationToken.None);

        result.Regions.Should().BeEmpty();
        (await db.UserGeographicScopes.CountAsync(s => s.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SetScopes_UnknownRegion_ThrowsAndChangesNothing()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "u@x.vn" };
        var a = NewRegion("A");
        db.Users.Add(user);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id });
        await db.SaveChangesAsync();

        var act = () => CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, new[] { Guid.NewGuid() }), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await db.UserGeographicScopes.CountAsync(s => s.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SetScopes_ManagerOnOwnScope_IsForbidden()
    {
        var managerId = Guid.NewGuid();
        var manager = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(manager.Object);
        var a = NewRegion("A");
        db.Users.Add(new User { Id = managerId, Email = "m@x.vn" });
        db.Regions.Add(a);
        await db.SaveChangesAsync();

        var act = () => CreateSetHandler(db, manager.Object)
            .Handle(new SetUserGeographicScopesCommand(managerId, new[] { a.Id }), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetScopes_NonManagerReadingAnotherUser_IsForbidden_ButOwnScopeIsAllowed()
    {
        var inspectorId = Guid.NewGuid();
        var inspector = CreateUserMock(inspectorId, UserRoles.Inspector);
        await using var db = CreateContext(inspector.Object);
        var other = new User { Id = Guid.NewGuid(), Email = "o@x.vn" };
        var a = NewRegion("A");
        db.Users.AddRange(new User { Id = inspectorId, Email = "i@x.vn" }, other);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = inspectorId, RegionId = a.Id });
        await db.SaveChangesAsync();
        var handler = CreateGetHandler(db, inspector.Object);

        var readOther = () => handler.Handle(new GetUserGeographicScopesQuery(other.Id), CancellationToken.None);
        await readOther.Should().ThrowAsync<ForbiddenException>();

        var own = await handler.Handle(new GetUserGeographicScopesQuery(inspectorId), CancellationToken.None);
        own.Regions.Should().ContainSingle(r => r.RegionId == a.Id && r.Code == "A");
    }
}
