# UAV PMS - OperationsService Integration & Regression Test Suites

Tài liệu và mã nguồn kiểm thử tích hợp (integration & regression test suites) cho toàn bộ phân hệ quản lý vận hành lưới điện, không gian PostGIS, phân cấp hạ tầng và chu trình nhiệm vụ bay UAV trong hệ thống UAV PMS.

---

## 1. Danh sách các Test Suites (Tổng cộng 181 Test Cases)

> ERD v2.2: hạ tầng chỉ còn **khu vực → cột điện** (`regions` → `towers`). Các suite cũ cho trạm biến áp (`substation-suites`), tuyến đường dây (`line-suites`) và thiết bị/Health Score (`asset-suites`) đã bị xóa cùng các endpoint tương ứng.

| Suite Thư Mục | Module Kiểm Thử | Số Lượng TC | Trạng Thái Kiểm Thử Trực Tiếp |
| :--- | :--- | :---: | :---: |
| [`region-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/region-suites) | Quản lý khu vực / vùng lưới điện (`/api/v1/regions`) | 26 TCs | **100% PASS** (26/26) |
| [`tower-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/tower-suites) | Cột điện thuộc khu vực (`regionId`), tọa độ PostGIS & truy vấn không gian (`/api/v1/towers`, `/api/v1/towers/spatial-query`) | 35 TCs | Chưa chạy lại sau ERD v2.2 |
| [`mission-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/mission-suites) | Chu trình nhiệm vụ bay (tạo qua `/api/v2/pre-mission-assessments/{id}/create-mission`), bàn giao UAV, check-in, AI review (`/api/v1/missions`) | 60 TCs | Chưa chạy lại sau ERD v2.2 |
| [`maintenance-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/maintenance-suites) | Phiếu bảo trì, minh chứng sửa chữa, vật tư & đóng phiếu (`/api/v1/maintenance/tickets`) | 32 TCs | Chưa chạy lại sau ERD v2.2 |
| [`monitor-suites/`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/monitor-suites) | Giám sát hệ thống & Nhật ký kiểm toán PostGIS (`/api/v1/monitor/*`, `/api/v1/audit-logs`) | 27 TCs | **100% PASS** (27/27) |
| **Tổng cộng** | **5 Module trong runner** | **181 TCs** | |

Ngoài runner còn có `media-suites/` (50 TCs, `/api/v1/missions/{id}/media`, `towerId` tùy chọn) và `flightlog-suites/` (29 TCs), chạy riêng lẻ.

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

### Chạy toàn bộ các Suites của OperationsService (181 TCs):
```bash
python3 tests/OperationsService/run_all_operations_suites.py
```

### Chạy từng Suite riêng lẻ:
```bash
# 1. Region Operations Suite (26 TCs)
python3 tests/OperationsService/region-suites/test_region_suite.py

# 2. Tower & PostGIS Spatial Suite (35 TCs)
python3 tests/OperationsService/tower-suites/test_tower_suite.py

# 3. Flight Mission Full Lifecycle Suite (60 TCs)
python3 tests/OperationsService/mission-suites/test_mission_suite.py

# 4. Maintenance Ticket Operations Suite (32 TCs)
python3 tests/OperationsService/maintenance-suites/test_maintenance_suite.py

# 5. Monitor & Audit Log Operations Suite (27 TCs)
python3 tests/OperationsService/monitor-suites/test_monitor_suite.py

# Ngoài runner: Media Ingestion (50 TCs) và Flight Log (29 TCs)
python3 tests/OperationsService/media-suites/test_media_suite.py
python3 tests/OperationsService/flightlog-suites/test_flightlog_suite.py
```

---

## 4. Các điểm lưu ý về kiến trúc & trạng thái hệ thống

1. **State Machine của Flight Missions:**
   - Hệ thống không sử dụng endpoint `PUT /status` chung mà sử dụng các REST Actions chuyên biệt: `/assignments/accept`, `/check-in`, `/confirm`, `/start`, `/complete`, `/suspend`, `/resume`, `/cancel`.
   - Điều kiện để một nhiệm vụ đạt trạng thái `Ready` để bắt đầu bay (`Start`):
     - Đã được Inspector chấp nhận phân công (`Accepted`).
     - Đã hoàn tất `Check-in` tại thực địa.
     - Đã xác nhận danh sách cột điện cần kiểm tra (`MissionTargets > 0`, lấy từ hồ sơ tiền nhiệm vụ hoặc `PUT /missions/{id}/towers`).
     - Đã hoàn tất bàn giao thiết bị bay (`DroneHandover`).

2. **Các tính năng Pending (Chờ Rebuild Docker Image):**
   - Các API `flight-log`, `incidents`, `drone-handover/return`, `POST /media` đã được định nghĩa trong mã nguồn Git (commit `bef47ae`), sẽ tự động hoạt động đầy đủ khi Docker container được rebuild.
