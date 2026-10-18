# Sơ đồ Thực thể Liên kết Hợp nhất Toàn diện (MF01 - MF04)
**Hệ thống Quản lý và Giám sát Lưới điện bằng Thiết bị bay không người lái (UAV PMS)**

Tài liệu này tổng hợp thiết kế cơ sở dữ liệu (Database Schema) đã được chuẩn hóa, tinh gọn và thống nhất trên toàn bộ 4 Luồng Nghiệp vụ Chính (Main Flows):
- **MF01:** Đánh giá Tiền Khả thi & Lập Kế hoạch Nhiệm vụ (Pre-Mission Feasibility & Planning)
- **MF02:** Điều phối Đội bay & Đặt lịch Tài nguyên (Scheduling & Team Dispatch)
- **MF03:** Thực thi Hiện trường, Bàn giao Thiết bị & Nạp Dữ liệu (Field Execution, Custody Handover & Ingestion)
- **MF04:** Phân tích AI, Thẩm định Chuyên viên, Lệnh Bảo trì & Trạng thái Cột điện (AI Defect Detection, Analyst Review, Maintenance & Tower Condition)

---

## 1. Sơ đồ Thực thể Liên kết (ERD) Toàn diện

```mermaid
erDiagram
    %% ==========================================
    %% HẠ TẦNG & TÀI NGUYÊN (SHARED FOUNDATION)
    %% ==========================================
    regions ||--o{ towers : "chứa các cột điện"
    regions ||--o{ missions : "phạm vi địa bàn nhiệm vụ"
    users ||--o{ user_roles : "gán chức danh"
    roles ||--o{ user_roles : "định nghĩa quyền hệ thống"

    %% ==========================================
    %% MF01: LẬP KẾ HOẠCH & TIỀN KHẢ THI
    %% ==========================================
    missions ||--o| pre_mission_assessments : "hồ sơ đánh giá tiền khả thi (1-1)"
    missions ||--o{ mission_targets : "danh sách cột cần kiểm tra"
    towers ||--o{ mission_targets : "đối tượng kiểm tra"
    users ||--o{ pre_mission_assessments : "quản lý đánh giá"

    %% ==========================================
    %% MF02: ĐIỀU PHỐI & KHÓA GIỮ TÀI NGUYÊN
    %% ==========================================
    missions ||--o{ mission_assignments : "phân công vai trò đội bay"
    users ||--o{ mission_assignments : "thành viên nhận nhiệm vụ"
    missions ||--o{ resource_bookings : "khóa lịch tài nguyên"
    users ||--o{ resource_bookings : "giữ chỗ phi công"
    uavs ||--o{ resource_bookings : "giữ chỗ máy bay"
    missions }o--o| uavs : "thiết bị bay chính thức"
    uavs ||--o{ drone_technical_inspections : "kiểm định kỹ thuật định kỳ"
    drone_technical_inspections ||--o{ drone_technical_metrics : "thông số đo đạc chi tiết"
    users ||--o{ drone_technical_inspections : "kỹ thuật viên kiểm định"

    %% ==========================================
    %% MF03: THỰC THI HIỆN TRƯỜNG & NẠP DỮ LIỆU
    %% ==========================================
    missions ||--o{ mission_check_ins : "xác nhận có mặt hiện trường"
    users ||--o{ mission_check_ins : "nhân sự check-in"
    missions ||--o{ drone_handovers : "biên bản giao nhận máy bay"
    uavs ||--o{ drone_handovers : "máy bay bàn giao"
    users ||--o{ drone_handovers : "người giao (handed_over_by)"
    users ||--o{ drone_handovers : "người nhận (received_by)"
    missions ||--o{ mission_flight_logs : "telemetry & quỹ đạo bay"
    missions ||--o{ incident_reports : "báo cáo sự cố hiện trường"
    towers ||--o{ incident_reports : "cột xảy ra sự cố"
    users ||--o{ incident_reports : "người phát hiện"
    missions ||--o{ inspection_media : "ảnh/video thanh tra tải lên"
    towers ||--o{ inspection_media : "cột điện trong ảnh"
    users ||--o{ inspection_media : "thanh tra viên tải lên"

    %% ==========================================
    %% MF04: PHÂN TÍCH AI & THẨM ĐỊNH LỖI
    %% ==========================================
    inspection_media ||--o{ ai_analysis_requests : "kích hoạt tác vụ AI"
    inspection_media ||--o{ detected_anomalies : "chứa các khiếm khuyết"
    missions ||--o{ ai_analysis_requests : "ngữ cảnh nhiệm vụ"
    towers ||--o{ detected_anomalies : "vị trí cột xuất hiện lỗi"
    defect_categories ||--o{ detected_anomalies : "phân loại danh mục lỗi"
    users ||--o{ detected_anomalies : "chuyên viên thẩm định (analyst)"

    %% ==========================================
    %% MF04: CẢNH BÁO KHẨN CẤP & BẢO TRÌ SỬA CHỮA
    %% ==========================================
    detected_anomalies ||--o{ emergency_alerts : "phát cảnh báo sự cố khẩn"
    emergency_alerts ||--o{ alert_escalations : "leo thang cấp xử lý"
    users ||--o{ emergency_alerts : "quản lý tiếp nhận xử lý"
    users ||--o{ alert_escalations : "người chuyển / người nhận leo thang"
    detected_anomalies ||--o{ maintenance_tickets : "sinh phiếu bảo trì"
    towers ||--o{ maintenance_tickets : "cột cần sửa chữa"
    users ||--o{ maintenance_tickets : "quản lý giao việc (manager)"
    users ||--o{ maintenance_tickets : "thợ sửa chữa nhận việc (technician)"
    maintenance_tickets ||--o{ maintenance_proofs : "ảnh chụp nghiệm thu trước/sau"
    users ||--o{ maintenance_proofs : "thợ bảo trì tải ảnh"
    towers ||--o{ tower_condition_histories : "ghi nhận lịch sử trạng thái rủi ro"

    %% ==========================================
    %% THÔNG BÁO & KIỂM TOÁN VẬN HÀNH
    %% ==========================================
    missions ||--o{ audit_logs : "nhật ký kiểm toán vòng đời"
    users ||--o{ audit_logs : "tác nhân thao tác"
    users ||--o{ notifications : "thông báo vận hành & cảnh báo"

    %% ==========================================
    %% CẤU TRÚC THUỘC TÍNH CHI TIẾT TỪNG BẢNG
    %% ==========================================
    regions {
        uuid id PK
        varchar code UK "Mã vùng (KV_HAIPHONG)"
        varchar name "Tên vùng quản lý"
        geometry geom "Ranh giới địa lý (Polygon)"
    }

    towers {
        uuid id PK
        uuid region_id FK "Trỏ tới regions.id"
        varchar tower_code UK "Mã cột điện (NPC_HP_01_001)"
        varchar line_code "Mã tuyến đường dây (22kV_471_E2.1)"
        varchar voltage "Cấp điện áp (22kV, 110kV)"
        varchar pole_type "Loại cột (Bê tông ly tâm, Tháp thép)"
        varchar address "Địa chỉ mô tả vị trí cột"
        geometry geom "Tọa độ không gian chính xác (Point 4326)"
        varchar risk_level "Mức rủi ro: Normal, Warning, Critical"
        int active_defects_count "Số khiếm khuyết đang tồn tại"
        timestamptz last_inspected_at "Lần kiểm tra gần nhất"
    }

    users {
        uuid id PK
        varchar username UK "Tên đăng nhập"
        varchar email UK "Email công vụ"
        varchar full_name "Họ và tên nhân sự"
        varchar phone "Số điện thoại"
        boolean is_active "Trạng thái tài khoản"
    }

    roles {
        int id PK
        varchar role_name UK "Admin, Manager, Inspector, Pilot, Technician, Analyst"
        varchar description "Mô tả vai trò hệ thống"
    }

    user_roles {
        uuid user_id PK,FK "Trỏ tới users.id"
        int role_id PK,FK "Trỏ tới roles.id"
        timestamptz assigned_at "Thời điểm cấp quyền"
    }

    uavs {
        uuid id PK
        varchar uav_code UK "Mã định danh thiết bị (UAV-01)"
        varchar name "Tên máy bay"
        varchar model "Dòng máy bay (DJI Matrice 300 RTK)"
        varchar serial_number UK "Số serial phần cứng"
        varchar status "Trạng thái: Idle, Assigned, Flying, Maintenance"
        varchar operational_status "Ready, Degraded, Grounded"
        varchar technical_health "Good, Fair, Critical, Unknown"
        float battery_level "Mức pin hiện tại (%)"
        float total_flight_hours "Tổng giờ bay tích lũy"
        geometry current_location "Tọa độ GPS hiện tại"
        uint version "Concurrency token"
    }

    missions {
        uuid id PK
        varchar mission_code UK "Mã nhiệm vụ duy nhất (MS-2026-0001)"
        varchar title "Tiêu đề nhiệm vụ"
        varchar status "Draft -> PendingAcceptance -> Assigned -> InProgress -> Completed"
        int priority "Mức ưu tiên: Normal, Urgent, Emergency"
        varchar objective "Mục tiêu: Periodic, PostIncident, Acceptance"
        timestamptz planned_start "Dự kiến bắt đầu"
        timestamptz planned_end "Dự kiến kết thúc"
        timestamptz actual_start "Bắt đầu bay thực tế"
        timestamptz actual_end "Kết thúc nhiệm vụ thực tế"
        uuid manager_id FK "Quản lý tạo nhiệm vụ (users.id)"
        uuid inspector_id FK "Phi công/Thanh tra trưởng phụ trách"
        uuid uav_id FK "Drone được gán chính thức (uavs.id)"
        uuid region_id FK "Khu vực kiểm tra (regions.id)"
        geometry boundary "Vùng bay quy hoạch (Polygon)"
        text manager_instructions "Chỉ thị vận hành"
        timestamptz confirmation_deadline "Hạn chót các thành viên xác nhận"
        uint version "Concurrency token"
    }

    pre_mission_assessments {
        uuid id PK
        uuid mission_id UK,FK "Liên kết 1-1 với missions.id"
        uuid manager_id FK "Quản lý đánh giá (users.id)"
        varchar status "Evaluating, Ready, NotReady, Completed"
        varchar site_feasibility "Feasible, Infeasible, Conditional"
        varchar weather_suitability "Suitable, Marginal, Unsuitable"
        varchar airspace_clearance "Clear, Restricted, Forbidden"
        varchar resource_readiness "Ready, Insufficient"
        jsonb findings "Chi tiết dữ liệu đánh giá rủi ro & khuyến cáo"
        geometry proposed_boundary "Vùng không gian đề xuất bay"
        timestamptz evaluated_at "Thời điểm đánh giá"
    }

    mission_targets {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid tower_id FK "Trỏ tới towers.id"
        int sequence_order "Thứ tự bay qua các cột"
        varchar inspection_status "Pending, Inspected, Skipped"
        timestamptz inspected_at "Thời điểm bay qua kiểm tra"
    }

    mission_assignments {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid user_id FK "Trỏ tới users.id"
        varchar assignment_role "Vai trò ngữ cảnh: PILOT, CO_PILOT, INSPECTOR, OBSERVER"
        boolean is_required "Thành viên bắt buộc"
        varchar status "Active, Inactive, Replaced"
        varchar response_status "Pending, Accepted, Postponed"
        timestamptz assigned_at "Thời điểm phân công"
        timestamptz responded_at "Thời điểm phản hồi"
        text response_reason "Lý do xin hoãn"
        uint version "Concurrency token"
    }

    resource_bookings {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid user_id FK "Phi công được khóa lịch"
        uuid drone_id FK "Drone được khóa lịch"
        timestamptz start_at "Thời gian bắt đầu giữ chỗ"
        timestamptz end_at "Thời gian kết thúc giữ chỗ"
        varchar status "Active -> Released khi hoàn thành"
    }

    drone_technical_inspections {
        uuid id PK
        uuid drone_id FK "Trỏ tới uavs.id"
        uuid technician_user_id FK "Kỹ thuật viên thực hiện (users.id)"
        varchar status "Pending, Passed, Failed"
        varchar health "Good, Fair, Poor, Critical"
        timestamptz started_at "Bắt đầu kiểm định"
        timestamptz completed_at "Hoàn thành kiểm định"
        timestamptz valid_until "Thời hạn giá trị phiếu kiểm định"
        jsonb raw_snapshot "Bản chụp thông số phần cứng"
        text notes "Ghi chú kỹ thuật viên"
    }

    drone_technical_metrics {
        uuid id PK
        uuid inspection_id FK "Trỏ tới drone_technical_inspections.id"
        varchar metric_code "Mã chỉ số: MOTOR_VIBRATION, BATTERY_HEALTH, IMU_DRIFT"
        varchar subsystem "Phân hệ: Propulsion, Avionics, Payload, Power"
        numeric numeric_value "Giá trị định lượng"
        boolean passed "Đạt tiêu chuẩn hay không"
        boolean is_critical "Chỉ số an toàn sống còn"
        varchar unit "Đơn vị đo (V, %, mm/s)"
    }

    mission_check_ins {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid user_id FK "Nhân sự check-in (users.id)"
        geometry check_in_location "Tọa độ GPS điểm check-in"
        timestamptz checked_in_at "Thời điểm check-in"
        varchar status "CheckedIn, Cancelled"
    }

    drone_handovers {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid drone_id FK "Trỏ tới uavs.id"
        uuid handed_over_by FK "Người giao máy bay (users.id)"
        uuid received_by FK "Người nhận máy bay (users.id - Phi công)"
        timestamptz received_at "Thời điểm nhận bàn giao"
        jsonb initial_checklist "Checklist trước bay: Cánh quạt, pin, camera"
        varchar status "Accepted, Rejected"
        timestamptz returned_at "Thời điểm thu hồi máy bay sau bay"
        jsonb return_checklist "Checklist sau bay: Tình trạng vỏ, pin còn lại"
    }

    mission_flight_logs {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        jsonb gps_track "Mảng chuỗi điểm quỹ đạo bay"
        float min_battery_recorded "Mức pin thấp nhất ghi nhận (%)"
        float max_altitude_m "Độ cao bay lớn nhất (m)"
        int flight_duration_seconds "Thời lượng bay thực tế (giây)"
        varchar connection_status "Chất lượng sóng: Normal, Degraded"
        timestamptz recorded_at "Thời điểm lưu log"
        uuid created_by "Nhân sự nạp file log"
    }

    incident_reports {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid reported_by FK "Nhân sự phát hiện (users.id)"
        uuid tower_id FK "Vị trí cột xảy ra sự cố (towers.id)"
        varchar incident_type "Loại sự cố: Thời tiết, Va chạm, Hỏng hóc"
        varchar severity "Mức độ: Low, Medium, High, Critical"
        text description "Mô tả sự cố (Tối thiểu 10 ký tự)"
        varchar file_url "Đường dẫn ảnh/video hiện trường"
        varchar status "Reported, Investigating, Resolved"
        timestamptz reported_at "Thời điểm lập biên bản"
    }

    inspection_media {
        uuid id PK
        uuid mission_id FK "Trỏ tới missions.id"
        uuid tower_id FK "Vị trí cột trong ảnh (towers.id)"
        uuid uploaded_by FK "Thanh tra viên tải lên (users.id)"
        geometry capture_location "Tọa độ GPS từ EXIF (Point 4326)"
        varchar media_type "Image hoặc Video"
        varchar file_url "Đường dẫn file trên Object Storage (S3/MinIO)"
        varchar ai_source "Nguồn ảnh: Drone Edge hoặc Server"
        varchar validation_status "Trạng thái: Pending, Approved, Rejected"
        timestamptz captured_at "Thời gian chụp bóc tách từ EXIF"
    }

    ai_analysis_requests {
        uuid id PK
        uuid media_id FK "Trỏ tới inspection_media.id"
        uuid mission_id FK "Trỏ tới missions.id"
        uuid tower_id FK "Trỏ tới towers.id"
        uuid uploaded_by FK "Người nạp ảnh (users.id)"
        uuid source_event_id "Idempotency key từ Outbox"
        varchar media_type "Image hoặc Video"
        varchar status "Pending, Processing, Completed, Failed"
        jsonb result "Raw JSON kết quả trả về từ AI Worker"
        timestamptz completed_at "Thời điểm AI xử lý xong"
        timestamptz created_at "Thời điểm gửi yêu cầu"
    }

    defect_categories {
        int id PK
        varchar category_code UK "CORROSION, BROKEN_INSULATOR, WIRE_SAGGING"
        varchar name "Tên khiếm khuyết (Ăn mòn, Vỡ sứ, Võng dây)"
        varchar default_severity "Mức độ mặc định: Low, Medium, High, Critical"
        text description "Tiêu chuẩn nhận diện kỹ thuật"
        boolean is_active "Trạng thái danh mục"
    }

    detected_anomalies {
        uuid id PK
        uuid media_id FK "Trỏ tới inspection_media.id"
        uuid tower_id FK "Trỏ tới towers.id"
        int category_id FK "Trỏ tới defect_categories.id"
        uuid analyst_id FK "Chuyên viên thẩm định (users.id)"
        jsonb bounding_box "Tọa độ chuẩn hóa: {x, y, width, height}"
        float confidence_score "Độ tin cậy AI (0.00 -> 1.00)"
        varchar validation_status "Pending, Confirmed, Rejected"
        jsonb analyst_feedback "Ý kiến chuyên viên chuẩn hóa theo mẫu JSON"
        timestamptz validated_at "Thời điểm thẩm định"
        varchar crop_url "Ảnh cắt cận cảnh vết lỗi (Thumbnail)"
        varchar image_url "Ảnh toàn cảnh có vẽ Bounding Box"
        jsonb gps "Tọa độ vị trí lỗi"
        varchar ai_source "Server AI hoặc Drone Edge"
    }

    emergency_alerts {
        uuid id PK
        uuid anomaly_id FK "Trỏ tới detected_anomalies.id"
        uuid tower_id FK "Trỏ tới towers.id"
        uuid mission_id FK "Trỏ tới missions.id"
        varchar status "Active, Investigating, Resolved, FalsePositive"
        varchar priority "Mức khẩn cấp: High, Critical, Emergency"
        timestamptz triggered_at "Thời điểm phát cảnh báo"
        timestamptz resolved_at "Thời điểm xử lý xong"
        uuid reviewed_by_user_id FK "Quản lý tiếp nhận xử lý (users.id)"
        text notes "Ghi chú điều tra khẩn cấp"
    }

    alert_escalations {
        uuid id PK
        uuid alert_id FK "Trỏ tới emergency_alerts.id"
        uuid escalated_by FK "Người chuyển cấp cảnh báo (users.id)"
        uuid escalated_to FK "Người nhận cấp cao hơn (users.id)"
        int escalation_level "Cấp bậc leo thang (1, 2, 3)"
        text reason "Lý do quá hạn chưa xử lý"
        timestamptz escalated_at "Thời điểm leo thang"
    }

    maintenance_tickets {
        uuid id PK
        varchar ticket_code UK "Mã phiếu sửa chữa (MT-2026-0001)"
        uuid anomaly_id FK "Trỏ tới detected_anomalies.id đã duyệt"
        uuid tower_id FK "Trỏ tới towers.id cần bảo dưỡng"
        uuid manager_id FK "Quản lý tạo & giao việc (users.id)"
        uuid technician_id FK "Kỹ thuật viên sửa chữa (users.id)"
        varchar status "Assigned, InProgress, Completed, Closed, Cancelled"
        varchar priority "Mức ưu tiên: Low, Medium, High, Critical"
        text description "Yêu cầu kỹ thuật khắc phục"
        timestamptz due_date "Hạn chót hoàn thành"
        timestamptz assigned_at "Thời điểm giao phiếu"
        timestamptz started_at "Thời điểm bắt đầu sửa chữa tại hiện trường"
        timestamptz resolved_at "Thời điểm hoàn thành nghiệm thu"
    }

    maintenance_proofs {
        uuid id PK
        uuid ticket_id FK "Trỏ tới maintenance_tickets.id"
        varchar proof_type "Giai đoạn ảnh: Before (Trước), InProgress (Đang làm), After (Sau)"
        varchar file_url "Đường dẫn ảnh/video nghiệm thu hiện trường"
        uuid uploaded_by FK "Thợ bảo trì tải ảnh (users.id)"
        timestamptz uploaded_at "Thời điểm chụp nghiệm thu"
    }

    tower_condition_histories {
        uuid id PK
        uuid tower_id FK "Trỏ tới towers.id"
        varchar risk_level "Mức rủi ro: Normal, Warning, Critical"
        int active_defects_count "Số khiếm khuyết đang tồn tại"
        timestamptz assessed_at "Thời điểm cập nhật trạng thái"
    }

    notifications {
        uuid id PK
        uuid user_id FK "Người nhận thông báo (users.id)"
        varchar type "MISSION_DISPATCH, CRITICAL_INCIDENT, TICKET_ASSIGNED"
        varchar reference_type "Polymorphic Type: MISSION, INCIDENT, TICKET"
        uuid reference_id "ID đối tượng liên kết (Không ràng buộc khóa ngoại cứng)"
        varchar title "Tiêu đề thông báo"
        text body "Nội dung chi tiết thông báo"
        boolean is_read "Đã đọc hay chưa"
        timestamptz sent_at "Thời điểm phát thông báo"
    }

    audit_logs {
        uuid id PK
        uuid user_id FK "Tác nhân thao tác (users.id)"
        varchar table_name "Tên bảng tác động (missions, maintenance_tickets)"
        uuid record_id "ID bản ghi chịu tác động"
        varchar action_type "Hành động: CREATE, START, CONFIRM, COMPLETE"
        jsonb old_values "Dữ liệu trước khi sửa"
        jsonb new_values "Dữ liệu sau khi sửa"
        timestamptz created_at "Thời điểm ghi nhật ký"
    }
```
