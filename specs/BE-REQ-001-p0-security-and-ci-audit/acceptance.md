# BE-REQ-001 — Gate 5: Agreed Test List

**Trạng thái:** `confirmed` — Control Tower và Human Owner đã duyệt tiêu chí. Kết quả từng ca sẽ được ghi sau khi thực thi.
**Approved Date:** 2026-11-26

1. **OTP:** MailKit + TLS, không OTP bypass hay bỏ qua lỗi gửi. PR CI kiểm thử SMTP thật qua container Mailpit cô lập, không cần secret production. Staging/Production xác minh với SMTP thật qua secret.
2. **REST:** Login thiết bị mới trả `data.otpRequired = true`. `POST /api/v1/users` nhận `geographicScopeIds` tùy chọn (`Guid[]` ID Region hợp lệ), bỏ qua/null/[] không gán scope, ID sai trả `400`. Lỗi Redis/SMTP sau commit trả `503 OTP_DELIVERY_FAILED`; user giữ `IsEmailVerified=false`, resend qua `/api/v1/auth/otp/send`, retry CreateUser không tạo trùng.
3. **CI/EF:** Tách EF và gỡ hai câu DDL khỏi `deploy.yml`, khóa workflow migration cũ, loại log email thật khỏi `assign-production-gis-scope.yml`. `ef-core-migrations.yml` rehearsal trên DB cô lập trước, production apply yêu cầu Human duyệt riêng.
4. **Quyền thực thi:** Đã duyệt IdentityService/Auth A1–A7 và `.github/workflows/**` C1–C3 cho CI và rehearsal. Chưa phê duyệt apply production.

## 1. Ánh xạ `dotnet test` và môi trường

Mỗi dòng Examples là một theory row hoặc test riêng. Dùng trait `Gate5=<mã nhóm>` để lọc. Chạy project thật dưới đây sau batch code và chạy `dotnet test UavPms.sln -c Release` trong CI. Tên project Gateway là **đề xuất mới** vì hiện solution chưa có Gateway tests. Không dùng kết quả của test mock hiện có thay cho Gate 5.

| Mã nhóm | Lệnh chạy dự kiến từ gốc BE |
| --- | --- |
| `P0-1`, `P0-2`, `CU` | `dotnet test Services/IdentityService/UavPms.IdentityService.Tests/UavPms.IdentityService.Tests.csproj --filter "Gate5=P0-1\|Gate5=P0-2\|Gate5=CU"` |
| `P0-3`, `P0-4` | `dotnet test Services/NotificationService/UavPms.NotificationService.Tests/UavPms.NotificationService.Tests.csproj --filter "Gate5=P0-3\|Gate5=P0-4"` |
| `GW` | `dotnet test UavPms.ApiGateway.Tests/UavPms.ApiGateway.Tests.csproj --filter "Gate5=GW"` |
| `OPS` | `dotnet test Services/OperationsService/UavPms.OperationsService.IntegrationTests/UavPms.OperationsService.IntegrationTests.csproj --filter "Gate5=OPS"` |
| `AI` | `dotnet test Services/AIInspectionService/UavPms.AIInspectionService.Tests/UavPms.AIInspectionService.Tests.csproj --filter "Gate5=AI"` |

Given các service chạy qua pipeline HTTP/SignalR thật trên mạng cô lập, JWT do Identity cấp, PostgreSQL/PostGIS, Redis, RabbitMQ và SMTP nhận email qua giao thức thật (Mailpit cô lập ở PR CI, SMTP server và mailbox được duyệt ở staging). Không fake Auth, EF InMemory, stub SMTP, OTP cố định để đăng nhập, seed dữ liệu giả hay gọi production trong test. Tài khoản/notification dùng cho test được tạo qua luồng thật, tách biệt theo lần chạy. Nếu thiếu hạ tầng bắt buộc, job phải fail. Đọc OTP từ thư tại runtime; TRX và log chỉ chứa ID ca/metadata đã lọc. Bằng chứng CI gồm số test chạy, pass/fail/skip và commit SHA.

