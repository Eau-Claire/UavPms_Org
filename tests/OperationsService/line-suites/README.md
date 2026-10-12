# UAV PMS - Transmission Line Test Suite

Kiểm thử tích hợp cho phân hệ quản lý tuyến đường dây truyền tải điện (`/api/v1/lines`).

- **Script:** `test_line_suite.py`
- **Số lượng testcase:** 29 TCs (Tuyến đơn/kép, liên kết trạm đầu/cuối, tính toán chiều dài, RBAC).
- **Kết quả:** 20 PASS, 9 DEVIATIONS.

```bash
python3 tests/OperationsService/line-suites/test_line_suite.py
```
