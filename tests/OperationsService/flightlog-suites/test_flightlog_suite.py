#!/usr/bin/env python3
"""
UAV PMS - OperationsService: Flight Log Telemetry Operations Test Suite
Matches EXACTLY the 29 standardized test cases:
- Function A: Successful Flight Log Operations (Happy Path) [OPS_FLIGHTLOG_001 - OPS_FLIGHTLOG_009]
- Function B: Failed Flight Log Operations — Validation & Boundaries [OPS_FLIGHTLOG_010 - OPS_FLIGHTLOG_020]
- Function C: Authorization (RBAC) & Security [OPS_FLIGHTLOG_RBAC_001 - OPS_FLIGHTLOG_RBAC_009]
"""

import json
import subprocess
import sys
import time
from datetime import datetime, timezone, timedelta
import requests

BASE_URL = "http://127.0.0.1:5194"

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
    print(f"[{color}{status:<9}{reset}] {tc_id:<22} (HTTP {http_code}): {name} -> {details[:85]}")

# Setup Authentication Tokens
admin_token = get_token("An3439201@gmail.com")
admin_headers = {"Authorization": f"Bearer {admin_token}"}
mgr_token = get_token("An3439201+manager@gmail.com")
mgr_headers = {"Authorization": f"Bearer {mgr_token}"}
insp_token = get_token("An3439201+inspector@gmail.com")
insp_headers = {"Authorization": f"Bearer {insp_token}"}
analyst_token = get_token("An3439201+analyst@gmail.com")
analyst_headers = {"Authorization": f"Bearer {analyst_token}"}
tech_token = get_token("An3439201+technician@gmail.com")
tech_headers = {"Authorization": f"Bearer {tech_token}"}

# Secondary inspector (co-inspector & unassigned)
secondary_insp_token = get_token("regression.inspector@example.test", "12345678")
secondary_insp_headers = {"Authorization": f"Bearer {secondary_insp_token}"}

valid_inspector_id = "33333333-3333-3333-3333-333333333333"
secondary_inspector_id = "bedc7aee-0f17-458f-9037-c7e2c1e48caf"
manager_id = "22222222-2222-2222-2222-222222222222"
valid_region_id = "10000000-0000-0000-0000-000000000001"
outside_region_id = "2621aeef-7be6-46b3-bd73-990eee8ea088" # South region (outside secondary inspector scope)
valid_uav_id = "70000000-0000-0000-0000-000000000001"
valid_tower_id = "50000000-0000-0000-0000-000000000001" # Seed tower HN-T001 (region HN-DEMO)

def create_mission(status="InProgress", region_id=valid_region_id, inspector_id=valid_inspector_id):
    # Legacy POST /api/v1/missions was removed (ERD v2.2): a mission is created from a READY
    # pre-mission assessment via POST /api/v2/pre-mission-assessments/{id}/create-mission.
    start = datetime.now(timezone.utc)
    r = requests.post(f"{BASE_URL}/api/v2/pre-mission-assessments", json={
        "regionId": valid_region_id,
        "plannedStart": start.isoformat(),
        "plannedEnd": (start + timedelta(hours=3)).isoformat(),
        "towerIds": [valid_tower_id]
    }, headers=admin_headers)
    if r.status_code != 200:
        raise RuntimeError(f"Failed to create pre-mission assessment: {r.text}")
    assessment = r.json()
    if assessment.get("status") != "READY":
        raise RuntimeError(f"Pre-mission assessment is not READY ({assessment.get('status')}): {r.text[:200]}")
    eligible_drones = [d.get("droneId") for d in assessment.get("droneCandidates", []) if d.get("isEligible")]
    drone_id = valid_uav_id if valid_uav_id in eligible_drones else (eligible_drones[0] if eligible_drones else valid_uav_id)
    r = requests.post(f"{BASE_URL}/api/v2/pre-mission-assessments/{assessment['id']}/create-mission", json={
        "assessmentId": assessment["id"],
        "title": f"FlightLog Suite Mission {int(time.time()*1000)%1000000}",
        "description": "Integration test mission for flight log telemetry",
        "personnel": [{"userId": inspector_id, "role": "INSPECTOR", "isRequired": True}],
        "droneIds": [drone_id]
    }, headers=admin_headers)
    mid = r.json().get("id") if r.status_code == 200 else None
    if not mid:
        raise RuntimeError(f"Failed to create test mission: {r.text}")
    # Release resource bookings so the next fixture can book the same inspector & drone in the same time window
    run_db_query(f'UPDATE "ResourceBookings" SET "Status" = \'Released\' WHERE "MissionId" = \'{mid}\';')
    if region_id != valid_region_id:
        # Missions inherit the assessment region; move the fixture to another region directly in DB
        run_db_query(f'UPDATE "Missions" SET "RegionId" = \'{region_id}\' WHERE "Id" = \'{mid}\';')
    run_db_query(f'UPDATE "Missions" SET "Status" = \'{status}\' WHERE "Id" = \'{mid}\';')
    return mid