## 2. IdentityService

### P0-1 — Proof và thu hồi đồng bộ

**Scenario Outline P01-A — `POST /api/v1/users/change-password` kiểm đúng bằng chứng**

Given A có access JWT và đang hoạt động, mật khẩu mới hợp lệ; nếu cần, step-up cấp qua OTP thật với purpose `ChangePassword`.
When A gửi request với `<proof>`.
Then API trả `<status>`; chỉ nhánh thành công thay hash và thu hồi theo P01-B; nhánh lỗi giữ nguyên hash, refresh và trust của A.

| Examples: proof | Status |
| --- | --- |
| Không JWT, bất kể có proof | 401 |
| Có JWT, bỏ qua/null/rỗng cả hai proof | 400 |
| Mật khẩu cũ đúng; không header, rồi header sai | 200 (mỗi ca độc lập) |
| Step-up đúng; không oldPassword, rồi oldPassword sai | 200 (mỗi ca độc lập) |
| Mật khẩu cũ sai hoặc cả hai sai | 401 |
| Step-up hết hạn, đã dùng, khác user, sai purpose hoặc không khớp Redis | 401 (mỗi loại một ca) |
| Mật khẩu mới thiếu hoặc rỗng dù proof đúng | 400 |

**Scenario Outline P01-B — Revocation theo từng entry point**

Given A có refresh đang hoạt động và trusted device còn hạn trên nhiều thiết bị; B cũng có cả hai; lấy trạng thái PostgreSQL trước thao tác.
When thực hiện thành công một trong `<đường dẫn>`.
Then ngay sau HTTP 2xx, không còn refresh/trust còn hoạt động của A trong PostgreSQL. Gọi `/auth/refresh-token` và `/auth/validate-session` bằng từng refresh cũ bị từ chối. Với A còn hoạt động và mật khẩu mới/được reset, mọi trust token cũ đều dẫn đến OTP mới; nếu A bị khóa thì login bị từ chối và mở khóa không hồi sinh trust/refresh cũ. B không bị đổi dữ liệu hoặc mất phiên (kiểm cả refresh và login bằng trust còn hạn).

| Examples: đường dẫn (prefix `/api/v1`) | Điều kiện |
| --- | --- |
| `POST /users/change-password` | Chủ tài khoản có bằng chứng hợp lệ |
| `POST /auth/reset-password` | Verification token thật từ OTP đúng purpose |
| `POST /users/{id}/reset-password` | SystemAdmin, mật khẩu tạm xử lý trong bộ nhớ |
| `POST /users/{id}/suspend`, `DELETE /users/{id}` | SystemAdmin, từng route một |
| `PUT /users/{id}`, `PATCH /users/{id}` | SystemAdmin, từng lối chuyển A từ Active sang Inactive/Suspended được hỗ trợ |

**Scenario P01-C — Từ chối và race:** Given reset proof sai/hết hạn, caller thiếu quyền hoặc khóa A đang gắn mission chưa kết thúc, when thao tác bị từ chối, then không đổi hash/trạng thái hay dữ liệu phiên. Given hai request chỉ dùng cùng một step-up, when thực thi đồng thời, then tối đa một request thành công; proof đã dùng không tái sử dụng. Given refresh/verify OTP login chạy đồng thời với đổi/reset/khóa, when kiểm DB sau commit, then phiên/trust dựa trên bằng chứng trước thay đổi không còn sử dụng được; challenge login phát trước thay đổi không cấp phiên mới. Given ghi transaction thất bại trên PostgreSQL, then không trả 2xx và hash, trạng thái, refresh/trust cùng rollback; trường hợp không xác định trạng thái commit không báo thành công.

### P0-2 — Chỉ OTP do Redis + SMTP phát hành

**Scenario Outline P02-A — Login thiết bị mới hoặc trust không hợp lệ**

