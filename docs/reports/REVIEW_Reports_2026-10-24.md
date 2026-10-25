# Review Report 1 – 3 (24/10/2026)

Phạm vi: `report1/GFA26SE66_Report1_Project_Introduction.md`, `report2/GFA26SE66_Report2_Project_Management_Plan.docx.md`, `report3/GFA26SE66_Report3_Software_Requirements_Specification.docx.md` (bản export từ Docs) và `report3/..._v0.4.md` (bản SRS đã căn theo code). Đối chiếu với BE (`E:\UavPms_BE\UavPms_Org`) và FE (`E:\UavPms_FE\UavPms_FE`).

Ký hiệu cột **Sửa ở đâu**: *Doc* = chỉ sửa tài liệu; *Code* = tài liệu đúng, code phải sửa; *Quyết định* = nhóm cần chọn sửa bên nào.

---

## 0. Vấn đề chung quan trọng nhất

| # | Vấn đề | Sửa ở đâu |
|---|---|---|
| 0.1 | Có **hai bản Report 3 lệch nhau**. File `.docx.md` (vừa export) là nội dung cũ ~07/10: login bằng username + khoá tài khoản, kết quả *Needs Review*, *Site Conditions*, *Field Completed*, flight altitude/speed, Field Execution Console… — đều đã bị loại bỏ ở v0.3/v0.4 vì không khớp code. Bản đúng là `_v0.4.md`. | Doc: đưa nội dung v0.4 (đã cập nhật hôm nay) vào Google Docs rồi export lại |
| 0.2 | **Asset Health đã bỏ khỏi scope nhưng code BE và FE vẫn còn và vẫn chạy**: trừ điểm sức khoẻ khi confirm defect, cộng +15 khi đóng ticket, `GET /assets/health-summary`, cột `HealthScore/RiskLevel`, bảng `AssetHealthHistory`, `TowerConditionHistory`; FE `/assets` vẫn là `AssetHealthDashboard` (“Sức khỏe & Rủi ro Thiết bị”), GIS drawer, tab *Tài sản* trong Mission Detail (dữ liệu mock 38/54/68/92/96), notification `newHealthScore`. Lưu ý: `Asset.LastInspectedAt` (SCR-07 hiển thị) hiện chỉ được cập nhật bởi code health — gỡ health thì phải giữ lại phần cập nhật này. | Code (BE + FE) |
| 0.3 | **Bộ lớp lỗi và mô hình AI không thống nhất giữa 3 report**: Report 1/2 = 6 lớp (`insulator, insulator_broken, insulator_flashover, bird_nest, foreign_object, broken_strand`) và YOLO11/YOLO26/RT-DETR; SRS = 7 lớp DC-01…DC-07 và YOLO11s/SDI/SCI/StructDETR; DB seed lại là `CORROSION, CRACK, HOTSPOT, VEGETATION`. Vì BR-51 bắt buộc map đúng mã category, kết quả AI mang mã DC-xx hay nhãn 6 lớp sẽ bị từ chối. | Quyết định → chốt 1 taxonomy, sửa SRS Table 61 + seed DB + mô tả model trong SRS (Product Overview, NFR-AI, Abbreviations) |

---

## 1. Report 1 – Project Introduction

