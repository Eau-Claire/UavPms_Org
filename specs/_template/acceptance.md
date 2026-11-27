# [BE-REQ-XXX] Agreed Acceptance Test List (Gate 5 — Locked TDD)

> **Gate 5 Rule**: Danh sách test này PHẢI được chốt trước khi Coding Agent sửa `Services/`. Gate 5 mặc định chạy in-process (`WebApplicationFactory<Program>` + JWT/DB/Redis/SMTP thật cô lập), không phụ thuộc URL server chạy sẵn.

- **Approved By (QA/PO/Human Owner)**: `<tên>`
- **Approved Date**: `YYYY-MM-DD`

---

## 0. Quy trình Khóa Test Chống Gian Lận (`RED → LOCK → GREEN`)

*Bắt buộc cho toàn bộ lát cắt **Tier 1** (Identity/Auth, RBAC, Check-in 50m, Ocelot Gateway, SignalR, Migrations):*
1. **Pha `RED`** (`SDD_PHASE=RED`): Viết test thất bại (RED) cho từng `FR-xxx` / `HC-xx` (`*Gate5*Tests.cs` hoặc `tests/acceptance/*.cs`), chạy xác nhận fail đúng lý do.
2. **Khóa File Test**: Ghi đường dẫn tương đối file test vào `.claude/locked-tests.txt` (nếu không thuộc mẫu `*Gate5*Tests.cs` / `tests/acceptance/`).
3. **Pha `CODE`** (mặc định `SDD_PHASE=CODE`): Hook `.claude/hooks/protect-sdd-and-tests.mjs` tự động chặn (`exit 2`) mọi thao tác sửa file test đã khóa — Agent chỉ được sửa `Services/` cho đến khi GREEN.

---

## 1. Bảng Ánh xạ `FR-xxx` / `HC-xx` ↔ Test Case (Given-When-Then)

| Mã Spec | Kịch bản Gherkin (Given / When / Then) | Loại Test & Project Đích | Trạng thái |
|---|---|---|---|
| `FR-001` | **Given** ... **When** ... **Then** ... | `Integration` (`Services/...Tests/...Gate5Tests.cs`) | `[ ] RED -> GREEN` |
| `FR-004` | **Given** ... **When** ... **Then** trả `403`/`409`/`400` không rò secret | `Integration` (`Services/...Tests/...`) | `[ ] RED -> GREEN` |
| `HC-01` | `SystemAdmin` bỏ qua lọc `GeographicScope`, 4 role còn lại bị lọc đúng vùng | `Integration` | `[ ] RED -> GREEN` |

```gherkin
Feature: <Tên Batch / Nghiệp vụ BE>

  Scenario: Happy path đúng theo FR-001
    Given một tài khoản với role "<Role>" và scope hợp lệ
    When gọi endpoint "<METHOD /api/v1/...>" với payload chuẩn
    Then trả HTTP <Status> cùng ApiResponse camelCase chuẩn UTC Z
    And bản ghi trong PostgreSQL/Redis/RabbitMQ được cập nhật đúng
```

---

## 2. Security, RBAC, Scope & No-Mock Gates

- [ ] Request thiếu JWT hoặc sai role/scope bị từ chối `401`/`403` từ claim JWT đã xác thực phía server.
- [ ] `SystemAdmin` truy cập được toàn bộ vùng & cấu hình `SystemSettings`.
- [ ] Zero Mock/Fake Data, Zero OTP Bypass (`123456` luôn fail); không rò JWT, OTP, mật khẩu, email thật trong response/log/TRX.

---

## 3. Data Integrity, Ordering & Concurrency Edge Cases

- [ ] Endpoint danh sách mặc định `OrderByDescending` mới nhất lên đầu (`CreatedAt` / `UpdatedAt` / `DetectedAt`).
- [ ] Trùng lịch Drone/Nhân sự trả `409 Conflict`; thao tác chuyển trạng thái sai trả `409`/`400` không làm hỏng DB.
- [ ] SignalR chỉ phát **1 event duy nhất** cho 1 hành động, `Distinct` connection IDs; `/hubs/telemetry` không ghi DB.

---

## 4. Verification Command (In-Process CI Safe)

- [ ]Targeted: `dotnet test Services/<Service>/<Service>.Tests/<Service>.Tests.csproj`
- [ ]Final Gate (1 lần chốt): `dotnet build UavPms.sln -c Release --no-restore && dotnet test UavPms.sln -c Release --no-build`
