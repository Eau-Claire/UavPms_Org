using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.AIInspectionService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Plan item 2.8: these columns hold a storage key in the database and a full URL in code
/// (see <see cref="StorageKeys"/>). OperationsService (owner of the migrations) maps the same columns with the same converter.
/// </summary>
public static class StorageKeyColumns
{
    public static readonly (string Table, string Column)[] Columns =
    [
        ("InspectionMedia", "FileUrl"),
        ("DetectedAnomalies", "ImageUrl"),
        ("DetectedAnomalies", "CropUrl"),
    ];

    private static readonly ValueConverter<string, string> Converter =
        new(url => StorageKeys.ToKey(url), stored => StorageKeys.ToUrl(stored));

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var (table, column) in Columns)
        {
            var entity = modelBuilder.Model.GetEntityTypes().SingleOrDefault(e => e.GetTableName() == table)
                ?? throw new InvalidOperationException($"Storage key column: table '{table}' is not mapped.");
            var property = entity.GetProperties().SingleOrDefault(p => p.GetColumnName() == column)
                ?? throw new InvalidOperationException($"Storage key column: column '{table}.{column}' is not mapped.");
            property.SetValueConverter(Converter);
        }
    }
}