| # | Vị trí | Vấn đề | Đề xuất |
|---|---|---|---|
| 1.1 | Record of Changes | Dòng `21/10` thiếu năm. | `21/10/2026`. |
| 1.2 | FE-06, Report 2 Table 2 (Emergency) | “Nhận cảnh báo từ thiết bị *kể cả khi mission chưa được duyệt/giao*” — code Vision Bridge chỉ nhận detection của UAV đang thuộc mission đang thực thi (BR-42 SRS). Ngoài ra Vision Bridge lọc `Status == "Executing"` trong khi Ops lưu `InProgress` → có thể **không bao giờ khớp** (cần chạy thử để xác nhận). | Sửa FE-06 cho đúng phạm vi hiện tại, hoặc bổ sung code; kiểm tra lỗi so sánh status. |
| 1.3 | FE-01 | “approve where applicable” — hệ thống không có bước duyệt mission; mission được tạo từ assessment Ready và dispatch ngay. | Bỏ “approve”. |
| 1.4 | FE-03, LI-5, III | 6 lớp, YOLO vs RT-DETR — lệch SRS (xem 0.3). | Đồng bộ sau khi chốt. |
| 1.5 | Feature Tree (Figure 1) | Ảnh đã được vẽ lại (`Feature_Tree.drawio.svg`) — kiểm tra đã gỡ nhánh FE-09 Asset Health. | — |

## 2. Report 2 – Project Management Plan

| # | Vị trí | Vấn đề | Đề xuất |
|---|---|---|---|
| 2.1 | Table 2 Objectives | Cột bị lệch: cột *Metric/Acceptance Target* chứa ngày, cột *Due Date* chứa lại tên Dimension. | Đổi lại thứ tự cột. |
| 2.2 | Record of Changes 18/10 | “Adjust scope and estimation” quá chung. | Ghi rõ thay đổi (ví dụ bỏ Asset Health, gộp WBS còn 99 person-days). |
| 2.3 | Table 1 WBS vs SRS Table 2 cột WBS | SRS vẫn dùng mã WBS cũ: MF02-07 (preview map), MF02-08, MF03-05, MF03-06, MF04-08…MF04-12 — Report 2 hiện chỉ có MF04-01…MF04-10, và nội dung các mã đã đổi (MF02-06 = preview GIS, MF02-07 = accept/postpone, MF03-05 = edge model, MF03-06 = incident report, MF04-06 = review, MF04-08/09 = maintenance, MF04-10 = reports). | Cập nhật lại cột WBS của SRS Table 2 theo Report 2. |
| 2.4 | WBS MF02-02 “Configure flight-mission parameters”, MF01-05 “Review site and flight conditions”, MF04-06 “…or correct detections” | Code không có tham số altitude/speed, không đánh giá thời tiết tự động, review chỉ Confirm/Reject. | Đổi tên WBS cho khớp (ví dụ MF02-02 “Set mission title, priority and objective”; MF01-05 “Validate site and target assets”; MF04-06 “confirm or reject”). |
| 2.5 | Iteration 2–3, Table 6 Training, Table 7 | Vẫn ghi SDI/SCI/StructDETR, “waypoint route/waypoint flight” — trong khi phần III và Report 1 dùng YOLO11/YOLO26/RT-DETR, SRS ghi “không sinh waypoint”. | Đồng bộ với 0.3; nếu waypoint chỉ là việc của nhóm UAV (ngoài phần mềm) thì nói rõ. |
| 2.6 | Table 7 #2 | Report 2 due 24/09/2026 nhưng đã sửa đến 24/10. | Ghi “v1 24/09/2026, revised 24/10/2026”. |
| 2.7 | 1.2 Objectives | “four students” nhưng 1.2 Report 1 có 4 thành viên + 2 GVHD — đúng, chỉ cần thống nhất mã SV (Report 2 Table 8 có mã SV, Report 1 không). | Tuỳ chọn. |

## 3. Report 3 – SRS (`_v0.4.md`)

### 3.1 Đã sửa trực tiếp trong file v0.4 (ghi vào Record of Changes là v0.5)

