# [BE-REQ-101] Drone Detail BIST/FC (Kèm Nhập Tay) & MF01 Headcount Only

- **Req ID**: `BE-REQ-101` *(bất biến, không tái sử dụng)*
- **Status**: `FROZEN (Approved by Control Tower & Human Owner — PR Lớn 1, 2026-11-27)`
- **Risk Tier**: `Tier 1 (RBAC Drone BIST/FC + Geographic Scope + MF01 Readiness Gate)`
- **Owner (BA/PO)**: `Control Tower / System Owner`
- **Named Approver (Judge / Human Owner)**: `Codex Diff Auditor + Human Owner`
- **Human-Approval-Required Zones Touched**: `None` *(Chỉ chạm `OperationsService` & `Shared` contracts nếu cần; nếu khi Readback phát hiện thiếu cột DB buộc phải tạo EF Core Migration thì PHẢI DỪNG báo Control Tower duyệt trước)*

---

## 1. Problem & Goal (What & Why)

Thực thi **PR Lớn 1** theo `docs/MASTER_SYSTEM_SPEC.md` v2.0 (Mục 2, Mục 4, Mục 5 Luồng 2):
1. Đưa toàn bộ nghiệp vụ kiểm tra BIST, sức khỏe Drone, đọc thông số Flight Controller (FC) và **nhập thủ công thông số BIST / sức khỏe Drone + ghi chú kiểm định** về ngữ cảnh **Chi tiết Drone (`UAVs` / `DroneTechnicalInspections` / `DroneTechnicalMetrics`)** — chỉ `Technician` (trong vùng) và `SystemAdmin` (toàn hệ thống) được phép ghi; `Manager` & `Inspector` (trong vùng) chỉ được xem kết quả; `Analyst` bị từ chối `403`.
2. **Gỡ bỏ hoàn toàn** bước/ràng buộc bắt kiểm tra kỹ thuật BIST/FC bên trong luồng **Pre-Mission Assessment (MF01)**; Pre-Mission chỉ chọn các Drone đang có trạng thái kỹ thuật đạt chuẩn (`Ready` / `Đạt chuẩn / Sẵn sàng`).
3. Tại **MF01 (`PreMissionAssessments`)**, phần nhân sự **chỉ nhận và lưu SỐ LƯỢNG theo từng role** (`InspectorCount`, `AnalystCount`, `TechnicianCount` trong `PreMissionAssessments.Findings` (jsonb) — không tạo EF Core migration) — không yêu cầu và không bắt gán đích danh `UserId` tại MF01 (việc chọn đích danh 3 role thuộc MF02 ở PR Lớn 2).

---

## 2. Hard Constraints (`HC-xx` — Bất biến, Vi phạm = Fail)

- **HC-01**: `SystemAdmin` toàn quyền 100% và bỏ qua lọc `UserGeographicScopes`; các role khác lọc chặt theo `UserGeographicScopes` (`C-02`). Multi-role tính theo hợp (union) quyền.
- **HC-02**: Chỉ `Technician` và `SystemAdmin` được tạo/cập nhật kết quả kiểm tra BIST / FC / nhập tay sức khỏe Drone; `Manager` và `Inspector` chỉ đọc (`GET`); `Analyst` nhận `403 Forbidden` (`C-02`).
- **HC-03**: Zero Mock/Fake Data (`C-03`); không tạo dữ liệu cảm biến/FC giả ở BE (cả endpoint mới lẫn route legacy `basic-technical-check`); lưu thời gian `UTC` (`C-04`), trả 1 JSON envelope `camelCase` (`C-05`).
- **HC-04**: Code tuyến tính `if/return` tối đa 2 cấp lồng, tái sử dụng helper trong `Shared/`, kết thúc bằng "Tóm tắt luồng để bảo vệ code" 5–8 dòng tiếng Việt (`C-06`).
- **HC-05**: Tuân thủ khóa TDD `RED → LOCK (.claude/locked-tests.txt) → GREEN`; không sửa `specs/`, `constitution.md` hay test đã khóa (`C-07`).

---

## 3. Functional Requirements (Cú pháp EARS / GEARS)

