-- ==========================================================================================
-- UAV-PMS: SCOPE REFINEMENT TO TP. HỒ CHÍ MINH (ROBUST DYNAMIC CLEAN & SEED)
-- Script: clean_and_seed_remote_hcm.sql
-- ==========================================================================================

BEGIN;

-- 1. DYNAMICALLY CLEAN UP ALL DEPENDENCIES
DO $$
DECLARE
    tbl text;
    tables_to_delete text[] := ARRAY[
        'MaintenanceProofs',
        'MaterialLogs',
        'AlertEscalations',
        'EmergencyAlerts',
        'MaintenanceTickets',
        'MissionPriorityDefects',
        'AIAnalysisRuns',
        'AIAnalysisRequests',
        'DetectedAnomalies',
        'IncidentReports',
        'InspectionMedia',
        'InspectionSchedules',
        'ReportMissions',
        'Reports',
        'MissionCheckIns',
        'MissionFlightLogs',
        'MissionCommunicationLogs',
        'MissionAssignments',
        'MissionTargets',
        'MissionTargetLines',
        'Missions',
        'PreMissionAssessmentDrones',
        'PreMissionAssessmentPersonnel',
        'PreMissionAssessmentTowers',
        'PreMissionAssessmentAssets',
        'PreMissionAssessments',
        'DroneFlightControllers',
        'DroneHandovers',
        'DroneTechnicalMetrics',
        'DroneTechnicalInspections',
        'ResourceBookings',
        'UserGeographicScopes',
        'AssetComponents',
        'AssetHealthHistories',
        'TowerConditionHistories',
        'LineSegments',
        'Towers',
        'TransmissionLines',
        'Substations',
        'Regions'
    ];
BEGIN
    UPDATE "UAVs" SET "AssignedRegionId" = NULL;

    FOREACH tbl IN ARRAY tables_to_delete
    LOOP
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = tbl) THEN
            EXECUTE 'DELETE FROM "' || tbl || '";';
        END IF;
    END LOOP;
END $$;

-- 2. DYNAMICALLY INSERT TP. HỒ CHÍ MINH REGIONS
DO $$
DECLARE
    has_region_name boolean;
    has_name boolean;
