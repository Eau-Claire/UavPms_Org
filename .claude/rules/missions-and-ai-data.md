---
paths:
  - "Services/OperationsService/**"
  - "Services/AIInspectionService/**"
---

# Quy tắc Missions, BIST, EXIF/Video & Maintenance (BE)

1. Toàn bộ API danh sách mặc định `OrderByDescending` mới nhất lên đầu (`CreatedAt`/`UpdatedAt`/`DetectedAt`), dùng 1 `ApiResponse` `camelCase` và UTC `Z`.
2. BIST / Sức khỏe Drone / FC gắn ở Drone Detail (chỉ `Technician` & `SystemAdmin`); Pre-Mission MF01 chỉ lưu số lượng nhân sự (`PreMissionAssessmentPersonnel`), hạn ≤ 7 ngày. Mission MF02 khóa đủ 3 role (`Inspector`, `Analyst`, `Technician`) và bắt buộc cả 3 `Accept` mới sang `Confirmed`.
3. Check-in GPS Haversine tại Cột 1 (`sequence = 1`) so sánh `<= CheckInRadiusMeters` (mặc định `50m`), chỉ cho `Inspector` của Mission (và `SystemAdmin`), phát SignalR ngay khi thành công.
4. `InspectionMedia` hỗ trợ `MissionId` và `TowerId` nullable (ảnh đọc EXIF GPS / video trích frame lỗi GPS live → tự gắn cột gần nhất, cho đổi cột). `Approve` defect trong Mission tự tạo `MaintenanceTicket` trạng thái `Assigned` cho `Technician` của Mission.
