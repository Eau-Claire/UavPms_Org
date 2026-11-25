using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.ActiveLearningService.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImageLabelSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageLabelSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameIndex = table.Column<int>(type: "integer", nullable: true),
                    AnomalyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Boxes = table.Column<string>(type: "jsonb", nullable: false),
                    LabelStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TrainingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AnalystId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageLabelSets", x => x.Id);
                    table.CheckConstraint("CK_ImageLabelSets_LabelStatus", "\"LabelStatus\" IN ('Draft', 'FullyReviewed')");
                    table.CheckConstraint("CK_ImageLabelSets_TrainingStatus", "\"TrainingStatus\" IS NULL OR \"TrainingStatus\" IN ('Queued')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageLabelSets_TrainingStatus",
                table: "ImageLabelSets",
                column: "TrainingStatus");

            migrationBuilder.CreateIndex(
                name: "UX_ImageLabelSets_Media_Frame",
                table: "ImageLabelSets",
                columns: new[] { "MediaId", "FrameIndex" },
                unique: true,
                filter: "\"FrameIndex\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ImageLabelSets_Media_Image",
                table: "ImageLabelSets",
                column: "MediaId",
                unique: true,
                filter: "\"FrameIndex\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageLabelSets");
        }
    }
}