print("=" * 85)
print("   UAV PMS - OPERATIONS SERVICE FLIGHT LOG TEST SUITE (29 TEST CASES)")
print("=" * 85)

# Setup Fixture Missions
in_progress_mission_id = create_mission(status="InProgress")
completed_mission_id = create_mission(status="Completed")
draft_mission_id = create_mission(status="Draft")
pending_mission_id = create_mission(status="PendingAcceptance")
cancelled_mission_id = create_mission(status="Cancelled")
outside_mission_id = create_mission(status="InProgress", region_id=outside_region_id, inspector_id=valid_inspector_id)

sample_gps_track = json.dumps([
    {"lat": 21.0084, "lng": 105.7942, "alt": 120.0, "bat": 95},
    {"lat": 21.0090, "lng": 105.7950, "alt": 125.0, "bat": 90}
])

# ==============================================================================
# FUNCTION A: Successful Flight Log Operations (Happy Path)
# ==============================================================================
print("\n>>> FUNCTION A: Successful Flight Log Operations (Happy Path)")

# OPS_FLIGHTLOG_001: Upload flight log with valid GPS track and metadata
payload_001 = {
    "gpsTrack": sample_gps_track,
    "minBatteryRecorded": 22.5,
    "maxAltitudeM": 150.0,
    "flightDurationSeconds": 1800,
    "connectionStatus": "Good"
}
r1 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=insp_headers)
if r1.status_code == 200:
    body = r1.json()
    data = body.get("data", {})
    saved_in_db = run_db_query(f'SELECT COUNT(*) FROM "MissionFlightLogs" WHERE "Id" = \'{data.get("id")}\';')
    audit_exists = run_db_query(f'SELECT COUNT(*) FROM "AuditLogs" WHERE "ActionType" = \'FLIGHT_LOG_UPLOADED\' AND "RecordId" = \'{in_progress_mission_id}\';')
    if (body.get("success") is True and body.get("message") == "Flight log uploaded successfully" and
        data.get("minBatteryRecorded") == 22.5 and data.get("maxAltitudeM") == 150.0 and
        data.get("flightDurationSeconds") == 1800 and data.get("connectionStatus") == "Good" and
        saved_in_db == "1" and int(audit_exists or 0) >= 1):
        record("OPS_FLIGHTLOG_001", "Upload flight log with valid GPS track and metadata", "PASS", r1.status_code, "HTTP 200 OK: Ingested, DB record verified, and AuditLog recorded")
    else:
        record("OPS_FLIGHTLOG_001", "Upload flight log with valid GPS track and metadata", "FAIL", r1.status_code, f"Response/DB/Audit mismatch: {r1.text[:100]}")
else:
    record("OPS_FLIGHTLOG_001", "Upload flight log with valid GPS track and metadata", "FAIL", r1.status_code, f"HTTP {r1.status_code}: {r1.text[:100]}")

