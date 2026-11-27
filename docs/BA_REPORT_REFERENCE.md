# UAV-PMS — Bộ Tài liệu Tham chiếu Chuẩn BA & Báo cáo Đồ án (Bản Chuẩn v2.0)

> **Mục đích**: Tài liệu tra cứu gọn, trực diện dành cho **BA / Nhóm trưởng / Thuyết trình Bảo vệ đồ án** dùng để viết & cập nhật **Report 3 (SRS)**, **Report 4 (SDD)**, **Report 5 (Test Doc)** và các tài liệu nghiệp vụ bên ngoài.
> **Nguồn sự thật gốc**: Đồng bộ 100% với [`docs/MASTER_SYSTEM_SPEC.md`](MASTER_SYSTEM_SPEC.md) (**v2.0**) và [`.specify/memory/constitution.md`](../.specify/memory/constitution.md) (`C-01..C-07`).

---

## Mục 1 — Bảng Đối chiếu Thay đổi (Report 3 / ERD v2.2 Cũ → Chuẩn v2.0 Hiện hành)

*Dùng bảng này để rà soát và thay thế các đoạn lỗi thời trong **Report 3 (SRS cũ)** và **ERD v2.2 cũ** sao cho tài liệu BA khớp 100% với hệ thống chạy thật:*

