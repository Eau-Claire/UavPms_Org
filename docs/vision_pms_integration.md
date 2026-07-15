# Vision ↔ PMS Integration Context

> **Ngày tạo**: 2026-06-30  
> **Trạng thái**: Hoàn thành tích hợp cơ bản (Test Ready)  
> **Mục đích**: Kết nối Vision-Base-Human-Motion-Detection (Python) với UavPms Backend (.NET 9)

---

## 1. Tổng quan Kiến trúc Kết nối

```
┌──────────────────────────┐     HTTP POST (multipart)     ┌──────────────────────────┐
│  Vision Edge Device      │ ─────────────────────────────→│  UavPms .NET Backend     │
│  (Python FastAPI)        │  /api/v1/vision/detections    │  (ASP.NET Core 9)        │
│  Port: 8001              │                               │  Port: 5196              │
├──────────────────────────┤                               ├──────────────────────────┤
│ • Camera capture         │     Health Check              │ • VisionBridgeController │
│ • YOLOv8 detection       │ ←───────────────────────────→ │ • ReceiveVisionDetection │
│ • LSTM classification    │  /api/v1/vision/health        │   CommandHandler         │
│ • ByteTrack tracking     │                               │ • Structured Logging     │
│ • AlertService           │     Song song với             │ • File Storage (ảnh)     │
│   ├─ ApiClient (mock BE) │     mock backend              │ • Notification (tương    │
│   └─ PmsBridgeClient ──→ │     (port 8000)               │   lai)                   │
└──────────────────────────┘                               └──────────────────────────┘
```

### Giao thức truyền dữ liệu
- **Transport**: HTTP REST (không dùng RabbitMQ vì Vision system là external service)
- **Format**: `multipart/form-data` (tương thích ApiClient hiện tại)
- **Auth**: AllowAnonymous (internal service-to-service, sẽ thêm API Key khi production)

---

## 2. Files đã tạo/sửa

### Phía PMS (.NET Backend)
| File | Mô tả |
|------|--------|
| `UavPms.Application/Features/VisionBridge/DTOs/VisionDetectionDto.cs` | DTO nhận detection (class_name, confidence, GPS, bbox, track_id) |
| `UavPms.Application/Features/VisionBridge/DTOs/VisionDetectionResultDto.cs` | DTO phản hồi (success, recordId, receivedAt) |
| `UavPms.Application/Features/VisionBridge/Commands/ReceiveVisionDetectionCommand.cs` | MediatR Command (CQRS pattern) |
| `UavPms.Application/Features/VisionBridge/Commands/ReceiveVisionDetectionCommandHandler.cs` | Handler: log, lưu ảnh, tạo record ID |
| `UavPms.WebApi/Controllers/VisionBridgeController.cs` | 3 endpoints: multipart receiver, JSON receiver, health check |

### Phía Vision (Python Edge Device)
| File | Mô tả |
|------|--------|
| `edge/clients/pms_bridge_client.py` | HTTP client gửi detection sang PMS backend |
| `edge/config.py` | Thêm `PmsConfig` dataclass + load từ env |
| `edge/main.py` | Wire PmsBridgeClient vào AlertService |
| `edge/services/alert_service.py` | Forward detection sang PMS sau khi gửi mock backend |
| `.env` | Thêm `PMS_BRIDGE_*` config vars |

---

## 3. API Endpoints (PMS Backend)

### `POST /api/v1/vision/detections` — Nhận Detection (multipart/form-data)
```
Fields:
  class_name: string     (e.g., "Anomaly", "SuspiciousBehavior")
  confidence: float      (0.0 - 1.0)
  timestamp:  string     (ISO 8601, e.g., "2026-06-30T09:00:00")
  lat:        float      (GPS latitude)
  lng:        float      (GPS longitude)
  track_id:   int        (ByteTrack ID, optional)
  image:      file       (JPEG evidence, optional)

Response (201):
{
  "success": true,
  "message": "Detection received: Anomaly (confidence: 85.0%) at (10.7769, 106.7009)",
  "recordId": "a1b2c3d4-...",
  "receivedAt": "2026-06-30T02:00:00Z"
}
```

