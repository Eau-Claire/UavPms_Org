-- Local development seed for the ERD v2.2 schema (see docs/database/ERD_v2_2_TARGET_SCHEMA.md).
-- Infrastructure is regions -> towers only (no substations, lines, management units or asset components).
-- Idempotent: every insert is guarded by ON CONFLICT, so the script can be re-run safely.
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 1. SEED ROLES
INSERT INTO "Roles" ("RoleName", "Description") VALUES
('SystemAdmin', 'Full system administrator'),
('Manager', 'Manager role for operational management'),
('Inspector', 'Inspector role for mission and inspection'),
('Analyst', 'Analyst role for monitoring and AI analysis'),
('Technician', 'Technician role for maintenance operations'),
('MaintenanceTechnician', 'Maintenance technician role for maintenance tasks')
ON CONFLICT ("RoleName") DO NOTHING;

-- 2. SEED MAIN USER: An3439201@gmail.com
INSERT INTO "Users" (
    "Id", "Email", "PasswordHash", "FullName", "Phone", "Status",
    "IsEmailVerified", "CreatedAt", "UpdatedAt", "IsDeleted"
) VALUES (
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    'An3439201@gmail.com',
    crypt('12345678', gen_salt('bf', 10)),
    'Nguyễn Nhật An',
    '0898520071',
    'Active',
    true,
    now(),
    now(),
    false
) ON CONFLICT ("Id") DO UPDATE SET
    "Email" = 'An3439201@gmail.com',
    "PasswordHash" = crypt('12345678', gen_salt('bf', 10)),
    "Status" = 'Active',
    "IsEmailVerified" = true,
    "IsDeleted" = false,
    "UpdatedAt" = now();

-- ASSIGN ALL ROLES TO MAIN USER
INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
SELECT '469bfac4-8b96-4f27-a772-945cff2fbaa8'::uuid, r."Id", now()
FROM "Roles" r
WHERE r."RoleName" IN ('SystemAdmin', 'Manager', 'Inspector', 'Analyst', 'Technician', 'MaintenanceTechnician')
ON CONFLICT DO NOTHING;

-- 3. SEED ADDITIONAL TEST USERS (FOR REGRESSION / WORKFLOW TESTING)
INSERT INTO "Users" (
    "Id", "Email", "PasswordHash", "FullName", "Phone", "Status",
    "IsEmailVerified", "CreatedAt", "UpdatedAt", "IsDeleted"
) VALUES
(
    '10000000-0000-0000-0000-000000000001',
    'admin@uavpms.com',
    crypt('12345678', gen_salt('bf', 10)),
    'System Admin',
    '0900000001',
    'Active',
    true,
    now(),
    now(),
    false
),
(
    '10000000-0000-0000-0000-000000000002',
    'manager@uavpms.com',
    crypt('12345678', gen_salt('bf', 10)),
    'Operations Manager',
    '0900000002',
    'Active',
    true,
    now(),
    now(),
    false
),
(
    '10000000-0000-0000-0000-000000000003',
    'inspector@uavpms.com',
    crypt('12345678', gen_salt('bf', 10)),
    'UAV Inspector',
    '0900000003',
    'Active',
    true,
    now(),
    now(),
    false
)
ON CONFLICT ("Id") DO UPDATE SET
    "PasswordHash" = crypt('12345678', gen_salt('bf', 10)),
    "Status" = 'Active',
    "IsEmailVerified" = true,
    "IsDeleted" = false;

INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
SELECT '10000000-0000-0000-0000-000000000001'::uuid, r."Id", now() FROM "Roles" r WHERE r."RoleName" = 'SystemAdmin'
ON CONFLICT DO NOTHING;

INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
SELECT '10000000-0000-0000-0000-000000000002'::uuid, r."Id", now() FROM "Roles" r WHERE r."RoleName" = 'Manager'
ON CONFLICT DO NOTHING;

INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
SELECT '10000000-0000-0000-0000-000000000003'::uuid, r."Id", now() FROM "Roles" r WHERE r."RoleName" = 'Inspector'
ON CONFLICT DO NOTHING;

-- 4. CORE GIS FOUNDATION & MASTER DATA
INSERT INTO "DefectCategories" ("CategoryCode", "CategoryName", "SeverityWeight", "IsEmergencyClass", "Description") VALUES 
('CORROSION', 'Ăn mòn', 0.7, false, 'Ăn mòn bề mặt thiết bị'),
('CRACK', 'Nứt vỡ', 0.9, true, 'Nứt vỡ kết cấu'),
('HOTSPOT', 'Điểm nóng', 1.0, true, 'Điểm nóng nhiệt'),
('VEGETATION', 'Cây cối xâm lấn', 0.6, false, 'Hành lang tuyến bị xâm lấn')
ON CONFLICT ("CategoryCode") DO NOTHING;

