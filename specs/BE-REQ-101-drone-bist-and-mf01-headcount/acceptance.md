# [BE-REQ-101] Agreed Acceptance Test List (Gate 5 — Locked TDD)

> **Gate 5 Rule**: Danh sách test này PHẢI được chốt trước khi Coding Agent sửa `Services/`. Gate 5 mặc định chạy in-process (`WebApplicationFactory<Program>` + JWT/DB/Redis thật cô lập), không phụ thuộc URL server chạy sẵn.

- **Approved By (QA/PO/Human Owner)**: `Control Tower + Human Owner`
- **Approved Date**: `2026-11-27`

---

## 0. Quy trình Khóa Test Chống Gian Lận (`RED → LOCK → GREEN`)

*Bắt buộc cho toàn bộ lát cắt **Tier 1** (RBAC Drone BIST/FC, Geographic Scope, MF01 Readiness Gate):*
1. **Pha `RED`** (`SDD_PHASE=RED`): Viết test thất bại (RED) cho từng `FR-001..FR-007` & `HC-01..HC-03` (`*Gate5*Tests.cs` trong `Services/OperationsService/UavPms.OperationsService.IntegrationTests/` hoặc test project sẵn có của `OperationsService`), chạy xác nhận fail đúng lý do.
2. **Khóa File Test**: Ghi đường dẫn tương đối file test vào `.claude/locked-tests.txt` (nếu không thuộc mẫu `*Gate5*Tests.cs` / `tests/acceptance/`).
3. **Pha `CODE`** (mặc định `SDD_PHASE=CODE`): Hook `.claude/hooks/protect-sdd-and-tests.mjs` tự động chặn (`exit 2`) mọi thao tác sửa file test đã khóa — Agent chỉ được sửa `Services/OperationsService/` (và `Shared/` nếu có) cho đến khi GREEN.

---

## 1. Bảng Ánh xạ `FR-xxx` / `HC-xx` ↔ Test Case (Given-When-Then)

| Mã Spec | Kịch bản Gherkin (Given / When / Then) | Loại Test & Project Đích | Trạng thái |
|---|---|---|---|
| `FR-001` | **Given** `Technician` trong vùng (hoặc `SystemAdmin`) **When** gửi payload kiểm tra FC/nhập tay BIST kèm ghi chú & kết luận, hoặc gửi partial check / `connected=false` ở `basic-technical-check` **Then** lưu đúng số đo thực tế (`connected` boolean, không bịa `0%`), đánh dấu `Ready` hoặc `MaintenanceRequired` (báo thiếu thông số nếu thiếu), và chỉ trả `400 METRICS_REQUIRED` khi không có dữ liệu kiểm tra nào | `Integration` (`Services/OperationsService/UavPms.OperationsService.IntegrationTests/`) | `[ ] RED -> GREEN` |
| `FR-002` | **Given** `SystemAdmin`, `Technician`, `Manager` hoặc `Inspector` hợp lệ scope **When** `GET` chi tiết Drone, lịch sử BIST/FC hoặc `GET /drones/eligible` **Then** lọc đúng `UserGeographicScopes` (ngoại lệ `SystemAdmin`), trả `200` `camelCase`, lịch sử `DESC` | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `FR-003` | **Given** `Analyst` gọi đọc/ghi BIST, hoặc `Manager`/`Inspector` gọi ghi, hoặc user ngoài `UserGeographicScopes` (`GET /drones/eligible`, ghép/hủy FC), hoặc ghép FC UID đang gắn ở Drone khác (`BRL-02`) **When** gọi endpoint **Then** trả `403`/`404` khi sai quyền/ngoài scope, trả `409 FC_ALREADY_PAIRED` khi FC đang gắn Drone khác (không tự gỡ), và để trống firmware khi thiếu | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `FR-004` | **Given** `Manager` trong vùng hoặc `SystemAdmin` tạo/duyệt/re-evaluate MF01 **When** chọn Drone `Ready` cùng vùng không cần BIST nội bộ, hoặc gọi `/re-evaluate` không truyền `droneIds[]` sau khi Drone đã lưu bị đổi vùng/hỏng (`BRL-03`), hoặc replay `idempotencyKey` đổi `droneIds`/headcount **Then** MF01 lưu/tự loại Drone lệch vùng/hỏng khi re-evaluate không body và không trả nhầm replay cũ khi đổi `droneIds`/headcount | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `FR-005` | **Given** `Manager` hoặc `SystemAdmin` tạo/cập nhật MF01 **When** chỉ truyền số lượng nhân sự 3 role (`InspectorCount >= 1`, `AnalystCount >= 1`, `TechnicianCount >= 1`) không truyền danh sách `UserId` đích danh **Then** lưu thành công vào `PreMissionAssessments.Findings` (jsonb) và trả về đúng `inspectorCount`/`analystCount`/`technicianCount` trên API response (không tạo migration) | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `FR-006` | **Given** cấu hình `PreMissionMaxValidityDays <= 7` **When** tạo/duyệt hồ sơ MF01 vượt quá 7 ngày hoặc mặc định **Then** áp trần hiệu lực tối đa `<= 7 ngày` | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `FR-007` | **Given** payload MF01 chứa Drone `MaintenanceRequired`, Drone khác vùng (`Uav.AssignedRegionId != assessment.RegionId` — test với cả `Manager` và `SystemAdmin`), hoặc thiếu/âm số lượng 1 trong 3 role bắt buộc **When** gọi tạo/cập nhật MF01 **Then** trả `400 Bad Request` (`INVALID_ROLE_HEADCOUNT`) cho lỗi số lượng nhân sự `< 1`/thiếu/âm, `409 Conflict` (`DRONE_REGION_MISMATCH`) cho Drone khác vùng/chưa gắn vùng (kể cả `SystemAdmin`), và `409 Conflict` (`DRONE_NOT_READY`) cho Drone không `Ready` | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `HC-01` | **Given** `SystemAdmin` không thuộc scope của Drone/Region **When** đọc/ghi BIST Drone hoặc tạo MF01 **Then** bỏ qua lọc `UserGeographicScopes` và thành công; 4 role còn lại bị lọc đúng vùng | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `HC-02` | **Given** multi-role user (`Manager + Technician`) **When** thao tác ghi BIST trong vùng **Then** hợp (union) quyền cho phép ghi BIST theo role `Technician` | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |
| `HC-03` | **Given** mọi endpoint của `BE-REQ-101` **When** trả response và lưu DB **Then** không có mock/fake data cảm biến, thời gian DB là `UTC`, response 1 `ApiResponse` `camelCase` | `Integration` (`OperationsService.IntegrationTests`) | `[ ] RED -> GREEN` |

