# Bản đồ tài liệu UAV-PMS Backend

Đọc file này trước để biết tài liệu nào là chuẩn và nên mở file nào. Khi hai tài liệu mâu thuẫn, ưu tiên theo thứ tự ở mục 1.

## 1. Nguồn sự thật (theo thứ tự ưu tiên)

| # | File | Dùng khi |
|---|---|---|
| 1 | `../.specify/memory/constitution.md` | 7 bất biến `C-01..C-07` (bị hook khóa) |
| 2 | `MASTER_SYSTEM_SPEC.md` (v2.1) | Nghiệp vụ, phạm vi, phân quyền §4, 5 luồng chính, enum, SystemSettings |
| 3 | `../specs/<BE-REQ>/spec.md`, `acceptance.md` | Yêu cầu và Gate 5 của từng batch |
| 4 | `../specs/RTM.md` | Truy vết: yêu cầu → test → branch/commit → trạng thái |
| 5 | `contracts/HANDOFF.md` + `contracts/openapi.json` | Hợp đồng và nhật ký bàn giao BE ↔ FE |

## 2. Theo chủ đề

| Chủ đề | File |
|---|---|
| Phân quyền theo endpoint, luật vùng, việc còn lại | `RBAC_PERMISSIONS.md` |
| Việc FE cần làm sau đợt RBAC | `contracts/FE_HANDOFF_RBAC_REGION_SCOPE.md` |
| Kiến trúc service | `service-architecture.md` |
| Quy ước code, đặt tên, `ApiResponse`, phân trang | `conventions.md` |
| Thông báo realtime (SignalR) | `realtime-notifications.md` |
| Thiết kế CSDL (sinh từ EF snapshot) | `database-design/` (README, `domains/*.mmd`) |
| Hợp đồng schema ERD v2.2 (migration đang trỏ tới) | `database/ERD_v2_2_TARGET_SCHEMA.md` |
| Migration an toàn / rollback | `NON_DESTRUCTIVE_MIGRATION_AND_ROLLBACK_GUIDE.md` |
| Nhập manifest cột điện production | `production-pole-manifest-import.md` |
| Tích hợp Vision Edge ↔ PMS | `vision_pms_integration.md`, `vpms_user_guide_integration.md` |
| Package diagram (drawio + mô tả) | `package-diagrams/` |
| Ảnh sơ đồ (activity, class, sequence, state, ERD, use case) | `diagrams/` |
| Tra cứu viết báo cáo đồ án | `BA_REPORT_REFERENCE.md`, `reports/` |

## 3. Đã ngưng dùng

`archive/` giữ tài liệu cũ trước spec v2.0 để tra lịch sử: `api_spec.md`, `database_schema.md`, `roadmap.md`, `ERD_V2_2_BACKEND_IMPLEMENTATION_PLAN.md`, `MASTER_IMPLEMENTATION_AND_REFACTORING_PLAN.md`, `mock-ai-worker.md`. Không dùng làm đặc tả.

## 4. Cấu hình agent (Claude Code / Codex)

| Loại | Vị trí | Ghi chú |
|---|---|---|
| Hướng dẫn chung | `../AGENTS.md` (Claude nạp qua `../CLAUDE.md`) | Một nguồn cho cả hai agent |
| Skill | `../.agents/skills/<tên>/SKILL.md` | Nội dung chuẩn. `../.claude/skills/<tên>` chỉ trỏ về đây |
| Rule theo vùng code | `../.claude/rules/` | `auth-and-rbac` (Auth, RBAC, phạm vi vùng), `missions-and-ai-data`, `notifications-and-realtime` |
| Subagent đối chiếu spec | `../.claude/agents/spec-checker.md` | Chỉ đọc |
| Hook khóa spec / test | `../.claude/hooks/protect-sdd-and-tests.mjs` | `SDD_PHASE=SPEC|RED|CODE`, `../.claude/locked-tests.txt` |
