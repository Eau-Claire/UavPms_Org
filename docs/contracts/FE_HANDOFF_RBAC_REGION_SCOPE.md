# FE Handoff — RBAC theo vùng (BE `feat/rbac-region-scope`)

> Dán nguyên file này cho agent FE. Repo FE: `UavPms_FE`. BE đã xong và test xanh; **chưa merge `main`, chưa deploy**.
> Nguồn chuẩn: `docs/MASTER_SYSTEM_SPEC.md` §4 (v2.1) và `docs/RBAC_PERMISSIONS.md` (bảng đầy đủ từng endpoint).
> Không đổi URL, request body hay response schema. Chỉ đổi **ai được gọi** và **dữ liệu nào được trả**.

## 1. Luật mới (FE cần hiểu trước khi sửa)

- `SystemAdmin`: mọi thao tác, mọi vùng.
- 4 role còn lại chỉ thấy dữ liệu trong **vùng được gán** (`UserGeographicScopes` + vùng con) hoặc **nhiệm vụ mình đang được giao**.
- User chưa được gán vùng (kể cả Manager) → danh sách rỗng, dashboard = 0. Đây là đúng, không phải lỗi.
- Lỗi quyền trả `403` dạng `{ success: false, errorCode: "FORBIDDEN", message: "<MÃ_CỤ_THỂ>" }`. FE đọc mã ở `message` (đã có sẵn cơ chế trong `src/app/shared/errors/business-error-messages.ts`).

## 2. Việc cần làm theo màn hình

| # | Màn / file | Việc |
|---|---|---|
| 1 | `src/app/core/auth/route-access.ts` → `ROUTE_ROLES.aiAnalysis` | Đổi thành `['Admin', 'Analyst']` (Manager không còn upload; standalone chỉ Admin, Analyst) |
| 2 | `ROUTE_ROLES.preMission` | Đổi thành `['Admin', 'Manager']` (Inspector, Technician bị `403` khi đọc Pre-Mission) |
| 3 | `ROUTE_ROLES.administration` / route `/system/audit-logs` | Tách audit logs ra key riêng chỉ `['Admin', 'SystemAdmin']`; `/admin/users` giữ Admin + Manager |
| 4 | Sidebar + route `/reports` | Inspector: ẩn "Báo cáo" và thêm guard (`GET/POST /reports*` bỏ Inspector) |
| 5 | Quản lý UAV (`drone-management`) | Technician: hiện nút "Xóa UAV" (`DELETE /drones/{id}` thêm Technician, chỉ Drone trong vùng) |
| 6 | Chi tiết phiếu bảo trì (`proof-upload`, vật tư) | Manager: ẩn "Thêm minh chứng" và "Thêm vật tư" (chỉ Technician được giao + Admin) |
| 7 | Nhiệm vụ hiện trường (`mission-inspector`, `mission-detail`) | Chỉ hiện nút **Check-in** cho user có assignment `INSPECTOR`; Analyst/Technician chỉ có Accept. Trạng thái `Ready` chỉ chờ Inspector check-in |
| 8 | Chi tiết nhiệm vụ → Telemetry / phát lại bay | Technician: ẩn tab (`GET /missions/{id}/telemetry` bỏ Technician) |
| 9 | Kết quả giám định (defect của nhiệm vụ) | Technician được xem tab defect. Nút Duyệt / Đổi cột: hiện cho SystemAdmin và Analyst (BE tự chặn Analyst ngoài vùng bằng `DEFECT_REVIEW_DENIED`) |
| 10 | Dashboard `monitor/*` | Không đổi API. Thêm empty state khi số liệu = 0 và user chưa có vùng: "Bạn chưa được gán khu vực, liên hệ quản trị viên" |
| 11 | Gán vùng cho user (`user-scope-dialog`) | Manager chỉ được chọn vùng của chính mình; vùng ngoài phạm vi → `403 REGION_OUTSIDE_MANAGER_SCOPE`. Nên lọc danh sách vùng theo vùng của Manager |
| 12 | Nếu còn gọi `POST /alerts/trigger` | Bắt buộc gửi `missionId` hoặc `anomalyId` (không gửi → `400 MISSION_REQUIRED`) |

