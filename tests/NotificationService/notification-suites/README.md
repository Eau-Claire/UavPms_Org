# NotificationService: Integration & Regression Test Suites

## Tổng Quan
Bộ kiểm thử tích hợp và kiểm thử hồi quy (integration & regression test suite) cho toàn bộ phân hệ **NotificationService** (`/api/v1/notifications/*`, `/hubs/notifications`).

---

## Danh Sách Các Suites

### 1. Notification Creation & Scheduling Suite (`test_notification_creation_suite.py`) - 51 Test Cases
- **Function A: Successful Creation & Scheduling (Happy Path - 19 TCs)**:
  - `NOTIF_CREATE_001` -> `NOTIF_CREATE_019`: Tạo thông báo hợp lệ, optional fields, SignalR real-time event `NotificationReceived`, 6 types, UTF-8 tiếng Việt, `sentAt` UTC, persistence, RBAC (Manager, Inspector, Analyst, Technician), enqueue Hangfire email, lập lịch Hangfire với delay và thời gian cụ thể.
- **Function B: Validation & Boundary Failures (21 TCs)**:
  - `NOTIF_CREATE_020` -> `NOTIF_CREATE_040`: Kiểm tra missing/invalid `userId`, rỗng hoặc null cho `title`/`body`/`type`, payload rỗng `{}`, boundary payload (600 ký tự title, 5000 ký tự body, 150 ký tự type), unsupported media type 415, malformed JSON 400, whitespace-only fields, schedule thiếu params, delay âm hoặc 0, schedule time trong quá khứ.
- **Function C: Authorization (RBAC) & Security (11 TCs)**:
  - `NOTIF_CREATE_RBAC_001` -> `NOTIF_CREATE_RBAC_011`: Từ chối Anonymous 401, expired JWT 401, invalid signature 401, SQLi, XSS, Command Injection, Payload DoS (2MB+), CORS domain lạ, concurrency 10 requests, email/schedule unauthenticated 401.

### 2. Notification History Retrieval & Lifecycle Suite (`test_notification_history_suite.py`) - 49 Test Cases
- **Function A: Successful History & Operations (Happy Path - 22 TCs)**:
  - `NOTIF_HISTORY_001`: Lấy lịch sử thông báo cho userId hợp lệ (HTTP 200).
  - `NOTIF_HISTORY_002`: Trả về mảng rỗng (`items: []`, `totalCount: 0`) khi user chưa có thông báo.
  - `NOTIF_HISTORY_003`: Trả về cả thông báo đã đọc (`isRead: true`) và chưa đọc (`isRead: false`).
  - `NOTIF_HISTORY_004`: Trả về thông báo thuộc các loại khác nhau (`General`, `MissionAssigned`, `DefectDetected`, `EmergencyAlert`, `SystemNotice`).
  - `NOTIF_HISTORY_005`: Loại trừ các thông báo đã bị xóa khỏi kết quả lịch sử.
  - `NOTIF_HISTORY_006`: Kết quả được sắp xếp theo thời gian gửi giảm dần `sentAt` (mới nhất lên đầu).
  - `NOTIF_HISTORY_007`: Trả về chính xác số lượng items và metadata `totalCount` khi có nhiều thông báo.
  - `NOTIF_HISTORY_008`: Cô lập dữ liệu triệt để theo `userId`, không rò rỉ thông báo giữa các người dùng.
  - `NOTIF_HISTORY_009`: `SystemAdmin` có quyền truy vấn lịch sử thông báo của mọi người dùng.
  - `NOTIF_HISTORY_010`: `Manager` có quyền truy vấn lịch sử thông báo của mọi người dùng.
  - `NOTIF_HISTORY_011`: `Inspector` truy vấn thành công lịch sử của chính mình.
  - `NOTIF_HISTORY_012`: `Analyst` truy vấn thành công lịch sử của chính mình.
  - `NOTIF_HISTORY_013`: `Technician` truy vấn thành công lịch sử của chính mình.
  - `NOTIF_HISTORY_014`: Kiểm tra cấu trúc `NotificationDto` với đầy đủ các trường dữ liệu.
  - `NOTIF_HISTORY_015`: Lấy lịch sử cho người dùng ở trạng thái Inactive/Pending an toàn.
  - `NOTIF_HISTORY_016`: Route alias gốc `GET /api/v1/notifications` hoạt động tương đương `/history`.
  - `NOTIF_HISTORY_017`: Lọc phân trang với `?limit=10` trả về tối đa 10 items, bảo toàn `totalCount`.
  - `NOTIF_HISTORY_018`: Lấy toàn bộ danh sách với `?limit=0` không bị cắt bớt phân trang.
  - `NOTIF_HISTORY_019`: Đọc chi tiết thông báo đơn lẻ qua `GET /api/v1/notifications/{id}`.
  - `NOTIF_HISTORY_020`: Đánh dấu đã đọc thành công qua `PUT /api/v1/notifications/{id}/read`.
  - `NOTIF_HISTORY_021`: Tính lũy đẳng (idempotent) khi gọi `PUT /{id}/read` nhiều lần.
  - `NOTIF_HISTORY_022`: Xóa thông báo thành công qua `DELETE /api/v1/notifications/{id}`.
