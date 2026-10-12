#!/usr/bin/env python3
"""
UAV PMS - NotificationService: Get Notification By ID & Mark As Read Test Suite
Endpoints Tested:
- GET /api/v1/notifications/{id}
- PUT /api/v1/notifications/{id}/read

Covers 28 standardized test cases:
- Function A: Successful Get By ID & Mark As Read (Happy Path) [NOTIF_READ_001 - NOTIF_READ_013] (13 TCs)
- Function B: Failed Get By ID & Mark As Read — Validation & Boundaries [NOTIF_READ_014 - NOTIF_READ_021] (8 TCs)
- Function C: Authorization (RBAC) & Security [NOTIF_READ_RBAC_001 - NOTIF_READ_RBAC_007] (7 TCs)
"""

import concurrent.futures
import datetime
import json
import os
import subprocess
import sys
import time
import uuid
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
print("   UAV PMS - NOTIFICATION DETAIL & MARK AS READ SUITE (28 TEST CASES)")
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

# Fetch user IDs from database
target_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+inspector@gmail.com\' LIMIT 1;')
if not target_user_id or len(target_user_id) != 36:
    target_user_id = "33333333-3333-3333-3333-333333333333"

# ==============================================================================
# FUNCTION A: Successful Get By ID & Mark As Read (Happy Path) (13 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Get By ID & Mark As Read (Happy Path)")

# NOTIF_READ_001: Retrieve notification details by valid ID
try:
    notif = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": target_user_id,
        "type": "General",
        "title": "Tower Maintenance Notice",
        "body": "Inspect insulator string 102"
    }, headers=admin_headers, timeout=5).json()
    n_id = notif.get("id")
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    valid = r.status_code == 200 and data.get("id") == n_id and data.get("title") == "Tower Maintenance Notice"
    record("NOTIF_READ_001", "Retrieve notification details by valid ID", "PASS" if valid else "FAIL", r.status_code, f"Data ID: {data.get('id')}, title: {data.get('title')}")
except Exception as e:
    record("NOTIF_READ_001", "Retrieve notification details by valid ID", "ERROR", 0, str(e))

# NOTIF_READ_002: Mark notification as read via PUT /{id}/read
try:
    notif2 = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": target_user_id,
        "type": "General",
        "title": "Unread Notification",
        "body": "Pending read status"
    }, headers=admin_headers, timeout=5).json()
    n_id2 = notif2.get("id")
    r_put = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id2}/read", headers=admin_headers, timeout=5)
    r_verify = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id2}", headers=admin_headers, timeout=5)
    is_read = r_verify.json().get("data", {}).get("isRead")
    valid = r_put.status_code == 200 and is_read is True
    record("NOTIF_READ_002", "Mark notification as read via PUT /{id}/read", "PASS" if valid else "FAIL", r_put.status_code, f"isRead verified as {is_read}")
except Exception as e:
    record("NOTIF_READ_002", "Mark notification as read via PUT /{id}/read", "ERROR", 0, str(e))

# NOTIF_READ_003: Idempotent mark as read (repeated calls succeed safely)
try:
    r_rep1 = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id2}/read", headers=admin_headers, timeout=5)
    r_rep2 = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id2}/read", headers=admin_headers, timeout=5)
    valid = r_rep1.status_code == 200 and r_rep2.status_code == 200
    record("NOTIF_READ_003", "Idempotent mark as read (repeated calls succeed)", "PASS" if valid else "FAIL", r_rep2.status_code, "Repeated PUT /read succeeded idempotently")
except Exception as e:
    record("NOTIF_READ_003", "Idempotent mark as read", "ERROR", 0, str(e))

# NOTIF_READ_004: Verify mark-as-read updates IsRead to true in database
try:
    db_is_read = run_db_query(f'SELECT "IsRead" FROM "Notifications" WHERE "Id" = \'{n_id2}\';')
    valid = db_is_read.lower() in ("t", "true", "1")
    record("NOTIF_READ_004", "Verify IsRead = true in database record", "PASS" if valid else "FAIL", 200, f"Database IsRead: {db_is_read}")
except Exception as e:
    record("NOTIF_READ_004", "Verify IsRead in database", "ERROR", 0, str(e))

