# Kế hoạch triển khai backend sau ERD v2.2

Trạng thái ngày 2026-10-28: ERD v2.2 đã được áp dụng vào code (commit `a0bd1e8`, `b2bfc9d`, `74b3cb8` trên `main`). Migration `20261027181344_AlignSchemaWithErdV2_2` đã chạy trên CSDL production bằng workflow *Migrate Database (ERD v2.2)*. Bản backup trước migration nằm ở `$DEPLOY_PATH/backups/uavpms_before_erd_v2_2_20261027195127.dump`.

Tài liệu nguồn:
- Thiết kế: `docs/reports/report3/report3_diagrams/ERD_Full_v2.mmd`, `ERD_Full_v2_CHANGES.md`.
- Hợp đồng code ↔ CSDL: `docs/database/ERD_v2_2_TARGET_SCHEMA.md`.
- Từ điển dữ liệu: `docs/database_schema.md`. API: `docs/api_spec.md`.

Thứ tự ưu tiên: **P0** làm ngay (lỗi chạy thật hoặc rủi ro dữ liệu), **P1** sprint kế tiếp, **P2** cải tiến.

---

## Giai đoạn 0 – Kiểm tra sau migration (P0, 0.5 ngày)

| # | Việc | Cách làm | Xong khi |
| :-- | :-- | :-- | :-- |
| 0.1 | Smoke test production | Gọi qua Swagger/gateway: `GET /api/v1/towers`, `/towers/{id}/history`, `/regions`, `/missions`, `/missions/{id}`, `/reports`, `/reports/{id}/download?format=pdf`, `/gis/infrastructure`, `/notifications`, `/uavs`. Đăng nhập, quên mật khẩu (IdentityService) | Không có 500; xem `docker compose logs` của 4 service không có lỗi `column ... does not exist` |
| 0.2 | Kiểm tra dữ liệu chuyển đổi | `SELECT` số cột ở khu vực `UNASSIGNED`, bất thường `Confirmed` chưa có cột, `UserGeographicScopes` theo user | Không có cột `UNASSIGNED` (dry-run cho kết quả 0); danh sách phạm vi user đúng như trước |
| 0.3 | Giữ backup | Copy file `.dump` ra ngoài server (MinIO hoặc máy local) | Có ít nhất 2 bản lưu ở nơi khác nhau |

## Giai đoạn 1 – Sửa lỗi còn tồn tại (P0, 1–2 ngày)

| # | Vấn đề | Vị trí | Hướng sửa |
| :-- | :-- | :-- | :-- |
| 1.1 | Phiếu bảo trì tự sinh khi duyệt bất thường gán `TechnicianId = Guid.Empty`, vi phạm FK `MaintenanceTickets → Users` nên lưu sẽ lỗi | `ReviewDetectedAnomalyCommandHandler.cs:114`, `MissionLifecycleService.cs:1717` | Đổi `maintenance_tickets.technician_id` thành **nullable** (đã đề xuất trong ERD), thêm migration, `MaintenanceTicket.TechnicianId` → `Guid?`; phiếu chưa giao kỹ thuật viên thì để NULL, trạng thái `Open` |
| 1.2 | `POST /api/v1/inspections/upload` vẫn bắt buộc `towerId` (`Guid` không nullable), trái với ERD (`tower_id` NULL = video hành lang) | `InspectionController.cs:34` | Đổi `[FromForm] Guid? towerId`; command đã hỗ trợ nullable |
| 1.3 | Mã lỗi duyệt bất thường không thống nhất: OperationsService trả `TOWER_REQUIRED`, AIInspectionService trả 400 kèm thông báo chữ (handler gắn nhãn `INVALID_AI_RESPONSE`) | `AIInspectionService.API/Middlewares/GlobalExceptionHandler.cs`, `AnomaliesController` | Dùng chung `BusinessRuleException("TOWER_REQUIRED", ...)` và map ra `errorCode` giống OperationsService |
| 1.4 | Email trùng nên `IX_Users_Email` không được tạo (2 nhóm: `uselessliem@gmail.com` và email rỗng) | Dữ liệu `Users` | Làm sạch: gộp hoặc vô hiệu hóa tài khoản trùng, sửa email rỗng; sau đó thêm migration tạo `IX_Users_Email` (UNIQUE) và `IX_Users_Phone` (UNIQUE, partial `Phone <> ''`) |
| 1.5 | Workflow cũ trỏ vào bảng/cột đã xóa, chạy nhầm sẽ lỗi hoặc tạo lại schema cũ | `.github/workflows/`: `repair-production-assets-schema`, `repair-production-detected-anomalies-schema`, `reset-and-seed-production-gis`, `import-production-pole-manifest`, `diagnose-production-assets-api`, `assign-production-gis-scope`, `apply-mf01-mf02-v2-migration`, `apply-mf01-mission-lifecycle`, `update-production-database` (preflight `AssetComponents`) | Xóa các workflow repair/diagnose/apply-* một lần; viết lại `import-production-pole-manifest` và `assign-production-gis-scope` theo `Towers.RegionId`/`UserGeographicScopes.RegionId`; bỏ preflight `AssetComponents` khỏi `update-production-database` |

