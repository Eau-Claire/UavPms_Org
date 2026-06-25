# 🗺️ TỔNG THỂ ROADMAP BACKEND: UAV-PMS SYSTEM (.NET 9)

- **Giai đoạn hiện tại:** Tái cấu trúc (Refactoring) & Xây dựng luồng nghiệp vụ cốt lõi.
- **Cơ sở tham chiếu:** `database_schema.md` (Database Schema chuẩn) & `SU26SE181xxx_UAV_AI_Inspection_v4_final.md` (Tài liệu đăng ký Capstone).
- **Kiến trúc:** Clean Architecture, CQRS (MediatR), PostgreSQL + PostGIS.

---

## EPIC 1: ĐẠI TU DATA LAYER (Refactoring Core & Infrastructure)

*Mục tiêu: Đồng bộ hoá cấu trúc Database 100% với Sơ đồ thực thể liên kết (ERD) và từ điển dữ liệu trong `database_schema.md`. Loại bỏ hoàn toàn các thực thể cũ không còn phù hợp (Drone, DroneTelemetry, EvidenceImage, DetectionAlert) và thay thế bằng hệ thống thực thể mới.*

### Phase 1.1: Thiết kế lại Domain Entities (Khớp 100% với Database Schema)
- [x] 1. **Cập nhật `BaseEntity.cs`**: Chứa các trường kiểm toán và trạng thái dùng chung (PascalCase):
  - `Id` (uuid)
  - `CreatedAt` (timestamp)
  - `UpdatedAt` (timestamp)
  - `CreatedBy` (uuid - liên kết User)
  - `UpdatedBy` (uuid - liên kết User)
  - `IsDeleted` (boolean)
  - `DeletedAt` (timestamp)
- [x] 2. **Xóa/Đổi tên thực thể cũ**: Loại bỏ `Drone.cs`, `DroneTelemetry.cs`, `EvidenceImage.cs`, `DetectionAlert.cs`. Giữ lại và viết lại `Notification.cs` để khớp schema mới.
- [x] 3. **Nhóm thực thể User & RBAC**:
  - `User` (PK: `user_id` / `Id` uuid, `username`, `password_hash`, `full_name`, `email`, `phone`, `status`, audit fields).
  - `Role` (PK: `role_id` / `Id` int, `role_name`, `description`).
  - `UserRole` (Bảng liên kết trung gian N-N: `UserId`, `RoleId`, `AssignedAt`).
- [x] 4. **Nhóm thực thể Asset Hierarchy (Phân cấp tài sản thực tế)**:
  - `Region` (PK: `region_asset_id` / `Id`, `region_name`, `geom` Point/Polygon, audit fields).
  - `Substation` (PK: `substation_asset_id` / `Id`, `RegionAssetId` FK, `substation_name`, `voltage_level`, `geom`, audit fields).
  - `TransmissionLine` (PK: `line_asset_id` / `Id`, `SubstationAssetId` FK, `line_name`, `is_critical_edge`, `geom`, audit fields).
  - `Tower` (PK: `tower_id` / `Id`, `LineAssetId` FK, `tower_code`, `geom` Point PostGIS, audit fields).
  - `Asset` (PK: `asset_id` / `Id`, `TowerId` FK, `asset_type`, `asset_code`, `status`, `current_health_score` float, `risk_level`, `last_inspected_at`, audit fields). *(Lưu ý: Điểm sức khỏe và mức độ rủi ro thuộc về thiết bị Asset nằm trên Tower, không trực tiếp nằm trên Tower)*.
  - `AssetHealthHistory` (PK: `history_id` / `Id`, `AssetId` FK, `health_score`, `active_defects_count`, `calculation_log` JSONB, `risk_level`, `calculated_at`).
- [x] 5. **Nhóm thực thể UAV Fleet & Missions**:
  - `Uav` (PK: `uav_id` / `Id`, `uav_code`, `model`, `status`, `battery_level`, `current_location` geom Point, `last_maintenance_at`, audit fields).
  - `Mission` (PK: `mission_id` / `Id`, `mission_code`, `manager_id` FK, `inspector_id` FK, `uav_id` FK, `status`, `scheduled_start_at`, `started_at`, `ended_at`, `description`, audit fields).
  - `MissionTargetLine` (Bảng trung gian N-N: `MissionId` FK, `LineAssetId` FK, `status`).
  - `MissionFlightLog` (PK: `log_id` / `Id`, `MissionId` FK, `gps_track` JSONB, `min_battery_recorded`, `max_altitude_m`, `flight_duration_seconds`, `connection_status`, `recorded_at`).
