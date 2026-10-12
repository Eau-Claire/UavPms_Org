# UAV PMS - Asset & Health Score Test Suite

Kiểm thử tích hợp cho phân hệ quản lý thiết bị gắn trên cột điện, chỉ số sức khỏe Health Score và đánh giá mức độ rủi ro Risk Level (`/api/v1/assets`).

- **Script:** `test_asset_suite.py`
- **Số lượng testcase:** 25 TCs (Insulator, Cable, CrossArm, cập nhật Health Score, suy diễn Risk Level, RBAC).
- **Kết quả:** 20 PASS, 5 DEVIATIONS.

```bash
python3 tests/OperationsService/asset-suites/test_asset_suite.py
```
