using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErdV2_2ConstraintsAndColumnLengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Every narrowed column must already fit: list all offenders at once instead of failing on the first ALTER.
            migrationBuilder.Sql("""
                DO $$
                DECLARE r record; max_len int; offenders text := '';
                BEGIN
                    FOR r IN SELECT * FROM (VALUES
                    ('Users', 'Status', 20),
                    ('Roles', 'RoleName', 50),
                    ('RefreshTokens', 'TokenHash', 128),
                    ('TrustedDevices', 'DeviceTokenHash', 128),
                    ('UAVs', 'FlightStatus', 20),
                    ('UAVs', 'OperationalStatus', 20),
                    ('UAVs', 'TechnicalHealth', 20),
                    ('DroneTechnicalInspections', 'Status', 20),
                    ('DroneTechnicalInspections', 'Health', 20),
                    ('DroneTechnicalInspections', 'SourceType', 20),
                    ('DroneTechnicalInspections', 'PolicyVersion', 50),
                    ('DroneTechnicalMetrics', 'Severity', 20),
                    ('PreMissionAssessments', 'Status', 20),
                    ('PreMissionAssessments', 'SiteFeasibilityStatus', 20),
                    ('PreMissionAssessments', 'OverallTechnicalHealth', 20),
                    ('PreMissionAssessments', 'EvaluationPolicyVersion', 50),
                    ('PreMissionAssessmentPersonnel', 'AvailabilityStatus', 20),
                    ('PreMissionAssessmentPersonnel', 'EligibilityStatus', 20),
                    ('PreMissionAssessmentPersonnel', 'ReasonCode', 50),
                    ('PreMissionAssessmentDrones', 'OperationalAvailabilityStatus', 20),
                    ('PreMissionAssessmentDrones', 'TechnicalEligibilityStatus', 20),
                    ('PreMissionAssessmentDrones', 'TechnicalHealth', 20),
                    ('PreMissionAssessmentDrones', 'Suitability', 20),
                    ('PreMissionAssessmentDrones', 'ReasonCode', 50),
                    ('Missions', 'MissionCode', 50),
                    ('Missions', 'Objective', 30),
                    ('Missions', 'MissionType', 20),
                    ('Missions', 'Status', 30),
                    ('MissionTargets', 'InspectionStatus', 20),
                    ('MissionAssignments', 'Status', 20),
                    ('MissionAssignments', 'ResponseStatus', 20),
                    ('ResourceBookings', 'Status', 20),
                    ('MissionCheckIns', 'Status', 20),
                    ('DroneHandovers', 'Status', 20),
                    ('MissionFlightLogs', 'ConnectionStatus', 20),
                    ('IncidentReports', 'Status', 20),
                    ('InspectionMedia', 'MediaType', 10),
                    ('InspectionMedia', 'FileUrl', 2048),
                    ('InspectionMedia', 'ValidationStatus', 20),
                    ('EmergencyAlerts', 'Status', 20),
                    ('MaintenanceTickets', 'TicketCode', 50),
                    ('MaintenanceTickets', 'Status', 30),
                    ('MaintenanceTickets', 'Priority', 20),
                    ('MaintenanceProofs', 'FileUrl', 2048),
                    ('Notifications', 'Type', 50),
                    ('Notifications', 'ReferenceType', 50),
                    ('AuditLogs', 'TableName', 100),
                    ('AuditLogs', 'ActionType', 20)
                    ) AS v(tbl, col, len)
                    LOOP
                        EXECUTE format('SELECT max(length(%I)) FROM %I', r.col, r.tbl) INTO max_len;
                        IF max_len > r.len THEN
                            offenders := offenders || format(' %s.%s (max %s > %s);', r.tbl, r.col, max_len, r.len);
                        END IF;
                    END LOOP;
                    IF offenders <> '' THEN
                        RAISE EXCEPTION 'Values too long for the ERD v2.2 column lengths:%', offenders;
                    END IF;
                END $$;
                """);

            // 2. One Active alert per anomaly: keep the newest, resolve older duplicates (rows are kept).
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", row_number() OVER (PARTITION BY "AnomalyId" ORDER BY "TriggeredAt" DESC, "CreatedAt" DESC, "Id") AS rn
                    FROM "EmergencyAlerts"
                    WHERE "Status" = 'Active' AND NOT "IsDeleted" AND "AnomalyId" IS NOT NULL
                )
                UPDATE "EmergencyAlerts" a
                SET "Status" = 'Resolved',
                    "ResolvedAt" = COALESCE(a."ResolvedAt", now()),
                    "UpdatedAt" = now(),
                    "Notes" = concat_ws(' ', a."Notes", '[auto] duplicate Active alert resolved by migration AddErdV2_2ConstraintsAndColumnLengths')
                FROM ranked
                WHERE a."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalHealth",
                table: "UAVs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "OperationalStatus",
                table: "UAVs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FlightStatus",
                table: "UAVs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceTokenHash",
                table: "TrustedDevices",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ResourceBookings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PreMissionAssessments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SiteFeasibilityStatus",
                table: "PreMissionAssessments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "OverallTechnicalHealth",
                table: "PreMissionAssessments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EvaluationPolicyVersion",
                table: "PreMissionAssessments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentPersonnel",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EligibilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AvailabilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalHealth",
                table: "PreMissionAssessmentDrones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalEligibilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Suitability",
                table: "PreMissionAssessmentDrones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentDrones",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OperationalAvailabilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "InspectionStatus",
                table: "MissionTargets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Missions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "Missions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "PeriodicInspection",
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "PeriodicInspection");

            migrationBuilder.AlterColumn<string>(
                name: "MissionType",
                table: "Missions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "MissionCode",
                table: "Missions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionStatus",
                table: "MissionFlightLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MissionCheckIns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MissionAssignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ResponseStatus",
                table: "MissionAssignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TicketCode",
                table: "MaintenanceTickets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MaintenanceTickets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "MaintenanceTickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "MaintenanceProofs",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ValidationStatus",
                table: "InspectionMedia",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                table: "InspectionMedia",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "InspectionMedia",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IncidentReports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EmergencyAlerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "DroneTechnicalMetrics",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DroneTechnicalInspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "DroneTechnicalInspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PolicyVersion",
                table: "DroneTechnicalInspections",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "DroneTechnicalInspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DroneHandovers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                table: "AuditLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Legacy rows that break the rule keep the constraint NOT VALID (still enforced for new rows).
            migrationBuilder.Sql("""
                ALTER TABLE "InspectionSchedules" ADD CONSTRAINT "CK_InspectionSchedules_RecurrenceRule" CHECK (btrim("RecurrenceRule") <> '') NOT VALID;
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "InspectionSchedules" WHERE NOT (btrim("RecurrenceRule") <> '')) THEN
                        ALTER TABLE "InspectionSchedules" VALIDATE CONSTRAINT "CK_InspectionSchedules_RecurrenceRule";
                    ELSE
                        RAISE NOTICE 'CK_InspectionSchedules_RecurrenceRule left NOT VALID: legacy rows violate it';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_EmergencyAlerts_ActivePerAnomaly",
                table: "EmergencyAlerts",
                column: "AnomalyId",
                unique: true,
                filter: "\"Status\" = 'Active' AND NOT \"IsDeleted\" AND \"AnomalyId\" IS NOT NULL");

            // Legacy rows that break the rule keep the constraint NOT VALID (still enforced for new rows).
            migrationBuilder.Sql("""
                ALTER TABLE "AlertEscalations" ADD CONSTRAINT "CK_AlertEscalations_DifferentUsers" CHECK ("EscalatedBy" <> "EscalatedTo") NOT VALID;
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM "AlertEscalations" WHERE NOT ("EscalatedBy" <> "EscalatedTo")) THEN
                        ALTER TABLE "AlertEscalations" VALIDATE CONSTRAINT "CK_AlertEscalations_DifferentUsers";
                    ELSE
                        RAISE NOTICE 'CK_AlertEscalations_DifferentUsers left NOT VALID: legacy rows violate it';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InspectionSchedules_RecurrenceRule",
                table: "InspectionSchedules");

            migrationBuilder.DropIndex(
                name: "UX_EmergencyAlerts_ActivePerAnomaly",
                table: "EmergencyAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AlertEscalations_DifferentUsers",
                table: "AlertEscalations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalHealth",
                table: "UAVs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "OperationalStatus",
                table: "UAVs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FlightStatus",
                table: "UAVs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceTokenHash",
                table: "TrustedDevices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "RoleName",
                table: "Roles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ResourceBookings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PreMissionAssessments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "SiteFeasibilityStatus",
                table: "PreMissionAssessments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "OverallTechnicalHealth",
                table: "PreMissionAssessments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "EvaluationPolicyVersion",
                table: "PreMissionAssessments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EligibilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "AvailabilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalHealth",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TechnicalEligibilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Suitability",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OperationalAvailabilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceType",
                table: "Notifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "InspectionStatus",
                table: "MissionTargets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Missions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Objective",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "PeriodicInspection",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "PeriodicInspection");

            migrationBuilder.AlterColumn<string>(
                name: "MissionType",
                table: "Missions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "MissionCode",
                table: "Missions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ConnectionStatus",
                table: "MissionFlightLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MissionCheckIns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MissionAssignments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ResponseStatus",
                table: "MissionAssignments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TicketCode",
                table: "MaintenanceTickets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MaintenanceTickets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "MaintenanceTickets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "MaintenanceProofs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "ValidationStatus",
                table: "InspectionMedia",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "MediaType",
                table: "InspectionMedia",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "InspectionMedia",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "IncidentReports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EmergencyAlerts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Severity",
                table: "DroneTechnicalMetrics",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyVersion",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Health",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DroneHandovers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "TableName",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ActionType",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }
    }
}
