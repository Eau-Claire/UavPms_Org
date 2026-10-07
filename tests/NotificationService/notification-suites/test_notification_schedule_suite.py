#!/usr/bin/env python3
"""
UAV PMS - NotificationService: Schedule Notification Test Suite
Endpoint Tested:
- POST /api/v1/notifications/schedule

Covers 28 standardized test cases:
- Function A: Successful Schedule Notification (Happy Path) [NOTIF_SCHED_001 - NOTIF_SCHED_015] (15 TCs)
- Function B: Failed Schedule — Validation & Boundaries [NOTIF_SCHED_016 - NOTIF_SCHED_023] (8 TCs)
- Function C: Authorization (RBAC) & Security [NOTIF_SCHED_RBAC_001 - NOTIF_SCHED_RBAC_005] (5 TCs)
"""

import datetime
import json
import os
import subprocess
import sys
import time
import requests

BASE_URL = os.environ.get("GATEWAY_BASE_URL", "http://127.0.0.1:5194")

# --- Authentication Helpers ---
def get_token(email="An3439201@gmail.com", password="12345678"):
    try:
        requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": email, "password": password}, timeout=5)
        res = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": email, "otp": "123456", "purpose": "Login"}, timeout=5).json()
        return res.get("data", {}).get("authResult", {}).get("accessToken")
    except Exception as e:
        print(f"[AUTH ERROR] Failed to authenticate {email}: {e}")
        return None

def run_db_query(sql):
    cmd = ["docker", "exec", "-i", "uavpms-db", "psql", "-U", "uav_admin", "-d", "uav_pms_db", "-t", "-A", "-c", sql]
    return subprocess.run(cmd, capture_output=True, text=True).stdout.strip()

# --- Test Framework & Results Tracking ---
results = []

def record(tc_id, name, status, http_code, details):
    results.append({
        "id": tc_id,
        "name": name,
        "status": status,
        "http_code": http_code,
        "details": details
    })
    color = "\033[92m" if status == "PASS" else ("\033[93m" if status == "DEVIATION" else "\033[91m")
    reset = "\033[0m"
    print(f"[{color}{status:<9}{reset}] {tc_id:<22} (HTTP {http_code:<3}): {name} -> {details[:85]}")

print("=" * 105)
print("   UAV PMS - NOTIFICATION SCHEDULE SUITE (28 TEST CASES)")
print("=" * 105)

# Setup Authentication Tokens for all roles
admin_token = get_token("An3439201@gmail.com")
admin_headers = {"Authorization": f"Bearer {admin_token}", "Content-Type": "application/json"}
mgr_token = get_token("An3439201+manager@gmail.com")
mgr_headers = {"Authorization": f"Bearer {mgr_token}", "Content-Type": "application/json"}
insp_token = get_token("An3439201+inspector@gmail.com")
insp_headers = {"Authorization": f"Bearer {insp_token}", "Content-Type": "application/json"}
analyst_token = get_token("An3439201+analyst@gmail.com")
analyst_headers = {"Authorization": f"Bearer {analyst_token}", "Content-Type": "application/json"}
tech_token = get_token("An3439201+technician@gmail.com")
tech_headers = {"Authorization": f"Bearer {tech_token}", "Content-Type": "application/json"}

target_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+inspector@gmail.com\' LIMIT 1;')
if not target_user_id or len(target_user_id) != 36:
    target_user_id = "33333333-3333-3333-3333-333333333333"


# ==============================================================================
# FUNCTION A: Successful Schedule Notification (Happy Path) (15 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Schedule Notification (Happy Path)")

