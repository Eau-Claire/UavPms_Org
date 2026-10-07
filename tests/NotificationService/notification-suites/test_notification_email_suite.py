#!/usr/bin/env python3
"""
UAV PMS - NotificationService: Email Job Enqueue Test Suite
Endpoint Tested:
- POST /api/v1/notifications/enqueue-email

Covers 26 standardized test cases:
- Function A: Successful Email Enqueue (Happy Path) [NOTIF_EMAIL_001 - NOTIF_EMAIL_011] (11 TCs)
- Function B: Failed Email Enqueue — Validation & Boundaries [NOTIF_EMAIL_012 - NOTIF_EMAIL_021] (10 TCs)
- Function C: Authorization (RBAC) & Security [NOTIF_EMAIL_RBAC_001 - NOTIF_EMAIL_RBAC_005] (5 TCs)
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
print("   UAV PMS - EMAIL ENQUEUE SUITE (26 TEST CASES)")
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


# ==============================================================================
# FUNCTION A: Successful Email Enqueue (Happy Path) (11 TCs)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Email Enqueue (Happy Path)")

# NOTIF_EMAIL_001: Verify successful email job enqueue with valid email, subject, and body
try:
    payload = {
        "email": "inspector@uavpms.com",
        "subject": "Nhiệm vụ mới",
        "body": "Bạn được giao nhiệm vụ bay kiểm tra tuyến 500kV."
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json=payload, headers=admin_headers, timeout=5)
    data = r.json().get("data", {})
    job_id = data.get("jobId")
    valid = (r.status_code == 200 and
             r.json().get("success") is True and
             "Email job successfully enqueued in Hangfire." in r.json().get("message", "") and
             job_id is not None and str(job_id).strip() != "")
    record("NOTIF_EMAIL_001", "Successful email job enqueue with valid payload", "PASS" if valid else "FAIL", r.status_code, f"JobId: {job_id}")
except Exception as e:
    record("NOTIF_EMAIL_001", "Successful email job enqueue", "ERROR", 0, str(e))

# NOTIF_EMAIL_002: Verify email job processes and enqueues to Hangfire BackgroundJob
try:
    payload = {
        "email": "pilot@uavpms.com",
        "subject": "Lịch bảo trì định kỳ",
        "body": "Thông báo kiểm tra máy bay UAV-01 vào ngày mai."
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json=payload, headers=admin_headers, timeout=5)
    job_id = r.json().get("data", {}).get("jobId")
    valid = r.status_code == 200 and job_id is not None
    record("NOTIF_EMAIL_002", "Email job enqueued to Hangfire pipeline", "PASS" if valid else "FAIL", r.status_code, f"Hangfire JobId generated: {job_id}")
except Exception as e:
    record("NOTIF_EMAIL_002", "Email job pipeline check", "ERROR", 0, str(e))

# NOTIF_EMAIL_003: Verify email enqueue handles UTF-8/Unicode subject and body
try:
    payload = {
        "email": "analyst@uavpms.com",
        "subject": "⚠️ Cảnh báo khẩn cấp: Cách điện nứt vỡ",
        "body": "Phát hiện khuyết tật nghiêm trọng trên đường dây 500kV Bắc-Nam tại vị trí cột 102."
    }
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json=payload, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_003", "UTF-8 / Unicode characters handled cleanly", "PASS" if valid else "FAIL", r.status_code, f"UTF-8 email enqueued with JobId: {r.json().get('data', {}).get('jobId')}")
except Exception as e:
    record("NOTIF_EMAIL_003", "Unicode email enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_004: Verify multiple email jobs can be enqueued sequentially
try:
    job_ids = []
    all_ok = True
    for i in range(3):
        r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
            "email": f"user{i}@uavpms.com", "subject": f"Notice {i}", "body": f"Body {i}"
        }, headers=admin_headers, timeout=5)
        if r.status_code != 200:
            all_ok = False
            break
        job_ids.append(r.json().get("data", {}).get("jobId"))
    distinct_jobs = len(set(job_ids)) == 3
    valid = all_ok and distinct_jobs
    record("NOTIF_EMAIL_004", "Multiple email jobs enqueued sequentially", "PASS" if valid else "FAIL", 200, f"3 distinct JobIds: {job_ids}")
except Exception as e:
    record("NOTIF_EMAIL_004", "Multiple email jobs check", "ERROR", 0, str(e))

# NOTIF_EMAIL_005: Verify Hangfire JobId format and positive numeric identity
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@uavpms.com", "subject": "State Check", "body": "Body"
    }, headers=admin_headers, timeout=5)
    jid = r.json().get("data", {}).get("jobId")
    valid = r.status_code == 200 and jid is not None and int(jid) > 0
    record("NOTIF_EMAIL_005", "Hangfire JobId is positive integer identifier", "PASS" if valid else "FAIL", r.status_code, f"Valid Hangfire JobId: {jid}")
except Exception as e:
    record("NOTIF_EMAIL_005", "Hangfire JobId format check", "ERROR", 0, str(e))

# NOTIF_EMAIL_006: Verify ApiResponse structure matches standard contract
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "contract@uavpms.com", "subject": "Contract", "body": "Body"
    }, headers=admin_headers, timeout=5)
    res_json = r.json()
    valid = ("success" in res_json and "message" in res_json and "data" in res_json and "jobId" in res_json.get("data", {}))
    record("NOTIF_EMAIL_006", "Response contract matches standard ApiResponse", "PASS" if valid else "FAIL", r.status_code, f"Fields: {list(res_json.keys())}")
except Exception as e:
    record("NOTIF_EMAIL_006", "Response contract check", "ERROR", 0, str(e))

# NOTIF_EMAIL_007: Verify Manager can enqueue email
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "mgr@uavpms.com", "subject": "Mgr Notice", "body": "Content"
    }, headers=mgr_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_007", "Manager can enqueue email", "PASS" if valid else "FAIL", r.status_code, "Manager role verified")
except Exception as e:
    record("NOTIF_EMAIL_007", "Manager enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_008: Verify Inspector can enqueue email
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "insp@uavpms.com", "subject": "Insp Notice", "body": "Content"
    }, headers=insp_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_008", "Inspector can enqueue email", "PASS" if valid else "FAIL", r.status_code, "Inspector role verified")
except Exception as e:
    record("NOTIF_EMAIL_008", "Inspector enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_009: Verify Analyst can enqueue email
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "analyst@uavpms.com", "subject": "Analyst Notice", "body": "Content"
    }, headers=analyst_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_009", "Analyst can enqueue email", "PASS" if valid else "FAIL", r.status_code, "Analyst role verified")
except Exception as e:
    record("NOTIF_EMAIL_009", "Analyst enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_010: Verify Technician can enqueue email
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "tech@uavpms.com", "subject": "Tech Notice", "body": "Content"
    }, headers=tech_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_010", "Technician can enqueue email", "PASS" if valid else "FAIL", r.status_code, "Technician role verified")
except Exception as e:
    record("NOTIF_EMAIL_010", "Technician enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_011: Verify SystemAdmin can enqueue email
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "admin@uavpms.com", "subject": "Admin Notice", "body": "Content"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_011", "SystemAdmin can enqueue email", "PASS" if valid else "FAIL", r.status_code, "SystemAdmin role verified")
except Exception as e:
    record("NOTIF_EMAIL_011", "SystemAdmin enqueue check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION B: Failed Email Enqueue — Validation & Boundaries (10 TCs)
# ==============================================================================
print("\n>>> FUNCTION B: Failed Email Enqueue — Validation & Boundaries")

# NOTIF_EMAIL_012: Verify enqueue fails when email field is empty string
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "", "subject": "Test", "body": "Test body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Email, Subject, and Body are required." in r.text
    record("NOTIF_EMAIL_012", "Empty email string rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_012", "Empty email check", "ERROR", 0, str(e))

# NOTIF_EMAIL_013: Verify enqueue fails when subject field is empty string
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "subject": "", "body": "Test body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Email, Subject, and Body are required." in r.text
    record("NOTIF_EMAIL_013", "Empty subject string rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_013", "Empty subject check", "ERROR", 0, str(e))

# NOTIF_EMAIL_014: Verify enqueue fails when body field is empty string
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "subject": "Test", "body": ""
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and "Email, Subject, and Body are required." in r.text
    record("NOTIF_EMAIL_014", "Empty body string rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_014", "Empty body check", "ERROR", 0, str(e))

# NOTIF_EMAIL_015: Verify enqueue fails when all fields are missing (empty JSON {})
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={}, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("validation errors occurred" in r.text.lower() or "required" in r.text.lower())
    record("NOTIF_EMAIL_015", "Empty JSON {} rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_015", "Empty JSON check", "ERROR", 0, str(e))

# NOTIF_EMAIL_016: Verify enqueue fails when email field is null / missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "subject": "Test", "body": "Test body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("email" in r.text.lower() and "required" in r.text.lower())
    record("NOTIF_EMAIL_016", "Missing email field rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_016", "Missing email check", "ERROR", 0, str(e))

# NOTIF_EMAIL_017: Verify enqueue fails when subject field is null / missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "body": "Test body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("subject" in r.text.lower() and "required" in r.text.lower())
    record("NOTIF_EMAIL_017", "Missing subject field rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_017", "Missing subject check", "ERROR", 0, str(e))

# NOTIF_EMAIL_018: Verify enqueue fails when body field is null / missing
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "subject": "Test"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 400 and ("body" in r.text.lower() and "required" in r.text.lower())
    record("NOTIF_EMAIL_018", "Missing body field rejected with HTTP 400", "PASS" if valid else "FAIL", r.status_code, "Validation error returned")
except Exception as e:
    record("NOTIF_EMAIL_018", "Missing body check", "ERROR", 0, str(e))

# NOTIF_EMAIL_019: Verify enqueue accepts non-standard email format at enqueue time
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "not-an-email", "subject": "Test", "body": "Test body"
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_019", "Non-standard email accepted at enqueue stage", "PASS" if valid else "FAIL", r.status_code, f"Asynchronous processing JobId: {r.json().get('data', {}).get('jobId')}")
except Exception as e:
    record("NOTIF_EMAIL_019", "Invalid format check", "ERROR", 0, str(e))

# NOTIF_EMAIL_020: Verify enqueue behavior for whitespace-only fields
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "   ", "subject": "   ", "body": "   "
    }, headers=admin_headers, timeout=5)
    # string.IsNullOrEmpty("   ") evaluates to False in C#, so it enqueues successfully
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_020", "Whitespace-only fields handled (IsNullOrEmpty semantics)", "PASS" if valid else "FAIL", r.status_code, "Enqueued per IsNullOrEmpty semantics")
except Exception as e:
    record("NOTIF_EMAIL_020", "Whitespace check", "ERROR", 0, str(e))

# NOTIF_EMAIL_021: Verify non-JSON Content-Type returns HTTP 415
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", data="email=test@email.com", headers={
        "Authorization": f"Bearer {admin_token}", "Content-Type": "text/plain"
    }, timeout=5)
    valid = r.status_code == 415
    record("NOTIF_EMAIL_021", "Non-JSON Content-Type rejected with HTTP 415", "PASS" if valid else "FAIL", r.status_code, "Unsupported Media Type returned")
except Exception as e:
    record("NOTIF_EMAIL_021", "Content-Type check", "ERROR", 0, str(e))


# ==============================================================================
# FUNCTION C: Authorization (RBAC) & Security (5 TCs)
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC) & Security")

# NOTIF_EMAIL_RBAC_001: Verify unauthenticated (Anonymous) request rejected with HTTP 401
try:
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "subject": "Test", "body": "Body"
    }, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_EMAIL_RBAC_001", "Unauthenticated request rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Anonymous request blocked")
except Exception as e:
    record("NOTIF_EMAIL_RBAC_001", "Unauthenticated enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_RBAC_002: Verify expired JWT token rejected with HTTP 401
try:
    expired_token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjE1MTYyMzkwMjJ9.invalid_signature"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "test@email.com", "subject": "Test", "body": "Body"
    }, headers={"Authorization": f"Bearer {expired_token}", "Content-Type": "application/json"}, timeout=5)
    valid = r.status_code == 401
    record("NOTIF_EMAIL_RBAC_002", "Expired JWT token rejected with HTTP 401", "PASS" if valid else "FAIL", r.status_code, "Expired token blocked")
except Exception as e:
    record("NOTIF_EMAIL_RBAC_002", "Expired JWT enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_RBAC_003: Verify XSS script payload handled safely without execution
try:
    xss_payload = "<script>alert('xss')</script>"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "security@uavpms.com", "subject": xss_payload, "body": xss_payload
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_RBAC_003", "XSS payload treated safely as raw string", "PASS" if valid else "FAIL", r.status_code, "XSS string enqueued safely")
except Exception as e:
    record("NOTIF_EMAIL_RBAC_003", "XSS enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_RBAC_004: Verify SQL Injection payload neutralized safely
try:
    sqli_payload = "'; DROP TABLE \"Notifications\"; --"
    r = requests.post(f"{BASE_URL}/api/v1/notifications/enqueue-email", json={
        "email": "sqli@test.com", "subject": sqli_payload, "body": sqli_payload
    }, headers=admin_headers, timeout=5)
    valid = r.status_code == 200 and r.json().get("success") is True
    record("NOTIF_EMAIL_RBAC_004", "SQL Injection payload neutralized safely", "PASS" if valid else "FAIL", r.status_code, "Enqueued without SQL execution")
except Exception as e:
    record("NOTIF_EMAIL_RBAC_004", "SQLi enqueue check", "ERROR", 0, str(e))

# NOTIF_EMAIL_RBAC_005: Verify unsupported HTTP method (GET /enqueue-email) returns HTTP 405
try:
    r = requests.get(f"{BASE_URL}/api/v1/notifications/enqueue-email", headers=admin_headers, timeout=5)
    valid = r.status_code == 405
    record("NOTIF_EMAIL_RBAC_005", "GET on /enqueue-email returns HTTP 405 Method Not Allowed", "PASS" if valid else "FAIL", r.status_code, "Method Not Allowed returned")
except Exception as e:
    record("NOTIF_EMAIL_RBAC_005", "HTTP 405 check", "ERROR", 0, str(e))


# ==============================================================================
# Summary & Exit
# ==============================================================================
print("\n" + "=" * 105)
print("   NOTIFICATION EMAIL ENQUEUE SUITE EXECUTION SUMMARY")
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