- [x] 6. **Nhóm thực thể Inspection Media & AI Anomaly**:
  - `InspectionMedia` (PK: `media_id` / `Id`, `MissionId` FK, `AssetId` FK, `media_type`, `file_url`, `ai_source`, `validation_status`, `captured_at`, audit fields). *(Lưu ý: Liên kết trực tiếp tới Asset được chụp)*.
  - `DefectCategory` (PK: `category_id` / `Id` int, `category_code`, `category_name`, `severity_weight`, `is_emergency_class` boolean, `description`).
  - `DetectedAnomaly` (PK: `anomaly_id` / `Id`, `MediaId` FK, `AssetId` FK, `CategoryId` FK, `analyst_id` FK (User), `bounding_box` JSONB, `confidence_score`, `validation_status`, `ai_source`, `analyst_notes`, `validated_at`, audit fields).
- [x] 7. **Nhóm thực thể Emergency Alert & Escalation**:
  - `EmergencyAlert` (PK: `alert_id` / `Id`, `AnomalyId` FK, `AssetId` FK, `MissionId` FK, `status`, `priority`, `delivery_latency_seconds`, `triggered_at`, `received_at`, `resolved_at`).
  - `AlertEscalation` (PK: `escalation_id` / `Id`, `AlertId` FK, `escalated_by` FK (User), `escalated_to` FK (User), `reason`, `escalated_at`).
- [x] 8. **Nhóm thực thể Incident Report (Báo cáo sự cố hiện trường)**:
  - `IncidentReport` (PK: `incident_id` / `Id`, `MissionId` FK, `reported_by` FK (User), `AssetId` FK, `incident_type`, `severity`, `description`, `file_url`, `status`, `reported_at`, audit fields).
- [x] 9. **Nhóm thực thể Maintenance & Material Logs**:
  - `MaintenanceTicket` (PK: `ticket_id` / `Id`, `ticket_code`, `AnomalyId` FK, `AssetId` FK, `manager_id` FK, `technician_id` FK, `status`, `priority`, `description`, `due_date`, `assigned_at`, `started_at`, `resolved_at`, audit fields).
  - `MaintenanceProof` (PK: `proof_id` / `Id`, `TicketId` FK, `uploaded_by` FK (User), `file_url`, `after_repair_image_url`, `technician_notes`, `uploaded_at`).
  - `MaterialLog` (PK: `material_log_id` / `Id`, `TicketId` FK, `logged_by` FK (User), `component_name`, `component_code`, `quantity_used`, `unit`, `field_observations`, `logged_at`).
- [x] 10. **Nhóm thực thể Hệ thống**:
  - `Notification` (PK: `notification_id` / `Id`, `UserId` FK, `type`, `reference_type`, `reference_id` uuid, `title`, `body`, `is_read`, `sent_at`, `read_at`).
  - `AuditLog` (PK: `log_id` / `Id`, `UserId` FK, `table_name`, `record_id` uuid, `action_type`, `old_values` JSONB, `new_values` JSONB, `ip_address`, `user_agent`, `created_at`).

### Phase 1.2: Thiết lập Fluent API & Migration (PostgreSQL + PostGIS)
- [x] 11. **Cập nhật `ApplicationDbContext.cs`**: Đăng ký lại toàn bộ các `DbSet<T>` tương ứng với các thực thể mới cấu hình.
- [x] 12. **Cấu hình Fluent API cho GIS (PostGIS)**: Sử dụng NetTopologySuite để cấu hình cột địa không gian `geom` cho các thực thể `Region`, `Substation`, `TransmissionLine`, `Tower`, `Uav`, và `InspectionMedia`. Thiết lập Spatial Index (GiST index) để tối ưu hoá các câu truy vấn không gian.
- [x] 13. **Cấu hình Fluent API cho cột JSONB**: Cấu hình các trường `gps_track` (`MissionFlightLog`), `bounding_box` (`DetectedAnomaly`), `calculation_log` (`AssetHealthHistory`), và `old_values` / `new_values` (`AuditLog`) lưu trữ dưới định dạng `jsonb` của Postgres.
- [x] 14. **Thiết lập ràng buộc & Index**:
  - Đảm bảo các mã code như `uav_code`, `mission_code`, `ticket_code`, `tower_code`, `asset_code`, `category_code` là Unique.
  - Cấu hình khoá ngoại, ràng buộc xoá (Restrict thay vì Cascade ở các quan hệ quan trọng để tránh mất mát dữ liệu asset).
