using System;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Persistence.Configurations;
using UavPms.Shared.Contracts.Settings;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class StorageKeysTests
{
    private const string Host = "https://unit-test-project.supabase.co";
    private const string Base = Host + "/storage/v1/object/public/";

    // StorageKeys is process-wide; other tests only use URLs outside this host, which pass through unchanged.
    private static void WithHost(Action test)
    {
        StorageKeys.Configure(Host + "/");
        try { test(); }
        finally { StorageKeys.Configure(null); }
    }

    [Fact]
    public void SupabasePublicUrl_RoundTripsThroughKey() => WithHost(() =>
    {
        var url = Base + "uav-images/abc%20x.jpg";

        StorageKeys.ToKey(url).Should().Be("uav-images/abc%20x.jpg");
        StorageKeys.ToUrl("uav-images/abc%20x.jpg").Should().Be(url);
    });

    [Theory]
    [InlineData("/images/local.jpg")]
    [InlineData("https://cdn.example.com/x.jpg")]
    [InlineData("https://other-project.supabase.co/storage/v1/object/public/b/1.jpg")]
    [InlineData("")]
    public void LocalPathsAndOtherHosts_AreStoredAndReturnedUnchanged(string value) => WithHost(() =>
    {
        StorageKeys.ToKey(value).Should().Be(value);
        StorageKeys.ToUrl(value).Should().Be(value);
    });

    [Fact]
    public void WithoutConfiguredStorage_KeysAreNotRewritten()
    {
        StorageKeys.ToKey(Base + "b/1.jpg").Should().Be(Base + "b/1.jpg");
        StorageKeys.ToUrl("b/1.jpg").Should().Be("b/1.jpg");
    }

    [Fact]
    public void EveryUrlColumn_HasTheStorageKeyConverter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only", o => o.UseNetTopologySuite())
            .Options;
        using var db = new ApplicationDbContext(options);

        foreach (var (table, column) in StorageKeyColumns.Columns)
        {
            var property = db.Model.GetEntityTypes().Single(e => e.GetTableName() == table)
                .GetProperties().Single(p => p.GetColumnName() == column);
            property.GetValueConverter().Should().NotBeNull($"{table}.{column}");
        }
        StorageKeyColumns.Columns.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("pole.JPG", "image/jpeg")]
    [InlineData("frame.png", "image/png")]
    [InlineData("flight.mp4", "video/mp4")]
    [InlineData("noext", "application/octet-stream")]
    public void StorageContentType_FollowsExtension(string fileName, string expected)
        => StorageContentTypes.For(fileName).Should().Be(expected);
}
