# Ma trận phân quyền BE (RBAC + phạm vi vùng)

Cập nhật 28/11/2026, branch `feat/rbac-region-scope`. Bảng endpoint sinh tự động từ `[Authorize(Roles = …)]` trong code (trước = `main` `fb7c28f`, sau = branch này). Nguồn yêu cầu: `docs/MASTER_SYSTEM_SPEC.md` §4.

## 1. Nguyên tắc

- **SystemAdmin**: toàn quyền, mọi vùng, không lọc `UserGeographicScopes`.
- **Manager, Inspector, Analyst, Technician**: chỉ dữ liệu trong vùng được gán (vùng trong `UserGeographicScopes` và vùng con trực tiếp). Thao tác trong nhiệm vụ (Inspector, Analyst, Technician) theo nhiệm vụ được giao.
- Quyền gồm 2 lớp: role ở attribute `[Authorize]` (bảng mục 4) và kiểm tra vùng / phân công trong service (mục 3).
- Manager chưa được gán vùng **không thấy dữ liệu nào** (trước đây thấy toàn bộ). Admin phải gán vùng.

## 2. Thay đổi so với `main`

| Endpoint | Trước | Sau | Ghi chú |
|---|---|---|---|
| `GET /ai-analysis/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Inspector, Analyst | Chỉ người xem được defect của nhiệm vụ |
| `GET /missions/{missionId:guid}/ai-analysis/detections` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Inspector, Analyst, Technician | Mọi role; chỉ người tham gia nhiệm vụ, Manager/Analyst của vùng |
| `PUT /missions/{missionId:guid}/ai-analysis/detections/{detectionId:guid}/review` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin; Analyst phải được giao hoặc thuộc vùng nhiệm vụ |
| `POST /ai-analysis/upload` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Inspector, Analyst | Bỏ Manager; vào nhiệm vụ: Inspector/Analyst được giao hoặc Analyst của vùng; standalone: chỉ SystemAdmin, Analyst |
| `PUT /anomalies/{id:guid}/tower` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin; Analyst được giao hoặc của vùng |
| `GET /anomalies/{id:guid}/tower-suggestions` | SystemAdmin, Manager, Analyst | SystemAdmin, Manager, Analyst | Chỉ người xem được defect |
| `GET /anomalies/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Chỉ người tham gia nhiệm vụ, Manager/Analyst của vùng |
| `GET /anomalies` | SystemAdmin, Manager, Analyst | SystemAdmin, Manager, Analyst | Lọc trong SQL: nhiệm vụ mình phụ trách / được giao / thuộc vùng (Manager, Analyst) |
| `GET /anomalies/pending` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin; lọc theo nhiệm vụ được giao / vùng |
| `PUT /anomalies/{id:guid}/validate` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin; Analyst được giao hoặc của vùng |
| `GET /audit-logs` | SystemAdmin, Manager | SystemAdmin | Bỏ Manager |
| `GET /drones` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Mới: chỉ liệt kê Drone trong vùng (Admin: mọi vùng) |
| `GET /drones/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Mới: Drone trong vùng, hoặc Drone của nhiệm vụ mình đang được giao |
| `POST /drones` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: chỉ tạo Drone trong vùng của mình |
| `PUT /drones/{id:guid}` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: chỉ sửa Drone trong vùng; chuyển vùng thì vùng mới cũng phải thuộc phạm vi |
| `DELETE /drones/{id:guid}` | SystemAdmin, Manager | SystemAdmin, Manager, Technician | Thêm Technician; mọi role trừ Admin chỉ xóa Drone trong vùng |
| `POST /inspections/upload` | SystemAdmin, Manager, Inspector | SystemAdmin, Inspector, Analyst | Thêm Analyst, bỏ Manager; Inspector/Analyst được giao hoặc Analyst của vùng |
| `POST /maintenance/tickets` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ tạo phiếu cho defect trong vùng |
| `PUT /maintenance/tickets/{id:guid}/assign` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ giao phiếu trong vùng / phiếu phụ trách |
| `PUT /maintenance/tickets/{id:guid}/status` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: Manager chỉ thao tác phiếu trong vùng / phiếu phụ trách |
| `POST /maintenance/tickets/{id:guid}/proof` | SystemAdmin, Manager, Technician | SystemAdmin, Technician | Bỏ Manager (chỉ Technician được giao + Admin) |
| `POST /maintenance/tickets/{id:guid}/materials` | SystemAdmin, Manager, Technician | SystemAdmin, Technician | Bỏ Manager (chỉ Technician được giao + Admin) |
| `PUT /maintenance/tickets/{id:guid}/close` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ đóng phiếu trong vùng / phiếu phụ trách |
| `GET /maintenance/tickets` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Technician | Bỏ Inspector, Analyst; lọc: Manager theo vùng / phiếu phụ trách, Technician phiếu được giao |
| `GET /maintenance/tickets/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Technician | Bỏ Inspector, Analyst; cùng luật lọc như danh sách |
| `POST /missions/{id:guid}/check-in` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Inspector | Chỉ Inspector được giao (và SystemAdmin khi được giao làm Inspector); mission Ready khi Inspector đã check-in |
| `GET /missions/{id:guid}/telemetry` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst | Bỏ Technician |
| `POST /missions/{id:guid}/media` | SystemAdmin, Manager, Inspector | SystemAdmin, Inspector, Analyst | Thêm Analyst, bỏ Manager; Inspector/Analyst được giao hoặc Analyst của vùng |
| `GET /missions/{id:guid}/detections` | Analyst | SystemAdmin, Manager, Inspector, Analyst, Technician | Mọi role; chỉ người tham gia nhiệm vụ, Manager/Analyst của vùng |
| `POST /missions/{missionId:guid}/detections/{detectionId:guid}/review` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin; Analyst được giao hoặc của vùng |
| `GET /pre-mission-assessments` | SystemAdmin, Manager, Inspector, Technician | SystemAdmin, Manager, Inspector, Technician | Chỉ SystemAdmin và Manager của vùng (bỏ Inspector, Technician) |
| `GET /pre-mission-assessments/{id:guid}` | SystemAdmin, Manager, Inspector, Technician | SystemAdmin, Manager, Inspector, Technician | Chỉ SystemAdmin, người tạo, Manager của vùng |
| `GET /pre-mission-assessments/{id:guid}/resource-readiness` | SystemAdmin, Manager, Inspector | SystemAdmin, Manager, Inspector, Technician | Chỉ SystemAdmin và Manager của vùng (bỏ Inspector) |
| `GET /regions` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Manager chưa được gán vùng không còn thấy mọi vùng |
| `GET /reports` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Analyst, Technician | Bỏ Inspector |
| `GET /reports/statistics` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Analyst, Technician | Bỏ Inspector |
| `GET /reports/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Analyst, Technician | Bỏ Inspector |
| `POST /reports` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `PUT /reports/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `DELETE /reports/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `PUT /reports/{id:guid}/submit` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `POST /reports/{id:guid}/generate` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `GET /reports/{id:guid}/download` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Analyst | Bỏ Inspector |
| `GET /system-settings` | SystemAdmin, Manager | SystemAdmin | Bỏ Manager |
| `GET /system-settings/{key}` | SystemAdmin, Manager | SystemAdmin | Bỏ Manager |
| `POST /system-settings/reload` | SystemAdmin, Manager | SystemAdmin | Bỏ Manager |
| `GET /towers` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Manager chưa được gán vùng không còn thấy mọi vùng |
| `PUT /users/{userId:guid}/geographic-scopes` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ gán/gỡ vùng của chính mình; vùng khác của user được giữ |

## 3. Luật trong service (không thấy ở attribute)

| Chức năng | Luật | Nơi kiểm tra |
|---|---|---|
| Drone: tạo / sửa / xóa | SystemAdmin mọi vùng; Manager, Technician chỉ Drone thuộc vùng của mình; chuyển vùng thì vùng mới cũng phải thuộc phạm vi | `UavManagementService` + `DroneRegionScope` |
| Drone: danh sách, chi tiết, `available`, `status`, `telemetry/latest` | SystemAdmin mọi Drone; người khác Drone thuộc vùng mình (gồm vùng con) hoặc Drone của nhiệm vụ mình đang được giao | `DroneScope` (dùng ở `UavManagementService`, `UavRepository`, `DroneTelemetryService`) |
| Dashboard `monitor/*` | Chỉ đếm nhiệm vụ / ảnh / defect / cảnh báo của nhiệm vụ người gọi được xem: phụ trách, được giao (Active) hoặc thuộc vùng; SystemAdmin tất cả | `MissionScope` (dùng ở `MonitorRepository`) |
| Cảnh báo khẩn `alerts/*` | Danh sách, chi tiết, duyệt, leo thang chỉ cảnh báo của nhiệm vụ người gọi được xem; tạo cảnh báo bắt buộc `missionId` hoặc `anomalyId` người gọi được xem (`MISSION_REQUIRED`, `MISSION_ACCESS_DENIED`) | `EmergencyAlertService` |
| Check-in hiện trường | Chỉ thành viên được giao với vai trò INSPECTOR; mission Ready khi mọi Inspector đã Accept đều check-in (Analyst, Technician chỉ Accept) | `MissionLifecycleService.CheckInAsync`, `Mission.RecalculateReadiness` |
| Pre-Mission: xem | SystemAdmin, người tạo, Manager có vùng của hồ sơ | `PreMissionAssessmentService` |
| Defect / kết quả AI: xem | SystemAdmin, Manager phụ trách nhiệm vụ, thành viên được giao, Manager/Analyst có vùng của nhiệm vụ | `IMissionAccessPolicy` (AI), `MissionLifecycleService` (Operations) |
| Defect: duyệt, đổi cột | SystemAdmin; Analyst được giao hoặc Analyst có vùng của nhiệm vụ | `IMissionAccessPolicy` (AI), `MissionLifecycleService` (Operations) |
| Danh sách defect | Lọc trong SQL theo `MissionVisibility` (nhiệm vụ phụ trách / được giao / thuộc vùng) | `AnomalyRepository` |
| Upload ảnh/video | SystemAdmin; Inspector hoặc Analyst được giao; Analyst của vùng. Standalone: SystemAdmin, Analyst. Manager, Technician không upload | `IMissionAccessPolicy` (AI), `UploadInspectionImageCommandHandler` (Operations) |
| Upload standalone (không `missionId`) | Chỉ gom vào nhiệm vụ `STD-` của chính người gọi, chưa có thì tạo mới; không bao giờ gắn vào nhiệm vụ thật hay nhiệm vụ người khác | `UploadStandaloneAnalysisCommandHandler.ResolveMissionAsync` |
| Phiếu bảo trì | Admin tất cả; Manager phiếu phụ trách hoặc trong vùng; Technician phiếu được giao; minh chứng/vật tư chỉ Technician được giao | `MaintenanceTicketService` |
| Gán vùng cho người dùng | SystemAdmin tùy ý; Manager chỉ vùng của chính mình, giữ nguyên vùng khác của người dùng | `SetUserGeographicScopesCommandHandler` |
| Cột điện, khu vực | Manager chưa có vùng không còn thấy toàn bộ | `GeographicAccessFilter` |

## 4. Bảng đầy đủ sau thay đổi

✓ = được gọi. "—" = endpoint không dùng JWT (đăng nhập, khóa nội bộ). Cột **Đổi** đánh dấu dòng khác `main` hoặc có luật vùng mới.

### AIInspection

| Method | Endpoint | Admin | Manager | Inspector | Analyst | Technician | Đổi |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| POST | `/ai-analysis/upload` | ✓ |  | ✓ | ✓ |  | ● |
| GET | `/ai-analysis/{id:guid}` | ✓ | ✓ | ✓ | ✓ |  | ● |
| GET | `/anomalies` | ✓ | ✓ |  | ✓ |  | ● |
| GET | `/anomalies/pending` | ✓ |  |  | ✓ |  | ● |
| GET | `/anomalies/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| PUT | `/anomalies/{id:guid}/tower` | ✓ |  |  | ✓ |  | ● |
| GET | `/anomalies/{id:guid}/tower-suggestions` | ✓ | ✓ |  | ✓ |  | ● |
| PUT | `/anomalies/{id:guid}/validate` | ✓ |  |  | ✓ |  | ● |
| GET | `/missions/{missionId:guid}/ai-analysis/detections` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| PUT | `/missions/{missionId:guid}/ai-analysis/detections/{detectionId:guid}/review` | ✓ |  |  | ✓ |  | ● |
| POST | `/missions/{missionId:guid}/ai-analysis/from-media/{mediaId:guid}` | ✓ | ✓ |  | ✓ |  |  |

### Identity

| Method | Endpoint | Admin | Manager | Inspector | Analyst | Technician | Đổi |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| POST | `/auth/login` | — | — | — | — | — |  |
| POST | `/auth/otp/send` | — | — | — | — | — |  |
| POST | `/auth/otp/verify` | — | — | — | — | — |  |
| POST | `/auth/refresh-token` | — | — | — | — | — |  |
| POST | `/auth/reset-password` | — | — | — | — | — |  |
| POST | `/auth/validate-session` | — | — | — | — | — |  |
| GET | `/users` | ✓ |  |  |  |  |  |
| POST | `/users` | ✓ |  |  |  |  |  |
| GET | `/users/assignable` | ✓ | ✓ |  |  |  |  |
| POST | `/users/change-password` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/users/me` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/users/{id:guid}` | ✓ |  |  |  |  |  |
| GET | `/users/{id:guid}` | ✓ |  |  |  |  |  |
| PATCH | `/users/{id:guid}` | ✓ |  |  |  |  |  |
| PUT | `/users/{id:guid}` | ✓ |  |  |  |  |  |
| POST | `/users/{id:guid}/reset-password` | ✓ |  |  |  |  |  |
| POST | `/users/{id:guid}/suspend` | ✓ |  |  |  |  |  |

### Notification

| Method | Endpoint | Admin | Manager | Inspector | Analyst | Technician | Đổi |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| GET | `/notifications` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/notifications/realtime/assessment-event` | — | — | — | — | — |  |
| POST | `/notifications/realtime/mission-event` | — | — | — | — | — |  |
| GET | `/notifications/unread-count` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/notifications/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/notifications/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/notifications/{id:guid}/read` | ✓ | ✓ | ✓ | ✓ | ✓ |  |

### Operations

| Method | Endpoint | Admin | Manager | Inspector | Analyst | Technician | Đổi |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| GET | `/alerts` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/alerts/active` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/alerts/trigger` | ✓ | ✓ | ✓ | ✓ |  |  |
| GET | `/alerts/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/alerts/{id:guid}/escalate` | ✓ | ✓ |  | ✓ |  |  |
| PUT | `/alerts/{id:guid}/review` | ✓ | ✓ |  | ✓ |  |  |
| GET | `/audit-logs` | ✓ |  |  |  |  | ● |
| POST | `/drone-technical-inspections` | ✓ |  |  |  | ✓ |  |
| GET | `/drone-technical-inspections/drone/{droneId:guid}/latest` | ✓ | ✓ | ✓ |  | ✓ |  |
| GET | `/drones` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| POST | `/drones` | ✓ | ✓ |  |  | ✓ | ● |
| GET | `/drones/available` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/drones/eligible` | ✓ | ✓ | ✓ |  | ✓ |  |
| GET | `/drones/estimate-gis-scope` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/drones/estimate-gis-scope` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/drones/{droneCode}/telemetry/latest` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/drones/{droneId:guid}/flight-controller` | ✓ |  |  |  | ✓ |  |
| GET | `/drones/{droneId:guid}/flight-controller` | ✓ | ✓ | ✓ |  | ✓ |  |
| POST | `/drones/{droneId:guid}/flight-controller` | ✓ |  |  |  | ✓ |  |
| GET | `/drones/{droneId:guid}/flight-controller/history` | ✓ | ✓ | ✓ |  | ✓ |  |
| DELETE | `/drones/{id:guid}` | ✓ | ✓ |  |  | ✓ | ● |
| GET | `/drones/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| PUT | `/drones/{id:guid}` | ✓ | ✓ |  |  | ✓ | ● |
| POST | `/drones/{id:guid}/basic-technical-check` | ✓ |  |  |  | ✓ |  |
| GET | `/drones/{id:guid}/status` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/drones/{id:guid}/technical-inspections` | ✓ | ✓ | ✓ |  | ✓ |  |
| GET | `/flight-controllers/{uid}/drone` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/infrastructure` | — | — | — | — | — |  |
| GET | `/inspection-schedules` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/inspection-schedules` | ✓ | ✓ |  |  |  |  |
| DELETE | `/inspection-schedules/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/inspection-schedules/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/inspection-schedules/{id:guid}` | ✓ | ✓ |  |  |  |  |
| POST | `/inspection-schedules/{id:guid}/activate` | ✓ | ✓ |  |  |  |  |
| POST | `/inspection-schedules/{id:guid}/deactivate` | ✓ | ✓ |  |  |  |  |
| PATCH | `/inspection-schedules/{id:guid}/toggle` | ✓ | ✓ |  |  |  |  |
| POST | `/inspection-schedules/{id:guid}/trigger` | ✓ | ✓ |  |  |  |  |
| GET | `/inspections/mission/{missionId:guid}` | ✓ | ✓ | ✓ | ✓ |  |  |
| GET | `/inspections/report/{id:guid}` | ✓ | ✓ | ✓ | ✓ |  |  |
| POST | `/inspections/upload` | ✓ |  | ✓ | ✓ |  | ● |
| GET | `/maintenance/tickets` | ✓ | ✓ |  |  | ✓ | ● |
| POST | `/maintenance/tickets` | ✓ | ✓ |  |  |  | ● |
| GET | `/maintenance/tickets/my` |  |  |  |  | ✓ |  |
| GET | `/maintenance/tickets/technicians` | ✓ | ✓ |  |  |  |  |
| GET | `/maintenance/tickets/{id:guid}` | ✓ | ✓ |  |  | ✓ | ● |
| PUT | `/maintenance/tickets/{id:guid}/assign` | ✓ | ✓ |  |  |  | ● |
| PUT | `/maintenance/tickets/{id:guid}/close` | ✓ | ✓ |  |  |  | ● |
| POST | `/maintenance/tickets/{id:guid}/materials` | ✓ |  |  |  | ✓ | ● |
| POST | `/maintenance/tickets/{id:guid}/proof` | ✓ |  |  |  | ✓ | ● |
| PUT | `/maintenance/tickets/{id:guid}/status` | ✓ | ✓ |  |  | ✓ | ● |
| GET | `/missions` | ✓ | ✓ |  | ✓ |  |  |
| POST | `/missions/check-resource-conflicts` | ✓ | ✓ |  |  |  |  |
| GET | `/missions/my` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/missions/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/missions/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/missions/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/missions/{id:guid}/activities` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/activities` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/missions/{id:guid}/assignments` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/assignments` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/assignments/accept` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/assignments/postpone` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/missions/{id:guid}/assignments/{assignmentId:guid}` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/assignments/{assignmentId:guid}/reassign` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/cancel` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/check-in` | ✓ |  | ✓ |  |  | ● |
| GET | `/missions/{id:guid}/communications` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/communications` | ✓ | ✓ | ✓ |  |  |  |
| POST | `/missions/{id:guid}/complete` | ✓ | ✓ | ✓ |  |  |  |
| POST | `/missions/{id:guid}/confirm` | ✓ | ✓ | ✓ |  |  |  |
| GET | `/missions/{id:guid}/detections` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| PUT | `/missions/{id:guid}/drone` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/flight-log` | ✓ | ✓ | ✓ |  |  |  |
| GET | `/missions/{id:guid}/flight-logs` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/missions/{id:guid}/incidents` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/incidents` | ✓ | ✓ | ✓ |  |  |  |
| GET | `/missions/{id:guid}/maintenance-tasks` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/missions/{id:guid}/media` | ✓ |  | ✓ | ✓ |  | ● |
| POST | `/missions/{id:guid}/postpone` | ✓ | ✓ |  |  |  |  |
| PATCH | `/missions/{id:guid}/priority` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/remind` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/resume` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/scope/resolve` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{id:guid}/start` | ✓ | ✓ | ✓ |  |  |  |
| POST | `/missions/{id:guid}/suspend` | ✓ | ✓ |  |  |  |  |
| GET | `/missions/{id:guid}/telemetry` | ✓ | ✓ | ✓ | ✓ |  | ● |
| PUT | `/missions/{id:guid}/towers` | ✓ | ✓ |  |  |  |  |
| POST | `/missions/{missionId:guid}/detections/{detectionId:guid}/review` | ✓ |  |  | ✓ |  | ● |
| GET | `/monitor/alerts` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/monitor/defects-statistics` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/monitor/inspections` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/monitor/mission-status` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/monitor/recent-defects` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/monitor/summary` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| GET | `/pre-mission-assessments` | ✓ | ✓ | ✓ |  | ✓ | ● |
| POST | `/pre-mission-assessments` | ✓ | ✓ |  |  |  |  |
| GET | `/pre-mission-assessments/{id:guid}` | ✓ | ✓ | ✓ |  | ✓ | ● |
| POST | `/pre-mission-assessments/{id:guid}/cancel` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/confirm` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/consume` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/create-mission` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/evaluate` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/mark-completed` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/re-evaluate` | ✓ | ✓ |  |  |  |  |
| GET | `/pre-mission-assessments/{id:guid}/resource-readiness` | ✓ | ✓ | ✓ |  | ✓ | ● |
| GET | `/regions` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| POST | `/regions` | ✓ | ✓ |  |  |  |  |
| DELETE | `/regions/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/regions/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/regions/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/reports` | ✓ | ✓ |  | ✓ | ✓ | ● |
| POST | `/reports` | ✓ | ✓ |  | ✓ |  | ● |
| GET | `/reports/statistics` | ✓ | ✓ |  | ✓ | ✓ | ● |
| DELETE | `/reports/{id:guid}` | ✓ | ✓ |  | ✓ |  | ● |
| GET | `/reports/{id:guid}` | ✓ | ✓ |  | ✓ | ✓ | ● |
| PUT | `/reports/{id:guid}` | ✓ | ✓ |  | ✓ |  | ● |
| PUT | `/reports/{id:guid}/approve` | ✓ | ✓ |  |  |  |  |
| GET | `/reports/{id:guid}/download` | ✓ | ✓ |  | ✓ |  | ● |
| POST | `/reports/{id:guid}/generate` | ✓ | ✓ |  | ✓ |  | ● |
| PUT | `/reports/{id:guid}/reject` | ✓ | ✓ |  |  |  |  |
| PUT | `/reports/{id:guid}/submit` | ✓ | ✓ |  | ✓ |  | ● |
| GET | `/system-settings` | ✓ |  |  |  |  | ● |
| POST | `/system-settings/reload` | ✓ |  |  |  |  | ● |
| GET | `/system-settings/{key}` | ✓ |  |  |  |  | ● |
| PUT | `/system-settings/{key}` | ✓ |  |  |  |  |  |
| GET | `/towers` | ✓ | ✓ | ✓ | ✓ | ✓ | ● |
| POST | `/towers` | ✓ | ✓ |  |  |  |  |
| POST | `/towers/import` | ✓ | ✓ |  |  |  |  |
| POST | `/towers/spatial-query` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| DELETE | `/towers/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/towers/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/towers/{id:guid}` | ✓ | ✓ |  |  |  |  |
| GET | `/towers/{id:guid}/history` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| POST | `/uavs/{droneId:guid}/inspections` | ✓ |  |  |  | ✓ |  |
| GET | `/users/{userId:guid}/geographic-scopes` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/users/{userId:guid}/geographic-scopes` | ✓ | ✓ |  |  |  | ● |

## 5. Sửa spec `MASTER_SYSTEM_SPEC.md` §4 (cần `SDD_PHASE=SPEC`)

Hook chặn sửa spec trong pha `CODE`. Thay các dòng sau trong bảng ma trận phân quyền:

| Dòng hiện tại | Thay bằng |
|---|---|
| `Quản lý Region, Tower, UAV, Flight Controller` — Technician: Xem *(trong vùng)* | Tách 2 dòng: `Quản lý Region, Tower, Flight Controller` (giữ nguyên) và `Quản lý UAV / Drone (tạo, sửa, xóa)`: SystemAdmin ✅, Manager ✅ *(trong vùng)*, Inspector Xem *(trong vùng)*, Analyst Xem *(trong vùng)*, **Technician ✅ *(trong vùng)*** |
| `Tạo / Duyệt Pre-Mission Assessment` — Inspector/Analyst/Technician: Xem *(trong vùng)* | Inspector ❌, Analyst ❌, Technician ❌ (chỉ SystemAdmin và Manager của vùng) |
| `Check-in hiện trường` | Giữ: chỉ Inspector được giao. Thêm ghi chú: mission `Ready` khi Inspector đã check-in; Analyst, Technician chỉ `Accept` |
| `Upload Ảnh / Video cho AI (Mission)` — Analyst: ✅ | Analyst: ✅ *(được giao hoặc trong vùng)* |
| `Xem danh sách & chi tiết Defect trong Nhiệm vụ` — Analyst: ✅ *(mission được giao)* | Analyst: ✅ *(mission được giao hoặc trong vùng)* |
| `Duyệt Defect` — Analyst: ✅ *(duy nhất: Analyst trong mission đó)* | Analyst: ✅ *(Analyst được giao hoặc Analyst của vùng nhiệm vụ)* |

## 6. Còn lại, chưa làm

| # | Việc | Ghi chú |
|---|---|---|
| 1 | `POST missions/{id}/ai-analysis/from-media/{mediaId}` (chạy lại AI) vẫn cho Manager | Không phải upload; giữ nguyên |
| 2 | Upload bên OperationsService chỉ xét vùng được gán trực tiếp (chưa tính vùng con) | Bên AIInspectionService đã tính vùng con |

## 7. FE cần làm theo (chưa sửa)

| Màn | Việc |
|---|---|
| Sidebar, `route-access.ts` | Manager: bỏ "Nhật ký hệ thống" (`/system/audit-logs` chỉ SystemAdmin) |
| Sidebar, route `/reports` | Inspector: bỏ "Báo cáo", thêm guard |
| Sidebar, route `/pre-mission` | Inspector, Technician: bỏ "Đánh giá tiền bay" (chỉ Manager, Admin) |
| Quản lý UAV | Technician: hiện nút "Xóa UAV" |
| Chi tiết phiếu bảo trì | Manager: ẩn thêm minh chứng / vật tư |
| Nhiệm vụ (màn hiện trường) | Chỉ Inspector thấy nút Check-in; Analyst, Technician chỉ Accept |
| AI Studio upload | Manager: ẩn trang upload (`aiAnalysis` đang có Manager) |
| Kết quả giám định | Technician xem tab defect; SystemAdmin và Analyst của vùng được duyệt |

Test FE đang ghi luật cũ và sẽ phải sửa: `sidebar.spec.ts`, `drone-management.spec.ts`. Trên `main` FE có sẵn 10 test fail không liên quan (facility-store, active-learning).

## 8. Ảnh hưởng khi deploy

- Manager chưa có vùng không thấy gì: production có 1 tài khoản `khanhnqse19+manager@gmail.com`.
- 4/7 Inspector chưa có vùng: danh sách Drone trống; dữ liệu nhiệm vụ được giao vẫn xem được.
- Production đang có check-in của Analyst/Technician (luồng cũ); sau deploy họ bị `403 CHECK_IN_INSPECTOR_ONLY`, nhiệm vụ không còn chờ họ check-in.
- Production có 17 file do Manager upload; sau deploy Manager không upload được.
- Dashboard và cảnh báo khẩn của Manager/Inspector/Analyst/Technician chỉ còn số liệu nhiệm vụ trong vùng hoặc được giao; người chưa có vùng và chưa được giao thấy 0.
- `POST /alerts/trigger` không còn tự lấy nhiệm vụ mới nhất; client phải gửi `missionId` hoặc `anomalyId`.
- Upload standalone cũ đã nằm trong nhiệm vụ của người khác thì giữ nguyên; chỉ upload mới đi vào nhiệm vụ `STD-` riêng.

## 9. Kiểm chứng

- `dotnet build UavPms.sln -c Release`: thành công.
- OperationsService: unit 440/440 (gồm `RegionVisibilityTests`), integration (PostgreSQL thật, Testcontainers, gồm các test khóa BE-REQ-101 và `RegionVisibilityQueryTests`) 142/142.
- AIInspectionService: 85/85 (gồm `MissionAccessPolicyTests`, `StandaloneMissionResolutionTests`).
- IdentityService: 93/93.
