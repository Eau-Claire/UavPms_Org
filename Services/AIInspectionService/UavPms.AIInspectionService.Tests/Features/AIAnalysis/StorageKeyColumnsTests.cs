using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Persistence.Configurations;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

/// <summary>AIInspectionService must read the same storage keys OperationsService writes (plan item 2.8).</summary>
public class StorageKeyColumnsTests
{
    [Fact]
    public void EveryUrlColumn_HasTheStorageKeyConverter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only", o => o.UseNetTopologySuite())
            .Options;
        using var db = new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());

        StorageKeyColumns.Columns.Should().HaveCount(3);
        foreach (var (table, column) in StorageKeyColumns.Columns)
        {
            var property = db.Model.GetEntityTypes().Single(e => e.GetTableName() == table)
                .GetProperties().Single(p => p.GetColumnName() == column);
            property.GetValueConverter().Should().NotBeNull($"{table}.{column}");
        }
    }
}
