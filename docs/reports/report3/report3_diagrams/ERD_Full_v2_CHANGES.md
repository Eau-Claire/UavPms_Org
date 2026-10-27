# ERD v2 – UAV-PMS Full Physical ERD: thay đổi và quy ước

| Tệp | Nội dung |
| :-- | :-- |
| `ERD_Full_v2.mmd` | Mã nguồn Mermaid (`erDiagram`), nguồn chuẩn để chỉnh sửa |
| `ERD_Full_v2.svg` / `ERD_Full_v2.png` | Bản render (Mermaid 11.6, layout ELK) |
| `ERD_Diagrams-ERD_Full.drawio.svg` | ERD cũ (v1), giữ nguyên để đối chiếu |

**Nguồn sự thật:** `Services/OperationsService/UavPms.OperationsService.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (một CSDL PostgreSQL + PostGIS dùng chung cho các service). Riêng cột `Users.PasswordHash` lấy từ IdentityService, vì OperationsService không map cột này.

**Mở trong draw.io:** *Arrange → Insert → Advanced → Mermaid*, dán nội dung `ERD_Full_v2.mmd`. draw.io chuyển sơ đồ sang shape gốc, giống cách ERD v1 đã được tạo.

---

## 1. Tóm tắt

| | ERD v1 | ERD v2 |
| :-- | :-: | :-: |
| Số bảng | 28 | **42** (v2.2, xem mục 1a–1b; bản v2 đầu có 50) |
| Phạm vi | MF01–MF04, một phần | Toàn hệ thống: Identity, hạ tầng (khu vực → cột điện), đội UAV, MF01–MF04, bảo trì, báo cáo, thông báo, audit, cấu hình, outbox |
| Khớp với DB | Nhiều cột không tồn tại, thiếu nhiều cột thật | Khớp snapshot EF, có các chỉnh sửa theo best practice (mục 4–6) và các quyết định rút gọn ở v2.1–v2.2 (mục 1a–1d) |

## 1a. Cập nhật v2.1–v2.2: hạ tầng chỉ còn khu vực → cột điện

**Quyết định:**
- Phân cấp hạ tầng chỉ còn `regions → towers`. **Cột điện là đơn vị nhỏ nhất** được quản lý. Không dùng trạm biến áp, đường dây, đơn vị quản lý hay linh kiện trên cột (`assets`).
- Bỏ tính năng tính sức khỏe và mức rủi ro của cột điện. Màn hình chi tiết cột điện chỉ hiển thị **lịch sử bảo trì** và **danh sách bất thường** (`detected_anomalies`).

**Bảng đã xóa (8):**

| Bảng | Lý do |
| :-- | :-- |
| `substations` | Bỏ tầng trạm biến áp |
| `transmission_lines` | Bỏ tầng đường dây; cột điện thuộc trực tiếp khu vực |
| `line_segments` | Đoạn dây chỉ có nghĩa khi còn đường dây |
| `mission_target_lines` | Junction mission ↔ đường dây; mục tiêu nhiệm vụ còn `mission_targets` (theo cột điện) và `missions.boundary` |
| `management_units` | Không còn dùng cho phân cấp lưới điện hay phân quyền |
| `assets` (bảng vật lý `AssetComponents`) | Không quản lý linh kiện; mọi FK `asset_id` chuyển thành `tower_id` |
| `tower_condition_histories` | Lịch sử mức rủi ro cột, thuộc tính năng tính sức khỏe |
| `asset_health_histories` | Lịch sử điểm sức khỏe linh kiện, thuộc tính năng tính sức khỏe |

**Bảng đổi tên:** `pre_mission_assessment_assets` → `pre_mission_assessment_towers` (`asset_id` → `tower_id`, UK `(assessment_id, tower_id)`).

**Cột thay đổi ở các bảng còn lại:**

| Bảng | Thay đổi |
| :-- | :-- |
| `towers` | Xóa `transmission_line_id`, `risk_level`. `region_id` thành **bắt buộc** (`regions \|\|--o{ towers`) vì là cha duy nhất |
| `mission_targets` | `asset_id` → `tower_id`, UK `(mission_id, tower_id)` |
| `inspection_media` | `asset_id` → `tower_id` (nullable: video quét hành lang đi qua nhiều cột) |
| `detected_anomalies` | `asset_id` → `tower_id` (nullable lúc AI vừa phát hiện, bắt buộc trước khi `Confirmed`) |
| `incident_reports` | `asset_id` → `tower_id` (nullable: sự cố không xảy ra tại cột nào) |
| `maintenance_tickets` | Xóa `asset_id`. Cột điện cần sửa suy ra qua `anomaly_id → detected_anomalies.tower_id` (3NF) |
| `user_geographic_scopes` | Xóa `management_unit_id`, `substation_id`, `transmission_line_id` và CHECK "exactly one". Còn `user_id`, `region_id` (cả hai bắt buộc), UK `(user_id, region_id)` |
| `reports` | Xóa `substation_id`, `transmission_line_id`; thêm `region_id FK` (nullable) để giữ phạm vi địa lý của báo cáo |

**Màn hình chi tiết cột điện lấy dữ liệu như sau (không cần bảng mới):**
- Bất thường: `towers → detected_anomalies (tower_id)`, chỉ lấy bản ghi có `superseded_by_id IS NULL`.
- Lịch sử bảo trì: `towers → detected_anomalies → maintenance_tickets (anomaly_id)`, kèm `maintenance_proofs` và `material_logs` của từng phiếu.
- Bất thường chưa gán `tower_id` sẽ chưa hiện trên trang cột. Quy trình kiểm duyệt phải gán `tower_id` trước khi xác nhận (`validation_status = Confirmed`). Ràng buộc: `CHECK (validation_status <> 'Confirmed' OR tower_id IS NOT NULL)`.
- `defect_categories.severity_weight` vẫn giữ. Nếu cột này chỉ phục vụ việc tính điểm sức khỏe thì có thể bỏ nốt.

## 1b. Cập nhật v2.2: phân tích AI hai nhánh (edge + cloud)

**Vấn đề của thiết kế cũ:**
- `ai_analysis_requests` và `detected_anomalies` cùng treo vào `inspection_media` nhưng không liên kết với nhau, nên không truy được bất thường do lần chạy nào, model phiên bản nào sinh ra.
- Nhánh edge (`ReceiveVisionDetectionCommandHandler`, `EmergencyAlertService`) tạo thẳng anomaly mà không có request.
- `ai_source` lẫn vị trí chạy (`EdgeAI`, `VisionEdge`) với tên model (`RF-DETR`).
- Edge và cloud cùng phân tích một ảnh thì sinh hai bản ghi cho cùng một lỗi.

**Thiết kế mới:**
- `ai_analysis_requests` đổi thành **`ai_analysis_runs`**: mỗi dòng là một lần chạy model trên một media, ở edge hoặc cloud.
  - Thêm `execution_location` (Edge/Cloud), `uav_id` (thiết bị edge), `model_version`, `started_at`.
  - `requested_by` thành nullable (edge chạy tự động).
  - Bỏ `result` (trùng với `detected_anomalies`). `notes` đổi tên thành `error_message`.
  - Nhánh edge ghi thẳng một run `status = Completed` khi nhận kết quả; nhánh cloud tạo run `Pending` rồi cập nhật khi có callback.
- `detected_anomalies`:
  - Thêm `analysis_run_id FK` (NULL = bất thường tạo tay).
  - Thêm `superseded_by_id` (self FK): bản edge được kết quả cloud xác nhận hoặc thay thế sẽ trỏ sang bản cloud.
  - Bỏ `ai_source`, vì suy ra được từ `analysis_run_id` và `ai_analysis_runs.execution_location`/`model_name`.
  - UK đổi từ `(media_id, ai_detection_id)` thành `(analysis_run_id, ai_detection_id)`. Edge TrackId lưu vào `ai_detection_id` thay vì `analyst_notes`.
  - FK ghép `(analysis_run_id, media_id) → ai_analysis_runs(id, media_id)` bảo đảm media của bất thường trùng media của lần chạy.

## 1c. Cập nhật v2.2: báo cáo sinh trực tiếp, không lưu file

**Quyết định:** báo cáo chỉ hiển thị dạng bảng khi xem và xuất PDF/Excel theo template khi tải về. Nội dung luôn sinh từ dữ liệu hiện tại tại thời điểm xem/tải, không lưu file.

**Cột đã xóa khỏi `reports`:**

| Cột | Lý do |
| :-- | :-- |
| `pdf_file_url`, `pdf_file_size`, `excel_file_url`, `excel_file_size` | Không còn file lưu trữ để trỏ tới |
| `defect_count` | Là giá trị đóng băng lúc sinh báo cáo, trong khi phần còn lại tính trực tiếp nên sẽ lệch nhau; tính bằng `COUNT` trên `detected_anomalies` trong phạm vi báo cáo |

**Phạm vi để sinh lại báo cáo:** `date_from`, `date_to`, `region_id` và danh sách nhiệm vụ trong `report_missions`.

**Hệ quả cần chấp nhận:** báo cáo đã duyệt (`status = Approved`) không đóng băng nội dung. Nếu sau khi duyệt có bất thường bị bác bỏ, xóa mềm hoặc gán lại cột điện, lần tải sau sẽ ra số liệu khác bản đã duyệt. Nếu sau này cần giữ nguyên bản đã duyệt để đối chiếu hoặc kiểm toán, thêm cột `snapshot jsonb` ghi lúc `approved_at`.

## 1d. Cập nhật v2.2: sự cố không lưu tệp bằng chứng

**Quyết định:** báo cáo sự cố chỉ gồm thông tin mô tả (`incident_type`, `severity`, `status`, `description`, `reported_at`), không cần ảnh hay tệp đính kèm.

**Cột đã xóa khỏi `incident_reports`:** `file_url`. Không cần bảng đính kèm thay thế.

Các mục 3–6 bên dưới mô tả bản v2 đầu. Những dòng nhắc tới các bảng và cột đã xóa hoặc đổi tên ở mục 1a–1d không còn áp dụng.

## 2. Nguyên tắc best practice đã áp dụng

1. **Một sự thật, một chỗ (3NF):** bỏ các cột suy ra được qua FK (ví dụ `towers.line_code`, `emergency_alerts.tower_id`) hoặc qua phép tính (`delivery_latency_seconds`, `active_defects_count`).
2. **Không lưu danh sách FK trong JSON:** `missions.priority_defects_json` được thay bằng bảng junction `mission_priority_defects`.
3. **Timestamp thay cho cờ boolean đi kèm:** dùng `read_at` thay `is_read`, `pushed_at` thay `is_pushed`, `unpaired_at` thay `is_active`, `overdue_notified_at` thay `is_overdue_notified`.
4. **Tên FK phản ánh bảng đích:** FK đặt tên theo bảng được trỏ tới. Ví dụ cột `RegionAssetId` trỏ tới `regions` được đặt tên `region_id`, cột trỏ tới `towers` là `tower_id`.
5. **Cột audit chung khai báo một lần:** thực thể `base_entity_audit` liệt kê `created_at, created_by, updated_at, updated_by, is_deleted, deleted_at`. Các bảng sau **không** có các cột này: `roles`, `user_roles`, `defect_categories`, `mission_priority_defects`, `report_missions`, `notifications`, `audit_logs`, `outbox_messages`.
6. **Kiểu dữ liệu có giới hạn:** dùng `varchar(n)` cho mã và enum, `numeric` cho điểm số và số đo, `jsonb` cho dữ liệu bán cấu trúc, `geometry` SRID 4326 cho dữ liệu không gian.
7. **Ràng buộc nằm trên sơ đồ:** ghi PK, FK, UK, unique kết hợp, unique có điều kiện (partial), CHECK "exactly one" và EXCLUDE chống trùng lịch bằng chú thích trên cột.
8. **Snapshot là hợp lệ:** các cột chụp trạng thái tại một thời điểm được giữ lại, vì chúng không phải dữ liệu thừa. Ví dụ `pre_mission_assessment_drones.technical_health`, `pre_mission_assessment_personnel.availability_status`.

## 3. Lỗi của ERD v1 đã sửa

| Bảng | ERD v1 | Thực tế / v2 |
| :-- | :-- | :-- |
| users | `username UK` | Không có username; đăng nhập bằng `email` (UK), `phone` (UK) |
| towers | Chỉ thuộc `regions` | Thuộc `transmission_lines` (bắt buộc), `region_id` nullable |
| mission_targets | FK tới `towers` | FK tới `assets` (thành phần cần kiểm tra) |
| pre_mission_assessments | `mission_id UK,FK` (assessment → mission) | Chiều ngược lại: `missions.pre_mission_assessment_id FK,UK` |
| pre_mission_assessments | `weather_suitability`, `airspace_clearance`, `resource_readiness` | Không tồn tại; kết quả nằm ở `findings` + 3 bảng con assets/personnel/drones |
| missions | `actual_start`, `actual_end`, `inspector_id` | `started_at`, `ended_at`; đội bay nằm trong `mission_assignments` |
| uavs | `total_flight_hours` | Không tồn tại |
| alert_escalations | `escalation_level` | Không tồn tại |
| defect_categories | `name`, `default_severity`, `is_active` | `category_name`, `severity_weight`, `is_emergency_class` |
| mission_flight_logs | `created_by` riêng | Đã có trong cột audit chung |
| ERD_Diagrams-Conceptual ERD.drawio.svg | – | Tệp này chứa **cùng nội dung** với ERD_Full (trang "ERD_Full"), không phải ERD mức khái niệm |

## 4. Bảng bổ sung (có trong DB nhưng thiếu ở v1, trừ bảng đánh dấu NEW)

| Nhóm | Bảng |
| :-- | :-- |
| Identity | `user_geographic_scopes`, `refresh_tokens`, `trusted_devices` |
| Hạ tầng | `management_units`, `substations`, `transmission_lines`, `assets` (bảng vật lý `AssetComponents`), `line_segments`, `asset_health_histories` |
| Đội UAV | `drone_flight_controllers` |
| MF01 | `inspection_schedules`, `pre_mission_assessment_assets`, `pre_mission_assessment_personnel`, `pre_mission_assessment_drones` |
| MF02 | `mission_target_lines`, `mission_communication_logs`, **`mission_priority_defects` (NEW)** |
| Bảo trì | `material_logs` |
| Báo cáo | `reports`, `report_missions` |
| Hệ thống | `system_settings`, `outbox_messages` |

## 5. Thuộc tính đã loại bỏ (dư thừa so với DB hiện tại)

| Bảng | Cột bỏ | Lý do / cách lấy lại |
| :-- | :-- | :-- |
| towers | `line_code`, `voltage` | Trùng `transmission_lines.code`, `voltage_level` |
| towers | `active_defects_count`, `last_inspected_at` | Giá trị tổng hợp: lấy từ `detected_anomalies`, `mission_targets` hoặc bản ghi `tower_condition_histories` mới nhất |
| assets | `power_line_id`, `management_unit_id` | Suy ra qua `tower_id → towers → transmission_lines` |
| assets | `last_inspected_at` | Giá trị tổng hợp từ `mission_targets` |
| uavs | `assigned_region_name` | Trùng `regions.name` |
| uavs | `technical_health_updated_at` | Bằng `completed_at` của `last_technical_inspection_id` |
| drone_flight_controllers | `is_active` | Tương đương `unpaired_at IS NULL` |
| drone_technical_metrics | `critical` | Tương đương `severity = 'Critical'` |
| inspection_schedules | `created_by_user_id` | Trùng cột audit `created_by` |
| pre_mission_assessments | `consumed_by_mission_id` | Quan hệ 1–1 hai chiều; giữ một chiều `missions.pre_mission_assessment_id` |
| pre_mission_assessment_personnel / _drones | `is_eligible` | Suy ra từ `availability_status` + `eligibility_status` (+ `suitability`) |
| missions | `inspector_id` | Trùng `mission_assignments` (role `INSPECTOR`/`PILOT`) |
| missions | `scheduled_start_at` | Trùng `planned_start` (code đang dùng `ScheduledStartAt ?? PlannedStart`) |
| missions | `emergency_reason` | Gộp vào `trigger_reason` |
| missions | `priority_defects_json` | Thay bằng bảng `mission_priority_defects` |
| missions | `is_overdue_notified` | Thay bằng `overdue_notified_at` |
| mission_communication_logs | `sender_name`, `sender_role` | Lấy qua `sender_id → users`; `type = System` khi không có người gửi |
| incident_reports, inspection_media, maintenance_tickets | `tower_id` | Suy ra qua `asset_id → assets.tower_id` |
| inspection_media | `ai_source` | Nguồn AI thuộc từng phát hiện (`detected_anomalies.ai_source`) |
| ai_analysis_requests | `file_url`, `media_type`, `mission_id`, `asset_id` | Lấy qua `media_id` (đổi thành NOT NULL) |
| detected_anomalies | `tower_id` (varchar) | Sai kiểu và trùng; dùng `asset_id` |
| detected_anomalies | `video_duration`, `video_fps`, `video_width`, `video_height` | Là thuộc tính của tệp media, chuyển sang `inspection_media` |
| emergency_alerts | `mission_id`, `asset_id`, `tower_id` | Suy ra qua `anomaly_id → media → mission` và `anomaly.asset_id` |
| emergency_alerts | `delivery_latency_seconds` | Bằng `received_at − triggered_at` |
| maintenance_proofs | `after_repair_image_url` | Trùng `file_url`; mỗi tệp là một dòng, phân biệt bằng `proof_type` (Before/After/Document) |
| notifications | `is_read`, `is_pushed` | Thay bằng `read_at`, `pushed_at` |
| outbox_messages | Cột audit / soft delete | Bảng hạ tầng, không cần |

Các thuộc tính đánh dấu `[NotMapped]` hoặc `Ignore()` trong code (`Mission.RouteData`, `AssignedToUserId`, `DroneCode`, `PreMissionAssessment.CompletedByMissionId`) không có trong DB nên không đưa vào ERD.

## 6. Đổi tên và bổ sung

**Đổi tên** (logical → cột vật lý hiện tại):

| Bảng | v2 | Hiện tại |
| :-- | :-- | :-- |
| regions | `name` | `RegionName` |
| substations | `region_id`, `name` | `RegionAssetId`, `SubstationName` |
| transmission_lines | `substation_id`, `name` | `SubstationAssetId`, `LineName` |
| towers | `transmission_line_id` | `LineAssetId` |
| line_segments | `transmission_line_id`, `geom` | `PowerLineId`, `Geometry` |
| mission_target_lines | `transmission_line_id` | `LineAssetId` |
| uavs | `flight_status` | `Status` (tránh nhầm với `operational_status`) |
| drone_technical_inspections | `technician_id` | `TechnicianUserId` |
| mission_assignments | `assigned_by_id` | `AssignedByUserId` |
| emergency_alerts | `reviewed_by_id` | `ReviewedByUserId` |
| ai_analysis_requests | `requested_by` | `UploadedBy` |
| detected_anomalies | `frame_time_seconds`, `location` (geometry) | `Timestamp` (từ khóa SQL), `Gps` (jsonb) |
| report_missions | `added_at` | `AssignedAt` |

**Bổ sung:**

| Bảng | Bổ sung | Lý do |
| :-- | :-- | :-- |
| substations | `code UK` | Mọi thực thể hạ tầng khác đều có mã định danh tự nhiên |
| inspection_media | `checksum UK`, `file_size_bytes`, `width_px`, `height_px`, `duration_seconds`, `fps` | Chống trùng khi đồng bộ từ Edge (SRS NS-05, BR-050); metadata video chuyển từ anomalies sang |
| uavs | FK `assigned_region_id → regions`, FK `last_technical_inspection_id → drone_technical_inspections` | Trong DB hiện tại hai cột này chưa có ràng buộc FK |
| user_geographic_scopes | FK cho 4 cột phạm vi + CHECK "đúng một cột khác NULL" | Hiện chưa có FK |
| line_segments | UK `(transmission_line_id, sequence)` | Bảo đảm thứ tự tuyến không trùng |
| users | UK `phone` | IdentityService đã có, OperationsService snapshot thiếu |
| ai_analysis_requests | `result` kiểu `jsonb` | Hiện là `text` |

## 7. Từ điển enum (lưu dạng chuỗi)

| Enum | Giá trị |
| :-- | :-- |
| UserStatus | Pending, Active, Inactive, Suspended |
| DroneStatus (`flight_status`) | Idle, Flying, Maintenance, Offline (Online: giá trị cũ, giữ để tương thích) |
| DroneOperationalStatus | Available, Reserved, InUse, Maintenance, Unavailable, Lost |
| TechnicalHealth | Unknown, Healthy, Warning, Critical |
| TechInspectionStatus | Pending, InProgress, Passed, Failed, Incomplete |
| AssessmentStatus | Draft, Evaluating, Ready, NotReady, Expired, Completed, Cancelled |
| ResourceAvailability | Unknown, Available, Conflict, Unavailable |
| ResourceEligibility | Unknown, Eligible, Ineligible, RequiresInspection |
| UavScopeSuitability | Suitable, SuitableWithSplit, Unsuitable |
| MissionStatus | Draft, PendingAcceptance, Assigned, Preparing, Ready, InProgress, Suspended, Postponed, Completed, Cancelled |
| MissionType | Scheduled, AdHoc |
| MissionPriority (smallint) | 0 Normal, 1 High, 2 Emergency |
| InspectionObjective | PeriodicInspection, TargetedReinspection, IncidentFollowUp, EmergencyInspection |
| TargetInspectionStatus | Pending, InProgress, Inspected, Skipped |
| AssignmentStatus | Active, Unavailable, Revoked |
| AssignmentResponse | Pending, Accepted, Postponed, Replaced, Cancelled |
| ResourceBookingStatus | Active, Released, Cancelled |
| CheckInStatus | CheckedIn, Revoked |
| HandoverStatus | Accepted, Rejected, Returned |
| AnalysisType | DefectDetection, HumanMotionDetection, ObjectClassification, AssetConditionAssessment, General |
| ExecutionLocation (`ai_analysis_runs`) | Edge, Cloud |
| AIAnalysisStatus | Pending, Processing, Completed, Failed |
| AnomalyStatus | Pending, Confirmed, Rejected, Resolved |
| AlertStatus | Active, Confirmed, Dismissed, Escalated |
| EmergencyAlertPriority | Low, Medium, High, Critical |
| TicketStatus | Open, InProgress, PendingVerification, Resolved, Closed |
| TicketPriority | Low, Medium, High, Emergency |
| ReportType | Defect, Periodic, Thermal, Corridor |
| ReportStatus | Draft, Pending, Approved |
| NotificationType | General, MissionAssigned, DefectDetected, EmergencyAlert, TicketAssigned, EscalationRequest |

## 8. Triển khai trong code và CSDL

ERD v2.2 đã được áp dụng vào toàn bộ backend. Chi tiết tên bảng/cột vật lý và quy tắc nghiệp vụ nằm ở `docs/database/ERD_v2_2_TARGET_SCHEMA.md`.

**Migration:** `Services/OperationsService/UavPms.OperationsService.Infrastructure/Migrations/20261027181344_AlignSchemaWithErdV2_2.cs`. Phần `Up()` viết tay bằng SQL, chạy trong một transaction theo thứ tự expand → migrate → contract:

1. Đổi tên giữ dữ liệu: `Regions.RegionName → Name`, `UAVs.Status → FlightStatus`, `DroneTechnicalInspections.TechnicianUserId → TechnicianId`, `MissionAssignments.AssignedByUserId → AssignedById`, `EmergencyAlerts.ReviewedByUserId → ReviewedById`, `DetectedAnomalies.Timestamp → FrameTimeSeconds`, `ReportMissions.AssignedAt → AddedAt`, `MissionTargets.AssetId → TowerId`, `PreMissionAssessmentAssets → PreMissionAssessmentTowers`, `AIAnalysisRequests → AIAnalysisRuns`.
2. Backfill:
   - `Towers.RegionId` ← khu vực của trạm qua đường dây cũ, sau đó ← vùng chứa điểm cột (PostGIS). Cột còn lại đưa vào khu vực `UNASSIGNED`.
   - `TowerId` của mission target, media, sự cố và bất thường ← `AssetComponents.TowerId`. Bất thường còn thiếu cột lấy thêm từ phiếu bảo trì, cảnh báo và media.
   - `UserGeographicScopes` ← khu vực của trạm/đường dây. Phạm vi đơn vị quản lý tách thành một dòng cho mỗi khu vực có cột của đơn vị đó.
   - `Reports.RegionId` ← khu vực của trạm hoặc đường dây cũ.
   - `Missions.InspectorId` → một `MissionAssignment` vai trò INSPECTOR. `PriorityDefectsJson` → `MissionPriorityDefects` (khớp `CategoryCode`/`CategoryName`; mã không có trong danh mục bị bỏ). `IsOverdueNotified` → `OverdueNotifiedAt`. `ScheduledStartAt` → `PlannedStart`. `EmergencyReason` → `TriggerReason`.
   - `PreMissionAssessments.ConsumedByMissionId` → `Missions.PreMissionAssessmentId`.
   - `AIAnalysisRequests` có media → `AIAnalysisRuns` (Cloud; status/analysis type chuyển sang chuỗi). Bất thường AI được gắn với run cloud cùng media + model. Bất thường edge cũ được gom thành một run Edge (Completed) cho mỗi media + nguồn.
   - `Gps` → `Location` (Point 4326). Video metadata → `InspectionMedia`. `DeliveryLatencySeconds` → `ReceivedAt`. `IsRead/IsPushed` → `ReadAt/PushedAt`. `AfterRepairImageUrl` → một dòng `MaintenanceProofs` loại After. `DroneFlightControllers.IsActive` → `UnpairedAt`, mỗi drone/UID chỉ còn một cặp đang hoạt động.
3. Ràng buộc mới:
   - FK `UAVs.AssignedRegionId`, `UAVs.LastTechnicalInspectionId`, `UserGeographicScopes.RegionId` (UK theo user + region), `Reports.RegionId`, `MissionAssignments.AssignedById`.
   - UK partial cho `InspectionMedia.Checksum` và cặp flight controller đang hoạt động.
   - FK ghép `(AnalysisRunId, MediaId) → AIAnalysisRuns(Id, MediaId)`.
   - `CHECK (ValidationStatus <> 'Confirmed' OR TowerId IS NOT NULL)` tạo `NOT VALID`: dữ liệu cũ được giữ nguyên, mọi dòng mới hoặc sửa đều bị kiểm tra.
4. Contract: drop 8 bảng ở mục 1a, các cột dư ở mục 5, cột lỗi thời ngoài ERD (`Missions.AssignedToUserId/DroneCode/RouteData`, `Users.Username`), cột audit của `OutboxMessages`. Đổi tên các constraint do script SQL cũ tạo theo quy ước EF. `IX_Users_Email` chỉ được tạo khi không có email trùng.

**Chịu được schema lệch:** CSDL production từng được sửa tay (các workflow repair), nên schema có thể khác lịch sử migration. Mỗi lệnh backfill chỉ chạy khi bảng/cột cũ mà nó đọc còn tồn tại. Mọi lệnh thêm/xóa/đổi tên cột, index và constraint đều idempotent. Nếu cả cột cũ và cột mới cùng tồn tại, dữ liệu được chép từ cột cũ sang rồi xóa cột cũ.

`Down()` không hỗ trợ vì migration xóa dữ liệu. Rollback bằng cách restore bản `pg_dump` do workflow tạo trước khi migrate.

**Đã kiểm thử trên PostgreSQL 16 + PostGIS 3.4:** nạp `scripts/production_dump.sql` cộng dữ liệu tổng hợp phủ mọi nhánh backfill, rồi chạy migration. Schema kết quả trùng với schema chạy toàn bộ chuỗi migration trên DB rỗng và khớp model EF. Chênh lệch còn lại là chủ ý: FK ghép, CHECK `NOT VALID`, ràng buộc EXCLUDE chống trùng lịch, `Users.PasswordHash` chỉ IdentityService map.

**Chạy trên production:** workflow thủ công `.github/workflows/migrate-database-erd-v2-2.yml` (Actions → *Migrate Database (ERD v2.2)*):
- `mode = dry-run`: build, chạy test, sinh script idempotent và DDL của model EF, `pg_dump` CSDL production, in schema hiện tại để chẩn đoán, áp script lên một bản sao, so cột của bản sao với model EF (thiếu cột thì báo lỗi), rồi xóa bản sao. CSDL thật không đổi.
- `mode = apply`: làm như dry-run, sau đó dừng các service ghi DB, áp script lên CSDL thật trong một transaction. Nếu `deploy_after = true` thì build và khởi động lại các service ở cùng commit.
- Ô xác nhận phải nhập `MIGRATE_ERD_V2_2`.

**Thay đổi code chính:**
- **OperationsService:**
  - Bỏ API/feature của asset, trạm, đường dây và tính sức khỏe.
  - Tower CRUD theo khu vực, kèm `GET /api/v1/towers/{id}/history` (bất thường + lịch sử bảo trì) và `POST /api/v1/towers/spatial-query`.
  - Phân quyền địa lý chỉ theo khu vực (kèm khu vực con) và phân công đang hoạt động.
  - Báo cáo sinh PDF/Excel trực tiếp từ dữ liệu, `DefectCount` tính trực tiếp.
  - Nhiệm vụ suy inspector từ `MissionAssignments`; lỗi ưu tiên lưu ở bảng junction.
  - Cảnh báo, phiếu bảo trì và sự cố lấy cột điện qua bất thường.
- **AIInspectionService:** `AIAnalysisRun` cho cả hai nhánh edge và cloud; bất thường có `AnalysisRunId`, `SupersededById`, `Location`. Kết quả cloud thay thế bản edge cùng media + loại lỗi.
- **Identity / Notification:** phạm vi người dùng chỉ còn khu vực; thông báo dùng `ReadAt`/`PushedAt`; job quá hạn dùng `OverdueNotifiedAt`; trạng thái phân công map đúng kiểu chuỗi.
- **Shared contracts:** `OutboxMessage` không còn cột audit; sự kiện media/AI dùng `TowerId` thay `AssetId`.
