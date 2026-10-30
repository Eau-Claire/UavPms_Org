using System;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Persistence.Configurations;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Workflows;

/// <summary>Guards the ERD v2.2 varchar lengths (plan item 2.7) against the real PostgreSQL model.</summary>
public class ErdColumnLengthsTests
{
    // Building the model needs no connection.
    private static IModel NpgsqlModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only", o => o.UseNetTopologySuite())
            .Options;
        using var db = new ApplicationDbContext(options);
        return Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(db).Model;
    }

    [Fact]
    public void EveryErdColumn_IsVarcharOfTheErdLength()
    {
        var model = NpgsqlModel();

        foreach (var (table, column, length) in ErdColumnLengths.Columns)
        {
            var entity = model.GetEntityTypes().Single(e => e.GetTableName() == table);
            var property = entity.GetProperties().Single(p => p.GetColumnName() == column);
            property.GetColumnType().Should().Be($"character varying({length})", $"{table}.{column}");
        }
    }

    [Fact]
    public void EnumStoredAsString_FitsItsColumnLength()
    {
        var model = NpgsqlModel();

        var tooLong = (
            from entity in model.GetEntityTypes()
            from property in entity.GetProperties()
            let enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType
            where enumType.IsEnum && property.GetMaxLength() is not null && property.GetProviderClrType() == typeof(string)
            from name in Enum.GetNames(enumType)
            where name.Length > property.GetMaxLength()
            select $"{entity.GetTableName()}.{property.GetColumnName()} = {name} ({name.Length} > {property.GetMaxLength()})"
        ).ToList();

        tooLong.Should().BeEmpty();
    }
}