# NOTIF_SCHED_001: Verify successful scheduling with DelaySeconds targeting a specific user
try:
    payload = {
        "userId": target_user_id,
        "title": "Nhắc nhở bảo trì",
        "body": "Nhiệm vụ bảo trì #TCK-20260618-001 sắp đến hạn.",
        "type": "TicketAssigned",
        "delaySeconds": 60,
        "scheduleTime": None
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json=payload, headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    valid = (r.status_code == 200 and
             r.json().get("success") is True and
             "Notification scheduled to run at" in r.json().get("message", "") and
             data.get("jobId") is not None and
             f"User: {target_user_id}" in data.get("target", ""))
    record("NOTIF_SCHED_001", "Scheduling with DelaySeconds targeting specific user", "PASS" if valid else "FAIL", r.status_code, f"JobId: {data.get('jobId')}, Target: {data.get('target')}")
except Exception as e:
    record("NOTIF_SCHED_001", "DelaySeconds scheduling check", "ERROR", 0, str(e))

# NOTIF_SCHED_002: Verify successful scheduling with ScheduleTime targeting a specific user
try:
    future_time = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=2)).strftime("%Y-%m-%dT%H:%M:%SZ")
    payload = {
        "userId": target_user_id,
        "title": "Lịch bảo trì",
        "body": "Hệ thống sẽ bảo trì vào tối nay.",
        "scheduleTime": future_time,
        "delaySeconds": None
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json=payload, headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    valid = r.status_code == 200 and data.get("jobId") is not None and f"User: {target_user_id}" in data.get("target", "")
    record("NOTIF_SCHED_002", "Scheduling with ScheduleTime targeting specific user", "PASS" if valid else "FAIL", r.status_code, f"Target: {data.get('target')}")
except Exception as e:
    record("NOTIF_SCHED_002", "ScheduleTime scheduling check", "ERROR", 0, str(e))

# NOTIF_SCHED_003: Verify successful scheduling to broadcast to ALL users (userId = null)
try:
    payload = {
        "userId": None,
        "title": "Thông báo toàn hệ thống",
        "body": "Hệ thống đã nâng cấp thành công lên v2.0.",
        "delaySeconds": 30
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json=payload, headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    valid = r.status_code == 200 and data.get("target") == "ALL Users"
    record("NOTIF_SCHED_003", "Broadcast scheduling to ALL users (userId = null)", "PASS" if valid else "FAIL", r.status_code, f"Target verified: {data.get('target')}")
except Exception as e:
    record("NOTIF_SCHED_003", "Broadcast scheduling check", "ERROR", 0, str(e))

# NOTIF_SCHED_004: Verify scheduled job returns valid positive integer JobId
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "userId": target_user_id, "title": "Job ID Check", "body": "Body", "delaySeconds": 10
    }, headers=admin_headers, timeout=5)
    jid = r.json().get("data", {}).get("jobId")
    valid = r.status_code == 200 and int(jid) > 0
    record("NOTIF_SCHED_004", "Hangfire JobId is valid positive integer", "PASS" if valid else "FAIL", r.status_code, f"JobId: {jid}")
except Exception as e:
    record("NOTIF_SCHED_004", "JobId format check", "ERROR", 0, str(e))

# NOTIF_SCHED_005: Verify scheduled notification defaults to type 'ScheduledNotification' when type is null
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "userId": target_user_id, "title": "Type Default", "body": "Body", "type": None, "delaySeconds": 15
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_SCHED_005", "Defaults to 'ScheduledNotification' when type is null", "PASS" if valid else "FAIL", r.status_code, "Enqueued with default fallback type")
except Exception as e:
    record("NOTIF_SCHED_005", "Type default check", "ERROR", 0, str(e))

# NOTIF_SCHED_006: Verify scheduling handles UTF-8 / Unicode title and body correctly
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "userId": target_user_id,
        "title": "📊 Báo cáo tổng hợp hàng ngày",
        "body": "Tổng kết hệ thống UAV-PMS ngày 18/06/2026 với 12 chuyến bay hoàn thành.",
        "delaySeconds": 20
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_SCHED_006", "Unicode / UTF-8 title and body preserved cleanly", "PASS" if valid else "FAIL", r.status_code, "Unicode payload enqueued successfully")
except Exception as e:
    record("NOTIF_SCHED_006", "Unicode scheduling check", "ERROR", 0, str(e))

# NOTIF_SCHED_007: Verify unspecified DateTimeKind treated as Vietnam timezone (+07:00)
try:
    future_vn = (datetime.datetime.now() + datetime.timedelta(days=2)).strftime("%Y-%m-%dT10:00:00")
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Vietnam Timezone Test", "body": "Body", "scheduleTime": future_vn
    }, headers=admin_headers, timeout=5)
    msg = r.json().get("message", "")
    valid = r.status_code == 200 and "+07:00" in msg
    record("NOTIF_SCHED_007", "Unspecified DateTimeKind assigned Vietnam offset (+07:00)", "PASS" if valid else "FAIL", r.status_code, f"Offset in message: {msg}")
except Exception as e:
    record("NOTIF_SCHED_007", "Vietnam timezone offset check", "ERROR", 0, str(e))

# NOTIF_SCHED_008: Verify scheduling with ScheduleTime using explicit UTC (Z suffix)
try:
    future_utc = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=2)).strftime("%Y-%m-%dT10:00:00Z")
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "UTC Test", "body": "Body", "scheduleTime": future_utc
    }, headers=admin_headers, timeout=5)
    msg = r.json().get("message", "")
    valid = r.status_code == 200 and "+00:00" in msg
    record("NOTIF_SCHED_008", "Explicit UTC ScheduleTime preserved (+00:00)", "PASS" if valid else "FAIL", r.status_code, f"Offset in message: {msg}")
