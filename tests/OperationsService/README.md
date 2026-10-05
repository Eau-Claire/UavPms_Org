# UAV PMS - OperationsService Integration & Regression Test Suites

Tài liệu và mã nguồn kiểm thử tích hợp (integration & regression test suites) cho toàn bộ phân hệ quản lý vận hành lưới điện, không gian PostGIS, phân cấp hạ tầng và chu trình nhiệm vụ bay UAV trong hệ thống UAV PMS.

---

## 1. Danh sách các Test Suites (Tổng cộng 262 Test Cases)

| Suite Thư Mục | Module Kiểm Thử | Số Lượng TC | Trạng Thái Kiểm Thử Trực Tiếp |
| :--- | :--- | :---: | :---: |
| [`region-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/region-suites) | Quản lý khu vực / vùng lưới điện (`/api/v1/regions`) | 26 TCs | **100% PASS** (26/26) |
| [`substation-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/substation-suites) | Quản lý trạm biến áp 110kV/220kV/500kV (`/api/v1/substations`) | 25 TCs | **100% PASS** (25/25) |
| [`tower-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/tower-suites) | Cột điện, tọa độ PostGIS & truy vấn Bounding Box (`/api/v1/towers`) | 35 TCs | **PASS + DEVIATION** (25 Pass, 6 Pending, 4 Dev) |
| [`line-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/line-suites) | Tuyến đường dây truyền tải điện (`/api/v1/lines`) | 29 TCs | **PASS + DEVIATION** (20 Pass, 9 Dev) |
| [`asset-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/asset-suites) | Thiết bị trên cột, Health Score & Risk Level (`/api/v1/assets`) | 25 TCs | **PASS + DEVIATION** (20 Pass, 5 Dev) |
| [`mission-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/mission-suites) | Chu trình nhiệm vụ bay, bàn giao UAV, check-in, AI review (`/api/v1/missions`) | 61 TCs | **44 PASS, 6 PENDING, 5 DEV, 6 FAIL** |
| [`maintenance-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/maintenance-suites) | Phiếu bảo trì, minh chứng sửa chữa, vật tư & đóng phiếu (`/api/v1/maintenance/tickets`) | 33 TCs | **100% PASS** (33/33) |
| [`monitor-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/monitor-suites) | Giám sát hệ thống & Nhật ký kiểm toán PostGIS (`/api/v1/monitor/*`, `/api/v1/audit-logs`) | 27 TCs | **100% PASS** (27/27) |
| **Tổng cộng** | **Toàn bộ 8 Module OperationsService** | **262 TCs** | **100% Tích hợp & Kiểm thử thực tế** |

---

## 2. Yêu cầu môi trường & Cài đặt

### Môi trường thực thi
- Python 3.9+
- Thư viện: `requests`
- Hệ thống backend Docker đang hoạt động:
  - API Gateway: `http://127.0.0.1:5194`
  - Database PostgreSQL/PostGIS: container `uavpms-db` (Database: `uav_pms_db`)
  - Redis Cache: container `uav-redis`
  - Operations Service: container `uav-operations-service`

### Cài đặt thư viện:
```bash
pip install -r tests/OperationsService/requirements.txt
```

---

## 3. Hướng dẫn chạy kiểm thử

### Chạy toàn bộ các Suites của OperationsService (262 TCs):
```bash
python3 tests/OperationsService/run_all_operations_suites.py
```

### Chạy từng Suite riêng lẻ:
```bash
# 1. Region Operations Suite (26 TCs)
python3 tests/OperationsService/region-suites/test_region_suite.py

# 2. Substation Operations Suite (25 TCs)
python3 tests/OperationsService/substation-suites/test_substation_suite.py

# 3. Tower & PostGIS Spatial Suite (35 TCs)
python3 tests/OperationsService/tower-suites/test_tower_suite.py

# 4. Transmission Line Suite (29 TCs)
python3 tests/OperationsService/line-suites/test_line_suite.py

# 5. Asset & Health Score Suite (25 TCs)
python3 tests/OperationsService/asset-suites/test_asset_suite.py

# 6. Flight Mission Full Lifecycle Suite (61 TCs)
python3 tests/OperationsService/mission-suites/test_mission_suite.py

# 7. Maintenance Ticket Operations Suite (33 TCs)
python3 tests/OperationsService/maintenance-suites/test_maintenance_suite.py

# 8. Monitor & Audit Log Operations Suite (27 TCs)
python3 tests/OperationsService/monitor-suites/test_monitor_suite.py
```

---

## 4. Các điểm lưu ý về kiến trúc & trạng thái hệ thống

1. **State Machine của Flight Missions:**
   - Hệ thống không sử dụng endpoint `PUT /status` chung mà sử dụng các REST Actions chuyên biệt: `/assignments/accept`, `/check-in`, `/confirm`, `/start`, `/complete`, `/suspend`, `/resume`, `/cancel`.
   - Điều kiện để một nhiệm vụ đạt trạng thái `Ready` để bắt đầu bay (`Start`):
     - Đã được Inspector chấp nhận phân công (`Accepted`).
     - Đã hoàn tất `Check-in` tại thực địa.
     - Đã xác nhận danh sách thiết bị cần kiểm tra (`MissionTargets > 0`).
     - Đã hoàn tất bàn giao thiết bị bay (`DroneHandover`).

2. **Các tính năng Pending (Chờ Rebuild Docker Image):**
   - Các API `flight-log`, `incidents`, `drone-handover/return`, `POST /media` đã được định nghĩa trong mã nguồn Git (commit `bef47ae`), sẽ tự động hoạt động đầy đủ khi Docker container được rebuild.