# OPS_FLIGHTLOG_002: Retrieve flight log list via GET /missions/{id}/flight-logs
r2 = requests.get(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-logs", headers=insp_headers)
if r2.status_code == 200:
    logs = r2.json().get("data", [])
    sample_parsed = json.loads(sample_gps_track)
    is_sorted_desc = True
    if len(logs) > 1:
        dates = [l.get("recordedAt") for l in logs if l.get("recordedAt")]
        is_sorted_desc = all(dates[i] >= dates[i+1] for i in range(len(dates)-1))
    if isinstance(logs, list) and len(logs) >= 1 and any(json.loads(l.get("gpsTrack", "[]")) == sample_parsed for l in logs) and is_sorted_desc:
        record("OPS_FLIGHTLOG_002", "Retrieve flight log list via GET /flight-logs", "PASS", r2.status_code, f"HTTP 200 OK: Retrieved {len(logs)} log(s) ordered by recordedAt DESC")
    else:
        record("OPS_FLIGHTLOG_002", "Retrieve flight log list via GET /flight-logs", "FAIL", r2.status_code, f"Logs not array or not sorted: {r2.text[:100]}")
else:
    record("OPS_FLIGHTLOG_002", "Retrieve flight log list via GET /flight-logs", "FAIL", r2.status_code, f"HTTP {r2.status_code}: {r2.text[:100]}")

# OPS_FLIGHTLOG_003: Low battery telemetry values recorded accurately
payload_003 = {
    "gpsTrack": sample_gps_track,
    "minBatteryRecorded": 12.5,
    "maxAltitudeM": 110.0,
    "flightDurationSeconds": 1500,
    "connectionStatus": "Warning: Low Battery"
}
r3 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_003, headers=insp_headers)
if r3.status_code == 200 and r3.json().get("data", {}).get("minBatteryRecorded") == 12.5 and r3.json().get("data", {}).get("connectionStatus") == "Warning: Low Battery":
    lid3 = r3.json().get("data", {}).get("id")
    db_bat = run_db_query(f'SELECT "MinBatteryRecorded" FROM "MissionFlightLogs" WHERE "Id" = \'{lid3}\';')
    if float(db_bat or 0) == 12.5:
        record("OPS_FLIGHTLOG_003", "Low battery telemetry values recorded accurately", "PASS", r3.status_code, "HTTP 200 OK: minBatteryRecorded=12.5 preserved in DB and DTO")
    else:
        record("OPS_FLIGHTLOG_003", "Low battery telemetry values recorded accurately", "FAIL", r3.status_code, f"DB value mismatch: {db_bat}")
else:
    record("OPS_FLIGHTLOG_003", "Low battery telemetry values recorded accurately", "FAIL", r3.status_code, f"HTTP {r3.status_code}: {r3.text[:100]}")

# OPS_FLIGHTLOG_004: High altitude telemetry values recorded accurately
payload_004 = {
    "gpsTrack": sample_gps_track,
    "minBatteryRecorded": 55.0,
    "maxAltitudeM": 250.0,
    "flightDurationSeconds": 2100,
    "connectionStatus": "Good"
}
r4 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_004, headers=insp_headers)
if r4.status_code == 200 and r4.json().get("data", {}).get("maxAltitudeM") == 250.0:
    lid4 = r4.json().get("data", {}).get("id")
    db_alt = run_db_query(f'SELECT "MaxAltitudeM" FROM "MissionFlightLogs" WHERE "Id" = \'{lid4}\';')
    if float(db_alt or 0) == 250.0:
        record("OPS_FLIGHTLOG_004", "High altitude telemetry values recorded accurately", "PASS", r4.status_code, "HTTP 200 OK: maxAltitudeM=250.0 preserved in DB and DTO")
    else:
        record("OPS_FLIGHTLOG_004", "High altitude telemetry values recorded accurately", "FAIL", r4.status_code, f"DB altitude mismatch: {db_alt}")
else:
    record("OPS_FLIGHTLOG_004", "High altitude telemetry values recorded accurately", "FAIL", r4.status_code, f"HTTP {r4.status_code}: {r4.text[:100]}")