### `POST /api/v1/vision/detections/json` — Nhận Detection (JSON, cho testing)
```json
{
  "className": "Anomaly",
  "confidence": 0.85,
  "timestamp": "2026-06-30T09:00:00",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "trackId": 42,
  "boundingBox": [100, 200, 300, 400],
  "imageName": "evidence.jpg"
}
```

### `GET /api/v1/vision/health` — Health Check
```json
{
  "status": "ok",
  "service": "UavPms Vision Bridge",
  "timestamp": "2026-06-30T02:00:00Z"
}
```

---

## 4. Cấu hình (.env của Vision)

```env
# ── PMS Bridge ───────────────────────────
PMS_BRIDGE_ENABLED=true              # Bật/tắt chuyển tiếp sang PMS
PMS_BRIDGE_URL=http://localhost:5196  # URL PMS backend
PMS_BRIDGE_ENDPOINT=/api/v1/vision/detections
PMS_BRIDGE_TIMEOUT=5                 # Timeout (seconds)
```

---

## 5. Cách Test

### Bước 1: Chạy PMS Backend
```bash
cd /home/minhchau/Documents/PMS
dotnet run --project UavPms.WebApi
```

### Bước 2: Test Health Check
```bash
curl http://localhost:5196/api/v1/vision/health
```

### Bước 3: Test gửi Detection (JSON)
```bash
curl -X POST http://localhost:5196/api/v1/vision/detections/json \
  -H "Content-Type: application/json" \
  -d '{
    "className": "TestAnomaly",
    "confidence": 0.92,
    "timestamp": "2026-06-30T09:00:00",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "trackId": 1,
    "boundingBox": [100, 200, 300, 400]
  }'
```

### Bước 4: Test gửi Detection (multipart - giống Vision Edge)
```bash
curl -X POST http://localhost:5196/api/v1/vision/detections \
  -F "class_name=Anomaly" \
  -F "confidence=0.85" \
  -F "timestamp=2026-06-30T09:00:00" \
  -F "lat=10.7769" \
  -F "lng=106.7009" \
  -F "track_id=42"
```

### Bước 5: Chạy Vision Edge (với PMS Bridge)
```bash
cd /home/minhchau/Documents/Vision-Base-Human-Motion-Detection
# Đảm bảo .env có PMS_BRIDGE_ENABLED=true
python -m edge.main
```

---

## 6. Lưu ý Quan trọng

### Đang Test vs Production
- **Hiện tại**: Vision detect **người** (Human Motion Detection) — dùng test tạm
- **Production**: Vision sẽ detect **dây điện/khuyết tật** (Wire Defect Detection)
- **ClassName mapping** sẽ cần thay đổi khi chuyển sang production:

| Test (hiện tại) | Production (tương lai) |
|-----------------|----------------------|
| `Anomaly` | `Corrosion` |
| `SuspiciousBehavior` | `SurfaceCrack` |
| - | `VegetationEncroachment` |
| - | `MissingComponents` |
| - | `InsulatorDamage` |

### Data Flow Design Decision
- Chọn **HTTP REST** thay vì RabbitMQ vì:
  1. Vision là external Python service, không nằm trong .NET DI container
  2. Đơn giản hơn cho testing và debugging
  3. RabbitMQ đã dùng cho internal events (ImageUploaded → AI Pipeline)
  4. Sẽ cân nhắc chuyển sang RabbitMQ khi scale lên production

### Bảo mật (TODO cho Production)
- [ ] Thêm API Key authentication cho VisionBridge endpoints
- [ ] Cấu hình IP whitelist cho internal service communication
- [ ] Rate limiting cho detection endpoint

---

