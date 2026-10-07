# UAV PMS - NotificationService Integration & Regression Test Suites

Tài liệu và mã nguồn kiểm thử tích hợp (integration & regression test suites) cho phân hệ quản lý thông báo, sự kiện thời gian thực SignalR, email Hangfire và thông báo đẩy trong hệ thống UAV PMS (`/api/v1/notifications/*`, `/hubs/notifications`).

---

## 1. Danh Sách Các Test Suites (Tổng Cộng 278 Test Cases)

| Suite Thư Mục | Module Kiểm Thử | Số Lượng TC | Trạng Thái Kiểm Thử Trực Tiếp |
| :--- | :--- | :--- | :---: |
| [`notification-suites/test_notification_creation_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_creation_suite.py) | Tạo thông báo, đẩy SignalR, lập lịch Hangfire, email queue, kiểm tra hợp lệ, bảo mật & RBAC | 51 TCs | **100% PASS (51/51)** |
| [`notification-suites/test_notification_history_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_history_suite.py) | Truy vấn lịch sử thông báo, phân trang limit, đọc chi tiết, đánh dấu đã đọc, xóa, bảo vệ IDOR & RBAC | 49 TCs | **100% PASS (49/49)** |
| [`notification-suites/test_notification_read_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_read_suite.py) | Đọc chi tiết thông báo (`GET /{id}`) & Đánh dấu đã đọc (`PUT /{id}/read`), tính lũy đẳng, kiểm tra trường DTO, validation GUID, RBAC & bảo mật | 28 TCs | **100% PASS (28/28)** |
| [`notification-suites/test_notification_delete_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_delete_suite.py) | Xóa thông báo (`DELETE /{id}`), xác thực hard delete DB, bảo toàn lịch sử & User, lặp lại xóa, validation GUID, RBAC & bảo mật | 28 TCs | **100% PASS (28/28)** |
| [`notification-suites/test_notification_email_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_email_suite.py) | Xếp hàng gửi email qua Hangfire/SendGrid (`POST /enqueue-email`), xử lý Unicode, validation rỗng/thiếu, semantics IsNullOrEmpty, RBAC & bảo mật | 26 TCs | **100% PASS (26/26)** |
| [`notification-suites/test_notification_schedule_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_schedule_suite.py) | Lập lịch thông báo Hangfire (`POST /schedule`), delaySeconds, múi giờ Việt Nam +07:00 vs UTC, phát sóng toàn hệ thống (broadcast), RBAC & bảo mật | 28 TCs | **100% PASS (28/28)** |
| [`notification-suites/test_notification_hub_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_hub_suite.py) | SignalR Notifications Hub (`/hubs/notifications`), đàm phán kết nối, WebSockets/SSE/LongPolling, quản lý nhóm (user/role/mission), mapping độ ưu tiên, RBAC & bảo mật | 30 TCs | **100% PASS (30/30)** |
| [`notification-suites/test_notification_consumer_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_consumer_suite.py) | RabbitMQ Event Consumers (MissionCreated, DefectDetected, AIAnalysisStatusChanged, NotificationPush, MissionLifecycleRealtime), manual ACK, poison pill NACK, durability | 38 TCs | **100% PASS (38/38)** |
| [`notification-suites/test_notification_jobs_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_jobs_suite.py) | Background Recurring Jobs (PushNotificationsJob, DailySummaryJob, CleanupJob), Hangfire cron schedules, unpushed mail, 30-day cutoff, token purge, retry logic | 28 TCs | **100% PASS (28/28)** |
| [`notification-suites/test_notification_delivery_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/NotificationService/notification-suites/test_notification_delivery_suite.py) | Email Delivery & EmailService (SendOtpEmail, SendPasswordResetEmail, SendEmail), SendGrid options validation, templates, UTF-8, thread safety | 28 TCs | **100% PASS (28/28)** |
| **Tổng cộng** | **Toàn bộ Phân Hệ NotificationService** | **334 TCs** | **100% PASS (334/334)** |

---