BEGIN
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Regions' AND column_name = 'RegionName') INTO has_region_name;
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Regions' AND column_name = 'Name') INTO has_name;

    IF has_region_name AND has_name THEN
        INSERT INTO "Regions" ("Id", "Name", "RegionName", "Code", "Type", "ParentId", "Geom", "CreatedAt", "IsDeleted")
        VALUES 
        ('10000000-0000-0000-0000-000000000001', 'TP. Hồ Chí Minh', 'TP. Hồ Chí Minh', 'HCM', 'City', NULL, ST_GeomFromText('POLYGON((106.50 10.60, 106.90 10.60, 106.90 11.00, 106.50 11.00, 106.50 10.60))', 4326), now(), false),
        ('10000000-0000-0000-0000-000000000101', 'Phường Bến Nghé, Quận 1', 'Phường Bến Nghé, Quận 1', 'HCM-Q1-BN', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7020, 10.7780), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000102', 'Phường Bến Thành, Quận 1', 'Phường Bến Thành, Quận 1', 'HCM-Q1-BT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6980, 10.7725), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000103', 'Phường Đa Kao, Quận 1', 'Phường Đa Kao, Quận 1', 'HCM-Q1-DK', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6970, 10.7870), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000104', 'Phường Tân Định, Quận 1', 'Phường Tân Định, Quận 1', 'HCM-Q1-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6900, 10.7910), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000105', 'Phường Võ Thị Sáu, Quận 3', 'Phường Võ Thị Sáu, Quận 3', 'HCM-Q3-VTS', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6880, 10.7820), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000106', 'Phường 1, Quận 3', 'Phường 1, Quận 3', 'HCM-Q3-P01', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6810, 10.7670), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000107', 'Phường Thảo Điền, TP. Thủ Đức', 'Phường Thảo Điền, TP. Thủ Đức', 'HCM-TDUC-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7320, 10.8030), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000108', 'Phường An Phú, TP. Thủ Đức', 'Phường An Phú, TP. Thủ Đức', 'HCM-TDUC-AP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7460, 10.7980), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000109', 'Phường Thủ Thiêm, TP. Thủ Đức', 'Phường Thủ Thiêm, TP. Thủ Đức', 'HCM-TDUC-TT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7180, 10.7720), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000110', 'Phường Linh Trung, TP. Thủ Đức', 'Phường Linh Trung, TP. Thủ Đức', 'HCM-TDUC-LT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7780, 10.8650), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000111', 'Phường Tân Phong, Quận 7', 'Phường Tân Phong, Quận 7', 'HCM-Q7-TP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000112', 'Phường Phú Mỹ, Quận 7', 'Phường Phú Mỹ, Quận 7', 'HCM-Q7-PM', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7350, 10.7180), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000113', 'Phường 25, Quận Bình Thạnh', 'Phường 25, Quận Bình Thạnh', 'HCM-BT-P25', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7150, 10.8040), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000114', 'Phường 2, Quận Tân Bình', 'Phường 2, Quận Tân Bình', 'HCM-TB-P02', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6650, 10.8120), 4326), now(), false);
    ELSIF has_name THEN
        INSERT INTO "Regions" ("Id", "Name", "Code", "Type", "ParentId", "Geom", "CreatedAt", "IsDeleted")
        VALUES 
        ('10000000-0000-0000-0000-000000000001', 'TP. Hồ Chí Minh', 'HCM', 'City', NULL, ST_GeomFromText('POLYGON((106.50 10.60, 106.90 10.60, 106.90 11.00, 106.50 11.00, 106.50 10.60))', 4326), now(), false),
        ('10000000-0000-0000-0000-000000000101', 'Phường Bến Nghé, Quận 1', 'HCM-Q1-BN', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7020, 10.7780), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000102', 'Phường Bến Thành, Quận 1', 'HCM-Q1-BT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6980, 10.7725), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000103', 'Phường Đa Kao, Quận 1', 'HCM-Q1-DK', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6970, 10.7870), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000104', 'Phường Tân Định, Quận 1', 'HCM-Q1-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6900, 10.7910), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000105', 'Phường Võ Thị Sáu, Quận 3', 'HCM-Q3-VTS', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6880, 10.7820), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000106', 'Phường 1, Quận 3', 'HCM-Q3-P01', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6810, 10.7670), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000107', 'Phường Thảo Điền, TP. Thủ Đức', 'HCM-TDUC-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7320, 10.8030), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000108', 'Phường An Phú, TP. Thủ Đức', 'HCM-TDUC-AP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7460, 10.7980), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000109', 'Phường Thủ Thiêm, TP. Thủ Đức', 'HCM-TDUC-TT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7180, 10.7720), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000110', 'Phường Linh Trung, TP. Thủ Đức', 'HCM-TDUC-LT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7780, 10.8650), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000111', 'Phường Tân Phong, Quận 7', 'HCM-Q7-TP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000112', 'Phường Phú Mỹ, Quận 7', 'HCM-Q7-PM', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7350, 10.7180), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000113', 'Phường 25, Quận Bình Thạnh', 'HCM-BT-P25', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7150, 10.8040), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000114', 'Phường 2, Quận Tân Bình', 'HCM-TB-P02', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6650, 10.8120), 4326), now(), false);
    ELSE
        INSERT INTO "Regions" ("Id", "RegionName", "Code", "Type", "ParentId", "Geom", "CreatedAt", "IsDeleted")
        VALUES 
        ('10000000-0000-0000-0000-000000000001', 'TP. Hồ Chí Minh', 'HCM', 'City', NULL, ST_GeomFromText('POLYGON((106.50 10.60, 106.90 10.60, 106.90 11.00, 106.50 11.00, 106.50 10.60))', 4326), now(), false),
        ('10000000-0000-0000-0000-000000000101', 'Phường Bến Nghé, Quận 1', 'HCM-Q1-BN', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7020, 10.7780), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000102', 'Phường Bến Thành, Quận 1', 'HCM-Q1-BT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6980, 10.7725), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000103', 'Phường Đa Kao, Quận 1', 'HCM-Q1-DK', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6970, 10.7870), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000104', 'Phường Tân Định, Quận 1', 'HCM-Q1-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6900, 10.7910), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000105', 'Phường Võ Thị Sáu, Quận 3', 'HCM-Q3-VTS', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6880, 10.7820), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000106', 'Phường 1, Quận 3', 'HCM-Q3-P01', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6810, 10.7670), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000107', 'Phường Thảo Điền, TP. Thủ Đức', 'HCM-TDUC-TD', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7320, 10.8030), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000108', 'Phường An Phú, TP. Thủ Đức', 'HCM-TDUC-AP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7460, 10.7980), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000109', 'Phường Thủ Thiêm, TP. Thủ Đức', 'HCM-TDUC-TT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7180, 10.7720), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000110', 'Phường Linh Trung, TP. Thủ Đức', 'HCM-TDUC-LT', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7780, 10.8650), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000111', 'Phường Tân Phong, Quận 7', 'HCM-Q7-TP', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000112', 'Phường Phú Mỹ, Quận 7', 'HCM-Q7-PM', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7350, 10.7180), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000113', 'Phường 25, Quận Bình Thạnh', 'HCM-BT-P25', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.7150, 10.8040), 4326), now(), false),
        ('10000000-0000-0000-0000-000000000114', 'Phường 2, Quận Tân Bình', 'HCM-TB-P02', 'Ward', '10000000-0000-0000-0000-000000000001', ST_SetSRID(ST_MakePoint(106.6650, 10.8120), 4326), now(), false);
    END IF;
