# [ISSUE-MF03-001] Chuẩn Hóa Thực Thi Hiện Trường, Bàn Giao Drone & Thu Thập Dữ Liệu MF03

- **Trạng thái**: ✅ Đã giải quyết (Resolved)
- **Mức độ ưu tiên**: 🔴 Nghiêm trọng (High / Critical - Trực tiếp quản lý vòng đời thiết bị UAV và dữ liệu bay thực địa)
- **Phân hệ**: OperationsService (API / Application / Infrastructure / Domain)
- **Tài liệu liên quan**:
  - `docs/requirements/mainflows/MF02_v2.0_Create_Assign_Inspection_Mission.md`
  - `docs/requirements/mainflows/MF04.md`
  - `docs/requirements/mainflows/MF03_v2.0_Field_Execution_And_Ingestion.md`

---

## 1. Phân Tích Vấn Đề Cốt Lõi (Problem Analysis)

Giai đoạn Thực thi Hiện trường (Main Flow 03 - Field Execution) là cầu nối giữa lập lịch (MF02) và phân tích thẩm định (MF04):
1. **Thiếu đồng bộ trạng thái UAV**: Khi nhiệm vụ chuyển sang `InProgress` (bắt đầu bay), trạng thái thiết bị UAV chưa được đồng bộ sang `Flying`. Khi hoàn thành nhiệm vụ (`Completed`), UAV chưa tự động trả về `Idle` và các đặt chỗ tài nguyên (`ResourceBooking`) chưa được giải phóng (`Released`).
2. **Thiếu quy trình hoàn trả Drone Handover**: Đã có bàn giao xuất phát (`DroneHandover`) nhưng thiếu API và logic hoàn trả thiết bị sau chuyến bay (`ReturnDroneHandover`).
3. **Thiếu Ingestion cho Flight Log / Telemetry**: Chưa có endpoint và quy chuẩn lưu trữ log bay (thời lượng, dung lượng pin, độ cao, tọa độ, dữ liệu JSON telemetry).
4. **Thiếu Báo cáo Sự cố Hiện trường (Field Incident Reporting)**: Khi xảy ra sự cố đột xuất (va chạm, thời tiết xấu, hỏng hóc), hiện trường chưa có kênh báo cáo trực tiếp gắn với nhiệm vụ và tài sản, đồng thời thông báo khẩn cấp cho Quản lý.
5. **Giới hạn quyền Upload Media**: Chỉ cho phép `mission.InspectorId` tải ảnh, chặn các thanh tra viên khác được phân công qua `MissionAssignment`.
6. **Thiếu trích xuất siêu dữ liệu EXIF tự động**: Ảnh chụp từ UAV chứa metadata GPS và thời gian chụp trong JPEG EXIF APP1, nhưng trước đây hệ thống yêu cầu truyền thủ công qua form-data.

---

## 2. Các Thành Phần Đã Hoàn Thiện (Implemented Components)

### 📌 1. Quản lý Vòng đời Bay & Đồng bộ Thiết bị (Drone Execution Lifecycle)
- **Bắt đầu bay (`StartAsync`)**:
  - Kiểm tra trạng thái hợp lệ (`Assigned`).
  - Tự động cập nhật `Uav.Status = DroneStatus.Flying`.
  - Phát sự kiện vòng đời thời gian thực `STARTED` qua SignalR.
- **Hoàn thành bay (`CompleteAsync`)**:
  - Cập nhật `Mission.Status = Completed`.
  - Tự động chuyển `Uav.Status = DroneStatus.Idle`.
  - Giải phóng toàn bộ `ResourceBooking` của nhiệm vụ sang `ResourceBookingStatus.Released`.
  - Phát sự kiện vòng đời thời gian thực `COMPLETED`.
- **Hoàn trả thiết bị (`ReturnDroneHandoverAsync`)**:
  - Xác thực nhân sự bàn giao và bản ghi handover xuất phát.
  - Cập nhật thời gian hoàn trả, người nhận, tình trạng thiết bị sau bay.
  - Endpoint: `POST /api/v1/missions/{id}/drone-handover/return`.

### 📌 2. Thu thập Nhật ký Bay & Telemetry (Flight Log Ingestion)
- **Endpoint**: `POST /api/v1/missions/{id}/flight-log` & `GET /api/v1/missions/{id}/flight-logs`.
- **Validation**:
  - Nhiệm vụ phải ở trạng thái thực thi (`InProgress` hoặc `Completed`).
  - Kiểm tra tính hợp lệ của JSON telemetry.
  - Ràng buộc thời lượng bay > 0, độ cao tối đa >= 0, mức pin còn lại trong khoảng `0 - 100%`.

### 📌 3. Báo cáo Sự cố Hiện trường (Field Incident Reporting)
- **Endpoint**: `POST /api/v1/missions/{id}/incidents` & `GET /api/v1/missions/{id}/incidents`.
- **Quy tắc nghiệp vụ**:
  - Phân loại mức độ nghiêm trọng: `Low`, `Medium`, `High`, `Critical`.
  - Mô tả sự cố tối thiểu 10 ký tự.
  - Tự động liên kết với tài sản mục tiêu (`AssetId`) của nhiệm vụ.
  - Khi sự cố ở mức `Critical`, tự động phát thông báo khẩn cấp tới Quản lý qua `IMissionRealtimeNotifier`.

### 📌 4. Mở rộng Tải ảnh Kiểm tra & Trích xuất EXIF (Media Ingestion & EXIF Extraction)
- **Đa vai trò phân công**: Cho phép cả `mission.InspectorId` lẫn bất kỳ thanh tra viên nào có trạng thái `Accepted` trong `MissionAssignment` tải ảnh kiểm tra.
- **Trích xuất EXIF không phụ thuộc thư viện ngoài (`ExifMetadataExtractor`)**:
  - Tự động bóc tách phân đoạn JPEG APP1 (TIFF header Big-Endian/Little-Endian).
  - Trích xuất tọa độ GPS (Độ, Phút, Giây sang Decimal Degrees) và thời gian chụp (`DateTime`).
  - Tự động điền tọa độ và thời gian chụp nếu client không gửi kèm.
- **Alias Route**: Hỗ trợ route chuẩn RESTful theo nhiệm vụ `POST /api/v1/missions/{id}/media` song song với `POST /api/v1/inspections/upload`.

---

## 3. Ma Trận Kiểm Thử Tự Động (Test Coverage Matrix)

| Test Suite | Số lượng Test | Kết quả | Mô tả bao phủ |
| :--- | :--- | :--- | :--- |
| `ExifMetadataExtractorTests` | 5 | ✅ 5/5 Passed | Stream null, non-JPEG, APP1 rỗng, synthetic TIFF Little-Endian GPS & DateTime |
| `MissionDroneExecutionTests` | 5 | ✅ 5/5 Passed | Check-in GPS validation, UAV flying on start, UAV idle & bookings released on complete, Drone Handover pre/post |
| `MissionFlightLogTests` | 5 | ✅ 5/5 Passed | Ingestion hợp lệ, status guard, invalid JSON, pin ngoài dải, truy xuất danh sách log |
| `MissionIncidentReportTests` | 4 | ✅ 4/4 Passed | Gửi sự cố thành công, thông báo khẩn cấp Critical, validate độ dài/mức độ, truy xuất sự cố |
| **Toàn bộ Solution** | **440** | **✅ 440/440 Passed** | 0 Failed, 0 Skipped trên toàn bộ các project .NET 9 |