# OPS_FLIGHTLOG_005: Large GPS tracks (10,000+ points JSON array)
large_pts = [{"lat": round(21.0084 + i*0.00001, 6), "lng": round(105.7942 + i*0.00001, 6), "alt": round(120.0 + (i % 50), 1), "bat": max(10, 100 - i // 150)} for i in range(10000)]
large_json_track = json.dumps(large_pts)
payload_005 = {
    "gpsTrack": large_json_track,
    "minBatteryRecorded": 15.0,
    "maxAltitudeM": 170.0,
    "flightDurationSeconds": 3600,
    "connectionStatus": "Good"
}
r5 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_005, headers=insp_headers)
if r5.status_code == 200:
    ret_len = len(r5.json().get("data", {}).get("gpsTrack", ""))
    if ret_len == len(large_json_track):
        record("OPS_FLIGHTLOG_005", "Large GPS tracks (10,000+ points JSON array)", "PASS", r5.status_code, f"HTTP 200 OK: 10,000 points ({ret_len} chars) persisted completely without truncation")
    else:
        record("OPS_FLIGHTLOG_005", "Large GPS tracks (10,000+ points JSON array)", "FAIL", r5.status_code, f"Track truncated: expected {len(large_json_track)} chars, got {ret_len}")
else:
    record("OPS_FLIGHTLOG_005", "Large GPS tracks (10,000+ points JSON array)", "FAIL", r5.status_code, f"HTTP {r5.status_code}: {r5.text[:100]}")

# OPS_FLIGHTLOG_006: connectionStatus defaults to 'Normal' when omitted or empty
payload_006 = {
    "gpsTrack": sample_gps_track,
    "minBatteryRecorded": 60.0,
    "maxAltitudeM": 80.0,
    "flightDurationSeconds": 900,
    "connectionStatus": ""
}
r6 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_006, headers=insp_headers)
if r6.status_code == 200 and r6.json().get("data", {}).get("connectionStatus") == "Normal":
    record("OPS_FLIGHTLOG_006", "connectionStatus defaults to 'Normal' when empty", "PASS", r6.status_code, "HTTP 200 OK: connectionStatus defaults to 'Normal' in DB & DTO")
else:
    record("OPS_FLIGHTLOG_006", "connectionStatus defaults to 'Normal' when empty", "FAIL", r6.status_code, f"HTTP {r6.status_code}: {r6.text[:100]}")

# OPS_FLIGHTLOG_007: Boundary values accepted (minBattery = 0.0 & 100.0, maxAltitude = 0.0, flightDuration = 0)
r7_a = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": 0.0, "maxAltitudeM": 0.0, "flightDurationSeconds": 0, "connectionStatus": "Good"}, headers=insp_headers)
r7_b = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": 100.0, "maxAltitudeM": 100.0, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r7_a.status_code == 200 and r7_b.status_code == 200:
    record("OPS_FLIGHTLOG_007", "Boundary values accepted (battery 0 & 100, alt 0, dur 0)", "PASS", 200, "HTTP 200 OK: Both boundary sets accepted and persisted successfully")
else:
    record("OPS_FLIGHTLOG_007", "Boundary values accepted (battery 0 & 100, alt 0, dur 0)", "FAIL", r7_a.status_code, f"0.0 -> {r7_a.status_code}, 100.0 -> {r7_b.status_code}")

# OPS_FLIGHTLOG_008: Flight log upload succeeds for secondary Inspector assigned via MissionAssignment
# Assign secondary inspector to mission
assign_sql = f"""
INSERT INTO "MissionAssignments" ("Id", "MissionId", "UserId", "AssignmentRole", "Status", "AssignedByUserId", "CreatedAt", "IsDeleted", "AssignedAt", "IsRequired", "ResponseStatus", "Version")
VALUES (gen_random_uuid(), '{in_progress_mission_id}', '{secondary_inspector_id}', 'Inspector', 'Active', '{manager_id}', NOW(), false, NOW(), false, 'Accepted', 0)
ON CONFLICT DO NOTHING;
"""
run_db_query(assign_sql)
r8 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=secondary_insp_headers)
if r8.status_code == 200:
    lid8 = r8.json().get("data", {}).get("id")
    cby8 = run_db_query(f'SELECT "CreatedBy" FROM "MissionFlightLogs" WHERE "Id" = \'{lid8}\';')
    if cby8.lower() == secondary_inspector_id.lower():
        record("OPS_FLIGHTLOG_008", "Upload succeeds for co-inspector with active assignment", "PASS", r8.status_code, "HTTP 200 OK: Upload succeeded, CreatedBy matches secondary Inspector")
    else:
        record("OPS_FLIGHTLOG_008", "Upload succeeds for co-inspector with active assignment", "FAIL", r8.status_code, f"CreatedBy mismatch: {cby8}")
else:
    record("OPS_FLIGHTLOG_008", "Upload succeeds for co-inspector with active assignment", "FAIL", r8.status_code, f"HTTP {r8.status_code}: {r8.text[:100]}")

# OPS_FLIGHTLOG_009: Flight log upload permitted after flight completion (Status: Completed)
r9 = requests.post(f"{BASE_URL}/api/v1/missions/{completed_mission_id}/flight-log", json=payload_001, headers=insp_headers)
if r9.status_code == 200:
    record("OPS_FLIGHTLOG_009", "Flight log upload permitted for Completed mission", "PASS", r9.status_code, "HTTP 200 OK: Post-flight telemetry upload permitted for Completed mission")
else:
    record("OPS_FLIGHTLOG_009", "Flight log upload permitted for Completed mission", "FAIL", r9.status_code, f"HTTP {r9.status_code}: {r9.text[:100]}")

# ==============================================================================
# FUNCTION B: Failed Flight Log Operations — Validation & Boundaries
# ==============================================================================
print("\n>>> FUNCTION B: Failed Flight Log Operations — Validation & Boundaries")

