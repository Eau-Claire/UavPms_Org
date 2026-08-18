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
- [X] 22h. **Refactor Authentication dùng Email làm Định danh Đăng nhập Duy nhất (Single Login Identifier - Issue #84)**:
  - Loại bỏ thuộc tính `Username` khỏi domain entity `User`, DTOs (`AuthUserDto`, `UserDetailDto`, `AssignableUserDto`), DbContext Configuration, và Database Schema.
  - Cập nhật toàn bộ các luồng xác thực (`LoginCommand`, `VerifyOtpCommand`, `SendOtpCommand`, `ResetPasswordCommand`, OTP Handlers) truy vấn người dùng duy nhất bằng `Email` (`x.Email == input`).
  - Loại bỏ các phương thức và kiểm tra trùng lặp `Username` (`GetByUsernameWithRolesAsync`) trong Repositories và Command Handlers.
  - Cập nhật `JwtProvider` phát hành Claim `ClaimTypes.Name` & `ClaimTypes.Email` dựa trên Email người dùng.
  - Tạo Migration gỡ bỏ cột `Username` và Unique Index `IX_Users_Username` khỏi bảng `Users`.
  - Cập nhật và bổ sung toàn bộ các Unit Tests bao gồm kiểm tra trùng lặp Email và các kịch bản xác thực mới.
- [X] 23. **Truy vấn Profile cá nhân (`GetMyProfileQuery`)**: Lấy thông tin tài khoản hiện tại dựa trên token gửi lên.
- [X] 24. **Cấu hình JwtBearerAuthentication**: Đăng ký Middleware xác thực JWT trong `Program.cs`. Thiết lập các Policy bảo vệ API dựa trên các vai trò: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- [X] 24b. **Bảo mật Endpoint Giám sát (`MonitorController`)**:
  - Áp dụng thuộc tính `[Authorize]` lên `MonitorController` để chặn truy cập ẩn danh.
  - Phân quyền chi tiết (Role-based Authorization) cho từng endpoint dựa trên vai trò của người dùng (ví dụ: `SystemAdmin`, `Manager`, `Analyst`, `Inspector`, `Technician` truy cập tương ứng với nhiệm vụ).
  - Kiểm tra và đảm bảo các endpoint: `GET /summary`, `GET /recent-defects`, `GET /defects-statistics`, `GET /mission-status`, `GET /inspections`, và `GET /alerts` từ chối người dùng chưa xác thực (401 Unauthorized) hoặc không đủ quyền (403 Forbidden).
- [X] 24c. **Giới hạn kích thước trang (Pagination Upper-Bound Validation)**:
  - Ràng buộc tham số `pageSize <= 100` tại các endpoint của `MonitorController` (`recent-defects` và `inspections`) để tránh cạn kiệt tài nguyên (DoS).
- [ ] 24d. **Chuẩn hóa Phân quyền RBAC toàn bộ API Endpoints theo Authorization Matrix**:
  - Áp dụng phân quyền vai trò (SystemAdmin, Manager, Inspector, Analyst, MaintenanceTechnician) trên toàn bộ endpoints của 4 Microservices (`IdentityService`, `OperationsService`, `AIInspectionService`, `NotificationService`).
  - Định nghĩa tập trung `UserRoles.cs` trong `UavPms.Shared.Contracts` thay cho hardcode chuỗi role trong Controller.
  - Kiểm tra quyền sở hữu tài nguyên (Inspector assigned mission, User owned notification) trả về HTTP 403 Forbidden.
  - Tách bạch các endpoint dành cho thiết bị / internal services (`/devices/heartbeat`, `/vision/detections`, `/notifications/*` internal).
  - Cấu hình Swagger Security Scheme hiển thị khóa xác thực Bearer token cho toàn bộ các dịch vụ.

### Phase 3.2: Quản trị Người dùng & Tự động ghi nhận Audit Log
- [X] 25. **CRUD API quản lý người dùng (Users)**: Chỉ tài khoản có vai trò `SystemAdmin` mới được phép tạo mới, cập nhật thông tin, thay đổi vai trò (Role) hoặc đình chỉ (suspend) tài khoản khác.
- [X] 26. **EF Core Interceptor / SaveChanges Override**: Viết bộ lắng nghe tự động ghi log thay đổi. Trước khi lưu vào DB, kiểm tra các thay đổi ở trạng thái Added/Modified/Deleted, so sánh giá trị cũ và mới để sinh bản ghi chèn tự động vào bảng `AuditLogs` (tự động ghi nhận IP người gọi và UserAgent).
- [X] 27. **API truy cập lịch sử Audit (`GetAuditLogsQuery`)**: Dành riêng cho `SystemAdmin` và `Manager` giám sát các tác vụ nhạy cảm trong hệ thống.
- [x] 27b. **API lấy danh sách người dùng cho phân công chuyến bay (`GetAssignableUsersQuery` / `GET /api/v1/users/assignable`)**: Yêu cầu quyền quản trị (`SystemAdmin`/`Manager`), chỉ trả về người dùng hoạt động (`Status == "Active"`) có vai trò `Inspector`, không bao gồm thông tin nhạy cảm.

### Phase 3.3: Tái Cấu Trúc Kiến Trúc & Bảo Mật Nâng Cao cho IdentityService (Refactoring & Enterprise Best Practices)
- [x] 27c. **Pure Functions & Common Utilities**:
  - Trích xuất `TokenHasher` băm SHA256 string (Pure Function).
  - Trích xuất `RedisKeyBuilder` định dạng Redis Keys (Pure Function).
  - Trích xuất `OtpCalculations` tính toán Cooldown và giới hạn số lần thử nhập sai (Pure Function).
- [x] 27d. **Reusable Auth Component (`IUserTokenService`)**:
  - Đóng gói `UserTokenService` chịu trách nhiệm sinh cặp AccessToken + RefreshToken, lưu Session Entity và trả về `AuthResultDto`.
  - Tái sử dụng ở `LoginCommandHandler`, `RefreshTokenCommandHandler`, và `VerifyOtpCommandHandler`.
- [x] 27e. **Strategy Pattern cho OTP Verification (`IOtpVerificationStrategy`)**:
  - Tách luồng `if-else` lồng nhau trong `VerifyOtpCommandHandler` thành các Strategy (`LoginOtpStrategy`, `ForgotPasswordOtpStrategy`, `StepUpOtpStrategy`) kèm `OtpVerificationStrategyResolver`.
- [x] 27f. **Strongly-Typed Options Pattern (`IOptions<JwtOptions>`)**:
  - Tạo `JwtOptions` class bind cấu hình JWT từ `appsettings.json`, hỗ trợ `ValidateOnStart()` (Fail Fast) thay thế việc đọc string indexers `IConfiguration["Jwt:..."]`.
- [x] 27g. **Đóng gói Rich Domain Model & Enum `UserStatus`**:
  - Chuyển trường `User.Status` từ `string` sang Enum `UserStatus` (`Active`, `Inactive`, `Pending`, `Suspended`).
  - Đóng gói các phương thức biến đổi trạng thái (`VerifyEmail()`, `Activate()`, `Deactivate()`, `Suspend()`) vào bên trong Entity `User`.
- [x] 27h. **Refresh Token Reuse Detection & Revocation Cascade (OAuth 2.0 Security BCP)**:
  - Phát hiện tái sử dụng Refresh Token đã bị thu hồi (`RevokedAt != null`) ➔ Tự động thu hồi TẤT CẢ các Refresh Token active của User để ngăn chặn tấn công chiếm đoạt session (Token Theft).
- [x] 27i. **Lan truyền `CancellationToken` xuyên suốt**:
  - Bổ sung tham số `CancellationToken cancellationToken = default` cho tất cả phương thức bất đồng bộ trong `IUserTokenService` và các invocations liên quan.

---

## EPIC 4: QUẢN LÝ TÀI SẢN LƯỚI ĐIỆN & GIS (Asset & Spatial Module)

### Phase 4.1: API Quản lý Tài sản (Hierarchy Asset Registry)
- [x] 28. **CRUD API phân cấp tài sản**:
  - CRUD cho `Regions`, `Substations`, `TransmissionLines`.
  - Quản lý `Towers` (Cột điện) và `Assets` (Thiết bị gắn trên cột như bát sứ, dây cáp, thanh giằng...).
- [x] 29. **Lệnh tạo Cột điện địa không gian (`CreateTowerCommand`)**: Nhận toạ độ phẳng Lat/Lng từ client, tự động tạo đối tượng địa lý `Point` (SRID 4326) để lưu trữ vào trường `geom`.
- [x] 30. **Nhập dữ liệu hàng loạt từ file Excel (`ImportTowersCommand`)**: Đọc file Excel danh sách cột điện truyền tải cùng toạ độ địa lý, thực hiện bulk insert tối ưu hiệu năng và tự động gán các loại thiết bị (`Assets`) mặc định lên các cột tương ứng.

### Phase 4.2: Truy vấn Không gian Bản đồ (GIS API)
- [ ] 31. **Truy vấn lấy tài sản theo viewport bản đồ (`GetAssetsInBoundingBoxQuery`)**: Nhận toạ độ hộp giới hạn (Bounding Box: MinLat, MinLng, MaxLat, MaxLng) từ bản đồ LeafletJS, trả về danh sách các cột điện, trạm biến áp nằm bên trong vùng hiển thị để tối ưu băng thông.
- [ ] 32. **Định dạng dữ liệu sự cố dạng GeoJSON (`GetDefectsGeoJsonQuery`)**: Query danh sách các `DetectedAnomalies` đang hoạt động kèm vị trí toạ độ địa lý của cột điện chứa lỗi, format chuẩn định dạng GeoJSON để frontend LeafletJS render trực tiếp lên bản đồ nhiệt (Heatmap) hoặc bản đồ điểm (Marker Cluster).

---

## EPIC 5: ĐIỀU PHỐI CHUYẾN BAY & TIẾP NHẬN DỮ LIỆU HIỆN TRƯỜNG (Mission & Media Ingestion)

### Phase 5.1: Quản lý Chuyến bay kiểm tra (Missions)
- [ ] 33. **Quản lý Hạm đội Thiết bị bay (`Uavs`)**: API CRUD và theo dõi thông tin UAV (dung lượng pin, tình trạng vận hành, vị trí thực tế thông qua GPS).
- [X] 34. **Lệnh tạo và Giao việc Chuyến bay (`CreateMissionCommand`)**: Manager lên kế hoạch, gán Inspector phụ trách, lựa chọn thiết bị bay `Uav` trống. (Đã hoàn thành với trường AssignedToUserId, DroneCode, Title, RouteData).
- [X] 35. **Quy trình chuyển đổi trạng thái chuyến bay**: (Đã hoàn thành với CRUD CRUD & Status Management cho Pending, In Progress, Completed).
- [X] 35b. **API Chuyến bay của tôi (`GET /missions/my`)**: Dành riêng cho kỹ sư (Inspector) truy xuất các chuyến bay được phân công.
- [X] 35c. **Bắn sự kiện `MissionCreatedEvent` qua RabbitMQ** cho các luồng xử lý ngoài.
- [ ] 36. **API nạp Nhật ký bay (`UploadFlightLogCommand`)**: Inspector tải lên tệp log chuyến bay từ drone (chứa chuỗi GPS track dạng JSONB, pin thấp nhất ghi nhận, độ cao tối đa, thời gian bay) để lưu vào bảng `MissionFlightLogs`.

### Phase 5.2: Tải lên hình ảnh kiểm định & Báo cáo Sự cố
- [x] 37. **API Tải ảnh/video kiểm tra (`UploadInspectionMediaCommand`)**: Inspector tải lên các tệp đa phương tiện độ phân giải cao thu thập từ UAV, liên kết tệp đó với `MissionId` và `AssetId` cụ thể.
- [ ] 38. **Dịch vụ tự động phân tích GPS EXIF**: Viết Service đọc siêu dữ liệu EXIF của tệp ảnh tải lên, trích xuất toạ độ GPS lúc chụp và thời gian chụp để lưu vào trường `geom` và `captured_at` của bảng `InspectionMedia`.
- [x] 39. **Hạ tầng Lưu trữ Tệp (`IFileStorageService`)**: Triển khai lưu trữ vật lý trên Local Disk hoặc tích hợp Object Storage (MinIO/S3).
- [x] 40. **Bắn sự kiện xử lý AI**: Sau khi lưu ảnh thành công, tự động phát hành sự kiện `MediaUploadedEvent` / `ImageUploadedEvent` thông qua RabbitMQ hoặc MediatR nội bộ để kích hoạt chuỗi phân tích AI ngầm.
- [ ] 41. **API báo cáo sự cố hiện trường (`SubmitIncidentReportCommand`)**: Cho phép Inspector gửi báo cáo khẩn cấp khi gặp sự cố bất ngờ ngoài thực địa (drone rơi, thời tiết xấu hoãn bay, hoặc phát hiện phá hoại, sạt lở hành lang lưới điện cần can thiệp ngay) lưu vào bảng `IncidentReports`.

---

## EPIC 6: AI PIPELINE & THẨM ĐỊNH LỖI (AI Defect Detection & HITL)

### Phase 6.1: Tích hợp Python YOLOv8 & Phân loại sự cố
- [x] 42. **HTTP Client / Message Consumer tích hợp Python AI**: Tạo bộ kết nối HTTP hoặc RabbitMQ Consumer kết nối với máy chủ xử lý Python AI. Sử dụng thư viện Polly để cấu hình cơ chế tự động thử lại (Retry) và ngắt mạch (Circuit Breaker) đề phòng máy chủ AI quá tải.
- [x] 43. **Đồng bộ Kết quả Phân tích AI**: Tiếp nhận danh sách các hộp giới hạn (bounding box), độ tin cậy (confidence score) và phân loại lỗi từ Python AI (5 nhóm lỗi: Corrosion, Surface Crack, Vegetation Encroachment, Missing Components, Insulator Damage).
- [x] 44. **Tự động lưu sự cố (`DetectedAnomalies`)**: Lưu thông tin sự cố vào cơ sở dữ liệu với trạng thái mặc định là `Pending` (Chờ thẩm định).
- [x] 45. **Tự động Kích hoạt Cảnh báo Khẩn cấp**: Nếu lỗi phát hiện nằm trong danh mục sự cố khẩn cấp (cờ `is_emergency_class` là true trong bảng `DefectCategories` bao gồm: Cháy/Nhiệt độ cao, Đứt dây cáp, Đổ cột điện), hệ thống lập tức tự động tạo bản ghi `EmergencyAlert` với mức độ ưu tiên cao nhất (`Priority` = Critical).

### Phase 6.2: Nghiệp vụ Duyệt lỗi của Analyst (Human-in-the-loop)
- [x] 46. **API lấy danh sách lỗi chờ duyệt phân trang (`GetMissionAiDetectionsQuery`)**: Hiển thị ảnh chụp gốc, khung bounding box do AI vẽ đè lên và các thông số đi kèm để Analyst thẩm định.
- [x] 47. **Lệnh duyệt lỗi (`ReviewMissionAiDetectionCommand`)**: Analyst có quyền xác nhận lỗi (`Confirmed` - lỗi chính xác) hoặc bác bỏ lỗi (`Rejected` - nhận diện sai của AI), cập nhật ghi chú cá nhân (`analyst_notes`), hệ thống tự động lưu ID của Analyst và thời điểm duyệt.

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
- [x] 61. **Phát triển `NotificationService`**: Viết cơ chế lưu trữ và phân phối thông báo in-app vào bảng `Notifications`.
- [x] 62. **Tích hợp thông báo tự động theo sự kiện**:
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

---

## EPIC 11: TÁI CẤU TRÚC KIẾN TRÚC & BẢO MẬT CHUẨN DOANH NGHIỆP CHO 4 MICROSERVICES CÒN LẠI (Operations, AIInspection, Notification, InspectionEvaluation)

### Phase 11.1: Refactoring OperationsService
- [x] 67. **Strongly-Typed Options Pattern (`SupabaseOptions`, `RabbitMQOptions`)**: Ép kiểu cấu hình với `ValidateOnStart()` (Fail Fast) thay vì đọc indexer `IConfiguration["..."]`.
- [x] 68. **Pure Utility (`FileSanitizer`)**: Trích xuất bộ chuẩn hóa tên tệp và kiểm tra định dạng mở rộng tập tin an toàn.
- [x] 69. **Rich Domain Model & Enums (`MissionStatus`, `TicketStatus`, `TicketPriority`)**: Thay thế các chuỗi magic string bằng Enum và đóng gói các hành vi chuyển trạng thái trong domain entity (`Mission.cs`, `MaintenanceTicket.cs`).
- [x] 70. **Lan truyền `CancellationToken`**: Bổ sung tham số `CancellationToken cancellationToken = default` xuyên suốt `IFileStorageService` và các Handlers.

### Phase 11.2: Refactoring AIInspectionService
- [x] 71. **Strongly-Typed Options Pattern (`PythonAIOptions`, `RabbitMQOptions`)**: Đóng gói cấu hình kết nối AI với `ValidateOnStart()`.
- [x] 72. **Pure Utility (`BoundingBoxCalculations`)**: Trích xuất hàm static tính toán chỉ số IOU, diện tích hộp giới hạn và tỉ lệ khung hình.
- [x] 73. **Rich Domain Model & Enums (`AnomalyStatus`, `EmergencyAlertPriority`, `MediaValidationStatus`)**: Đóng gói các trạng thái sự cố và quy trình thẩm định lỗi AI (`Anomaly.Confirm()`, `Anomaly.Reject()`).
- [x] 74. **Lan truyền `CancellationToken`**: Bổ sung `CancellationToken` cho `IAIAnalysisService`, HTTP Client kết nối Python AI và các Handlers.

### Phase 11.3: Refactoring NotificationService
- [x] 75. **Strongly-Typed Options Pattern (`SendGridOptions`, `SupabaseOptions`, `RabbitMQOptions`)**: Đóng gói cấu hình gửi email/thông báo với DataAnnotations Validation.
- [x] 76. **Pure Utility (`NotificationFormatter`)**: Trích xuất hàm định dạng tiêu đề, nội dung và thay thế template biến thông báo.
- [x] 77. **Rich Domain Model & Enums (`NotificationType`)**: Chuyển đổi thuộc tính `Type` sang Enum và đóng gói hành vi `Notification.MarkAsRead()`.
- [x] 78. **Lan truyền `CancellationToken`**: Bổ sung `CancellationToken` cho `IEmailService`, `IRealtimeNotificationService` và các Handlers.

### Phase 11.4: Refactoring InspectionEvaluationService
- [x] 79. **Strongly-Typed Options Pattern (`EvaluationThresholdOptions`)**: Đóng gói cấu hình ngưỡng điểm severity và rủi ro với DataAnnotations Validation.
- [x] 80. **Pure Utility Engine (`DetectionEvaluationEngine`)**: Trích xuất engine tính toán mức độ nghiêm trọng, điểm ưu tiên và rủi ro.
- [x] 81. **Rich Domain Model & Enums (`EvaluationSeverity`, `EvaluationRiskLevel`)**: Cập nhật các Enums mức độ rủi ro và nghiêm trọng được gõ kiểu mạnh.
- [x] 82. **Lan truyền `CancellationToken`**: Bổ sung `CancellationToken` cho gRPC services và Handlers.

---

## EPIC 12: KIỂM THỬ TOÀN DIỆN HỆ THỐNG (Comprehensive Test Plan & Execution)

*Mục tiêu: Xây dựng bộ Test Plan bài bản, chi tiết, bao phủ 100% nghiệp vụ cốt lõi của hệ thống UAV-PMS Backend trên cả 5 Microservices (.NET 9), API Gateway (Ocelot), gRPC internal, RabbitMQ Event Bus, SignalR Realtime và giao diện người dùng Frontend. Đảm bảo tính chính xác, bảo mật, hiệu năng và khả năng chịu tải trước khi chuyển sang Production.*

*Tham chiếu: `api_spec.md` (Đặc tả API), `service-architecture.md` (Kiến trúc Microservices), `database_schema.md` (ERD & Từ điển dữ liệu).*

*Công cụ sử dụng: xUnit + FluentAssertions + Moq (Unit Test), WebApplicationFactory (Integration Test), Postman/Newman (API Contract Test), JMeter/k6 (Performance & Load Test), Playwright (UI E2E Test).*

---

### Phase 12.0: Dashboard Tổng hợp Tiến độ Kiểm thử (Test Progress Dashboard)

- [ ] 83. **Thiết lập Dashboard theo dõi tiến độ Test Plan**:
  - Tổng hợp số lượng Test Case toàn hệ thống theo trạng thái: `Passed` / `Failed` / `Blocked` / `Untested` / `Skipped`.
  - Phân nhóm theo Module/Service: IdentityService, OperationsService, AIInspectionService, InspectionEvaluationService, NotificationService, ApiGateway, Frontend.
  - Phân nhóm theo loại Test: Unit Test, Integration Test, API Contract Test, Security Test, Performance Test, E2E Test.
- [ ] 84. **Thiết lập cấu trúc thư mục Test Artifacts**:
  - `docs/test-plan/00_Dashboard/` — Tổng hợp kết quả, biểu đồ coverage.
  - `docs/test-plan/01_Test_Strategy/` — Chiến lược, phạm vi, công cụ, tiêu chí đạt/rớt.
  - `docs/test-plan/02_API_Backend_Test/` — Ma trận Test Cases chi tiết cho từng Microservice.
  - `docs/test-plan/03_UI_Frontend_Test/` — Test Cases giao diện và trải nghiệm người dùng.
  - `docs/test-plan/04_Performance_Load_Test/` — Kịch bản đo lường hiệu năng, chịu tải, Stress Test.
  - `docs/test-plan/05_E2E_Integration_Test/` — Test Cases luồng nghiệp vụ liên dịch vụ (Cross-service).
  - `docs/test-plan/06_Test_Data_Matrix/` — Quản lý dữ liệu test, token, tài khoản, cấu hình môi trường.
- [ ] 85. **Tích hợp Code Coverage Report tự động**:
  - Cấu hình `coverlet.collector` cho tất cả 5 dự án `.Tests`.
  - Viết script `scripts/run-tests-with-coverage.sh` thu thập coverage Cobertura/LCOV sau mỗi lần chạy `dotnet test`.
  - Tích hợp `ReportGenerator` để sinh báo cáo HTML trực quan (`docs/test-plan/00_Dashboard/coverage-report/index.html`).
  - Thiết lập ngưỡng Code Coverage tối thiểu: >= 85% cho Application layer, >= 90% cho Domain layer.

---

### Phase 12.1: Chiến lược & Phạm vi Kiểm thử (Test Strategy & Scope Definition)

- [ ] 86. **Định nghĩa phạm vi kiểm thử (Test Scope)**:
  - **Trong phạm vi (In Scope)**: 5 Microservices .NET 9 (IdentityService, OperationsService, AIInspectionService, InspectionEvaluationService, NotificationService), ApiGateway Ocelot, RabbitMQ Event Bus, gRPC internal, SignalR Hub, PostgreSQL + PostGIS, Redis Cache, Frontend Web Application.
  - **Ngoài phạm vi (Out of Scope)**: FastAPI Python AI Service (kiểm thử riêng bởi nhóm AI/ML), Mobile Application (nếu có), hạ tầng DevOps/Kubernetes.
- [ ] 87. **Phân loại các cấp độ kiểm thử (Test Levels)**:
  - **Level 1 — Unit Tests**: Kiểm tra logic nghiệp vụ cô lập (Command Handlers, Query Handlers, Domain Entities, Pure Utilities, Validators). Mock tất cả dependencies bên ngoài (Repository, EventPublisher, FileStorage, Redis, HttpClient).
  - **Level 2 — Component / Integration Tests**: Kiểm tra tương tác giữa các lớp trong cùng một Microservice bằng `WebApplicationFactory<Program>` với In-Memory Database hoặc Testcontainers PostgreSQL. Kiểm tra HTTP Status Codes, Request/Response Contract, FluentValidation pipeline, Global Exception Handler (`ProblemDetails` RFC 7807).
  - **Level 3 — API Contract Tests**: Kiểm tra hợp đồng API đầu vào/đầu ra bằng Postman Collection + Newman CLI. Đảm bảo Response JSON schema khớp với đặc tả trong `api_spec.md`.
  - **Level 4 — End-to-End Integration Tests**: Kiểm tra luồng nghiệp vụ liên dịch vụ xuyên suốt (Cross-service Workflows) qua API Gateway, RabbitMQ, gRPC.
  - **Level 5 — Performance & Load Tests**: Đo lường thời gian phản hồi (Response Time), thông lượng (Throughput), khả năng chịu tải (Concurrency), và ngưỡng chịu đựng (Stress Breaking Point) bằng JMeter hoặc k6.
  - **Level 6 — UI/UX Tests**: Kiểm tra giao diện, luồng tương tác người dùng, Responsive Layout, Cross-browser Compatibility bằng Playwright.
- [ ] 88. **Định nghĩa tiêu chí Đạt / Không Đạt (Entry & Exit Criteria)**:
  - **Entry Criteria**: Code đã được build thành công (`dotnet build` không lỗi), Database Migration đã chạy xong, Docker Compose các services đều healthy.
  - **Exit Criteria (Pass)**: >= 95% Test Cases ở trạng thái `Passed`, 0 lỗi `Critical` / `Blocker` còn tồn đọng, Code Coverage >= 85% trên Application layer.
  - **Exit Criteria (Fail)**: Bất kỳ Test Case `Critical` nào Failed mà chưa được fix, hoặc Code Coverage < 70%.
- [ ] 89. **Ma trận phân quyền kiểm thử RBAC (Authorization Test Matrix)**:
  - Lập bảng ma trận 5 Vai trò (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`) x Tất cả Endpoints trong `api_spec.md`.
  - Mỗi ô xác định kết quả kỳ vọng: `200 OK`, `201 Created`, `204 No Content`, `401 Unauthorized`, `403 Forbidden`.
  - Bao gồm cả trường hợp Anonymous (không token) và Token hết hạn.

---

### Phase 12.2: Kiểm thử API Backend chi tiết theo từng Microservice (API Backend Test Cases)

#### Phase 12.2.1: IdentityService — Xác thực, Phân quyền & Quản trị Người dùng

- [ ] 90. **Test Suite: `LoginCommand` (`POST /api/v1/auth/login`)**:
  - Đăng nhập thành công với Email hợp lệ + Mật khẩu đúng → HTTP 200, trả về `accessToken`, `refreshToken`, `user` object chứa `roles`.
  - Đăng nhập thất bại: Email sai, Mật khẩu sai, Email rỗng/null, Mật khẩu rỗng/null → HTTP 400.
  - Đăng nhập với User bị đình chỉ (`Status == Suspended`) → HTTP 400, thông báo tài khoản bị khóa.
  - Đăng nhập với User chưa kích hoạt (`Status == Pending` / `Inactive`) → HTTP 400.
  - **[Security]** Chống Timing Attack: Thời gian phản hồi khi Email không tồn tại phải xấp xỉ bằng khi Email tồn tại (chênh lệch < 50ms trung bình trên 100 request).
  - Kiểm tra JWT Token chứa đầy đủ Claims: `UserId`, `Email`, `Roles`, `exp`, `iss`, `aud`.
- [ ] 91. **Test Suite: `RefreshTokenCommand` (`POST /api/v1/auth/refresh-token`)**:
  - Refresh thành công với cặp `accessToken` + `refreshToken` hợp lệ → Cấp cặp token mới, Refresh Token cũ bị thu hồi (`RevokedAt != null`).
  - Refresh thất bại: Refresh Token hết hạn (`ExpiresAt < now`) → HTTP 401.
  - Refresh thất bại: Refresh Token đã bị thu hồi trước đó → HTTP 401.
  - **[Security] Refresh Token Reuse Detection (Token Theft)**: Khi gửi Refresh Token đã bị thu hồi → Hệ thống tự động thu hồi CASCADE tất cả Refresh Token active của User đó (chặn toàn bộ phiên đăng nhập trên mọi thiết bị).
  - Kiểm tra bản ghi mới trong bảng `RefreshTokens` có `DeviceInfo` đúng.
- [ ] 92. **Test Suite: OTP Workflows (`POST /api/v1/auth/otp/send`, `POST /api/v1/auth/otp/verify`)**:
  - Gửi OTP thành công → Lưu OTP hash vào Redis với TTL, trả về HTTP 200.
  - Gửi OTP khi chưa hết Cooldown → HTTP 429 Too Many Requests hoặc HTTP 400 với thông báo chờ.
  - Verify OTP đúng mã → Trả về kết quả tương ứng theo Strategy (`LoginOtpStrategy` → token, `ForgotPasswordOtpStrategy` → step-up token, `StepUpOtpStrategy` → xác nhận nâng cấp quyền).
  - Verify OTP sai mã → Tăng bộ đếm lần thử sai, HTTP 400.
  - Verify OTP nhập sai quá giới hạn cho phép (ví dụ 5 lần) → Lockout tạm thời, HTTP 400.
  - Verify OTP hết hạn (TTL Redis đã xóa) → HTTP 400.
  - **[Security]** Step-Up Token Ownership: So sánh `UserId` trong Step-Up token với `UserId` trong `HttpContext.User` → Từ chối nếu không trùng khớp (HTTP 403).
- [ ] 93. **Test Suite: `ResetPasswordCommand` (`POST /api/v1/auth/reset-password`)**:
  - Reset mật khẩu thành công với Step-Up Token hợp lệ → Cập nhật `PasswordHash` mới trong DB, thu hồi tất cả Refresh Token cũ.
  - Reset thất bại: Step-Up Token không hợp lệ hoặc hết hạn → HTTP 401.
  - Reset thất bại: Mật khẩu mới không đạt yêu cầu validation (quá ngắn, thiếu ký tự đặc biệt...) → HTTP 400 với `ProblemDetails`.
- [ ] 94. **Test Suite: User Management (`CRUD /api/v1/users`)**:
  - `POST /users` — Tạo User mới thành công (SystemAdmin) → HTTP 201, tự động gán Role, PasswordHash được mã hóa BCrypt.
  - `POST /users` — Tạo User với Email đã tồn tại → HTTP 400 (trùng lặp Email).
  - `POST /users` — Tạo User với Role không tồn tại → HTTP 400.
  - `GET /users` — Phân trang danh sách User (kiểm tra `pageIndex`, `pageSize`, `totalCount`).
  - `GET /users/{id}` — Trả về thông tin chi tiết User kèm Roles.
  - `GET /users/{id}` — ID không tồn tại → HTTP 404.
  - `PUT /users/{id}` — Cập nhật thông tin User thành công (FullName, Phone, Status, Roles).
  - `PUT /users/{id}/suspend` — Đình chỉ tài khoản → Chuyển Enum `UserStatus` sang `Suspended`, vô hiệu hóa tất cả Refresh Token active.
  - `GET /users/assignable` — Chỉ trả về User có Role `Inspector` và `Status == Active`, không chứa thông tin nhạy cảm (PasswordHash).
  - **[RBAC]** Mỗi endpoint chỉ cho phép Role `SystemAdmin` truy cập; Inspector/Analyst/Technician gọi → HTTP 403.
- [ ] 95. **Test Suite: `GetMyProfileQuery` (`GET /api/v1/auth/me`)**:
  - Trả về thông tin User hiện tại từ JWT Token → HTTP 200 với `id`, `email`, `fullName`, `roles`.
  - Token hết hạn hoặc không hợp lệ → HTTP 401.
- [ ] 96. **Test Suite: `GetAuditLogsQuery` (`GET /api/v1/audit-logs`)**:
  - Trả về danh sách lịch sử thay đổi phân trang → HTTP 200.
  - Kiểm tra nội dung AuditLog: `table_name`, `record_id`, `action_type` (Added/Modified/Deleted), `old_values`, `new_values`, `ip_address`, `user_agent`.
  - **[RBAC]** Chỉ `SystemAdmin` và `Manager` được phép truy cập; Inspector/Analyst/Technician → HTTP 403.
- [ ] 97. **Test Suite: Infrastructure Services (BCrypt, JWT, FileStorage, Pure Utilities)**:
  - `BCryptPasswordHasher`: Hash trả về chuỗi khác plain text, Verify đúng/sai mật khẩu, Verify với hash null/rỗng/bất hợp lệ.
  - `JwtProvider`: Sinh token chứa đủ Claims (UserId, Email, Roles, Exp), Token hết hạn không giải mã được.
  - `LocalFileStorageService`: Lưu file thành công trả về URL, từ chối extension không an toàn (`.php`, `.exe`, `.sh`), sanitize ký tự đặc biệt trong tên file (`#`, `&`, `=`), chống Path Traversal (`../../etc/passwd`).
  - `TokenHasher`: SHA256 hash deterministic cho cùng input.
  - `RedisKeyBuilder`: Format key đúng pattern namespace.
  - `OtpCalculations`: Tính toán cooldown và lockout threshold chính xác.

#### Phase 12.2.2: OperationsService — Tài sản Lưới điện, GIS & Chuyến bay

- [ ] 98. **Test Suite: CRUD Regions (`/api/v1/regions`)**:
  - Tạo Region mới thành công → HTTP 201, trả về `id`, `regionName`.
  - Lấy danh sách Regions phân trang → HTTP 200, kiểm tra `totalCount`, `items`.
  - Lấy Region theo ID → HTTP 200. ID không tồn tại → HTTP 404.
  - Cập nhật Region → HTTP 200/204. Region không tồn tại hoặc đã xóa mềm → HTTP 404.
  - Xóa mềm Region (Soft Delete) → `IsDeleted = true`, `DeletedAt != null`. Global Query Filter tự động ẩn khỏi kết quả truy vấn.
- [ ] 99. **Test Suite: CRUD Substations (`/api/v1/substations`)**:
  - Tạo Substation liên kết với Region tồn tại → HTTP 201.
  - Tạo Substation với `RegionAssetId` không tồn tại → HTTP 404.
  - Cập nhật Substation: đổi Region cha, đổi tên, đổi `voltage_level`.
  - Xóa mềm Substation.
  - Lấy danh sách Substations phân trang, lọc theo `RegionAssetId`.
- [ ] 100. **Test Suite: CRUD TransmissionLines (`/api/v1/lines`)**:
  - Tạo TransmissionLine liên kết Substation → HTTP 201.
  - Tạo với Substation không tồn tại → HTTP 404.
  - Cập nhật: đổi tên, đổi cờ `is_critical_edge`.
  - Xóa mềm TransmissionLine.
  - Lấy danh sách phân trang.
- [ ] 101. **Test Suite: Towers & PostGIS (`/api/v1/towers`)**:
  - `POST /towers` — Tạo Tower với toạ độ `latitude`, `longitude` hợp lệ → Tự động chuyển đổi thành PostGIS `Point(lng, lat)` SRID 4326, lưu vào cột `geom`.
  - Tạo Tower với `LineAssetId` không tồn tại → HTTP 404.
  - Tạo Tower với `towerCode` đã tồn tại → HTTP 400 (Unique Constraint Violation).
  - **[GIS Boundary]** Tạo Tower với Latitude ngoài khoảng `[-90, 90]` hoặc Longitude ngoài `[-180, 180]` → Kiểm tra validation.
  - `POST /towers/import` — Import file Excel hợp lệ (45 cột điện) → Trả về `importedCount: 45`, tự động tạo Assets mặc định (`createdAssetsCount`).
  - `POST /towers/import` — Import file Excel rỗng (0 dòng dữ liệu) → Xử lý graceful, trả về `importedCount: 0`.
  - `POST /towers/import` — Import file sai format (không phải Excel, cột thiếu) → HTTP 400.
  - `POST /towers/import` — Import file chứa toạ độ GPS rác / giá trị null → Bỏ qua dòng lỗi, tiếp tục import các dòng hợp lệ.
  - `GET /towers/in-bbox?minLat=...&minLng=...&maxLat=...&maxLng=...` — Trả về chỉ các Tower nằm trong vùng Bounding Box.
  - `GET /towers/in-bbox` với Bounding Box rỗng (không chứa Tower nào) → Trả về mảng rỗng.
  - `GET /towers/in-bbox` với Bounding Box bao trọn toàn bộ → Trả về tất cả Towers.
  - Xóa mềm Tower, lấy Tower theo ID.
- [ ] 102. **Test Suite: Assets (`/api/v1/assets`)**:
  - Tạo Asset gắn với Tower → HTTP 201.
  - Tạo Asset với `TowerId` không tồn tại → HTTP 404.
  - Tạo Asset với `asset_code` đã tồn tại → HTTP 400 (Unique).
  - `GET /assets/{id}` — Trả về chi tiết Asset kèm `currentHealthScore`, `riskLevel`, danh sách `activeAnomalies` (Eager Loading).
  - Lấy danh sách Assets phân trang.
- [ ] 103. **Test Suite: Missions (`/api/v1/missions`)**:
  - `POST /missions` — Tạo chuyến bay mới, gán Inspector, chỉ định UAV → HTTP 201, tự động phát sự kiện `MissionCreatedEvent` qua RabbitMQ.
  - Tạo Mission với `inspectorId` không tồn tại hoặc không phải Inspector → HTTP 400.
  - Tạo Mission với `missionCode` trùng → HTTP 400 (Unique).
  - `PUT /missions/{id}/status` — Chuyển trạng thái `Pending → InProgress → Completed` → HTTP 204.
  - **[State Transition]** Chuyển trạng thái không hợp lệ: `Completed → Pending`, `Cancelled → InProgress` → HTTP 400 với thông báo trạng thái không hợp lệ.
  - `GET /missions/my` — Inspector truy xuất danh sách chuyến bay được phân công cho chính mình → Chỉ trả về missions có `inspectorId == currentUserId`.
  - **[RBAC]** `POST /missions` chỉ cho phép `Manager`; Inspector gọi → HTTP 403.
- [ ] 104. **Test Suite: Upload Inspection Media (`POST /missions/{id}/media`)**:
  - Tải lên ảnh hợp lệ (`.jpg`, `.png`) → Lưu file qua `IFileStorageService`, tạo bản ghi `InspectionMedia`, phát sự kiện `MediaUploadedEvent` → HTTP 200.
  - Tải lên video hợp lệ (`.mp4`, `.webm`) → Phân loại `MediaType = Video`.
  - Tải lên file không hợp lệ (`.php`, `.exe`, `.bat`) → Từ chối qua `FileSanitizer` → HTTP 400.
  - Tải lên với `MissionId` không tồn tại → HTTP 404.
  - Tải lên với `AssetId` không tồn tại (nếu cung cấp) → HTTP 404.
  - **[Security]** Tên file chứa Path Traversal (`../../etc/passwd.jpg`) → Sanitize thành tên an toàn.
  - **[Security]** Tên file chứa ký tự đặc biệt (`test#1&2=3.jpg`) → Sanitize thành tên URL-safe.
- [ ] 105. **Test Suite: Upload Flight Log (`POST /missions/{id}/flight-log`)**:
  - Tải lên nhật ký bay hợp lệ (GPS track JSONB, `minBatteryRecorded`, `maxAltitudeM`, `flightDurationSeconds`, `connectionStatus`) → HTTP 200.
  - Tải lên với `MissionId` không tồn tại → HTTP 404.
  - Tải lên với dữ liệu GPS track rỗng hoặc malformed JSON → HTTP 400.
- [ ] 106. **Test Suite: Monitor & Dashboard Endpoints (`/api/v1/monitor`)**:
  - `GET /monitor/summary` — Trả về tổng hợp thống kê hệ thống.
  - `GET /monitor/recent-defects?pageSize=10` — Trả về lỗi gần đây phân trang.
  - `GET /monitor/defects-statistics` — Trả về thống kê sự cố.
  - `GET /monitor/mission-status` — Trả về trạng thái chuyến bay.
  - `GET /monitor/inspections?pageSize=20` — Trả về danh sách kiểm tra.
  - `GET /monitor/alerts` — Trả về danh sách cảnh báo.
  - **[Security]** `pageSize > 100` → Trả về HTTP 400 (Pagination Upper-Bound Validation chống DoS).
  - **[RBAC]** Mỗi endpoint áp dụng `[Authorize]` — Anonymous → HTTP 401, Role không phù hợp → HTTP 403.
- [ ] 107. **Test Suite: EF Core Audit Interceptor**:
  - Khi thêm mới entity (`Added`) → Tự động sinh bản ghi `AuditLog` với `action_type = Added`, `new_values` chứa JSON các trường mới.
  - Khi cập nhật entity (`Modified`) → `AuditLog` ghi nhận `old_values` và `new_values` khác nhau.
  - Khi xóa mềm entity (`Modified IsDeleted = true`) → `AuditLog` ghi nhận thay đổi `IsDeleted`.
  - **[GIS]** Khi entity có trường `geom` (Geometry) → Serialize thành WKT (Well-Known Text) thay vì binary trong `AuditLog`.
  - Kiểm tra `ip_address` và `user_agent` được ghi nhận từ `HttpContext`.

#### Phase 12.2.3: AIInspectionService — Tích hợp AI, Phân tích Ảnh & Thẩm định Lỗi

- [ ] 108. **Test Suite: `AnalyzeMissionMediaCommand` (`POST /api/v1/missions/{missionId}/ai-analysis`)**:
  - Tải lên lô ảnh/video hỗn hợp (2 ảnh + 1 video) → Phân loại đúng `MediaType`, lưu `InspectionMedia`, tạo `AIAnalysisRequest` với `Status = Pending`, phát sự kiện `AIAnalysisRequestedEvent` cho mỗi file.
  - Tải lên lô chứa file không hỗ trợ (`.txt`, `.docx`) xen lẫn file hợp lệ (`.jpg`) → File hợp lệ được xử lý bình thường, file không hỗ trợ bị reject. Response chứa `acceptedFiles`, `rejectedFiles`.
  - Tải lên lô có 1 file bị lỗi lưu trữ (IOException) → File lỗi bị skip, các file còn lại tiếp tục xử lý.
  - Mission không tồn tại → HTTP 404 (`KeyNotFoundException`).
  - Kiểm tra `BatchId` duy nhất được sinh cho mỗi lô.
  - Kiểm tra sự kiện `AIAnalysisStatusChangedEvent` được phát cho mỗi file thành công.
- [ ] 109. **Test Suite: `ProcessAiAnalysisResultCommandHandler` (AI Callback Consumer)**:
  - Nhận kết quả AI phân tích thành công (`Status = Completed`) chứa danh sách detections (bounding boxes, confidence scores, category codes) → Lưu `DetectedAnomaly` với `ValidationStatus = Pending`.
  - Nhận kết quả với category có `is_emergency_class = true` (VD: `FIRE_01`, `CABLE_BREAK`) → Tự động tạo `EmergencyAlert` với `Priority = Critical`, phát sự kiện `DefectDetectedEvent`.
  - Nhận kết quả với `Status = Failed` → Cập nhật `AIAnalysisRequest.Status = Failed`, không tạo anomaly.
  - Nhận kết quả với danh sách detections rỗng → Cập nhật status, không tạo anomaly.
- [ ] 110. **Test Suite: `GetMissionAiDetectionsQuery` (`GET /api/v1/anomalies/pending`)**:
  - Trả về danh sách lỗi AI chờ duyệt phân trang → HTTP 200 với `totalCount`, `items[]` chứa `mediaUrl`, `assetCode`, `categoryName`, `boundingBox`, `confidenceScore`, `validationStatus`.
  - Phân trang: `pageIndex=0, pageSize=10` → Trả về đúng 10 items (nếu đủ).
  - Không có lỗi nào ở trạng thái `Pending` → Trả về `totalCount: 0`, `items: []`.
  - **[RBAC]** Chỉ `Analyst` được phép truy cập; Inspector → HTTP 403.
- [ ] 111. **Test Suite: `ReviewMissionAiDetectionCommand` (`PUT /api/v1/anomalies/{id}/validate`)**:
  - Analyst xác nhận lỗi (`Confirmed`) → Cập nhật `ValidationStatus = Confirmed`, lưu `AnalystId`, `ValidatedAt`, `analyst_notes`. Phát sự kiện `AnomalyValidatedEvent`.
  - Analyst bác bỏ lỗi (`Rejected`) → Cập nhật `ValidationStatus = Rejected`, lưu `analyst_notes`.
  - Duyệt anomaly không tồn tại → HTTP 404.
  - Duyệt anomaly đã được duyệt trước đó (`Confirmed` / `Rejected`) → HTTP 400 (đã xử lý rồi).
  - **[RBAC]** Chỉ `Analyst` được phép duyệt; Inspector/Technician → HTTP 403.
- [ ] 112. **Test Suite: `BoundingBoxCalculations` (Pure Utility)**:
  - Tính IoU (Intersection over Union) giữa 2 khung hoàn toàn trùng khớp → IoU = 1.0.
  - Tính IoU giữa 2 khung hoàn toàn không giao nhau → IoU = 0.0.
  - Tính IoU giữa 2 khung giao nhau một phần → Giá trị trong khoảng (0, 1).
  - Tính diện tích khung (Area) → Giá trị dương.
  - Tính tỉ lệ khung hình (Aspect Ratio) → width / height.
  - Trường hợp khung có kích thước 0 (width hoặc height = 0) → Xử lý không ném ngoại lệ.
- [ ] 113. **Test Suite: Polly Resilience (HTTP Client kết nối Python AI)**:
  - Python AI trả về HTTP 500 → Polly tự động Retry 3 lần với Exponential Backoff.
  - Python AI không phản hồi (Timeout) → Polly Retry và cuối cùng ném `TimeoutRejectedException`.
  - Python AI liên tục lỗi → Circuit Breaker chuyển sang trạng thái `Open`, các request tiếp theo bị chặn ngay mà không gọi thực tế.
  - Circuit Breaker Half-Open → Thử lại 1 request, nếu thành công → chuyển về `Closed`.

#### Phase 12.2.4: InspectionEvaluationService — Engine Đánh giá Mức độ Nghiêm trọng (gRPC)

- [ ] 114. **Test Suite: `EvaluateDetection` (gRPC Service)**:
  - Lỗi khẩn cấp (`IsEmergencyClass = true`) + Confidence cao (0.95) → `Severity = Critical`, `RiskLevel = ImmediateAction`, `PriorityScore >= 90`, `RequiresImmediateAlert = true`.
  - Lỗi không khẩn cấp + Confidence thấp (0.30) → `Severity = Low`, `RiskLevel = Monitor`, `RequiresImmediateAlert = false`.
  - Lỗi không khẩn cấp + Confidence trung bình (0.65) → `Severity = Medium`, `RiskLevel = ScheduleMaintenance`.
  - Lỗi khẩn cấp + Confidence thấp (0.15) → Vẫn ưu tiên cao hơn lỗi thường do tính chất khẩn cấp.
  - **[BVA Boundary]** Confidence = 0.0 → Xử lý không ném ngoại lệ, trả về `Severity = Low`.
  - **[BVA Boundary]** Confidence = 1.0 → Trả về `PriorityScore` tối đa.
  - **[BVA Boundary]** Confidence < 0 hoặc > 1 → Xử lý validation hoặc clamp giá trị.
  - Ma trận 5 danh mục lỗi (`Corrosion`, `Surface Crack`, `Vegetation Encroachment`, `Missing Components`, `Insulator Damage`) x 3 mức Confidence (Low/Medium/High) → 15 test cases kiểm tra `Severity`, `RiskLevel`, `PriorityScore` chính xác.
- [ ] 115. **Test Suite: `DetectionEvaluationEngine` (Pure Utility Engine)**:
  - Kiểm tra tính toán `PriorityScore` dựa trên công thức trọng số (Confidence * SeverityWeight * EmergencyMultiplier).
  - Kiểm tra phân loại `EvaluationSeverity` Enum: `Low`, `Medium`, `High`, `Critical`.
  - Kiểm tra phân loại `EvaluationRiskLevel` Enum: `Monitor`, `ScheduleMaintenance`, `PrioritizeMaintenance`, `ImmediateAction`.
  - Cấu hình `EvaluationThresholdOptions` thay đổi (ngưỡng tùy chỉnh) → Kết quả tính toán thay đổi tương ứng.

#### Phase 12.2.5: NotificationService — Thông báo In-app, Email & Realtime

- [ ] 116. **Test Suite: Notification CRUD (`/api/v1/notifications`)**:
  - `CreateNotificationCommand` → Tạo thông báo mới với `NotificationType` Enum hợp lệ, lưu `title`, `body`, `reference_type`, `reference_id`, `sent_at`. `IsRead = false` mặc định.
  - `GetNotificationsQuery` → Trả về danh sách thông báo của User hiện tại phân trang, sắp xếp theo `sent_at` giảm dần.
  - `GetNotificationByIdQuery` → Trả về chi tiết 1 thông báo. ID không tồn tại → HTTP 404.
  - `MarkNotificationAsReadCommand` → Cập nhật `IsRead = true`, `ReadAt = DateTime.UtcNow`. Notification không tồn tại → HTTP 404.
  - `DeleteNotificationCommand` → Xóa thông báo. Notification không tồn tại → HTTP 404.
- [ ] 117. **Test Suite: Event-Driven Notification (RabbitMQ Consumers)**:
  - `MissionCreatedConsumer`: Khi nhận sự kiện `MissionCreatedEvent` → Tự động tạo Notification cho Inspector được phân công với `type = MissionAssigned`.
  - `DefectDetectedConsumer`: Khi nhận sự kiện `DefectDetectedEvent` (lỗi khẩn cấp) → Tự động tạo Notification cho tất cả Analyst với `type = EmergencyAlert`.
  - Kiểm tra nội dung thông báo được format đúng qua `NotificationFormatter` (thay thế template variables: `{missionCode}`, `{assetCode}`, `{categoryName}`...).
- [ ] 118. **Test Suite: SignalR Hub (`/hubs/notifications`)**:
  - Client kết nối SignalR Hub thành công với JWT Token hợp lệ.
  - Client kết nối không có Token → Bị từ chối.
  - Khi `EmergencyAlert` được tạo → SignalR đẩy sự kiện realtime tới tất cả Analyst đang online.
  - Khi Mission mới được tạo → SignalR đẩy thông báo tới Inspector được phân công.
- [ ] 119. **Test Suite: Email Service (`IEmailService`)**:
  - Gửi email OTP qua SendGrid/SMTP thành công → Kiểm tra template email được render đúng.
  - Gửi email thất bại (SMTP lỗi) → Xử lý graceful, log lỗi, không crash ứng dụng.

#### Phase 12.2.6: ApiGateway & Cross-Cutting Concerns

- [ ] 120. **Test Suite: Ocelot API Gateway Routing**:
  - Request tới `/api/v1/auth/login` → Route đúng tới `IdentityService`.
  - Request tới `/api/v1/regions` → Route đúng tới `OperationsService`.
  - Request tới `/api/v1/missions/{id}/ai-analysis` → Route đúng tới `AIInspectionService` (ưu tiên trước route `/missions` chung).
  - Request tới `/api/v1/notifications` → Route đúng tới `NotificationService`.
  - Request tới endpoint không tồn tại → HTTP 404.
  - Health check: `GET /health` → HTTP 200.
- [ ] 121. **Test Suite: Global Exception Handler (`ProblemDetails` RFC 7807)**:
  - `ValidationException` (FluentValidation) → HTTP 400 với `ProblemDetails` chứa `errors` dictionary chi tiết các trường bị lỗi.
  - `KeyNotFoundException` / `NotFoundException` → HTTP 404 với `ProblemDetails`.
  - `UnauthorizedAccessException` → HTTP 401 với `ProblemDetails`.
  - `BusinessRuleException` → HTTP 400 với `ProblemDetails` chứa message nghiệp vụ.
  - Unhandled Exception (NullReferenceException, DbException...) → HTTP 500 với `ProblemDetails` tổng quát, không leak stack trace ra response.
- [ ] 122. **Test Suite: FluentValidation Pipeline (MediatR `ValidationBehavior`)**:
  - Command với tất cả trường hợp lệ → Đi qua validation, xử lý Handler bình thường.
  - Command với trường required bị thiếu → `ValidationException` với message chi tiết.
  - Command với trường vượt max length → `ValidationException`.
  - Command với giá trị enum không hợp lệ → `ValidationException`.
  - Nhiều trường cùng lỗi → `ValidationException` chứa tất cả lỗi (không dừng ở lỗi đầu tiên).

---

### Phase 12.3: Kiểm thử Giao diện Người dùng Frontend (UI Frontend Test Cases)

- [ ] 123. **Test Suite: Trang Đăng nhập (Login Page)**:
  - Hiển thị form đăng nhập đầy đủ: input Email, input Password, nút Login.
  - Đăng nhập thành công → Chuyển hướng tới Dashboard.
  - Đăng nhập thất bại → Hiển thị thông báo lỗi (không hiển thị thông tin kỹ thuật).
  - Validation client-side: Email rỗng, Password rỗng → Hiển thị thông báo yêu cầu nhập.
  - Hiển thị/Ẩn mật khẩu (toggle visibility).
- [ ] 124. **Test Suite: Dashboard & Bản đồ GIS**:
  - Dashboard hiển thị đúng số liệu thống kê tổng hợp (tổng missions, tổng anomalies, tổng alerts...).
  - Bản đồ LeafletJS render Marker Cluster cột điện đúng vị trí GPS.
  - Zoom in/out bản đồ → Gọi API `GetAssetsInBoundingBoxQuery` với viewport mới → Cập nhật markers.
  - Click vào cột điện → Hiển thị popup chi tiết (Tower Code, Assets, Health Score).
  - Heatmap / Marker Cluster lỗi AI hiển thị đúng từ dữ liệu GeoJSON.
- [ ] 125. **Test Suite: Quản lý Chuyến bay (Mission Management)**:
  - Manager tạo chuyến bay mới → Form hiển thị danh sách Inspector khả dụng (từ API `/users/assignable`), chọn UAV, chọn tuyến dây.
  - Danh sách chuyến bay hiển thị phân trang, lọc theo trạng thái.
  - Inspector xem "Chuyến bay của tôi" → Chỉ hiển thị missions được phân công.
  - Chuyển trạng thái chuyến bay → UI cập nhật badge trạng thái tương ứng.
- [ ] 126. **Test Suite: Upload ảnh Kiểm tra & Phân tích AI**:
  - Kéo thả / Chọn file ảnh → Preview ảnh trước khi upload.
  - Upload thành công → Hiển thị trạng thái "Đang phân tích AI".
  - Upload file không hợp lệ → Hiển thị thông báo lỗi phía client.
  - Kết quả AI trả về → Hiển thị ảnh gốc với bounding box overlay, confidence score, category label.
- [ ] 127. **Test Suite: Duyệt lỗi AI (Analyst HITL Review)**:
  - Hiển thị danh sách lỗi chờ duyệt phân trang.
  - Analyst click Confirm / Reject → Gửi request API → Cập nhật trạng thái trên UI.
  - Analyst nhập ghi chú (`analyst_notes`) → Hiển thị trong lịch sử duyệt.
  - Cảnh báo khẩn cấp → Hiển thị popup / sound alert realtime qua SignalR.
- [ ] 128. **Test Suite: Phiếu Bảo trì (Maintenance Tickets)**:
  - Manager tạo phiếu bảo trì → Form gán Technician, thiết lập Priority, Due Date.
  - Technician xem phiếu được giao → Danh sách phiếu bảo trì của mình.
  - Technician upload ảnh minh chứng sửa chữa → Preview và submit.
  - Technician khai báo vật tư sử dụng → Form nhập component name, code, quantity.
  - Manager nghiệm thu → Approve/Reject đóng phiếu.
- [ ] 129. **Test Suite: Responsive Layout & Cross-Browser**:
  - Kiểm tra giao diện trên các kích thước màn hình: Desktop (1920x1080), Tablet (768x1024), Mobile (375x667).
  - Menu navigation responsive (Hamburger menu trên mobile).
  - Bảng dữ liệu (data tables) responsive trên mobile → Scroll ngang hoặc card layout.
  - Kiểm tra trên trình duyệt: Chrome, Firefox, Safari (WebKit), Edge.
- [ ] 130. **Test Suite: Accessibility & UX**:
  - Tất cả các form input có label đi kèm (`<label for="...">`).
  - Nút bấm có kích thước đủ lớn cho thao tác touch (minimum 44x44px).
  - Loading state hiển thị skeleton / spinner khi đang chờ API phản hồi.
  - Error state hiển thị rõ ràng khi API trả lỗi.
  - Toast notification hiển thị khi thao tác thành công (tạo, cập nhật, xóa).

---

### Phase 12.4: Kiểm thử Hiệu năng & Chịu tải (Performance & Load Test)

- [ ] 131. **Thiết lập môi trường Performance Test**:
  - Cấu hình JMeter Test Plan hoặc k6 script cho từng nhóm API.
  - Chuẩn bị dữ liệu seed: >= 1.000 Users, >= 10.000 Towers, >= 50.000 Assets, >= 100.000 DetectedAnomalies.
  - Xác định phần cứng máy chủ test (CPU, RAM, Disk I/O baseline).
- [ ] 132. **Kịch bản Đo lường Hiệu năng Baseline (Performance Benchmark)**:
  - `POST /auth/login` — Response Time P95 < 500ms.
  - `GET /towers/in-bbox` (khu vực chứa 500 cột điện) — Response Time P95 < 1000ms.
  - `GET /anomalies/pending?pageSize=50` — Response Time P95 < 800ms.
  - `GET /assets/{id}` (Eager Loading anomalies) — Response Time P95 < 500ms.
  - `POST /missions/{id}/media` (upload ảnh 5MB) — Response Time P95 < 3000ms.
  - `EvaluateDetection` gRPC — Response Time P95 < 100ms.
- [ ] 133. **Kịch bản Kiểm thử Tải đồng thời (Concurrency Load Test)**:
  - 100 concurrent users đăng nhập đồng thời → Tất cả nhận JWT thành công, không bị deadlock database.
  - 50 concurrent users query `GET /towers/in-bbox` → PostGIS GiST Index xử lý tốt, không timeout.
  - 20 concurrent Inspectors upload ảnh đồng thời → File storage không conflict, message queue không mất event.
  - 200 concurrent users query `GET /monitor/summary` → API Gateway route phân tán tải tốt.
- [ ] 134. **Kịch bản Stress Test (Ngưỡng chịu đựng)**:
  - Tăng dần số lượng concurrent users: 100 → 200 → 500 → 1000 → Xác định điểm gãy (Breaking Point) khi Response Time P95 > 5000ms hoặc Error Rate > 5%.
  - Stress Test RabbitMQ: Publish 1000 events/phút → Consumer xử lý không bị accumulate message backlog.
  - Stress Test SignalR: 100 WebSocket connections đồng thời → Hub broadcast không bị drop message.
- [ ] 135. **Kịch bản Soak Test (Kiểm thử bền vững)**:
  - Chạy 50 concurrent users liên tục 2 giờ → Giám sát Memory Leak, Database Connection Pool exhaustion, Thread Pool starvation.
  - Kiểm tra GC (Garbage Collection) pressure không tăng đột biến theo thời gian.
- [ ] 136. **Kịch bản Spike Test (Kiểm thử đột biến)**:
  - Từ 10 users → đột ngột tăng lên 500 users trong 10 giây → Hệ thống xử lý mà không crash, sau đó tự hồi phục khi tải giảm.

---

### Phase 12.5: Kiểm thử Luồng nghiệp vụ liên dịch vụ End-to-End (E2E Integration Test)

- [ ] 137. **E2E Flow 1: Luồng Kiểm tra & Phát hiện Lỗi AI đầy đủ (Inspection → AI → Evaluation → Alert → Notification)**:
  1. Manager tạo chuyến bay (`POST /missions`) → `MissionCreatedEvent` được publish qua RabbitMQ.
  2. NotificationService nhận event → Tạo thông báo cho Inspector được phân công.
  3. Inspector chuyển trạng thái chuyến bay sang `InProgress` (`PUT /missions/{id}/status`).
  4. Inspector upload ảnh kiểm tra (`POST /missions/{id}/media`) → `MediaUploadedEvent` published.
  5. AIInspectionService nhận ảnh, gửi tới Python AI → Nhận callback kết quả phân tích.
  6. AIInspectionService gọi gRPC `EvaluateDetection` tới InspectionEvaluationService → Nhận `Severity`, `RiskLevel`.
  7. Nếu lỗi khẩn cấp → Tự động tạo `EmergencyAlert`, publish `DefectDetectedEvent`.
  8. NotificationService nhận `DefectDetectedEvent` → Tạo notification + đẩy SignalR realtime cho Analyst.
  9. Kiểm tra toàn bộ dữ liệu trong DB: `InspectionMedia`, `DetectedAnomaly`, `EmergencyAlert`, `Notification` đều nhất quán.
- [ ] 138. **E2E Flow 2: Luồng Thẩm định Lỗi → Bảo trì → Khôi phục Sức khỏe Asset (HITL → Maintenance → Health Recalculation)**:
  1. Analyst duyệt lỗi AI (`PUT /anomalies/{id}/validate` → `Confirmed`) → `AnomalyValidatedEvent` published.
  2. Hệ thống tính lại điểm sức khỏe Asset (`AssetHealthCalculationService`) → `current_health_score` giảm, `risk_level` tăng.
  3. Manager tạo phiếu bảo trì (`POST /maintenance/tickets`) gán cho Technician.
  4. Technician chuyển trạng thái `InProgress`, upload ảnh minh chứng, khai báo vật tư.
  5. Manager nghiệm thu đóng phiếu (`PUT /maintenance/tickets/{id}/close`) → Anomaly chuyển `Resolved`.
  6. Hệ thống tính lại điểm sức khỏe Asset → `current_health_score` tăng lại, `risk_level` giảm.
  7. Kiểm tra lịch sử `AssetHealthHistories` ghi nhận 2 bản ghi (trước bảo trì / sau bảo trì).
- [ ] 139. **E2E Flow 3: Luồng Bảo mật Phiên đăng nhập đa thiết bị (Multi-Device Session & Token Theft Detection)**:
  1. User đăng nhập trên Thiết bị A → Nhận token pair (AccessToken_A, RefreshToken_A).
  2. User đăng nhập trên Thiết bị B → Nhận token pair (AccessToken_B, RefreshToken_B). RefreshToken_A vẫn valid.
  3. User dùng RefreshToken_A để refresh → Nhận token pair mới (AccessToken_A2, RefreshToken_A2). RefreshToken_A bị thu hồi.
  4. Attacker đánh cắp RefreshToken_A (đã bị thu hồi) và gửi request refresh.
  5. Hệ thống phát hiện Token Reuse → **Thu hồi CASCADE tất cả session** (RefreshToken_A2, RefreshToken_B đều bị revoke).
  6. User trên cả 2 thiết bị bị buộc đăng nhập lại.
  7. Kiểm tra DB: Tất cả bản ghi `RefreshTokens` của User đều có `RevokedAt != null`.
- [ ] 140. **E2E Flow 4: Luồng Leo thang Cảnh báo Khẩn cấp (Emergency Alert Escalation)**:
  1. AI phát hiện lỗi khẩn cấp (`FIRE_01`) → `EmergencyAlert` được tạo tự động.
  2. SignalR đẩy cảnh báo realtime tới Analyst.
  3. Analyst xác nhận cảnh báo (`PUT /alerts/{id}/review` → `Confirmed`).
  4. Analyst leo thang cảnh báo (`POST /alerts/{id}/escalate`) → Lưu `AlertEscalation` với `reason`, gửi notification cho Manager.
  5. Manager nhận notification → Xem chi tiết cảnh báo và lịch sử escalation.
- [ ] 141. **E2E Flow 5: Luồng OTP Đặt lại Mật khẩu (Forgot Password → OTP → Reset)**:
  1. User gửi yêu cầu OTP (`POST /auth/otp/send` → `purpose: forgot_password`).
  2. Email chứa mã OTP được gửi qua SendGrid.
  3. User verify OTP đúng (`POST /auth/otp/verify`) → Nhận Step-Up Token.
  4. User gửi mật khẩu mới kèm Step-Up Token (`POST /auth/reset-password`) → Mật khẩu được cập nhật.
  5. Tất cả Refresh Token cũ bị thu hồi.
  6. User đăng nhập lại bằng mật khẩu mới → Thành công.
  7. User đăng nhập bằng mật khẩu cũ → Thất bại.

---

### Phase 12.6: Ma trận Dữ liệu Kiểm thử & Cấu hình Môi trường (Test Data Matrix & Environment Config)

- [ ] 142. **Thiết lập tài khoản Test Users cho mỗi vai trò**:
  - `admin@uavpms.test` → Role: `SystemAdmin`, Status: `Active`.
  - `manager@uavpms.test` → Role: `Manager`, Status: `Active`.
  - `inspector@uavpms.test` → Role: `Inspector`, Status: `Active`.
  - `analyst@uavpms.test` → Role: `Analyst`, Status: `Active`.
  - `technician@uavpms.test` → Role: `Technician`, Status: `Active`.
  - `suspended@uavpms.test` → Role: `Inspector`, Status: `Suspended`.
  - `pending@uavpms.test` → Role: `Inspector`, Status: `Pending`.
  - `multi-role@uavpms.test` → Roles: `Manager` + `Analyst`, Status: `Active`.
- [ ] 143. **Thiết lập JWT Tokens cho các kịch bản kiểm thử**:
  - Token hợp lệ (chưa hết hạn) cho mỗi vai trò.
  - Token đã hết hạn (`exp` < `DateTime.UtcNow`).
  - Token có `signature` bị sửa đổi (tampered token).
  - Token thiếu claim `roles` → Kiểm tra hệ thống từ chối truy cập.
  - Token chứa role không tồn tại (`FakeRole`) → HTTP 403.
  - Refresh Token hợp lệ, Refresh Token đã thu hồi, Refresh Token hết hạn.
- [ ] 144. **Thiết lập Seed Data cho các thực thể nghiệp vụ**:
  - 3 Regions mẫu (Miền Bắc, Miền Trung, Miền Nam).
  - 5 Substations mẫu phân bố trong 3 Regions.
  - 10 TransmissionLines mẫu liên kết Substations.
  - 50 Towers mẫu với toạ độ GPS thực tế khu vực Hà Nội (lat: 20.9 – 21.1, lng: 105.7 – 105.9).
  - 200 Assets mẫu gắn trên 50 Towers (4 Assets/Tower: Insulator, Cable, Cross-arm, Foundation).
  - 5 DefectCategories: `CORROSION_01`, `CRACK_01`, `VEGETATION_01`, `MISSING_01`, `INSULATOR_01`.
  - 3 Emergency DefectCategories: `FIRE_01`, `CABLE_BREAK_01`, `TOWER_COLLAPSE_01`.
- [ ] 145. **Thiết lập Seed Data cho luồng AI & Bảo trì**:
  - 10 Missions mẫu ở các trạng thái khác nhau (`Pending`, `InProgress`, `Completed`, `Cancelled`).
  - 30 InspectionMedia mẫu liên kết với Missions và Assets.
  - 20 DetectedAnomalies mẫu: 10 `Pending`, 5 `Confirmed`, 3 `Rejected`, 2 `Resolved`.
  - 5 EmergencyAlerts mẫu: 2 `Active`, 2 `Confirmed`, 1 `Dismissed`.
  - 8 MaintenanceTickets mẫu: 2 `Assigned`, 2 `InProgress`, 2 `PendingVerification`, 2 `Resolved`.
  - 10 Notifications mẫu: 5 chưa đọc (`IsRead = false`), 5 đã đọc.
- [ ] 146. **Cấu hình môi trường kiểm thử (Test Environment Config)**:
  - **Local Development**: `docker-compose.test.yml` khởi tạo PostgreSQL + PostGIS, Redis, RabbitMQ riêng biệt cho test. Seed data tự động.
  - **CI/CD Pipeline**: GitHub Actions workflow chạy `dotnet test` với Testcontainers (PostgreSQL container tạm thời). Coverage report upload artifact.
  - **Staging Environment**: Triển khai toàn bộ 5 services + Gateway lên Docker Compose staging để chạy E2E và Performance tests.
  - Biến môi trường test: `ASPNETCORE_ENVIRONMENT=Testing`, connection strings riêng, JWT secret key test riêng.
- [ ] 147. **Postman Collection & Environment**:
  - Tạo Postman Collection chứa tất cả API endpoints theo nhóm Module (Auth, Users, Regions, Towers, Missions, Anomalies, Alerts, Maintenance, Notifications).
  - Tạo Postman Environment: `{{base_url}}`, `{{access_token}}`, `{{refresh_token}}`, `{{admin_token}}`, `{{manager_token}}`, `{{inspector_token}}`, `{{analyst_token}}`, `{{technician_token}}`.
  - Thiết lập Pre-request Script tự động đăng nhập lấy token trước khi chạy test.
  - Thiết lập Test Script (Postman Tests) kiểm tra HTTP status code, response schema, response time.
  - Export Collection dạng JSON để chạy tự động bằng Newman CLI trong CI/CD.
- [ ] 148. **Tạo script khởi tạo & dọn dẹp dữ liệu test (Test Data Lifecycle)**:
  - Script SQL `seed-test-data.sql`: Insert toàn bộ dữ liệu mẫu vào PostgreSQL.
  - Script SQL `cleanup-test-data.sql`: Truncate / Delete dữ liệu test sau mỗi vòng chạy.
  - Tích hợp vào `WebApplicationFactory` (Integration Test): Tự động seed data trước mỗi test class, cleanup sau mỗi test class.
  - Redis flush script: Xóa tất cả OTP cache và session cache trong Redis test environment.