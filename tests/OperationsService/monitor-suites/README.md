# OperationsService: Monitor & Audit Log Operations Test Suite

## Overview
This test suite covers end-to-end integration and API test cases for the **Monitor & Audit Log** module of the Operations Service (`/api/v1/monitor/*` and `/api/v1/audit-logs`).

### Test Scope: 27 Test Cases
1. **Function A: Successful Operations (Happy Path) (14 TCs)**
   - `OPS_MONITOR_001`: Verify `GET /monitor/summary` returns aggregated system metrics (total missions, active missions, defects, anomalies).
   - `OPS_MONITOR_002`: Verify `GET /monitor/recent-defects` returns paginated list of recent defects sorted by timestamp descending.
   - `OPS_MONITOR_003`: Verify `GET /monitor/defects-statistics` returns breakdown of defects by category and type.
   - `OPS_MONITOR_004`: Verify `GET /monitor/mission-status` returns mission status counts (pending, in-progress, completed).
   - `OPS_MONITOR_005`: Verify `GET /monitor/inspections` returns paginated list of inspection logs.
   - `OPS_MONITOR_005B`: Verify `GET /monitor/inspections?isDefect=true/false` filters inspections accurately.
   - `OPS_MONITOR_005C`: Verify `GET /monitor/inspections?missionId=<uuid>` filters inspections by mission.
   - `OPS_MONITOR_006`: Verify `GET /monitor/alerts` returns active emergency alerts summary with severity levels.
   - `OPS_MONITOR_007`: Verify `GET /audit-logs` returns paginated audit records captured by EF Core `SaveChangesAsync()`.
   - `OPS_MONITOR_007B`: Verify EF Core `SaveChangesAsync()` captures `Modified` events with `OldValues` and `NewValues`.
   - `OPS_MONITOR_008`: Verify EF Core formats PostGIS `Geometry` properties as Well-Known Text (WKT) (e.g. `POINT (lng lat)`).
   - `OPS_MONITOR_009`: Verify `GET /audit-logs?tableName=<name>` filters audit records by affected table name.
   - `OPS_MONITOR_009B`: Verify `GET /audit-logs?search=<keyword>` searches across table names, actions, IP addresses, user agents, and user emails.
   - `OPS_MONITOR_010`: Verify `GET /audit-logs?actionType=<Added|Modified|Deleted>` filters records by operation type.

2. **Function B: Failed Operations — DoS Protection & Boundaries (6 TCs)**
   - `OPS_MONITOR_011`: Reject `pageSize > 100` on `/monitor/recent-defects` with HTTP 400.
   - `OPS_MONITOR_012`: Reject `pageSize > 100` on `/monitor/inspections` with HTTP 400.
   - `OPS_MONITOR_012B`: Reject `fromDate > toDate` on `/monitor/inspections` with HTTP 400.
   - `OPS_MONITOR_013`: Reject `pageSize > 100` on `/audit-logs` with HTTP 400.
   - `OPS_MONITOR_014`: Reject non-positive page index (`page <= 0`) with HTTP 400.
   - `OPS_MONITOR_015`: Reject non-positive page size (`pageSize <= 0`) with HTTP 400.

3. **Function C: Authorization (RBAC) & Security (7 TCs)**
   - `OPS_MONITOR_RBAC_001`: Strict RBAC: `/monitor/summary` restricted to Manager/SystemAdmin (HTTP 403 for Inspector/Technician).
   - `OPS_MONITOR_RBAC_001B`: Analyst permitted on defect analytics (`recent-defects`, `defects-statistics`, `inspections`, `alerts`) with HTTP 200.
   - `OPS_MONITOR_RBAC_002`: Strict RBAC: `/audit-logs` restricted to SystemAdmin/Manager (HTTP 403 for Analyst/Inspector/Technician).
   - `OPS_MONITOR_RBAC_003`: Reject unauthenticated Anonymous requests with HTTP 401 Unauthorized.
   - `OPS_MONITOR_RBAC_004`: SQL Injection and XSS resistance in audit-logs search and filter query parameters.
   - `OPS_MONITOR_RBAC_005`: Verify IP address and User-Agent are accurately captured in audit logs.
   - `OPS_MONITOR_RBAC_006`: Verify sensitive `PasswordHash` is strictly masked as `[MASKED]` in audit logs.

## Execution
```bash
python3 tests/OperationsService/monitor-suites/test_monitor_suite.py
```
Or run all OperationsService test suites:
```bash
python3 tests/OperationsService/run_all_operations_suites.py
```