```gherkin
Feature: Drone Detail BIST/FC (Manual + Serial) & MF01 Headcount Only

  Scenario: Technician records manual BIST inspection at Drone Detail
    Given một tài khoản với role "Technician" và scope chứa UAV "uav-ready-01"
    When gọi endpoint ghi kiểm tra BIST/FC với thông số nhập tay, ghi chú và kết luận đạt chuẩn
    Then trả HTTP 200 cùng ApiResponse camelCase chuẩn UTC Z
    And bản ghi mới được lưu trong DroneTechnicalInspections và UAV cập nhật trạng thái "Ready"

  Scenario: Manager creates Pre-Mission Assessment with Ready drones and role headcounts only
    Given một tài khoản với role "Manager" và scope chứa Region và UAV ở trạng thái "Ready"
    When tạo PreMissionAssessment chỉ truyền inspectorCount=1, analystCount=1, technicianCount=1 (không userId, không BIST nội bộ)
    Then trả HTTP 200/201 và lưu vào PreMissionAssessments.Findings (jsonb) kèm trả đúng số lượng trên API response
```

---

## 2. Security, RBAC, Scope & No-Mock Gates

- [ ] Request thiếu JWT hoặc sai role/scope bị từ chối `401`/`403` từ claim JWT đã xác thực phía server.
- [ ] `SystemAdmin` truy cập được toàn bộ vùng & thực hiện cả BIST Drone lẫn MF01.
- [ ] Zero Mock/Fake Data (`C-03`); không tạo telemetry/BIST giả trong BE, không rò secret/PII trong log/response.

---

## 3. Data Integrity, Ordering & Concurrency Edge Cases

- [ ] Lịch sử BIST/FC và danh sách Pre-Mission mặc định `OrderByDescending` mới nhất lên đầu (`CreatedAt` / `UpdatedAt`).
- [ ] Chọn Drone `MaintenanceRequired` trong MF01 hoặc số lượng nhân sự `< 1` trả `400`/`409` không làm hỏng DB.
- [ ] Không tạo EF Core Migration trái phép khi schema hiện hữu đã đáp ứng (nếu thiếu cột buộc phải xin duyệt trước).

---

## 4. Verification Command (In-Process CI Safe)

- [ ] Targeted: `dotnet test Services/OperationsService/UavPms.OperationsService.IntegrationTests/UavPms.OperationsService.IntegrationTests.csproj` *(kèm UnitTest project của `OperationsService` nếu có)*
- [ ] Final Gate (1 lần chốt): `dotnet build UavPms.sln -c Release --no-restore && dotnet test UavPms.sln -c Release --no-build`
