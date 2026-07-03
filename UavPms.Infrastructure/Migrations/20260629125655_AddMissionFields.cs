using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "Missions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "DroneCode",
                table: "Missions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RouteData",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Missions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_AssignedToUserId",
                table: "Missions",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_Users_AssignedToUserId",
                table: "Missions",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Missions_Users_AssignedToUserId",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_AssignedToUserId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "DroneCode",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "RouteData",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Missions");
        }
    }
}