> **Quy tắc**: Mọi yêu cầu phải gắn mã `FR-xxx` theo 4 mẫu EARS chuẩn để ánh xạ 1–1 sang Gate 5 `acceptance.md`.

- **FR-001 [Event — Manual & FC BIST Inspection & Partial Check]**: WHEN `Technician` (thuộc vùng của Drone) hoặc `SystemAdmin` gửi kết quả kiểm tra kỹ thuật Drone (từ thông số Serial/FC đọc được, route `basic-technical-check`, hoặc nhập thủ công các chỉ số BIST/pin/motor/sensor/GPS kèm ghi chú kiểm định và kết luận đạt/không đạt), THE `OperationsService` SHALL lưu bản ghi vào `DroneTechnicalInspections` / `DroneTechnicalMetrics` (UTC) và ánh xạ trạng thái kỹ thuật không cần migration: `conclusion = "Passed"` → `status = Passed`, `Uav.TechnicalHealth = Healthy`, `readiness = "Ready"`; `conclusion = "Failed"` → `status = Failed`, `Uav.TechnicalHealth = Critical`, `readiness = "MaintenanceRequired"`; mọi trạng thái `Uav.TechnicalHealth != Healthy` (`Unknown`/`Warning`/`Critical`) đều ánh xạ `readiness = "MaintenanceRequired"` và không đạt chuẩn cho MF01. **`[BRL-04]`**: Trên `basic-technical-check`, lưu trạng thái kết nối dạng `boolean` (`connected`), giữ nguyên toàn bộ số đo thực tế đã gửi (kể cả khi chỉ gửi `batteryVoltage` hoặc `batteryCellCount`), tuyệt đối không bịa `0%` cho trường thiếu, và nếu thiếu thông số bắt buộc thì đánh dấu chưa đạt/báo thiếu (`MaintenanceRequired`) để FE báo đỏ và cho phép nhập thêm thủ công; chỉ trả `400 METRICS_REQUIRED` khi không có bất kỳ dữ liệu kiểm tra nào.
- **FR-002 [Event — Read Drone Inspection Details & Eligible Drones]**: WHEN `SystemAdmin`, `Technician`, `Manager`, hoặc `Inspector` (hợp lệ theo `UserGeographicScopes`, trừ `SystemAdmin` toàn vùng) truy vấn chi tiết Drone, lịch sử/thông số BIST/FC mới nhất, hoặc `GET /api/v1/drones/eligible`, THE `OperationsService` SHALL kiểm tra quyền đọc và lọc chặt theo `UserGeographicScopes` (ngoại lệ `SystemAdmin`), trả thông tin trạng thái kỹ thuật, thông số FC/BIST và lịch sử kiểm định sắp xếp mới nhất lên đầu (`DESC`).
- **FR-003 [Error — Drone BIST/FC RBAC, Scope & FC Lock]**: IF user chỉ có role `Analyst`, hoặc `Technician`/`Manager`/`Inspector` thao tác ngoài `UserGeographicScopes` (kể cả `GET /drones/eligible` và thao tác ghép/hủy FC trên mọi Drone bị ảnh hưởng), hoặc `Manager`/`Inspector` gọi endpoint ghi BIST/FC, THEN THE `OperationsService` SHALL từ chối với `403 Forbidden` (hoặc `404` theo chuẩn scope hiện hữu của service) và không ghi DB. **`[BRL-02]`**: Khi ghép FC (`POST /api/v1/drones/{droneId}/flight-controller`), CẤM tự động gỡ FC đang `active` trên Drone khác (kể cả cùng vùng, kể cả `SystemAdmin`) — bắt buộc trả **`409 Conflict` (`FC_ALREADY_PAIRED`)** để ngắt (`DELETE`) ở Drone cũ trước; không tự điền `"Betaflight"` nếu client không truyền firmware.
- **FR-004 [Event — MF01 Remove In-Flow BIST, Select Ready Drones & Re-evaluate Prune]**: WHEN `Manager` (trong vùng) hoặc `SystemAdmin` tạo/cập nhật/duyệt `PreMissionAssessment` (MF01), THE `OperationsService` SHALL chỉ chấp nhận các Drone đang ở trạng thái kỹ thuật đạt chuẩn (`Ready`) **và thuộc đúng vùng của hồ sơ (`Uav.AssignedRegionId == assessment.RegionId`, áp dụng cho MỌI role kể cả `SystemAdmin`)**, đồng thời KHÔNG yêu cầu thực hiện bước chạy/lưu BIST mới bên trong hồ sơ Pre-Mission. **`[BRL-03]`**: Khi gọi `POST /api/v1/pre-mission-assessments/{id}/re-evaluate` **không truyền `droneIds[]` mới**, tự động loại bỏ các Drone đã lưu trước đó nếu bị chuyển vùng (`AssignedRegionId != assessment.RegionId`) hoặc mất trạng thái `Ready`, rồi tính lại kết quả sẵn sàng; đồng thời replay cùng `idempotencyKey` trên `POST /api/v1/pre-mission-assessments` PHẢI đối chiếu cả `droneIds[]` và `inspectorCount`/`analystCount`/`technicianCount`.
- **FR-005 [Event — MF01 Role Headcount Only]**: WHEN `Manager` (trong vùng) hoặc `SystemAdmin` tạo/cập nhật `PreMissionAssessment` (MF01), THE `OperationsService` SHALL nhận và lưu số lượng nhân sự cần thiết theo 3 role (`InspectorCount >= 1`, `AnalystCount >= 1`, `TechnicianCount >= 1` trong `PreMissionAssessments.Findings` (jsonb) — không tạo EF Core migration), kiểm tra tính khả dụng theo số lượng trong vùng/khung giờ (nếu có logic đối chiếu nguồn lực), và KHÔNG bắt buộc truyền danh sách `UserId` đích danh ở MF01.
- **FR-006 [Config — Pre-Mission Validity <= 7 Days]**: WHERE thời hạn hiệu lực của `PreMissionAssessment` được thiết lập hoặc kiểm tra, WHEN tạo/duyệt hồ sơ MF01, THE `OperationsService` SHALL áp trần hiệu lực tối đa `<= 7 ngày` (hoặc đọc `PreMissionMaxValidityDays` tối đa 7 ngày).
- **FR-007 [Error — Unready Drone, Region Mismatch, or Invalid Counts in MF01]**: IF danh sách Drone chọn trong MF01 chứa Drone `MaintenanceRequired`/không sẵn sàng, Drone khác vùng với hồ sơ (`Uav.AssignedRegionId != assessment.RegionId` hoặc chưa gắn vùng — áp dụng cả `SystemAdmin`), hoặc số lượng nhân sự âm / thiếu 1 trong 3 role bắt buộc (`Inspector`, `Analyst`, `Technician`), THEN THE `OperationsService` SHALL trả `400 Bad Request` (`INVALID_ROLE_HEADCOUNT`) cho lỗi số lượng nhân sự `< 1` hoặc thiếu/âm, `409 Conflict` (`DRONE_REGION_MISMATCH`) cho Drone khác vùng/chưa gắn vùng (kể cả `SystemAdmin`), và `409 Conflict` (`DRONE_NOT_READY`) cho Drone không `Ready`.