- **Function B: Failed History Retrieval & Boundaries (15 TCs)**:
  - `NOTIF_HISTORY_023`: Bỏ trống query param `userId` (tự động fallback về thông báo của chính caller hoặc 400).
  - `NOTIF_HISTORY_024`: Truyền `userId` là chuỗi rỗng (`?userId=`) (fallback an toàn hoặc 400).
  - `NOTIF_HISTORY_025`: Truyền `userId` không phải UUID (`?userId=invalid-uuid`) (fallback an toàn hoặc 400).
  - `NOTIF_HISTORY_026`: Vô hiệu hóa mã XSS script trong `userId` an toàn.
  - `NOTIF_HISTORY_027`: Vô hiệu hóa mã SQL Injection trong `userId` an toàn qua parameterized queries.
  - `NOTIF_HISTORY_028`: Xử lý an toàn khi `userId` có khoảng trắng thừa (`?userId= <uuid> `).
  - `NOTIF_HISTORY_029`: Bỏ qua các query parameter không xác định (`?extraParam=test&foo=bar`).
  - `NOTIF_HISTORY_030`: Phân tích chính xác UUID định dạng chữ hoa (`?userId=A1B2C3D4-...`).
  - `NOTIF_HISTORY_031`: Xử lý an toàn tham số phân trang âm (`?limit=-1`).
  - `NOTIF_HISTORY_032`: Đọc chi tiết thông báo với UUID không tồn tại trả về `HTTP 404 Not Found`.
  - `NOTIF_HISTORY_033`: Đọc chi tiết thông báo với route không phải GUID bị chặn bởi route constraint (HTTP 404).
  - `NOTIF_HISTORY_034`: Đánh dấu đã đọc thông báo không tồn tại trả về `HTTP 404 Not Found`.
  - `NOTIF_HISTORY_035`: Đánh dấu đã đọc với route không phải GUID bị chặn (HTTP 404).
  - `NOTIF_HISTORY_036`: Xóa thông báo không tồn tại trả về `HTTP 404 Not Found`.
  - `NOTIF_HISTORY_037`: Xóa thông báo với route không phải GUID bị chặn (HTTP 404).
- **Function C: Authorization (RBAC), IDOR & Security (12 TCs)**:
  - `NOTIF_HISTORY_RBAC_001`: Từ chối yêu cầu nặc danh tới `/history` với `HTTP 401 Unauthorized`.
  - `NOTIF_HISTORY_RBAC_002`: Từ chối token JWT hết hạn với `HTTP 401 Unauthorized`.
  - `NOTIF_HISTORY_RBAC_003`: Xác nhận phản hồi lỗi không làm lộ stack trace hay thông tin nhạy cảm.
  - `NOTIF_HISTORY_RBAC_004`: Xử lý 20 yêu cầu đọc lịch sử đồng thời không xảy ra race condition.
  - `NOTIF_HISTORY_RBAC_005`: **Bảo vệ IDOR**: `Inspector` truy cập thông báo của `Manager` bị từ chối với `HTTP 403 Forbidden`.
  - `NOTIF_HISTORY_RBAC_006`: **Bảo vệ IDOR**: `Technician` truy cập thông báo của `Inspector` bị từ chối với `HTTP 403 Forbidden`.
  - `NOTIF_HISTORY_RBAC_007`: **Bảo vệ IDOR**: `Analyst` truy cập thông báo của `Manager` bị từ chối với `HTTP 403 Forbidden`.
  - `NOTIF_HISTORY_RBAC_008`: Quyền giám sát: `SystemAdmin` xem được thông báo của mọi người dùng mà không bị 403.
  - `NOTIF_HISTORY_RBAC_009`: Quyền giám sát: `Manager` xem được thông báo của mọi người dùng mà không bị 403.
  - `NOTIF_HISTORY_RBAC_010`: Từ chối yêu cầu nặc danh tới `GET /{id}` với `HTTP 401 Unauthorized`.
  - `NOTIF_HISTORY_RBAC_011`: Từ chối yêu cầu nặc danh tới `PUT /{id}/read` với `HTTP 401 Unauthorized`.
  - `NOTIF_HISTORY_RBAC_012`: Từ chối yêu cầu nặc danh tới `DELETE /{id}` với `HTTP 401 Unauthorized`.

### 3. Notification Detail & Mark As Read Suite (`test_notification_read_suite.py`) - 28 Test Cases
- **Function A: Successful Get By ID & Mark As Read (Happy Path - 13 TCs)**:
  - `NOTIF_READ_001`: Truy xuất chi tiết thông báo theo ID hợp lệ (HTTP 200).
  - `NOTIF_READ_002`: Đánh dấu đã đọc thành công qua `PUT /api/v1/notifications/{id}/read`.
  - `NOTIF_READ_003`: Tính lũy đẳng (idempotent) khi đánh dấu đã đọc nhiều lần trên cùng một ID.
  - `NOTIF_READ_004`: Cập nhật `IsRead = true` chính xác trong cơ sở dữ liệu PostgreSQL.
  - `NOTIF_READ_005`: Đánh dấu đã đọc bảo toàn toàn bộ các trường khác (`title`, `body`, `type`, `userId`, `sentAt`).
  - `NOTIF_READ_006`: `GET /{id}` trả về `NotificationDto` sạch, ẩn hoàn toàn các trường nội bộ (`isPushed`, `pushedAt`).
  - `NOTIF_READ_007`: Ánh xạ chính xác cả 6 giá trị enum `NotificationType` (`General`, `MissionAssigned`, `DefectDetected`, `EmergencyAlert`, `TicketAssigned`, `EscalationRequest`).
  - `NOTIF_READ_008`: Không phân biệt hoa/thường (case-insensitive) đối với tham số GUID trên URL.
  - `NOTIF_READ_009` -> `NOTIF_READ_013`: Quyền hạn đọc và đánh dấu đã đọc cho mọi vai trò hợp lệ: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- **Function B: Failed Get By ID & Mark As Read — Validation & Boundaries (8 TCs)**:
  - `NOTIF_READ_014`: `GET` với GUID rỗng `00000000-0000-0000-0000-000000000000` trả về `HTTP 404 Not Found`.
  - `NOTIF_READ_015`: `GET` với chuỗi không phải GUID bị chặn bởi route constraint `{id:guid}` (HTTP 404).
  - `NOTIF_READ_016`: `PUT /read` với GUID rỗng trả về `HTTP 404 Not Found`.
  - `NOTIF_READ_017`: `PUT /read` với chuỗi không phải GUID bị chặn bởi route constraint `{id:guid}` (HTTP 404).
  - `NOTIF_READ_018`: `GET` với GUID ngẫu nhiên không tồn tại trả về `HTTP 404 Not Found`.
  - `NOTIF_READ_019`: `PUT /read` với GUID ngẫu nhiên không tồn tại trả về `HTTP 404 Not Found`.
  - `NOTIF_READ_020`: Xử lý 5 yêu cầu `PUT /read` đồng thời trên cùng một thông báo không bị xung đột khóa dữ liệu.
  - `NOTIF_READ_021`: Phương thức HTTP không được hỗ trợ (`POST /{id}`) trả về `HTTP 405 Method Not Allowed`.
