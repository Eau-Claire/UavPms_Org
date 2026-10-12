# OperationsService — Flight Log Operations Test Suite

Tập kiểm thử tích hợp tự động cho tính năng tiếp nhận dữ liệu nhật ký bay (Flight Log Telemetry) và bảo mật truy cập RBAC thuộc `OperationsService`.

---

## 1. Danh Mục Kiểm Thử Chuẩn Hóa (29 Test Cases)

### Function A: Successful Flight Log Operations (Happy Path)
- **`OPS_FLIGHTLOG_001`**: Verify successful upload of drone flight log telemetry with valid GPS track. (Kiểm tra HTTP 200, phản hồi DTO, bản ghi DB `MissionFlightLogs` và AuditLog `FLIGHT_LOG_UPLOADED`).
- **`OPS_FLIGHTLOG_002`**: Verify retrieval of flight log telemetry list via `GET /api/v1/missions/{id}/flight-logs` (mảng log sắp xếp theo `recordedAt DESC`).
- **`OPS_FLIGHTLOG_003`**: Verify flight log ingestion records low battery telemetry values accurately (`minBatteryRecorded: 12.5`, `connectionStatus: "Warning: Low Battery"`).
- **`OPS_FLIGHTLOG_004`**: Verify flight log ingestion records high altitude telemetry values accurately (`maxAltitudeM: 250.0`).
- **`OPS_FLIGHTLOG_005`**: Verify flight log handles large GPS tracks (10,000+ points JSON array, ~600KB payload) nguyên vẹn không bị cắt ngắn.
- **`OPS_FLIGHTLOG_006`**: Verify `connectionStatus` defaults to `'Normal'` when omitted or empty.
- **`OPS_FLIGHTLOG_007`**: Verify boundary values accepted (`minBattery = 0.0 & 100.0`, `maxAltitude = 0.0`, `flightDuration = 0`).
- **`OPS_FLIGHTLOG_008`**: Verify flight log upload succeeds for secondary Inspector assigned via `MissionAssignment` (trạng thái `Active`, gán `CreatedBy = Inspector B`).
- **`OPS_FLIGHTLOG_009`**: Verify flight log upload is permitted after flight completion (Status: `Completed`).

### Function B: Failed Flight Log Operations — Validation & Boundaries
- **`OPS_FLIGHTLOG_010`**: Verify upload fails when `gpsTrack` is malformed JSON syntax (HTTP 400, `INVALID_GPS_TRACK_JSON`).
- **`OPS_FLIGHTLOG_011`**: Verify upload fails when `gpsTrack` is empty string or whitespace (HTTP 400, `GPS_TRACK_REQUIRED`).
- **`OPS_FLIGHTLOG_012`**: Verify upload fails when `minBatteryRecorded` is negative (< 0) (HTTP 400, `INVALID_BATTERY_LEVEL`).
- **`OPS_FLIGHTLOG_013`**: Verify upload fails when `minBatteryRecorded` exceeds 100 (> 100) (HTTP 400, `INVALID_BATTERY_LEVEL`).
- **`OPS_FLIGHTLOG_014`**: Verify upload fails when `flightDurationSeconds` is negative (< 0) (HTTP 400, `INVALID_FLIGHT_DURATION`).
- **`OPS_FLIGHTLOG_015`**: Verify upload fails when `maxAltitudeM` is negative (< 0) (HTTP 400, `INVALID_ALTITUDE`).
- **`OPS_FLIGHTLOG_016`**: Verify upload fails when mission ID does not exist or outside accessible scope (HTTP 403, `FORBIDDEN` - `MISSION_ACCESS_DENIED`).
- **`OPS_FLIGHTLOG_017`**: State Machine Guard: Verify upload fails when mission is in `Draft` status (HTTP 400, `FLIGHT_LOG_INVALID_STATE`).
- **`OPS_FLIGHTLOG_018`**: State Machine Guard: Verify upload fails when mission is in `PendingAcceptance` status (HTTP 400, `FLIGHT_LOG_INVALID_STATE`).
- **`OPS_FLIGHTLOG_019`**: State Machine Guard: Verify upload behavior when mission is `Cancelled` (*Ghi nhận kiến trúc: Backend chấp nhận upload để bảo toàn log sự cố / abort khẩn cấp*).
- **`OPS_FLIGHTLOG_020`**: Verify `gpsTrack` accepts valid JSON object/array telemetry payloads without error (HTTP 200 OK).

### Function C: Authorization (RBAC) & Security
- **`OPS_FLIGHTLOG_RBAC_001`**: Verify flight log upload is allowed for assigned Inspector and Manager (HTTP 200 OK).
- **`OPS_FLIGHTLOG_RBAC_002`**: Verify flight log upload is allowed for SystemAdmin (`IsGlobal` bypass) (HTTP 200 OK).
- **`OPS_FLIGHTLOG_RBAC_003`**: Verify flight log upload is rejected for unassigned Inspector (HTTP 403, `FORBIDDEN` - `USER_NOT_ASSIGNED_TO_MISSION`).
- **`OPS_FLIGHTLOG_RBAC_004`**: Verify flight log upload is strictly rejected for Analyst role (HTTP 403 Forbidden).
- **`OPS_FLIGHTLOG_RBAC_005`**: Verify flight log upload is strictly rejected for Technician role (HTTP 403 Forbidden).
- **`OPS_FLIGHTLOG_RBAC_006`**: Verify flight log endpoints reject unauthenticated (Anonymous) requests (HTTP 401 Unauthorized cho cả POST và GET).
- **`OPS_FLIGHTLOG_RBAC_007`**: Verify SQL Injection and XSS resistance in `gpsTrack` payload (HTTP 200 OK, EF Core tham số hoá an toàn).
- **`OPS_FLIGHTLOG_RBAC_008`**: Verify `GET /missions/{id}/flight-logs` enforces region scope isolation for viewers (HTTP 403, `MISSION_ACCESS_DENIED`).
- **`OPS_FLIGHTLOG_RBAC_009`**: Verify singular route `GET /api/v1/missions/{id}/flight-log` returns 405 Method Not Allowed.

---

## 2. Cách Thực Thi

```bash
# Chạy bộ kiểm thử Flight Log
python3 tests/OperationsService/flightlog-suites/test_flightlog_suite.py
```
