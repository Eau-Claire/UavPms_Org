# UAV PMS - Flight Mission Lifecycle & Field Operations Test Suite

Tài liệu và mã nguồn kiểm thử tự động cho toàn bộ vòng đời nhiệm vụ bay, bàn giao UAV, check-in GPS thực địa, upload telemetry log, xử lý sự cố và phân quyền bảo mật.

---

## 1. Thông tin tổng quan (61 Test Cases)

- **Đường dẫn script:** `tests/OperationsService/mission-suites/test_mission_suite.py`
- **Số lượng test cases:** 61 Test Cases
- **Nhóm chức năng:**
  - **Function A: Successful Mission Operations (Happy Path - Lifecycle & Core Flow)** (15 TCs: `OPS_MISSION_001` → `015`)
  - **Function B: Field Operations — Check-in, Drone Handover, Flight Logs & Telemetry** (10 TCs: `OPS_MISSION_016` → `025`)
  - **Function C: Scope, Targets & Multi-Inspector Assignments** (9 TCs: `OPS_MISSION_026` → `034`)
  - **Function D: Failed Operations — Validation, State Machine & Scheduling Conflicts** (17 TCs: `OPS_MISSION_035` → `051`)
  - **Function E: Authorization (RBAC) & Security** (10 TCs: `OPS_MISSION_RBAC_001` → `010`)

---

## 2. Kết quả kiểm thử trực tiếp trên môi trường Docker

- **PASS:** 44 / 61 TCs (72.1%)
- **PENDING:** 6 / 61 TCs (9.8% - Các endpoint `flight-log`, `incidents`, `drone-handover/return`, `media` đã commit ở `bef47ae`, chờ rebuild Docker image).
- **DEVIATIONS:** 5 / 61 TCs (8.2%)
- **FAIL:** 6 / 61 TCs (9.8% - Gồm `004`, `005` do `Confirm` reset trạng thái về `Assigned`; `035`, `038` do thiếu validate tồn tại tài khoản/drone liên service; `043`, `044` do container hiện tại chưa có validation tọa độ GPS).

---

## 3. Cách chạy kiểm thử

```bash
python3 tests/OperationsService/mission-suites/test_mission_suite.py
```
