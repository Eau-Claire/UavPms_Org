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
| `GET /missions/{missionId:guid}/ai-analysis/detections` | SystemAdmin, Manager, Inspector, Analyst | SystemAdmin, Manager, Inspector, Analyst, Technician | Thêm Technician (chỉ nhiệm vụ được giao) |
| `PUT /missions/{missionId:guid}/ai-analysis/detections/{detectionId:guid}/review` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin |
| `PUT /anomalies/{id:guid}/tower` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin |
| `GET /anomalies/pending` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin |
| `PUT /anomalies/{id:guid}/validate` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin |
| `GET /audit-logs` | SystemAdmin, Manager | SystemAdmin | Bỏ Manager |
| `GET /drones` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Mới: chỉ liệt kê Drone trong vùng (Admin: mọi vùng) |
| `GET /drones/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst, Technician | Mới: Drone trong vùng, hoặc Drone của nhiệm vụ mình đang được giao |
| `POST /drones` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: chỉ tạo Drone trong vùng của mình |
| `PUT /drones/{id:guid}` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: chỉ sửa Drone trong vùng; chuyển vùng thì vùng mới cũng phải thuộc phạm vi |
| `DELETE /drones/{id:guid}` | SystemAdmin, Manager | SystemAdmin, Manager, Technician | Thêm Technician; mọi role trừ Admin chỉ xóa Drone trong vùng |
| `POST /inspections/upload` | SystemAdmin, Manager, Inspector | SystemAdmin, Manager, Inspector, Analyst | Thêm Analyst |
| `POST /maintenance/tickets` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ tạo phiếu cho defect trong vùng |
| `PUT /maintenance/tickets/{id:guid}/assign` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ giao phiếu trong vùng / phiếu phụ trách |
| `PUT /maintenance/tickets/{id:guid}/status` | SystemAdmin, Manager, Technician | SystemAdmin, Manager, Technician | Mới: Manager chỉ thao tác phiếu trong vùng / phiếu phụ trách |
| `POST /maintenance/tickets/{id:guid}/proof` | SystemAdmin, Manager, Technician | SystemAdmin, Technician | Bỏ Manager (chỉ Technician được giao + Admin) |
| `POST /maintenance/tickets/{id:guid}/materials` | SystemAdmin, Manager, Technician | SystemAdmin, Technician | Bỏ Manager (chỉ Technician được giao + Admin) |
| `PUT /maintenance/tickets/{id:guid}/close` | SystemAdmin, Manager | SystemAdmin, Manager | Mới: Manager chỉ đóng phiếu trong vùng / phiếu phụ trách |
| `GET /maintenance/tickets` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Technician | Bỏ Inspector, Analyst; lọc: Manager theo vùng / phiếu phụ trách, Technician phiếu được giao |
| `GET /maintenance/tickets/{id:guid}` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Technician | Bỏ Inspector, Analyst; cùng luật lọc như danh sách |
| `GET /missions/{id:guid}/telemetry` | SystemAdmin, Manager, Inspector, Analyst, Technician | SystemAdmin, Manager, Inspector, Analyst | Bỏ Technician |
| `POST /missions/{id:guid}/media` | SystemAdmin, Manager, Inspector | SystemAdmin, Manager, Inspector, Analyst | Thêm Analyst |
| `GET /missions/{id:guid}/detections` | Analyst | SystemAdmin, Manager, Inspector, Analyst, Technician | Mở cho mọi thành viên nhiệm vụ + Manager vùng (service kiểm tra) |
| `POST /missions/{missionId:guid}/detections/{detectionId:guid}/review` | Analyst | SystemAdmin, Analyst | Thêm SystemAdmin |
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

| Chức năng | Luật mới | Nơi kiểm tra |
|---|---|---|
| Drone: tạo / sửa / xóa | SystemAdmin mọi vùng; Manager, Technician chỉ Drone thuộc vùng của mình; sửa vùng thì vùng mới cũng phải thuộc phạm vi | `UavManagementService` + `DroneRegionScope` |
| Drone: danh sách | Chỉ Drone của vùng người gọi; Drone chưa gắn vùng chỉ Admin thấy | `UavManagementService.GetDronesAsync` |
| Drone: chi tiết | Drone trong vùng, hoặc Drone của nhiệm vụ người gọi đang được giao | `UavManagementService.GetDroneByIdAsync` |
| Phiếu bảo trì: xem | Admin tất cả; Manager phiếu mình phụ trách hoặc trong vùng (vùng theo cột điện của defect, defect chưa gắn cột thì theo vùng nhiệm vụ); Technician phiếu được giao | `MaintenanceTicketService` |
| Phiếu bảo trì: tạo / giao / đóng / đổi trạng thái | Manager chỉ trong vùng hoặc phiếu mình phụ trách | `MaintenanceTicketService` |
| Phiếu bảo trì: minh chứng, vật tư | Chỉ Technician được giao và SystemAdmin | `MaintenanceTicketService` |
| Gán vùng cho người dùng | SystemAdmin: tùy ý. Manager: chỉ vùng của chính mình, vùng khác của người dùng được giữ nguyên, không tự sửa vùng của mình | `SetUserGeographicScopesCommandHandler` |
| Cột điện, khu vực (đọc) | Manager chưa có vùng không còn thấy toàn bộ | `GeographicAccessFilter` |
| Xem defect trong nhiệm vụ (Operations) | SystemAdmin; Manager phụ trách hoặc Manager có vùng của nhiệm vụ; mọi thành viên đang được giao | `MissionLifecycleService.GetMissionDetectionsAsync` |
| Duyệt defect trong nhiệm vụ (Operations) | Analyst được giao trong nhiệm vụ và SystemAdmin | `MissionLifecycleService.ReviewDetectionAsync` |
| Xem defect trong nhiệm vụ (AI) | Technician chỉ nhiệm vụ được giao (Inspector, Manager giữ luật cũ) | `GetMissionAiDetectionsQueryHandler` |

## 4. Bảng đầy đủ sau thay đổi

✓ = được gọi. "—" = endpoint không dùng JWT (đăng nhập, khóa nội bộ). Cột **Đổi** đánh dấu dòng khác `main` hoặc có luật vùng mới.

### AIInspection

| Method | Endpoint | Admin | Manager | Inspector | Analyst | Technician | Đổi |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| POST | `/ai-analysis/upload` | ✓ | ✓ | ✓ | ✓ |  |  |
| GET | `/ai-analysis/{id:guid}` | ✓ | ✓ | ✓ | ✓ |  |  |
| GET | `/anomalies` | ✓ | ✓ |  | ✓ |  |  |
| GET | `/anomalies/pending` | ✓ |  |  | ✓ |  | ● |
| GET | `/anomalies/{id:guid}` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
| PUT | `/anomalies/{id:guid}/tower` | ✓ |  |  | ✓ |  | ● |
| GET | `/anomalies/{id:guid}/tower-suggestions` | ✓ | ✓ |  | ✓ |  |  |
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
| POST | `/inspections/upload` | ✓ | ✓ | ✓ | ✓ |  | ● |
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
| POST | `/missions/{id:guid}/check-in` | ✓ | ✓ | ✓ | ✓ | ✓ |  |
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
| POST | `/missions/{id:guid}/media` | ✓ | ✓ | ✓ | ✓ |  | ● |
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
| GET | `/pre-mission-assessments` | ✓ | ✓ | ✓ |  | ✓ |  |
| POST | `/pre-mission-assessments` | ✓ | ✓ |  |  |  |  |
| GET | `/pre-mission-assessments/{id:guid}` | ✓ | ✓ | ✓ |  | ✓ |  |
| POST | `/pre-mission-assessments/{id:guid}/cancel` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/confirm` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/consume` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/create-mission` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/evaluate` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/mark-completed` | ✓ | ✓ |  |  |  |  |
| POST | `/pre-mission-assessments/{id:guid}/re-evaluate` | ✓ | ✓ |  |  |  |  |
| GET | `/pre-mission-assessments/{id:guid}/resource-readiness` | ✓ | ✓ | ✓ |  |  |  |
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