- [x] 15. **Global Query Filter cho Soft Delete**: Thiết lập filter tự động `IsDeleted == false` cho tất cả các thực thể thừa kế từ `BaseEntity`.
- [x] 16. **Tạo Migration sạch**: Xoá lịch sử Migrations cũ bị lỗi thời, chạy lệnh tạo Migration mới tinh `InitUavPmsSchema` và cập nhật cơ sở dữ liệu Postgres.

---

## EPIC 2: HOÀN THIỆN APPLICATION LAYER & HẠ TẦNG CƠ SỞ

### Phase 2.1: Base Services & Repositories
- [x] 17. **Cập nhật Generic Repository & Unit of Work**: Khớp cấu trúc DbContext mới, hỗ trợ truy vấn không đồng bộ và tự động tracking.
- [X] 18. **Dịch vụ Repositories Đặc thù**:
  - `ITowerRepository` & `IAssetRepository`: Các hàm truy vấn không gian phức tạp.
  - `IAnomalyRepository` & `IMaintenanceTicketRepository`: Hỗ trợ nạp eager loading các thực thể liên quan (Media, Category, User).
- [X] 19. **MediatR Pipeline Behaviors**:
  - Triển khai `ValidationBehavior` tích hợp FluentValidation tự động validate đầu vào của Request Command trước khi vào Handler.
  - Triển khai `LoggingBehavior` tự động ghi nhận nhật ký (NLog/Serilog) cho mỗi API Request/Response.
- [x] 20. **Security & Cryptography**:
  - Triển khai `BCryptPasswordHasher` để mã hoá bảo mật mật khẩu người dùng.
  - Triển khai `JwtProvider` sinh JWT Token đính kèm Claims chi tiết (UserId, Username, Roles).
- [X] 21. **Current User Service**: Viết `CurrentUserService` lấy thông tin `UserId` và `Roles` từ `HttpContext.User` của HTTP request hiện tại.
- [X] 21b. **Global Exception Handling Middleware**:
  - Triển khai Middleware bắt lỗi tập trung (Global Exception Handler) sử dụng tiêu chuẩn `ProblemDetails` (RFC 7807) của .NET.
  - Tự động bắt lỗi `ValidationException` từ MediatR/FluentValidation để format về dạng `400 Bad Request` chứa chi tiết các trường bị lỗi.
- [X] 21c. **API Versioning & Swagger Integration**:
  - Cấu hình thư viện `Asp.Versioning.Http` để quản lý phiên bản API động (URL versioning `/api/v{version:apiVersion}`).
  - Tích hợp với Swagger để tự động tạo và hiển thị tài liệu các phiên bản API tương ứng (v1, v2).
- [X] 21d. **Model Binding & Route Constraints**:
  - Thiết lập Route Constraints dạng `{id:guid}` trên các Controller để tự động validate kiểu dữ liệu ID của API.
  - Cấu hình Content Negotiation hỗ trợ thương lượng định dạng dữ liệu (JSON/XML) và cấu hình chuẩn camelCase / PascalCase.

---

## EPIC 3: NGHIỆP VỤ XÁC THỰC, PHÂN QUYỀN & AUDIT LOG (Identity & Audit)

### Phase 3.1: API Xác thực & Phân quyền (RBAC)
- [x] 22. **Lệnh Đăng nhập (`LoginCommand` / `POST /login`)**: Kiểm tra tài khoản, đối chiếu hash mật khẩu, trả về Access Token và Refresh Token.
- [x] 22b. **Lệnh Làm mới Token (`RefreshTokenCommand` / `POST /refresh-token`)**: Kiểm tra Refresh Token còn hạn trong database để cấp lại cặp token mới và hỗ trợ thu hồi.
- [X] 22c. **Tách bảng `RefreshTokens` hỗ trợ Multi-Device Session**:
  - Tạo entity `RefreshToken` riêng biệt với các trường: `Id`, `UserId`, `TokenHash`, `ExpiresAt`, `CreatedAt`, `RevokedAt`, `DeviceInfo`.
  - Xoá 2 cột `RefreshToken` và `RefreshTokenExpiryTime` khỏi bảng `Users`.
  - Hỗ trợ nhiều phiên đăng nhập đồng thời trên nhiều thiết bị (không ghi đè token cũ khi đăng nhập thiết bị mới).
  - Hỗ trợ chức năng thu hồi token theo session (logout từng thiết bị) và cải thiện khả năng kiểm toán bảo mật.
