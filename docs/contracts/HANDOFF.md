# BE ↔ FE Contract Handoff Log (Codex / Claude ↔ Antigravity)

> **Quy tắc**: Giữ file này ngắn gọn (≤ 50 dòng). Mọi thay đổi phát sinh giữa **Backend (`D:\UavPms_BEE`)** và **Frontend (`d:\UavPms_FEE`)** ngoài `openapi.json` (SignalR events, mã lỗi, RBAC, bug chéo) đều ghi ngắn tại đây.
> **Hợp đồng REST chính**: `docs/contracts/openapi.json` (do Backend xuất sau mỗi đợt đổi REST).

## Trạng thái đồng bộ hiện tại
- **Master Spec**: `docs/MASTER_SYSTEM_SPEC.md` (**v2.0** — khóa `2026-11-27`, Zero-Context Ready).
- **Kế hoạch BA & Prompt Mẫu**: `docs/TEAM_MVP_ACTION_PLAN.md` (**v2.0**).
- **OpenAPI Contract**: `docs/contracts/openapi.json` *(chờ Backend xuất bản v1.0 ở Đợt 1)*.
- **Đợt hiện hành**: **Đợt 0 + Mainflow Bugfix** (FE chốt `FE-REQ-005` + CI PR Vercel; BE tiếp tục `P0-4` `N2+N3` → `P0-1`, `P0-2`, `CU`).
- **Harness v2.0**: Đã dựng `.specify/memory/constitution.md`, `CLAUDE.md`, `.claude/hooks/` (`exit 2`), `.codex/config.toml`, EARS `_template/`, và `d:\UavPms_Holdout\`.

## Nhật ký bàn giao (Mới nhất ở trên)

- **[v2.0 Spec Lock][2026-11-27]**: Đã khóa đặc tả v2.0 cho cả BE & FE:
  1. **`SystemAdmin`** toàn quyền 100% (không giới hạn vùng) & cấu hình `SystemSettings` (Check-in `50m`, ngưỡng Pre-Mission, Hangfire cron, upload `20MB`/`200MB`, token TTL).
  2. **BIST / Sức khỏe Drone / FC** (kể cả nhập tay) chuyển sang **Chi tiết Drone** (`Technician` + `SystemAdmin`), bỏ khỏi Pre-Mission. MF01 chọn số lượng từng role → MF02 chọn đích danh 3 role (`Inspector`, `Analyst`, `Technician`) → cả 3 `Accept` → chỉ `Inspector` (`+ SystemAdmin`) Check-in GPS `≤ 50m` tại Cột 1 + BE phát SignalR realtime.
  3. **Telemetry MF03**: qua SignalR `/hubs/telemetry` (RAM-only, delay `≤ 10s`, không script giả lập, không ghi DB `DroneTelemetry`).
  4. **Upload Ảnh/Video & Review**: Ảnh đọc EXIF GPS / Video chụp frame lỗi GPS live → tự gắn cột gần nhất (không ép chọn cột khi upload, cho đổi tay); mọi thành viên Mission **Xem** defect (`Analyst` Mission / `SystemAdmin` **Duyệt**); bỏ chữ `"AI Review"` sau duyệt, xóa `N/A`, đồng nhất UI, BBox chuẩn `0..100%`.
  5. **Maintenance, Notification & Lists**: `Approve` defect Mission → tự gán `MaintenanceTicket` (`Assigned`) cho `Technician` của Mission; Notification chia đúng role + chống duplicate popup (BE 1 event + `Distinct`, FE dedupe theo ID); mọi List mặc định mới nhất lên đầu; FE thêm Silent Refresh Token.
- **[BE][2026-11-27]**: Đã duyệt `H`, `C1`, `C2a` (+ fix `deploy.yml` báo thiếu `RABBITMQ_USER`/`RABBITMQ_PASSWORD`/`REDIS_PASSWORD` trên VPS `.env`), `C2b`, `C3a`, `G1` (P0-5), `N1` (P0-3). Tiếp tục `P0-4` (`N2+N3`) → `P0-1`, `P0-2`, `CU` (`C3b` hoãn cuối Đợt 1; server `uavpms.ddns.net` giữ ổn định).
- **[FE][2026-11-27]**: Xong `FE-REQ-002` (Lint/Utils), `FE-REQ-003` (gỡ `drone-handover`/`Alerts`/ẩn Active Learning), `FE-REQ-004` (4/6 màn PMTiles). `FE-REQ-005` chờ Judge re-review; sửa 2 lỗi CI PR Vercel (`drone-management.spec.ts:144` & `EmergencyAlertsApi:40`) + sort `/ai-review` mới nhất lên đầu.
