# [ISSUE-MF02-001] Chuẩn hóa Vòng đời Tuyến tính (Linear Workflow) & Bộ 5 API Backend MF02

- **Trạng thái**: Đã giải quyết (Resolved)
- **Mức độ ưu tiên**: 🔴 Nghiêm trọng (High / Critical - Quyết định tính đúng đắn dữ liệu nghiệp vụ)
- **Phân hệ**: OperationsService / AIInspectionService / ApiGateway
- **Tài liệu tham chiếu**: 
  - `docs/BACKEND_SPEC_LINEAR_WORKFLOW_AND_APIS_MF02.md`
  - `docs/requirements/mainflows/MF02_v2.0_Create_Assign_Inspection_Mission.md`
  - `docs/mainflows/MF02_v2.0_FE_Create_Assign_Mission.md`

---

## 1. Phân Tích Vấn Đề Cốt Lõi (Problem Analysis)

Hệ thống đang gặp lỗi dữ liệu phi tuyến tính (**Non-linear Data Leak**):
1. **Rò rỉ dữ liệu khi tạo mới**: Nhiệm vụ mới ở trạng thái `PENDING_CONFIRMATION` (`PendingAcceptance`) nhưng giao diện đã hiển thị khuyết tật AI, phiếu bảo trì, và tích xanh hoàn thành tiếp nhận.
2. **Nguyên nhân kỹ thuật**:
   - **Frontend**: Khi API trả về mảng rỗng `[]`, FE fallback sang mock data làm sai lệch dữ liệu thực tế.
   - **Backend**:
     - Thiếu 5 endpoint chuyên biệt trực tiếp theo `missionId`: Detections, Review Detection, Maintenance Tasks, Activity Stream, Assignments Matrix.
     - Thiếu State Machine Guard ngăn nạp ảnh/video UAV khi nhiệm vụ chưa ở trạng thái thực thi (`InProgress` / `Completed`).
     - Thiếu logic tự động chuyển trạng thái sang `Assigned` (CONFIRMED) khi đủ 3 vai trò tiếp nhận.

---

## 2. Danh Mục Các Vấn Đề Cần Giải Quyết (Sub-Issues & Work Breakdown)

### 📌 Vấn đề 1 (Issue #1): State Machine Guard cho Upload Media UAV
- **Mô tả**: Chặn hành vi tải ảnh/video kiểm tra nếu nhiệm vụ đang ở trạng thái `Draft` hoặc `PendingAcceptance`.
- **Vị trí**: `UploadInspectionImageCommandHandler.cs`
- **Tiêu chuẩn nghiệm thu**: Trả về `BusinessRuleException` ("INVALID_MISSION_STATUS_FOR_UPLOAD") nếu nhiệm vụ chưa được xác nhận/bắt đầu.
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

### 📌 Vấn đề 2 (Issue #2): Quản lý Phân công Đa Vai trò & Tự động Xác nhận
- **Mô tả**: 
  - Endpoint `GET /api/v1/missions/{id}/assignments`: Trả về tổng số vai trò cần thiết, số vai trò đã nhận, cờ `allConfirmed`, hạn chót và danh sách chi tiết nhân sự.
  - Tự động chuyển trạng thái nhiệm vụ sang `Assigned` (`CONFIRMED`) và phát thông báo Realtime SignalR khi 100% vai trò bắt buộc đã chấp nhận (`AcceptAssignmentAsync`).
- **Tiêu chuẩn nghiệm thu**: Trả về 200 OK với đúng schema JSON, tự động chuyển state và broadcast realtime lifecycle event.
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

### 📌 Vấn đề 3 (Issue #3): API Truy xuất Kết quả AI theo Nhiệm vụ
- **Mô tả**: Endpoint `GET /api/v1/missions/{id}/detections`.
- **Tính năng**: Hỗ trợ lọc theo `status`, `mediaType`, `isEmergency`. Map BoundingBox JSON, tính nhãn thời gian `timestampLabel` (dạng `mm:ss`).
- **Tiêu chuẩn nghiệm thu**: Trả về `[]` rỗng với HTTP 200 OK nếu chưa có kết quả AI (tuyệt đối không 500 hoặc 404).
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

### 📌 Vấn đề 4 (Issue #4): API Thẩm định Kết quả AI & Kích hoạt Bảo trì Tự động
- **Mô tả**: Endpoint `POST /api/v1/missions/{missionId}/detections/{detectionId}/review`.
- **Hành vi**:
  - `Approved`: Khấu trừ `CurrentHealthScore` của tài sản liên quan, ghi log `AssetHealthHistory`, tự động sinh bản ghi khuyến nghị bảo dưỡng `MaintenanceTicket`.
  - `Rejected`: Đánh dấu khuyết tật bị từ chối, không trừ điểm tài sản, không tạo ticket bảo trì.
- **Tiêu chuẩn nghiệm thu**: Thẩm định thành công, dữ liệu tài sản và ticket bảo dưỡng được cập nhật nguyên tử (transactional).
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

### 📌 Vấn đề 5 (Issue #5): API Truy xuất Khuyến nghị Bảo dưỡng theo Nhiệm vụ
- **Mô tả**: Endpoint `GET /api/v1/missions/{id}/maintenance-tasks`.
- **Tiêu chuẩn nghiệm thu**: Trả về danh sách phiếu bảo trì phát sinh từ các lỗi `Approved` thuộc nhiệm vụ; trả về `[]` rỗng nếu chưa có lỗi duyệt.
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

### 📌 Vấn đề 6 (Issue #6): Luồng Trao đổi & Nhật ký Hoạt động Thời gian Thực
- **Mô tả**: Endpoint `GET /api/v1/missions/{id}/activities` & `POST /api/v1/missions/{id}/activities`.
- **Tiêu chuẩn nghiệm thu**: Ghi nhận tin nhắn/hoạt động, hỗ trợ phân vai (Manager / Inspector / Analyst / Technician), bắn realtime event tới các bên liên quan.
- **Trạng thái**: ✅ Đã giải quyết & Đã có Unit Test bao phủ.

---

## 3. Kế Hoạch Triển Khai & Kiểm Thử (Execution Plan)

- [x] Tạo Issue đặc tả và phân tích kiến trúc chi tiết
- [x] Giải quyết Vấn đề 1: Thêm State Machine Guard trong `UploadInspectionImageCommandHandler.cs`
- [x] Giải quyết Vấn đề 2, 3, 4, 5, 6: Triển khai trong `MissionLifecycleService.cs`
- [x] Expose 6 endpoint tương ứng trong `MissionController.cs`
- [x] Viết Unit Test bao phủ toàn bộ các use-case mới trong `UavPms.OperationsService.Tests`
- [x] Biên dịch toàn hệ thống và chạy kiểm thử tự động (252/252 tests passed, 0 error)
