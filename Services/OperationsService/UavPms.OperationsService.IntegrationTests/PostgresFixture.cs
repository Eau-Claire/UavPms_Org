using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// One PostGIS container per test run with every OperationsService migration applied (plan item 3.6).
/// Without Docker the tests are skipped locally (they return early) but fail on CI (env CI=true).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Image = "postgis/postgis:16-3.4";

    private PostgreSqlContainer? _container;

    public bool Available { get; private set; }
    public string ConnectionString => _container!.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder().WithImage(Image).Build();
            await _container.StartAsync();
        }
        catch (Exception) when (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Available = false;
            return;
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Available = true;
    }

    public ApplicationDbContext CreateContext(ICurrentUserServices? user = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseNetTopologySuite())
            .Options;
        return new ApplicationDbContext(options, user ?? Mock.Of<ICurrentUserServices>());
    }

    public async Task DisposeAsync()
    {
        if (_container != null) await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