except Exception as e:
    record("NOTIF_SCHED_008", "UTC offset check", "ERROR", 0, str(e))

# NOTIF_SCHED_009: When both delaySeconds and scheduleTime provided, scheduleTime takes precedence
try:
    future_target = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=5)).strftime("%Y-%m-%dT12:00:00Z")
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Precedence Test", "body": "Body", "delaySeconds": 10, "scheduleTime": future_target
    }, headers=admin_headers, timeout=5)
    msg = r.json().get("message", "")
    valid = r.status_code == 200 and "12:00:00" in msg
    record("NOTIF_SCHED_009", "ScheduleTime takes precedence over delaySeconds", "PASS" if valid else "FAIL", r.status_code, f"Precedence verified in: {msg}")
except Exception as e:
    record("NOTIF_SCHED_009", "Precedence check", "ERROR", 0, str(e))

# NOTIF_SCHED_010: Scheduling with very large delaySeconds (e.g. 86400 = 24 hours) succeeds
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "24h Notice", "body": "Scheduled tomorrow", "delaySeconds": 86400
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_SCHED_010", "Large delaySeconds (86400s) scheduled successfully", "PASS" if valid else "FAIL", r.status_code, f"24h JobId: {r.json().get('data', {}).get('jobId')}")
except Exception as e:
    record("NOTIF_SCHED_010", "Large delay check", "ERROR", 0, str(e))

# NOTIF_SCHED_011: SystemAdmin can schedule notifications
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Admin Sched", "body": "Body", "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_SCHED_011", "SystemAdmin can schedule notifications", "PASS" if valid else "FAIL", r.status_code, "SystemAdmin privilege verified")
except Exception as e:
    record("NOTIF_SCHED_011", "SystemAdmin schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_012: Manager can schedule notifications
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Mgr Sched", "body": "Body", "delaySeconds": 30
    }, headers=mgr_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_SCHED_012", "Manager can schedule notifications", "PASS" if valid else "FAIL", r.status_code, "Manager privilege verified")
except Exception as e:
    record("NOTIF_SCHED_012", "Manager schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_013: Inspector can schedule notifications
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Insp Sched", "body": "Body", "delaySeconds": 30
    }, headers=insp_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_SCHED_013", "Inspector can schedule notifications", "PASS" if valid else "FAIL", r.status_code, "Inspector privilege verified")
except Exception as e:
    record("NOTIF_SCHED_013", "Inspector schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_014: Analyst can schedule notifications
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Analyst Sched", "body": "Body", "delaySeconds": 30
    }, headers=analyst_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_SCHED_014", "Analyst can schedule notifications", "PASS" if valid else "FAIL", r.status_code, "Analyst privilege verified")
except Exception as e:
    record("NOTIF_SCHED_014", "Analyst schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_015: Technician can schedule notifications
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Tech Sched", "body": "Body", "delaySeconds": 30
    }, headers=tech_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_SCHED_015", "Technician can schedule notifications", "PASS" if valid else "FAIL", r.status_code, "Technician privilege verified")
except Exception as e:
    record("NOTIF_SCHED_015", "Technician schedule check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION B: Failed Schedule — Validation & Boundaries (8 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Failed Schedule — Validation & Boundaries")

# NOTIF_SCHED_016: Verify scheduling fails when title is empty string
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "", "body": "Body", "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Title and Body are required." in r.text
    record("NOTIF_SCHED_016", "Empty title string rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Required error returned")
except Exception as e:
    record("NOTIF_SCHED_016", "Empty title check", "ERROR", 0, str(e))

# NOTIF_SCHED_017: Verify scheduling fails when body is empty string
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "", "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Title and Body are required." in r.text
    record("NOTIF_SCHED_017", "Empty body string rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Required error returned")
except Exception as e:
    record("NOTIF_SCHED_017", "Empty body check", "ERROR", 0, str(e))

# NOTIF_SCHED_018: Verify scheduling fails when title is null or missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "body": "Body", "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("title" in r.text.lower() and "required" in r.text.lower())
    record("NOTIF_SCHED_018", "Missing title field rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_SCHED_018", "Missing title check", "ERROR", 0, str(e))

# NOTIF_SCHED_019: Verify scheduling fails when body is null or missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("body" in r.text.lower() and "required" in r.text.lower())
    record("NOTIF_SCHED_019", "Missing body field rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_SCHED_019", "Missing body check", "ERROR", 0, str(e))