- [X] 22d. **Refactor Auth sang CQRS Pattern (Clean Architecture)**:
  - Chuyển toàn bộ business logic từ `AuthController` sang Application layer.
  - Tạo và triển khai các Command/Handler sau:
    - `LoginCommand` + `LoginCommandHandler` (`POST /api/v1/auth/login`)
    - `SendOtpCommand` + `SendOtpCommandHandler` (`POST /api/v1/auth/otp/send`)
    - `VerifyOtpCommand` + `VerifyOtpCommandHandler` (`POST /api/v1/auth/otp/verify`)
    - `RefreshTokenCommand` + `RefreshTokenCommandHandler` (`POST /api/v1/auth/refresh-token`)
    - `ResetPasswordCommand` + `ResetPasswordCommandHandler` (`POST /api/v1/auth/reset-password`)
  - Áp dụng `AuthResultDto` làm đối tượng trả về chung thay vì các entity/nội dung ẩn danh trực tiếp.
  - Controller chỉ giữ vai trò nhận HTTP Request -> gọi `_mediator.Send(command)` -> trả về kết quả.
  - Cập nhật `GlobalExceptionHandler` để xử lý tập trung:
    - `UnauthorizedAccessException` -> Trả về HTTP 401.
    - `ValidationException`, `NotFoundException`, `BusinessRuleException` -> Trả về lỗi định dạng chuẩn của hệ thống.
- [X] 22e. **Sửa lỗi NullReferenceException khi làm mới Token (Refresh Token)**:
  - Định nghĩa phương thức `GetByIdWithRolesAsync(Guid id)` trong `IUserRepository` và cài đặt trong `UserRepository` để nạp kèm (Eager Loading) `UserRoles` và `Role`.
  - Cập nhật `RefreshTokenCommandHandler` để gọi `GetByIdWithRolesAsync` thay vì `GetByIdAsync`.
  - Chạy Unit Test đảm bảo logic hoạt động chính xác.
- [X] 22f. **Chống Tấn công Dò quét Tài khoản (Timing Attack) trong Đăng nhập**:
  - Thực hiện xác thực mật khẩu giả định (dummy verification) khi người dùng không tồn tại hoặc không hoạt động nhằm chuẩn hoá thời gian phản hồi giữa user tồn tại và không tồn tại.
- [X] 22g. **Xác thực Chủ sở hữu của Step-Up Token**:
  - So sánh định danh người dùng trong Step-Up token với định danh người dùng đang đăng nhập trong `HttpContext.User` trước khi cho phép thực hiện hành động cần Step-Up.
- [X] 23. **Truy vấn Profile cá nhân (`GetMyProfileQuery`)**: Lấy thông tin tài khoản hiện tại dựa trên token gửi lên.
- [X] 24. **Cấu hình JwtBearerAuthentication**: Đăng ký Middleware xác thực JWT trong `Program.cs`. Thiết lập các Policy bảo vệ API dựa trên các vai trò: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- [X] 24b. **Bảo mật Endpoint Giám sát (`MonitorController`)**:
  - Áp dụng thuộc tính `[Authorize]` lên `MonitorController` để chặn truy cập ẩn danh.
  - Phân quyền chi tiết (Role-based Authorization) cho từng endpoint dựa trên vai trò của người dùng (ví dụ: `SystemAdmin`, `Manager`, `Analyst`, `Inspector`, `Technician` truy cập tương ứng với nhiệm vụ).
  - Kiểm tra và đảm bảo các endpoint: `GET /summary`, `GET /recent-defects`, `GET /defects-statistics`, `GET /mission-status`, `GET /inspections`, và `GET /alerts` từ chối người dùng chưa xác thực (401 Unauthorized) hoặc không đủ quyền (403 Forbidden).
- [X] 24c. **Giới hạn kích thước trang (Pagination Upper-Bound Validation)**:
  - Ràng buộc tham số `pageSize <= 100` tại các endpoint của `MonitorController` (`recent-defects` và `inspections`) để tránh cạn kiệt tài nguyên (DoS).