Given email đã xác minh, mật khẩu đúng, chạy riêng trên Development, Staging và cấu hình Production cô lập.
When thiết bị login với trust `<loại>`.
Then `200` có `data.otpRequired=true`, không JWT/refresh/trust mới; Redis đã có hash đúng purpose với TTL, mailbox thực nhận được thư mới. Khi xác thực mã mới đủ điều kiện, Identity cấp access/refresh và trust khi luồng cho phép; replay không cấp thêm phiên.

Examples: không cung cấp trust | không khớp | hết hạn | thuộc user khác | đã thu hồi. Kiểm các đường truyền trust qua body/cookie/header. Control: trust hợp lệ của chính user + mật khẩu đúng vẫn login trực tiếp; mật khẩu sai hoặc email chưa xác minh không cấp phiên theo nhánh thiết bị.

**Scenario P02-B — Không bypass, giới hạn và cô lập:** Given từng môi trường có hoặc thiếu cấu hình `Otp:BypassCode` cũ trong DB, when kiểm mã cố định `123456` không có challenge hiện hành hoặc challenge chứa hash mã khác, then verify từ chối cho mọi purpose (Login, EmailVerification, ForgotPassword, ChangePassword, ChangeEmail, DeleteAccount); key cũ không dùng được sau rollout. Kiểm mã sai, email khác, purpose khác, hết TTL, replay và verify đồng thời; chỉ mã khớp challenge hiện hành mới được chấp nhận một lần. Sau số lần sai tối đa challenge không dùng được; trong cooldown không phát thêm mail; khi resend hợp lệ mã cũ mất hiệu lực. Qua Operations, key legacy không hiện trong settings và không ghi lại được (`OPS`).

**Scenario P02-C — Hạ tầng lỗi:** Given Redis unavailable hoặc provider SMTP từ chối, when login/resend yêu cầu OTP, then báo lỗi rõ (`503` đề xuất), không phát JWT, không báo OTP đã gửi và không để challenge lỗi xác thực thành công. When kết nối được phục hồi và người dùng yêu cầu lại theo quy tắc cooldown, then nhận thư thật mới và xác thực được. Kiểm read-only log/TRX không chứa OTP, email, JWT, password, cookie hay body từ SMTP.

### CU — CreateUser (SystemAdmin, multi-role, scope, email)

**Scenario Outline CU-A — Quyền và validation**

Given gọi `POST /api/v1/users` với dữ liệu còn lại hợp lệ.
When gửi `<trường hợp>`.
Then trả `<status>`; với mọi 4xx, không có bản ghi user/role/scope mới, challenge Redis hoặc thư OTP cho request.

| Examples: trường hợp | Status |
| --- | --- |
| Ẩn danh hoặc JWT không hợp lệ | 401 |
| Manager/Inspector/Analyst/Technician, kể cả tổ hợp không có SystemAdmin | 403 |
| SystemAdmin, roles bị thiếu/null/[] | 400 từng ca |
| Giá trị lạ, null/rỗng trong danh sách, alias Admin, sai chính tả/casing hoặc trộn một role đúng với một role sai | 400 từng ca |
| Role chuẩn không có bản ghi trong DB theo precondition test cô lập | 400 |
| Mật khẩu thiếu/trống hoặc scope sai kiểu/không tồn tại/đã xóa | 400 từng ca |
| SystemAdmin hoặc tổ hợp có SystemAdmin, một role chuẩn bất kỳ / nhiều role chuẩn / role lặp | 200 từng ca |

**Scenario CU-B — Lưu và kích hoạt:** Given SystemAdmin, when tạo với Manager + Analyst và các scope hợp lệ hoặc bỏ hẳn trường scope, then PostgreSQL có đúng role/scope yêu cầu (role trùng chỉ một dòng), không tự thêm role, response khớp DB và `IsEmailVerified=false`. Redis có challenge `EmailVerification` và mailbox nhận thư thật; login trước verify không có phiên; OTP sai/khác purpose không đổi cờ; OTP đúng đổi cờ và login hợp lệ sau đó cấp JWT chứa đủ roles. Kiểm không scope khi bỏ trường/null/[].