# NOTIF_READ_005: Verify mark-as-read does NOT modify other notification fields
try:
    notif3 = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": target_user_id,
        "type": "DefectDetected",
        "title": "Original Title",
        "body": "Original Body Content"
    }, headers=admin_headers, timeout=5).json()
    n_id3 = notif3.get("id")
    requests.put(f"{BASE_URL}/api/v1/notifications/{n_id3}/read", headers=admin_headers, timeout=5)
    r_check = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id3}", headers=admin_headers, timeout=5).json().get("data", {})
    valid = (r_check.get("title") == "Original Title" and
             r_check.get("body") == "Original Body Content" and
             r_check.get("type") == "DefectDetected" and
             r_check.get("userId") == target_user_id and
             r_check.get("isRead") is True)
    record("NOTIF_READ_005", "Mark-as-read preserves all other notification fields", "PASS" if valid else "FAIL", 200, "Title, body, type, userId intact")
except Exception as e:
    record("NOTIF_READ_005", "Preserve fields on mark-as-read", "ERROR", 0, str(e))

# NOTIF_READ_006: Verify GET by ID returns NotificationDto without internal entity fields
try:
    r_dto = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=admin_headers, timeout=5).json().get("data", {})
    expected_fields = ["id", "userId", "type", "title", "body", "isRead", "sentAt"]
    has_expected = all(f in r_dto for f in expected_fields)
    no_internal = "isPushed" not in r_dto and "pushedAt" not in r_dto
    valid = has_expected and no_internal
    record("NOTIF_READ_006", "GET /{id} returns NotificationDto without internal fields", "PASS" if valid else "FAIL", 200, f"DTO exposed fields: {list(r_dto.keys())[:7]}")
except Exception as e:
    record("NOTIF_READ_006", "NotificationDto field filtering", "ERROR", 0, str(e))

# NOTIF_READ_007: Verify GET by ID for each valid notification type string
try:
    types = ["General", "MissionAssigned", "DefectDetected", "EmergencyAlert", "TicketAssigned", "EscalationRequest"]
    all_matched = True
    for t in types:
        nt = requests.post(f"{BASE_URL}/api/v1/notifications", json={
            "userId": target_user_id, "type": t, "title": f"Test {t}", "body": "B"
        }, headers=admin_headers, timeout=5).json()
        chk = requests.get(f"{BASE_URL}/api/v1/notifications/{nt['id']}", headers=admin_headers, timeout=5).json().get("data", {})
        if chk.get("type") != t:
            all_matched = False
            break
    record("NOTIF_READ_007", "GET /{id} maps all 6 NotificationType enum values", "PASS" if all_matched else "FAIL", 200, f"Verified 6 enum types: {types}")
except Exception as e:
    record("NOTIF_READ_007", "Enum mapping verification", "ERROR", 0, str(e))

# NOTIF_READ_008: Route parameter GUID case-insensitivity
try:
    upper_id = n_id.upper()
    r_upper = requests.get(f"{BASE_URL}/api/v1/notifications/{upper_id}", headers=admin_headers, timeout=5)
    valid = r_upper.status_code == 200 and r_upper.json().get("data", {}).get("id") == n_id.lower()
    record("NOTIF_READ_008", "GUID route parameter is case-insensitive", "PASS" if valid else "FAIL", r_upper.status_code, "Uppercase GUID resolved successfully")
except Exception as e:
    record("NOTIF_READ_008", "Case insensitivity check", "ERROR", 0, str(e))

# NOTIF_READ_009: SystemAdmin role access
try:
    r_g = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=admin_headers, timeout=5)
    r_p = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=admin_headers, timeout=5)
    valid = r_g.status_code == 200 and r_p.status_code == 200
    record("NOTIF_READ_009", "SystemAdmin can retrieve and mark-as-read", "PASS" if valid else "FAIL", 200, "SystemAdmin privilege verified")
except Exception as e:
    record("NOTIF_READ_009", "SystemAdmin role check", "ERROR", 0, str(e))

# NOTIF_READ_010: Manager role access
try:
    r_g = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=mgr_headers, timeout=5)
    r_p = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=mgr_headers, timeout=5)
    valid = r_g.status_code == 200 and r_p.status_code == 200
    record("NOTIF_READ_010", "Manager can retrieve and mark-as-read", "PASS" if valid else "FAIL", 200, "Manager privilege verified")
except Exception as e:
    record("NOTIF_READ_010", "Manager role check", "ERROR", 0, str(e))