- **Function C: Authorization (RBAC) & Security (7 TCs)**:
  - `NOTIF_READ_RBAC_001`: Yêu cầu nặc danh tới `GET /{id}` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_READ_RBAC_002`: Yêu cầu nặc danh tới `PUT /{id}/read` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_READ_RBAC_003`: Token JWT hết hạn tới `GET /{id}` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_READ_RBAC_004`: Token JWT hết hạn tới `PUT /{id}/read` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_READ_RBAC_005`: Kháng tấn công SQL Injection trên tham số route GUID.
  - `NOTIF_READ_RBAC_006`: Thông điệp lỗi an toàn, không rò rỉ stack trace hay cấu trúc CSDL nội bộ.
  - `NOTIF_READ_RBAC_007`: Dữ liệu chứa mã XSS script được lưu trữ và trả về an toàn dưới dạng chuỗi JSON escape.

### 4. Notification Deletion Suite (`test_notification_delete_suite.py`) - 28 Test Cases
- **Function A: Successful Deletion (Happy Path - 15 TCs)**:
  - `NOTIF_DELETE_001`: Xóa thông báo hợp lệ (`DELETE /{id}`), xác nhận HTTP 200, phản hồi `Notification deleted successfully.`, sau đó GET trả lời HTTP 404 và database count = 0.
  - `NOTIF_DELETE_002`: Thông báo đã xóa không còn xuất hiện trong danh sách lịch sử `GET /history?userId=...`.
  - `NOTIF_DELETE_003`: Xóa thông báo chưa đọc (`isRead = false`) thành công không bị ràng buộc.
  - `NOTIF_DELETE_004`: Xóa thông báo đã đọc (`isRead = true`) thành công.
  - `NOTIF_DELETE_005`: Xóa thông báo đã đẩy (`IsPushed = true`) thành công khỏi database.
  - `NOTIF_DELETE_006`: Xác nhận cơ chế Hard Delete (hàng trong bảng `Notifications` bị xóa vật lý, thực thể không dùng soft-delete).
  - `NOTIF_DELETE_007`: Xóa thông báo A không làm ảnh hưởng đến thông báo B của cùng một người dùng.
  - `NOTIF_DELETE_008`: Tham số route GUID không phân biệt hoa/thường khi gọi `DELETE`.
  - `NOTIF_DELETE_009`: Xóa thành công thông báo thuộc tất cả 6 loại enum `NotificationType`.
  - `NOTIF_DELETE_010`: Xóa thông báo bảo toàn thực thể `User` liên kết, không cascade xóa người dùng.
  - `NOTIF_DELETE_011` -> `NOTIF_DELETE_015`: Quyền hạn xóa thành công cho mọi vai trò: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- **Function B: Failed Deletion — Validation & Boundaries (6 TCs)**:
  - `NOTIF_DELETE_016`: Xóa với GUID rỗng `00000000-0000-0000-0000-000000000000` trả về `HTTP 404 Not Found`.
  - `NOTIF_DELETE_017`: Xóa với chuỗi không phải GUID bị chặn bởi route constraint `{id:guid}` (HTTP 404).
  - `NOTIF_DELETE_018`: Xóa lặp lại cùng một ID lần 2 trả về `HTTP 404 Not Found` (`NotFoundException`).
  - `NOTIF_DELETE_019`: Xóa với GUID ngẫu nhiên không tồn tại trong CSDL trả về `HTTP 404 Not Found`.
  - `NOTIF_DELETE_020`: Gửi nhiều yêu cầu xóa liên tiếp nhanh chóng: yêu cầu đầu tiên trả về HTTP 200, các yêu cầu sau trả về HTTP 404 an toàn.
  - `NOTIF_DELETE_021`: Xóa với GUID chứa khoảng trắng thừa bị chặn bởi route constraint (HTTP 404).
- **Function C: Authorization (RBAC) & Security (7 TCs)**:
  - `NOTIF_DELETE_RBAC_001`: Yêu cầu nặc danh tới `DELETE /{id}` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_DELETE_RBAC_002`: Token JWT hết hạn tới `DELETE /{id}` bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_DELETE_RBAC_003`: Kháng tấn công SQL Injection trên tham số route GUID khi gọi DELETE.
  - `NOTIF_DELETE_RBAC_004`: Kháng tấn công XSS Script trong tham số route GUID khi gọi DELETE.
  - `NOTIF_DELETE_RBAC_005`: Kháng tấn công Command Injection trong tham số route GUID.
  - `NOTIF_DELETE_RBAC_006`: Kháng tấn công Path Traversal trong tham số route GUID.
  - `NOTIF_DELETE_RBAC_007`: Thông điệp lỗi an toàn, không rò rỉ stack trace hay chuỗi kết nối máy chủ nội bộ.

