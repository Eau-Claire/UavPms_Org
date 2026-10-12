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
    "Id", "Username", "Email", "PasswordHash", "FullName", "Phone", "Status",
    "IsEmailVerified", "CreatedAt", "UpdatedAt", "IsDeleted"
) VALUES (
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    'an3439201',
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
    "Id", "Username", "Email", "PasswordHash", "FullName", "Phone", "Status",
    "IsEmailVerified", "CreatedAt", "UpdatedAt", "IsDeleted"
) VALUES 
(
    '10000000-0000-0000-0000-000000000001',
    'admin',
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
    'manager',
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
    'inspector',
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

INSERT INTO "Regions" ("Id", "RegionName", "Code", "Type", "Geom", "CreatedAt", "IsDeleted") VALUES 
('10000000-0000-0000-0000-000000000001', 'Khu vực Hà Nội demo', 'HN-DEMO', 'Region', ST_GeomFromText('POLYGON((105.75 20.98,105.95 20.98,105.95 21.12,105.75 21.12,105.75 20.98))', 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "ManagementUnits" ("Id", "Code", "Name", "Type", "Status", "CreatedAt", "IsDeleted") VALUES 
('20000000-0000-0000-0000-000000000001', 'EVN-HN-DEMO', 'Đơn vị quản lý Hà Nội demo', 'PowerCompany', 'Active', now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Substations" ("Id", "RegionAssetId", "SubstationName", "VoltageLevel", "Geom", "CreatedAt", "IsDeleted") VALUES 
('30000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Trạm biến áp Hà Nội demo', '220kV', ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "TransmissionLines" ("Id", "SubstationAssetId", "LineName", "Code", "VoltageLevel", "ManagementUnitId", "Status", "IsCriticalEdge", "Geom", "CreatedAt", "IsDeleted") VALUES 
('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'Đường dây 220kV Hà Nội demo', 'HN-220-01', '220kV', '20000000-0000-0000-0000-000000000001', 'Active', true, ST_GeomFromText('LINESTRING(105.80 21.02,105.82 21.04,105.86 21.07)', 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Towers" ("Id", "LineAssetId", "TowerCode", "Geom", "CreatedAt", "IsDeleted") VALUES 
('50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000001', 'HN-T001', ST_SetSRID(ST_MakePoint(105.80, 21.02), 4326), now(), false),
('50000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000001', 'HN-T002', ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326), now(), false),
('50000000-0000-0000-0000-000000000003', '40000000-0000-0000-0000-000000000001', 'HN-T003', ST_SetSRID(ST_MakePoint(105.86, 21.07), 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "AssetComponents" ("Id", "TowerId", "PowerLineId", "ManagementUnitId", "Location", "ComponentType", "ComponentCode", "Status", "CurrentHealthScore", "RiskLevel", "CreatedAt", "IsDeleted") VALUES 
('60000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(105.80, 21.02), 4326), 'Insulator', 'INS-HN-001', 'Active', 92, 'Low Risk', now(), false),
('60000000-0000-0000-0000-000000000002', '50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326), 'Conductor', 'CON-HN-002', 'Operational', 73, 'Medium Risk', now(), false),
('60000000-0000-0000-0000-000000000003', '50000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(105.86, 21.07), 4326), 'Transformer', 'TRF-HN-003', 'Active', 45, 'High Risk', now(), false)
ON CONFLICT ("Id") DO NOTHING;

-- 5. USER GIS SCOPES
INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "IsDeleted")
SELECT md5(u."Id"::text || ':gis-demo-scope')::uuid, u."Id", '10000000-0000-0000-0000-000000000001'::uuid, now(), false 
FROM "Users" u WHERE u."Status" = 'Active'
ON CONFLICT ("Id") DO NOTHING;

-- 6. UAVS & MISSIONS
INSERT INTO "UAVs" ("Id", "UavCode", "Model", "Status", "BatteryLevel", "CurrentLocation", "CreatedAt", "IsDeleted") VALUES 
('70000000-0000-0000-0000-000000000001', 'UAV-HN-01', 'DJI Matrice 350 RTK', 'Idle', 86, ST_SetSRID(ST_MakePoint(105.82, 21.04), 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Missions" (
    "Id", "MissionCode", "Title", "ManagerId", "InspectorId", "AssignedToUserId",
    "UavId", "DroneCode", "RouteData", "Status", "ScheduledStartAt", "Description",
    "MissionType", "Version", "IsOverdueNotified", "CreatedAt", "IsDeleted"
) VALUES (
    '80000000-0000-0000-0000-000000000001',
    'MIS-HN-DEMO-001',
    'Kiểm tra đường dây Hà Nội demo',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    '70000000-0000-0000-0000-000000000001',
    'UAV-HN-01',
    '{}',
    'Pending',
    now() + interval '1 day',
    'Nhiệm vụ kiểm tra định kỳ UAV',
    'Scheduled',
    0,
    false,
    now(),
    false
) ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "MissionTargets" ("Id", "MissionId", "AssetId", "Sequence", "InspectionStatus", "CreatedAt", "IsDeleted") VALUES 
('90000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', '60000000-0000-0000-0000-000000000001', 1, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000002', '80000000-0000-0000-0000-000000000001', '60000000-0000-0000-0000-000000000002', 2, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000003', '80000000-0000-0000-0000-000000000001', '60000000-0000-0000-0000-000000000003', 3, 'Pending', now(), false)
ON CONFLICT ("Id") DO NOTHING;