END $$;

-- 3. INSERT REALISTIC TOWERS IN TP. HỒ CHÍ MINH
INSERT INTO "Towers" ("Id", "RegionId", "TowerCode", "PoleType", "Address", "Geom", "CreatedAt", "IsDeleted")
VALUES
-- Phường Bến Nghé, Quận 1
('50000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T01', 'Cột thép đơn thân', 'Số 15 Lê Duẩn, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7005, 10.7812), 4326), now(), false),
('50000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T02', 'Thép hình liên kết bu-lông', 'Đường Tôn Đức Thắng, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7062, 10.7778), 4326), now(), false),
('50000000-0000-0000-0000-000000000103', '10000000-0000-0000-0000-000000000101', 'HCM-Q1-T03', 'Cột thép đơn thân', 'Đường Nguyễn Bỉnh Khiêm, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7041, 10.7865), 4326), now(), false),

-- Phường Đa Kao, Quận 1
('50000000-0000-0000-0000-000000000104', '10000000-0000-0000-0000-000000000103', 'HCM-Q1-T04', 'Cột thép đơn thân', 'Đường Đinh Tiên Hoàng, Phường Đa Kao, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6975, 10.7885), 4326), now(), false),
('50000000-0000-0000-0000-000000000105', '10000000-0000-0000-0000-000000000103', 'HCM-Q1-T05', 'Thép hình liên kết bu-lông', 'Đường Điện Biên Phủ, Phường Đa Kao, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6942, 10.7892), 4326), now(), false),

-- Phường Tân Định, Quận 1
('50000000-0000-0000-0000-000000000106', '10000000-0000-0000-0000-000000000104', 'HCM-Q1-T06', 'Cột thép đơn thân', 'Đường Hai Bà Trưng, Phường Tân Định, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6908, 10.7915), 4326), now(), false),
('50000000-0000-0000-0000-000000000107', '10000000-0000-0000-0000-000000000104', 'HCM-Q1-T07', 'Cột thép đơn thân', 'Đường Hoàng Sa, Phường Tân Định, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6872, 10.7938), 4326), now(), false),

-- Phường Bến Thành, Quận 1
('50000000-0000-0000-0000-000000000108', '10000000-0000-0000-0000-000000000102', 'HCM-Q1-T08', 'Cột thép đơn thân', 'Đường Nguyễn Thị Nghĩa, Phường Bến Thành, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6925, 10.7718), 4326), now(), false),
('50000000-0000-0000-0000-000000000109', '10000000-0000-0000-0000-000000000102', 'HCM-Q1-T09', 'Cột thép đơn thân', 'Đường Cách Mạng Tháng Tám, Phường Bến Thành, Quận 1, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6902, 10.7735), 4326), now(), false),

-- Phường Võ Thị Sáu, Quận 3
('50000000-0000-0000-0000-000000000110', '10000000-0000-0000-0000-000000000105', 'HCM-Q3-T01', 'Cột thép đơn thân', 'Đường Nam Kỳ Khởi Nghĩa, Phường Võ Thị Sáu, Quận 3, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6892, 10.7845), 4326), now(), false),
('50000000-0000-0000-0000-000000000111', '10000000-0000-0000-0000-000000000105', 'HCM-Q3-T02', 'Cột thép đơn thân', 'Đường Võ Văn Tần, Phường Võ Thị Sáu, Quận 3, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6855, 10.7792), 4326), now(), false),
('50000000-0000-0000-0000-000000000112', '10000000-0000-0000-0000-000000000105', 'HCM-Q3-T03', 'Thép hình liên kết bu-lông', 'Đường Nguyễn Thị Minh Khai, Phường Võ Thị Sáu, Quận 3, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6832, 10.7758), 4326), now(), false),

-- Phường 1, Quận 3
('50000000-0000-0000-0000-000000000113', '10000000-0000-0000-0000-000000000106', 'HCM-Q3-T04', 'Cột thép đơn thân', 'Đường Lý Thái Tổ, Phường 1, Quận 3, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6785, 10.7672), 4326), now(), false),

-- Phường Thủ Thiêm, TP. Thủ Đức
('50000000-0000-0000-0000-000000000114', '10000000-0000-0000-0000-000000000109', 'HCM-TT-T01', 'Thép hình liên kết bu-lông', 'Đại lộ Vòng Cung, Phường Thủ Thiêm, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7142, 10.7738), 4326), now(), false),
('50000000-0000-0000-0000-000000000115', '10000000-0000-0000-0000-000000000109', 'HCM-TT-T02', 'Thép hình liên kết bu-lông', 'Tuyến Mai Chí Thọ, Phường Thủ Thiêm, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7195, 10.7705), 4326), now(), false),
('50000000-0000-0000-0000-000000000116', '10000000-0000-0000-0000-000000000109', 'HCM-TT-T03', 'Thép hình liên kết bu-lông', 'Đường Nguyễn Cơ Thạch, Phường Thủ Thiêm, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7235, 10.7752), 4326), now(), false),

-- Phường An Phú, TP. Thủ Đức
('50000000-0000-0000-0000-000000000117', '10000000-0000-0000-0000-000000000108', 'HCM-AP-T01', 'Thép hình liên kết bu-lông', 'Xa Lộ Hà Nội, Phường An Phú, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7455, 10.8015), 4326), now(), false),
('50000000-0000-0000-0000-000000000118', '10000000-0000-0000-0000-000000000108', 'HCM-AP-T02', 'Thép hình liên kết bu-lông', 'Tuyến Mai Chí Thọ, Phường An Phú, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7482, 10.7932), 4326), now(), false),

-- Phường Thảo Điền, TP. Thủ Đức
('50000000-0000-0000-0000-000000000119', '10000000-0000-0000-0000-000000000107', 'HCM-TD-T01', 'Cột thép đơn thân', 'Đường Nguyễn Văn Hưởng, Phường Thảo Điền, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7352, 10.8095), 4326), now(), false),
('50000000-0000-0000-0000-000000000120', '10000000-0000-0000-0000-000000000107', 'HCM-TD-T02', 'Cột thép đơn thân', 'Đường Quốc Hương, Phường Thảo Điền, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7315, 10.8035), 4326), now(), false),

-- Phường Linh Trung, TP. Thủ Đức
('50000000-0000-0000-0000-000000000121', '10000000-0000-0000-0000-000000000110', 'HCM-LT-T01', 'Thép hình liên kết bu-lông', 'Quốc lộ 1A, Phường Linh Trung, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7765, 10.8642), 4326), now(), false),
('50000000-0000-0000-0000-000000000122', '10000000-0000-0000-0000-000000000110', 'HCM-LT-T02', 'Thép hình liên kết bu-lông', 'Đường số 14, Phường Linh Trung, TP. Thủ Đức, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7825, 10.8698), 4326), now(), false),

-- Phường Tân Phong, Quận 7
('50000000-0000-0000-0000-000000000123', '10000000-0000-0000-0000-000000000111', 'HCM-Q7-T01', 'Thép hình liên kết bu-lông', 'Đại lộ Nguyễn Văn Linh, Phường Tân Phong, Quận 7, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7045, 10.7315), 4326), now(), false),
('50000000-0000-0000-0000-000000000124', '10000000-0000-0000-0000-000000000111', 'HCM-Q7-T02', 'Cột thép đơn thân', 'Đường Nguyễn Thị Thập, Phường Tân Phong, Quận 7, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7082, 10.7382), 4326), now(), false),

-- Phường Phú Mỹ, Quận 7
('50000000-0000-0000-0000-000000000125', '10000000-0000-0000-0000-000000000112', 'HCM-Q7-T03', 'Thép hình liên kết bu-lông', 'Đường Hoàng Quốc Việt, Phường Phú Mỹ, Quận 7, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7312, 10.7185), 4326), now(), false),
('50000000-0000-0000-0000-000000000126', '10000000-0000-0000-0000-000000000112', 'HCM-Q7-T04', 'Thép hình liên kết bu-lông', 'Đường Huỳnh Tấn Phát, Phường Phú Mỹ, Quận 7, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7365, 10.7142), 4326), now(), false),

-- Phường 25, Quận Bình Thạnh
('50000000-0000-0000-0000-000000000127', '10000000-0000-0000-0000-000000000113', 'HCM-BT-T01', 'Thép hình liên kết bu-lông', 'Đường Điện Biên Phủ, Phường 25, Quận Bình Thạnh, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7148, 10.8012), 4326), now(), false),
('50000000-0000-0000-0000-000000000128', '10000000-0000-0000-0000-000000000113', 'HCM-BT-T02', 'Cột thép đơn thân', 'Đường Xô Viết Nghệ Tĩnh, Phường 25, Quận Bình Thạnh, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.7175, 10.8065), 4326), now(), false),

-- Phường 2, Quận Tân Bình
('50000000-0000-0000-0000-000000000129', '10000000-0000-0000-0000-000000000114', 'HCM-TB-T01', 'Cột thép đơn thân', 'Đường Trường Sơn, Phường 2, Quận Tân Bình, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6645, 10.8125), 4326), now(), false),
('50000000-0000-0000-0000-000000000130', '10000000-0000-0000-0000-000000000114', 'HCM-TB-T02', 'Cột thép đơn thân', 'Đường Bạch Đằng, Phường 2, Quận Tân Bình, TP. Hồ Chí Minh', ST_SetSRID(ST_MakePoint(106.6698, 10.8168), 4326), now(), false);

-- 4. REASSIGN USER GEOGRAPHIC SCOPES FOR ALL ACTIVE USERS TO TP. HỒ CHÍ MINH
INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "IsDeleted")
SELECT 
    md5(u."Id"::text || ':hcm-root')::uuid,
    u."Id",
    '10000000-0000-0000-0000-000000000001'::uuid,
    now(),
    false
FROM "Users" u WHERE u."Status" = 'Active' AND NOT u."IsDeleted"
ON CONFLICT DO NOTHING;

INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "IsDeleted")
SELECT 
    md5(u."Id"::text || ':' || r."Id"::text)::uuid,
    u."Id",
    r."Id",
    now(),
    false
FROM "Users" u
CROSS JOIN "Regions" r
WHERE u."Status" = 'Active' AND NOT u."IsDeleted" AND r."ParentId" IS NOT NULL
ON CONFLICT DO NOTHING;

-- 5. REASSIGN & UPDATE ALL 11 UAVS TO TP. HỒ CHÍ MINH
UPDATE "UAVs" SET "UavCode" = 'TMP-' || "Id"::text;

DO $$
DECLARE
    has_status boolean;
    has_flight_status boolean;
BEGIN
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'UAVs' AND column_name = 'Status') INTO has_status;
    SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'UAVs' AND column_name = 'FlightStatus') INTO has_flight_status;

    IF has_status THEN
        EXECUTE 'UPDATE "UAVs" SET "Status" = ''Idle''';
    END IF;
    IF has_flight_status THEN
        EXECUTE 'UPDATE "UAVs" SET "FlightStatus" = ''Idle''';
    END IF;
END $$;

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-01',
    "Name" = 'DJI Matrice 350 RTK - Alpha 01',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 98,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7005, 10.7812), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000001';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-02',
    "Name" = 'DJI Matrice 350 RTK - Alpha 02',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 95,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7142, 10.7738), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000002';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-03',
    "Name" = 'DJI Matrice 300 RTK - Beta 01',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 92,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.6980, 10.7725), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000003';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-04',
    "Name" = 'DJI Mavic 3 Enterprise - Rapid',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 89,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.6880, 10.7820), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000004';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-05',
    "Name" = 'Autel EVO Max 4T - Eagle',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 94,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000005';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-06',
    "Name" = 'DJI Inspire 3 - Precision RTK',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 91,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7320, 10.8030), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000006';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-07',
    "Name" = 'Freefly Alta X - Heavy Lift',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 88,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7150, 10.8040), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000007';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-08',
    "Name" = 'DJI Matrice 350 RTK - Metro 01',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 96,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.6650, 10.8120), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000008';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-09',
    "Name" = 'DJI Matrice 30T - Metro 02',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 90,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7460, 10.7980), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-000000000009';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-10',
    "Name" = 'DJI Matrice 350 RTK - Nam Sài Gòn',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 97,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.7050, 10.7300), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-00000000000a';

UPDATE "UAVs" SET
    "UavCode" = 'UAV-HCM-11',
    "Name" = 'DJI Mavic 3 Thermal - Trung Tâm',
    "AssignedRegionId" = '10000000-0000-0000-0000-000000000001',
    "OperationalStatus" = 'Available',
    "TechnicalHealth" = 'Healthy',
    "BatteryLevel" = 93,
    "CurrentLocation" = ST_SetSRID(ST_MakePoint(106.6970, 10.7870), 4326),
    "UpdatedAt" = now()
WHERE "Id" = '70000000-0000-0000-0000-00000000000b';

COMMIT;
