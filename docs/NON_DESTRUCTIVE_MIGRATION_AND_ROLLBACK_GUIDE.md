# 🛡️ Hướng Dẫn Non-Destructive Data Migration & Chiến Lược Rollback (Giai Đoạn 5)

Tài liệu này đặc tả quy trình di chuyển dữ liệu an toàn, không gián đoạn dịch vụ (**Zero-Downtime Non-Destructive Migration**) và kịch bản Rollback khi chuyển đổi toàn diện sang mô hình lấy Cột điện (`towers`) làm trung tâm theo [ERD_SYSTEM_MF01_TO_MF04.md](file:///home/an/RiderProjects/UavPms_Org/docs/ERD_SYSTEM_MF01_TO_MF04.md).

---

## 1. Nguyên Tắc An Toàn Dữ Liệu

1. **Không xóa trực tiếp (Zero-Drop Policy)**:
   - Trong giai đoạn đầu triển khai (tối thiểu 2 tuần trên Staging & Production), các bảng cũ `Assets`, `Substations`, `TransmissionLines` được giữ nguyên ở mức database.
   - Các bảng liên kết được bổ sung trường khóa ngoại `tower_id` song song với `asset_id`.
2. **Đồng bộ song song 2 chiều (Dual-Write / Sync Triggers)**:
   - Dữ liệu mới ghi vào bảng `towers` được trigger `trg_sync_towers_assets` tự động sao chép sang `assets` để duy trì tính tương thích ngược với các báo cáo, client hoặc script cũ chưa kịp nâng cấp.
3. **PostgreSQL Compatibility Views**:
   - Khởi tạo các view:
     - `v_assets_compat`: Map từ `towers` ra các trường của `assets`.
     - `v_substations_compat`: Tương thích trạm biến áp.
     - `v_transmission_lines_compat`: Tương thích tuyến đường dây.
4. **Cổng kiểm duyệt 2 tuần (Staging Gate)**:
   - Chỉ được phép chạy script `DROP TABLE assets;` sau khi hệ thống hoạt động ổn định liên tục tối thiểu 14 ngày mà không phát sinh truy vấn phụ thuộc vào schema cũ.

---

## 2. Kịch Bản Triển Khai (Deployment Procedure)

### Bước 1: Chạy Migration Script
Thực thi script đã kiểm thử [scripts/migrations/001_non_destructive_tower_migration_and_views.sql](file:///home/an/RiderProjects/UavPms_Org/scripts/migrations/001_non_destructive_tower_migration_and_views.sql):

```bash
psql -h $DB_HOST -U $DB_USER -d uavpms_operations -f scripts/migrations/001_non_destructive_tower_migration_and_views.sql
```

Script sẽ:
- Tạo bảng `towers` có chỉ mục không gian `GIST(geom)` nếu chưa có.
- Chuyển toàn bộ dữ liệu cột điện từ `assets` sang `towers`.
- Điền dữ liệu `tower_id` cho `inspection_media`, `detected_anomalies`, `maintenance_tickets`.
- Tạo các compatibility view và sync trigger.

### Bước 2: Kiểm tra tính toàn vẹn (Verification Query)
```sql
-- Kiểm tra số lượng bản ghi
SELECT count(*) FROM towers;
SELECT count(*) FROM v_assets_compat;

-- Kiểm tra backfill foreign keys
SELECT count(*) FROM inspection_media WHERE tower_id IS NULL;
SELECT count(*) FROM detected_anomalies WHERE tower_id IS NULL;
SELECT count(*) FROM maintenance_tickets WHERE tower_id IS NULL;
```

---

## 3. Kịch Bản Khôi Phục (Rollback Strategy)

Trong trường hợp phát hiện sự cố nghiêm trọng trên môi trường Production cần quay lại trạng thái trước khi nâng cấp:

### Bước 1: Revert Code Gateway & Services
- Chuyển Git tag về bản release trước:
```bash
git checkout <pre-phase5-release-tag>
```

### Bước 2: Xóa Triggers & Compatibility Views (SQL Rollback)
Thực thi đoạn rollback an toàn (không làm mất dữ liệu đã ghi nhận):
```sql
BEGIN;
DROP TRIGGER IF EXISTS trg_sync_towers_assets ON towers;
DROP FUNCTION IF EXISTS trg_sync_tower_to_asset();
DROP VIEW IF EXISTS v_assets_compat;
DROP VIEW IF EXISTS v_substations_compat;
DROP VIEW IF EXISTS v_transmission_lines_compat;
COMMIT;
```

Dữ liệu cũ trong `assets` vẫn được bảo toàn nguyên vẹn 100%.
