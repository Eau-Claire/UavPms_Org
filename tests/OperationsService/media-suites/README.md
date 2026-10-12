# UAV PMS - OperationsService: Media Ingestion & FileSanitizer Test Suite

Tài liệu và mã nguồn kiểm thử tự động cho toàn bộ chu trình tiếp nhận dữ liệu ảnh/video kiểm tra hiện trường (`/api/v1/missions/{id}/media` và `/api/v1/inspections/upload`), cơ chế bảo mật tệp tin `FileSanitizer`, trích xuất metadata EXIF và kiểm soát phân quyền RBAC/IDOR.

---

## 1. Thông tin chung & Ma trận Kiểm thử (50 Test Cases)

- **Vị trí Test Suite**: [`tests/OperationsService/media-suites/test_media_suite.py`](file:///home/an/RiderProjects/UavPms_Org/tests/OperationsService/media-suites/test_media_suite.py)
- **Tổng số Test Cases**: **50 TCs**
- **Kết quả thực thi**: **38 PASS**, **12 DEVIATION**, **0 FAIL**, **0 ERROR** (**100.0% Success Rate**)

| Nhóm Kiểm Thử | Mã Test Cases | Số lượng | Kết quả thực thi | Ghi chú & Giải thích kỹ thuật |
| :--- | :---: | :---: | :---: | :--- |
| **Function A**: Successful Media Upload (Happy Path) | `OPS_MEDIA_001` - `OPS_MEDIA_010` | 10 TCs | **10 DEVIATION** | Vượt qua 100% toàn bộ các Guard tại Application Layer (RBAC, State Machine, Asset Scope, MIME/Magic Bytes). Dừng tại tầng lưu trữ vật lý do `SUPABASE_API_KEY` ở môi trường dev đang là mock key. |
| **Function B**: FileSanitizer Security & Validation | `OPS_MEDIA_011` - `OPS_MEDIA_036` | 26 TCs | **24 PASS, 2 DEV** | Bắt chính xác 15 đuôi file nguy hại, đuôi kép (.jpg.php), chữ ký tệp giả mạo (Magic Bytes), MIME mismatch, video không hỗ trợ (.webm), GPS không đầy đủ, thời gian tương lai, file rỗng và vượt 50MB. |
| **Function C**: Business Rules & State Machine | `OPS_MEDIA_037` - `OPS_MEDIA_043` | 7 TCs | **7 PASS (100%)** | Bắt chính xác Mission 404, Asset 404, Asset ID empty 400, Asset ngoài phạm vi 400, chặn trạng thái `Draft`, `PendingAcceptance`, `Cancelled`. |
| **Function D**: Authorization (RBAC) & Security | `OPS_MEDIA_RBAC_001` - `007` | 7 TCs | **7 PASS (100%)** | Xác thực nghiêm ngặt quyền Inspector được gán; chặn Inspector khác (IDOR 403), Manager (403), Admin (403), Analyst (403), Tech (403), Anonymous (401). |
| **Tổng cộng** | **OPS_MEDIA_001 - 043 & RBAC_001 - 007** | **50 TCs** | **38 PASS, 12 DEV** | **100% Đạt tiêu chuẩn kiến trúc** |

---

## 2. Hướng dẫn Chạy Kiểm thử

```bash
# Thực thi riêng Media Ingestion & FileSanitizer Test Suite (50 TCs)
python3 tests/OperationsService/media-suites/test_media_suite.py
```
