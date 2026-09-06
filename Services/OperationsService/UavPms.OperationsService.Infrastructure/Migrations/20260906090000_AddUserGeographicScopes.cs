using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UavPms.OperationsService.Infrastructure.Persistence;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations;

/// <summary>Purely additive migration for durable default GIS scopes.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906090000_AddUserGeographicScopes")]
public sealed class AddUserGeographicScopes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserGeographicScopes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                SubstationId = table.Column<Guid>(type: "uuid", nullable: true),
                TransmissionLineId = table.Column<Guid>(type: "uuid", nullable: true),
                ManagementUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserGeographicScopes", x => x.Id);
                table.ForeignKey("FK_UserGeographicScopes_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_UserGeographicScopes_HasScope", "(\"RegionId\" IS NOT NULL OR \"SubstationId\" IS NOT NULL OR \"TransmissionLineId\" IS NOT NULL OR \"ManagementUnitId\" IS NOT NULL)");
            });

        migrationBuilder.CreateIndex("IX_UserGeographicScopes_UserId_RegionId", "UserGeographicScopes", new[] { "UserId", "RegionId" });
        migrationBuilder.CreateIndex("IX_UserGeographicScopes_UserId_SubstationId", "UserGeographicScopes", new[] { "UserId", "SubstationId" });
        migrationBuilder.CreateIndex("IX_UserGeographicScopes_UserId_TransmissionLineId", "UserGeographicScopes", new[] { "UserId", "TransmissionLineId" });
        migrationBuilder.CreateIndex("IX_UserGeographicScopes_UserId_ManagementUnitId", "UserGeographicScopes", new[] { "UserId", "ManagementUnitId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("UserGeographicScopes");
}