### 5. Notification Email Enqueue Suite (`test_notification_email_suite.py`) - 26 Test Cases
- **Function A: Successful Email Enqueue (Happy Path - 11 TCs)**:
  - `NOTIF_EMAIL_001`: Xếp hàng gửi email hợp lệ (`POST /enqueue-email`), trả về HTTP 200, phản hồi `Email job successfully enqueued in Hangfire.`, trả về `data.jobId` hợp lệ.
  - `NOTIF_EMAIL_002`: Enqueue thành công vào pipeline Hangfire BackgroundJob.
  - `NOTIF_EMAIL_003`: Xử lý mượt mà tiêu đề và nội dung tiếng Việt có dấu / ký tự Unicode UTF-8 (`⚠️ Cảnh báo khẩn cấp...`).
  - `NOTIF_EMAIL_004`: Enqueue nhiều email liên tiếp tạo ra các Hangfire JobId duy nhất độc lập.
  - `NOTIF_EMAIL_005`: Kiểm tra định dạng JobId trả về là định danh số nguyên dương hợp lệ.
  - `NOTIF_EMAIL_006`: Cấu trúc phản hồi tuân thủ hợp đồng `ApiResponse` (`success`, `message`, `data`, `errors`).
  - `NOTIF_EMAIL_007` -> `NOTIF_EMAIL_011`: Quyền hạn enqueue email cho mọi vai trò: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- **Function B: Failed Email Enqueue — Validation & Boundaries (10 TCs)**:
  - `NOTIF_EMAIL_012`: `email` là chuỗi rỗng `""` bị từ chối với `HTTP 400 Bad Request` ("Email, Subject, and Body are required.").
  - `NOTIF_EMAIL_013`: `subject` là chuỗi rỗng `""` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_EMAIL_014`: `body` là chuỗi rỗng `""` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_EMAIL_015`: Request rỗng `{}` bị từ chối với `HTTP 400 Bad Request` (ProblemDetails validation).
  - `NOTIF_EMAIL_016`: Thiếu trường `email` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_EMAIL_017`: Thiếu trường `subject` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_EMAIL_018`: Thiếu trường `body` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_EMAIL_019`: Email không đúng định dạng chuẩn (`not-an-email`) được enqueue thành công với HTTP 200 (xác thực email xảy ra tại worker).
  - `NOTIF_EMAIL_020`: Trường chỉ chứa khoảng trắng được xử lý theo ngữ nghĩa `string.IsNullOrEmpty` (không phải `IsNullOrWhiteSpace`), được enqueue với HTTP 200.
  - `NOTIF_EMAIL_021`: `Content-Type` không phải `application/json` (e.g. `text/plain`) trả về `HTTP 415 Unsupported Media Type`.
- **Function C: Authorization (RBAC) & Security (5 TCs)**:
  - `NOTIF_EMAIL_RBAC_001`: Yêu cầu nặc danh bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_EMAIL_RBAC_002`: Token JWT hết hạn bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_EMAIL_RBAC_003`: XSS payload trong nội dung email được xử lý an toàn dưới dạng chuỗi thuần.
  - `NOTIF_EMAIL_RBAC_004`: SQL Injection trong email/subject/body được trung hòa, không thực thi truy vấn SQL trực tiếp.
  - `NOTIF_EMAIL_RBAC_005`: Phương thức HTTP không được hỗ trợ (`GET /enqueue-email`) trả về `HTTP 405 Method Not Allowed`.

### 6. Notification Schedule Suite (`test_notification_schedule_suite.py`) - 28 Test Cases
- **Function A: Successful Schedule Notification (Happy Path - 15 TCs)**:
  - `NOTIF_SCHED_001`: Lập lịch thông báo hợp lệ với `delaySeconds` nhắm tới người dùng cụ thể (`userId`), trả về HTTP 200, phản hồi thông báo và `Target: User: <userId>`.
  - `NOTIF_SCHED_002`: Lập lịch thông báo hợp lệ với `scheduleTime` tương lai nhắm tới người dùng cụ thể.
  - `NOTIF_SCHED_003`: Lập lịch thông báo phát sóng (broadcast) toàn hệ thống (`userId = null`), xác nhận `Target: ALL Users`.
  - `NOTIF_SCHED_004`: Xác nhận Hangfire JobId trả về là định danh số nguyên dương hợp lệ.
  - `NOTIF_SCHED_005`: Mặc định loại thông báo thành `ScheduledNotification` khi `type` là null.
  - `NOTIF_SCHED_006`: Xử lý tiêu đề và nội dung tiếng Việt có dấu Unicode UTF-8 (`📊 Báo cáo...`).
  - `NOTIF_SCHED_007`: `scheduleTime` không có thông tin múi giờ (`Unspecified`) tự động gán múi giờ Việt Nam (`+07:00`).
  - `NOTIF_SCHED_008`: `scheduleTime` định dạng UTC rõ ràng (`Z`) được bảo toàn múi giờ UTC (`+00:00`).
  - `NOTIF_SCHED_009`: Khi cung cấp cả `scheduleTime` và `delaySeconds`, `scheduleTime` được ưu tiên xử lý trước.
  - `NOTIF_SCHED_010`: Hỗ trợ lập lịch với khoảng trễ lớn (`delaySeconds = 86400` tương đương 24 giờ).
  - `NOTIF_SCHED_011` -> `NOTIF_SCHED_015`: Quyền hạn lập lịch thành công cho mọi vai trò: `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`.
- **Function B: Failed Schedule — Validation & Boundaries (8 TCs)**:
  - `NOTIF_SCHED_016`: `title` rỗng `""` bị từ chối với `HTTP 400 Bad Request` ("Title and Body are required.").
  - `NOTIF_SCHED_017`: `body` rỗng `""` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_018`: Thiếu trường `title` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_019`: Thiếu trường `body` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_020`: Thiếu cả `delaySeconds` và `scheduleTime` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_021`: `delaySeconds = 0` bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_022`: `delaySeconds` âm (`-30`) bị từ chối với `HTTP 400 Bad Request`.
  - `NOTIF_SCHED_023`: `scheduleTime` trong quá khứ bị từ chối với `HTTP 400 Bad Request` (kèm thời gian hiện tại).
- **Function C: Authorization (RBAC) & Security (5 TCs)**:
  - `NOTIF_SCHED_RBAC_001`: Yêu cầu nặc danh bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_SCHED_RBAC_002`: Token JWT hết hạn bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_SCHED_RBAC_003`: XSS payload trong title/body được lưu trữ an toàn dưới dạng chuỗi thuần.
  - `NOTIF_SCHED_RBAC_004`: SQL Injection trong title/body được trung hòa, không thực thi truy vấn SQL trực tiếp.
  - `NOTIF_SCHED_RBAC_005`: Phương thức HTTP không được hỗ trợ (`GET /schedule`) trả về `HTTP 405 Method Not Allowed`.

