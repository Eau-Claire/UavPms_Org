# Hướng dẫn vận hành luồng tích hợp Vision Edge ↔ PMS Backend

Tài liệu này hướng dẫn chi tiết cách cấu hình, khởi chạy, kiểm tra dữ liệu và vận hành hệ thống tích hợp giữa dịch vụ AI Vision (Python Edge) và hệ thống Quản lý (.NET Backend) bao gồm:
1. Đăng ký thiết bị tự động (Device Registration)
2. Nhịp tim trạng thái (Device Heartbeat)
3. Tải lên và tự động liên kết Khuyết tật với Lịch trình bay đang thực thi (Anomaly & Mission Association)

---

## 1. Chuẩn bị môi trường & Cấu hình

### Phía PMS WebApi (.NET Backend)
- Đảm bảo cơ sở dữ liệu Supabase/PostgreSQL đã được kết nối (cấu hình trong `appsettings.json` hoặc biến môi trường).
- Hệ thống tự động khởi tạo dữ liệu mẫu thông qua `DatabaseSeeder.cs` khi khởi chạy lần đầu, bao gồm:
  - Tài khoản kiểm định viên (`469bfac4-8b96-4f27-a772-945cff2fbaa8`).
  - Hệ thống cây tài sản mặc định có ID `Guid.Empty` (`00000000-0000-0000-0000-000000000000`).
  - UAV mẫu có mã hiệu `UAV001`.
  - Một lịch trình bay mẫu (Mission) có trạng thái `Executing` gắn với `UAV001`.

### Phía Vision Device (Python Edge)
Cấu hình các tham số tích hợp trong file `/home/minhchau/Documents/Vision-Base-Human-Motion-Detection/.env`:

```env
# Kích hoạt tính năng kết nối và gửi dữ liệu sang PMS
PMS_BRIDGE_ENABLED=true

# Địa chỉ gốc của PMS WebApi
PMS_BRIDGE_URL=http://localhost:5196

# Endpoint nhận dữ liệu nhận diện khuyết tật
PMS_BRIDGE_ENDPOINT=/api/v1/vision/detections

# Thời gian timeout cho mỗi request HTTP (giây)
PMS_BRIDGE_TIMEOUT=5

# Mã số định danh thiết bị (để map với UAV trên PMS)
PMS_BRIDGE_SERIAL=UAV001

# Phiên bản phần mềm chạy trên Edge
PMS_BRIDGE_SOFTWARE_VERSION=1.0.0
```

---

## 2. Các bước vận hành & Kiểm tra

### Bước 1: Khởi chạy PMS Backend
Mở terminal và di chuyển đến thư mục dự án PMS:
```bash
cd /home/minhchau/Documents/PMS
dotnet run --project UavPms.WebApi
```
Đợi log hiển thị thông báo ứng dụng đã chạy:
`Now listening on: http://localhost:5196`

### Bước 2: Khởi chạy Vision Edge Service
Mở một terminal khác và di chuyển đến thư mục dự án Vision:
```bash
cd /home/minhchau/Documents/Vision-Base-Human-Motion-Detection
.venv/bin/python -m edge.main
```

**Các kịch bản xảy ra khi khởi chạy:**
- **Kịch bản A (Chưa được duyệt)**: Nếu cấu hình mã thiết bị mới trong `.env` (ví dụ `PMS_BRIDGE_SERIAL=RPI-999`), log sẽ báo:
  `Device registration status: Pending. Retrying in 10s...`
  Đồng thời trên cơ sở dữ liệu sẽ tự động tạo một UAV mới với trạng thái `Pending` để chờ Admin duyệt.
- **Kịch bản B (Đã được duyệt)**: Nếu cấu hình mã thiết bị trùng khớp với UAV đã đăng ký và duyệt thành công trên hệ thống (ví dụ `PMS_BRIDGE_SERIAL=UAV001` được seed sẵn), log sẽ báo:
  `Device registered with PMS. Drone ID: UAV001`
  Và sau đó chuyển sang luồng gửi nhịp tim trạng thái định kỳ:
  `Heartbeat sent to PMS. Battery: 95.0%`

