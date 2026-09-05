using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UavPms.OperationsService.Infrastructure.Persistence;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260905090000_UnifyInspectionAiFlow")]
public sealed class UnifyInspectionAiFlow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MessageType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OutboxMessages", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_MessageType_OccurredAt",
            table: "OutboxMessages",
            columns: new[] { "MessageType", "OccurredAt" },
            filter: "\"PublishedAt\" IS NULL AND \"IsDeleted\" = false");

        migrationBuilder.AddColumn<Guid>(
            name: "UploadedBy",
            table: "InspectionMedia",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceEventId",
            table: "AIAnalysisRequests",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AssetId",
            table: "AIAnalysisRequests",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ModelName",
            table: "AIAnalysisRequests",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "SERVER");

        migrationBuilder.CreateIndex(
            name: "IX_InspectionMedia_UploadedBy",
            table: "InspectionMedia",
            column: "UploadedBy");

        migrationBuilder.CreateIndex(
            name: "IX_AIAnalysisRequests_SourceEventId",
            table: "AIAnalysisRequests",
            column: "SourceEventId",
            unique: true,
            filter: "\"SourceEventId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AIAnalysisRequests_AssetId",
            table: "AIAnalysisRequests",
            column: "AssetId");

        migrationBuilder.CreateIndex(
            name: "IX_AIAnalysisRequests_ActiveLogicalAnalysis",
            table: "AIAnalysisRequests",
            columns: new[] { "MediaId", "AnalysisType", "ModelName" },
            unique: true,
            filter: "\"MediaId\" IS NOT NULL AND \"IsDeleted\" = false AND \"Status\" IN (0, 1)");

        migrationBuilder.CreateIndex(
            name: "IX_DetectedAnomalies_MediaId_AiDetectionId",
            table: "DetectedAnomalies",
            columns: new[] { "MediaId", "AiDetectionId" },
            unique: true,
            filter: "\"AiDetectionId\" IS NOT NULL AND \"IsDeleted\" = false");

        migrationBuilder.AddForeignKey(
            name: "FK_InspectionMedia_Users_UploadedBy",
            table: "InspectionMedia",
            column: "UploadedBy",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_AIAnalysisRequests_AssetComponents_AssetId",
            table: "AIAnalysisRequests",
            column: "AssetId",
            principalTable: "AssetComponents",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_InspectionMedia_Users_UploadedBy", "InspectionMedia");
        migrationBuilder.DropForeignKey("FK_AIAnalysisRequests_AssetComponents_AssetId", "AIAnalysisRequests");
        migrationBuilder.DropIndex("IX_InspectionMedia_UploadedBy", "InspectionMedia");
        migrationBuilder.DropIndex("IX_AIAnalysisRequests_SourceEventId", "AIAnalysisRequests");
        migrationBuilder.DropIndex("IX_AIAnalysisRequests_ActiveLogicalAnalysis", "AIAnalysisRequests");
        migrationBuilder.DropIndex("IX_DetectedAnomalies_MediaId_AiDetectionId", "DetectedAnomalies");
        migrationBuilder.DropColumn("UploadedBy", "InspectionMedia");
        migrationBuilder.DropColumn("SourceEventId", "AIAnalysisRequests");
        migrationBuilder.DropColumn("AssetId", "AIAnalysisRequests");
        migrationBuilder.DropColumn("ModelName", "AIAnalysisRequests");
        migrationBuilder.DropTable("OutboxMessages");
    }
}
