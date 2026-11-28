---
paths:
  - "Services/IdentityService/**"
  - "UavPms.ApiGateway/**"
  - "Services/*/*.API/Controllers/**"
  - "Services/*/*.API/Hubs/**"
  - "Services/OperationsService/**/*Scope*.cs"
  - "Services/AIInspectionService/**/*AccessPolicy*.cs"
  - "Shared/UavPms.Shared.Contracts/Constants/UserRoles.cs"
---

# Quy tắc Auth, RBAC, Phạm vi vùng & Gateway (BE)

1. `SystemAdmin` bỏ qua lọc `UserGeographicScopes` và có toàn quyền mọi endpoint nghiệp vụ + `/api/v1/system-settings`. Không tự gán `SystemAdmin` khi tạo user thiếu role (trả `400`).
2. Cấm tuyệt đối OTP bypass (`"123456"`), cấm trả OTP/secret/token/email thật vào logs hay error payload. Tạo user/đăng nhập thiết bị mới gửi OTP thật qua MailKit + TLS và lưu Redis.
3. Đổi mật khẩu / reset mật khẩu / khóa tài khoản phải thu hồi ngay toàn bộ `RefreshTokens` và `TrustedDevices` trong cùng transaction.
4. Ocelot Gateway tách route SignalR `/hubs/*/negotiate` (`http`) và WebSocket (`ws`), luôn enforce `[Authorize]` và không mở đường dẫn nội bộ (`/ai-service`, `/hangfire`).
5. Phạm vi vùng (spec §4 v2.1, chi tiết `docs/RBAC_PERMISSIONS.md`): dùng helper có sẵn, không viết lại luật ở chỗ mới.
   - OperationsService: `RegionScope` (vùng được gán + vùng con), `MissionScope` (nhiệm vụ phụ trách / được giao / thuộc vùng), `DroneScope` (Drone thuộc vùng / của nhiệm vụ được giao).
   - AIInspectionService: `IMissionAccessPolicy` (xem, duyệt, upload theo nhiệm vụ).
   - `Manager` không có ngoại lệ "thấy mọi vùng"; user chưa có vùng thấy rỗng.
   - Lọc trong SQL (`IQueryable`), không tải hết rồi lọc trong RAM. Từ chối bằng `ForbiddenException("<MÃ_CỤ_THỂ>")`.
6. Đổi role/attribute hay luật vùng → cập nhật cùng lúc `docs/RBAC_PERMISSIONS.md` và `docs/contracts/FE_HANDOFF_*.md` để FE làm theo.
