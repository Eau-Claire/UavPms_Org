using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.ActiveLearningService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ImageLabelSets_TrainingStatus",
                table: "ImageLabelSets");

            migrationBuilder.AddColumn<Guid>(
                name: "TrainingBatchId",
                table: "ImageLabelSets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainingBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ImageCount = table.Column<int>(type: "integer", nullable: false),
                    CycleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Result = table.Column<string>(type: "jsonb", nullable: true),
                    ModelName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ModelSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingBatches", x => x.Id);
                    table.CheckConstraint("CK_TrainingBatches_Status", "\"Status\" IN ('Running', 'deployed', 'gate_passed_not_deployed', 'gate_failed', 'rolled_back', 'rollback_failed', 'error')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageLabelSets_TrainingBatchId",
                table: "ImageLabelSets",
                column: "TrainingBatchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ImageLabelSets_TrainingStatus",
                table: "ImageLabelSets",
                sql: "\"TrainingStatus\" IS NULL OR \"TrainingStatus\" IN ('Queued', 'Batched', 'Trained')");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingBatches_CreatedAt",
                table: "TrainingBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "UX_TrainingBatches_Running",
                table: "TrainingBatches",
                column: "Status",
                unique: true,
                filter: "\"Status\" = 'Running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImageLabelSets_TrainingBatchId",
                table: "ImageLabelSets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ImageLabelSets_TrainingStatus",
                table: "ImageLabelSets");

            migrationBuilder.DropColumn(
                name: "TrainingBatchId",
                table: "ImageLabelSets");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ImageLabelSets_TrainingStatus",
                table: "ImageLabelSets",
                sql: "\"TrainingStatus\" IS NULL OR \"TrainingStatus\" IN ('Queued')");
        }
    }
}
