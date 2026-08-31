using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UavPms.OperationsService.Infrastructure.Persistence;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260825120000_AlignRevisedSupabaseDomainBaseline")]
public partial class AlignRevisedSupabaseDomainBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: the revised schema is managed in Supabase first.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty baseline synchronization migration.
    }
}