### Phase 3.2: Quản trị Người dùng & Tự động ghi nhận Audit Log
- [X] 25. **CRUD API quản lý người dùng (Users)**: Chỉ tài khoản có vai trò `SystemAdmin` mới được phép tạo mới, cập nhật thông tin, thay đổi vai trò (Role) hoặc đình chỉ (suspend) tài khoản khác.
- [X] 26. **EF Core Interceptor / SaveChanges Override**: Viết bộ lắng nghe tự động ghi log thay đổi. Trước khi lưu vào DB, kiểm tra các thay đổi ở trạng thái Added/Modified/Deleted, so sánh giá trị cũ và mới để sinh bản ghi chèn tự động vào bảng `AuditLogs` (tự động ghi nhận IP người gọi và UserAgent).
- [X] 27. **API truy cập lịch sử Audit (`GetAuditLogsQuery`)**: Dành riêng cho `SystemAdmin` và `Manager` giám sát các tác vụ nhạy cảm trong hệ thống.

---

## EPIC 4: QUẢN LÝ TÀI SẢN LƯỚI ĐIỆN & GIS (Asset & Spatial Module)

### Phase 4.1: API Quản lý Tài sản (Hierarchy Asset Registry)
- [ ] 28. **CRUD API phân cấp tài sản**:
  - CRUD cho `Regions`, `Substations`, `TransmissionLines`.
  - Quản lý `Towers` (Cột điện) và `Assets` (Thiết bị gắn trên cột như bát sứ, dây cáp, thanh giằng...).
- [ ] 29. **Lệnh tạo Cột điện địa không gian (`CreateTowerCommand`)**: Nhận toạ độ phẳng Lat/Lng từ client, tự động tạo đối tượng địa lý `Point` (SRID 4326) để lưu trữ vào trường `geom`.
- [ ] 30. **Nhập dữ liệu hàng loạt từ file Excel (`ImportTowersCommand`)**: Đọc file Excel danh sách cột điện truyền tải cùng toạ độ địa lý, thực hiện bulk insert tối ưu hiệu năng và tự động gán các loại thiết bị (`Assets`) mặc định lên các cột tương ứng.

### Phase 4.2: Truy vấn Không gian Bản đồ (GIS API)
- [ ] 31. **Truy vấn lấy tài sản theo viewport bản đồ (`GetAssetsInBoundingBoxQuery`)**: Nhận toạ độ hộp giới hạn (Bounding Box: MinLat, MinLng, MaxLat, MaxLng) từ bản đồ LeafletJS, trả về danh sách các cột điện, trạm biến áp nằm bên trong vùng hiển thị để tối ưu băng thông.
- [ ] 32. **Định dạng dữ liệu sự cố dạng GeoJSON (`GetDefectsGeoJsonQuery`)**: Query danh sách các `DetectedAnomalies` đang hoạt động kèm vị trí toạ độ địa lý của cột điện chứa lỗi, format chuẩn định dạng GeoJSON để frontend LeafletJS render trực tiếp lên bản đồ nhiệt (Heatmap) hoặc bản đồ điểm (Marker Cluster).

---

## EPIC 5: ĐIỀU PHỐI CHUYẾN BAY & TIẾP NHẬN DỮ LIỆU HIỆN TRƯỜNG (Mission & Media Ingestion)

### Phase 5.1: Quản lý Chuyến bay kiểm tra (Missions)
- [ ] 33. **Quản lý Hạm đội Thiết bị bay (`Uavs`)**: API CRUD và theo dõi thông tin UAV (dung lượng pin, tình trạng vận hành, vị trí thực tế thông qua GPS).
- [ ] 34. **Lệnh tạo và Giao việc Chuyến bay (`CreateMissionCommand`)**: Manager lên kế hoạch, gán Inspector phụ trách, lựa chọn thiết bị bay `Uav` trống và danh sách tuyến đường dây truyền tải cần kiểm tra (`MissionTargetLines`).
- [ ] 35. **Quy trình chuyển đổi trạng thái chuyến bay**:
  - API `StartMission`: Inspector bắt đầu bay, chuyển trạng thái chuyến bay sang `InProgress` và kích hoạt thời gian bắt đầu.
  - API `CompleteMission`: Kết thúc chuyến bay, chuyển trạng thái sang `Completed`.
- [ ] 36. **API nạp Nhật ký bay (`UploadFlightLogCommand`)**: Inspector tải lên tệp log chuyến bay từ drone (chứa chuỗi GPS track dạng JSONB, pin thấp nhất ghi nhận, độ cao tối đa, thời gian bay) để lưu vào bảng `MissionFlightLogs`.

