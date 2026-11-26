# BE ↔ FE Contract Handoff Log (Codex ↔ Antigravity)

> **Quy tắc**: Giữ file này ngắn gọn (≤ 50 dòng). Mọi thay đổi phát sinh giữa **Backend (`D:\UavPms_BEE` — Codex)** và **Frontend (`d:\UavPms_FEE` — Antigravity)** ngoài `openapi.json` (như tên sự kiện SignalR, mã lỗi mới, field đang chờ bổ sung) đều ghi ngắn 2–3 dòng tại đây.
> **Hợp đồng REST chính**: `docs/contracts/openapi.json` (do Backend xuất sau mỗi đợt).

## Trạng thái đồng bộ hiện tại
- **Master Spec**: `docs/MASTER_SYSTEM_SPEC.md` (v1.0 — đã khóa 100%, 38 bảng DB + 2 State Machine + 5 luồng).
- **OpenAPI Contract**: `docs/contracts/openapi.json` *(chờ Backend xuất bản v1.0 ở Đợt 1)*.
- **Đợt hiện hành**: **Đợt 0** (Backend Harness + vá 5 lỗ hổng bảo mật P0).

## Nhật ký bàn giao (Mới nhất ở trên)

- **[Đợt 0][FE → BE][2026-11-26]**:
  - Đã khóa `docs/MASTER_SYSTEM_SPEC.md` và `.agents/skills/readable-code-defense/SKILL.md`.
  - Chờ Backend hoàn thành Đợt 0 (vá 5 lỗ hổng P0 + sửa `CreateUser`) và Đợt 1 (DROP 4 bảng qua GitHub Actions, đổi `InspectionMedia.MissionId` nullable, chuẩn hóa 1 kiểu `ApiResponse` + `camelCase`, xuất `openapi.json` v1.0 chép vào `d:\UavPms_FEE\docs\contracts\openapi.json`).
  - SignalR cần chốt ở Đợt 2: `/hubs/telemetry` dùng `camelCase` + `[Authorize]` + tách Ocelot `/negotiate` dùng `http`; `/hubs/notifications` bỏ các hàm `Broadcast*` từ client và tránh bắn trùng 3 event cùng lúc.
