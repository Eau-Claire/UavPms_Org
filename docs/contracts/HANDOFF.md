# BE ↔ FE Contract Handoff Log (Codex / Claude ↔ Antigravity)

> **Quy tắc**: Giữ file này ngắn gọn (≤ 50 dòng). Mọi thay đổi phát sinh giữa **Backend (`D:\UavPms_BEE`)** và **Frontend (`d:\UavPms_FEE`)** ngoài `openapi.json` (SignalR events, mã lỗi, RBAC, bug chéo) đều ghi ngắn tại đây.
> **Hợp đồng REST chính**: `docs/contracts/openapi.json` (do Backend xuất sau mỗi đợt đổi REST).

## Trạng thái đồng bộ hiện tại
- **Master Spec**: `docs/MASTER_SYSTEM_SPEC.md` (**v2.0** — khóa `2026-11-27`, Zero-Context Ready).
- **Kế hoạch BA & Prompt Mẫu**: `docs/TEAM_MVP_ACTION_PLAN.md` (**v2.0**).
- **OpenAPI Contract**: `docs/contracts/openapi.json` *(xuất bản chính thức v1.0 ở PR Lớn 4)*.
- **Đợt hiện hành**: **PR Lớn 1 (Mainflow-First)**: BE chạy `BE-REQ-101` (Drone Detail BIST/FC + nhập tay cho Technician/SystemAdmin + bỏ BIST khỏi MF01 + MF01 chỉ chọn số lượng nhân sự); FE chạy song song `FE-REQ-004` `PR-1b2` (nốt 2 màn PMTiles Pre-Mission) → nối tiếp `FE-REQ-101`.
- **Harness v2.0**: Đã dựng `.specify/memory/constitution.md`, `CLAUDE.md`, `.claude/hooks/` (`exit 2`), `.codex/config.toml`, EARS `_template/`, và `d:\UavPms_Holdout\`.

## Nhật ký bàn giao (Mới nhất ở trên)

- **[BE-REQ-101 Contract Lock][2026-11-28]**: Khóa & **Codex R3 APPROVE** REST PR 1 (`OperationsService`):
  - `POST /api/v1/uavs/{droneId}/inspections` (`SystemAdmin` + `Technician` trong vùng): nhận `sourceType: manual|fc|bist`, `conclusion: Passed|Failed`, `notes`, `metrics[]`, bỏ `preMissionAssessmentId`; trả `readiness: Ready|MaintenanceRequired` (`Healthy`/`Passed` = `Ready`, còn lại = `MaintenanceRequired`). `GET /api/v1/uavs/{droneId}/inspections/latest`, `GET /api/v1/drones/{id}/technical-inspections` (`DESC`, `Analyst` `403`), `GET /api/v1/drones/eligible` (lọc `UserGeographicScopes`, ngoại lệ `SystemAdmin`). `BRL-02`: ghép FC cấm tự gỡ ở Drone khác (`409 FC_ALREADY_PAIRED`), không bịa firmware `"Betaflight"`. `BRL-04`: `basic-technical-check` giữ số đo thật (`connected` boolean, không bịa `0%`), thiếu mục bắt buộc trả `status: Incomplete`, `readiness: MaintenanceRequired`, `missingMetrics[]` để FE báo đỏ & vẫn cho nhập tay bổ sung; chỉ `400 METRICS_REQUIRED` khi rỗng hoàn toàn.
  - `POST /api/v1/pre-mission-assessments` & `/{id}/re-evaluate`: nhận `inspectorCount`, `analystCount`, `technicianCount` (`>=1`, lưu `Findings` jsonb, lỗi `400 INVALID_ROLE_HEADCOUNT`), `droneIds[]` (chỉ Drone `Ready` cùng vùng `BRL-01` cả `SystemAdmin` — `409 DRONE_REGION_MISMATCH`/`DRONE_NOT_READY`), `idempotencyKey` đối chiếu cả `droneIds[]` & 3 headcount (`409 IDEMPOTENCY_KEY_REUSED`); `BRL-03`: `/re-evaluate` không gửi `droneIds[]` tự loại Drone lệch vùng/hỏng rồi tính lại; trần hiệu lực `<= 7 ngày`.
- **[PR1 Kickoff][2026-11-27]**: Khóa Spec & Gate 5 cho `BE-REQ-101` và `FE-REQ-101`; thứ tự mới chốt Mainflow trước (PR 1→4), Auth/Silent Refresh/OTP chốt cuối ở PR Lớn 5.

- **[v2.0 Spec Lock][2026-11-27]**: Đã khóa đặc tả v2.0 cho cả BE & FE:
  1. **`SystemAdmin`** toàn quyền 100% (không giới hạn vùng) & cấu hình `SystemSettings` (Check-in `50m`, ngưỡng Pre-Mission, Hangfire cron, upload `20MB`/`200MB`, token TTL).
  2. **BIST / Sức khỏe Drone / FC** (kể cả nhập tay) chuyển sang **Chi tiết Drone** (`Technician` + `SystemAdmin`), bỏ khỏi Pre-Mission. MF01 chọn số lượng từng role → MF02 chọn đích danh 3 role (`Inspector`, `Analyst`, `Technician`) → cả 3 `Accept` → chỉ `Inspector` (`+ SystemAdmin`) Check-in GPS `≤ 50m` tại Cột 1 + BE phát SignalR realtime.
  3. **Telemetry MF03**: qua SignalR `/hubs/telemetry` (RAM-only, delay `≤ 10s`, không script giả lập, không ghi DB `DroneTelemetry`).
  4. **Upload Ảnh/Video & Review**: Ảnh đọc EXIF GPS / Video chụp frame lỗi GPS live → tự gắn cột gần nhất (không ép chọn cột khi upload, cho đổi tay); mọi thành viên Mission **Xem** defect (`Analyst` Mission / `SystemAdmin` **Duyệt**); bỏ chữ `"AI Review"` sau duyệt, xóa `N/A`, đồng nhất UI, BBox chuẩn `0..100%`.
  5. **Maintenance, Notification & Lists**: `Approve` defect Mission → tự gán `MaintenanceTicket` (`Assigned`) cho `Technician` của Mission; Notification chia đúng role + chống duplicate popup (BE 1 event + `Distinct`, FE dedupe theo ID); mọi List mặc định mới nhất lên đầu; FE thêm Silent Refresh Token.
- **[BE][2026-11-27]**: Đã duyệt `H`, `C1`, `C2a` (+ fix `deploy.yml` báo thiếu `RABBITMQ_USER`/`RABBITMQ_PASSWORD`/`REDIS_PASSWORD` trên VPS `.env`), `C2b`, `C3a`, `G1` (P0-5), `N1` (P0-3). Tiếp tục `P0-4` (`N2+N3`) → `P0-1`, `P0-2`, `CU` (`C3b` hoãn cuối Đợt 1; server `uavpms.ddns.net` giữ ổn định).
- **[FE][2026-11-27]**: Xong `FE-REQ-002` (Lint/Utils), `FE-REQ-003` (gỡ `drone-handover`/`Alerts`/ẩn Active Learning), `FE-REQ-004` (4/6 màn PMTiles). `FE-REQ-005` chờ Judge re-review; sửa 2 lỗi CI PR Vercel (`drone-management.spec.ts:144` & `EmergencyAlertsApi:40`) + sort `/ai-review` mới nhất lên đầu.