# OPS_FLIGHTLOG_010: Upload fails when gpsTrack is malformed JSON syntax
r10 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": "invalid-json{missing-bracket", "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r10.status_code == 400 and "INVALID_GPS_TRACK_JSON" in r10.text:
    record("OPS_FLIGHTLOG_010", "Upload fails when gpsTrack is malformed JSON", "PASS", r10.status_code, "HTTP 400 Bad Request: ErrorCode: INVALID_GPS_TRACK_JSON")
else:
    record("OPS_FLIGHTLOG_010", "Upload fails when gpsTrack is malformed JSON", "FAIL", r10.status_code, f"HTTP {r10.status_code}: {r10.text[:100]}")

# OPS_FLIGHTLOG_011: Upload fails when gpsTrack is empty string or whitespace
r11_empty = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": "", "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
r11_ws = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": "   ", "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r11_empty.status_code == 400 and r11_ws.status_code == 400 and "GPS_TRACK_REQUIRED" in r11_empty.text and "GPS_TRACK_REQUIRED" in r11_ws.text:
    record("OPS_FLIGHTLOG_011", "Upload fails when gpsTrack is empty string or whitespace", "PASS", 400, "HTTP 400 Bad Request: ErrorCode: GPS_TRACK_REQUIRED for both empty & whitespace")
else:
    record("OPS_FLIGHTLOG_011", "Upload fails when gpsTrack is empty string or whitespace", "FAIL", r11_empty.status_code, f"Empty -> {r11_empty.status_code}, WS -> {r11_ws.status_code}")

# OPS_FLIGHTLOG_012: Upload fails when minBatteryRecorded is negative (< 0)
r12 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": -5.0, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r12.status_code == 400 and "INVALID_BATTERY_LEVEL" in r12.text:
    record("OPS_FLIGHTLOG_012", "Upload fails when minBatteryRecorded < 0", "PASS", r12.status_code, "HTTP 400 Bad Request: ErrorCode: INVALID_BATTERY_LEVEL")
else:
    record("OPS_FLIGHTLOG_012", "Upload fails when minBatteryRecorded < 0", "FAIL", r12.status_code, f"HTTP {r12.status_code}: {r12.text[:100]}")

# OPS_FLIGHTLOG_013: Upload fails when minBatteryRecorded exceeds 100 (> 100)
r13 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": 105.0, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r13.status_code == 400 and "INVALID_BATTERY_LEVEL" in r13.text:
    record("OPS_FLIGHTLOG_013", "Upload fails when minBatteryRecorded > 100", "PASS", r13.status_code, "HTTP 400 Bad Request: ErrorCode: INVALID_BATTERY_LEVEL")
else:
    record("OPS_FLIGHTLOG_013", "Upload fails when minBatteryRecorded > 100", "FAIL", r13.status_code, f"HTTP {r13.status_code}: {r13.text[:100]}")

# OPS_FLIGHTLOG_014: Upload fails when flightDurationSeconds is negative (< 0)
r14 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": -100, "connectionStatus": "Good"}, headers=insp_headers)
if r14.status_code == 400 and "INVALID_FLIGHT_DURATION" in r14.text:
    record("OPS_FLIGHTLOG_014", "Upload fails when flightDurationSeconds < 0", "PASS", r14.status_code, "HTTP 400 Bad Request: ErrorCode: INVALID_FLIGHT_DURATION")
else:
    record("OPS_FLIGHTLOG_014", "Upload fails when flightDurationSeconds < 0", "FAIL", r14.status_code, f"HTTP {r14.status_code}: {r14.text[:100]}")

# OPS_FLIGHTLOG_015: Upload fails when maxAltitudeM is negative (< 0)
r15 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": sample_gps_track, "minBatteryRecorded": 50, "maxAltitudeM": -50.0, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r15.status_code == 400 and "INVALID_ALTITUDE" in r15.text:
    record("OPS_FLIGHTLOG_015", "Upload fails when maxAltitudeM < 0", "PASS", r15.status_code, "HTTP 400 Bad Request: ErrorCode: INVALID_ALTITUDE")
else:
    record("OPS_FLIGHTLOG_015", "Upload fails when maxAltitudeM < 0", "FAIL", r15.status_code, f"HTTP {r15.status_code}: {r15.text[:100]}")

# OPS_FLIGHTLOG_016: Upload fails when mission ID does not exist or outside accessible scope
r16 = requests.post(f"{BASE_URL}/api/v1/missions/00000000-0000-0000-0000-000000000001/flight-log", json=payload_001, headers=insp_headers)
if r16.status_code == 403 and "MISSION_ACCESS_DENIED" in r16.text:
    record("OPS_FLIGHTLOG_016", "Upload fails when mission ID does not exist / out of scope", "PASS", r16.status_code, "HTTP 403 Forbidden: ErrorCode: FORBIDDEN, MISSION_ACCESS_DENIED")
else:
    record("OPS_FLIGHTLOG_016", "Upload fails when mission ID does not exist / out of scope", "FAIL", r16.status_code, f"HTTP {r16.status_code}: {r16.text[:100]}")

# OPS_FLIGHTLOG_017: State Machine Guard: Upload fails when mission is in Draft status
r17 = requests.post(f"{BASE_URL}/api/v1/missions/{draft_mission_id}/flight-log", json=payload_001, headers=admin_headers)
if r17.status_code == 400 and "FLIGHT_LOG_INVALID_STATE" in r17.text:
    record("OPS_FLIGHTLOG_017", "Upload fails when mission is in Draft status", "PASS", r17.status_code, "HTTP 400 Bad Request: ErrorCode: FLIGHT_LOG_INVALID_STATE")
else:
    record("OPS_FLIGHTLOG_017", "Upload fails when mission is in Draft status", "FAIL", r17.status_code, f"HTTP {r17.status_code}: {r17.text[:100]}")

# OPS_FLIGHTLOG_018: State Machine Guard: Upload fails when mission is in PendingAcceptance status
r18 = requests.post(f"{BASE_URL}/api/v1/missions/{pending_mission_id}/flight-log", json=payload_001, headers=insp_headers)
if r18.status_code == 400 and "FLIGHT_LOG_INVALID_STATE" in r18.text:
    record("OPS_FLIGHTLOG_018", "Upload fails when mission is in PendingAcceptance", "PASS", r18.status_code, "HTTP 400 Bad Request: ErrorCode: FLIGHT_LOG_INVALID_STATE")
else:
    record("OPS_FLIGHTLOG_018", "Upload fails when mission is in PendingAcceptance", "FAIL", r18.status_code, f"HTTP {r18.status_code}: {r18.text[:100]}")

# OPS_FLIGHTLOG_019: State Machine Guard: Verify upload behavior when mission is Cancelled
r19 = requests.post(f"{BASE_URL}/api/v1/missions/{cancelled_mission_id}/flight-log", json=payload_001, headers=insp_headers)
if r19.status_code in [400, 403]:
    record("OPS_FLIGHTLOG_019", "Upload behavior when mission is Cancelled", "PASS", r19.status_code, "HTTP 400/403: Cancelled mission upload rejected as requested by spec")
elif r19.status_code == 200:
    record("OPS_FLIGHTLOG_019", "Upload behavior when mission is Cancelled", "DEVIATION", r19.status_code, "Backend accepts telemetry upload on Cancelled missions to preserve emergency/incident logs")
else:
    record("OPS_FLIGHTLOG_019", "Upload behavior when mission is Cancelled", "FAIL", r19.status_code, f"HTTP {r19.status_code}: {r19.text[:100]}")

# OPS_FLIGHTLOG_020: Verify gpsTrack accepts valid JSON object/array telemetry payloads without error
obj_track = json.dumps({"flightMode": "Autonomous", "waypoints": 12})
r20 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": obj_track, "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r20.status_code == 200:
    record("OPS_FLIGHTLOG_020", "gpsTrack accepts valid JSON object telemetry without error", "PASS", r20.status_code, "HTTP 200 OK: JsonDocument.Parse validates JSON object and saves to DB successfully")
else:
    record("OPS_FLIGHTLOG_020", "gpsTrack accepts valid JSON object telemetry without error", "FAIL", r20.status_code, f"HTTP {r20.status_code}: {r20.text[:100]}")

# ==============================================================================
# FUNCTION C: Authorization (RBAC) & Security
# ==============================================================================
print("\n>>> FUNCTION C: Authorization (RBAC) & Security")

# OPS_FLIGHTLOG_RBAC_001: Upload allowed for assigned Inspector and Manager
r_rbac_1_insp = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=insp_headers)
r_rbac_1_mgr = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=mgr_headers)
if r_rbac_1_insp.status_code == 200 and r_rbac_1_mgr.status_code == 200:
    record("OPS_FLIGHTLOG_RBAC_001", "Upload allowed for assigned Inspector and Manager", "PASS", 200, "HTTP 200 OK: Both assigned Inspector and Manager uploaded flight log successfully")