## Giai đoạn 2 – Bổ sung chức năng theo ERD (P1, 1–2 sprint)

| # | Chức năng | Chi tiết | Bảng/cột ERD |
| :-- | :-- | :-- | :-- |
| 2.1 | Quản lý phạm vi địa lý người dùng | Hiện không có API, chỉ gán bằng SQL/workflow. Thêm `GET/PUT /api/v1/users/{id}/geographic-scopes` (IdentityService hoặc OperationsService), input là danh sách `regionId`; kiểm tra region tồn tại; chỉ SystemAdmin/Manager được sửa | `user_geographic_scopes (user_id, region_id)` UK |
| 2.2 | Metadata và chống trùng media | Khi upload: tính SHA-256 → `Checksum`, lưu `FileSizeBytes`, `WidthPx/HeightPx` (ảnh), `DurationSeconds/Fps` (video). Trùng checksum → trả media cũ (idempotent cho đồng bộ edge) | `inspection_media.checksum UK, file_size_bytes, width_px, height_px, duration_seconds, fps` |
| 2.3 | Lần chạy AI đầy đủ | Worker trả `modelVersion` → lưu `ModelVersion`; `StartedAt` khi worker nhận job; nhánh edge lưu `UavId` từ telemetry. Quy tắc thay thế edge → cloud hiện so cùng media + cùng loại lỗi: nâng cấp bằng so khớp bounding box (IoU ≥ 0.5) | `ai_analysis_runs.model_version, started_at, uav_id`; `detected_anomalies.superseded_by_id` |
| 2.4 | Trang chi tiết cột điện | API `GET /towers/{id}/history` đã có; thêm phân trang, lọc theo `validation_status` và khoảng thời gian; thêm thống kê đếm trực tiếp (số bất thường mở, phiếu đang xử lý), không lưu cột tổng hợp | `towers`, `detected_anomalies`, `maintenance_tickets` |
| 2.5 | Gán cột điện cho bất thường | API `PUT /anomalies/{id}/tower` (Analyst) để gán `TowerId` trước khi xác nhận; gợi ý cột gần nhất theo `Location` (PostGIS `ST_DWithin`) | `detected_anomalies.tower_id`, CHECK confirmed requires tower |
| 2.6 | Báo cáo đã duyệt không đổi theo thời gian | Báo cáo sinh trực tiếp nên số liệu bản đã duyệt có thể thay đổi. Nếu cần kiểm toán: thêm `reports.snapshot jsonb`, ghi lúc `approved_at`, `/download` của báo cáo Approved đọc từ snapshot | `reports` (+ `snapshot` nếu chốt) |
| 2.7 | Ràng buộc còn thiếu so với ERD | `CHECK (escalated_by <> escalated_to)` trên `alert_escalations`; partial UK một cảnh báo `Active` cho mỗi anomaly; `CHECK` lịch kiểm tra hợp lệ; độ dài `varchar` theo ERD cho các cột hiện là `text` (`InspectionMedia.MediaType/FileUrl`, `Missions.Status`, ...), kiểm tra dữ liệu trước khi ALTER | `alert_escalations`, `emergency_alerts`, `inspection_schedules`, ... |
| 2.8 | Lưu khóa lưu trữ thay vì URL | Các cột `file_url`, `image_url`, `crop_url` còn lưu URL đầy đủ (MinIO). Đổi sang storage key, sinh URL ký (presigned) khi đọc | `inspection_media.file_url`, `maintenance_proofs.file_url`, `detected_anomalies.image_url/crop_url` |