### Phase 5.2: Tải lên hình ảnh kiểm định & Báo cáo Sự cố
- [ ] 37. **API Tải ảnh/video kiểm tra (`UploadInspectionMediaCommand`)**: Inspector tải lên các tệp đa phương tiện độ phân giải cao thu thập từ UAV, liên kết tệp đó với `MissionId` và `AssetId` cụ thể.
- [ ] 38. **Dịch vụ tự động phân tích GPS EXIF**: Viết Service đọc siêu dữ liệu EXIF của tệp ảnh tải lên, trích xuất toạ độ GPS lúc chụp và thời gian chụp để lưu vào trường `geom` và `captured_at` của bảng `InspectionMedia`.
- [ ] 39. **Hạ tầng Lưu trữ Tệp (`IFileStorageService`)**: Triển khai lưu trữ vật lý trên Local Disk hoặc tích hợp Object Storage (MinIO/S3).
- [ ] 40. **Bắn sự kiện xử lý AI**: Sau khi lưu ảnh thành công, tự động phát hành sự kiện `MediaUploadedEvent` thông qua RabbitMQ hoặc MediatR nội bộ để kích hoạt chuỗi phân tích AI ngầm.
- [ ] 41. **API báo cáo sự cố hiện trường (`SubmitIncidentReportCommand`)**: Cho phép Inspector gửi báo cáo khẩn cấp khi gặp sự cố bất ngờ ngoài thực địa (drone rơi, thời tiết xấu hoãn bay, hoặc phát hiện phá hoại, sạt lở hành lang lưới điện cần can thiệp ngay) lưu vào bảng `IncidentReports`.

---

## EPIC 6: AI PIPELINE & THẨM ĐỊNH LỖI (AI Defect Detection & HITL)

### Phase 6.1: Tích hợp Python YOLOv8 & Phân loại sự cố
- [ ] 42. **HTTP Client tích hợp Polly**: Tạo bộ kết nối HTTP hoặc RabbitMQ Consumer kết nối với máy chủ xử lý Python AI. Sử dụng thư viện Polly để cấu hình cơ chế tự động thử lại (Retry) và ngắt mạch (Circuit Breaker) đề phòng máy chủ AI quá tải.
- [ ] 43. **Đồng bộ Kết quả Phân tích AI**: Tiếp nhận danh sách các hộp giới hạn (bounding box), độ tin cậy (confidence score) và phân loại lỗi từ Python AI (5 nhóm lỗi: Corrosion, Surface Crack, Vegetation Encroachment, Missing Components, Insulator Damage).
- [ ] 44. **Tự động lưu sự cố (`DetectedAnomalies`)**: Lưu thông tin sự cố vào cơ sở dữ liệu với trạng thái mặc định là `Pending` (Chờ thẩm định).
- [ ] 45. **Tự động Kích hoạt Cảnh báo Khẩn cấp**: Nếu lỗi phát hiện nằm trong danh mục sự cố khẩn cấp (cờ `is_emergency_class` là true trong bảng `DefectCategories` bao gồm: Cháy/Nhiệt độ cao, Đứt dây cáp, Đổ cột điện), hệ thống lập tức tự động tạo bản ghi `EmergencyAlert` với mức độ ưu tiên cao nhất (`Priority` = Critical).

### Phase 6.2: Nghiệp vụ Duyệt lỗi của Analyst (Human-in-the-loop)
- [ ] 46. **API lấy danh sách lỗi chờ duyệt phân trang (`GetPendingAnomaliesQuery`)**: Hiển thị ảnh chụp gốc, khung bounding box do AI vẽ đè lên và các thông số đi kèm để Analyst thẩm định.
- [ ] 47. **Lệnh duyệt lỗi (`ValidateAnomalyCommand`)**: Analyst có quyền xác nhận lỗi (`Confirmed` - lỗi chính xác) hoặc bác bỏ lỗi (`Rejected` - nhận diện sai của AI), cập nhật ghi chú cá nhân (`analyst_notes`), hệ thống tự động lưu ID của Analyst và thời điểm duyệt.

---

## EPIC 7: BỘ MÁY ĐÁNH GIÁ SỨC KHỎE TÀI SẢN (Asset Health Assessment)