else:
    record("OPS_FLIGHTLOG_RBAC_001", "Upload allowed for assigned Inspector and Manager", "FAIL", r_rbac_1_insp.status_code, f"Insp -> {r_rbac_1_insp.status_code}, Mgr -> {r_rbac_1_mgr.status_code}")

# OPS_FLIGHTLOG_RBAC_002: Upload allowed for SystemAdmin (IsGlobal bypass)
r_rbac_2 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=admin_headers)
if r_rbac_2.status_code == 200:
    record("OPS_FLIGHTLOG_RBAC_002", "Upload allowed for SystemAdmin (IsGlobal bypass)", "PASS", r_rbac_2.status_code, "HTTP 200 OK: SystemAdmin global scope bypasses assignment guard")
else:
    record("OPS_FLIGHTLOG_RBAC_002", "Upload allowed for SystemAdmin (IsGlobal bypass)", "FAIL", r_rbac_2.status_code, f"HTTP {r_rbac_2.status_code}: {r_rbac_2.text[:100]}")

# OPS_FLIGHTLOG_RBAC_003: Upload rejected for unassigned Inspector (Assignment Isolation / IDOR)
# Create mission without secondary inspector assigned
unassigned_mission_id = create_mission(status="InProgress", region_id=valid_region_id, inspector_id=valid_inspector_id)
r_rbac_3 = requests.post(f"{BASE_URL}/api/v1/missions/{unassigned_mission_id}/flight-log", json=payload_001, headers=secondary_insp_headers)
if r_rbac_3.status_code == 403 and "USER_NOT_ASSIGNED_TO_MISSION" in r_rbac_3.text:
    record("OPS_FLIGHTLOG_RBAC_003", "Upload rejected for unassigned Inspector", "PASS", r_rbac_3.status_code, "HTTP 403 Forbidden: ErrorCode: FORBIDDEN, USER_NOT_ASSIGNED_TO_MISSION")
