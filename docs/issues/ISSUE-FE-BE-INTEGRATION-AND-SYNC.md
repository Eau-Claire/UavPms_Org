# [ISSUE-SYS-001] Báo Cáo Chi Tiết Lỗi & Kế Hoạch Đồng Bộ Hóa Toàn Diện Frontend - Backend (UAV-PMS)

- **Trạng thái**: ✅ Đã giải quyết & Kiểm thử thành công (Resolved & Fully Verified)
- **Mức độ ưu tiên**: 🔴 Nghiêm trọng (Critical - Ảnh hưởng toàn bộ luồng hoạt động từ xác thực, quản trị đến giám sát hiện trường)
- **Phân hệ**: ApiGateway / IdentityService / OperationsService / AIInspectionService / NotificationService / Frontend (Angular)
- **Kết quả nghiệm thu**: 
  - Backend .NET: `dotnet build` 0 Warning, 0 Error; 135 unit/integration test passed 100%.
  - Frontend Angular: `npm run build` thành công, bundle hoàn tất.
- **Tài liệu tham chiếu**:
  - `docs/api_spec.md`
  - `docs/issues/ISSUE-MF02-LINEAR-WORKFLOW-AND-BACKEND-APIS.md`
  - `docs/issues/ISSUE-MF03-FIELD-EXECUTION-AND-INGESTION.md`
  - `docs/mainflows/MF01_v2.0_FE_PreMission_Readiness.md`
  - `docs/mainflows/MF02_v2.0_FE_Create_Assign_Mission.md`

---

## 1. Tổng Quan & Phân Tích Vấn Đề (Problem Statement)

Sau khi tiến hành quét toàn bộ codebase giữa **Backend (.NET Microservices tại `UavPms_Org`)** và **Frontend (Angular tại `uav_fe/UavPms_FE`)**, hệ thống ghi nhận 9 vấn đề không đồng bộ chính dẫn đến các lỗi HTTP 401, 403, 404, 405 và lỗi làm sai lệch hoặc mất trạng thái dữ liệu (state corruption) trên giao diện.

Các nguyên nhân chính bao gồm:
1. **Lệch cấu hình ApiGateway**: Thiếu route chuyển tiếp trong file nạp cấu hình `ocelot.Development.json`.
2. **Xung đột phân quyền (RBAC)**: Các API Dashboard và Quản lý đánh giá chỉ mở cho `SystemAdmin` và `Manager`, chặn các vai trò `Analyst`, `Inspector`, `Technician`.
3. **Mất toàn vẹn dữ liệu (State Corruption)**: API `updatePriority` trả về DTO rút gọn khiến hàm normalizer trên FE xóa trắng dữ liệu chi tiết của nhiệm vụ.
4. **Sai lệch giao thức CRUD User**: Frontend gọi `PATCH /users/{id}`, `DELETE /users/{id}`, `POST /users/{id}/reset-password` và ép kiểu mảng sai cấu trúc phân trang.
5. **Cơ chế Step-Up OTP**: Luồng đổi mật khẩu trên Backend bắt buộc Header `X-StepUp-Token` nhưng Frontend gọi trực tiếp không có OTP token.
6. **Lệch cấu hình DTO Cảnh báo khẩn cấp (Emergency Alerts)**: Backend trả về DTO dạng lồng nhau (`Location`, `Evidence`) và object phân trang, Frontend map phẳng và ép kiểu mảng gây mất tọa độ/hình ảnh.
7. **Thiếu Endpoint Upload & Duyệt sự cố độc lập**: Giao diện Upload phân tích AI và Duyệt sự cố gọi endpoint độc lập (`/ai-analysis/upload`, `/anomalies/pending`) không tồn tại trên Backend.
8. **Lệch tên tham số truy vấn UAV**: Frontend gửi `status`, Backend nhận `operationalStatus`.
9. **Thiếu phân trang & hỗ trợ query trong Pre-Mission V2**: Backend v2 chỉ nhận tham số `status` và trả về mảng không phân trang.

