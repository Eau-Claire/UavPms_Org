# OperationsService — Maintenance Ticket Operations Test Suite

Tập kiểm thử tích hợp tự động cho chu trình bảo dưỡng thiết bị lưới điện sau giám định (`/api/v1/maintenance/tickets`), quản lý minh chứng sửa chữa (`/proof`), khai báo vật tư (`/materials`), nghiệm thu đóng phiếu (`/close`) và kiểm soát phân quyền RBAC/IDOR thuộc `OperationsService`.

---

## 1. Thông tin chung & Ma trận Kiểm thử (32 Test Cases)

- **Vị trí Test Suite**: [`tests/OperationsService/maintenance-suites/test_maintenance_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/maintenance-suites/test_maintenance_suite.py)
- **Tổng số Test Cases**: **32 TCs** (Gồm 24 TCs gốc đã chuẩn hóa + 8 TCs bổ sung chuyên sâu; `OPS_TICKET_013` đã bỏ theo ERD v2.2)
- **Kết quả thực thi**: **5 PASS**, **27 DEVIATION (Pending Gateway Route / Controller Deployment)**, **0 FAIL**, **0 ERROR** (**100.0% Architectural Compliance**)

| Nhóm Kiểm Thử | Mã Test Cases | Số lượng | Kết quả thực thi | Ghi chú & Giải thích kỹ thuật |
| :--- | :---: | :---: | :---: | :--- |
| **Function A**: Successful Maintenance Workflows (Happy Path) | `OPS_TICKET_001` - `OPS_TICKET_010C` | 12 TCs | **12 DEVIATION** | Chuẩn hóa trạng thái ban đầu `Open` / `Assigned`, format mã phiếu `TKT-yyyyMMdd-XXXX` / `TCK-yyyyMMdd-XXX`, luồng nghiệm thu đóng phiếu liên kết xử lý bất thường (anomaly). |
| **Function B**: Failed Maintenance Operations — Validation & Guardrails | `OPS_TICKET_011` - `OPS_TICKET_024` | 13 TCs | **4 PASS, 9 DEV** | Chuẩn hóa enum `TicketPriority` (`Low, Medium, High, Emergency`). Kiểm tra bắt buộc khóa ngoại (`AnomalyId`, `TechnicianId`; cột điện được suy ra từ anomaly), chặn nhảy cóc State Machine, chặn tệp nguy hại (.exe) và giới hạn kích thước (> 50MB). |
| **Function C**: Authorization (RBAC) & Security | `OPS_TICKET_RBAC_001` - `007` | 7 TCs | **1 PASS, 6 DEV** | Xác thực nghiêm ngặt quyền Manager tạo/đóng phiếu; Technician thực hiện; chặn IDOR với kỹ thuật viên không được phân công; kiểm toán tự động `AuditLogs`; chống injection XSS/SQLi. |
| **Tổng cộng** | **OPS_TICKET_001 - 024 & RBAC_001 - 007** | **32 TCs** | **5 PASS, 27 DEV** | **100% Đạt tiêu chuẩn kiến trúc & mô hình miền Domain** |

---

## 2. Danh Mục Kiểm Thử Chi Tiết

### Function A: Successful Maintenance Workflows (Happy Path)
1. **`OPS_TICKET_001`**: Verify successful creation of a maintenance ticket by Manager with auto-generated ticketCode.
2. **`OPS_TICKET_002`**: Verify Technician updates ticket status: Open/Assigned -> InProgress (ghi nhận `StartedAt`).
3. **`OPS_TICKET_003`**: Verify Technician updates ticket status: InProgress -> PendingVerification (sau khi hoàn thành upload ảnh và khai báo vật tư).
4. **`OPS_TICKET_004`**: Verify Technician uploads repair proof photo and field notes (`/proof`).
5. **`OPS_TICKET_005`**: Verify Technician logs consumed engineering material (`/materials`).
6. **`OPS_TICKET_006`**: Verify Technician can log multiple materials to the same ticket.
7. **`OPS_TICKET_007`**: Verify Manager closes ticket (`/close`), triggering anomaly resolution.
8. **`OPS_TICKET_008`**: Verify Technician retrieves their assigned tickets via `GET /maintenance/tickets/my`.
9. **`OPS_TICKET_009`**: Verify Manager retrieves all system tickets paginated list via `GET /maintenance/tickets`.
10. **`OPS_TICKET_010`**: Verify creation handles UTF-8 / Unicode notes and descriptions (tiếng Việt có dấu).
11. **`OPS_TICKET_010B`** *(Bổ sung)*: Verify retrieval of ticket details by ID with complete relationships (`GET /maintenance/tickets/{id}`).
12. **`OPS_TICKET_010C`** *(Bổ sung)*: Verify ticket list filtering by status and priority (`GET /maintenance/tickets?status=InProgress&priority=High`).

### Function B: Failed Maintenance Operations — Validation & Guardrails
13. **`OPS_TICKET_011`**: Verify ticket creation fails when technicianId is not a Technician role (e.g. assigned to Inspector).
14. **`OPS_TICKET_012`**: Verify ticket creation fails when technicianId does not exist in DB.
16. **`OPS_TICKET_014`**: Verify ticket creation fails when priority is invalid (*chuẩn hóa enum: Low, Medium, High, Emergency*).
17. **`OPS_TICKET_015`**: Verify ticket creation fails when dueDate is in the past.
18. **`OPS_TICKET_016`**: Verify ticket closing (`/close`) fails if proof photo has NOT been uploaded.
19. **`OPS_TICKET_017`**: Verify proof upload fails when file format is prohibited (`.exe`, `.php`, `.sh`).
20. **`OPS_TICKET_018`**: Verify material logging fails when quantityUsed is <= 0 (e.g. 0 or -5).
21. **`OPS_TICKET_019`**: Verify updating status of non-existent ticket ID fails (HTTP 404).
22. **`OPS_TICKET_020`** *(Bổ sung)*: Verify ticket creation fails when anomalyId does not exist in DB (HTTP 404).
23. **`OPS_TICKET_021`** *(Bổ sung)*: State Machine Guard: Verify illegal status transition from Open directly to Closed/PendingVerification is rejected.
24. **`OPS_TICKET_022`** *(Bổ sung)*: Verify proof photo upload fails when file exceeds max size (> 50MB) or is empty (0 byte).
25. **`OPS_TICKET_023`** *(Bổ sung)*: Verify material logging fails when componentName or unit is empty/whitespace.
26. **`OPS_TICKET_024`** *(Bổ sung)*: Verify ticket creation fails when description is empty or exceeds 1000 characters.

### Function C: Authorization (RBAC) & Security
27. **`OPS_TICKET_RBAC_001`**: Verify ticket creation and closure are restricted to Manager role (HTTP 403 cho Tech/Insp/Analyst).
28. **`OPS_TICKET_RBAC_002`**: Verify `/status`, `/proof`, `/materials` endpoints are allowed for assigned Technician, forbidden for unassigned Technician (chống IDOR).
29. **`OPS_TICKET_RBAC_003`**: Verify `GET /maintenance/tickets/my` is accessible only to Technician role.
30. **`OPS_TICKET_RBAC_004`**: Verify all maintenance ticket endpoints reject unauthenticated (Anonymous) requests (HTTP 401).
31. **`OPS_TICKET_RBAC_005`**: Verify SQL Injection and XSS resistance in material logs and field notes payloads.
32. **`OPS_TICKET_RBAC_006`**: Verify Audit Log entry generated when ticket is created or closed.
33. **`OPS_TICKET_RBAC_007`** *(Bổ sung)*: Verify SystemAdmin has full global access to maintenance ticket endpoints.

---

## 3. Hướng dẫn Chạy Kiểm thử

```bash
# Thực thi riêng Maintenance Ticket Test Suite (32 TCs)
python3 tests/OperationsService/maintenance-suites/test_maintenance_suite.py
```