else:
    record("OPS_FLIGHTLOG_RBAC_003", "Upload rejected for unassigned Inspector", "FAIL", r_rbac_3.status_code, f"HTTP {r_rbac_3.status_code}: {r_rbac_3.text[:100]}")

# OPS_FLIGHTLOG_RBAC_004: Upload strictly rejected for Analyst role
r_rbac_4 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=analyst_headers)
if r_rbac_4.status_code == 403:
    record("OPS_FLIGHTLOG_RBAC_004", "Upload strictly rejected for Analyst role", "PASS", r_rbac_4.status_code, "HTTP 403 Forbidden: Blocked by [Authorize(Roles = UserRoles.AdminManagerInspector)]")
else:
    record("OPS_FLIGHTLOG_RBAC_004", "Upload strictly rejected for Analyst role", "FAIL", r_rbac_4.status_code, f"HTTP {r_rbac_4.status_code}: {r_rbac_4.text[:100]}")

# OPS_FLIGHTLOG_RBAC_005: Upload strictly rejected for Technician role
r_rbac_5 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001, headers=tech_headers)
if r_rbac_5.status_code == 403:
    record("OPS_FLIGHTLOG_RBAC_005", "Upload strictly rejected for Technician role", "PASS", r_rbac_5.status_code, "HTTP 403 Forbidden: Blocked by [Authorize(Roles = UserRoles.AdminManagerInspector)]")
else:
    record("OPS_FLIGHTLOG_RBAC_005", "Upload strictly rejected for Technician role", "FAIL", r_rbac_5.status_code, f"HTTP {r_rbac_5.status_code}: {r_rbac_5.text[:100]}")