| Chủ đề | Nội dung CŨ (Report 3 / ERD v2.2 cũ) | Chuẩn MỚI v2.0 (Đang áp dụng thật) |
|---|---|---|
| **Số Role & Multi-Role** | Mỗi user chỉ có 1 role (`BR-002`); `SystemAdmin` đôi khi bị giới hạn hoặc thiếu quyền nghiệp vụ. | Chuẩn **5 role** (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`); hỗ trợ **Multi-Role** (quyền = hợp union). **`SystemAdmin` toàn quyền 100%** mọi thao tác và mọi vùng địa lý (không lọc `UserGeographicScopes`). |
| **Kiểm tra BIST / FC Drone** | Gắn vào bước **Pre-Mission Readiness (MF01)**, hiệu lực 72 giờ (`BR-021..024`). | **Loại bỏ hoàn toàn màn hình kiểm tra kỹ thuật Drone khỏi Pre-Mission.** Chuyển toàn bộ kết nối đọc thông số FC (Serial), chạy BIST và nhập tay BIST vào **Chi tiết Drone (`Drone Detail`)**, chỉ cho **`Technician`** và **`SystemAdmin`** thao tác. |
| **Nhân sự trong Pre-Mission (MF01) vs Mission (MF02)** | MF01 chấm tính sẵn sàng nhân sự chi tiết; MF02 chỉ bắt buộc chọn `Inspector` + `UAV` (`BR-033`). | **MF01 chỉ chọn SỐ LƯỢNG nhân sự theo từng role** (`InspectorCount`, `AnalystCount`, `TechnicianCount`, chưa chọn tên người), hiệu lực Pre-Mission ≤ **7 ngày**. **MF02 chọn ĐÍCH DANH đủ 3 role** (`Inspector`, `Analyst`, `Technician`) → **cả 3 role bấm `Accept`** thì Mission mới sang `Confirmed`. |
| **Check-in Hiện trường GPS** | Chỉ cảnh báo nếu cách cột > 1 km (`BR-044`, `MSG-302`). | **Chỉ `Inspector` được giao (và `SystemAdmin`)** mới được Check-in. Bắt buộc GPS cách **Cột điện số 1 (`sequence = 1`) `≤ 50m`** (cấu hình từ `CheckInRadiusMeters`); sai số > 50m bị **từ chối cứng**, không có GPS bị từ chối. Thành công phát **SignalR realtime** (không cần F5). |
| **Telemetry MF03 & Bàn giao Drone** | Lưu lịch sử telemetry vào DB; có màn hình/bảng `DroneHandovers` bàn giao & trả Drone thủ công. | Telemetry đi từ phần cứng thật → BE → SignalR `/hubs/telemetry` (**RAM-only, delay ≤ 10s, KHÔNG ghi xuống DB `DroneTelemetry`, KHÔNG script giả lập**). Bỏ hoàn toàn `DroneHandovers`: Mission `Completed`/`Cancelled` tự trả UAV về `Available`. |
| **Bản đồ GIS** | Dùng nền bản đồ online / Google Maps / OSM / CDN. | **Local-only GIS 100%**: MapLibre + `/maps/evnspc-south-z12.pmtiles` + schematic local fallback (**0 request ra Internet**). |
| **Upload Ảnh/Video & EXIF GPS** | Ảnh/Video ≤ 50MB (gồm TIFF), ép chọn cột trước khi upload (`BR-047`, `BR-048`). | **Ảnh ≤ 20 MB** (`JPEG`/`PNG`/`WebP`) **không cần chọn cột trước** — BE/AI đọc **EXIF GPS** tự gắn cột gần nhất sau AI. **Video ≤ 200 MB** (`MP4`) mang **GPS live** — AI phát hiện lỗi tự chụp frame ảnh gắn cột gần nhất. Cho phép **`Analyst` đổi cột thủ công** hoặc giữ `TowerId = NULL` ("Chưa gắn cột") nếu lỗi trên dây giữa 2 cột (gỡ bỏ CHECK bắt buộc `TowerId IS NOT NULL` khi duyệt). |
| **Xem Defect & Duyệt AI** | Tin cậy ≥ 0.70 tự `Auto-Accepted` (`BR-060`); màn hình dùng nhãn `"AI Review"`, đôi khi hiện `N/A`; role khác nhau nhìn lệch dữ liệu. | **Mọi kết quả AI vào `Pending` (Chờ duyệt)** — bắt buộc **`Analyst` của Mission (hoặc `SystemAdmin`)** duyệt (`Approve`/`Reject`). **Tất cả thành viên Mission** đều **Xem** được defect với **1 UI đồng nhất** (`towerCode`, BBox `0..100%`). **Sau duyệt bỏ sạch chữ `"AI Review"` và không để `"N/A"`**. |
| **Emergency Alert & Bảo trì** | Tồn tại 2 phân hệ riêng (`EmergencyAlerts`, `AlertEscalations`, `IncidentReports`), phiếu bảo trì sinh ra thường ở `Open` chờ giao tay. | **Gỡ hoàn toàn** phân hệ `EmergencyAlerts`, `AlertEscalations`, `IncidentReports`, `InspectionSchedules` khỏi DB/BE/FE. Khẩn cấp là mức độ (`Emergency` \| `Normal`). `Approve` defect trong Mission → tự tạo `MaintenanceTicket` và **tự gán thẳng (`Assigned`) cho `Technician` của Mission**. |
| **Token, List & Popup** | Access token hết hạn dễ văng ra login; list sort lộn xộn; multi-role dễ bật 2 popup trùng. | FE chạy **Silent Refresh Token** trước hết hạn + lock `isRefreshing` khi `401`. **Mọi danh sách** mặc định mới nhất lên đầu (**`DESC`**). BE phát 1 event SignalR + `Distinct`; FE khử trùng lặp popup (**Zero Duplicate Popup**). |
| **Luật ngoài phạm vi MVP** | Report 3 cũ chèn các luật hàng không/ISO nặng (`BR-068..078` xin phép quân sự/VLOS/bay đêm, `BR-106/108` A0/Ax, `BR-111` chữ ký số QR). | **Cắt khỏi scope đồ án MVP** để tài liệu SRS trung thực 100% với sản phẩm đã làm. |

---

## Mục 2 — Danh mục Business Rules Nguyên tử Chuẩn v2.0 (`BR-001`..`BR-080`)

> **Chuẩn viết SRS (IEEE 830 / Atomic BR)**: Mỗi mã `BR-xxx` chỉ mô tả **duy nhất 1 điều kiện → 1 kết quả kiểm thử được**, bám sát 100% mã nguồn & 37 bảng DB v2.0 (loại bỏ hoàn toàn các luật phần cứng/EVN ngoài phạm vi MVP để tránh bị hỏi vặn khi bảo vệ).

### 2.1. Chung, Quyền Tổng thể, Thời gian, Ordering & Audit (`BR-001`..`BR-010`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-001** | Chỉ tài khoản ở trạng thái `Active` và có JWT hợp lệ do server xác thực mới được gọi các chức năng bảo vệ. | Only authenticated users with an `Active` account and valid server-verified JWT may access protected endpoints. | `Users`, API Gateway |
| **BR-002** | Hệ thống có cố định 5 role (`SystemAdmin`, `Manager`, `Inspector`, `Analyst`, `Technician`); một user có thể giữ nhiều role và quyền hiệu lực bằng hợp (union) các role đó. | Exactly five canonical roles exist; users may hold multiple roles whose effective permissions equal their union. | `Roles`, `UserRoles` |
| **BR-003** | `SystemAdmin` là Superuser 100%: bỏ qua hoàn toàn bộ lọc `UserGeographicScopes` và có quyền thực hiện mọi thao tác nghiệp vụ lẫn quản trị trên toàn hệ thống. | `SystemAdmin` bypasses `UserGeographicScopes` filtering and has unrestricted authority across all operational and administrative actions. | Toàn hệ thống |
| **BR-004** | Bốn role còn lại (`Manager`, `Inspector`, `Analyst`, `Technician`) bắt buộc chỉ được truy vấn và thao tác dữ liệu thuộc các `Region` được gán trong `UserGeographicScopes`. | Non-admin roles (`Manager`, `Inspector`, `Analyst`, `Technician`) are strictly scoped to regions assigned in `UserGeographicScopes`. | `UserGeographicScopes` |
| **BR-005** | Các thực thể mang lịch sử nghiệp vụ (`Users`, `Regions`, `Towers`, `UAVs`, `DefectCategories`) chỉ áp dụng xóa mềm (`IsDeleted = true`), không xóa vật lý khỏi DB. | Historical entities (`Users`, `Regions`, `Towers`, `UAVs`, `DefectCategories`) use soft delete (`IsDeleted = true`), never hard delete. | Soft Delete |
| **BR-006** | Cấm xóa mềm hoặc vô hiệu hóa `User`, `Tower`, `UAV` nếu đang gắn với bất kỳ Mission nào chưa ở trạng thái cuối (`Completed` hoặc `Cancelled`). | Deleting or deactivating a `User`, `Tower`, or `UAV` is blocked while it is linked to any active (non-`Completed`, non-`Cancelled`) mission. | Resource Lock |
| **BR-007** | Toàn bộ mốc thời gian ở Backend và PostgreSQL lưu chuẩn UTC (`Z`); Frontend hiển thị tiếng Việt theo `UTC+7` (`dd/MM/yyyy HH:mm`) qua bộ helper dùng chung. | All backend and database timestamps are stored in UTC (`Z`); frontend formats dates in `UTC+7` (`dd/MM/yyyy HH:mm`) via shared utilities. | All Entities / FE |
| **BR-008** | Mặc định khi không truyền tham số sắp xếp, toàn bộ API và màn hình danh sách luôn xếp bản ghi mới nhất / gần nhất lên đầu (`OrderByDescending`). | Unless explicitly overridden, all list APIs and UI tables sort newest/most recent records first (`DESC`). | All List Screens |
| **BR-009** | Toàn bộ ngưỡng vận hành được lưu động trong `SystemSettings` (chỉ `SystemAdmin` sửa được); mọi secret nội bộ trong cấu hình phải che kín và không bao giờ trả ra Frontend. | Operational thresholds are dynamically stored in `SystemSettings` (`SystemAdmin`-only writes); internal secrets are never exposed to the frontend. | `SystemSettings` |
| **BR-010** | Mọi hành động nhạy cảm và thay đổi trạng thái trọng yếu phải ghi `AuditLogs` (`actor`, `time`, `target`, `oldValue`, `newValue`, `reason`); tuyệt đối không ghi JWT, OTP, mật khẩu, email thật hay tọa độ GPS thô ra log ứng dụng. | Material actions record `actor`, `time`, `target`, `oldValue`, `newValue`, and `reason` in `AuditLogs`; JWTs, OTPs, passwords, raw emails, and raw GPS are never logged. | `AuditLogs` / `C-03` |

### 2.2. Xác thực, OTP SMTP Thật, Silent Refresh & Tài khoản (`BR-011`..`BR-021`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-011** | Địa chỉ `Email` là duy nhất trên toàn bảng `Users`; số điện thoại (`Phone`) khi khác rỗng cũng phải duy nhất toàn hệ thống. | `Email` is globally unique across `Users`, and non-empty `Phone` values are globally unique. | `Users` |
| **BR-012** | Đăng nhập sai mật khẩu 5 lần liên tiếp tự động khóa tài khoản trong 15 phút. | Five consecutive failed login attempts lock the account for 15 minutes. | `IdentityService` |
| **BR-013** | Khi `SystemAdmin` tạo tài khoản (`POST /api/v1/users`), danh sách role bắt buộc có ít nhất 1 role hợp lệ; danh sách rỗng hoặc sai tên role trả `400 Bad Request` và tuyệt đối không tự gán `SystemAdmin`. | User creation requires at least one valid canonical role; empty or invalid roles return `400 Bad Request` and never default to `SystemAdmin`. | `Users`, `UserRoles` |
| **BR-014** | Khi tạo tài khoản có truyền `geographicScopeIds`, mọi ID phải khớp `Region` đang hoạt động; có ID không hợp lệ phải trả `400 Bad Request` (cho phép null hoặc mảng rỗng). | Optional `geographicScopeIds` on user creation must reference active `Regions`; any invalid ID returns `400 Bad Request`. | `UserGeographicScopes` |
| **BR-015** | Tài khoản mới (`IsEmailVerified = false`) và lượt đăng nhập từ thiết bị chưa tin cậy bắt buộc sinh mã OTP lưu Redis và gửi qua SMTP thật (MailKit + TLS); cấm tuyệt đối OTP bypass (`"123456"`) ở mọi môi trường. | Unverified users and untrusted-device logins require a Redis-backed OTP delivered via real MailKit + TLS SMTP, with zero OTP bypass in any environment. | `IdentityService`, Redis |
| **BR-016** | Nếu gửi OTP qua Redis/SMTP thất bại sau khi tạo user, API trả `503 OTP_DELIVERY_FAILED`, giữ `IsEmailVerified = false`, cho phép gửi lại OTP và không tạo trùng user khi retry. | If Redis/SMTP delivery fails after user creation, the API returns `503 OTP_DELIVERY_FAILED`, retains `IsEmailVerified = false`, allows resend, and avoids duplicate user rows on retry. | `IdentityService` |
| **BR-017** | Mỗi mã OTP chỉ có giá trị trong thời gian TTL cấu hình và chỉ được xác thực thành công tối đa 1 lần (single-use), kể cả khi nhận nhiều request xác thực đồng thời. | Each OTP expires after its TTL and can be consumed at most once, even under concurrent verification requests. | Redis OTP |
| **BR-018** | Frontend chủ động theo dõi thời điểm hết hạn (`exp`) của `AccessToken` và tự chạy ngầm `POST /api/v1/auth/refresh-token` trước khi hết hạn trong lúc người dùng đang thao tác. | Frontend proactively executes silent `RefreshToken` renewal before `AccessToken` expiry while the user is active. | FE `core/auth/` |
| **BR-019** | Khi gặp HTTP `401` do `AccessToken` vừa hết hạn, HTTP Interceptor trên FE khóa 1 luồng refresh duy nhất (`isRefreshing`), xếp hàng các request đồng thời rồi tự retry; chỉ đăng xuất khi `RefreshToken` hết hạn hoặc bị thu hồi. | On `401` expiry, the frontend HTTP interceptor serializes one refresh call (`isRefreshing`), queues concurrent requests for automatic retry, and logs out only when `RefreshToken` fails or is revoked. | FE `core/auth/` |
| **BR-020** | Đổi mật khẩu (`POST /api/v1/users/change-password`) bắt buộc có `oldPassword` đúng hoặc `X-StepUp-Token` hợp lệ (khớp đúng user, đúng mục đích `ChangePassword`, chưa dùng); thiếu cả hai trả `400`, sai trả `401`. | Changing password requires either a valid `oldPassword` or a valid single-use `X-StepUp-Token` bound to the same user and `ChangePassword` purpose (`400` if both missing, `401` if invalid). | `IdentityService` |
| **BR-021** | Hành động đổi mật khẩu, đặt lại mật khẩu, tạm khóa hoặc vô hiệu hóa tài khoản bắt buộc thu hồi ngay toàn bộ `RefreshTokens` và `TrustedDevices` của user đó trong cùng transaction DB. | Password changes, resets, suspensions, and deactivations atomically revoke all active `RefreshTokens` and `TrustedDevices` for that user in one transaction. | `RefreshTokens`, `TrustedDevices` |

### 2.3. Tài sản Lưới điện, Flight Controller, Drone BIST/FC & Local GIS (`BR-022`..`BR-030`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-022** | Mọi tọa độ địa lý dùng hệ chuẩn WGS 84 (`EPSG:4326`) với vĩ độ trong `[-90, 90]` và kinh độ trong `[-180, 180]`. | All geographic coordinates use WGS 84 (`EPSG:4326`) with latitude in `[-90, 90]` and longitude in `[-180, 180]`. | `Regions`, `Towers` |
| **BR-023** | Mỗi Cột điện (`Tower`) bắt buộc thuộc đúng 1 Khu vực (`RegionId NOT NULL`) và mã cột (`TowerCode`) phải duy nhất trong phạm vi khu vực đó. | Each `Tower` belongs to exactly one `Region` (`RegionId NOT NULL`) and has a unique tower code within that region. | `Regions`, `Towers` |
| **BR-024** | Một `DroneFlightController` đang ở trạng thái ghép nối hoạt động (`UnpairedAt IS NULL`) chỉ được gắn cho tối đa 1 `UAV` tại cùng một thời điểm. | An active `DroneFlightController` (`UnpairedAt IS NULL`) may be paired to at most one `UAV` at a time. | `DroneFlightControllers` |
| **BR-025** | `UAV` có 4 trạng thái vận hành (`Available`, `InMission`, `InMaintenance`, `Retired`); chỉ `UAV` ở trạng thái `Available` mới được đưa vào danh sách đặt lịch bay. | A `UAV` holds status `Available`, `InMission`, `InMaintenance`, or `Retired`; only `Available` UAVs can be booked. | `UAVs` |
| **BR-026** | Toàn bộ thao tác kiểm tra BIST, đánh giá sức khỏe Drone, kết nối đọc thông số Flight Controller (Serial/FC) và nhập tay BIST chỉ nằm tại màn hình **Chi tiết Drone** và chỉ dành cho **`Technician`** hoặc **`SystemAdmin`** (`Manager`/`Inspector` chỉ xem). | Drone BIST, health checks, Flight Controller reads, and manual BIST entry exist exclusively on Drone Detail and are restricted to `Technician` and `SystemAdmin` (`Manager`/`Inspector` read-only). | `DroneTechnicalInspections` |
| **BR-027** | Khi `Technician` hoặc `SystemAdmin` nhập thủ công kết quả BIST / thông số FC phải nhập kèm ghi chú kiểm định và lưu đầy đủ vào `DroneTechnicalInspections` cùng `DroneTechnicalMetrics`. | Manual BIST or Flight Controller metric entries by `Technician` or `SystemAdmin` require inspection notes and persist to `DroneTechnicalInspections` and `DroneTechnicalMetrics`. | `DroneTechnicalMetrics` |
| **BR-028** | Một lượt kiểm tra kỹ thuật Drone đạt `Ready` khi toàn bộ hạng mục BIST bắt buộc và thông số FC đạt ngưỡng; nếu bất kỳ hạng mục trọng yếu nào không đạt sẽ chuyển `MaintenanceRequired`. | A technical inspection results in `Ready` only when all mandatory BIST items and critical FC metrics pass; otherwise it yields `MaintenanceRequired`. | `DroneTechnicalInspections` |
| **BR-029** | Bản ghi kiểm tra kỹ thuật mới nhất (`UAVs.LastTechnicalInspectionId`) quyết định trạng thái kỹ thuật hiện tại của Drone khi chọn ở Pre-Mission và Mission. | A UAV's latest technical inspection (`LastTechnicalInspectionId`) governs its technical eligibility for Pre-Mission and Mission planning. | `UAVs` |
| **BR-030** | Toàn bộ bản đồ GIS trên hệ thống bắt buộc dùng dữ liệu cục bộ (`MapLibre` + `/maps/evnspc-south-z12.pmtiles` + schematic local fallback), cấm mọi request map/tile/CDN ra Internet và cấm nhúng ảnh base64 vào DB/UI. | All GIS maps use local PMTiles and schematic fallbacks with zero external map/tile/CDN requests and zero base64 image embedding. | Local GIS / `C-01` |

### 2.4. Đánh giá Tiền nhiệm vụ — Pre-Mission Readiness MF01 (`BR-031`..`BR-038`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-031** | Chỉ `Manager` có quyền trong khu vực (hoặc `SystemAdmin`) mới được tạo, đánh giá lại hoặc hủy `PreMissionAssessment`. | Only a `Manager` authorized for the target `Region` (or `SystemAdmin`) may create, re-evaluate, or cancel a `PreMissionAssessment`. | `PreMissionAssessments` |
| **BR-032** | Màn hình và quy trình Pre-Mission (MF01) loại bỏ hoàn toàn bước kiểm tra kỹ thuật / BIST Drone, chỉ kế thừa kết quả kiểm tra kỹ thuật từ màn hình Chi tiết Drone. | Pre-Mission (MF01) excludes all on-screen drone BIST/technical inspection steps and inherits readiness directly from Drone Detail inspections. | MF01 |
| **BR-033** | Mỗi `PreMissionAssessment` phải chọn ít nhất 1 cột điện (`PreMissionAssessmentTowers`) và toàn bộ cột được chọn phải thuộc đúng `Region` của bản đánh giá. | A `PreMissionAssessment` requires at least one tower (`PreMissionAssessmentTowers`), and every selected tower must belong to the assessment's `Region`. | `PreMissionAssessmentTowers` |
| **BR-034** | Chỉ các UAV thuộc đúng `Region`, đang `Available`, không bị trùng lịch và có lần kiểm tra BIST gần nhất đạt `Ready` mới được chọn trong `PreMissionAssessmentDrones`. | Only `Available`, unbooked UAVs in the same `Region` whose latest technical inspection is `Ready` may be selected in `PreMissionAssessmentDrones`. | `PreMissionAssessmentDrones` |
| **BR-035** | Điều kiện thời tiết và pin an toàn tại MF01 được đối chiếu theo ngưỡng động trong `SystemSettings` (mặc định tốc độ gió `≤ PreMissionMaxWindSpeedMs` [`12 m/s`], pin `≥ PreMissionMinBatteryPct` [`30%`]). | MF01 site weather and battery checks evaluate dynamically against `SystemSettings` (`PreMissionMaxWindSpeedMs`, `PreMissionMinBatteryPct`). | `PreMissionAssessments` |
| **BR-036** | Tại bước Pre-Mission (MF01), hệ thống chỉ khai báo **số lượng nhân sự cần thiết theo từng role** (`InspectorCount`, `AnalystCount`, `TechnicianCount` lưu tại `PreMissionAssessmentPersonnel`), chưa chỉ định đích danh tên người. | MF01 captures only required headcount per role in `PreMissionAssessmentPersonnel` without binding named individuals yet. | `PreMissionAssessmentPersonnel` |
| **BR-037** | Thời hạn hiệu lực của một bản `PreMissionAssessment` đạt chuẩn (`Ready`) không được vượt quá `PreMissionMaxValidityDays` (trần tối đa **≤ 7 ngày**) và tự hết hạn (`Expired`) khi quá hạn. | A `Ready` assessment has a maximum validity of `PreMissionMaxValidityDays` (capped at `≤ 7` days) and transitions to `Expired` once elapsed. | `PreMissionAssessments` |
| **BR-038** | Chỉ bản `PreMissionAssessment` đang ở trạng thái `Ready` và còn hạn mới được dùng để tạo tối đa **1** Mission (chuyển thành `Completed`), và hạn hiệu lực được kiểm tra lại cả khi tạo lẫn khi bắt đầu bay Mission. | Only an unexpired `Ready` assessment can spawn at most one mission (marking it `Completed`), and validity is re-verified at both mission creation and mission start. | `PreMissionAssessments`, `Missions` |

### 2.5. Tạo, Giao Mission & Xác nhận 3 Role — MF02 (`BR-039`..`BR-048`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-039** | Tạo và giao Mission (MF02) bắt buộc chọn **đích danh người đang `Active`** cho đủ cả **3 role (`Inspector`, `Analyst`, `Technician`)** trong `MissionAssignments`. | Creating and dispatching a mission (MF02) requires assigning named `Active` personnel for all three roles (`Inspector`, `Analyst`, `Technician`). | `MissionAssignments` |
| **BR-040** | Một Mission phải có ít nhất 1 cột mục tiêu (`MissionTargets`) thuộc phạm vi Pre-Mission với thứ tự rõ ràng (`sequence = 1..n`), trong đó cột `sequence = 1` là điểm gốc cho Check-in GPS. | A mission requires at least one ordered target tower (`MissionTargets`, `sequence = 1..n`) within the assessment scope, with `sequence = 1` serving as the GPS check-in anchor. | `MissionTargets` |
| **BR-041** | Thời gian dự kiến của Mission phải thỏa `ScheduledStart < ScheduledEnd`, nằm trong khung hiệu lực Pre-Mission, và `ConfirmationDeadline < ScheduledStart`. | Mission scheduling enforces `ScheduledStart < ScheduledEnd` within the assessment window and `ConfirmationDeadline < ScheduledStart`. | `Missions` |
| **BR-042** | Mọi thao tác tạo, giao, đổi lịch hoặc đổi người phải ghi `MissionAssignments` và `ResourceBookings` trong cùng transaction DB; nếu trùng khung giờ sử dụng cùng UAV hoặc cùng nhân sự với Mission khác chưa kết thúc phải trả ngay **`409 Conflict`**. | Mission creation, dispatch, rescheduling, and reassignment atomically update `MissionAssignments` and `ResourceBookings`, returning `409 Conflict` on any overlapping UAV or user booking. | `ResourceBookings` |
| **BR-043** | Khi Mission ở `Draft`, `PendingAcceptance`, `Postponed` hoặc `Confirmed`, cho phép sửa qua `PUT /api/v1/missions/{id}`; nếu đang `Confirmed` mà đổi lịch hoặc đổi nhân sự, Mission tự lùi về `PendingAcceptance` để xác nhận lại. | Pre-flight missions are updatable; modifying schedule or assignees on a `Confirmed` mission resets its status to `PendingAcceptance` for re-confirmation. | `Missions` |
| **BR-044** | Mission đã đạt trạng thái cuối (`Completed` hoặc `Cancelled`) là bất biến (cấm chỉnh sửa/mở lại); Mission đang `InProgress` chỉ được chuyển sang `Suspended`, `Completed` hoặc `Cancelled`. | `Completed` and `Cancelled` missions are immutable; an `InProgress` mission may only transition to `Suspended`, `Completed`, or `Cancelled`. | `Missions` |
| **BR-045** | Chỉ chính nhân sự được phân công mới được bấm phản hồi (`Accept` hoặc `Postpone`) cho dòng `MissionAssignment` của mình trước `ConfirmationDeadline`, và mỗi phiên bản phân công chỉ nhận 1 phản hồi cuối. | Only the assignee may respond (`Accept`/`Postpone`) to their own assignment before `ConfirmationDeadline`, and only once per assignment version. | `MissionAssignments` |
| **BR-046** | Yêu cầu xin hoãn (`Postpone`) bắt buộc nhập lý do và thời gian đề xuất; hệ thống giữ Mission ở `PendingAcceptance`/`Postponed` và thông báo ngay cho `Manager`. | A postponement request requires a non-empty reason and proposed time, keeping confirmation open and immediately notifying the `Manager`. | `MissionAssignments` |
| **BR-047** | Mission chỉ tự động chuyển từ `PendingAcceptance` sang **`Confirmed`** khi **tất cả 3 người thuộc 3 role (`Inspector`, `Analyst`, `Technician`)** đều có trạng thái `Accepted`. | A mission transitions from `PendingAcceptance` to `Confirmed` if and only if all three assigned roles (`Inspector`, `Analyst`, `Technician`) are `Accepted`. | `Missions`, `MissionAssignments` |
| **BR-048** | Mọi thao tác hủy, tạm dừng, tiếp tục, đổi lịch, đổi người của Mission hoặc bỏ qua cột (`Skip`) đều bắt buộc nhập lý do, ghi `AuditLogs` và thông báo các thành viên bị ảnh hưởng. | Cancelling, suspending, resuming, rescheduling, reassigning a mission, or skipping a target tower requires a reason, writes an `AuditLogs` entry, and notifies affected users. | `Missions`, `AuditLogs` |

### 2.6. Check-in GPS 50m & Telemetry Bay Thực địa — MF03 (`BR-049`..`BR-056`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-049** | Chỉ **`Inspector` được phân công trong chính Mission đó (hoặc `SystemAdmin`)** mới có quyền thực hiện Check-in hiện trường trên Mission đang `Confirmed`; `Analyst`, `Technician` hoặc `Inspector` ngoài Mission bị chặn `403`. | Only the mission's assigned `Inspector` (or `SystemAdmin`) may check in on a `Confirmed` mission; all other roles receive `403 Forbidden`. | `MissionCheckIns` |
| **BR-050** | Check-in hiện trường tính khoảng cách Haversine giữa GPS thiết bị và tọa độ **Cột điện số 1 (`sequence = 1`)**; nếu vượt quá `CheckInRadiusMeters` (mặc định **`50m`**) hệ thống từ chối cứng kèm khoảng cách thực tế và giới hạn `50m`. | Check-in computes Haversine distance against Tower #1 (`sequence = 1`) and strictly rejects requests exceeding `CheckInRadiusMeters` (default `50m`) with measured distance feedback. | `MissionCheckIns`, `MissionTargets` |
| **BR-051** | Yêu cầu Check-in không cung cấp tọa độ GPS hợp lệ từ thiết bị bị từ chối (cấm bỏ qua kiểm tra GPS). | Check-in requests lacking valid device GPS coordinates are rejected without silent bypass. | `MissionCheckIns` |
| **BR-052** | Ngay khi Check-in hợp lệ, hệ thống lưu `MissionCheckIns`, chuyển Mission sang `Ready` và phát **1 sự kiện SignalR realtime** để `Manager` và các thành viên đang mở màn hình tự cập nhật không cần F5. | Verified check-in persists `MissionCheckIns`, transitions the mission to `Ready`, and emits a single real-time SignalR update to active viewers without manual refresh. | `MissionCheckIns`, SignalR |
| **BR-053** | Lệnh bắt đầu bay (`StartMission`: `Ready → InProgress`) chỉ thực hiện được 1 lần duy nhất và phải kiểm tra đồng thời điều kiện đã Check-in (`Ready`) và Pre-Mission liên kết vẫn còn hạn. | `StartMission` (`Ready → InProgress`) runs once and verifies both verified check-in (`Ready`) and an unexpired linked `PreMissionAssessment`. | `Missions` |
| **BR-054** | Luồng Telemetry MF03 đi từ phần cứng UAV / Mission Planner qua `/hubs/telemetry` (`[Authorize]`, JSON `camelCase`) **100% trên RAM** với độ trễ **`≤ 10 giây`**, **KHÔNG ghi DB `DroneTelemetry`** và **KHÔNG dùng script giả lập**. | Real-time MF03 telemetry streams from real UAV/Mission Planner hardware over authorized `/hubs/telemetry` purely in RAM within `≤ 10s` latency, with zero DB writes and zero simulation scripts. | `/hubs/telemetry` / `C-04` |
| **BR-055** | Khi UAV đang bay mất gói Telemetry liên tiếp quá **30 giây**, SignalR hub tự động phát sự kiện `DroneOffline` tới màn hình giám sát. | If telemetry packets cease for more than 30 consecutive seconds, `/hubs/telemetry` emits `DroneOffline` to connected viewers. | `/hubs/telemetry` |
| **BR-056** | Khi Mission chuyển sang `Completed` hoặc `Cancelled`, Backend tự động giải phóng `ResourceBookings` và đặt `UAVs.FlightStatus` về `Available` không cần bước bàn giao thủ công. | Completing or cancelling a mission automatically releases `ResourceBookings` and restores the UAV to `Available` without manual handover. | `Missions`, `UAVs` |

### 2.7. Upload Ảnh EXIF / Video Live GPS, AI & Duyệt Khuyết tật — MF04 (`BR-057`..`BR-068`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-057** | Chỉ `Inspector` được phân công, `Analyst` được phân công trong Mission, hoặc `SystemAdmin` mới được phép upload ảnh/video vào Mission vận hành. | Only the mission's assigned `Inspector`, assigned `Analyst`, or `SystemAdmin` may upload operational mission media. | `InspectionMedia` |
| **BR-058** | Upload chỉ chấp nhận Ảnh (`image/jpeg`, `image/png`, `image/webp`) tối đa **`MaxImageSizeMb` (`20 MB`)** và Video (`video/mp4`) tối đa **`MaxVideoSizeMb` (`200 MB`)**; FE không hiển thị lựa chọn `preferredModel`. | Media uploads enforce `20 MB` (JPEG/PNG/WebP) and `200 MB` (MP4) limits from `SystemSettings` and expose no `preferredModel` selector. | `InspectionMedia` |
| **BR-059** | Trong cùng 1 Mission, file có mã băm `SHA-256` (`Checksum`) trùng với file đã nộp sẽ được bỏ qua chống lưu trùng; ảnh Standalone không chia sẻ kiểm tra trùng checksum với ảnh Mission thật. | Duplicate files sharing a SHA-256 `Checksum` within the same mission are skipped; standalone uploads do not share checksum deduplication with operational missions. | `InspectionMedia` |
| **BR-060** | Upload ảnh vào Mission **không bắt buộc chọn cột trước** (`TowerId = NULL`); Backend/AI đọc **EXIF GPS** (`GPSLatitude`, `GPSLongitude`) của ảnh gốc và tự gắn cột gần nhất trong `MissionTargets` sau khi phân tích AI. | Mission image uploads permit `TowerId = NULL`; backend/AI reads original EXIF GPS metadata and automatically attaches the nearest tower from `MissionTargets`. | `InspectionMedia`, `DetectedAnomalies` |
| **BR-061** | Video MP4 quay từ Drone mang dữ liệu **GPS live**: khi FastAPI AI Worker phát hiện lỗi sẽ tự động trích xuất (chụp) **frame ảnh chứa lỗi**, lưu `FrameTimeSeconds` + GPS `Location` và tự gắn cột gần nhất trong Mission. | Drone videos carrying live GPS metadata trigger automatic defect frame extraction with `FrameTimeSeconds`, GPS `Location`, and nearest-tower matching. | `AIAnalysisRuns`, `DetectedAnomalies` |
| **BR-062** | Nếu ảnh/video mất GPS hoặc khuyết tật nằm trên khoảng dây giữa 2 cột, `TowerId` vẫn được phép để **`NULL`** (*"Chưa gắn cột"*) và vẫn duyệt hợp lệ; `Analyst` của Mission (hoặc `SystemAdmin`) được phép gán hoặc đổi cột thủ công. | Detections lacking GPS or located mid-span remain valid for review with `TowerId = NULL`, and the mission's `Analyst` (or `SystemAdmin`) may manually assign or reassign the tower. | `DetectedAnomalies` |
| **BR-063** | Phân tích AI chạy bất đồng bộ qua RabbitMQ (`AIAnalysisRuns`), thử lại tối đa 3 lần khi gặp lỗi tạm thời trước khi chuyển `Failed` (hỗ trợ bấm `Retry`), và chặn xử lý trùng callback theo `SourceEventId` duy nhất. | AI jobs run asynchronously via RabbitMQ, retry transient errors up to 3 times before `Failed` (allowing manual `Retry`), and deduplicate callbacks by unique `SourceEventId`. | `AIAnalysisRuns` |
| **BR-064** | Mọi khuyết tật AI phát hiện phải ánh xạ vào `DefectCategories`, có độ tin cậy `confidence ∈ [0, 1]`, tọa độ Bounding Box chuẩn hóa **`0..100%`**, và mức độ vận hành `Emergency` hoặc `Normal`. | Every AI detection maps to `DefectCategories`, carries `confidence ∈ [0, 1]`, normalizes Bounding Box coordinates to `0..100%`, and assigns severity `Emergency` or `Normal`. | `DetectedAnomalies`, `DefectCategories` |
| **BR-065** | Tất cả thành viên có mặt trong Mission (`Manager` vùng, `Inspector`, `Analyst`, `Technician` được giao) và `SystemAdmin` đều được **Xem** danh sách và chi tiết khuyết tật của Mission qua cùng 1 nguồn dữ liệu đồng nhất; **chỉ `Analyst` của chính Mission đó (và `SystemAdmin`)** được quyền bấm **`Approve` / `Reject`**. | All mission members and `SystemAdmin` can view mission defects through one unified data view, whereas only the mission's assigned `Analyst` (or `SystemAdmin`) may `Approve` or `Reject`. | `DetectedAnomalies` |
| **BR-066** | Mọi kết quả AI bắt buộc vào trạng thái `Pending` (*"Chờ duyệt"*) để con người xác nhận (không tự duyệt ngầm); ảnh 0 lỗi vẫn chờ xác nhận sạch; hành động `Reject` bắt buộc nhập lý do; kết quả sau khi duyệt (`Approved`/`Rejected`) bị khóa cứng bất biến. | Every AI detection requires explicit human validation (`Pending → Approved/Rejected`), zero-defect media still await confirmation, `Reject` requires a reason, and finalized outcomes are immutable. | `DetectedAnomalies` |
| **BR-067** | Sau khi khuyết tật đã duyệt, giao diện xóa toàn bộ nhãn `"AI Review"` / `"Chờ AI"` và hiển thị đúng nhãn chính thức (`Đã xác nhận`, `Đã loại bỏ`, `Đã khắc phục`); tuyệt đối không hiển thị `"N/A"` ở mọi màn hình. | Post-review UI removes all `"AI Review"` wording in favor of official status labels and prohibits `"N/A"` placeholders across all screens. | FE UI / `C-05` |
| **BR-068** | Chức năng Standalone Test (`MissionId = NULL`, `TowerId = NULL`) chỉ dành cho `Analyst` và `SystemAdmin`, tách biệt hoàn toàn khỏi danh sách vận hành (`WHERE MissionId IS NOT NULL`), không sinh Phiếu bảo trì, không tính vào Dashboard/Reports, và tự dọn file S3 sau `StandaloneMediaRetentionDays`. | Standalone Test (`Analyst`/`SystemAdmin` only) is isolated from operational queries (`MissionId IS NOT NULL`), tickets, and reports, and expires S3 files after `StandaloneMediaRetentionDays`. | `InspectionMedia`, Hangfire |

### 2.8. Tự động Bảo trì, Notification Matrix, Hangfire & Báo cáo (`BR-069`..`BR-080`)

| Mã | Quy tắc Nghiệp vụ Nguyên tử (Tiếng Việt) | Atomic Rule (English — SRS Report 3) | Entity / Áp dụng |
|---|---|---|---|
| **BR-069** | Duyệt (`Approve`) 1 khuyết tật thuộc Mission tạo **tối đa 1 `MaintenanceTicket`** (quan hệ 1–1 với `AnomalyId`, kế thừa `Severity = Emergency \| Normal`), chống sinh trùng phiếu khi bấm đúp hoặc retry. | Approving a mission anomaly idempotently generates at most one `MaintenanceTicket` inheriting its `Severity` (`Emergency` \| `Normal`). | `MaintenanceTickets` |
| **BR-070** | Nếu Mission của khuyết tật đã có `Technician` trong `MissionAssignments`, phiếu bảo trì tự sinh được **gán thẳng cho `Technician` đó ở trạng thái ban đầu `Assigned`**; chỉ khi chưa có `Technician` mới để `Open` (`TechnicianId = NULL`). | Auto-created mission tickets are immediately initialized as `Assigned` to the mission's assigned `Technician`; tickets without a mission technician start as `Open` (`TechnicianId = NULL`). | `MaintenanceTickets` |
| **BR-071** | Phiếu bảo trì ở trạng thái `Open` (chưa gán `Technician`) không được chuyển sang `InProgress`; chỉ `Technician` đang được gán phiếu (hoặc `SystemAdmin`) mới được thực hiện sửa chữa trên phiếu đó. | An unassigned `Open` ticket cannot move to `InProgress`; only the assigned `Technician` (or `SystemAdmin`) may execute repair actions on it. | `MaintenanceTickets` |
| **BR-072** | Mọi bản ghi vật tư thay thế trong `MaterialLogs` phải có số lượng dương (`Quantity > 0`) kèm tên/đơn vị vật tư hợp lệ. | Every replacement entry in `MaterialLogs` must specify a positive quantity (`Quantity > 0`) and valid item/unit metadata. | `MaterialLogs` |
| **BR-073** | Chuyển phiếu từ `InProgress` sang `PendingVerification` bắt buộc đã tải lên đủ ảnh minh chứng **trước và sau sửa chữa** lưu trên S3 (`MaintenanceProofs`, khóa `bucket/object`). | Submitting a ticket for verification (`InProgress → PendingVerification`) requires both before-repair and after-repair photo proofs stored in S3 (`MaintenanceProofs`). | `MaintenanceProofs` |
| **BR-074** | Chỉ `Manager` phụ trách vùng (hoặc `SystemAdmin`) được duyệt đóng phiếu (`PendingVerification → Closed`) hoặc trả lại làm lại (`InProgress`, bắt buộc lý do); khi phiếu chuyển `Closed`, khuyết tật cha tự động chuyển `Resolved`. | Only the regional `Manager` (or `SystemAdmin`) may approve closure (`Closed`) or reject proofs back to `InProgress` with a reason; closing a ticket automatically marks its parent anomaly `Resolved`. | `MaintenanceTickets`, `DetectedAnomalies` |
| **BR-075** | `NotificationService` bắt buộc lưu bản ghi `Notifications` vào PostgreSQL trước khi đẩy SignalR hoặc SMTP Email, gửi đúng role theo Ma trận Notification và không chèn mật khẩu/token vào nội dung. | `NotificationService` persists notifications to PostgreSQL before SignalR/email delivery, routes strictly per the role Notification Matrix, and omits secrets/tokens from payloads. | `Notifications` |
| **BR-076** | Mỗi hành động nghiệp vụ ở Backend chỉ phát **1 sự kiện SignalR duy nhất**; `NotificationConnectionRegistry` khử trùng lặp bằng `Distinct` `connectionId` và dọn toàn bộ dynamic group trong `OnDisconnectedAsync`. | Each backend business event emits one SignalR notification, deduplicates connections via `Distinct`, and clears dynamic groups on disconnect. | `NotificationService` |
| **BR-077** | Frontend bắt buộc khử trùng lặp thông báo SignalR theo `notificationId`/`eventId` (dự phòng `type + referenceId + actorId`) trước khi đẩy vào hàng đợi Toast/Popup để đảm bảo không bao giờ bật 2 popup trùng. | Frontend deduplicates incoming SignalR notifications by `notificationId`/`eventId` (or composite fallback key) before rendering toasts, preventing duplicate popups. | FE `notifications/` |
| **BR-078** | Cả 6 tác vụ định kỳ trên Hangfire đọc ngưỡng thời gian từ `SystemSettings` và kiểm tra/ghi cờ `*NotifiedAt` để mỗi mốc nhắc hạn hoặc quá hạn chỉ gửi đúng 1 lần cho mỗi bản ghi. | All six Hangfire cron jobs read thresholds from `SystemSettings` and guard each milestone with `*NotifiedAt` flags so reminders fire at most once per record. | Hangfire / `SystemSettings` |
| **BR-079** | Báo cáo (`Reports`, `ReportMissions`) chỉ tổng hợp các Mission thuộc `UserGeographicScopes` của người tạo (`SystemAdmin` toàn hệ thống); khi duyệt báo cáo (`Approved`), hệ thống đóng băng dữ liệu vào cột `Reports.Snapshot` (`jsonb`). | Reports aggregate only missions inside the user's `UserGeographicScopes` (`SystemAdmin` unrestricted) and freeze an immutable `Snapshot` (`jsonb`) upon approval. | `Reports`, `ReportMissions` |
| **BR-080** | Các tệp xuất PDF và Excel của Báo cáo được sinh trực tiếp khi người dùng tải xuống (từ phạm vi lọc hoặc từ `Snapshot` đã duyệt), không lưu trữ file PDF/Excel dư thừa trong DB hay S3. | Report PDF and Excel files are generated dynamically on download from scope data or the frozen `Snapshot`, avoiding redundant stored binary files. | `Reports` |

---

## Mục 3 — Ma trận Phân quyền & Phạm vi Dữ liệu v2.0 (Role & Permission Matrix)

> **Quy tắc chung**:
> 1. **`SystemAdmin` = Superuser 100%**: Truy cập và thao tác toàn bộ chức năng, **không bị lọc** bởi `UserGeographicScopes`.
> 2. **4 Role còn lại (`Manager`, `Inspector`, `Analyst`, `Technician`)**: Mọi truy vấn dữ liệu bắt buộc lọc theo `UserGeographicScopes` được gán; nếu người dùng có **nhiều role**, quyền bằng **hợp (union)** các quyền bên dưới.
> 3. **UI đồng nhất**: Các role xem cùng màn hình (ví dụ tab Kết quả giám định) phải thấy **cùng một dữ liệu chuẩn**; chỉ khác quyền hiện/ẩn nút thao tác.

| Nhóm Chức năng / Hành động | `SystemAdmin` | `Manager` | `Inspector` | `Analyst` | `Technician` |
|---|:---:|:---:|:---:|:---:|:---:|
| **Quản trị User, Multi-Role, Khóa Vùng, `SystemSettings`, Audit Logs** | ✅ Toàn hệ thống | ❌ | ❌ | ❌ | ❌ |
| **Quản lý `Regions`, `Towers`, `UAVs`, `FlightControllers` (CRUD)** | ✅ Toàn quyền | ✅ *(Trong vùng)* | Xem *(Trong vùng)* | Xem *(Trong vùng)* | Xem *(Trong vùng)* |
| **Kiểm tra BIST / Sức khỏe Drone / Đọc FC & Nhập tay BIST (tại Chi tiết Drone)** | ✅ | Xem kết quả | Xem kết quả | ❌ | ✅ *(Duy nhất ngoài Admin)* |
| **Tạo / Duyệt / Hủy Pre-Mission Assessment (MF01 — chỉ chọn số lượng, không BIST)** | ✅ | ✅ *(Trong vùng)* | Xem *(Trong vùng)* | Xem *(Trong vùng)* | Xem *(Trong vùng)* |
| **Tạo / Sửa / Hủy / Giao Mission (MF02 — chọn đích danh 3 role)** | ✅ | ✅ *(Trong vùng)* | Xem *(Được giao)* | Xem *(Được giao)* | Xem *(Được giao)* |
| **Bấm Xác nhận nhận Mission (`Accept` / `Postpone`)** | ✅ | ❌ *(Người giao)* | ✅ *(Được giao)* | ✅ *(Được giao)* | ✅ *(Được giao)* |
| **Check-in hiện trường (GPS `≤ 50m` tại Cột điện số 1)** | ✅ | ❌ | ✅ *(Duy nhất ngoài Admin)* | ❌ | ❌ |
| **Bắt đầu / Tạm dừng / Tiếp tục / Hoàn thành bay Mission** | ✅ | Hủy / Tạm dừng | ✅ *(Được giao)* | ❌ | ❌ |
| **Xem Telemetry bay realtime (`/hubs/telemetry`, RAM-only trên Local GIS)** | ✅ | ✅ *(Trong vùng)* | ✅ *(Được giao)* | Xem *(Được giao)* | ❌ |
| **Upload Ảnh (EXIF GPS) / Video (GPS live) vào Mission** | ✅ | ❌ | ✅ *(Được giao)* | ✅ *(Được giao)* | ❌ |
| **Upload Standalone Test (`MissionId = NULL`)** | ✅ | ❌ | ❌ | ✅ | ❌ |
| **Xem danh sách & chi tiết Khuyết tật (Defect) trong Mission** | ✅ | ✅ *(Trong vùng)* | ✅ *(Được giao)* | ✅ *(Được giao)* | ✅ *(Được giao)* |
| **Duyệt Khuyết tật (`Approve` / `Reject`) & đổi cột thủ công / `Retry` AI** | ✅ | ❌ | ❌ | ✅ *(Chỉ Analyst của Mission đó)* | ❌ |
| **Xem Dashboard & Tạo / Xuất Báo cáo (`Reports` PDF/Excel)** | ✅ Toàn hệ thống | ✅ *(Trong vùng)* | ❌ | ✅ *(Trong vùng)* | Xem danh sách |
| **Giao lại / Kiểm tra minh chứng / Đóng / Hủy Phiếu Bảo trì** | ✅ | ✅ *(Trong vùng)* | ❌ | ❌ | ❌ |
| **Xử lý Phiếu Bảo trì, ghi vật tư & upload minh chứng Trước/Sau** | ✅ | Xem / Duyệt | ❌ | ❌ | ✅ *(Phiếu được giao)* |

---

## Mục 4 — Danh mục Application Messages, Notification Matrix & Hangfire Triggers

### 4.1. Danh mục Application Messages Chuẩn v2.0 (Thay thế Bảng 36 Report 3)

*Quy ước đánh số: `MSG-0xx` Chung/Auth, `MSG-1xx` Drone BIST & MF01, `MSG-2xx` MF02 Giao việc, `MSG-3xx` Check-in 50m & Telemetry MF03, `MSG-4xx` Upload EXIF/Video & Review MF04, `MSG-5xx` Bảo trì & Cấu hình.*

| Mã | Loại UI | Ngữ cảnh | Nội dung Tiếng Việt (Hiển thị trên FE) | Nội dung Tiếng Anh (Ghi vào SRS Report 3) |
|---|---|---|---|---|
| `MSG-001` | Inline | Thiếu trường bắt buộc | Vui lòng nhập `{Field}`. | `{Field} is required.` |
| `MSG-002` | Inline | Khoảng ngày giờ không hợp lệ | `{Start}` phải nhỏ hơn `{End}`. | `{Start} must be earlier than {End}.` |
| `MSG-003` | Empty state | Danh sách không có dữ liệu | Không có dữ liệu phù hợp với bộ lọc hiện tại. | `No records match the selected filters.` |
| `MSG-004` | Toast | Lưu thành công | Lưu thay đổi thành công. | `Changes saved successfully.` |
| `MSG-005` | Error banner | Sai mật khẩu | Email hoặc mật khẩu không chính xác. | `Invalid email or password.` |
| `MSG-006` | Error banner | Tài khoản bị khóa / vô hiệu | Tài khoản đã bị khóa hoặc tạm ngưng. Vui lòng thử lại sau `{minutes}` phút hoặc liên hệ quản trị viên. | `Account is locked or inactive. Try again after {minutes} minutes or contact SystemAdmin.` |
| `MSG-007` | Error banner | Sai / hết hạn OTP | Mã OTP không hợp lệ hoặc đã hết hạn. | `Invalid or expired verification code.` |
| `MSG-008` | Error banner | Lỗi gửi OTP (`503 OTP_DELIVERY_FAILED`) | Tài khoản đã được tạo nhưng gửi email OTP thất bại. Vui lòng bấm gửi lại mã OTP. | `User created, but OTP email delivery failed. Please resend the OTP.` |
| `MSG-009` | Error banner | Phiên hết hạn thật (Refresh thất bại) | Phiên đăng nhập đã hết hạn hoặc bị thu hồi. Vui lòng đăng nhập lại. | `Your session has expired or been revoked. Please sign in again.` |
| `MSG-010` | Error banner | Không đủ quyền (`403`) | Bạn không có quyền thực hiện thao tác này hoặc dữ liệu nằm ngoài phạm vi vùng được cấp. | `You do not have permission or geographic scope for this action.` |
| `MSG-011` | Inline | Trùng dữ liệu | `{Field}` "`{value}`" đã tồn tại trong hệ thống. | `{Field} "{value}" already exists.` |
| `MSG-012` | Inline | Tọa độ GPS sai biên | Vĩ độ phải từ -90 đến 90 và kinh độ từ -180 đến 180. | `Latitude/longitude is outside valid WGS 84 bounds.` |
| `MSG-013` | Confirm dialog | Xác nhận thao tác quan trọng | Bạn có chắc chắn muốn `{action}` `{record}`? | `Are you sure you want to {action} {record}?` |
| `MSG-014` | Error banner | Tài nguyên đang bận trong Mission | Không thể xóa/khóa `{resource}` vì đang thuộc nhiệm vụ chưa kết thúc. | `Cannot delete or deactivate {resource} while assigned to an active mission.` |
| `MSG-101` | Toast | Lưu BIST / sức khỏe Drone thành công | Đã lưu kết quả kiểm tra kỹ thuật Drone `{serial}` (`{Ready/MaintenanceRequired}`). | `Technical inspection for UAV {serial} saved as {status}.` |
| `MSG-102` | Inline | Không có quyền thao tác BIST/FC | Chỉ Kỹ thuật viên hoặc Quản trị hệ thống mới được cập nhật BIST / thông số FC của Drone. | `Only Technician or SystemAdmin can update UAV BIST or FC parameters.` |
| `MSG-103` | Error banner | Không có Drone đạt chuẩn | Không có UAV ở trạng thái Đạt chuẩn / Sẵn sàng cho khu vực và khung giờ đã chọn. | `No technically ready UAV is available in this region and schedule.` |
| `MSG-104` | Status | Pre-Mission đạt / không đạt | Đánh giá Tiền nhiệm vụ: `{Đạt chuẩn / Không đạt}`. `{reasons}` | `Pre-mission assessment status: {Ready/NotReady}. {reasons}` |
| `MSG-105` | Error banner | Pre-Mission hết hạn (≤ 7 ngày) | Bản đánh giá Tiền nhiệm vụ đã hết hạn hiệu lực (tối đa `{days}` ngày) hoặc đã được dùng cho nhiệm vụ khác. | `Pre-mission assessment is expired or already consumed.` |
| `MSG-201` | Error banner | Chưa chọn đủ 3 role ở MF02 | Vui lòng chọn đủ nhân sự cho cả 3 vai trò: `Inspector`, `Analyst` và `Technician`. | `Select named personnel for all three roles (Inspector, Analyst, Technician).` |
| `MSG-202` | Inline | Trùng lịch (`409 Conflict`) | `{Resource}` đã được đặt lịch cho nhiệm vụ khác trong khung giờ `{start}` – `{end}`. | `{Resource} is already booked from {start} to {end}.` |
| `MSG-203` | Toast | Giao nhiệm vụ thành công | Đã giao nhiệm vụ và gửi thông báo tới 3 thành viên phụ trách. | `Mission dispatched; assignment notifications sent to all 3 assigned roles.` |
| `MSG-204` | Toast | Phản hồi nhiệm vụ (`Accept`/`Postpone`) | Đã ghi nhận phản hồi nhiệm vụ. `{pendingCount}` thành viên còn lại đang chờ xác nhận. | `Assignment response recorded.` |
| `MSG-205` | Error banner | Chỉnh sửa / giao nhiệm vụ chưa đủ điều kiện | Nhiệm vụ cần tối thiểu 1 cột mục tiêu, 1 UAV sẵn sàng và đủ 3 vai trò được phân công. | `Dispatch requires at least one target tower, one ready UAV, and 3 assigned roles.` |
| `MSG-301` | Error banner | Sai role khi Check-in | Chỉ Nhân viên hiện trường (`Inspector`) được phân công mới được phép Check-in nhiệm vụ này. | `Only the assigned Inspector (or SystemAdmin) can check in for this mission.` |
| `MSG-302` | Error banner | Check-in vượt bán kính `50m` Cột 1 | Vị trí GPS hiện tại cách Cột điện số 1 (`{towerCode}`) `{distance}` m, vượt quá bán kính cho phép `{limit}` m. Vui lòng di chuyển lại gần điểm tập kết. | `Current GPS distance ({distance} m) exceeds the allowed {limit} m radius from Tower #1 ({towerCode}).` |
| `MSG-303` | Error banner | Thiếu GPS khi Check-in | Không lấy được tọa độ GPS từ thiết bị. Vui lòng bật định vị trước khi Check-in. | `Device GPS coordinates are unavailable for check-in verification.` |
| `MSG-304` | Toast | Check-in thành công | Check-in thành công tại Cột số 1 (`{distance}` m). Nhiệm vụ đã sẵn sàng cất cánh. | `Check-in verified ({distance} m from Tower #1). Mission is Ready.` |
| `MSG-305` | Error banner | Chưa đủ điều kiện bắt đầu bay | Không thể bắt đầu bay: cần đủ 3 thành viên xác nhận, đã Check-in và Pre-Mission còn hiệu lực. | `Cannot start flight: check-in or valid pre-mission readiness missing.` |
| `MSG-306` | Status | Trạng thái Telemetry MF03 | Đang nhận Telemetry trực tiếp (`/hubs/telemetry`) · Mất tín hiệu sau > 30s (`DroneOffline`). | `Live telemetry active / UAV telemetry offline (> 30s).` |
| `MSG-307` | Toast | Hoàn thành / Hủy Mission | Đã `{Hoàn thành/Hủy}` nhiệm vụ và tự động trả Drone về trạng thái Sẵn sàng. | `Mission {Completed/Cancelled}; assigned UAV returned to Available.` |
| `MSG-401` | Error banner | Sai định dạng / vượt dung lượng upload | Tệp `{File}` không hợp lệ. Ảnh chỉ nhận JPEG/PNG/WebP tối đa `20 MB`; Video nhận MP4 tối đa `200 MB`. | `Unsupported file {File}. Allowed: JPEG/PNG/WebP <= 20 MB, MP4 <= 200 MB.` |
| `MSG-402` | Info | Trùng file SHA-256 | Tệp `{File}` đã được tải lên cho nhiệm vụ này trước đó và đã được bỏ qua. | `{File} matches an existing mission upload and was skipped.` |
| `MSG-403` | Status | Đã gửi AI phân tích | Đã tiếp nhận dữ liệu (đang đối chiếu EXIF GPS / trích xuất frame video). | `Media queued for AI inspection and GPS tower matching.` |
| `MSG-404` | Toast | AI xử lý xong / Tự gắn cột | Phân tích AI hoàn tất (`{count}` phát hiện). Các lỗi có GPS đã được tự gắn cột gần nhất. | `AI analysis completed with {count} detections.` |
| `MSG-405` | Error banner | AI thất bại / timeout | Phân tích AI thất bại. Vui lòng bấm Thử lại (`Retry`) hoặc kiểm tra AI Worker. | `AI analysis failed. Retry the job or inspect worker status.` |
| `MSG-406` | Toast | Lưu duyệt khuyết tật thành công | Đã `{Xác nhận/Loại bỏ}` phát hiện. `{ticketInfo}` | `Detection {Approved/Rejected}.` |
| `MSG-407` | Error banner | Không có quyền duyệt khuyết tật (`403`) | Chỉ Chuyên viên phân tích (`Analyst`) thuộc nhiệm vụ này (hoặc `SystemAdmin`) mới được duyệt kết quả. | `Only the mission's assigned Analyst (or SystemAdmin) can validate detections.` |
| `MSG-408` | Toast | Đổi cột thủ công thành công | Đã cập nhật cột điện gắn với khuyết tật sang `{towerLabel}`. | `Anomaly tower assignment updated.` |
| `MSG-501` | Toast | Tự gán Phiếu Bảo trì thành công | Đã tạo Phiếu bảo trì `#...` và tự động gán cho Kỹ thuật viên `{technicianName}`. | `Maintenance ticket created and auto-assigned to {technicianName}.` |
| `MSG-502` | Error banner | Đóng phiếu thiếu minh chứng | Vui lòng tải lên đầy đủ ảnh minh chứng trước và sau sửa chữa trước khi gửi nghiệm thu. | `Before and after maintenance proof photos are required before submission.` |
| `MSG-503` | Toast | Cập nhật trạng thái Phiếu bảo trì | Đã chuyển trạng thái phiếu bảo trì sang `{status}`. | `Maintenance ticket moved to {status}.` |
| `MSG-504` | Toast | Xuất báo cáo / Lưu SystemSettings | `{Đã xuất báo cáo thành công / Đã lưu cấu hình hệ thống}`. | `{Report exported / System settings updated}.` |

### 4.2. Ma trận Notification theo Role & Quy tắc Chống Trùng Lặp Popup

> **Quy tắc chống duplicate**:
> - **Backend (`NotificationService`)**: Mỗi hành động chỉ phát **1 tên event SignalR duy nhất**; `NotificationConnectionRegistry` hợp nhất `connectionId` bằng **`Distinct`** và dọn sạch dynamic group trong `OnDisconnectedAsync`.
> - **Frontend (`notifications-realtime.ts` / `store`)**: Dedupe theo `notificationId` / `eventId` (dự phòng `type + referenceId + actorId` trong cửa sổ ngắn) trước khi bật Toast.

| Sự kiện nghiệp vụ | Đối tượng nhận chính thức *(+ `SystemAdmin` xem toàn hệ thống)* | Kênh gửi |
|---|---|---|
| **Tạo / Giao lại nhiệm vụ (`dispatch` / `reassign`)** | `Inspector`, `Analyst`, `Technician` được phân công | SignalR + DB |
| **Phản hồi nhận / hoãn nhiệm vụ (`Accept` / `Postpone`)** | `Manager` tạo nhiệm vụ | SignalR + DB |
| **Check-in hiện trường tại Cột 1 thành công** | `Manager` tạo nhiệm vụ *(+ thành viên đang mở màn Mission nhận reload UI)* | SignalR + DB |
| **Hoàn tất upload / AI xử lý xong hoặc lỗi** | `Analyst` của nhiệm vụ *(+ thông báo Thành công/Thất bại cho `Inspector` nộp)* | SignalR + DB |
| **Duyệt khuyết tật & Tự sinh/gán Phiếu Bảo trì** | `Technician` được gán phiếu + `Manager` của nhiệm vụ | SignalR + DB *(+ Email nếu `Emergency`)* |
| **Xử lý / Nộp minh chứng / Trả lại / Đóng Phiếu Bảo trì** | Hai chiều giữa `Technician` xử lý phiếu ↔ `Manager` phụ trách | SignalR + DB |

### 4.3. Danh mục 6 Hangfire Cron Triggers & 14 `SystemSettings` Keys

| Nhóm | Key trong `SystemSettings` *(Chính xác từng chữ)* | Mặc định | Đơn vị | Ai đọc & Điều kiện áp dụng |
|---|---|---:|---|---|
| **Check-in** | `CheckInRadiusMeters` | `50` | mét | `OperationsService` (bán kính Haversine quanh Cột 1) |
| **MF01** | `PreMissionMaxValidityDays` | `7` | ngày | `OperationsService` (trần hiệu lực Assessment) |
| **MF01** | `PreMissionMaxWindSpeedMs` | `12` | m/s | `OperationsService` (ngưỡng gió an toàn) |
| **MF01** | `PreMissionMinBatteryPct` | `30` | % | `OperationsService` (pin tối thiểu Drone) |
| **Upload** | `MaxImageSizeMb` | `20` | MB | `AIInspectionService` + FE upload guard |
| **Upload** | `MaxVideoSizeMb` | `200` | MB | `AIInspectionService` + FE upload guard |
| **Auth** | `AccessTokenExpiryMinutes` | `15` | phút | `IdentityService` + FE Silent Refresh |
| **Auth** | `RefreshTokenExpiryDays` | `7` | ngày | `IdentityService` |
| **Cleanup** | `StandaloneMediaRetentionDays` | `7` | ngày | Hangfire job dọn file Standalone Test trên S3 |
| **Hangfire 1** | *(Dùng `ConfirmationDeadline` của Mission)* | — | — | Quá hạn `PendingAcceptance` → Báo thành viên chưa `Accept` + `Manager` (`SignalR + DB`) |
| **Hangfire 2** | *(Dùng `PreMission` `ValidUntil` ≤ `7` ngày)* | — | — | Assessment hết hạn → Báo `Manager`, `Inspector` (`SignalR + DB`) |
| **Hangfire 3** | `RemindBeforeFlightMinutes` | `30` | phút | Trước `ScheduledStart` 30p → Báo `Inspector` được giao + `Manager` (`SignalR + DB`) |
| **Hangfire 4** | `CheckInOverdueMinutes` | `15` | phút | Sau `ScheduledStart` 15p chưa check-in → Báo `Inspector`, `Manager` (`SignalR + Email`) |
| **Hangfire 5** | `EmergencyAiReviewOverdueMinutes` | `30` | phút | Khuyết tật `Emergency` chờ duyệt > 30p → Báo `Analyst` của Mission (`SignalR + Email`) |
| **Hangfire 6** | `MaintenanceRemindNormalHours` / `MaintenanceRemindEmergencyHours` | `24` / `2` | giờ | Nhắc trước `DueDate` và khi quá hạn → Báo `Technician` + `Manager` (`SignalR + Email` nếu khẩn/quá hạn) |

---

## Mục 5 — Entity Rules & State Machines (7 Entity Cốt lõi)

> **Lưu ý cho Report 3 (Section 5.4.2)**: Xóa hoàn toàn **Table 44 (Emergency alert states)** khỏi SRS vì phân hệ `EmergencyAlerts` đã bị gỡ bỏ. Giữ và cập nhật 7 bảng trạng thái chuẩn dưới đây:

### 5.1. `User` & Session (`RefreshToken` / `TrustedDevice`)
- **Ràng buộc**: Email duy nhất toàn hệ thống; SĐT duy nhất khi không rỗng; `UserRoles` ≥ 1 role chuẩn; `SystemAdmin` bỏ qua `UserGeographicScopes`. Không tự gán `SystemAdmin` khi tạo user thiếu role.

| Trạng thái | Ý nghĩa | Chuyển tiếp hợp lệ | Actor & Tác dụng phụ |
|---|---|---|---|
| `Active` | Hoạt động bình thường (đã/chưa verify email) | `Locked` (sai MK 5 lần hoặc Admin khóa), `Deactivated` (xóa mềm Admin) | Login thành công sau verify OTP SMTP lần đầu |
| `Locked` | Bị tạm khóa 15 phút hoặc Admin đình chỉ | `Active` (hết 15p hoặc Admin mở khóa) | Khi Admin khóa → thu hồi toàn bộ `RefreshTokens` & `TrustedDevices` |
| `Deactivated` | Ngưng vĩnh viễn (`IsDeleted = true`) | *(Trạng thái cuối — không tái sử dụng ID/Email)* | Thu hồi mọi token; chặn gán vào Mission mới |

### 5.2. `UAV` (`FlightStatus`) & `DroneTechnicalInspection` (`TechnicalStatus`)
- **Ràng buộc**: `UAV` chỉ được đặt lịch khi vừa `Available` vừa có lần kiểm tra BIST gần nhất đạt `Ready` (`Đạt chuẩn / Sẵn sàng`). Không cho xóa mềm nếu đang có `ResourceBookings` của Mission chưa kết thúc.

| Trạng thái `UAV` | Ý nghĩa | Chuyển tiếp hợp lệ | Điều kiện / Tác dụng phụ |
|---|---|---|---|
| `Available` | Sẵn sàng phân công bay | `InMission` (bắt đầu bay), `InMaintenance` (BIST không đạt / bảo trì), `Retired` (ngưng dùng) | Bắt buộc BIST gần nhất `Ready` để vào MF01/MF02 |
| `InMission` | Đang thực hiện nhiệm vụ | `Available` (Mission `Completed` hoặc `Cancelled`) | Tự động trả về `Available` không cần bàn giao |
| `InMaintenance` | Đang bảo trì / BIST không đạt | `Available` (`Technician`/`SystemAdmin` kiểm tra BIST đạt `Ready`), `Retired` | Bị chặn chọn trong MF01/MF02 |
| `Retired` | Ngưng khai thác vĩnh viễn | *(Trạng thái cuối)* | Giữ lịch sử bay/BIST để truy vết |

*(`DroneTechnicalInspection` chỉ có 2 trạng thái chuẩn UI: **`Ready`** [`Đạt chuẩn / Sẵn sàng`] | **`MaintenanceRequired`** [`Không đạt / Cần bảo trì`], thao tác bởi `Technician` hoặc `SystemAdmin` tại màn Chi tiết Drone.)*

### 5.3. `PreMissionAssessment` (MF01)
- **Ràng buộc**: Không chứa BIST Drone; chỉ chứa danh sách Cột điện (`≥ 1`), UAV đã `Ready`, điều kiện thời tiết, và số lượng nhân sự theo role (`PreMissionAssessmentPersonnel`). Hạn hiệu lực tối đa **≤ 7 ngày**.

| Trạng thái | Ý nghĩa | Chuyển tiếp hợp lệ | Actor & Điều kiện |
|---|---|---|---|
| `Evaluating` | Đang tổng hợp đánh giá các tiêu chí | `Ready`, `NotReady`, `Cancelled`, `Expired` | Hệ thống đánh giá tự động |
| `Ready` | Đạt đủ điều kiện, sẵn sàng tạo Mission | `Completed` (tạo thành công 1 Mission), `Evaluating`, `Cancelled`, `Expired` | `Manager` / `SystemAdmin` tạo Mission |
| `NotReady` | Có ít nhất 1 tiêu chí chưa đạt | `Evaluating` (cập nhật & đánh giá lại), `Cancelled`, `Expired` | Hiển thị rõ danh sách lý do chưa đạt |
| `Completed` | Đã tiêu thụ để tạo 1 Mission | *(Trạng thái cuối — 1 Assessment chỉ tạo 1 Mission)* | Giữ liên kết `Missions.PreMissionAssessmentId` |
| `Expired` | Quá hạn `ValidUntil` (`≤ 7 ngày`) | *(Trạng thái cuối)* | Hangfire kiểm tra & báo `Manager`, `Inspector` |
| `Cancelled` | Đã hủy trước khi tạo Mission | *(Trạng thái cuối)* | `Manager` / `SystemAdmin` |

### 5.4. `Mission` (9 Trạng thái)

| Trạng thái | Ý nghĩa | Chuyển tiếp hợp lệ | Điều kiện bắt buộc |
|---|---|---|---|
| `Draft` | Nháp, đang cấu hình mục tiêu/UAV/3 role | `PendingAcceptance` (`dispatch`), `Cancelled` (`cancelMission`) | Đủ ≥ 1 cột, ≥ 1 UAV sẵn sàng, đủ 3 role đích danh |
| `PendingAcceptance` | Đã giao, chờ đủ 3 role bấm `Accept` | `Confirmed` (đủ 3/3 `Accept`), `Postponed` (`requestPostpone`), `Cancelled` | Cả `Inspector`, `Analyst`, `Technician` đều phải `Accept` |
| `Postponed` | Có thành viên xin hoãn hoặc chờ đổi lịch | `PendingAcceptance` (`reschedule`), `Cancelled` | `Manager` xếp lịch/người mới → phát lại xác nhận |
| `Confirmed` | Đủ 3/3 role đã xác nhận; khóa lịch bay | `Ready` (`checkIn`), `PendingAcceptance` (`reschedule/reassign`), `Cancelled` | Chỉ `Inspector` được giao / `SystemAdmin` Check-in GPS `≤ 50m` tại Cột 1 |
| `Ready` | Đã Check-in hợp lệ tại Cột 1 | `InProgress` (`startMission`), `Cancelled` (`emergencyCancel`) | Kiểm tra lại Pre-Mission còn hạn |
| `InProgress` | Đang bay thực địa & stream Telemetry RAM | `Suspended` (`pauseExecution`), `Completed` (`finishInspection`), `Cancelled` (`criticalAbort`) | Telemetry qua `/hubs/telemetry` RAM-only `≤ 10s` |
| `Suspended` | Tạm dừng giữa chừng (thời tiết/pin) | `InProgress` (`resumeExecution`), `Cancelled` (`abortMission`) | Lưu lý do tạm dừng |
| `Completed` | Hoàn thành bay thực địa | *(Trạng thái cuối)* | Tự động trả UAV về `Available` & giải phóng booking |
| `Cancelled` | Đã hủy nhiệm vụ | *(Trạng thái cuối)* | Tự động trả UAV về `Available` & giải phóng booking |

### 5.5. `MissionAssignment` (Áp dụng cho cả 3 Role trong Mission)

| Trạng thái | Ý nghĩa | Chuyển tiếp hợp lệ | Quyền & Ghi chú |
|---|---|---|---|
| `Pending` | Chờ thành viên phản hồi trước hạn | `Accepted`, `Postponed`, `Expired` (Hangfire quá hạn), `Cancelled` | Chỉ chính người được gán bấm phản hồi |
| `Accepted` | Đã xác nhận tham gia | `Pending` (khi đổi lịch/nhân sự), `Cancelled` (khi hủy Mission) | Khi cả 3 role = `Accepted` → Mission sang `Confirmed` |
| `Postponed` | Thành viên xin hoãn kèm lý do | `Pending` (đổi lịch mới), `Cancelled` (gán người thay thế) | Báo `Manager` ngay lập tức |
| `Expired` | Quá `ConfirmationDeadline` chưa phản hồi | `Pending` (gia hạn), `Cancelled` (đổi người) | Hangfire cảnh báo cho người thiếu + `Manager` |
| `Cancelled` | Phân công bị hủy/thay thế | *(Trạng thái cuối)* | Giải phóng `ResourceBookings` |

### 5.6. `AIAnalysisRun` & `DetectedAnomaly` (MF04)

| Entity | Trạng thái | Nhãn UI Tiếng Việt *(Chuẩn v2.0)* | Chuyển tiếp hợp lệ & Ghi chú |
|---|---|---|---|
| **`AIAnalysisRun`** | `Pending` | Đang chờ xử lý | `Processing` (FastAPI nhận qua RabbitMQ), `Failed` |
| **`AIAnalysisRun`** | `Processing` | Đang phân tích AI | `Completed` (lưu detections `0..100%` BBox + gắn cột gần nhất), `Failed` |
| **`AIAnalysisRun`** | `Completed` | Hoàn tất phân tích | *(Trạng thái cuối cho lần chạy đó)* |
| **`AIAnalysisRun`** | `Failed` | Phân tích thất bại | `Pending` (`Retry` bởi `Analyst` / `SystemAdmin`) |
| **`DetectedAnomaly`** | `Pending` | **`Chờ duyệt`** | `Approved` (chỉ `Analyst` Mission / `SystemAdmin`), `Rejected` |
| **`DetectedAnomaly`** | `Approved` | **`Đã xác nhận`** *(Cấm ghi `"AI Review"`)* | `Resolved` (khi `MaintenanceTicket` đóng). **Khóa cứng (immutable)** sau duyệt; tự tạo `MaintenanceTicket` (`Assigned` nếu Mission có `Technician`, ngược lại `Open`) |
| **`DetectedAnomaly`** | `Rejected` | **`Đã loại bỏ`** *(Cấm ghi `"AI Review"`)* | *(Khóa cứng — loại khỏi phiếu bảo trì & thống kê lỗi)* |
| **`DetectedAnomaly`** | `Resolved` | **`Đã khắc phục`** | *(Trạng thái cuối khi `MaintenanceTicket` chuyển `Closed`)* |

### 5.7. `MaintenanceTicket` (6 Trạng thái)

| Trạng thái | Ý nghĩa | Chuyển tiếp hợp lệ | Actor & Tác dụng phụ |
|---|---|---|---|
| `Open` | Phiếu tự sinh từ defect không gắn sẵn `Technician` | `Assigned` (`Manager` gán người), `Cancelled` | Không thể nhảy thẳng sang `InProgress` khi chưa gán `Technician` |
| `Assigned` | **Mặc định khi `Approve` defect thuộc Mission có `Technician`** | `InProgress` (`startMaintenance`), `Cancelled` | Tự bắn SignalR báo `Technician` được gán + `Manager` |
| `InProgress` | `Technician` đang sửa hiện trường & ghi vật tư | `PendingVerification` (`submitProof`) | Bắt buộc có `MaintenanceProofs` trước/sau |
| `PendingVerification` | Đã nộp minh chứng, chờ `Manager` nghiệm thu | `Closed` (`approveClosure`), `InProgress` (`rejectProof` trả lại làm lại) | Bắn Notification về `Manager` |
| `Closed` | Đã nghiệm thu hoàn tất | *(Trạng thái cuối)* | Tự chuyển `DetectedAnomaly` cha sang `Resolved` |
| `Cancelled` | Hủy phiếu trước khi hoàn thành | *(Trạng thái cuối)* | `Manager` / `SystemAdmin` |

---

## Mục 6 — Database Sau Khi DROP & Thay đổi Cách Lưu trữ (Storage Architecture)

### 6.1. Các bảng BỊ DROP / Ngưng ghi từ nền ERD v2.2 (42 → 37 bảng nghiệp vụ)

*Nền ERD v2.2 tại commit gốc có **42 bảng nghiệp vụ**. Sau đợt dọn dẹp v2.0 (qua EF Core Migration trên GitHub Actions, `OperationsService` sở hữu DB):*

| Bảng bị xử lý | Hành động v2.0 | Lý do gỡ bỏ & Thay thế hiện hành |
|---|---|---|
| `EmergencyAlerts` | **DROP** | Không tách phân hệ Alert riêng; mức khẩn là trường `Severity = Emergency \| Normal` trên `DetectedAnomalies` & `MaintenanceTickets`. |
| `AlertEscalations` | **DROP** | Theo phân hệ Emergency Alerts bị gỡ; nhắc quá hạn dùng Hangfire cron + Notification Matrix. |
| `IncidentReports` | **DROP** | Ngoài phạm vi MVP hiện hành; sự cố hủy/hoãn ghi thẳng vào lịch sử `Missions` & `AuditLogs`. |
| `InspectionSchedules` | **DROP** | Không chạy lịch định kỳ tự động ngoài Pre-Mission & Hangfire nhắc hạn. |
| `DroneHandovers` | **DROP** | Gỡ quy trình bàn giao/trả Drone thủ công (`drone-handover`); kết thúc/hủy Mission tự giải phóng UAV về `Available`. |
| `DroneTelemetry` *(nếu còn trong DB nhánh cũ)* | **DROP hoặc 0 writes** | MF03 chỉ đẩy Telemetry **100% trên RAM** qua SignalR `/hubs/telemetry`, tuyệt đối không INSERT/UPDATE vào PostgreSQL. |

> **Tổng kết số lượng bảng PostgreSQL sau khi DROP**:
> **37 bảng nghiệp vụ** *(42 − 5)* + **3 nhóm bảng hạ tầng hệ thống** (`__EFMigrationsHistory`, PostGIS `spatial_ref_sys`, bảng nội bộ `Hangfire`).

### 6.2. Danh mục 37 Bảng Nghiệp vụ Giữ lại trong PostgreSQL

| Nhóm phân hệ | Số bảng | Tên bảng vật lý trong PostgreSQL (`ERD v2.2 → v2.0`) |
|---|:---:|---|
| **1. Identity, RBAC & Audit** | **7** | `Users`, `Roles`, `UserRoles`, `UserGeographicScopes`, `RefreshTokens`, `TrustedDevices`, `AuditLogs` |
| **2. Assets, Drone BIST & Pre-Mission** | **10** | `Regions`, `Towers`, `UAVs`, `DroneFlightControllers`, `DroneTechnicalInspections`, `DroneTechnicalMetrics`, `PreMissionAssessments`, `PreMissionAssessmentTowers`, `PreMissionAssessmentPersonnel`, `PreMissionAssessmentDrones` |
| **3. Missions & Execution** | **8** | `Missions`, `MissionPriorityDefects`, `MissionTargets`, `MissionAssignments`, `ResourceBookings`, `MissionCommunicationLogs`, `MissionCheckIns`, `MissionFlightLogs` *(`DroneHandovers` đã DROP)* |
| **4. AI Inspection & Review** | **4** | `InspectionMedia`, `AIAnalysisRuns`, `DefectCategories`, `DetectedAnomalies` |
| **5. Maintenance, Reports & System** | **8** | `MaintenanceTickets`, `MaintenanceProofs`, `MaterialLogs`, `Reports`, `ReportMissions`, `Notifications`, `SystemSettings`, `OutboxMessages` |

*(Kiểm đếm chính xác: `7 + 10 + 8 + 4 + 8 = 37` bảng nghiệp vụ — khớp đúng `42 − 5` bảng đã DROP ở Mục 6.1.)*

### 6.3. Thay đổi Cột, Ràng buộc & Cách lưu trên Các Bảng Giữ lại

| Bảng | Thay đổi Cách lưu & Ràng buộc Chuẩn v2.0 |
|---|---|
| **`InspectionMedia`** | - `MissionId` thành **`uuid NULL`** (cho phép Standalone Test của `Analyst` / `SystemAdmin`).<br>- `TowerId` thành **`uuid NULL`** (cho phép upload ảnh không chọn cột trước; gắn sau bằng EXIF GPS / GPS live video hoặc đổi tay).<br>- Giữ `Checksum varchar(64)` chống trùng trong cùng Mission; `FileUrl` lưu khóa S3 **`bucket/object`** + EF value converter dựng URL công khai khi đọc. |
| **`DetectedAnomalies`** | - `TowerId` **`uuid NULL`** và **gỡ CHECK constraint bắt buộc `TowerId IS NOT NULL` khi duyệt** (lỗi nằm trên dây giữa 2 cột vẫn duyệt hợp lệ, UI hiện *"Chưa gắn cột"* thay vì `"N/A"`).<br>- `Location` dùng PostGIS `geometry(Point, 4326)` lưu GPS từ EXIF ảnh hoặc GPS live frame video; `FrameTimeSeconds` lưu giây lỗi trên video.<br>- Tọa độ Bounding Box chuẩn hóa **`0..100%`** trước khi lưu/trả FE. |
| **`PreMissionAssessmentPersonnel`** | - Lưu **số lượng nhân sự yêu cầu theo từng role** (`InspectorCount`, `AnalystCount`, `TechnicianCount`) cho MF01 — không ép chọn đích danh người tại bước Pre-Mission. |
| **`MissionAssignments`** | - Bắt buộc có đủ 3 dòng đại diện 3 role (`Inspector`, `Analyst`, `Technician`) khi giao Mission MF02. |
| **`MissionTargets`** | - Bắt buộc có cột **`sequence = 1`** cùng tọa độ GPS PostGIS để thuật toán Haversine Check-in 50m so sánh khoảng cách. |
| **`MaintenanceTickets`** | - `TechnicianId` nullable: khi `Approve` khuyết tật thuộc Mission có `Technician` → tự điền `TechnicianId` + `AssignedAt = UTC now` + `Status = Assigned`; chỉ để `NULL` (`Open`) khi Mission không có hoặc chưa có Technician. Không lưu thừa `TowerId` (suy ra từ `Anomaly.TowerId`). |
| **`Reports`** | - Không lưu cột URL file PDF/Excel; khi Status = `Approved` đóng băng dữ liệu vào cột **`Snapshot jsonb`**; khi bấm tải mới render PDF/Excel động. |
| **`Notifications`** | - Dùng timestamp `ReadAt` / `PushedAt`; gắn thêm các cột/trạng thái `*NotifiedAt` trên các thực thể liên quan để Hangfire không gửi thông báo trùng mỗi phút. |

### 6.4. Phân tầng Lưu trữ Toàn hệ thống (Storage Matrix)

| Hạ tầng Lưu trữ | Dữ liệu được lưu & Quy tắc vận hành | Tuyệt đối KHÔNG lưu ở đây |
|---|---|---|
| **PostgreSQL + PostGIS** | 37 bảng nghiệp vụ (UTC `Z`, WGS 84 `EPSG:4326`), `__EFMigrationsHistory`, `spatial_ref_sys`, Hangfire jobs | Dòng telemetry bay từng giây (`DroneTelemetry`), mật khẩu plain-text, secret cấu hình thô qua API |
| **Redis** | Mã OTP (kèm TTL), danh sách `SystemSettings` cache, trạng thái tạm | Dữ liệu nghiệp vụ chính dài hạn (Mission, Ticket, Anomaly) |
| **S3 / MinIO Object Storage** | Ảnh UAV gốc, Video MP4 gốc, Frame ảnh cắt từ lỗi video, Ảnh minh chứng bảo trì Trước/Sau — DB lưu khóa **`bucket/object`** | Ảnh base64 nhúng thẳng vào bảng PostgreSQL hoặc response JSON |
| **RabbitMQ (+ Outbox)** | Hàng đợi phân tích AI giữa `AIInspectionService` ↔ `FastAPI AI Worker`, sự kiện domain bất đồng bộ (`OutboxMessages`) | Gói tin Telemetry real-time độ trễ cao |
| **RAM / SignalR (`/hubs/telemetry`, `/hubs/notifications`)** | Luồng Telemetry MF03 (pin, vị trí GPS, độ cao, `DroneOnline`/`Offline`), registry `connectionId` (`Distinct`, tự dọn trong `OnDisconnectedAsync`) | Ghi xuống đĩa hoặc PostgreSQL cho luồng bay real-time |
| **Local Assets (`d:\UavPms_FEE\public\maps\`)** | Bản đồ vector offline `evnspc-south-z12.pmtiles` + dữ liệu schematic local fallback | Gọi tile/CDN từ Google Maps, OpenStreetMap, Mapbox, ArcGIS |
