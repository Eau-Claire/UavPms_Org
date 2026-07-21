using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectionPlaybackMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiDetectionId",
                table: "DetectedAnomalies",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CropUrl",
                table: "DetectedAnomalies",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrameIndex",
                table: "DetectedAnomalies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gps",
                table: "DetectedAnomalies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "DetectedAnomalies",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Timestamp",
                table: "DetectedAnomalies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TowerId",
                table: "DetectedAnomalies",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoDuration",
                table: "DetectedAnomalies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoFps",
                table: "DetectedAnomalies",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideoHeight",
                table: "DetectedAnomalies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VideoWidth",
                table: "DetectedAnomalies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDetectionId",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "CropUrl",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "FrameIndex",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "Gps",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "TowerId",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "VideoDuration",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "VideoFps",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "VideoHeight",
                table: "DetectedAnomalies");

            migrationBuilder.DropColumn(
                name: "VideoWidth",
                table: "DetectedAnomalies");
        }
    }
}