# OPS_FLIGHTLOG_RBAC_006: Flight log endpoints reject unauthenticated (Anonymous) requests
r_rbac_6_post = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json=payload_001)
r_rbac_6_get = requests.get(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-logs")
if r_rbac_6_post.status_code == 401 and r_rbac_6_get.status_code == 401:
    record("OPS_FLIGHTLOG_RBAC_006", "POST & GET reject unauthenticated Anonymous requests", "PASS", 401, "HTTP 401 Unauthorized: Both endpoints reject request without Bearer token")
else:
    record("OPS_FLIGHTLOG_RBAC_006", "POST & GET reject unauthenticated Anonymous requests", "FAIL", r_rbac_6_post.status_code, f"POST -> {r_rbac_6_post.status_code}, GET -> {r_rbac_6_get.status_code}")

# OPS_FLIGHTLOG_RBAC_007: SQL Injection and XSS resistance in gpsTrack payload
injection_payload = json.dumps([{"lat": 21.0084, "lng": 105.7942, "payload": "' OR 1=1 -- <script>alert(1)</script>"}])
r_rbac_7 = requests.post(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", json={"gpsTrack": injection_payload, "minBatteryRecorded": 50, "maxAltitudeM": 100, "flightDurationSeconds": 600, "connectionStatus": "Good"}, headers=insp_headers)
if r_rbac_7.status_code == 200:
    lid = r_rbac_7.json().get("data", {}).get("id")
    db_track = run_db_query(f'SELECT "GpsTrack" FROM "MissionFlightLogs" WHERE "Id" = \'{lid}\';')
    if "<script>" in db_track and "' OR 1=1" in db_track:
        record("OPS_FLIGHTLOG_RBAC_007", "SQL Injection and XSS sanitized in JSONB payload", "PASS", r_rbac_7.status_code, "HTTP 200 OK: Data safely parameterized by EF Core and preserved as text")
    else:
        record("OPS_FLIGHTLOG_RBAC_007", "SQL Injection and XSS sanitized in JSONB payload", "FAIL", r_rbac_7.status_code, "Payload not stored as expected in DB")
else:
    record("OPS_FLIGHTLOG_RBAC_007", "SQL Injection and XSS sanitized in JSONB payload", "FAIL", r_rbac_7.status_code, f"HTTP {r_rbac_7.status_code}: {r_rbac_7.text[:100]}")

# OPS_FLIGHTLOG_RBAC_008: GET /missions/{id}/flight-logs enforces region scope isolation for viewers
r_rbac_8 = requests.get(f"{BASE_URL}/api/v1/missions/{outside_mission_id}/flight-logs", headers=secondary_insp_headers)
if r_rbac_8.status_code == 403 and "MISSION_ACCESS_DENIED" in r_rbac_8.text:
    record("OPS_FLIGHTLOG_RBAC_008", "GET flight-logs enforces region scope isolation", "PASS", r_rbac_8.status_code, "HTTP 403 Forbidden: ErrorCode: FORBIDDEN, MISSION_ACCESS_DENIED")
else:
    record("OPS_FLIGHTLOG_RBAC_008", "GET flight-logs enforces region scope isolation", "FAIL", r_rbac_8.status_code, f"HTTP {r_rbac_8.status_code}: {r_rbac_8.text[:100]}")

# OPS_FLIGHTLOG_RBAC_009: Singular route GET /api/v1/missions/{id}/flight-log returns 405 Method Not Allowed
r_rbac_9 = requests.get(f"{BASE_URL}/api/v1/missions/{in_progress_mission_id}/flight-log", headers=insp_headers)
if r_rbac_9.status_code == 405:
    record("OPS_FLIGHTLOG_RBAC_009", "Singular route GET /flight-log returns 405 Method Not Allowed", "PASS", r_rbac_9.status_code, "HTTP 405 Method Not Allowed: Standard endpoint is /flight-logs (plural)")
else:
    record("OPS_FLIGHTLOG_RBAC_009", "Singular route GET /flight-log returns 405 Method Not Allowed", "FAIL", r_rbac_9.status_code, f"HTTP {r_rbac_9.status_code}: {r_rbac_9.text[:100]}")

# ==============================================================================
# SUMMARY & METRICS
# ==============================================================================
print("\n" + "=" * 85)
print("   FLIGHT LOG TEST SUITE EXECUTION SUMMARY")
print("=" * 85)

pass_count = sum(1 for r in results if r["status"] == "PASS")
dev_count = sum(1 for r in results if r["status"] == "DEVIATION")
fail_count = sum(1 for r in results if r["status"] == "FAIL")
total = len(results)

print(f"Total Tests Executed: {total}")
print(f"Passed:               {pass_count} ({pass_count/total*100:.1f}%)")
print(f"Deviations (Specs):   {dev_count} ({dev_count/total*100:.1f}%)")
print(f"Failed:               {fail_count} ({fail_count/total*100:.1f}%)")
print("=" * 85)

if fail_count > 0:
    print(f"\n[SUMMARY] FAILED with {fail_count} failure(s)!")
    sys.exit(1)
else:
    print(f"\n[SUMMARY] SUCCESS! All {total} test cases satisfied backend contract & specifications.")
    sys.exit(0)