## Giai đoạn 3 – Dọn dẹp và chất lượng (P1/P2)

| # | Việc | Ghi chú |
| :-- | :-- | :-- |
| 3.1 | Xóa API tạo nhiệm vụ cũ | `CreateMissionCommand/Handler` (đã `[Obsolete]`) và endpoint tương ứng trong `MissionController`; mọi nhiệm vụ tạo qua PreMissionAssessment |
| 3.2 | Đưa FK ghép `(AnalysisRunId, MediaId)` vào model EF | Hiện chỉ có trong SQL migration (`AK_AIAnalysisRuns_Id_MediaId`); khai báo alternate key + FK trong `UavPmsConfigurations` để snapshot khớp DB |
| 3.3 | Cập nhật bộ test ngoài .NET | `api-test/` (pytest, Playwright), `tests/OperationsService/*-suites` (bỏ `asset-suites`, `line-suites`, `substation-suites`), `scripts/seed_local_dev.sql`, `scripts/production_dump.sql` (dump lại sau migration) |
| 3.4 | Cập nhật phần còn cũ của `docs/api_spec.md` | 8.1–8.3 (route thật là `/api/v1/alerts`, trạng thái cảnh báo), 4.1 (response assessment), 5.x (request nhiệm vụ), 3.x (trạng thái UAV) |
| 3.5 | Hiệu năng phân quyền địa lý | `GeographicAccessFilter` dùng subquery/UNION trên mỗi truy vấn; đo bằng `EXPLAIN ANALYZE` với dữ liệu thật, thêm index nếu cần (`MissionAssignments (UserId, Status)`, `MaintenanceTickets (TechnicianId, Status)`) |
| 3.6 | Test tích hợp với PostgreSQL thật | Thêm project test dùng Testcontainers (postgis/postgis:16-3.4) chạy migration + các truy vấn chính (đã làm thủ công trong lần migrate này) để CI bắt lỗi dịch LINQ và lỗi migration |
| 3.7 | Hợp đồng với AI worker (Python) | `AIAnalysisRequestedEvent.AssetId` giờ chứa tower id (giữ tên trường để tương thích); thống nhất với worker để đổi sang `TowerId` và bỏ `AssetId` |
| 3.8 | Bảng `session` lạ trong CSDL | Xác định ứng dụng nào tạo (dạng `connect-pg-simple`); nếu không còn dùng thì xóa, nếu còn dùng thì chuyển sang schema riêng |

## Việc phía Frontend (repo khác, cần phối hợp)

- `assetId` → `towerId` ở mọi request/response; bỏ màn hình quản lý linh kiện, trạm, đường dây, đơn vị quản lý; bỏ điểm sức khỏe và mức rủi ro.
- Cột điện: form có `regionId`, `poleType`, `address`; trang chi tiết dùng `GET /towers/{id}/history`; chọn trên bản đồ dùng `POST /towers/spatial-query`; GIS dùng `GET /gis/infrastructure` (towers/anomalies/alerts).
- Báo cáo: lọc và tạo theo `regionId`; bỏ trạng thái "đã sinh tệp", nút tải gọi thẳng `/download`.
- Sự cố: bỏ upload tệp. Phiếu bảo trì: bỏ chọn cột (lấy theo bất thường).
- Bất thường: hiển thị `aiSource`, `executionLocation`, ẩn hoặc gộp bản có `supersededById`; xử lý lỗi `TOWER_REQUIRED` khi xác nhận.

## Quy trình cho thay đổi schema tiếp theo

1. Sửa entity + `UavPmsConfigurations` → `dotnet ef migrations add <Tên>` → nếu đổi tên hoặc chuyển dữ liệu thì viết SQL tay theo mẫu `AlignSchemaWithErdV2_2` (helper `pg_temp.has_col/exec_if/rename_col`, mọi lệnh idempotent).
2. Cập nhật mapping ở các service khác dùng chung bảng (AIInspection, Notification, Identity) và `docs/database/ERD_v2_2_TARGET_SCHEMA.md`.
3. Push → chạy workflow migrate (có thể nhân bản workflow ERD v2.2 và đổi `TARGET_MIGRATION`) ở `dry-run`, rồi `apply`.