## 7. Bước Tiếp theo (Roadmap)

1. **Kết nối với Domain Entities**: Khi có Mission + Asset data thật, handler sẽ tạo `DetectedAnomaly` records vào DB
2. **Map DefectCategory**: Tạo mapping từ Vision `class_name` sang `DefectCategory.CategoryCode` trong DB
3. **Emergency Alert**: Tự động tạo `EmergencyAlert` khi detection thuộc loại `is_emergency_class`
4. **Notification**: Gửi notification cho Admin/Manager khi có detection mới
5. **SignalR Push**: Đẩy real-time detection lên frontend dashboard

---

## 8. Liên kết với Roadmap chính

Tích hợp này liên quan đến các Epic trong `roadmap.md`:
- **EPIC 6 (Phase 6.1)**: AI Pipeline & Tích hợp Python - Task #42, #43, #44
- **EPIC 8 (Phase 8.1)**: SignalR & Emergency Alerts - Task #52, #53
- **EPIC 10 (Phase 10.1)**: Notification Service - Task #61, #62

---

## 9. Tích hợp Device Management & Heartbeat (Mới)

### Kiến trúc Luồng Đăng ký & Heartbeat
1. **Đăng ký (Register)**: Khi Edge Service (Python) khởi động, nó tự động gửi thông tin phần cứng (`serial_number`, `software_version`) qua endpoint `POST /api/v1/devices/register`.
   - Nếu thiết bị chưa từng tồn tại: PMS tự động tạo bản ghi UAV với trạng thái `Pending`. Trả về `{"status":"Pending"}`. Edge sẽ thử lại mỗi 10 giây.
   - Khi Admin duyệt thiết bị trên PMS: Lần gọi register tiếp theo sẽ trả về `{"droneId": "UAV001", "deviceToken": "..."}`. Edge lưu `droneId` và chuyển sang bước gửi heartbeat.
2. **Heartbeat định kỳ**: Gửi định kỳ mỗi 10 giây qua endpoint `POST /api/v1/devices/heartbeat`.
   - Cập nhật mức pin và nhiệt độ giả lập từ Edge lên database.
   - Cập nhật trạng thái hoạt động của UAV trong DB thành `Online`.

### Luồng Gắn Kết Detection với Mission Thực Tế
- Khi Edge gửi detection qua `POST /api/v1/vision/detections`:
  1. PMS truy vấn UAV tương ứng bằng `drone_id`.
  2. Truy vấn Mission đang ở trạng thái `Executing` gắn liền với UAV đó.
  3. Lưu metadata ảnh vào bảng `InspectionMedia` (được lưu tại đường dẫn vật lý `uav_storage/images/`).
  4. Phân tích bbox và thông tin track, ánh xạ class khuyết tật sang `DefectCategory`, lưu bản ghi anomaly vào `DetectedAnomaly` gắn liền với Media vừa tạo và Asset ID mặc định (`Guid.Empty`).

### Dữ liệu Khởi Tạo (Seeding)
Để phục vụ việc test liên thông trực tiếp, hệ thống tự động khởi tạo sẵn các dữ liệu sau qua `DatabaseSeeder.cs` của PMS:
1. **User Test**: Tạo user ID `469bfac4-8b96-4f27-a772-945cff2fbaa8` làm người giám sát và bay UAV.
2. **Cấu trúc Asset Mặc định (Guid.Empty)**: Sử dụng raw SQL chèn các bản ghi Region, Substation, TransmissionLine, Tower, và Asset với ID `00000000-0000-0000-0000-000000000000` để làm nơi chứa mặc định cho các media/anomalies mới được upload trước khi được kiểm định viên duyệt gắn vào tài sản thật.
3. **UAV và Mission mẫu**: UAV code `UAV001` và 01 Mission ở trạng thái `Executing` được gán cho user trên để sẵn sàng bắt cặp với dữ liệu gửi về từ Edge.