### 7. SignalR Notifications Hub Suite (`test_notification_hub_suite.py`) - 30 Test Cases
- **Function A: Successful Hub Connection & Real-time Events (Happy Path - 24 TCs)**:
  - `NOTIF_HUB_001`: Người dùng đã xác thực đàm phán (`POST /hubs/notifications/negotiate?negotiateVersion=1`) và kết nối thành công, nhận `connectionToken` và danh sách giao thức hỗ trợ (`WebSockets`, `ServerSentEvents`, `LongPolling`).
  - `NOTIF_HUB_002`: Người dùng có vai trò Manager được tự động thêm vào nhóm `user:{userId}` và `role:Manager`.
  - `NOTIF_HUB_003`: Người dùng đa vai trò (e.g. Manager + Analyst) được tự động gán vào tất cả các nhóm vai trò tương ứng.
  - `NOTIF_HUB_004`: Kích hoạt pipeline sự kiện `NotificationReceived` khi một thông báo mới được tạo qua REST API.
  - `NOTIF_HUB_005`: Payload sự kiện `NotificationReceived` có `priority = "High"` đối với thông báo loại `CriticalAlert` (thông qua `ResolvePriority`).
  - `NOTIF_HUB_006`: Payload sự kiện `NotificationReceived` có `priority = "High"` đối với thông báo loại `EmergencyAlert`.
  - `NOTIF_HUB_007`: Payload sự kiện `NotificationReceived` có `priority = "Normal"` đối với thông báo loại `General`.
  - `NOTIF_HUB_008`: Hợp đồng sự kiện `AiAnalysisStatusChanged` được định nghĩa chuẩn trong `RealtimeNotificationEvents.AiAnalysisStatusChanged`.
  - `NOTIF_HUB_009`: Các client kết nối nhận sự kiện phân lập theo nhóm `user:{userId}`, không bị rò rỉ chéo sang người dùng khác.
  - `NOTIF_HUB_010`: Vòng đời ngắt kết nối client được xử lý nhẹ nhàng trong `OnDisconnectedAsync`, dọn dẹp kết nối khỏi nhóm an toàn.
  - `NOTIF_HUB_011`: Cơ chế ghi log cảnh báo khi client ngắt kết nối bất thường kèm thông tin ngoại lệ (`User disconnected with error`).
  - `NOTIF_HUB_012`: Người dùng kết nối lại thành công sau khi ngắt kết nối và được cấp `connectionToken` mới.
  - `NOTIF_HUB_013`: Quản trị viên `SystemAdmin` kết nối và được thêm vào nhóm `role:SystemAdmin`.
  - `NOTIF_HUB_014`: Giám định viên `Inspector` kết nối và được thêm vào nhóm `role:Inspector`.
  - `NOTIF_HUB_015`: Chuyên viên phân tích `Analyst` kết nối và được thêm vào nhóm `role:Analyst`.
  - `NOTIF_HUB_016`: Kỹ thuật viên `Technician` kết nối và được thêm vào nhóm `role:Technician`.
  - `NOTIF_HUB_017`: Payload sự kiện có `priority = "Normal"` đối với thông báo loại `MissionAssigned`.
  - `NOTIF_HUB_018`: Payload sự kiện có `priority = "Normal"` đối với thông báo loại `DefectDetected`.
  - `NOTIF_HUB_019`: Payload sự kiện có `priority = "Normal"` đối với thông báo loại `TicketAssigned`.
  - `NOTIF_HUB_020`: Một tài khoản mở nhiều kết nối đồng thời từ các thiết bị khác nhau đều thuộc cùng nhóm `user:{userId}` và cùng nhận được thông báo đẩy.
  - `NOTIF_HUB_021`: Các sự kiện SignalR được điều phối theo đúng thứ tự thời gian tạo lập tuần tự.
  - `NOTIF_HUB_022`: Hub xử lý đồng thời 20+ kết nối đàm phán một lúc mà không phát sinh lỗi tài nguyên hay crash server.
  - `NOTIF_HUB_023`: Khi người dùng đang ngoại tuyến (offline, chưa mở WebSocket), thông báo vẫn được lưu trữ an toàn trong DB PostgreSQL và không làm gián đoạn hệ thống.
  - `NOTIF_HUB_024`: Xác thực định dạng tên nhóm phòng nhiệm vụ `mission_{missionId}` (`NotificationHub.MissionGroupName`).
- **Function B: Failed Hub Connection — Authentication & Security (6 TCs)**:
  - `NOTIF_HUB_025`: Yêu cầu kết nối nặc danh không có token JWT bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_HUB_026`: Kết nối với token JWT đã hết hạn bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_HUB_027`: Kết nối với token JWT bị sửa đổi chữ ký hoặc claims bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_HUB_028`: Tham số chuỗi truy vấn `access_token` không hợp lệ bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_HUB_029`: Kết nối với token JWT được ký bởi khóa bí mật giả mạo bị từ chối với `HTTP 401 Unauthorized`.
  - `NOTIF_HUB_030`: Yêu cầu HTTP không được hỗ trợ (e.g. `DELETE /hubs/notifications/negotiate`) bị từ chối với `HTTP 404` hoặc `HTTP 405`.

### 8. RabbitMQ Event Consumers Suite (`test_notification_consumer_suite.py`) - 38 Test Cases
- **Function A: MissionCreatedConsumer (7 TCs)**:
  - `NOTIF_MQ_001`: Sự kiện `MissionCreatedEvent` tạo thông báo cho Inspector được phân công (`MissionAssigned`, `referenceType: Mission`, `Nhiệm vụ mới được giao`, body chứa mã nhiệm vụ và mô tả).
  - `NOTIF_MQ_002`: Sự kiện `MissionCreatedEvent` tạo thông báo riêng cho Manager khi `ManagerId != AssignedToUserId` (`MissionCreated`, `Nhiệm vụ mới đã tạo`).
  - `NOTIF_MQ_003`: Tránh trùng lặp thông báo khi `ManagerId == AssignedToUserId` (chỉ tạo 1 thông báo `MissionAssigned`).
  - `NOTIF_MQ_004`: Bỏ qua tạo thông báo Manager khi `ManagerId = Guid.Empty`.
  - `NOTIF_MQ_005`: Sự kiện `MissionCreatedFromAssessmentEvent` tạo thông báo cho Manager (`MissionCreated`, `Nhiệm vụ mới đã tạo từ thẩm định`).
  - `NOTIF_MQ_006`: Bỏ qua tạo thông báo Manager khi `MissionCreatedFromAssessmentEvent` có `ManagerId = Guid.Empty`.
  - `NOTIF_MQ_007`: Bắt lỗi JSON hỏng cú pháp, ghi log lỗi và NACK không đẩy lại hàng đợi (`requeue = false`), consumer duy trì trạng thái ổn định cho các thông điệp tiếp theo.