**Scenario CU-C — Gửi lỗi và khôi phục:** Given tạo hợp lệ nhưng SMTP/Redis lỗi sau commit, when API trả `503` với mã `OTP_DELIVERY_FAILED`, then chỉ có một tài khoản với `IsEmailVerified=false`, không có phiên và challenge lỗi không dùng được. Khi hạ tầng phục hồi, `/api/v1/auth/otp/send` gửi thư và OTP mới xác minh được; gọi CreateUser lặp lại không tạo user/role/scope trùng.

## 3. NotificationService, Gateway và AI

**Scenario P03-A (`P0-3`/`GW`) — Hub chỉ nhận kết nối đã xác thực và không có RPC phát:** Given kết nối qua cả service và Gateway, when không có JWT/JWT hết hạn ở negotiate hoặc WebSocket, then bị từ chối, không nhận event. Given JWT hợp lệ, when kiểm tập public hub methods hoặc thử gọi tên Broadcast/Send cũ trong môi trường test, then không có method phát callable và observer không nhận event. Join nhóm không có quyền không nhận push. Token hợp lệ + join hợp lệ qua HTTP negotiate và WebSocket nhận push đúng nhóm từ một sự kiện RabbitMQ; người dùng/nhóm ngoài đích không nhận, cùng một kết nối không nhận lặp sự kiện nghiệp vụ. Kiểm tên event qua contract hiện hành.

**Scenario Outline P04-A (`P0-4`/`GW`) — Notification IDOR**

Given PostgreSQL có thông báo thực cho A và B, JWT của A; lặp quyền thường, Manager, SystemAdmin và tổ hợp role.
When A thực hiện `<yêu cầu>` qua API và các route public Gateway.
Then trả `<status>`, không rò nội dung/unread count hoặc sửa/xóa dữ liệu B.

| Examples: yêu cầu (prefix `/api/v1/notifications`) | Status / kiểm chứng |
| --- | --- |
| `GET /`, `/history`, `/unread-count` không truyền ID hoặc truyền A | 200, dữ liệu chỉ của A |
| Các route trên với `userId=B` hoặc GUID sai định dạng | 403 cho B, 400 cho định dạng sai |
| `GET /{id}`, `PUT /{id}/read`, `DELETE /{id}` của B hoặc ID không tồn tại | 404 giống nhau; trạng thái B không đổi |
| Ba thao tác theo ID thuộc A | 200; read/delete cập nhật đúng bản ghi A |
| Mọi route đọc/ghi trên khi không có JWT hoặc claim user thiếu/sai ngay cả khi query hợp lệ | 401, không đưa userId do request cung cấp vào truy vấn |

**Scenario P04-B (`P0-4`/`GW`/`OPS`) — Chặn open relay và khóa đường nội bộ:** Given caller public ẩn danh hoặc có JWT kể cả SystemAdmin, when gọi tạo/schedule/relay mail hoặc realtime internal qua Gateway, then route không được forward, không tạo DB/Hangfire job, không gửi thư và không phát SignalR. Kiểm như vậy ở cả cấu hình Gateway. Direct NotificationService từ mạng test với user JWT, key thiếu/sai hoặc cấu hình key trống cũng phải bị từ chối, không có side effect. Given Operations gửi sự kiện hợp lệ qua RabbitMQ hoặc qua fallback nội bộ với service credential khi broker không sẵn sàng, then NotificationService phát đúng người nhận; chỉ luồng server được phép sinh email theo template. Public OpenAPI không liệt kê các route nội bộ đã khóa.

**Scenario P05 (`GW`/`AI`/`P0-3`) — Loại public worker/dashboard:** Given từng cấu hình Ocelot thực tế, when gọi `/ai-service` và `/hangfire` ở dạng gốc, dấu slash cuối, đường con và các dạng chuẩn hóa chữ/đường dẫn mà runtime hỗ trợ, bằng method từng được map (cả anonymous và JWT), then Gateway trả `404` và không kết nối downstream. Kiểm allowlist cấu hình và smoke HTTP đều pass; không có cổng public thay thế trong topology test. Given `/api/v1/ai-analysis/*` hợp lệ, when gửi job thật được cấp quyền, then AIInspectionService vẫn hoạt động, RabbitMQ tới worker thật (không bật MockAI), job Hangfire nội bộ vẫn chạy, và `/hubs/notifications/negotiate` + WebSocket còn hoạt động.