---

## 4. API / DB / SignalR / File Targets Contract

- **Target Files**: `Services/OperationsService/` (+ `Shared/UavPms.Shared.Contracts/` nếu có DTO dùng chung).
- **Database / Migration Impact**: Ưu tiên 100% dùng schema sẵn có (`UAVs`, `DroneFlightControllers`, `DroneTechnicalInspections`, `DroneTechnicalMetrics`, `PreMissionAssessments` với `Findings` jsonb, `PreMissionAssessmentDrones`). Không tự tạo EF Core migration.
- **REST Contract (Đã chốt)**:
  - **Ghi BIST / FC / nhập tay** (`SystemAdmin`, `Technician` trong scope):
    - `POST /api/v1/uavs/{droneId}/inspections`: body `{ "droneId": "guid?", "sourceType": "manual|fc|bist", "flightControllerUid": "string?", "conclusion": "Passed|Failed", "notes": "string?", "metrics": [...] }`, bỏ `preMissionAssessmentId`; response `DroneTechnicalInspectionDto` có `readiness` (`"Ready"` | `"MaintenanceRequired"`), `sourceType`, các mốc thời gian chuẩn `UTC Z`.
    - Giữ tạm các route cũ `POST /api/v1/drone-technical-inspections` và `POST /api/v1/drones/{id}/basic-technical-check` nhưng siết chặt cùng quyền `SystemAdmin + Technician` trong scope và bỏ `preMissionAssessmentId`; tại `basic-technical-check`, lưu `connected` dạng boolean, giữ số đo thực tế đã gửi (không bịa `0%`), thiếu chỉ số bắt buộc thì đánh dấu chưa đạt để báo đỏ & cho nhập thêm thủ công, chỉ trả `400 METRICS_REQUIRED` khi không có dữ liệu kiểm tra nào (`BRL-04`).
    - Siết `POST|DELETE /api/v1/drones/{droneId}/flight-controller` còn `SystemAdmin + Technician` trong scope; cấm tự động gỡ FC đang gắn trên Drone khác (`409 FC_ALREADY_PAIRED`) và để trống firmware khi không truyền (`BRL-02`).
  - **Đọc** (`SystemAdmin`, `Technician`, `Manager`, `Inspector`; `Analyst` `403`):
    - `GET /api/v1/uavs/{droneId}/inspections/latest`, `GET /api/v1/drones/{id}/technical-inspections?page&pageSize` (`DESC` theo `CompletedAt`), `GET /api/v1/drones/{droneId}/flight-controller` & `/flight-controller/history`, và `GET /api/v1/drones/eligible` (lọc chặt `UserGeographicScopes`, ngoại lệ `SystemAdmin`).
  - **MF01** (`SystemAdmin`, `Manager` trong scope):
    - `POST /api/v1/pre-mission-assessments` & `POST /api/v1/pre-mission-assessments/{id}/re-evaluate`: nhận `inspectorCount`, `analystCount`, `technicianCount` (`>= 1`), `droneIds[]` (chỉ Drone `Ready` cùng vùng `BRL-01`), không nhận `userId`; `idempotencyKey` đối chiếu cả `droneIds[]` và 3 headcount; khi `/re-evaluate` không truyền `droneIds[]` mới thì tự loại Drone lệch vùng/không còn `Ready` rồi tính lại (`BRL-03`); trần hiệu lực `min(PreMissionMaxValidityDays, 7)` ngày; response trả `inspectorCount`, `analystCount`, `technicianCount`, `validUntil`, `droneCandidates[]`.
