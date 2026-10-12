# UAV PMS - Tower & PostGIS Spatial Test Suite

Kiểm thử tích hợp cho phân hệ quản lý cột điện, tọa độ PostGIS Point WGS84 và truy vấn không gian Bounding Box (`/api/v1/towers`).

- **Script:** `test_tower_suite.py`
- **Số lượng testcase:** 35 TCs (PostGIS conversion, truy vấn `in-bbox`, chuỗi cột dọc tuyến, RBAC).
- **Kết quả:** 25 PASS, 6 PENDING, 4 DEVIATIONS.

```bash
python3 tests/OperationsService/tower-suites/test_tower_suite.py
```
