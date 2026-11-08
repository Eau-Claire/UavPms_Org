-- Local development seed for the ERD v2.2 schema (see docs/database/ERD_v2_2_TARGET_SCHEMA.md).
-- Scope: Exclusively TP. Hồ Chí Minh.
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
('VEGETATION', 'Cây cối xâm lấn', 0.6, false, 'Hành lang tuyến bị xâm lấn'),
-- Codes sent by the server AI bridge (bridge/categories.json): bird_nest -> BIRD_NEST;
-- insulator_broken, insulator_flashover, foreign_object, broken_strand -> BROKEN_INSULATOR
('BIRD_NEST', 'Tổ chim', 0.6, false, 'Tổ chim trên cột hoặc chuỗi cách điện'),
('BROKEN_INSULATOR', 'Bát cách điện vỡ', 0.9, true, 'Hư hỏng cách điện/dây dẫn do AI phát hiện (vỡ, phóng điện, vật lạ, đứt sợi)')
ON CONFLICT ("CategoryCode") DO NOTHING;

-- Synchronize Region column aliases
ALTER TABLE "Regions" ADD COLUMN IF NOT EXISTS "Name" text;
ALTER TABLE "Regions" ADD COLUMN IF NOT EXISTS "RegionName" text;
ALTER TABLE "Towers" ALTER COLUMN "LineAssetId" DROP NOT NULL;

-- Regions: TP. Hồ Chí Minh (City & Wards)
INSERT INTO "Regions" ("Id", "Name", "RegionName", "Code", "Type", "Geom", "CreatedAt", "IsDeleted") VALUES
('10000000-0000-0000-0000-000000000001', 'TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 'HCM', 'City', ST_GeomFromText('POLYGON((106.50 10.60,106.90 10.60,106.90 11.00,106.50 11.00,106.50 10.60))', 4326), now(), false)
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO "Regions" ("Id", "Name", "RegionName", "Code", "Type", "ParentId", "Geom", "CreatedAt", "IsDeleted") VALUES
('10000000-0000-0000-0000-000000000101', 'Phường Bến Nghé, Quận 1', 'Phường Bến Nghé, Quận 1', 'HCM-Q1-BN', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7020, 10.7780), 4326), now(), false),
('10000000-0000-0000-0000-000000000102', 'Phường Bến Thành, Quận 1', 'Phường Bến Thành, Quận 1', 'HCM-Q1-BT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6980, 10.7725), 4326), now(), false),
('10000000-0000-0000-0000-000000000105', 'Phường Võ Thị Sáu, Quận 3', 'Phường Võ Thị Sáu, Quận 3', 'HCM-Q3-VTS', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6880, 10.7820), 4326), now(), false),
('10000000-0000-0000-0000-000000000107', 'Phường Thảo Điền, TP. Thủ Đức', 'Phường Thảo Điền, TP. Thủ Đức', 'HCM-TDUC-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7320, 10.8030), 4326), now(), false),
('10000000-0000-0000-0000-000000000109', 'Phường Thủ Thiêm, TP. Thủ Đức', 'Phường Thủ Thiêm, TP. Thủ Đức', 'HCM-TDUC-TT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7180, 10.7720), 4326), now(), false),
('10000000-0000-0000-0000-000000000111', 'Phường Tân Phong, Quận 7', 'Phường Tân Phong, Quận 7', 'HCM-Q7-TP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326), now(), false)
ON CONFLICT ("Code") DO NOTHING;

-- Towers in TP. Hồ Chí Minh
INSERT INTO "Towers" ("Id", "RegionId", "TowerCode", "PoleType", "Address", "Geom", "CreatedAt", "IsDeleted") VALUES
('50000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T01', 'Cột thép đơn thân', 'Số 15 Lê Duẩn, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7005, 10.7812), 4326), now(), false),
('50000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T02', 'Thép hình liên kết bu-lông', 'Đường Tôn Đức Thắng, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7062, 10.7778), 4326), now(), false),
('50000000-0000-0000-0000-000000000103', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T03', 'Cột thép đơn thân', 'Đường Nguyễn Bỉnh Khiêm, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7041, 10.7865), 4326), now(), false)
ON CONFLICT ("Id") DO NOTHING;

-- 5. USER GIS SCOPES
INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "IsDeleted")
SELECT md5(u."Id"::text || ':hcm-root')::uuid, u."Id", '10000000-0000-0000-0000-000000000001'::uuid, now(), false
FROM "Users" u WHERE u."Status" = 'Active'
ON CONFLICT DO NOTHING;

-- 6. UAVS & MISSIONS IN TP. HỒ CHÍ MINH
INSERT INTO "UAVs" (
    "Id", "UavCode", "Name", "Model", "Category", "SerialNumber", "AssignedRegionId",
    "FlightStatus", "OperationalStatus", "TechnicalHealth",
    "FlightEnduranceMinutes", "OperationalRangeKm", "BatteryLevel", "CurrentLocation",
    "Version", "CreatedAt", "IsDeleted"
) VALUES (
    '70000000-0000-0000-0000-000000000001', 'UAV-HCM-01', 'DJI Matrice 350 RTK - Alpha 01', 'DJI Matrice 350 RTK', 'Quadcopter', 'SN-DEMO-HCM-01',
    '10000000-0000-0000-0000-000000000001',
    'Idle', 'Available', 'Healthy',
    55, 15, 98, ST_SetSRID(ST_MakePoint(106.7005, 10.7812), 4326),
    1, now(), false
) ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Missions" (
    "Id", "MissionCode", "Title", "Description", "ManagerId", "RegionId", "UavId",
    "Status", "MissionType", "PlannedStart", "PlannedEnd", "Version", "CreatedAt", "IsDeleted"
) VALUES (
    '80000000-0000-0000-0000-000000000001',
    'MIS-HCM-001',
    'Kiểm tra lưới điện Quận 1 - TP. Hồ Chí Minh',
    'Nhiệm vụ kiểm tra định kỳ UAV',
    '469bfac4-8b96-4f27-a772-945cff2fbaa8',
    '10000000-0000-0000-0000-000000000101',
    '70000000-0000-0000-0000-000000000001',
    'Draft',
    'Scheduled',
    now() + interval '1 day',
    now() + interval '1 day 4 hours',
    1,
    now(),
    false
) ON CONFLICT ("Id") DO NOTHING;

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
) ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "MissionTargets" ("Id", "MissionId", "TowerId", "Sequence", "InspectionStatus", "CreatedAt", "IsDeleted") VALUES
('90000000-0000-0000-0000-000000000001', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000101', 1, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000002', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000102', 2, 'Pending', now(), false),
('90000000-0000-0000-0000-000000000003', '80000000-0000-0000-0000-000000000001', '50000000-0000-0000-0000-000000000103', 3, 'Pending', now(), false)
ON CONFLICT ("Id") DO NOTHING;