- **RBAC Matrix**:
  - Drone BIST/FC ghi: `SystemAdmin` (toàn hệ thống), `Technician` (trong `UserGeographicScopes`).
  - Drone BIST/FC đọc: `SystemAdmin` (toàn hệ thống), `Technician`, `Manager`, `Inspector` (trong `UserGeographicScopes`).
  - Drone BIST/FC bị từ chối: `Analyst` (`403 Forbidden`), `Manager`/`Inspector` gọi ghi (`403 Forbidden`), ngoài scope (`403`/`404`).
  - MF01 Pre-Mission tạo/cập nhật/duyệt: `SystemAdmin` (toàn hệ thống), `Manager` (trong `UserGeographicScopes`).
- **OpenAPI / Handoff Sync**: Đã khóa hợp đồng sơ bộ vào `docs/contracts/HANDOFF.md`; khi xong, xác nhận lại trong khối `[DONE]` để Control Tower kiểm tra đồng bộ cho `FE-REQ-101`.

---

## 5. Out of Scope (Cấm AI tự ý thêm)

- Chưa sửa MF02 (chọn đích danh 3 role + 3/3 `Accept` + Check-in GPS `50m`) và MF03 (`/hubs/telemetry`) — thuộc **PR Lớn 2**.
- Không chạm `IdentityService`, `AIInspectionService`, `NotificationService`, `.github/workflows/**`.
- Không thêm endpoint, cột DB, hoặc cơ chế SRE ngoài phạm vi `FR-001..FR-007` đã đóng băng.
- Không sửa `specs/`, `.specify/memory/constitution.md`, hay test Gate 5 đã khóa trong pha `CODE`.