- **3.1.1 Screens Flow**: vẽ lại theo route và role guard thực tế của FE; file draw.io mới `report3_diagrams/Screen_Flow_Diagram_v0.5.drawio` (mở bằng draw.io, export SVG/PNG để chèn Word). Mermaid trong md cũng đã thay.
- **3.1.2 Screen Descriptions**: mô tả theo đúng tab/dialog thực tế (SCR-10 Workspace 5 tab: Tổng quan & bản đồ, Mặt bằng tuyến & khí tượng, Ứng viên nhân sự, Ứng viên UAV, Kiểm định BIST; SCR-11 có 2 tab; SCR-13 có 7 tab; SCR-18 và SCR-26 là một phần của SCR-13; SCR-23 chỉ còn dialog Reject).
- **3.1.3 Screen Authorization**: ma trận theo 5 role, mỗi user 1 role.
- **Bỏ công thức**: BR-17, BR-21, BR-23, BR-53, bước 6 UC-MF01-02, checklist UC-MF01-05; bỏ 2 dòng `Ai:EmergencyWeight`, `Ai:MaxConfidenceWeight` trong Table 57.
- **Sửa theo code**: BR-03 (một role / Pilot chỉ là vai trò trong mission), BR-12 (thêm flight status Online), BR-54 + NS-03 (không có thông báo “defect confirmed”), Table 58 (thêm Draft), Table 60 (assignment Replaced/Cancelled; technical inspection Pending/In Progress/Incomplete; ticket Resolved).

### 3.2 Còn phải quyết định / sửa (chưa sửa)

| # | SRS | Thực tế | Sửa ở đâu |
|---|---|---|---|
| 3.1 | UC-CF-08 / Table 57: System Settings API qua gateway | `SystemSettingsController` có, nhưng **không có route Ocelot** `/api/v1/system-settings`. | Code (thêm route) |
| 3.2 | BR-09: các ngưỡng `Ai:*` là system setting đổi được | Service Inspection Evaluation đọc `EvaluationThresholds` từ appsettings (hard-code default), không đọc bảng SystemSettings; `Ai:DefaultPreferredModel`, `Ai:TimeoutSeconds` không được dùng. | Quyết định: nối Eval service vào SystemSettings, hoặc ghi các key này là cấu hình cố định |
| 3.3 | NFR-SEC-05: OTP bypass `123456` tắt ở production | Migration seed `123456` vô điều kiện, không kiểm tra môi trường; khi có bypass thì OTP sinh ra chính là bypass. | **Code (bảo mật)** |
| 3.4 | Table 61 Defect categories DC-01…07 | Seed: CORROSION, CRACK, HOTSPOT, VEGETATION. | Xem 0.3 |
| 3.5 | BR-56 / UC-MF04-08: ticket theo Open → In Progress → Pending Verification → Closed | `PUT /maintenance/tickets/{id}/close` đóng thẳng từ Open/In Progress, không chặn ticket đã Closed; đóng qua endpoint status thì không set detection Resolved. | Code |
| 3.6 | BR-38, UC-MF03-03, UC-MF04-01: thành viên mission (Inspector, Analyst, Technician) start/complete/upload | API chỉ cho `Admin, Manager, Inspector`; FE cũng chỉ hiện tab Upload/AI processing cho Inspector, Admin. | Doc: ghi rõ chỉ Inspector (ma trận mới đã ghi như vậy); sửa câu chữ BR-38, BR-40 |
| 3.7 | UC-MF03-01/02/09, UC-MF03-07 (incident): check-in, bàn giao/trả UAV, flight log, incident report qua SCR-15 | BE có API; **FE chưa có UI nào gọi** (`checkIn`, `confirmDroneHandover`, `returnDroneHandover`, `uploadFlightLog`, `submitIncidentReport` chỉ nằm trong `missions-api.ts`). Start/Complete nằm ở SCR-13. | Quyết định: làm UI (FE) hoặc đánh dấu Deferred trong SRS |
| 3.8 | UC-MF02-09: đổi UAV | FE không có dialog đổi UAV (chỉ Postpone, Suspend/Resume, Cancel, Reassign, Priority). | FE hoặc Doc |
| 3.9 | UC-CF-04 Manage Infrastructure Assets (CRUD, import Excel) | FE `/assets` chỉ xem; không có màn tạo/sửa/import. | FE hoặc ghi rõ thực hiện qua API/import script |
| 3.10 | UC-MF04-05: Manager review detection | API cho Admin/Manager/Analyst; FE `canReview` chỉ Analyst, Admin. Ma trận mới ghi Manager = V. | Quyết định |
| 3.11 | BR-53 / UC-MF04-04: severity hiển thị và lưu | Severity/score chỉ ghi dạng text vào `AnalystNotes`, không có cột; `RequiresImmediateAlert` tính ra nhưng không dùng. | Code hoặc Doc |
| 3.12 | BR-55 | Override là chuỗi `"Critical Risk"`/`"Urgent"`; thêm điều kiện `SeverityWeight >= 4` → Emergency. | Doc |
| 3.13 | Table 59: Suspended → Ready/Assigned khi resume | `Resume` không bao giờ về Ready; `RecalculateReadiness` có thể đặt `Draft`; `POST /missions/{id}/confirm` đưa Postponed → Assigned (chưa có trong bảng). | Code hoặc Doc |
| 3.14 | BR-40: lấy capture time từ EXIF; asset phải là target | Controller gán `capturedAt = now` nên EXIF time không bao giờ dùng; `assetId` rỗng tự gán target đầu tiên. | Code |
| 3.15 | ERD Table 6 | Thiếu các bảng nghiệp vụ: SystemSetting, UserGeographicScope, MissionTarget/MissionTargetLine, PreMissionAssessmentPersonnel/Drone/Asset, ReportMission… | Doc (tuỳ mức chi tiết của conceptual ERD) |
| 3.16 | NFR-SEC-03 | `POST /notifications/realtime/assessment-event` không có `[Authorize]`; `POST /notifications`, `/enqueue-email`, `/schedule` mở cho mọi user đăng nhập. | **Code (bảo mật)** |
| 3.17 | Nhỏ | NS-02 tên job `mission-confirmation-overdue-job`; JWT exp lấy từ `Jwt:ExpiryMinutes` chứ không phải `Auth:AccessTokenExpiryMinutes`; trusted device hard-code 30 ngày; SignalR method là `AssessmentRealtimeEvent` (Type = `AssessmentStatusChanged`); AI GET/reanalysis endpoints không cho SystemAdmin. | Doc/Code nhỏ |