## 5. Chưa làm, cần nhóm quyết

| # | Việc | Lý do chưa làm |
|---|---|---|
| 1 | Check-in chỉ Inspector (và SystemAdmin) như spec | Luồng hiện tại cả 3 role đều check-in (production: 4 check-in mỗi role) và mission chỉ `Ready` khi **mọi** thành viên đã Accept đều check-in (`Mission.RecalculateReadiness`). Đổi quyền phải đổi luôn điều kiện Ready và nút check-in trên FE. |
| 2 | Bỏ quyền upload của Manager (`POST /ai-analysis/upload`, `/inspections/upload`, `/missions/{id}/media`) | Production có 17 file do Manager upload vào nhiệm vụ: luồng đang được dùng. |
| 3 | Analyst / Technician xem `pre-mission-assessments/{id}/resource-readiness`, Analyst xem Pre-Mission | Luật đọc Pre-Mission trong `PreMissionAssessmentService` phức tạp (Inspector/Technician xem mọi vùng theo commit `0933670`), FE cũng chặn Analyst. |
| 4 | Lọc vùng danh sách anomaly (`GET /anomalies`) | AIInspectionService chưa map `UserGeographicScopes`, `Regions`; cần thêm entity/mapping. |
| 5 | Analyst duyệt defect phải thuộc nhiệm vụ (AIInspectionService) | `ReviewMissionAiDetectionCommandHandler` và `PUT /anomalies/{id}/validate` không kiểm tra phân công: Analyst nào cũng duyệt được defect của mọi nhiệm vụ (phát hiện khi rà). Bên OperationsService đã kiểm tra. |
| 6 | Telemetry realtime `/hubs/telemetry` | Chưa rà SignalR hub; chỉ sửa endpoint REST. |

