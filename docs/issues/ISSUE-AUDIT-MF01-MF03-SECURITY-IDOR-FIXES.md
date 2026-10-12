# [SECURITY-AUDIT-MF01-MF03] Báo Cáo & Khắc Phục Lỗ Hổng Bảo Mật, IDOR và Logic Nghiệp Vụ

- **Trạng thái**: ✅ Đã giải quyết (Resolved)
- **Mức độ ưu tiên**: 🔴 Nghiêm trọng (High / Critical - An ninh hệ thống, bảo vệ dữ liệu phi đội UAV và lưới điện)
- **Phân hệ**: OperationsService / AIInspectionService
- **Quy chuẩn đối chiếu**: OWASP API Security Top 10 (API1: BOLA/IDOR, API2: Broken Authentication, API5: BFLA, API8: Security Misconfiguration)

---

## 1. Danh Mục Các Lỗ Hổng Đã Khắc Phục (Resolved Vulnerabilities)

### 📌 1. [AUTH-01] Thiếu xác thực tại `DronesController`
- **Tập tin**: `Services/OperationsService/UavPms.OperationsService.API/Controllers/DronesController.cs`
- **Hiện trạng trước sửa**: Class không có attribute `[Authorize]`, cho phép người ngoài nặc danh gọi `GET /api/v1/drones`, `/available`, `/{id}`, `/{id}/status` để trinh sát thông tin phi đội UAV, tọa độ và mức pin.
- **Biện pháp khắc phục**: Đã thêm `[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]` ở cấp độ Controller.

### 📌 2. [AUTH-02] Thiếu xác thực tại `DevicesController.heartbeat`
- **Tập tin**: `Services/OperationsService/UavPms.OperationsService.API/Controllers/DevicesController.cs`
- **Hiện trạng trước sửa**: `POST /api/v1/devices/heartbeat` không yêu cầu xác thực, cho phép gửi tín hiệu giả mạo cho bất kỳ DeviceId nào.
- **Biện pháp khắc phục**: Đã bổ sung `[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]` bảo vệ endpoint.

### 📌 3. [AUTH-03] Thiếu xác thực tại Drone Vision Bridge
- **Tập tin**: `Services/AIInspectionService/UavPms.AIInspectionService.API/Controllers/VisionBridgeController.cs`
- **Hiện trạng trước sửa**: `POST /api/v1/vision/detections` nhận diện trực tiếp từ camera drone không có `[Authorize]`.
- **Biện pháp khắc phục**: Đã bổ sung `[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]` ở cấp độ Controller.

### 📌 4. [BFLA-01] Leo thang đặc quyền khi Thẩm định kết quả AI (`ReviewDetection`)
- **Tập tin**: `Services/OperationsService/UavPms.OperationsService.API/Controllers/MissionController.cs`
- **Hiện trạng trước sửa**: Endpoint `POST/PUT /api/v1/missions/{missionId}/detections/{detectionId}/review` sử dụng `UserRoles.AllAuthenticatedRoles`, cho phép thợ bảo trì (Technician) hoặc thanh tra viên (Inspector) tự ý phê duyệt/hủy bỏ khuyết tật và sinh phiếu bảo trì.
- **Biện pháp khắc phục**: Thu hẹp quyền hạn xuống `[Authorize(Roles = UserRoles.AdminManagerAnalyst)]` theo đúng quy chuẩn nghiệp vụ MF04 & MF02.

### 📌 5. [IDOR-01] Truy xuất ảnh kiểm tra và dữ liệu khuyết tật không kiểm tra quyền
- **Tập tin**: 
  - `GetInspectionsByMissionQueryHandler.cs`
  - `GetInspectionReportByIdQueryHandler.cs`
- **Hiện trạng trước sửa**: Bất kỳ người dùng nào có Token đều có thể cung cấp `missionId` hoặc `mediaId` để xem ảnh cận cảnh lưới điện, tọa độ GPS và khuyết tật của nhiệm vụ ở chi nhánh/vùng khác.
- **Biện pháp khắc phục**: Đã tích hợp `IMissionRepository.UserCanAccessAsync` và `ICurrentUserServices` để xác thực quyền truy cập trước khi trả dữ liệu.

### 📌 6. [IDOR-02] Inspector cùng vùng can thiệp chéo trạng thái bay của đồng nghiệp
- **Tập tin**: `MissionLifecycleService.cs`
- **Hiện trạng trước sửa**: `AccessibleMission` cho phép người dùng có phạm vi vùng truy cập được tất cả nhiệm vụ trong vùng đó. Khi Inspector A gọi `StartAsync`, `CompleteAsync`, `UploadFlightLogAsync`, `SubmitIncidentReportAsync` hoặc `ReturnDroneHandoverAsync` trên nhiệm vụ của Inspector B, hệ thống không ngăn chặn.
- **Biện pháp khắc phục**: Bổ sung hàm kiểm tra `EnsureAssignedInspectorOrManager(mission)`. Nếu người gọi không phải Manager hay SystemAdmin, bắt buộc phải là `InspectorId` hoặc có phân công `Active` trong `mission.Assignments`.

### 📌 7. [INFO-02] Bỏ qua bộ lọc phân quyền vùng khi liệt kê nhiệm vụ (`ListMissions`)
- **Tập tin**: 
  - `MissionRepository.cs`
  - `ListMissionsQueryHandler.cs`
  - `IMissionRepository.cs`
- **Hiện trạng trước sửa**: `GetMissionsPagedAsync` truy vấn trực tiếp bảng `Missions` mà không lọc theo `UserGeographicScopes`, khiến Quản lý cấp chi nhánh thấy danh sách nhiệm vụ toàn quốc.
- **Biện pháp khắc phục**: Đã thêm tham số `allowedRegionIds` vào `GetMissionsPagedAsync` và nạp tự động qua `UserGeographicScope` trong `ListMissionsQueryHandler`.

---

## 2. Kết Quả Kiểm Thử (Verification & Testing)

- **Tổng số Test Suites**: 448 unit tests chạy tự động qua lệnh `dotnet test`.
- **Kết quả**: **448/448 Passed (100%)**, 0 Failed, 0 Skipped trên toàn bộ 5 dịch vụ (.NET 9).
- **Test Suites bảo mật mới tạo**:
  - `InspectionAuthorizationTests.cs` (4 tests): Kiểm tra chặn IDOR khi đọc media/báo cáo nhiệm vụ trái phép.
  - `MissionDroneExecutionTests.cs` (bổ sung 2 tests IDOR): Xác minh Inspector cùng vùng nhưng không được giao nhiệm vụ sẽ bị chặn (`ForbiddenException: USER_NOT_ASSIGNED_TO_MISSION`) khi gọi `StartAsync` hoặc `CompleteAsync`.
  - `ListMissionsGeographicScopeTests.cs` (2 tests): Xác minh quản lý cấp vùng chỉ nhận được nhiệm vụ trong phạm vi khu vực được phân công, còn SystemAdmin được xem toàn quốc.