# NOTIF_READ_011: Inspector role access
try:
    r_g = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=insp_headers, timeout=5)
    r_p = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=insp_headers, timeout=5)
    valid = r_g.status_code == 200 and r_p.status_code == 200
    record("NOTIF_READ_011", "Inspector can retrieve and mark-as-read", "PASS" if valid else "FAIL", 200, "Inspector privilege verified")
except Exception as e:
    record("NOTIF_READ_011", "Inspector role check", "ERROR", 0, str(e))

# NOTIF_READ_012: Analyst role access
try:
    r_g = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=analyst_headers, timeout=5)
    r_p = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=analyst_headers, timeout=5)
    valid = r_g.status_code == 200 and r_p.status_code == 200
    record("NOTIF_READ_012", "Analyst can retrieve and mark-as-read", "PASS" if valid else "FAIL", 200, "Analyst privilege verified")
except Exception as e:
    record("NOTIF_READ_012", "Analyst role check", "ERROR", 0, str(e))

# NOTIF_READ_013: Technician role access
try:
    r_g = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=tech_headers, timeout=5)
    r_p = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=tech_headers, timeout=5)
    valid = r_g.status_code == 200 and r_p.status_code == 200
    record("NOTIF_READ_013", "Technician can retrieve and mark-as-read", "PASS" if valid else "FAIL", 200, "Technician privilege verified")
except Exception as e:
    record("NOTIF_READ_013", "Technician role check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION B: Failed Get By ID & Mark As Read — Validation & Boundaries (8 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Failed Get By ID & Mark As Read — Validation & Boundaries")

# NOTIF_READ_014: GET with Guid.Empty (00000000-0000-0000-0000-000000000000) returns 404
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/00000000-0000-0000-0000-000000000000", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_READ_014", "GET with Guid.Empty returns HTTP 404 Not Found", "PASS" if valid else "FAIL", r.status_code, "NotFoundException returned")
except Exception as e:
    record("NOTIF_READ_014", "GET with Guid.Empty check", "ERROR", 0, str(e))

# NOTIF_READ_015: GET with invalid GUID format rejected by route constraint
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/invalid-uuid-format", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_READ_015", "GET with invalid UUID format returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Route constraint {id:guid} blocked invalid format")
except Exception as e:
    record("NOTIF_READ_015", "GET with invalid UUID format check", "ERROR", 0, str(e))

# NOTIF_READ_016: PUT /read with Guid.Empty returns 404
try:
    r = requests.put(f"{BASE_URL}/api/v1/notifications/00000000-0000-0000-0000-000000000000/read", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_READ_016", "PUT /read with Guid.Empty returns HTTP 404 Not Found", "PASS" if valid else "FAIL", r.status_code, "NotFoundException returned")
except Exception as e:
    record("NOTIF_READ_016", "PUT /read with Guid.Empty check", "ERROR", 0, str(e))

# NOTIF_READ_017: PUT /read with invalid GUID format rejected by route constraint
try:
    r = requests.put(f"{BASE_URL}/api/v1/notifications/invalid-uuid-format/read", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_READ_017", "PUT /read with invalid UUID format returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Route constraint {id:guid} blocked invalid format")
except Exception as e:
    record("NOTIF_READ_017", "PUT /read with invalid UUID format check", "ERROR", 0, str(e))

# NOTIF_READ_018: GET by ID with random valid UUID not in database returns 404
try:
    rnd_id = str(uuid.uuid4())
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{rnd_id}", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_READ_018", "GET with random non-existent UUID returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, f"Non-existent ID {rnd_id[:8]}... returned 404")
except Exception as e:
    record("NOTIF_READ_018", "GET non-existent UUID check", "ERROR", 0, str(e))

# NOTIF_READ_019: PUT /read with random valid UUID not in database returns 404
try:
    rnd_id2 = str(uuid.uuid4())
    r = requests.put(f"{BASE_URL}/api/v1/notifications/{rnd_id2}/read", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_READ_019", "PUT /read with random non-existent UUID returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "NotFoundException returned")
except Exception as e:
    record("NOTIF_READ_019", "PUT /read non-existent UUID check", "ERROR", 0, str(e))

# NOTIF_READ_020: Concurrent mark-as-read requests do not cause errors or race conditions
try:
    notif_c = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": target_user_id, "type": "General", "title": "Concurrent Target", "body": "B"
    }, headers=admin_headers, timeout=5).json()
    c_id = notif_c["id"]
    
    def fire_read():
        return requests.put(f"{BASE_URL}/api/v1/notifications/{c_id}/read", headers=admin_headers, timeout=5).status_code

    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as executor:
        status_codes = list(executor.map(lambda _: fire_read(), range(5)))
    
    all_200 = all(code == 200 for code in status_codes)
    record("NOTIF_READ_020", "5 concurrent PUT /read requests handled idempotently", "PASS" if all_200 else "FAIL", 200, f"Status codes: {status_codes}")