---

## 3. Mô phỏng gửi dữ liệu nhận diện khuyết tật (Detections)

Để giả lập sự kiện camera của thiết bị bay phát hiện ra khuyết tật và gửi báo cáo về PMS, chạy script test đã chuẩn bị sẵn ở phía Python Edge:
```bash
cd /home/minhchau/Documents/Vision-Base-Human-Motion-Detection
.venv/bin/python scratch_test.py
```

Khi chạy thành công, output sẽ trả về:
`FORWARD SUCCESS: True`

Đồng thời, xem log ở phía PMS WebApi để kiểm chứng các bước xử lý tự động:
1. Nhận thông tin gửi tới từ UAV `UAV001`.
2. Kiểm tra và tìm thấy Mission mẫu đang thực thi (`Executing`) là `MS-0001` gắn với UAV này.
3. Lưu hình ảnh bằng chứng vào thư mục vật lý `uav_storage/images/`.
4. Tạo và lưu bản ghi `InspectionMedia` liên kết với Mission.
5. Tạo bản ghi `DetectedAnomaly` lưu thông tin tọa độ bounding box, độ tin cậy và liên kết với Media vừa tạo ở trên.

---

## 4. Truy vấn Kiểm tra Cơ sở dữ liệu (Supabase/PostgreSQL)

Có thể chạy các câu truy vấn sau trực tiếp trên SQL Editor của Supabase để kiểm tra tính chính xác của dữ liệu:

### 1. Kiểm tra trạng thái UAV và mức pin nhận được từ heartbeat
```sql
SELECT "UavCode", "Status", "BatteryLevel", "UpdatedAt" 
FROM "UAVs" 
WHERE "UavCode" = 'UAV001';
```

### 2. Kiểm tra ảnh bằng chứng nhận diện (InspectionMedia)
```sql
SELECT "Id", "MissionId", "MediaType", "FileUrl", "AiSource", "CapturedAt" 
FROM "InspectionMedia" 
ORDER BY "CapturedAt" DESC 
LIMIT 5;
```

### 3. Kiểm tra thông tin khuyết tật chi tiết (DetectedAnomaly)
```sql
SELECT a."Id", a."ConfidenceScore", a."BoundingBox", a."ValidationStatus", c."CategoryName", a."AnalystNotes"
FROM "DetectedAnomalies" a
LEFT JOIN "DefectCategories" c ON a."CategoryId" = c."Id"
ORDER BY a."CreatedAt" DESC 
LIMIT 5;
```

---

## 5. Khắc phục sự cố (Troubleshooting)

1. **Lỗi `Connection Refused` khi gửi Heartbeat hoặc Detection**
   - *Nguyên nhân*: PMS WebApi chưa được chạy hoặc chạy ở cổng khác với cấu hình của Edge.
   - *Khắc phục*: Kiểm tra cổng HTTP trong log PMS WebApi lúc khởi động (mặc định là `5196`), cập nhật lại biến `PMS_BRIDGE_URL` trong `.env` tương ứng.

2. **Lỗi `Unspecified DateTime Kind` khi ghi nhận dữ liệu**
   - *Nguyên nhân*: PostgreSQL yêu cầu giá trị thời gian UTC được chỉ định loại rõ ràng (`DateTimeKind.Utc`).
   - *Khắc phục*: Hệ thống đã xử lý tự động thông qua việc bọc biến thời gian bằng hàm `DateTime.SpecifyKind(..., DateTimeKind.Utc)`. Hãy chắc chắn sử dụng phiên bản code đã cập nhật mới nhất.

3. **Thiết bị cứ báo `Pending` liên tục**
   - *Nguyên nhân*: UAV với serial tương ứng đang ở trạng thái `Pending` trong DB.
   - *Khắc phục*: Cập nhật cột `Status` của UAV đó trong database thành `Active` hoặc `Online` để mô phỏng hành động phê duyệt của Admin:
     ```sql
     UPDATE "UAVs" SET "Status" = 'Active' WHERE "UavCode" = 'mã_serial_của_bạn';
     ```
