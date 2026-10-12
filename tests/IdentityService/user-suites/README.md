# UAV PMS - User Management Integration Test Suites

Tài liệu và mã nguồn kiểm thử tích hợp (integration & regression test suites) cho toàn bộ phân hệ quản lý người dùng (User Management) của hệ thống UAV PMS.

---

## 1. Danh sách các Test Suites (Tổng cộng 80 Test Cases)

| Suite File | Module Kiểm Thử | Số Lượng TC | Trạng Thái |
| :--- | :--- | :---: | :---: |
| `test_user_create_suite.py` | Tạo người dùng mới, gán Role, kiểm tra Audit Log (`POST /api/v1/users`) | 25 TCs | **100% PASS** |
| `test_user_list_suite.py` | Danh sách phân trang, tìm kiếm & chi tiết người dùng (`GET /api/v1/users`, `GET /api/v1/users/{id}`) | 20 TCs | **100% PASS** |
| `test_user_update_suite.py` | Cập nhật thông tin, trạng thái, vai trò & RBAC (`PUT /api/v1/users/{id}`) | 20 TCs | **100% PASS** |
| `test_user_assign_suite.py` | Danh sách người dùng có thể phân công (`GET /api/v1/users/assignable`) | 15 TCs | **100% PASS** |
| **Tổng cộng** | | **80 TCs** | **100% PASS** |

---

## 2. Yêu cầu môi trường & Cài đặt

### Môi trường thực thi
- Python 3.9+
- Thư viện: `requests`
- Hệ thống backend Docker đang hoạt động:
  - API Gateway: `http://127.0.0.1:5194`
  - Database PostgreSQL: container `uavpms-db` (Database: `uav_pms_db`)
  - Redis Cache: container `uav-redis`

### Cài đặt thư viện:
```bash
pip install -r tests/IdentityService/user-suites/requirements.txt
```

---

## 3. Hướng dẫn chạy kiểm thử

### Chạy toàn bộ 80 test case:
```bash
python3 tests/IdentityService/user-suites/run_all_suites.py
```

### Chạy từng suite riêng lẻ:
```bash
# 1. User Creation Suite (25 TCs)
python3 tests/IdentityService/user-suites/test_user_create_suite.py

# 2. User Listing & Detail Suite (20 TCs)
python3 tests/IdentityService/user-suites/test_user_list_suite.py

# 3. User Update Suite (20 TCs)
python3 tests/IdentityService/user-suites/test_user_update_suite.py

# 4. User Assignable Suite (15 TCs)
python3 tests/IdentityService/user-suites/test_user_assign_suite.py
```

---

## 4. Biến môi trường tùy chỉnh (Tùy chọn)

Nếu chạy kiểm thử trên server staging/CI/CD, bạn có thể thiết lập các biến môi trường:
- `GATEWAY_URL`: URL Gateway (mặc định: `http://127.0.0.1:5194`).
- `DB_CONTAINER`: Tên container PostgreSQL (mặc định: `uavpms-db`).

Ví dụ:
```bash
GATEWAY_URL="http://staging-gateway.uavpms.local:5194" python3 tests/IdentityService/user-suites/run_all_suites.py
```