### Phase 7.1: Công cụ Tính điểm Tự động (Rule-Based Engine)
- [ ] 48. **Phát triển `AssetHealthCalculationService`**: Triển khai công thức tính điểm sức khoẻ (Health Score từ 0 - 100) cho `Asset` dựa trên cấu hình trọng số quy định trong tài liệu Capstone:
  - Mức độ nghiêm trọng của các lỗi hiện có (`Defect Severity`): **50%**
  - Số lượng lỗi đang hoạt động trên thiết bị (`Number of Active Defects`): **20%**
  - Lịch sử sửa chữa, bảo trì (`Maintenance History`): **20%**
  - Thời gian kể từ lần kiểm định gần nhất (`Inspection Recency`): **10%**
- [ ] 49. **Phân loại Mức độ Rủi ro (Risk Level)**: Gán nhãn tự động dựa trên điểm số:
  - **80 – 100**: Low Risk (Giám sát định kỳ)
  - **60 – 79**: Medium Risk (Lên lịch bảo trì)
  - **40 – 59**: High Risk (Ưu tiên bảo trì)
  - **0 – 39**: Critical Risk (Yêu cầu xử lý khẩn cấp lập tức)
- [ ] 50. **Thiết lập cơ chế tính lại tự động (Event Listener)**:
  - Lắng nghe sự kiện Analyst duyệt lỗi (`AnomalyValidatedEvent`): Tự động tính toán lại điểm sức khoẻ của Asset liên quan.
  - Lắng nghe sự kiện Đóng phiếu bảo trì (`MaintenanceTicketClosedEvent`): Khi sự cố được khắc phục xong, tính lại điểm để khôi phục sức khoẻ cho Asset.
  - Lắng nghe sự kiện hoàn tất kiểm tra mới.
- [ ] 51. **Ghi nhận lịch sử tính toán (`AssetHealthHistories`)**: Lưu lại điểm số mới, nhãn rủi ro, thời điểm tính và log chi tiết các hệ số nhân của công thức dạng JSONB (`calculation_log`), đồng thời cập nhật trực tiếp hai trường `current_health_score` và `risk_level` trong bảng `Assets`.

---

## EPIC 8: LUỒNG CẢNH BÁO KHẨN CẤP REAL-TIME (Emergency Alerts & Escalation)

### Phase 8.1: Thiết lập SignalR & Luồng Phản hồi Khẩn cấp
- [ ] 52. **Cấu hình SignalR Hub**: Thiết lập kết nối thời gian thực giữa Backend và Web Client dành riêng cho Analyst và Manager.
- [ ] 53. **Đẩy thông báo Khẩn cấp Tức thì**: Ngay khi một `EmergencyAlert` được kích hoạt ngầm từ kết quả AI, SignalR tự động phát tín hiệu âm thanh và popup cảnh báo lên màn hình làm việc của tất cả các Analyst đang trực tuyến.
- [ ] 54. **API phản hồi khẩn cấp của Analyst (`ReviewEmergencyAlertCommand`)**: Cho phép Analyst xác nhận nhanh tình trạng cảnh báo khẩn cấp (xác nhận hoặc từ chối cảnh báo giả).
- [ ] 55. **Quy trình Leo thang Cảnh báo (`EscalateAlertCommand`)**: Trong trường hợp sự cố quá nghiêm trọng hoặc quá hạn xử lý, Analyst gửi yêu cầu leo thang trực tiếp tới Manager chỉ định. Lưu trữ thông tin lý do leo thang và các bên liên quan vào bảng `AlertEscalations` để theo dõi trách nhiệm.

---

## EPIC 9: LUỒNG CÔNG VIỆC BẢO TRÌ & QUẢN LÝ VẬT TƯ (Maintenance & Material Logistics)

### Phase 9.1: Vận hành Phiếu Bảo trì (Maintenance Tickets)
- [ ] 56. **Tự động đề xuất & Tạo phiếu sửa chữa (`CreateMaintenanceTicketCommand`)**: Hệ thống tự động đề xuất tạo phiếu bảo trì cho các sự cố đã được xác nhận (`Confirmed` Anomaly), hoặc cho phép Manager tự tạo tay. Thiết lập độ ưu tiên, hạn hoàn thành (`due_date`) và gán trực tiếp cho tài khoản của Technician.
- [ ] 57. **Cập nhật trạng thái thực thi**:
  - API chuyển trạng thái sang `InProgress` khi Technician xác nhận bắt đầu tiến hành sửa chữa tại cột điện.
