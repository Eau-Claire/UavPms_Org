using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddUavExtendedManagementAndSuitability : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "UAVs",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Category",
            table: "UAVs",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "Hexacopter");

        migrationBuilder.AddColumn<string>(
            name: "SerialNumber",
            table: "UAVs",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<Guid>(
            name: "AssignedRegionId",
            table: "UAVs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AssignedRegionName",
            table: "UAVs",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "FlightEnduranceMinutes",
            table: "UAVs",
            type: "double precision",
            nullable: false,
            defaultValue: 30.0);

        migrationBuilder.AddColumn<double>(
            name: "OperationalRangeKm",
            table: "UAVs",
            type: "double precision",
            nullable: false,
            defaultValue: 10.0);

        migrationBuilder.AddColumn<string>(
            name: "CameraPayloadInfo",
            table: "UAVs",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Suitability",
            table: "PreMissionAssessmentDrones",
            type: "text",
            nullable: false,
            defaultValue: "Suitable");

        migrationBuilder.AddColumn<int>(
            name: "EstimatedFlightsNeeded",
            table: "PreMissionAssessmentDrones",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<double>(
            name: "CoverageDistanceKm",
            table: "PreMissionAssessmentDrones",
            type: "double precision",
            nullable: false,
            defaultValue: 0.0);

        migrationBuilder.AddColumn<string>(
            name: "SuitabilityReason",
            table: "PreMissionAssessmentDrones",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name", table: "UAVs");
        migrationBuilder.DropColumn(name: "Category", table: "UAVs");
        migrationBuilder.DropColumn(name: "SerialNumber", table: "UAVs");
        migrationBuilder.DropColumn(name: "AssignedRegionId", table: "UAVs");
        migrationBuilder.DropColumn(name: "AssignedRegionName", table: "UAVs");
        migrationBuilder.DropColumn(name: "FlightEnduranceMinutes", table: "UAVs");
        migrationBuilder.DropColumn(name: "OperationalRangeKm", table: "UAVs");
        migrationBuilder.DropColumn(name: "CameraPayloadInfo", table: "UAVs");

        migrationBuilder.DropColumn(name: "Suitability", table: "PreMissionAssessmentDrones");
        migrationBuilder.DropColumn(name: "EstimatedFlightsNeeded", table: "PreMissionAssessmentDrones");
        migrationBuilder.DropColumn(name: "CoverageDistanceKm", table: "PreMissionAssessmentDrones");
        migrationBuilder.DropColumn(name: "SuitabilityReason", table: "PreMissionAssessmentDrones");
    }
}