- **Function B: DefectDetectedConsumer (5 TCs)**:
  - `NOTIF_MQ_008`: Sự kiện `DefectDetectedEvent` với `IsDefect = true` tạo cảnh báo `CriticalAlert` cho tất cả SystemAdmin và Manager (`DetectedAnomaly`, `⚠️ Phát hiện khuyết tật nghiêm trọng`).
  - `NOTIF_MQ_009`: Sự kiện với `IsDefect = false` không tạo bất kỳ thông báo nào, ACK ngay lập tức.
  - `NOTIF_MQ_010`: Khử trùng lặp (`DistinctBy`) đối với người dùng nắm giữ đồng thời nhiều vai trò (SystemAdmin + Manager) chỉ nhận đúng 1 thông báo.
  - `NOTIF_MQ_011`: Hoàn tất truy vấn và gửi thông báo an toàn ngay cả khi danh sách người dùng quản trị thay đổi.
  - `NOTIF_MQ_012`: Bắt lỗi JSON hỏng cú pháp trên hàng đợi `notification.defect-detected`, NACK an toàn không làm tắc nghẽn queue.
- **Function C: AIAnalysisStatusChangedConsumer (5 TCs)**:
  - `NOTIF_MQ_013`: Sự kiện `AIAnalysisStatusChangedEvent` chuyển tiếp qua SignalR tới nhóm người dùng `user:{userId}` (`AiAnalysisStatusChanged`).
  - `NOTIF_MQ_014`: Bỏ qua phát sóng SignalR an toàn khi `UserId = Guid.Empty`.
  - `NOTIF_MQ_015`: Điều phối chính xác các trạng thái phân tích AI (`Queued`, `Processing`, `Completed`, `Failed`).
  - `NOTIF_MQ_016`: Chuẩn hóa an toàn trường `MediaType` và `Status` bị null thành chuỗi rỗng `""` tránh lỗi null reference.
  - `NOTIF_MQ_017`: NACK không requeue khi nhận JSON hỏng cú pháp trên hàng đợi `notification.ai-analysis-status-changed`.
- **Function D: NotificationPushConsumer (6 TCs)**:
  - `NOTIF_MQ_018`: Sự kiện `NotificationPushEvent` tạo bản ghi thông báo mới trong DB với `IsPushed = true`, `PushedAt` gán timestamp UTC hiện tại.
  - `NOTIF_MQ_019`: Tái sử dụng bản ghi thông báo có sẵn nếu `NotificationId` đã tồn tại, tránh ghi đè hoặc nhân bản trùng lặp trong DB.
  - `NOTIF_MQ_020`: Bỏ qua không tạo bản ghi khi `UserId = Guid.Empty`.
  - `NOTIF_MQ_021`: Tự động sinh UUID mới hợp lệ khi `NotificationId = Guid.Empty` và lưu thành công vào DB.
  - `NOTIF_MQ_022`: Lưu trữ an toàn các tham chiếu nghiệp vụ tùy biến `ReferenceType` và `ReferenceId`.
  - `NOTIF_MQ_023`: NACK không requeue khi nhận JSON hỏng cú pháp trên hàng đợi `notification.push-notification`.
- **Function E: MissionLifecycleRealtimeConsumer (6 TCs)**:
  - `NOTIF_MQ_024`: Tập hợp sự kiện `MissionLifecycleRealtimeEvent` tới nhóm phòng nhiệm vụ `mission_{missionId}` (`MissionLifecycleEvent`).
  - `NOTIF_MQ_025`: Phát sóng sự kiện chuyên biệt `MissionConfirmed` khi trạng thái là `CONFIRMED`.
  - `NOTIF_MQ_026`: Phát sóng sự kiện chuyên biệt cho các trạng thái `SUSPENDED`, `POSTPONED`, `RESUMED`, `CANCELLED`.
  - `NOTIF_MQ_027`: Phát sóng sự kiện `REMINDER` đồng thời tới phòng nhiệm vụ và trực tiếp nhóm người dùng mục tiêu `user:{TargetUserId}`.
  - `NOTIF_MQ_028`: Phát sóng sự kiện `DISPATCHED` đồng thời tới phòng nhiệm vụ, Inspector phụ trách và các thành viên đội bay được phân công (`AssignedUserIds`).
  - `NOTIF_MQ_029`: Bỏ qua an toàn khi `MissionId` bị rỗng và NACK không requeue khi JSON hỏng.
- **Function F: Consumer Lifecycle, Reliability & Resilience (9 TCs)**:
  - `NOTIF_MQ_030`: Toàn bộ 5 Consumer thiết lập manual ACK (`autoAck: false`) để đảm bảo cơ chế phân phối at-least-once.
  - `NOTIF_MQ_031`: Toàn bộ 5 Hàng đợi và Exchange `identity-exchange` (Topic) được khai báo bền vững (`durable = true`).
  - `NOTIF_MQ_032`: Mỗi vòng đời xử lý thông điệp sử dụng `IServiceScopeFactory` tạo scope riêng biệt, giải phóng `DbContext` ngay sau khi hoàn thành.
  - `NOTIF_MQ_033`: Xử lý ngoại lệ vi phạm khóa ngoại người dùng (`DbUpdateException`) an toàn, ghi log và NACK mà không làm sập background worker.
  - `NOTIF_MQ_034`: Bảo toàn toàn vẹn tiếng Việt Unicode UTF-8 trên tiêu đề và nội dung thông báo (`BAY-2026-ĐH-01`, `Thạnh Mỹ - Pleiku`).
  - `NOTIF_MQ_035`: Đóng kênh và kết nối nhẹ nhàng trong `StopAsync` khi nhận tín hiệu tắt ứng dụng (`CancellationToken`).
  - `NOTIF_MQ_036`: Định dạng nội dung thông báo sạch sẽ khi `Description = null` mà không chứa chuỗi ký tự null.
  - `NOTIF_MQ_037`: Xử lý an toàn khi `RecordId = Guid.Empty`, không ném ngoại lệ dữ liệu.
  - `NOTIF_MQ_038`: Xử lý đồng thời khối lượng lớn thông điệp mà không gây deadlock cơ sở dữ liệu hay cạn kiệt tài nguyên kết nối.

