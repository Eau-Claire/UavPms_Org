using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UavPms.OperationsService.Infrastructure.Migrations;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Tests.Infrastructure;

public sealed class MigrationDiscoveryTests
{
    [Fact]
    public void Mf01Migration_IsDiscoveredByApplicationDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=discovery_only;Username=test;Password=test",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new ApplicationDbContext(options, currentUserServices: null);

        Assert.Contains("20260909130000_AddMf01MissionLifecycle", context.Database.GetMigrations());
        Assert.Equal(
            typeof(ApplicationDbContext),
            typeof(AddMf01MissionLifecycle)
                .GetCustomAttributes(typeof(DbContextAttribute), inherit: false)
                .Cast<DbContextAttribute>()
                .Single()
                .ContextType);
    }
}