---

## 2. Danh Mục Các Issue Chi Tiết (Detailed Sub-Issues)

### 📌 Issue #1: Bổ sung Route Reports và Maintenance trong ApiGateway Development (`ocelot.Development.json`)
- **Mức độ**: 🔴 P0 - Chặn luồng nghiệp vụ
- **Mô tả**:
  - `ocelot.json` có route `/api/v{version}/reports` và `/api/v{version}/maintenance`.
  - `ocelot.Development.json` thiếu 2 route này, khiến mọi request từ FE trong môi trường dev bị 404.
- **Giải pháp**:
  - Bổ sung cấu hình downstream tới `http://localhost:5102` cho `/api/v{version}/reports` và `/api/v{version}/maintenance` trong `ocelot.Development.json`.
- **Tiêu chuẩn nghiệm thu**:
  - Gọi `GET http://localhost:5194/api/v1/reports` và `GET http://localhost:5194/api/v1/maintenance` qua Gateway trả về 200/401 thay vì 404.

---

### 📌 Issue #2: Chuẩn hóa Phân quyền Dashboard Giám sát (`MonitorController.cs`)
- **Mức độ**: 🔴 P0 - Chặn truy cập trang chủ
- **Mô tả**:
  - Frontend tự động chuyển hướng mọi user sau đăng nhập về `/dashboard`.
  - Frontend gọi `forkJoin` 5 API: `summary`, `recent-defects`, `defects-statistics`, `mission-status`, `alerts`.
  - Backend yêu cầu `AdminAndManager` cho `summary` và `mission-status`, yêu cầu `AdminManagerAnalyst` cho 3 API còn lại.
  - Khi `Analyst`, `Inspector`, `Technician` đăng nhập, các request này trả về 403 Forbidden khiến trang chủ sập hoàn toàn.