## 4. FE – lệch với phân quyền/Screen Flow mới

| # | Vấn đề | File |
|---|---|---|
| 4.1 | `/drones` và `/reports` không có `roleGuard`; trang drones không kiểm tra role (ai cũng thêm/sửa/xoá). | `app.routes.ts`, `drone-management.ts` |
| 4.2 | Sidebar Technician có *Bản đồ GIS* nhưng guard `/gis` loại Technician → 403. | `sidebar.ts`, `app.routes.ts` |
| 4.3 | Link *Cấu hình hệ thống* `/system` không có route (rơi về dashboard); *Hỗ trợ/Trợ giúp* trỏ tạm `/inspections`. Footer *Nhật ký hệ thống* hiện cho mọi role. | `sidebar.ts:122`, `shell.html:21` |
| 4.4 | `/emergency-alerts` không có trong sidebar (chỉ vào được từ banner của Mission Detail); `/ai-analysis/upload` chỉ có trong menu Analyst; Audit Log chỉ hiện ở menu mobile. | `sidebar.ts`, `sidebar.html:14` |
| 4.5 | `assessment-workspace.ts:70` kiểm tra `role === 'SystemAdmin'` nhưng `auth.ts` đã chuẩn hoá thành `Admin` → Admin không được coi là manager. Nút *Tạo đánh giá* hiện với mọi role. | `assessment-workspace.ts`, `assessment-list` |
| 4.6 | Text tiếng Việt màn Emergency Alerts bị lỗi encoding (hiện `?`). | `emergency-alerts-review.html` |
| 4.7 | Role *Viewer* có trong FE nhưng SRS không có actor tương ứng. | `auth.models.ts` |
