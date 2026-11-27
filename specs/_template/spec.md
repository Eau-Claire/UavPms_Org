# [BE-REQ-XXX] Tên Tính năng / Batch

- **Req ID**: `BE-REQ-XXX` *(bất biến, không tái sử dụng)*
- **Status**: `DRAFT` | `FROZEN (Approved by BA/PO at commit <sha>)`
- **Risk Tier**: `Tier 1 (Auth/RBAC/Ocelot/SignalR/Migration)` | `Tier 2 (CRUD/Business)` | `Tier 3 (CI/YAML/Docs)`
- **Owner (BA/PO)**: `<tên>`
- **Named Approver (Judge / Human Owner)**: `<tên khác người code>`
- **Human-Approval-Required Zones Touched**: `None` | `IdentityService/Auth` | `.github/workflows/**` | `Production Migration`

---

## 1. Problem & Goal (What & Why)

<!-- Nêu ngắn gọn yêu cầu từ docs/MASTER_SYSTEM_SPEC.md v2.0, service đích (IdentityService / OperationsService / AIInspectionService / NotificationService / ApiGateway), và kết quả đo được. -->

---

## 2. Hard Constraints (`HC-xx` — Bất biến, Vi phạm = Fail)

- **HC-01**: `SystemAdmin` luôn toàn quyền mọi endpoint và bỏ qua lọc `UserGeographicScopes` (`C-02`).
- **HC-02**: Zero Mock/Fake Data, Zero OTP Bypass (`123456` bị cấm), không log JWT/OTP/password/email thật (`C-03`).
- **HC-03**: Mọi list API mặc định `OrderByDescending` mới nhất lên đầu, trả 1 `ApiResponse` `camelCase`, lưu DB `UTC` (`C-04`, `C-05`).
- **HC-04**: Code tuyến tính `if/return` tối đa 2 cấp, tái sử dụng helper trong `Shared/`, kết thúc bằng "Tóm tắt luồng để bảo vệ code" (`C-06`).

---

## 3. Functional Requirements (Cú pháp EARS / GEARS)

> **Quy tắc**: Mọi yêu cầu phải gắn mã `FR-xxx` theo 4 mẫu EARS chuẩn để ánh xạ 1–1 sang Gate 5 `acceptance.md`.

- **FR-001 [Event]**: WHEN `<HTTP request / SignalR / Hangfire / RabbitMQ trigger>`, THE `<Service/Handler>` SHALL `<xử lý DB/domain & HTTP status/event cụ thể>`.
- **FR-002 [State]**: WHILE `<trạng thái entity đang diễn ra>`, WHEN `<lệnh chuyển trạng thái đến>`, THE `<Service/Handler>` SHALL `<kiểm tra hợp lệ hoặc trả 409 Conflict>`.
- **FR-003 [Config]**: WHERE `<giá trị đọc từ SystemSettings hoặc quyền role>`, WHEN `<nghiệp vụ thực thi>`, THE `<Service/Handler>` SHALL `<áp dụng đúng ngưỡng động>`.
- **FR-004 [Error]**: IF `<thiếu quyền 403, sai scope, thiếu GPS/EXIF, hoặc lỗi SMTP/RabbitMQ>`, THEN THE `<Service/Handler>` SHALL `<trả mã lỗi chuẩn không nuốt exception, không rò secret>`.

---

## 4. API / DB / SignalR / File Targets Contract

- **Target Files**: `Services/<ServiceName>/...`
- **Database / Migration Impact**: `None` | `OperationsService Migration via GitHub Actions rehearsal`
- **RBAC Matrix**:
  - Allowed roles: `SystemAdmin`, `...`
  - Denied roles (`403` / ownership check): `...`
- **OpenAPI / Handoff Sync**: Chạy `./scripts/export-openapi.ps1` và cập nhật `docs/contracts/HANDOFF.md` nếu đổi REST/SignalR.

---

## 5. Out of Scope (Cấm AI tự ý thêm)

- Không thêm endpoint, cột DB, hoặc cơ chế SRE ngoài phạm vi `FR-xxx` đã đóng băng.
- Không sửa `specs/`, `.specify/memory/constitution.md`, hay test Gate 5 đã khóa trong pha `CODE`.
