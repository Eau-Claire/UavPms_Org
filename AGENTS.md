# UAV-PMS Backend — Hướng dẫn làm việc

Repo BE: `D:/UavPms_BEE`, .NET 9. Áp dụng từ gốc repo.

## Nguồn sự thật, Hiến pháp & Rào chắn SDD

- `docs/MASTER_SYSTEM_SPEC.md` (v2.0) & `docs/contracts/HANDOFF.md` — nguồn sự thật đồng bộ FE/BE *(cấm dùng doc cũ đã gắn banner `DEPRECATED`)*.
- `.specify/memory/constitution.md` — 7 bất biến kỹ thuật (`C-01..C-07`); `CLAUDE.md` & `.claude/hooks/` khóa ghi (`SDD_PHASE=SPEC|RED|CODE`, `.claude/locked-tests.txt`) + `.codex/config.toml`.
- `specs/` — mẫu EARS/GEARS (`specs/_template/`) & ma trận `specs/RTM.md`. Khi nhận `BE-REQ`, chỉ đọc spec/acceptance, code và test của đúng batch được giao.
- Nạp `.agents/skills/readable-code-defense/SKILL.md` trước khi viết/review code; Judge nạp `.agents/skills/pr-review-judge/SKILL.md`.
- Gate 5 là danh sách test đã thống nhất. Với Tier 1: viết RED → khóa vào `.claude/locked-tests.txt` → mới code GREEN. `confirmed` ≠ test đã chạy.

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

## Small Batches v2.0 và code dễ bảo vệ

1. **Mainflow / Demo First**: Luôn hỏi deadline gần nhất và kiểm chứng lỗi trên hệ thống thật trước khi ưu tiên CI/YAML hay refactor phụ. Xác nhận phạm vi, Gate 5 (`acceptance.md`) và đảm bảo working tree sạch WIP ngoài phạm vi.
2. **Vertical Slice**: Gom 1 lỗi P0 hoặc 1 tính năng trọn vẹn thành 1 slice (~250–450 dòng diff; refactor cơ học/xóa dead code được linh hoạt theo domain) và chấm **1 lượt Independent Judge** cho mỗi slice.
3. **Chạy test trúng đích khi lặp**: Trong lúc sửa chỉ chạy `dotnet test` đúng project liên quan; chỉ chạy `dotnet build UavPms.sln -c Release` đúng **1 lần chốt cuối** trước khi báo cáo. Không search web hay dựng fixture bash/Docker nặng cho batch C#.
4. **Test tự chạy được trong `dotnet test`**: Test Gate 5 mặc định phải chạy qua pipeline ASP.NET Core thật (`WebApplicationFactory<Program>` / in-process HTTP + JWT thật) và pass trong `dotnet test UavPms.sln` (không phụ thuộc URL server chạy sẵn ngoài môi trường làm đỏ CI); các probe live multi-service tách riêng category.
5. **Risk-Tiered Pragmatic Judge (Anti-Banking Ceiling)**: Nạp `.agents/skills/pr-review-judge/SKILL.md`. Tier 1 (Auth/RBAC/Ocelot/SignalR — soi chặt bảo mật thật & test integrity); Tier 2 (CRUD/refactor — 1 lượt thực dụng, max 3 non-blocking); Tier 3 (CI/YAML — `actionlint` + chặn secret/injection/false-green, **cấm over-engineering SRE** như `pg_hba.conf` hay cluster DB thứ 2). Nhắm tối đa **1 vòng sửa** và tôn trọng quyết định accept-risk của Human Owner.
6. Reset chat Coding/Judge ngay khi đổi domain (Workflow → Notification → Auth) để giữ context sạch. Dừng sau mỗi slice chờ review.

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