### 9. Background Recurring Jobs Suite (`test_notification_jobs_suite.py`) - 28 Test Cases
- **Function A: PushNotificationsJob (5 TCs)**:
  - `NOTIF_JOB_001`: Đẩy email cho thông báo chưa đẩy (`IsPushed = false`) của người dùng hợp lệ, cập nhật `IsPushed = true` và `PushedAt = UtcNow`.
  - `NOTIF_JOB_002`: Xử lý an toàn người dùng có email null hoặc rỗng, đánh dấu `IsPushed = true` để tránh vòng lặp vô tận (poison message).
  - `NOTIF_JOB_003`: Bỏ qua các thông báo đã đẩy (`IsPushed = true`), không gửi trùng lặp email.
  - `NOTIF_JOB_004`: Khả năng phục hồi xử lý hàng loạt: Khi 1 thông báo lỗi (SendGrid error), các thông báo còn lại trong batch tiếp tục được xử lý bình thường.
  - `NOTIF_JOB_005`: Người dùng bị xóa mềm (`user.IsDeleted = true`) được nạp dưới dạng navigation null an toàn, đánh dấu `IsPushed = true` và không gửi email.
- **Function B: DailySummaryJob (5 TCs)**:
  - `NOTIF_JOB_006`: Tạo thông báo tổng kết ngày cho `SystemAdmin` và `Manager` với type `"DailySummary"`, referenceType `"System"`, tiêu đề `"📊 Báo cáo tổng hợp hàng ngày"`.
  - `NOTIF_JOB_007`: Khử trùng lặp (`DistinctBy`) đối với người dùng giữ đồng thời nhiều vai trò (SystemAdmin + Manager) nhận đúng 1 thông báo.
  - `NOTIF_JOB_008`: Loại trừ người dùng có vai trò khác (`Inspector`, `Analyst`, `Technician`) không nhận báo cáo quản trị.
  - `NOTIF_JOB_009`: Bắt lỗi ngoại lệ từ `MonitorRepository` an toàn, ghi log lỗi mà không làm sập ứng dụng.
  - `NOTIF_JOB_010`: Tiêu đề nội dung chứa ngày theo múi giờ Việt Nam (UTC+7: `dd/MM/yyyy`).
- **Function C: CleanupJob (7 TCs)**:
  - `NOTIF_JOB_011`: Dọn dẹp tệp lưu trữ cũ hơn 30 ngày trong thư mục ảnh cảnh báo và giữ nguyên các tệp gần đây (< 30 ngày).
  - `NOTIF_JOB_012`: Xóa các refresh token hết hạn (`ExpiresAt < UtcNow`) khỏi cơ sở dữ liệu.
  - `NOTIF_JOB_013`: Xóa các refresh token bị thu hồi quá 30 ngày (`RevokedAt < 30 ngày trước`) và bảo toàn các token thu hồi gần đây.
  - `NOTIF_JOB_014`: Xử lý an toàn khi thư mục lưu trữ không tồn tại, bỏ qua dọn tệp và tiếp tục dọn token DB.
  - `NOTIF_JOB_015`: Bắt lỗi cơ sở dữ liệu khi dọn token an toàn mà không làm sập tiến trình Hangfire worker.
  - `NOTIF_JOB_016`: Tự động fallback về đường dẫn mặc định (`<CurrentDirectory>/uav_storage/images`) khi đường dẫn cấu hình không thể truy cập.
  - `NOTIF_JOB_017`: Ghi log thông báo không có token hết hạn/thu hồi khi bảng `RefreshTokens` hoàn toàn sạch.
- **Function D: Job Edge Cases & Reliability (11 TCs)**:
  - `NOTIF_JOB_018`: Xử lý an toàn số lượng lớn thông báo chưa đẩy (batch 50+ items).
  - `NOTIF_JOB_019`: Xử lý lỗi Rate Limit (HTTP 429) từ SendGrid: bắt lỗi ở cấp phần tử, giữ `IsPushed = false` để Hangfire thử lại trong chu kỳ minutely tiếp theo.
  - `NOTIF_JOB_020`: Bảo toàn toàn vẹn bảng mã tiếng Việt Unicode UTF-8 (`⚠️ Cảnh báo khẩn cấp`, `Kiểm tra cột điện #105`).
  - `NOTIF_JOB_021`: Xử lý hệ thống khi có 0 thống kê (0 missions, 0 inspections, 0 defects) an toàn mà không bị lỗi chia cho 0.
  - `NOTIF_JOB_022`: Xử lý khi hệ thống không có bất kỳ người dùng SystemAdmin hoặc Manager nào, hoàn thành êm dịu và log số lượng gửi = 0.
  - `NOTIF_JOB_023`: Tuân thủ chuẩn hợp đồng thực thể: `ReferenceType = "System"` và `Type = "DailySummary"`.
  - `NOTIF_JOB_024`: Bắt ngoại lệ `IOException` khi gặp tệp bị khóa (in-use), ghi log lỗi và tiếp tục dọn dẹp cơ sở dữ liệu.
  - `NOTIF_JOB_025`: Kiểm thử ngưỡng biên 30 ngày nghiêm ngặt (tệp 29.9 ngày được giữ lại, tệp 30.1 ngày bị xóa).
  - `NOTIF_JOB_026`: Dọn dẹp tất cả định dạng phần mở rộng (.log, .tmp, .dat, .jpg) cũ hơn 30 ngày trong thư mục chỉ định.
  - `NOTIF_JOB_027`: Khởi tạo và đăng ký tự động các RecurringJob trong Hangfire (`push-notifications-sync` * * * * *, `daily-summary-job` 0 0 * * *, `auto-cleanup-job` 0 0 * * 1).
  - `NOTIF_JOB_028`: Kích hoạt thủ công qua Hangfire Recurring Jobs Dashboard / API (`POST /hangfire/recurring/trigger`) phản hồi HTTP 204 No Content.

