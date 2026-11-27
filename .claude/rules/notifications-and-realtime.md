---
paths:
  - "Services/NotificationService/**"
  - "Services/OperationsService/**/Hubs/**"
---

# Quy tắc Notification Matrix, SignalR & Telemetry RAM (BE)

1. `NotificationService`: mỗi hành động nghiệp vụ phát tối đa **1 sự kiện SignalR** theo đúng Ma trận Role (điều phối/giao việc ↔ `Accept`/`Reject` ↔ Check-in ↔ AI xong ↔ Maintenance hai chiều); `NotificationConnectionRegistry` hợp nhất `connectionId` bằng `Distinct` và dọn sạch dynamic group trong `OnDisconnectedAsync`.
2. Hangfire cron jobs đọc toàn bộ ngưỡng từ `SystemSettings` (không hardcode) và kiểm tra cờ `*NotifiedAt` để không spam mỗi phút.
3. `/hubs/telemetry`: chỉ trung chuyển Telemetry thật trên RAM với độ trễ ≤ `10 giây`, gắn `[Authorize]`, **KHÔNG ghi vào bảng `DroneTelemetry`** và không chứa code/script giả lập bay.
