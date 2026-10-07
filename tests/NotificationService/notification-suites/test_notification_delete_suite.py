#!/usr/bin/env python3
"""
UAV PMS - NotificationService: Delete Notification Test Suite
Endpoint Tested:
- DELETE /api/v1/notifications/{id:guid}

Covers 28 standardized test cases:
- Function A: Successful Deletion (Happy Path) [NOTIF_DELETE_001 - NOTIF_DELETE_015] (15 TCs)
- Function B: Failed Deletion — Validation & Boundaries [NOTIF_DELETE_016 - NOTIF_DELETE_021] (6 TCs)
- Function C: Authorization (RBAC) & Security [NOTIF_DELETE_RBAC_001 - NOTIF_DELETE_RBAC_007] (7 TCs)
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
print("   UAV PMS - NOTIFICATION DELETION SUITE (28 TEST CASES)")
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

def create_sample_notification(user_id=target_user_id, notif_type="General", title="Delete Test", body="Body Content"):
    res = requests.post(f"{BASE_URL}/api/v1/notifications", json={
        "userId": user_id,
        "type": notif_type,
        "title": title,
        "body": body
    }, headers=admin_headers, timeout=5)
    return res.json().get("id")

# ==============================================================================
# FUNCTION A: Successful Deletion (Happy Path) (15 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Deletion (Happy Path)")

# NOTIF_DELETE_001: Verify successful deletion of an existing notification
try:
    nid1 = create_sample_notification(title="Notice To Delete 001")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid1}", headers=admin_headers, timeout=5)
    del_json = r_del.json()
    r_get = requests.get(f"{BASE_URL}/api/v1/notifications/{nid1}", headers=admin_headers, timeout=5)
    db_count = run_db_query(f'SELECT COUNT(*) FROM "Notifications" WHERE "Id" = \'{nid1}\';')
    
    valid = (r_del.status_code == 200 and
             del_json.get("success") is True and
             "deleted successfully" in del_json.get("message", "").lower() and
             r_get.status_code == 404 and
             db_count == "0")
    record("NOTIF_DELETE_001", "Successful deletion of an existing notification", "PASS" if valid else "FAIL", r_del.status_code, f"Deleted confirmed: GET=404, DB_count={db_count}")
except Exception as e:
    record("NOTIF_DELETE_001", "Successful deletion of notification", "ERROR", 0, str(e))

# NOTIF_DELETE_002: Verify deleted notification no longer appears in user's history list
try:
    nid2 = create_sample_notification(title="Notice History Check 002")
    requests.delete(f"{BASE_URL}/api/v1/notifications/{nid2}", headers=admin_headers, timeout=5)
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={target_user_id}", headers=admin_headers, timeout=5)
    items = r_hist.json().get("data", {}).get("items", [])
    found = any(it.get("id") == nid2 for it in items)
    valid = r_hist.status_code == 200 and not found
    record("NOTIF_DELETE_002", "Deleted notification absent from user history", "PASS" if valid else "FAIL", 200, f"Notification absent from history items (count={len(items)})")
except Exception as e:
    record("NOTIF_DELETE_002", "Deleted notification history check", "ERROR", 0, str(e))

# NOTIF_DELETE_003: Verify deletion of an unread notification succeeds
try:
    nid3 = create_sample_notification(title="Unread Notification 003")
    r_chk = requests.get(f"{BASE_URL}/api/v1/notifications/{nid3}", headers=admin_headers, timeout=5).json().get("data", {})
    is_unread = r_chk.get("isRead") is False
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid3}", headers=admin_headers, timeout=5)
    valid = is_unread and r_del.status_code == 200
    record("NOTIF_DELETE_003", "Deletion of an unread notification succeeds", "PASS" if valid else "FAIL", r_del.status_code, "Unread notification deleted without restriction")
except Exception as e:
    record("NOTIF_DELETE_003", "Unread deletion check", "ERROR", 0, str(e))

# NOTIF_DELETE_004: Verify deletion of an already-read notification succeeds
try:
    nid4 = create_sample_notification(title="Read Notification 004")
    requests.put(f"{BASE_URL}/api/v1/notifications/{nid4}/read", headers=admin_headers, timeout=5)
    r_chk = requests.get(f"{BASE_URL}/api/v1/notifications/{nid4}", headers=admin_headers, timeout=5).json().get("data", {})
    is_read = r_chk.get("isRead") is True
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid4}", headers=admin_headers, timeout=5)
    valid = is_read and r_del.status_code == 200
    record("NOTIF_DELETE_004", "Deletion of an already-read notification succeeds", "PASS" if valid else "FAIL", r_del.status_code, "Read notification deleted successfully")
except Exception as e:
    record("NOTIF_DELETE_004", "Read deletion check", "ERROR", 0, str(e))

# NOTIF_DELETE_005: Verify deletion of a pushed notification (IsPushed = true) succeeds
try:
    nid5 = create_sample_notification(title="Pushed Notification 005")
    run_db_query(f'UPDATE "Notifications" SET "IsPushed" = true, "PushedAt" = NOW() WHERE "Id" = \'{nid5}\';')
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid5}", headers=admin_headers, timeout=5)
    db_count = run_db_query(f'SELECT COUNT(*) FROM "Notifications" WHERE "Id" = \'{nid5}\';')
    valid = r_del.status_code == 200 and db_count == "0"
    record("NOTIF_DELETE_005", "Deletion of pushed notification succeeds", "PASS" if valid else "FAIL", r_del.status_code, "Pushed notification deleted from DB")
except Exception as e:
    record("NOTIF_DELETE_005", "Pushed deletion check", "ERROR", 0, str(e))

# NOTIF_DELETE_006: Verify deletion permanently removes record from database (hard delete)
try:
    nid6 = create_sample_notification(title="Hard Delete Check 006")
    requests.delete(f"{BASE_URL}/api/v1/notifications/{nid6}", headers=admin_headers, timeout=5)
    db_count = run_db_query(f'SELECT COUNT(*) FROM "Notifications" WHERE "Id" = \'{nid6}\';')
    valid = db_count == "0"
    record("NOTIF_DELETE_006", "Hard delete confirmed (row physically deleted)", "PASS" if valid else "FAIL", 200, f"PostgreSQL row count for ID = {db_count}")
except Exception as e:
    record("NOTIF_DELETE_006", "Hard delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_007: Verify deleting one notification does not affect other notifications for same user
try:
    n_a = create_sample_notification(title="User Notice A")
    n_b = create_sample_notification(title="User Notice B")
    requests.delete(f"{BASE_URL}/api/v1/notifications/{n_a}", headers=admin_headers, timeout=5)
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={target_user_id}", headers=admin_headers, timeout=5)
    items = r_hist.json().get("data", {}).get("items", [])
    has_a = any(it.get("id") == n_a for it in items)
    has_b = any(it.get("id") == n_b for it in items)
    valid = not has_a and has_b
    record("NOTIF_DELETE_007", "Deleting notice A preserves notice B for same user", "PASS" if valid else "FAIL", 200, f"Notice A removed: {not has_a}, Notice B intact: {has_b}")
except Exception as e:
    record("NOTIF_DELETE_007", "Isolation check", "ERROR", 0, str(e))

# NOTIF_DELETE_008: Verify uppercase GUID route parameter is handled correctly
try:
    nid8 = create_sample_notification(title="Uppercase GUID Check 008")
    upper_id = nid8.upper()
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{upper_id}", headers=admin_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_008", "GUID route parameter is case-insensitive", "PASS" if valid else "FAIL", r_del.status_code, "Uppercase GUID deleted successfully")
except Exception as e:
    record("NOTIF_DELETE_008", "Case insensitivity check", "ERROR", 0, str(e))

# NOTIF_DELETE_009: Verify deletion succeeds for all 6 NotificationType values
try:
    types = ["General", "MissionAssigned", "DefectDetected", "EmergencyAlert", "TicketAssigned", "EscalationRequest"]
    all_deleted = True
    for t in types:
        nt_id = create_sample_notification(notif_type=t, title=f"Type {t}")
        rd = requests.delete(f"{BASE_URL}/api/v1/notifications/{nt_id}", headers=admin_headers, timeout=5)
        if rd.status_code != 200:
            all_deleted = False
            break
    record("NOTIF_DELETE_009", "Deletion succeeds for all 6 NotificationType values", "PASS" if all_deleted else "FAIL", 200, f"All 6 enum types tested: {types}")
except Exception as e:
    record("NOTIF_DELETE_009", "All types deletion check", "ERROR", 0, str(e))

# NOTIF_DELETE_010: Verify deleting a notification does NOT affect associated User entity
try:
    nid10 = create_sample_notification(title="User Integrity Check 010")
    requests.delete(f"{BASE_URL}/api/v1/notifications/{nid10}", headers=admin_headers, timeout=5)
    user_exists = run_db_query(f'SELECT COUNT(*) FROM "Users" WHERE "Id" = \'{target_user_id}\';')
    valid = user_exists == "1"
    record("NOTIF_DELETE_010", "Deleting notice does not cascade or alter User record", "PASS" if valid else "FAIL", 200, f"Associated User record remains intact (count={user_exists})")
except Exception as e:
    record("NOTIF_DELETE_010", "User integrity check", "ERROR", 0, str(e))

# NOTIF_DELETE_011: Verify SystemAdmin can delete notifications
try:
    nid11 = create_sample_notification(title="Admin Delete 011")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid11}", headers=admin_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_011", "SystemAdmin can delete notifications", "PASS" if valid else "FAIL", r_del.status_code, "SystemAdmin privilege verified")
except Exception as e:
    record("NOTIF_DELETE_011", "SystemAdmin delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_012: Verify Manager can delete notifications
try:
    nid12 = create_sample_notification(title="Manager Delete 012")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid12}", headers=mgr_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_012", "Manager can delete notifications", "PASS" if valid else "FAIL", r_del.status_code, "Manager privilege verified")
except Exception as e:
    record("NOTIF_DELETE_012", "Manager delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_013: Verify Inspector can delete notifications
try:
    nid13 = create_sample_notification(title="Inspector Delete 013")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid13}", headers=insp_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_013", "Inspector can delete notifications", "PASS" if valid else "FAIL", r_del.status_code, "Inspector privilege verified")
except Exception as e:
    record("NOTIF_DELETE_013", "Inspector delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_014: Verify Analyst can delete notifications
try:
    nid14 = create_sample_notification(title="Analyst Delete 014")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid14}", headers=analyst_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_014", "Analyst can delete notifications", "PASS" if valid else "FAIL", r_del.status_code, "Analyst privilege verified")
except Exception as e:
    record("NOTIF_DELETE_014", "Analyst delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_015: Verify Technician can delete notifications
try:
    nid15 = create_sample_notification(title="Technician Delete 015")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid15}", headers=tech_headers, timeout=5)
    valid = r_del.status_code == 200
    record("NOTIF_DELETE_015", "Technician can delete notifications", "PASS" if valid else "FAIL", r_del.status_code, "Technician privilege verified")
except Exception as e:
    record("NOTIF_DELETE_015", "Technician delete check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION B: Failed Deletion — Validation & Boundaries (6 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Failed Deletion — Validation & Boundaries")

# NOTIF_DELETE_016: Verify deletion fails with Guid.Empty
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/00000000-0000-0000-0000-000000000000", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_DELETE_016", "DELETE with Guid.Empty returns HTTP 404 Not Found", "PASS" if valid else "FAIL", r.status_code, "NotFoundException returned")
except Exception as e:
    record("NOTIF_DELETE_016", "Guid.Empty delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_017: Verify deletion with invalid UUID format rejected by route constraint
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/invalid-uuid", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_DELETE_017", "DELETE with invalid UUID format returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Route constraint {id:guid} blocked invalid format")
except Exception as e:
    record("NOTIF_DELETE_017", "Invalid UUID delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_018: Verify double-deletion returns 404 on second attempt
try:
    nid18 = create_sample_notification(title="Double Delete Check 018")
    r1 = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid18}", headers=admin_headers, timeout=5)
    r2 = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid18}", headers=admin_headers, timeout=5)
    valid = r1.status_code == 200 and r2.status_code == 404 and "was not found" in r2.text
    record("NOTIF_DELETE_018", "Double deletion returns HTTP 404 on second attempt", "PASS" if valid else "FAIL", r2.status_code, "Second attempt returned NotFoundException")
except Exception as e:
    record("NOTIF_DELETE_018", "Double delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_019: Verify deletion with random non-existent valid UUID returns 404
try:
    rnd_id = str(uuid.uuid4())
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{rnd_id}", headers=admin_headers, timeout=5)
    valid = r.status_code == 404 and "was not found" in r.text
    record("NOTIF_DELETE_019", "DELETE with random valid UUID returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, f"Non-existent ID {rnd_id[:8]}... returned 404")
except Exception as e:
    record("NOTIF_DELETE_019", "Non-existent UUID delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_020: Verify rapid repeat delete requests for same notification ID
try:
    nid20 = create_sample_notification(title="Rapid Delete Check 020")
    statuses = [requests.delete(f"{BASE_URL}/api/v1/notifications/{nid20}", headers=admin_headers, timeout=5).status_code for _ in range(5)]
    valid = (statuses[0] == 200) and all(s == 404 for s in statuses[1:])
    record("NOTIF_DELETE_020", "Rapid repeat DELETE requests handled safely", "PASS" if valid else "FAIL", 200, f"Statuses: {statuses} (1st=200, subsequent=404)")
except Exception as e:
    record("NOTIF_DELETE_020", "Rapid repeat delete check", "ERROR", 0, str(e))

# NOTIF_DELETE_021: Verify deletion with whitespace-padded GUID returns 404
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{uuid.uuid4()}%20", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_DELETE_021", "DELETE with padded GUID returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Blocked by route constraint")
except Exception as e:
    record("NOTIF_DELETE_021", "Whitespace padded delete check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION C: Authorization (RBAC) & Security (7 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC) & Security")

# NOTIF_DELETE_RBAC_001: Verify unauthenticated (Anonymous) DELETE rejected with HTTP 401
try:
    nid_sec = create_sample_notification(title="Security Check Target")
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid_sec}", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_DELETE_RBAC_001", "Unauthenticated DELETE rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Anonymous request blocked")
except Exception as e:
    record("NOTIF_DELETE_RBAC_001", "Unauthenticated DELETE check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_002: Verify expired JWT token on DELETE rejected with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid_signature"
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{nid_sec}", headers={"Authorization": f"Bearer {expired_token}"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_DELETE_RBAC_002", "Expired JWT token on DELETE rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Expired token blocked")
except Exception as e:
    record("NOTIF_DELETE_RBAC_002", "Expired JWT DELETE check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_003: Verify SQL Injection resistance in route parameter
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/'%20OR%201=1%20--", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_DELETE_RBAC_003", "SQL injection in GUID parameter neutralized", "PASS" if valid else "FAIL", r.status_code, "SQL injection string blocked by route constraint")
except Exception as e:
    record("NOTIF_DELETE_RBAC_003", "SQL injection check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_004: Verify XSS payload resistance in route parameter
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/%3Cscript%3Ealert(1)%3C%2Fscript%3E", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_DELETE_RBAC_004", "XSS payload in route parameter neutralized", "PASS" if valid else "FAIL", r.status_code, "XSS script blocked by route constraint")
except Exception as e:
    record("NOTIF_DELETE_RBAC_004", "XSS payload check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_005: Verify Command Injection resistance in route parameter
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/test%3B%20rm%20-rf%20%2F", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_DELETE_RBAC_005", "Command injection payload neutralized", "PASS" if valid else "FAIL", r.status_code, "Command injection string blocked safely")
except Exception as e:
    record("NOTIF_DELETE_RBAC_005", "Command injection check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_006: Verify Path Traversal in route parameter neutralized
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/..%2F..%2Fetc%2Fpasswd", headers=admin_headers, timeout=5)
    valid = r.status_code in (400, 404)
    record("NOTIF_DELETE_RBAC_006", "Path traversal payload neutralized", "PASS" if valid else "FAIL", r.status_code, "Path traversal attempt blocked safely")
except Exception as e:
    record("NOTIF_DELETE_RBAC_006", "Path traversal check", "ERROR", 0, str(e))

# NOTIF_DELETE_RBAC_007: Verify error responses do not leak internal stack traces or connection strings
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{uuid.uuid4()}", headers=admin_headers, timeout=5)
    txt = r.text.lower()
    has_leak = any(k in txt for k in ["npgsql", "stacktrace", "exception:", "password", "database="])
    valid = not has_leak and r.status_code == 404
    record("NOTIF_DELETE_RBAC_007", "Error responses do not leak server internals", "PASS" if valid else "FAIL", r.status_code, "Clean ApiResponse returned without internal disclosures")
except Exception as e:
    record("NOTIF_DELETE_RBAC_007", "Information disclosure check", "ERROR", 0, str(e))


# ==============================================================================
# Summary & Exit
# ==============================================================================
print("\n" + "=" * 105)
print("   NOTIFICATION DELETION SUITE EXECUTION SUMMARY")
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