-- ERD v2.2: Regions.RegionName was renamed to "Name".
INSERT INTO "Regions" ("Id", "Name", "Code", "Type", "Geom", "CreatedAt", "IsDeleted") VALUES
('10000000-0000-0000-0000-000000000001', 'Khu vực Hà Nội demo', 'HN-DEMO', 'Region', ST_GeomFromText('POLYGON((105.75 20.98,105.95 20.98,105.95 21.12,105.75 21.12,105.75 20.98))', 4326), now(), false)
ON CONFLICT DO NOTHING;

-- ERD v2.2: towers belong directly to a region ("RegionId" NOT NULL); latitude/longitude live in "Geom".
INSERT INTO "Towers" ("Id", "RegionId", "TowerCode", "PoleType", "Address", "Geom", "CreatedAt", "IsDeleted") VALUES
('50000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'HN-T001', 'Steel lattice', 'Hà Nội demo - cột 1', ST_SetSRID(ST_MakePoint(105.80, 21.02), 4326), now(), false),
('50000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'HN-T002', 'Steel lattice', 'Hà Nội demo - cột 2', ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326), now(), false),
('50000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', 'HN-T003', 'Steel lattice', 'Hà Nội demo - cột 3', ST_SetSRID(ST_MakePoint(105.86, 21.07), 4326), now(), false)
ON CONFLICT DO NOTHING;

-- 5. USER GIS SCOPES (unique (UserId, RegionId) WHERE NOT IsDeleted, so no conflict target)
INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "IsDeleted")
SELECT md5(u."Id"::text || ':gis-demo-scope')::uuid, u."Id", '10000000-0000-0000-0000-000000000001'::uuid, now(), false
FROM "Users" u WHERE u."Status" = 'Active'
ON CONFLICT DO NOTHING;

-- 6. UAVS & MISSIONS
-- ERD v2.2: UAVs.Status was renamed to "FlightStatus" (DroneStatus enum as string).
INSERT INTO "UAVs" (
    "Id", "UavCode", "Name", "Model", "Category", "SerialNumber", "AssignedRegionId",
    "FlightStatus", "OperationalStatus", "TechnicalHealth",
    "FlightEnduranceMinutes", "OperationalRangeKm", "BatteryLevel", "CurrentLocation",
    "Version", "CreatedAt", "IsDeleted"
) VALUES (
    '70000000-0000-0000-0000-000000000001', 'UAV-HN-01', 'UAV Hà Nội 01', 'DJI Matrice 350 RTK', 'Quadcopter', 'SN-DEMO-HN-01',
    '10000000-0000-0000-0000-000000000001',
    'Idle', 'Available', 'Healthy',
    55, 15, 86, ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326),
    1, now(), false
) ON CONFLICT DO NOTHING;

-- ERD v2.2: Missions no longer have InspectorId / AssignedToUserId / DroneCode / RouteData /
-- ScheduledStartAt / IsOverdueNotified. The plan window is "PlannedStart"/"PlannedEnd" and the
-- inspector is a row in "MissionAssignments".
INSERT INTO "Missions" (
    "Id", "MissionCode", "Title", "Description", "ManagerId", "RegionId", "UavId",
    "Status", "MissionType", "PlannedStart", "PlannedEnd", "Version", "CreatedAt", "IsDeleted"
) VALUES (
    '80000000-0000-0000-0000-000000000001',
    'MIS-HN-DEMO-001',
    'Kiểm tra cột điện Hà Nội demo',
    'Nhiệm vụ kiểm tra định kỳ UAV',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    '10000000-0000-0000-0000-000000000001',
    '70000000-0000-0000-0000-000000000001',
    'Draft',
    'Scheduled',
    now() + interval '1 day',
    now() + interval '1 day 4 hours',
    1,
    now(),
    false
) ON CONFLICT DO NOTHING;

INSERT INTO "MissionAssignments" (
    "Id", "MissionId", "UserId", "AssignmentRole", "IsRequired", "Status", "ResponseStatus",
    "AssignedById", "AssignedAt", "Version", "CreatedAt", "IsDeleted"
) VALUES (
    'a0000000-0000-0000-0000-000000000001',
    '80000000-0000-0000-0000-000000000001',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    'INSPECTOR',
    true,
    'Active',
    'Pending',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    now(),
    1,
    now(),
    false
) ON CONFLICT DO NOTHING;

-- ERD v2.2: MissionTargets.AssetId became "TowerId" (FK Towers), unique (MissionId, TowerId).
INSERT INTO "MissionTargets" ("Id", "MissionId", "TowerId", "Sequence", "InspectionStatus", "CreatedAt", "IsDeleted") VALUES
('90000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', 1, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000002', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', 2, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000003', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000003', 3, 'Pending', now(), false)
ON CONFLICT DO NOTHING;
