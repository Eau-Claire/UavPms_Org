# UAV-PMS Backend — Hướng dẫn làm việc

Repo BE: `D:/UavPms_BEE`, .NET 9. Áp dụng từ gốc repo.

## Nguồn sự thật và phạm vi

- `D:/UavPms_FEE/docs/MASTER_SYSTEM_SPEC.md` xác định nghiệp vụ và kiến trúc.
- `D:/UavPms_FEE/docs/contracts/HANDOFF.md` ghi hợp đồng và bàn giao BE/FE.
- Khi nhận mã BE-REQ và batch, đọc spec, acceptance và checklist của đúng yêu cầu.
- Chỉ mở file triển khai và test cần cho batch đã được giao.
- Nạp `.agents/skills/readable-code-defense/SKILL.md` trước khi viết hoặc review code.
- Gate 5 là danh sách test đã thống nhất. `confirmed` không có nghĩa test đã chạy.

## Bản đồ kiến trúc

| Thành phần | Đường dẫn từ gốc | Trách nhiệm |
| --- | --- | --- |
| IdentityService | `Services/IdentityService/` | JWT, RBAC, user/scope, OTP và phiên |
| OperationsService | `Services/OperationsService/` | Tài sản, mission, telemetry, báo cáo; sở hữu migration EF Core |
| AIInspectionService | `Services/AIInspectionService/` | Media, phân tích và review; kết nối FastAPI worker qua RabbitMQ |
| NotificationService | `Services/NotificationService/` | Thông báo, SignalR, email và Hangfire nội bộ |
| InspectionEvaluationService | `Services/InspectionEvaluationService/` | Chấm kiểm tra kỹ thuật qua gRPC nội bộ |
| Ocelot API Gateway | `UavPms.ApiGateway/` | Routing HTTP/SignalR, xác thực/rate limit và Swagger gộp |

- Các lớp API, Application, Domain và Infrastructure nằm trong từng service.
- Hợp đồng dùng chung ở `Shared/UavPms.Shared.Contracts/`.
- REST công khai đi qua Gateway. Nội bộ dùng RabbitMQ hoặc gRPC theo đặc tả.
- PostgreSQL/PostGIS giữ dữ liệu nghiệp vụ, Redis giữ OTP/cache và S3 giữ media.
- Dữ liệu ngày giờ trong BE lưu UTC, client chịu trách nhiệm hiển thị múi giờ.

## Quyền thực thi và bảo mật

- **Human-Approval-Required:** vùng IdentityService/Auth, gồm user/role, OTP, JWT,
  password, session và thu hồi token, cùng vùng `.github/workflows/**`.
- Xác nhận approval bao phủ batch hiện tại trước khi sửa các vùng này.
- Mọi apply EF Core migration phải đi qua GitHub Actions. OperationsService sở hữu
  model, snapshot và migration của shared DB.
- Production apply cần approval riêng sau rehearsal. Không chạy tay SQL DROP.
- Zero Mock/Fake Data, Zero OTP Bypass. Gate 5 dùng hạ tầng thật, cô lập theo test.
- PR CI cho OTP được dùng SMTP container Mailpit qua giao thức SMTP thật.
- Staging/Production dùng MailKit + TLS với SMTP thật và secret theo môi trường.
- Không log JWT, OTP, password, email thật, SMTP/SSH secret hay raw payload nhạy cảm.
- Log theo mã lỗi và correlation ID. Lọc log, TRX, artifact và output của Actions.
- Quyền user và phạm vi truy cập lấy từ JWT đã xác thực và dữ liệu phía server.
- Kiểm tra ownership/scope tại nơi đọc hoặc ghi. Từ chối khi thiếu claim bắt buộc.

## Small Batches và code dễ bảo vệ

1. Xác nhận phạm vi, tiêu chí acceptance và thứ tự batch trước khi sửa.
2. Giữ mỗi batch tối đa 350 dòng diff (thêm + xóa, gồm test và tài liệu).
3. Tách stacked PR nếu vượt trần; định rõ ranh giới và điều kiện rollout.
4. Chạy kiểm chứng của batch. Batch đổi service phải chạy test project tương ứng.
5. Ghi kết quả thực tế, tick checklist khi có và bàn giao phần đã hoàn thành.
6. Dừng sau mỗi batch. Chờ Control Tower / Human review rồi mới mở batch sau.

- Luồng `if/return` đi thẳng, guard clause sớm, lồng `if` tối đa hai cấp.
- Không nested ternary, magic string/number hoặc helper che khuất luồng.
- Dùng kiểu, hằng, helper và quy ước sẵn có khi hợp nghiệp vụ.
- Comment ngắn bằng tiếng Việt, giải thích TẠI SAO cần quy tắc hoặc xử lý biên.
- Kết thúc báo cáo bằng **Tóm tắt luồng để bảo vệ code** (5–8 dòng tiếng Việt).

## Build, test và bàn giao

Chạy từ gốc repo với .NET 9 SDK:

```powershell
dotnet restore UavPms.sln
dotnet build UavPms.sln -c Release --no-restore
dotnet test UavPms.sln -c Release --no-build
dotnet test Services/IdentityService/UavPms.IdentityService.Tests/UavPms.IdentityService.Tests.csproj
dotnet test Services/NotificationService/UavPms.NotificationService.Tests/UavPms.NotificationService.Tests.csproj
dotnet test Services/OperationsService/UavPms.OperationsService.IntegrationTests/UavPms.OperationsService.IntegrationTests.csproj
```

- Ánh xạ test và lệnh theo project nằm trong `acceptance.md` của yêu cầu.
- Nếu thiếu hạ tầng, báo ca chưa chạy hoặc lỗi thiết lập; không đánh dấu Pass.
- Batch đổi REST chạy script export với URL Gateway thật. Dùng PowerShell 7 hoặc
  Windows PowerShell 5.1 có Node.js để kiểm cú pháp JSON nghiêm ngặt:

```powershell
./scripts/export-openapi.ps1 -GatewayBaseUrl http://localhost:5194
```

- Lệnh trên chỉ kiểm tra. Khi được phép xuất file, thêm
  `-OutputPath 'D:/UavPms_FEE/docs/contracts/openapi.json'`.
- Script chỉ ghi đè file đã có khi người chạy truyền `-ReplaceExisting`.
- Chỉ xuất bản OpenAPI v1.0 ở Đợt 1. Khi thay hợp đồng, ghi bàn giao FEE theo phạm vi đã duyệt.
