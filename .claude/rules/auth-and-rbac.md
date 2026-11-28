---
paths:
  - "Services/IdentityService/**"
  - "UavPms.ApiGateway/**"
---

# Quy tắc Auth, RBAC & Gateway (BE)

1. `SystemAdmin` bỏ qua lọc `UserGeographicScopes` và có toàn quyền mọi endpoint nghiệp vụ + `/api/v1/system-settings`. Không tự gán `SystemAdmin` khi tạo user thiếu role (trả `400`).
2. Cấm tuyệt đối OTP bypass (`"123456"`), cấm trả OTP/secret/token/email thật vào logs hay error payload. Tạo user/đăng nhập thiết bị mới gửi OTP thật qua MailKit + TLS và lưu Redis.
3. Đổi mật khẩu / reset mật khẩu / khóa tài khoản phải thu hồi ngay toàn bộ `RefreshTokens` và `TrustedDevices` trong cùng transaction.
4. Ocelot Gateway tách route SignalR `/hubs/*/negotiate` (`http`) và WebSocket (`ws`), luôn enforce `[Authorize]` và không mở đường dẫn nội bộ (`/ai-service`, `/hangfire`).