### 10. Email Delivery & EmailService Suite (`test_notification_delivery_suite.py`) - 28 Test Cases
- **Function A: Successful Email Delivery (Happy Path - 4 TCs)**:
  - `NOTIF_MAIL_001`: Kiểm tra gửi email OTP với tiêu đề `Your OTP Verification Code`, định dạng nội dung HTML `<strong>{code}</strong>` và thời gian hết hạn UTC.
  - `NOTIF_MAIL_002`: Kiểm tra gửi email đặt lại mật khẩu với tiêu đề `Password Reset Request`, nhúng reset token và mốc thời hạn UTC.
  - `NOTIF_MAIL_003`: Kiểm tra hàm gửi email tổng quát `SendEmailAsync` gán đầy đủ cả PlainText và HTML content cho body.
  - `NOTIF_MAIL_004`: Bảo toàn toàn vẹn bảng mã tiếng Việt Unicode UTF-8 (`⚠️ Cảnh báo khẩn cấp`, `Thạnh Mỹ - Pleiku`).
- **Function B: Failed Email — Error Handling & Configuration (4 TCs)**:
  - `NOTIF_MAIL_005`: Ném ngoại lệ `InvalidOperationException` ("SendGrid:ApiKey is not configured.") khi ApiKey bị rỗng hoặc null.
  - `NOTIF_MAIL_006`: Ném ngoại lệ `Exception` ("SendGrid returned status code {StatusCode}") khi SendGrid trả về lỗi cho OTP/Reset email.
  - `NOTIF_MAIL_007`: Ném ngoại lệ `Exception` ("SendGrid returned status code {StatusCode} for sending email to {toEmail}") cho hàm `SendEmailAsync`.
  - `NOTIF_MAIL_008`: Xử lý ngoại lệ kết nối mạng / timeout (`HttpRequestException`, `TaskCanceledException`) an toàn.
- **Function C: Template Formatting & Validation Edge Cases (14 TCs)**:
  - `NOTIF_MAIL_009`: Định dạng thời gian hết hạn OTP theo chuẩn `yyyy-MM-dd HH:mm:ss UTC` (chu kỳ 3 phút).
  - `NOTIF_MAIL_010`: Hỗ trợ mượt mà các độ dài mã OTP khác nhau (4 số, 6 số, 8 số).
  - `NOTIF_MAIL_011`: Xử lý an toàn khi mã OTP là chuỗi rỗng mà không bị NullReferenceException trong tiến trình.
  - `NOTIF_MAIL_012`: Cấu trúc HTML email đặt lại mật khẩu hiển thị chính xác token và mốc thời gian hết hạn.
  - `NOTIF_MAIL_013`: Xử lý token chứa ký tự đặc biệt URL/Base64 (`+`, `/`, `=`, `&`) an toàn.
  - `NOTIF_MAIL_014`: Xử lý an toàn khi reset token là chuỗi rỗng.
  - `NOTIF_MAIL_015`: Bảo toàn các thẻ HTML tùy biến (`<h1>`, `<p>`) khi truyền vào body của `SendEmailAsync`.
  - `NOTIF_MAIL_016`: Sử dụng cấu hình người gửi tùy biến `FromEmail` và `FromName` từ `SendGridOptions`.
  - `NOTIF_MAIL_017`: Xử lý ngoại lệ khi SendGrid trả về lỗi HTTP 400 Bad Request.
  - `NOTIF_MAIL_018`: Xử lý ngoại lệ khi SendGrid trả về lỗi HTTP 429 Too Many Requests (Rate limit) hỗ trợ retry chính sách Hangfire.
  - `NOTIF_MAIL_019`: Xử lý ngoại lệ khi SendGrid trả về lỗi HTTP 500 Internal Server Error.
  - `NOTIF_MAIL_020`: Phân tách kiến trúc logging: `EmailService` tập trung gửi mail, ghi log dispatch do orchestrator (`PushNotificationsJob`) đảm nhiệm.
  - `NOTIF_MAIL_021`: Xử lý tiêu đề và nội dung rỗng mà không quăng `NullReferenceException`.
  - `NOTIF_MAIL_022`: Đảm bảo an toàn đa luồng (thread-safe) khi gọi đồng thời nhiều tiến trình gửi email.
- **Function D: Supplementary Configuration & Reliability (6 TCs)**:
  - `NOTIF_MAIL_023`: Kiểm tra DataAnnotation validation của `SendGridOptions` (`[Required]` ApiKey, `[EmailAddress]` FromEmail).
  - `NOTIF_MAIL_024`: Kiểm tra giá trị mặc định của `FromName` là `"UavPms Notification System"`.
  - `NOTIF_MAIL_025`: Xử lý an toàn trường hợp địa chỉ email nhận là null thông qua SendGrid client pipeline.
  - `NOTIF_MAIL_026`: Kiểm tra ràng buộc thời gian hết hạn OTP được xác định rõ là 3 phút UTC.
  - `NOTIF_MAIL_027`: Kiểm tra hợp đồng xếp hàng Hangfire `BackgroundJob.Enqueue<IEmailService>`.
  - `NOTIF_MAIL_028`: Xác thực đăng ký Dependency Injection của `IEmailService` dưới dạng `Scoped` trong DI container.

---

## Hướng Dẫn Thực Thi

```bash
# Chạy toàn bộ 10 suites kiểm thử NotificationService:
python3 tests/NotificationService/run_all_notification_suites.py

# Hoặc chạy riêng Email Delivery Suite:
python3 tests/NotificationService/notification-suites/test_notification_delivery_suite.py
```