## 3. API đổi hành vi (không đổi schema)

| API | Thay đổi |
|---|---|
| `GET /monitor/*` (summary, mission-status, inspections, recent-defects, defects-statistics, alerts) | Chỉ đếm nhiệm vụ người gọi được xem |
| `GET /alerts`, `/alerts/active`, `/alerts/{id}`, `PUT /alerts/{id}/review`, `POST /alerts/{id}/escalate` | Chỉ cảnh báo của nhiệm vụ được xem; ngoài phạm vi: chi tiết `null`/404, review/escalate trả lỗi |
| `POST /alerts/trigger` | Không còn tự gắn nhiệm vụ mới nhất; cần `missionId`/`anomalyId` được xem |
| `GET /drones`, `/drones/available`, `/drones/{id}`, `/drones/{id}/status` | Drone trong vùng hoặc của nhiệm vụ được giao; ngoài phạm vi: `403 DRONE_ACCESS_DENIED` |
| `GET /drones/{code}/telemetry/latest` | Cùng luật trên; Manager không còn xem mọi vùng → `403 DRONE_TELEMETRY_ACCESS_DENIED` |
| `POST /ai-analysis/upload` không có `missionId` | File vào nhiệm vụ riêng mã `STD-...` của người upload (không lẫn vào nhiệm vụ thật). `missionId` trả về là nhiệm vụ `STD-` này |
| `GET /anomalies*`, `GET /missions/{id}/detections` | Lọc theo nhiệm vụ được xem |
| `GET /maintenance/tickets*` | Bỏ Inspector, Analyst; Manager theo vùng, Technician phiếu được giao |
| `GET /audit-logs`, `/system-settings*` | Chỉ SystemAdmin |

## 4. Mã lỗi cần thêm vào `business-error-messages.ts`

```ts
CHECK_IN_INSPECTOR_ONLY: 'Chỉ Inspector được giao trong nhiệm vụ mới được check-in.',
MISSION_DEFECT_ACCESS_DENIED: 'Bạn không có quyền xem kết quả giám định của nhiệm vụ này.',
DEFECT_REVIEW_DENIED: 'Chỉ Analyst của nhiệm vụ hoặc của khu vực mới được duyệt khuyết tật này.',
UPLOAD_NOT_ALLOWED: 'Bạn không có quyền tải ảnh/video cho nhiệm vụ này.',
REGION_OUTSIDE_MANAGER_SCOPE: 'Khu vực này nằm ngoài phạm vi bạn quản lý.',
TICKET_REGION_SCOPE_REQUIRED: 'Phiếu bảo trì nằm ngoài khu vực bạn quản lý.',
DRONE_TELEMETRY_ACCESS_DENIED: 'Bạn không có quyền xem telemetry của UAV này.',
```

Đã có sẵn, giữ nguyên: `MISSION_ACCESS_DENIED`, `DRONE_ACCESS_DENIED`, `MISSION_REQUIRED`.

## 5. Test FE phải sửa theo

- `sidebar.spec.ts`: menu theo role mới (mục 1–4).
- `drone-management.spec.ts`: Technician thấy nút Xóa.
- Trên `main` FE có sẵn 10 test fail không liên quan (facility-store, active-learning). Đừng tính vào batch này.

## 6. Checklist nghiệm thu FE

- [ ] Manager không thấy menu Upload AI, Nhật ký hệ thống, Cài đặt hệ thống
- [ ] Inspector, Technician không thấy Đánh giá tiền bay; Inspector không thấy Báo cáo
- [ ] Analyst/Technician không có nút Check-in; Inspector check-in xong thì nhiệm vụ `Ready`
- [ ] Technician xóa được UAV trong vùng; Manager không thấy nút thêm minh chứng/vật tư
- [ ] Mọi `403` hiện câu tiếng Việt ở mục 4, không hiện "FORBIDDEN"
- [ ] User chưa có vùng thấy empty state, không vỡ trang
- [ ] `npm run lint` + test pass (trừ 10 test fail sẵn trên `main`)
