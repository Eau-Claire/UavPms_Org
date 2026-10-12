# UAV PMS - Authentication & Authorization Integration Test Suites

Tài liệu và mã nguồn kiểm thử tích hợp (integration & regression test suites) cho toàn bộ phân hệ xác thực, phân quyền và phiên làm việc của hệ thống UAV PMS.

---

## 1. Danh sách các Test Suites (Tổng cộng 147 Test Cases)

| Suite File | Module Kiểm Thử | Số Lượng TC | Trạng Thái |
| :--- | :--- | :---: | :---: |
| `test_login_suite.py` | Xác thực đăng nhập 2 bước (`POST /api/v1/auth/login` & OTP verify) | 32 TCs | **100% PASS** |
| `test_refresh_token_suite.py` | Cấp mới token & phát hiện đánh cắp phiên (`POST /api/v1/auth/refresh-token`) | 25 TCs | **100% PASS** |
| `test_send_otp_suite.py` | Gửi mã OTP xác thực đa mục đích (`POST /api/v1/auth/otp/send`) | 25 TCs | **100% PASS** |
| `test_verify_otp_suite.py` | Xác thực mã OTP & cấp StepUpToken (`POST /api/v1/auth/otp/verify`) | 25 TCs | **100% PASS** |
| `test_reset_password_suite.py` | Đặt lại mật khẩu với VerificationToken (`POST /api/v1/auth/reset-password`) | 20 TCs | **100% PASS** |
| `test_me_profile_suite.py` | Truy vấn hồ sơ tài khoản cá nhân (`GET /api/v1/users/me`) | 20 TCs | **100% PASS** |
| **Tổng cộng** | | **147 TCs** | **100% PASS** |

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
pip install -r tests/IdentityService/auth-suites/requirements.txt
```

---

## 3. Hướng dẫn chạy kiểm thử

### Chạy toàn bộ 147 test case:
```bash
python3 tests/IdentityService/auth-suites/run_all_suites.py
```

### Chạy từng suite riêng lẻ:
```bash
# 1. Login Suite (32 TCs)
python3 tests/IdentityService/auth-suites/test_login_suite.py

# 2. Refresh Token Suite (25 TCs)
python3 tests/IdentityService/auth-suites/test_refresh_token_suite.py

# 3. Send OTP Suite (25 TCs)
python3 tests/IdentityService/auth-suites/test_send_otp_suite.py

# 4. Verify OTP Suite (25 TCs)
python3 tests/IdentityService/auth-suites/test_verify_otp_suite.py

# 5. Reset Password Suite (20 TCs)
python3 tests/IdentityService/auth-suites/test_reset_password_suite.py

# 6. Get My Profile Suite (20 TCs)
python3 tests/IdentityService/auth-suites/test_me_profile_suite.py
```

---

## 4. Biến môi trường tùy chỉnh (Tùy chọn)

Nếu chạy kiểm thử trên server staging/CI/CD, bạn có thể thiết lập các biến môi trường:
- `GATEWAY_URL`: URL Gateway (mặc định: `http://127.0.0.1:5194`).
- `DB_CONTAINER`: Tên container PostgreSQL (mặc định: `uavpms-db`).
- `REDIS_CONTAINER`: Tên container Redis (mặc định: `uav-redis`).

Ví dụ:
```bash
GATEWAY_URL="http://staging-gateway.uavpms.local:5194" python3 tests/IdentityService/auth-suites/run_all_suites.py
```