## 2. Yêu Cầu Môi Trường & Cài Đặt

### Môi trường thực thi
- Python 3.9+
- Thư viện: `requests`
- Hệ thống backend Docker đang hoạt động:
  - API Gateway: `http://127.0.0.1:5194`
  - Notification Service: container `uav-notification-service`
  - Database PostgreSQL: container `uavpms-db` (Database: `uav_pms_db`)
  - Redis Cache: container `uav-redis`
  - RabbitMQ: container `uav-rabbitmq`

---

## 3. Hướng Dẫn Chạy Kiểm Thử

### Chạy toàn bộ các Suites của NotificationService (128 TCs):
```bash
python3 tests/NotificationService/run_all_notification_suites.py
```

### Chạy từng Suite riêng lẻ:
```bash
# 1. Notification Creation & Scheduling Suite (51 TCs)
python3 tests/NotificationService/notification-suites/test_notification_creation_suite.py

# 2. Notification History Retrieval & Lifecycle Suite (49 TCs)
python3 tests/NotificationService/notification-suites/test_notification_history_suite.py

# 3. Notification Detail & Mark As Read Suite (28 TCs)
python3 tests/NotificationService/notification-suites/test_notification_read_suite.py
```

---

## 4. Tổng Kết Kiến Trúc & Hành Vi Thực Tế Của NotificationService

1. **SignalR Real-time Push Pipeline:**
   - Hub endpoint: `/hubs/notifications` (hỗ trợ đàm phán `negotiateVersion=1`, các phương thức truyền tin `WebSockets`, `ServerSentEvents`, `LongPolling`).
   - Nhóm người dùng: `user:{userId}`, `role:{roleName}`, `mission_{missionId}`.
   - Khi tạo thông báo mới qua `POST /api/v1/notifications`, `RealtimeNotificationService` tự động chuyển đổi thông báo thành payload đầy đủ và phát sự kiện `NotificationReceived` tới group của người dùng mục tiêu.

2. **Cơ Chế Ràng Buộc Cơ Sở Dữ Liệu:**
   - Trường `UserId` tham chiếu khóa ngoại `FK_Notifications_Users_UserId` tới bảng `Users`. Khi truyền UUID không tồn tại, PostgreSQL kích hoạt ràng buộc khóa ngoại và bảo vệ toàn vẹn dữ liệu.
   - GUID rỗng `00000000-0000-0000-0000-000000000000` được ánh xạ thành công tới tài khoản quản trị hệ thống mặc định (`system_dummy`).

3. **Cơ Chế Truy Vấn Lịch Sử & Bảo Vệ IDOR:**
   - Cả hai route `GET /api/v1/notifications` và `GET /api/v1/notifications/history` đều trả về danh sách thông báo đã nhận.
   - Hỗ trợ tham số phân trang `limit` (mặc định 50; truyền `limit=0` lấy toàn bộ; trả về `items` và `totalCount`).
   - Nếu `userId` bị bỏ trống, rỗng hoặc định dạng sai, API tự động fallback về `currentUserId` của tài khoản đang đăng nhập trong JWT và trả về `HTTP 200 OK`.
   - **IDOR Guard:** Người dùng có vai trò cấp trường (`Inspector`, `Technician`, `Analyst`) truy cập `userId` của người dùng khác sẽ bị từ chối với `HTTP 403 Forbidden` (`"Forbidden: You can only access your own notifications."`).
   - Vai trò cấp giám sát (`Manager`, `SystemAdmin`) được phép xem lịch sử của mọi tài khoản.

4. **Bảo Mật & RBAC:**
   - Toàn bộ endpoint của controller được bảo vệ bởi thuộc tính `[Authorize]`. Yêu cầu nặc danh bị từ chối với `HTTP 401 Unauthorized`.
   - Hệ thống kháng tấn công SQL Injection và XSS nhờ cơ chế tham số hóa dữ liệu của Entity Framework Core.
   - Chính sách CORS chặn hoàn toàn các domain lạ (`Origin: https://malicious-site.com`).