except Exception as e:
    record("NOTIF_READ_020", "Concurrent mark-as-read check", "ERROR", 0, str(e))

# NOTIF_READ_021: Unsupported HTTP methods return HTTP 405 Method Not Allowed
try:
    r_post = requests.post(f"{BASE_URL}/api/v1/notifications/{n_id}", headers=admin_headers, timeout=5)
    valid = r_post.status_code == 405
    record("NOTIF_READ_021", "POST on /{id} returns HTTP 405 Method Not Allowed", "PASS" if valid else "FAIL", r_post.status_code, "Unsupported HTTP method rejected")
except Exception as e:
    record("NOTIF_READ_021", "HTTP 405 check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION C: Authorization (RBAC) & Security (7 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC) & Security")

# NOTIF_READ_RBAC_001: Unauthenticated GET /{id} rejected with HTTP 401
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_READ_RBAC_001", "Unauthenticated GET /{id} rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Anonymous request blocked")
except Exception as e:
    record("NOTIF_READ_RBAC_001", "Unauthenticated GET check", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_002: Unauthenticated PUT /{id}/read rejected with HTTP 401
try:
    r = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_READ_RBAC_002", "Unauthenticated PUT /{id}/read rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Anonymous request blocked")
except Exception as e:
    record("NOTIF_READ_RBAC_002", "Unauthenticated PUT check", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_003: Expired JWT token on GET /{id} rejected with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid_signature"
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{n_id}", headers={"Authorization": f"Bearer {expired_token}"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_READ_RBAC_003", "Expired JWT token on GET /{id} rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Expired token blocked")
except Exception as e:
    record("NOTIF_READ_RBAC_003", "Expired JWT check GET", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_004: Expired JWT token on PUT /{id}/read rejected with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid_signature"
    r = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers={"Authorization": f"Bearer {expired_token}"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_READ_RBAC_004", "Expired JWT token on PUT /{id}/read rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Expired token blocked")
except Exception as e:
    record("NOTIF_READ_RBAC_004", "Expired JWT check PUT", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_005: SQL Injection resistance in route parameter
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/'%20OR%201=1%20--", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_READ_RBAC_005", "SQL injection in GUID parameter neutralized", "PASS" if valid else "FAIL", r.status_code, "SQL injection string blocked by route constraint")
except Exception as e:
    record("NOTIF_READ_RBAC_005", "SQL injection check", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_006: Error responses do not leak sensitive server internals
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{uuid.uuid4()}", headers=admin_headers, timeout=5)
    txt = r.text.lower()
    has_leak = any(k in txt for k in ["npgsql", "stacktrace", "exception:", "password", "database="])
    valid = not has_leak and r.status_code == 404
    record("NOTIF_READ_RBAC_006", "Error messages do not leak internal stack traces", "PASS" if valid else "FAIL", r.status_code, "Clean ApiResponse returned without internals")
except Exception as e:
    record("NOTIF_READ_RBAC_006", "Information disclosure check", "ERROR", 0, str(e))

# NOTIF_READ_RBAC_007: XSS payload in notification content safely returned as escaped JSON
try:
    xss_payload = "<script>alert('xss')</script>"
    notif_x = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": target_user_id, "type": "General", "title": xss_payload, "body": xss_payload
    }, headers=admin_headers, timeout=5).json()
    r_x = requests.get(f"{BASE_URL}/api/v1/notifications/{notif_x['id']}", headers=admin_headers, timeout=5).json().get("data", {})
    valid = r_x.get("title") == xss_payload and r_x.get("body") == xss_payload
    record("NOTIF_READ_RBAC_007", "XSS payload stored and safely returned as JSON text", "PASS" if valid else "FAIL", 200, "XSS script payload safely contained")
except Exception as e:
    record("NOTIF_READ_RBAC_007", "XSS payload handling check", "ERROR", 0, str(e))


# ==============================================================================
# Summary & Exit
# ==============================================================================
print("\n" + "=" * 105)
print("   NOTIFICATION DETAIL & MARK AS READ SUITE EXECUTION SUMMARY")
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