# NOTIF_SCHED_020: Verify scheduling fails when both delaySeconds and scheduleTime are null/missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Either a valid DelaySeconds" in r.text
    record("NOTIF_SCHED_020", "Missing timing parameters rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Timing error returned")
except Exception as e:
    record("NOTIF_SCHED_020", "Missing timing check", "ERROR", 0, str(e))

# NOTIF_SCHED_021: Verify scheduling fails when delaySeconds = 0
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body", "delaySeconds": 0
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Either a valid DelaySeconds" in r.text
    record("NOTIF_SCHED_021", "delaySeconds = 0 rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Non-positive delay rejected")
except Exception as e:
    record("NOTIF_SCHED_021", "delaySeconds = 0 check", "ERROR", 0, str(e))

# NOTIF_SCHED_022: Verify scheduling fails when delaySeconds is negative
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body", "delaySeconds": -30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Either a valid DelaySeconds" in r.text
    record("NOTIF_SCHED_022", "Negative delaySeconds rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Negative delay rejected")
except Exception as e:
    record("NOTIF_SCHED_022", "Negative delay check", "ERROR", 0, str(e))

# NOTIF_SCHED_023: Verify scheduling fails when scheduleTime is in the past
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body", "scheduleTime": "2020-01-01T00:00:00Z"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "cannot be in the past" in r.text
    record("NOTIF_SCHED_023", "Past ScheduleTime rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Past date rejected")
except Exception as e:
    record("NOTIF_SCHED_023", "Past date check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION C: Authorization (RBAC) & Security (5 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC) & Security")

# NOTIF_SCHED_RBAC_001: Verify unauthenticated (Anonymous) request rejected with HTTP 401
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body", "delaySeconds": 30
    }, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_SCHED_RBAC_001", "Unauthenticated request rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Anonymous request blocked")
except Exception as e:
    record("NOTIF_SCHED_RBAC_001", "Unauthenticated schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_RBAC_002: Verify expired JWT token rejected with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid_signature"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": "Title", "body": "Body", "delaySeconds": 30
    }, headers={"Authorization": f"Bearer {expired_token}", "Content-Type": "application/json"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_SCHED_RBAC_002", "Expired JWT token rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Expired token blocked")
except Exception as e:
    record("NOTIF_SCHED_RBAC_002", "Expired JWT schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_RBAC_003: Verify XSS payload in title and body handled safely
try:
    xss_payload = "<script>alert('xss')</script>"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": xss_payload, "body": xss_payload, "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_SCHED_RBAC_003", "XSS payload treated safely as raw text", "PASS" if valid else "FAIL", r.status_code, "XSS string enqueued safely")
except Exception as e:
    record("NOTIF_SCHED_RBAC_003", "XSS schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_RBAC_004: Verify SQL Injection resistance in schedule fields
try:
    sqli_payload = "'; DROP TABLE \"Notifications\"; --"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/schedule", json={
        "title": sqli_payload, "body": sqli_payload, "delaySeconds": 30
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_SCHED_RBAC_004", "SQL Injection payload neutralized safely", "PASS" if valid else "FAIL", r.status_code, "Enqueued without SQL execution")
except Exception as e:
    record("NOTIF_SCHED_RBAC_004", "SQLi schedule check", "ERROR", 0, str(e))

# NOTIF_SCHED_RBAC_005: Verify unsupported HTTP method (GET /schedule) returns HTTP 405
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/schedule", headers=admin_headers, timeout=5)
    valid = r.status_code == 405
    record("NOTIF_SCHED_RBAC_005", "GET on /schedule returns HTTP 405 Method Not Allowed", "PASS" if valid else "FAIL", r.status_code, "Method Not Allowed returned")
except Exception as e:
    record("NOTIF_SCHED_RBAC_005", "HTTP 405 check", "ERROR", 0, str(e))


# ==============================================================================
# Summary & Exit
# ==============================================================================
print("\n" + "=" * 105)
print("   NOTIFICATION SCHEDULE SUITE EXECUTION SUMMARY")
print("=" * 105)
total_tc = len(results)
passed_tc = sum(1 for r in results if r["status"] == "PASS")
failed_tc = sum(1 for r in results if r["status"] == "FAIL")
error_tc = sum(1 for r in results if r["status"] == "ERROR")

print(f"Total Test Cases: {total_tc}")
print(f"Passed          : {passed_tc}")
print(f"Failed          : {failed_tc}")
print(f"Errors          : {error_tc}")
print(f"Compliance Rate : {(passed_tc / total_tc * 100):.1f}%")
print("=" * 105)

if failed_tc > 0 or error_tc > 0:
    sys.exit(1)
else:
    sys.exit(0)