## 4. CI, EF Core migration và bằng chứng duyệt

- **CI-01 (GitHub PR):** Given PR vào `main` hoặc `develop`, when chạy required check `Build and Test`, then SDK 9 restore/build/`dotnet test UavPms.sln` và kết quả liệt kê đủ project/traits Gate 5 bắt buộc; test fail, thiếu kết quả test bắt buộc hoặc skip bất thường làm check fail. Chứng minh bằng PR kiểm thử có test đỏ có chủ đích rồi chạy xanh; lưu run ID + bằng chứng required check/ruleset và trạng thái merge bị chặn. Test đỏ không vào nhánh đích. PR không có production secrets.
- **CI-02 (staging OTP smoke):** Given staging được duyệt và mailbox bảo mật, when smoke login thiết bị mới, then lấy mã từ thư vừa gửi thật và xác minh thành công trong TTL, không dùng secret OTP tĩnh. Thiếu SMTP/mailbox hoặc lỗi gửi làm gate staging thất bại; lưu chỉ thời điểm, run ID và kết quả bằng chứng delivery.
- **CI-03 (GitHub Actions + PostgreSQL/PostGIS):** Given SHA đã qua CI, baseline/target cho phép và artifact EF idempotent có checksum đã đối chiếu, when workflow rehearsal chạy trên DB cô lập với backup đã restore, then schema và `__EFMigrationsHistory` đúng target; apply lại là no-op, log không có secret, schema assertion qua `OPS` nếu phù hợp. Đợt 0 dùng migration hiện có để chứng minh đường chạy, không đổi schema Đợt 1.
- **CI-04 (negative/approval):** Given SHA/target ngoài allowlist, artifact sửa, baseline drift, backup/restore/rehearsal fail hoặc thiếu duyệt production, when request apply, then không có thay đổi DB ứng dụng; deploy thường và workflow legacy không apply pending ngoài mốc. Given concurrent dispatch/deploy, then không có hai tiến trình ghi schema cùng lúc. Nếu apply fail giữa chừng, kiểm trạng thái dừng, quy trình restore backup và health trước khi mở writers trên staging cô lập.
- **CI-04a (log GIS scope):** Kiểm `assign-production-gis-scope.yml` và output đã lọc trên dữ liệu thử nghiệm cô lập. Email đầu vào không xuất hiện ở các dòng log, debug hoặc lỗi.
- **CI-05 (điều kiện cho Đợt 1):** Given migration Đợt 1 được duyệt riêng và bản dữ liệu baseline cô lập, when apply từ workflow rehearsal, then bốn bảng `EmergencyAlerts`, `AlertEscalations`, `IncidentReports`, `InspectionSchedules` vắng mặt, `InspectionMedia.MissionId` nullable, dữ liệu/liên kết Mission cũ và các bảng ngoài phạm vi còn nguyên; history có đúng migration, chạy lại idempotent. Production yêu cầu artifact đã rehearsal, backup/restore và phê duyệt riêng. Không chạy tay SQL DROP.
- **H-01 (Harness):** Given Gateway của batch đổi REST, when chạy `scripts/export-openapi.ps1` và kiểm JSON, then có tài liệu cả bốn service public, chứa hợp đồng mới, vắng route nội bộ đã khóa; export fail rõ khi Swagger thiếu service/sai JSON. Acceptance phần này theo script và kiểm contract, không cần chờ xuất bản OpenAPI v1.0 Đợt 1.

Kết quả test, bằng chứng ruleset và migration rehearsal được bổ sung ở từng PR tương ứng. Dừng sau mỗi batch để Control Tower / Human review trước khi mở batch tiếp theo.