## 6. FE cần làm theo (chưa sửa)

| Màn | Việc |
|---|---|
| Sidebar, `route-access.ts` | Manager: bỏ mục "Nhật ký hệ thống" (`/system/audit-logs` giờ chỉ SystemAdmin) |
| Sidebar, route `/reports` | Inspector: bỏ mục "Báo cáo", thêm guard cho route |
| Quản lý UAV | Technician: hiện nút "Xóa UAV" (`canManageFleet` đang chỉ Admin/Manager) |
| Chi tiết phiếu bảo trì | Manager: ẩn nút thêm minh chứng / vật tư (`canAddEvidence`), chỉ còn nghiệm thu, trả lại, đóng |
| Kết quả giám định trong nhiệm vụ | Technician được xem tab defect; SystemAdmin được duyệt |

Test FE đang ghi luật cũ và sẽ phải sửa theo: `sidebar.spec.ts` (menu Inspector, link phụ Manager), `drone-management.spec.ts` (Technician không xóa). Trên `main` FE có sẵn 10 test fail không liên quan (facility-store, active-learning).

## 7. Ảnh hưởng khi deploy

- Manager chưa có vùng sẽ không thấy gì. Production hiện có 1 tài khoản như vậy: `khanhnqse19+manager@gmail.com`.
- 4/7 Inspector chưa có vùng: danh sách Drone của họ sẽ trống; chi tiết Drone của nhiệm vụ được giao vẫn xem được.
- Spec `MASTER_SYSTEM_SPEC.md` §4 dòng "Quản lý Region, Tower, UAV, Flight Controller" còn ghi Technician chỉ **Xem**: cần sửa (ở `SDD_PHASE=SPEC`) thành Technician quản lý UAV trong vùng.

## 8. Kiểm chứng

- `dotnet build UavPms.sln -c Release`: thành công.
- Unit test: OperationsService 428/428, AIInspectionService 77/77, IdentityService pass.
- Integration test OperationsService (PostgreSQL thật qua Testcontainers): 141/141.
- Test mới: Drone theo vùng (5), phiếu bảo trì theo vùng (3), Manager gán vùng (1), xem/duyệt defect trong nhiệm vụ (2), Technician xem defect AI (2), RBAC attribute AnomaliesController (1).