- **Giải pháp**:
  - Cập nhật phân quyền trên [MonitorController.cs](file:///home/an/RiderProjects/UavPms_Org/Services/OperationsService/UavPms.OperationsService.API/Controllers/MonitorController.cs) cho phép `AllAuthenticatedRoles` hoặc `AdminManagerInspectorAnalyst` truy xuất dashboard metrics.
  - Cập nhật Frontend `monitor-api.ts` để bắt lỗi độc lập cho từng widget, tránh `forkJoin` chết chùm.
- **Tiêu chuẩn nghiệm thu**:
  - Người dùng thuộc mọi vai trò (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`) đều mở được trang Dashboard mà không gặp lỗi 403.

---

### 📌 Issue #3: Toàn vẹn Dữ liệu Cập nhật Mức độ Ưu tiên Nhiệm vụ (`updatePriority`)
- **Mức độ**: 🔴 P0 - Gây mất trạng thái dữ liệu (State Corruption)
- **Mô tả**:
  - Backend `PATCH /api/v1/missions/{id}/priority` chỉ trả về DTO rút gọn: `{ Id, MissionCode, Priority, Objective, EmergencyReason, PriorityDefects }`.
  - Frontend `mission-detail.ts` gán đè response này vào state `this.mission.set(...)`, làm các trường `status`, `assignments`, `targets`, `plannedStart`, `routeData` bị reset về giá trị mặc định / rỗng.
- **Giải pháp**:
  - Backend: `MissionController.UpdatePriority` trả về toàn bộ thông tin chi tiết của nhiệm vụ sau cập nhật (hoặc gọi `GetMissionDetailsQuery`).
  - Frontend: Trong `mission-detail.ts`, chỉ merge các trường thay đổi (`priority`, `objective`, `emergencyReason`, `priorityDefects`) vào state hiện tại.
- **Tiêu chuẩn nghiệm thu**:
  - Cập nhật mức độ ưu tiên trên giao diện chi tiết nhiệm vụ giữ nguyên danh sách nhân sự, cột điện mục tiêu và trạng thái hiện tại.

---

### 📌 Issue #4: Đồng bộ CRUD Quản lý Người dùng (`UserController.cs` & `users-api.ts`)
- **Mức độ**: 🟡 P1 - Lỗi nghiệp vụ quản trị
- **Mô tả**:
  - Frontend `getAll` mong đợi mảng, Backend trả về object `{ items, pagination }`.
  - Frontend gửi `PATCH /api/v1/users/{id}`, Backend chỉ có `PUT /api/v1/users/{id}` nhận đủ trường.
  - Frontend gửi `CreateUserRequest` thiếu `password`, `role` dạng chuỗi đơn. Backend bắt buộc `Password` và `Roles: List<string>`.
  - Frontend gọi `POST /users/{id}/reset-password` và `DELETE /users/{id}` không tồn tại trên Backend.
  - Tên role: Frontend dùng `Admin`, Database Backend chỉ có `SystemAdmin`.
- **Giải pháp**:
  - Backend:
    - Bổ sung `HttpPatch("{id:guid}")` trên `UserController.cs` hỗ trợ cập nhật linh hoạt role/status.
    - Hỗ trợ endpoint `POST /api/v1/users/{id}/reset-password` để Admin đặt lại mật khẩu tạm thời cho người dùng.
    - Bổ sung `HttpDelete("{id:guid}")` ánh xạ sang `SuspendUserCommand` (soft suspend).
    - Chuẩn hóa ánh xạ role: Tự động chuyển đổi `Admin` $\leftrightarrow$ `SystemAdmin`.
  - Frontend:
    - Sửa `users-api.ts` để đọc mảng `items` từ `PaginatedUsersResponse`.
    - Sinh mật khẩu ngẫu nhiên hoặc yêu cầu mật khẩu khi tạo User.
- **Tiêu chuẩn nghiệm thu**:
  - Thao tác xem danh sách, lọc, tạo mới, cập nhật vai trò, khóa tài khoản và đặt lại mật khẩu người dùng hoạt động trơn tru.

---

### 📌 Issue #5: Hỗ trợ Đổi Mật Khẩu Linh Hoạt (Step-Up OTP hoặc Xác Thực Mật Khẩu Hiện Tại)
- **Mức độ**: 🟡 P1 - Lỗi xác thực
- **Mô tả**:
  - Backend `POST /api/v1/users/change-password` chặn bằng `[RequireStepUp("ChangePassword")]`.
  - Frontend `change-password.ts` không có bước nhập OTP, chỉ gửi `{ newPassword }`.
- **Giải pháp**:
  - Backend: Cho phép đổi mật khẩu khi user đã đăng nhập hợp lệ nếu cung cấp đúng `OldPassword`, HOẶC nếu cung cấp `X-StepUp-Token`.
  - Frontend: Bổ sung trường `oldPassword` (mật khẩu hiện tại) vào form đổi mật khẩu.
- **Tiêu chuẩn nghiệm thu**:
  - Người dùng đăng nhập có thể đổi mật khẩu thành công.

---

### 📌 Issue #6: Đồng bộ Cấu Trúc Dữ Liệu Cảnh Báo Khẩn Cấp Edge AI (`EmergencyAlertsApi`)
- **Mức độ**: 🟡 P1 - Lỗi hiển thị sự cố khẩn cấp
- **Mô tả**:
  - `GET /alerts/active` trả về `Location` và `Evidence` lồng nhau, Frontend map phẳng làm mất tọa độ GPS và hình ảnh.
  - `GET /alerts` trả về object `{ totalCount, page, pageSize, items }`, Frontend kiểm tra `Array.isArray` thất bại và fallback về mock.
- **Giải pháp**:
  - Frontend: Viết hàm normalizer chuẩn cho `ActiveEmergencyAlertDto` trong `emergency-alerts-api.ts`.
  - Frontend: Bóc tách mảng `items` từ response phân trang của `/alerts`.
- **Tiêu chuẩn nghiệm thu**:
  - Cảnh báo khẩn cấp từ Edge AI hiển thị đầy đủ tọa độ trên bản đồ GIS và ảnh vi phạm.

---

### 📌 Issue #7: Triển Khai Endpoint Upload AI Độc Lập & Routing Duyệt Sự Cố
- **Mức độ**: 🟡 P1 - Tính năng phân tích AI
- **Mô tả**:
  - Frontend gọi `POST /api/v1/ai-analysis/upload` để upload ảnh/video độc lập không qua mission.
  - Frontend gọi `GET /api/v1/anomalies/pending` và `PUT /api/v1/anomalies/{id}/validate` để duyệt lỗi.
- **Giải pháp**:
  - Backend: Thêm endpoint `POST /api/v1/ai-analysis/upload` trên `AIAnalysisController` xử lý file media tải lên.
  - Backend & Gateway: Khai báo route và controller cho `/api/v1/anomalies/pending` và `/api/v1/anomalies/{id}/validate` (hoặc ánh xạ vào `AIAnalysisController`).
- **Tiêu chuẩn nghiệm thu**:
  - Upload file trên `/ai-analysis/upload` chạy thành công.
  - Trang `/ai-review` hiển thị danh sách sự cố chờ thẩm định.

---

### 📌 Issue #8: Đồng Bộ Tham Số Lọc Đội Bay UAV (`DronesController`)
- **Mức độ**: 🟢 P2 - Trải nghiệm người dùng
- **Mô tả**:
  - Frontend gửi query `status`, Backend nhận `operationalStatus`.
- **Giải pháp**:
  - Backend: Hỗ trợ cả 2 tham số `operationalStatus` và `status` (fallback).
  - Frontend: Cập nhật `drone-management-api.ts` gửi `operationalStatus`.
- **Tiêu chuẩn nghiệm thu**:
  - Lọc UAV theo trạng thái hoạt động trên giao diện hoạt động chính xác.

---

### 📌 Issue #9: Mở Rộng Bộ Lọc & Phân Quyền Pre-Mission Assessment V2
- **Mức độ**: 🟢 P2 - Hoàn thiện nghiệp vụ
- **Mô tả**:
  - `PreMissionAssessmentV2Controller.List` chỉ nhận `status`, thiếu các tiêu chí lọc `page`, `pageSize`, `regionId`, `search`.
  - Phân quyền chỉ cho phép `AdminAndManager`, trong khi Inspector cần xem danh sách đánh giá liên quan.
- **Giải pháp**:
  - Backend: Bổ sung tham số phân trang và lọc cho `PreMissionAssessmentV2Controller.List`.
  - Cho phép `Inspector` xem danh sách đánh giá.
- **Tiêu chuẩn nghiệm thu**:
  - Phân trang và tìm kiếm bản đánh giá tiền nhiệm vụ hoạt động đầy đủ.

---

## 3. Thứ Tự Triển Khai (Implementation Roadmap)

1. **Giai đoạn 1 (Ngay lập tức - P0):**
   - Issue #1: Bổ sung route `/reports` và `/maintenance` vào `ocelot.Development.json`.
   - Issue #2: Cập nhật phân quyền trên `MonitorController.cs` và xử lý resilient trên Frontend.
   - Issue #3: Sửa `updatePriority` trên Backend và Frontend để chống mất dữ liệu.
2. **Giai đoạn 2 (Cốt lõi - P1):**
   - Issue #4: Hoàn thiện CRUD User (Patch, Delete/Suspend, Reset PW, Roles mapping).
   - Issue #5: Cập nhật luồng đổi mật khẩu xác thực mật khẩu cũ.
   - Issue #6: Chuẩn hóa Normalizer cho Active Alerts và Alert History.
   - Issue #7: Triển khai Standalone Upload và Anomaly Review endpoints.
3. **Giai đoạn 3 (Tối ưu - P2):**
   - Issue #8: Đồng bộ query param `status`/`operationalStatus` cho Drones.
   - Issue #9: Mở rộng phân trang và quyền đọc cho Pre-Mission Assessment V2.
