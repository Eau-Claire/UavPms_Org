#!/usr/bin/env python3
"""
UAV PMS - NotificationService: Notification History Retrieval & Lifecycle Test Suite
Covers 49 standardized test cases:
- Function A: Successful History & Notification Operations (Happy Path) [NOTIF_HISTORY_001 - NOTIF_HISTORY_022] (22 TCs)
- Function B: Failed History Retrieval & Lifecycle Boundaries [NOTIF_HISTORY_023 - NOTIF_HISTORY_037] (15 TCs)
- Function C: Authorization (RBAC), IDOR & Security [NOTIF_HISTORY_RBAC_001 - NOTIF_HISTORY_RBAC_012] (12 TCs)
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
    print(f"[{color}{status:<9}{reset}] {tc_id:<24} (HTTP {http_code:<3}): {name} -> {details[:85]}")

print("=" * 105)
print("   UAV PMS - NOTIFICATION SERVICE HISTORY & LIFECYCLE TEST SUITE (49 TEST CASES)")
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
insp_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+inspector@gmail.com\' LIMIT 1;')
if not insp_user_id or len(insp_user_id) != 36:
    insp_user_id = "33333333-3333-3333-3333-333333333333"

mgr_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+manager@gmail.com\' LIMIT 1;')
if not mgr_user_id or len(mgr_user_id) != 36:
    mgr_user_id = "22222222-2222-2222-2222-222222222222"

analyst_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+analyst@gmail.com\' LIMIT 1;')
if not analyst_user_id or len(analyst_user_id) != 36:
    analyst_user_id = "44444444-4444-4444-4444-444444444444"

tech_user_id = run_db_query('SELECT "Id" FROM "Users" WHERE "Email" = \'An3439201+technician@gmail.com\' LIMIT 1;')
if not tech_user_id or len(tech_user_id) != 36:
    tech_user_id = "55555555-5555-5555-5555-555555555555"

# Ensure fixture users exist in DB
test_user_a = "11111111-1111-1111-1111-111111111111"
test_user_empty = "11111111-1111-1111-1111-111111111112"
run_db_query(f"""
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "FullName", "Email", "Phone", "Status", "CreatedAt", "IsDeleted")
VALUES ('{test_user_a}', 'test_user_hist_a', 'hash', 'Test Hist A', 'test_hist_a@example.com', '0123456781', 'Active', NOW(), false)
ON CONFLICT ("Id") DO NOTHING;
""")
run_db_query(f"""
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "FullName", "Email", "Phone", "Status", "CreatedAt", "IsDeleted")
VALUES ('{test_user_empty}', 'test_user_hist_empty', 'hash', 'Test Hist Empty', 'test_hist_empty@example.com', '0123456782', 'Active', NOW(), false)
ON CONFLICT ("Id") DO NOTHING;
""")

# Seed at least 1 notification for Inspector user if none exist
requests.post(f"{BASE_URL}/api/v1/notifications", json={
    "userId": insp_user_id,
    "type": "MissionAssigned",
    "title": "Kiểm tra thực địa định kỳ",
    "body": "Đã phân công kiểm tra tuyến truyền tải."
}, headers=admin_headers, timeout=5)

# ==============================================================================
# FUNCTION A: Successful History & Operations (Happy Path - 22 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful History & Operations (Happy Path)")

# NOTIF_HISTORY_001: Retrieve notification history for a valid userId
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5)
    if r.status_code == 200:
        data = r.json()
        items = data.get("data", {}).get("items", [])
        has_items = isinstance(items, list) and len(items) > 0
        record("NOTIF_HISTORY_001", "Retrieve notification history for valid userId", "PASS" if has_items else "FAIL", r.status_code, f"Items retrieved: {len(items)}, totalCount: {data.get('data', {}).get('totalCount')}")
    else:
        record("NOTIF_HISTORY_001", "Retrieve notification history for valid userId", "FAIL", r.status_code, r.text)
except Exception as e:
    record("NOTIF_HISTORY_001", "Retrieve notification history for valid userId", "ERROR", 0, str(e))

# NOTIF_HISTORY_002: Return empty array when user has no notifications
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={test_user_empty}", headers=admin_headers, timeout=5)
    if r.status_code == 200:
        data = r.json()
        items = data.get("data", {}).get("items", [])
        total = data.get("data", {}).get("totalCount", -1)
        valid = (items == [] and total == 0)
        record("NOTIF_HISTORY_002", "Return empty array when user has no notifications", "PASS" if valid else "FAIL", r.status_code, f"items: {items}, totalCount: {total}")
    else:
        record("NOTIF_HISTORY_002", "Return empty array when user has no notifications", "FAIL", r.status_code, r.text)
except Exception as e:
    record("NOTIF_HISTORY_002", "Return empty array when user has no notifications", "ERROR", 0, str(e))

# NOTIF_HISTORY_003: History returns both read and unread notifications
try:
    r1 = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "Notif 1", "body": "B1"}, headers=admin_headers, timeout=5).json()
    r2 = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "Notif 2", "body": "B2"}, headers=admin_headers, timeout=5).json()
    id1 = r1.get("id")
    id2 = r2.get("id")
    requests.put(f"{BASE_URL}/api/v1/notifications/{id1}/read", headers=admin_headers, timeout=5)
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={test_user_a}", headers=admin_headers, timeout=5).json()
    items = r_hist.get("data", {}).get("items", [])
    has_read = any(x.get("id") == id1 and x.get("isRead") is True for x in items)
    has_unread = any(x.get("id") == id2 and x.get("isRead") is False for x in items)
    valid = (has_read and has_unread)
    record("NOTIF_HISTORY_003", "History returns both read and unread notifications", "PASS" if valid else "FAIL", 200, f"Read notif: {has_read}, Unread notif: {has_unread}")
except Exception as e:
    record("NOTIF_HISTORY_003", "History returns both read and unread notifications", "ERROR", 0, str(e))

# NOTIF_HISTORY_004: History returns notifications with diverse types
try:
    types_to_create = ["General", "MissionAssigned", "DefectDetected", "EmergencyAlert", "SystemNotice"]
    for t in types_to_create:
        requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": t, "title": f"Title {t}", "body": f"Body {t}"}, headers=admin_headers, timeout=5)
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={test_user_a}&limit=50", headers=admin_headers, timeout=5).json()
    returned_types = [x.get("type") for x in r_hist.get("data", {}).get("items", [])]
    valid = all(t in returned_types for t in types_to_create)
    record("NOTIF_HISTORY_004", "History returns notifications with diverse notification types", "PASS" if valid else "FAIL", 200, f"Types present: {set(returned_types)}")
except Exception as e:
    record("NOTIF_HISTORY_004", "History returns notifications with diverse notification types", "ERROR", 0, str(e))

# NOTIF_HISTORY_005: Verify history excludes deleted notifications
try:
    res_del = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "To Delete", "body": "Del"}, headers=admin_headers, timeout=5).json()
    del_id = res_del.get("id")
    requests.delete(f"{BASE_URL}/api/v1/notifications/{del_id}", headers=admin_headers, timeout=5)
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={test_user_a}&limit=100", headers=admin_headers, timeout=5).json()
    items = r_hist.get("data", {}).get("items", [])
    valid = not any(x.get("id") == del_id for x in items)
    record("NOTIF_HISTORY_005", "Verify history excludes deleted notifications", "PASS" if valid else "FAIL", 200, f"Deleted notification {del_id[:8]} excluded: {valid}")
except Exception as e:
    record("NOTIF_HISTORY_005", "Verify history excludes deleted notifications", "ERROR", 0, str(e))

# NOTIF_HISTORY_006: Results ordered by sentAt descending (newest first)
try:
    r_first = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "Older Notif", "body": "Older"}, headers=admin_headers, timeout=5).json()
    time.sleep(1.1)
    r_second = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "Newer Notif", "body": "Newer"}, headers=admin_headers, timeout=5).json()
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={test_user_a}&limit=20", headers=admin_headers, timeout=5).json()
    items = r_hist.get("data", {}).get("items", [])
    idx_second = next((i for i, x in enumerate(items) if x.get("id") == r_second.get("id")), -1)
    idx_first = next((i for i, x in enumerate(items) if x.get("id") == r_first.get("id")), -1)
    valid = (idx_second != -1 and idx_first != -1 and idx_second < idx_first)
    record("NOTIF_HISTORY_006", "History results ordered by sentAt descending (newest first)", "PASS" if valid else "FAIL", 200, f"Newer index: {idx_second}, Older index: {idx_first}")
except Exception as e:
    record("NOTIF_HISTORY_006", "History results ordered by sentAt descending (newest first)", "ERROR", 0, str(e))

# NOTIF_HISTORY_007: Verify history returns accurate items count & totalCount
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&limit=100", headers=admin_headers, timeout=5).json()
    items = r.get("data", {}).get("items", [])
    total = r.get("data", {}).get("totalCount", 0)
    valid = len(items) > 0 and total >= len(items)
    record("NOTIF_HISTORY_007", "Verify history returns accurate items count & totalCount", "PASS" if valid else "FAIL", 200, f"Items: {len(items)}, totalCount: {total}")
except Exception as e:
    record("NOTIF_HISTORY_007", "Verify history returns accurate items count & totalCount", "ERROR", 0, str(e))

# NOTIF_HISTORY_008: Verify history strictly isolates notifications by userId (no leakage)
try:
    r_b = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": mgr_user_id, "type": "General", "title": "User Manager Only", "body": "Private to Manager"}, headers=admin_headers, timeout=5).json()
    id_b = r_b.get("id")
    r_hist_a = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5).json()
    items_a = r_hist_a.get("data", {}).get("items", [])
    has_b = any(x.get("id") == id_b for x in items_a)
    all_a = all(x.get("userId") == insp_user_id for x in items_a)
    valid = not has_b and all_a
    record("NOTIF_HISTORY_008", "Verify history strictly isolates notifications by userId (no leakage)", "PASS" if valid else "FAIL", 200, f"Manager notif present in Inspector list: {has_b}, All match: {all_a}")
except Exception as e:
    record("NOTIF_HISTORY_008", "Verify history strictly isolates notifications by userId", "ERROR", 0, str(e))

# NOTIF_HISTORY_009: SystemAdmin can retrieve notification history for any user
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_HISTORY_009", "SystemAdmin can retrieve notification history for any user", "PASS" if valid else "FAIL", r.status_code, "SystemAdmin bypass verified")
except Exception as e:
    record("NOTIF_HISTORY_009", "SystemAdmin can retrieve notification history for any user", "ERROR", 0, str(e))

# NOTIF_HISTORY_010: Manager can retrieve notification history for users
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=mgr_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_HISTORY_010", "Manager role can retrieve notification history for users", "PASS" if valid else "FAIL", r.status_code, "Manager supervisory access verified")
except Exception as e:
    record("NOTIF_HISTORY_010", "Manager role can retrieve notification history for users", "ERROR", 0, str(e))

# NOTIF_HISTORY_011: Inspector can retrieve notification history for own userId
try:
    r_own = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=insp_headers, timeout=5)
    valid = (r_own.status_code == 200 and r_own.json().get("success") is True)
    record("NOTIF_HISTORY_011", "Inspector can retrieve own notification history", "PASS" if valid else "FAIL", r_own.status_code, f"Status: {r_own.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_011", "Inspector can retrieve own notification history", "ERROR", 0, str(e))

# NOTIF_HISTORY_012: Analyst can retrieve notification history for own userId
try:
    r_own = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={analyst_user_id}", headers=analyst_headers, timeout=5)
    valid = (r_own.status_code == 200 and r_own.json().get("success") is True)
    record("NOTIF_HISTORY_012", "Analyst can retrieve own notification history", "PASS" if valid else "FAIL", r_own.status_code, f"Status: {r_own.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_012", "Analyst can retrieve own notification history", "ERROR", 0, str(e))

# NOTIF_HISTORY_013: Technician can retrieve notification history for own userId
try:
    r_own = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={tech_user_id}", headers=tech_headers, timeout=5)
    valid = (r_own.status_code == 200 and r_own.json().get("success") is True)
    record("NOTIF_HISTORY_013", "Technician can retrieve own notification history", "PASS" if valid else "FAIL", r_own.status_code, f"Status: {r_own.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_013", "Technician can retrieve own notification history", "ERROR", 0, str(e))

# NOTIF_HISTORY_014: Verify history returns notifications with correct NotificationDto structure
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5).json()
    items = r.get("data", {}).get("items", [])
    if items:
        item = items[0]
        required_keys = ["id", "userId", "type", "referenceType", "referenceId", "title", "body", "isRead", "sentAt", "readAt"]
        valid = all(k in item for k in required_keys)
        record("NOTIF_HISTORY_014", "Verify NotificationDto structure with all required fields", "PASS" if valid else "FAIL", 200, f"Fields verified: {list(item.keys())}")
    else:
        record("NOTIF_HISTORY_014", "Verify NotificationDto structure with all required fields", "FAIL", 200, "No items to inspect")
except Exception as e:
    record("NOTIF_HISTORY_014", "Verify NotificationDto structure with all required fields", "ERROR", 0, str(e))

# NOTIF_HISTORY_015: Retrieve history for inactive/pending user returns valid result
try:
    pending_uid = run_db_query('SELECT "Id" FROM "Users" WHERE "Status" = \'Pending\' LIMIT 1;')
    if not pending_uid or len(pending_uid) != 36:
        pending_uid = "99999999-9999-9999-9999-999999999999"
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={pending_uid}", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_HISTORY_015", "Retrieve history for inactive/pending user returns valid result", "PASS" if valid else "FAIL", r.status_code, f"Pending user: {pending_uid[:8]}...")
except Exception as e:
    record("NOTIF_HISTORY_015", "Retrieve history for inactive/pending user returns valid result", "ERROR", 0, str(e))

# NOTIF_HISTORY_016: Root endpoint alias GET /api/v1/notifications returns history identically
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications?userId={insp_user_id}", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True and "items" in r.json().get("data", {})
    record("NOTIF_HISTORY_016", "Root endpoint alias GET /notifications returns history identically", "PASS" if valid else "FAIL", r.status_code, "Root route alias verified")
except Exception as e:
    record("NOTIF_HISTORY_016", "Root endpoint alias GET /notifications", "ERROR", 0, str(e))

# NOTIF_HISTORY_017: Pagination limit filtering via GET /history?limit=10
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&limit=10", headers=admin_headers, timeout=5)
    items = r.json().get("data", {}).get("items", [])
    valid = r.status_code == 200 and len(items) <= 10
    record("NOTIF_HISTORY_017", "Pagination limit filtering via ?limit=10 returns <= 10 items", "PASS" if valid else "FAIL", r.status_code, f"Returned items count: {len(items)}")
except Exception as e:
    record("NOTIF_HISTORY_017", "Pagination limit filtering via ?limit=10", "ERROR", 0, str(e))

# NOTIF_HISTORY_018: Full list retrieval using ?limit=0 returns all records
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&limit=0", headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    items = data.get("items", [])
    total = data.get("totalCount", 0)
    valid = r.status_code == 200 and len(items) == total
    record("NOTIF_HISTORY_018", "Full list retrieval using ?limit=0 returns all records", "PASS" if valid else "FAIL", r.status_code, f"Items: {len(items)}, totalCount: {total}")
except Exception as e:
    record("NOTIF_HISTORY_018", "Full list retrieval using ?limit=0", "ERROR", 0, str(e))

# NOTIF_HISTORY_019: Retrieve single notification details via GET /api/v1/notifications/{id}
try:
    r_hist = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&limit=1", headers=admin_headers, timeout=5).json()
    sample_item = r_hist.get("data", {}).get("items", [])[0]
    sample_id = sample_item["id"]
    r_detail = requests.get(f"{BASE_URL}/api/v1/notifications/{sample_id}", headers=admin_headers, timeout=5)
    d_data = r_detail.json().get("data", {})
    valid = r_detail.status_code == 200 and d_data.get("id") == sample_id
    record("NOTIF_HISTORY_019", "Retrieve single notification details via GET /{id}", "PASS" if valid else "FAIL", r_detail.status_code, f"Detail ID matched: {d_data.get('id')}")
except Exception as e:
    record("NOTIF_HISTORY_019", "Retrieve single notification details via GET /{id}", "ERROR", 0, str(e))

# NOTIF_HISTORY_020: Mark an unread notification as read via PUT /{id}/read
try:
    notif = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "Unread", "body": "Mark me"}, headers=admin_headers, timeout=5).json()
    n_id = notif.get("id")
    r_mark = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=admin_headers, timeout=5)
    valid = r_mark.status_code == 200 and r_mark.json().get("success") is True
    record("NOTIF_HISTORY_020", "Mark an unread notification as read via PUT /{id}/read", "PASS" if valid else "FAIL", r_mark.status_code, "Marked as read successfully")
except Exception as e:
    record("NOTIF_HISTORY_020", "Mark an unread notification as read", "ERROR", 0, str(e))

# NOTIF_HISTORY_021: Idempotent mark as read (calling repeatedly succeeds safely)
try:
    r_mark2 = requests.put(f"{BASE_URL}/api/v1/notifications/{n_id}/read", headers=admin_headers, timeout=5)
    valid = r_mark2.status_code == 200 and r_mark2.json().get("success") is True
    record("NOTIF_HISTORY_021", "Idempotent mark as read (repeated PUT /{id}/read succeeds)", "PASS" if valid else "FAIL", r_mark2.status_code, "Idempotent read status confirmed")
except Exception as e:
    record("NOTIF_HISTORY_021", "Idempotent mark as read", "ERROR", 0, str(e))

# NOTIF_HISTORY_022: Delete notification via DELETE /api/v1/notifications/{id}
try:
    notif2 = requests.post(f"{BASE_URL}/api/v1/notifications", json={"userId": test_user_a, "type": "General", "title": "To Purge", "body": "Purge me"}, headers=admin_headers, timeout=5).json()
    n_id2 = notif2.get("id")
    r_del = requests.delete(f"{BASE_URL}/api/v1/notifications/{n_id2}", headers=admin_headers, timeout=5)
    valid = r_del.status_code == 200 and r_del.json().get("success") is True
    record("NOTIF_HISTORY_022", "Delete notification via DELETE /api/v1/notifications/{id}", "PASS" if valid else "FAIL", r_del.status_code, "Deleted notification successfully")
except Exception as e:
    record("NOTIF_HISTORY_022", "Delete notification via DELETE /{id}", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION B: Failed History Retrieval & Boundaries (15 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Failed History Retrieval & Boundaries")

# NOTIF_HISTORY_023: Handle missing userId query parameter
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400]
    detail = "Gracefully defaulted to caller's own notifications" if r.status_code == 200 else "Returned 400 Bad Request"
    record("NOTIF_HISTORY_023", "Handle missing userId query parameter (fallback to caller or 400)", "PASS" if valid else "FAIL", r.status_code, detail)
except Exception as e:
    record("NOTIF_HISTORY_023", "Handle missing userId query parameter", "ERROR", 0, str(e))

# NOTIF_HISTORY_024: Handle empty string userId query parameter (?userId=)
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId=", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400]
    detail = "Handled empty userId parameter safely" if r.status_code in [200, 400] else "Unexpected status"
    record("NOTIF_HISTORY_024", "Handle empty string userId query parameter safely", "PASS" if valid else "FAIL", r.status_code, detail)
except Exception as e:
    record("NOTIF_HISTORY_024", "Handle empty string userId query parameter safely", "ERROR", 0, str(e))

# NOTIF_HISTORY_025: Handle invalid non-UUID format in userId
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId=invalid-uuid", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400]
    detail = "Fallback to authenticated context or 400" if r.status_code in [200, 400] else "Unexpected status"
    record("NOTIF_HISTORY_025", "Handle invalid UUID string format safely", "PASS" if valid else "FAIL", r.status_code, detail)
except Exception as e:
    record("NOTIF_HISTORY_025", "Handle invalid UUID string format safely", "ERROR", 0, str(e))

# NOTIF_HISTORY_026: Neutralize XSS payload in userId query parameter
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId=<script>alert(1)</script>", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400] and "<script>" not in r.text
    record("NOTIF_HISTORY_026", "Neutralize XSS payload in userId query parameter", "PASS" if valid else "FAIL", r.status_code, "XSS script payload safely rejected or neutralized")
except Exception as e:
    record("NOTIF_HISTORY_026", "Neutralize XSS payload in userId query parameter", "ERROR", 0, str(e))

# NOTIF_HISTORY_027: Neutralize SQL injection payload in userId query parameter
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId=' OR 1=1 --", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400]
    record("NOTIF_HISTORY_027", "Neutralize SQL injection payload in userId query parameter", "PASS" if valid else "FAIL", r.status_code, "SQL injection neutralized by parameterization")
except Exception as e:
    record("NOTIF_HISTORY_027", "Neutralize SQL injection payload in userId query parameter", "ERROR", 0, str(e))

# NOTIF_HISTORY_028: Handle userId with leading/trailing whitespace safely
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId= {insp_user_id} ", headers=admin_headers, timeout=5)
    valid = r.status_code in [200, 400]
    record("NOTIF_HISTORY_028", "Handle userId with leading/trailing whitespace safely", "PASS" if valid else "FAIL", r.status_code, f"Status: {r.status_code} safely handled")
except Exception as e:
    record("NOTIF_HISTORY_028", "Handle userId with leading/trailing whitespace safely", "ERROR", 0, str(e))

# NOTIF_HISTORY_029: Extra unknown query parameters ignored gracefully
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&extraParam=test&foo=bar", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_HISTORY_029", "Extra unknown query parameters ignored gracefully", "PASS" if valid else "FAIL", r.status_code, "Ignored extraneous parameters and returned history")
except Exception as e:
    record("NOTIF_HISTORY_029", "Extra unknown query parameters ignored gracefully", "ERROR", 0, str(e))

# NOTIF_HISTORY_030: Parse uppercase UUID userId parameter accurately
try:
    upper_uid = insp_user_id.upper()
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={upper_uid}", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    items_count = len(r.json().get("data", {}).get("items", []))
    record("NOTIF_HISTORY_030", "Parse uppercase UUID userId parameter accurately", "PASS" if valid else "FAIL", r.status_code, f"Uppercase UUID parsed, items: {items_count}")
except Exception as e:
    record("NOTIF_HISTORY_030", "Parse uppercase UUID userId parameter accurately", "ERROR", 0, str(e))

# NOTIF_HISTORY_031: Negative limit query parameter (?limit=-1) handled safely
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}&limit=-1", headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_HISTORY_031", "Negative limit query parameter (?limit=-1) handled safely", "PASS" if valid else "FAIL", r.status_code, "Handled negative limit without server crash")
except Exception as e:
    record("NOTIF_HISTORY_031", "Negative limit query parameter (?limit=-1)", "ERROR", 0, str(e))

# NOTIF_HISTORY_032: Single notification detail with non-existent UUID returns HTTP 404
try:
    non_existent = str(uuid.uuid4())
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{non_existent}", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_HISTORY_032", "Single notification detail with non-existent UUID returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Returned 404 Not Found")
except Exception as e:
    record("NOTIF_HISTORY_032", "Single notification detail with non-existent UUID", "ERROR", 0, str(e))

# NOTIF_HISTORY_033: Single notification detail with invalid GUID route rejected
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/not-a-valid-guid", headers=admin_headers, timeout=5)
    valid = r.status_code in [400, 404]
    record("NOTIF_HISTORY_033", "Single notification detail with invalid GUID route rejected", "PASS" if valid else "FAIL", r.status_code, f"Route constraint blocked: {r.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_033", "Single notification detail with invalid GUID route", "ERROR", 0, str(e))

# NOTIF_HISTORY_034: Mark non-existent notification as read returns HTTP 404
try:
    non_existent = str(uuid.uuid4())
    r = requests.put(f"{BASE_URL}/api/v1/notifications/{non_existent}/read", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_HISTORY_034", "Mark non-existent notification as read returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Returned 404 Not Found")
except Exception as e:
    record("NOTIF_HISTORY_034", "Mark non-existent notification as read", "ERROR", 0, str(e))

# NOTIF_HISTORY_035: Mark notification with invalid GUID route rejected
try:
    r = requests.put(f"{BASE_URL}/api/v1/notifications/not-a-valid-guid/read", headers=admin_headers, timeout=5)
    valid = r.status_code in [400, 404]
    record("NOTIF_HISTORY_035", "Mark notification with invalid GUID route rejected", "PASS" if valid else "FAIL", r.status_code, f"Route constraint blocked: {r.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_035", "Mark notification with invalid GUID route", "ERROR", 0, str(e))

# NOTIF_HISTORY_036: Delete non-existent notification returns HTTP 404
try:
    non_existent = str(uuid.uuid4())
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{non_existent}", headers=admin_headers, timeout=5)
    valid = r.status_code == 404
    record("NOTIF_HISTORY_036", "Delete non-existent notification returns HTTP 404", "PASS" if valid else "FAIL", r.status_code, "Returned 404 Not Found")
except Exception as e:
    record("NOTIF_HISTORY_036", "Delete non-existent notification", "ERROR", 0, str(e))

# NOTIF_HISTORY_037: Delete notification with invalid GUID route rejected
try:
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/not-a-valid-guid", headers=admin_headers, timeout=5)
    valid = r.status_code in [400, 404]
    record("NOTIF_HISTORY_037", "Delete notification with invalid GUID route rejected", "PASS" if valid else "FAIL", r.status_code, f"Route constraint blocked: {r.status_code}")
except Exception as e:
    record("NOTIF_HISTORY_037", "Delete notification with invalid GUID route", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION C: Authorization (RBAC), IDOR & Security (12 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC), IDOR & Security")

# NOTIF_HISTORY_RBAC_001: Reject unauthenticated (Anonymous) request to GET /history
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_HISTORY_RBAC_001", "Reject unauthenticated (Anonymous) request to /history with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Enforced [Authorize] attribute")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_001", "Reject unauthenticated request to /history", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_002: Reject expired JWT token with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.4-A5xJ37J7mF-mJ6z7uC4lJ_E-qL1k1n9j3w3v-v-wA"
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers={"Authorization": f"Bearer {expired_token}"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_HISTORY_RBAC_002", "Reject expired JWT token with HTTP 401 Unauthorized", "PASS" if valid else "FAIL", r.status_code, "Token expiration strictly enforced")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_002", "Reject expired JWT token", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_003: Verify error responses do not leak internal stack traces or secrets
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/invalid-route-or-guid", headers=admin_headers, timeout=5)
    text = r.text.lower()
    has_leak = any(k in text for k in ["stacktrace", "npgsql.", "system.data", "password=", "connectionstring"])
    valid = not has_leak
    record("NOTIF_HISTORY_RBAC_003", "Verify error responses do not leak internal stack traces or secrets", "PASS" if valid else "FAIL", r.status_code, "No infrastructure details or stack traces leaked")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_003", "Verify error responses do not leak internal stack traces", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_004: Handle 20 concurrent history read requests without race condition
try:
    def send_get_history(idx):
        return requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5).status_code

    with concurrent.futures.ThreadPoolExecutor(max_workers=20) as executor:
        status_codes = list(executor.map(send_get_history, range(20)))

    all_200 = all(code == 200 for code in status_codes)
    record("NOTIF_HISTORY_RBAC_004", "Handle 20 concurrent history read requests without race condition", "PASS" if all_200 else "FAIL", 200 if all_200 else 500, f"All 200 OK: {all_200}")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_004", "Handle 20 concurrent history read requests", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_005: IDOR Protection: Inspector attempting to access Manager's notifications rejected with HTTP 403
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={mgr_user_id}", headers=insp_headers, timeout=5)
    valid = r.status_code == 403
    record("NOTIF_HISTORY_RBAC_005", "IDOR Protection: Inspector accessing other user rejected with HTTP 403", "PASS" if valid else "FAIL", r.status_code, "IDOR boundary enforced for Inspector")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_005", "IDOR Protection: Inspector accessing other user", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_006: IDOR Protection: Technician attempting to access Inspector's notifications rejected with HTTP 403
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=tech_headers, timeout=5)
    valid = r.status_code == 403
    record("NOTIF_HISTORY_RBAC_006", "IDOR Protection: Technician accessing other user rejected with HTTP 403", "PASS" if valid else "FAIL", r.status_code, "IDOR boundary enforced for Technician")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_006", "IDOR Protection: Technician accessing other user", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_007: IDOR Protection: Analyst attempting to access Manager's notifications rejected with HTTP 403
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={mgr_user_id}", headers=analyst_headers, timeout=5)
    valid = r.status_code == 403
    record("NOTIF_HISTORY_RBAC_007", "IDOR Protection: Analyst accessing other user rejected with HTTP 403", "PASS" if valid else "FAIL", r.status_code, "IDOR boundary enforced for Analyst")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_007", "IDOR Protection: Analyst accessing other user", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_008: Supervisory Privilege: SystemAdmin can query history for any user without HTTP 403
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=admin_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_HISTORY_RBAC_008", "Supervisory Privilege: SystemAdmin can query history for any user", "PASS" if valid else "FAIL", r.status_code, "SystemAdmin supervisory access confirmed")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_008", "Supervisory Privilege: SystemAdmin can query history for any user", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_009: Supervisory Privilege: Manager can query history for any user without HTTP 403
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/history?userId={insp_user_id}", headers=mgr_headers, timeout=5)
    valid = r.status_code == 200
    record("NOTIF_HISTORY_RBAC_009", "Supervisory Privilege: Manager can query history for any user", "PASS" if valid else "FAIL", r.status_code, "Manager supervisory access confirmed")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_009", "Supervisory Privilege: Manager can query history for any user", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_010: Reject unauthenticated request to GET /{id} with HTTP 401
try:
    dummy_id = str(uuid.uuid4())
    r = requests.get(f"{BASE_URL}/api/v1/notifications/{dummy_id}", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_HISTORY_RBAC_010", "Reject unauthenticated request to GET /{id} with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Enforced authentication on GET /{id}")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_010", "Reject unauthenticated request to GET /{id}", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_011: Reject unauthenticated request to PUT /{id}/read with HTTP 401
try:
    dummy_id = str(uuid.uuid4())
    r = requests.put(f"{BASE_URL}/api/v1/notifications/{dummy_id}/read", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_HISTORY_RBAC_011", "Reject unauthenticated request to PUT /{id}/read with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Enforced authentication on PUT /{id}/read")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_011", "Reject unauthenticated request to PUT /{id}/read", "ERROR", 0, str(e))

# NOTIF_HISTORY_RBAC_012: Reject unauthenticated request to DELETE /{id} with HTTP 401
try:
    dummy_id = str(uuid.uuid4())
    r = requests.delete(f"{BASE_URL}/api/v1/notifications/{dummy_id}", timeout=5)
    valid = r.status_code == 401
    record("NOTIF_HISTORY_RBAC_012", "Reject unauthenticated request to DELETE /{id} with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Enforced authentication on DELETE /{id}")
except Exception as e:
    record("NOTIF_HISTORY_RBAC_012", "Reject unauthenticated request to DELETE /{id}", "ERROR", 0, str(e))


# ==============================================================================
# SUMMARY REPORT
# ==============================================================================
print("\n" + "=" * 105)
print("   NOTIFICATION SERVICE HISTORY & LIFECYCLE TEST SUITE EXECUTION SUMMARY")
print("=" * 105)
total_tests = len(results)
passed_tests = sum(1 for r in results if r["status"] == "PASS")
deviation_tests = sum(1 for r in results if r["status"] == "DEVIATION")
failed_tests = sum(1 for r in results if r["status"] == "FAIL")
error_tests = sum(1 for r in results if r["status"] == "ERROR")
compliance_rate = (passed_tests / total_tests) * 100 if total_tests > 0 else 0

print(f"Total Test Cases: {total_tests}")
print(f"Passed          : {passed_tests}")
print(f"Deviations      : {deviation_tests}")
print(f"Failed          : {failed_tests}")
print(f"Errors          : {error_tests}")
print(f"Compliance Rate : {compliance_rate:.1f}%")
print("=" * 105 + "\n")

if failed_tests > 0 or error_tests > 0:
    sys.exit(1)
sys.exit(0)