- [ ] 58. **Tải lên Minh chứng Hoàn thành (`SubmitMaintenanceProofCommand`)**: Technician tải lên hình ảnh chụp thiết bị sau khi đã sửa chữa/thay thế (`after_repair_image_url`) và ghi chú kỹ thuật để lưu vào bảng `MaintenanceProofs`. Chuyển trạng thái phiếu sang `Pending Verification`.

### Phase 9.2: Ghi nhận vật tư kỹ thuật (Material Logs)
- [ ] 59. **API khai báo vật tư sử dụng (`LogMaterialUsageCommand`)**: Cho phép Technician khai báo chi tiết các vật tư đã dùng để sửa chữa thiết bị (ví dụ: Thay thế bát sứ cách điện mã cách điện XYZ, số lượng 2 cái) cùng với các quan sát thực tế tại hiện trường, lưu trữ trực tiếp vào bảng `MaterialLogs` liên kết với ticket.
- [ ] 60. **Nghiệm thu và Đóng phiếu bảo trì (`CloseTicketCommand`)**: Manager kiểm tra hình ảnh minh chứng và nhật ký vật tư sử dụng. Nếu đạt yêu cầu, phê duyệt đóng phiếu (Trạng thái chuyển sang `Resolved`), hệ thống tự động cập nhật trạng thái lỗi liên quan trong bảng `DetectedAnomalies` thành `Resolved` và kích hoạt dịch vụ tính toán lại điểm sức khỏe của Asset.

---

## EPIC 10: THÔNG BÁO HỆ THỐNG & BÁO CÁO THỐNG KÊ (Notifications & Analytics)

### Phase 10.1: Dịch vụ Thông báo in-app (Notifications Service)
- [ ] 61. **Phát triển `NotificationService`**: Viết cơ chế lưu trữ và phân phối thông báo in-app vào bảng `Notifications`.
- [ ] 62. **Tích hợp thông báo tự động theo sự kiện**:
  - Gửi thông báo cho Inspector khi được giao chuyến bay mới.
  - Gửi thông báo cho Analyst khi có hình ảnh kiểm tra mới cần duyệt hoặc cảnh báo khẩn cấp mới.
  - Gửi thông báo cho Technician khi có phiếu bảo trì mới được gán.
  - Gửi thông báo cho Manager khi có yêu cầu leo thang cảnh báo hoặc phiếu bảo trì chuyển sang trạng thái chờ nghiệm thu.
- [X] 63. **Refactor Notification sang CQRS Pattern (Clean Architecture)**:
  - Chuyển toàn bộ business logic liên quan đến thông báo từ Controllers sang Application layer.
  - Tạo và triển khai các Command/Handler sau:
    - `CreateNotificationCommand` + `CreateNotificationCommandHandler`
    - `MarkNotificationAsReadCommand` + `MarkNotificationAsReadCommandHandler`
    - `DeleteNotificationCommand` + `DeleteNotificationCommandHandler`
  - Tạo và triển khai các Query/Handler sau:
    - `GetNotificationsQuery` + `GetNotificationsQueryHandler`
    - `GetNotificationByIdQuery` + `GetNotificationByIdQueryHandler`
  - Sử dụng `NotificationDto` làm đối tượng trả về chung từ Application layer thay vì trả trực tiếp domain entities.
  - Loại bỏ hoàn toàn sự phụ thuộc trực tiếp vào repository từ controller.

### Phase 10.2: Truy vấn Thống kê & Xuất dữ liệu (Analytics & Export)
- [ ] 64. **API Thống kê Xu hướng Sự cố (`GetDefectAnalyticsQuery`)**: Trả về số liệu thống kê về tần suất xuất hiện các nhóm lỗi theo tháng, phân bố mức độ nghiêm trọng của lỗi trên các vùng lưới điện truyền tải.
- [ ] 65. **API Thống kê Tỷ lệ Kiểm tra (`GetInspectionCoverageQuery`)**: Tính toán phần trăm số lượng trạm biến áp, tuyến dây, cột điện đã được kiểm tra bằng UAV trong một khoảng thời gian chọn trước.
- [ ] 66. **Tích hợp QuestPDF & EPPlus xuất báo cáo**:
  - Triển khai API xuất danh sách sự cố và vật tư tiêu hao bảo trì ra tệp Excel (`EPPlus`).
  - Triển khai API xuất Báo cáo kỹ thuật tổng hợp tình trạng sức khoẻ lưới điện kèm đồ thị trực quan ra tệp PDF (`QuestPDF`).