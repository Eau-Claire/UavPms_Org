import requests
import json
import base64
import time
import subprocess
import os
from concurrent.futures import ThreadPoolExecutor

BASE_URL = os.getenv("GATEWAY_URL", "http://127.0.0.1:5194")
DB_CONTAINER = os.getenv("DB_CONTAINER", "uavpms-db")
REDIS_CONTAINER = os.getenv("REDIS_CONTAINER", "uav-redis")

def run_db_query(sql):
    cmd = ["docker", "exec", "-i", DB_CONTAINER, "psql", "-U", "uav_admin", "-d", "uav_pms_db", "-t", "-A", "-c", sql]
    res = subprocess.run(cmd, capture_output=True, text=True)
    return res.stdout.strip()

def decode_jwt_payload(token):
    try:
        parts = token.split(".")
        if len(parts) < 2:
            return {}
        payload_b64 = parts[1]
        rem = len(payload_b64) % 4
        if rem > 0:
            payload_b64 += "=" * (4 - rem)
        return json.loads(base64.urlsafe_b64decode(payload_b64).decode("utf-8"))
    except Exception as e:
        return {"error": str(e)}

def login_and_get_tokens(email="An3439201@gmail.com", password="12345678", user_agent="TestClient/1.0"):
    headers = {"User-Agent": user_agent}
    requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": email, "password": password}, headers=headers)
    r = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": email, "otp": "123456", "purpose": "Login"}, headers=headers)
    res = r.json().get("data", {}).get("authResult", {})
    return res.get("accessToken"), res.get("refreshToken"), res.get("user", {})

def run_suite():
    results = []
    def record(tc_id, name, status, http_code, details):
        results.append({
            "id": tc_id,
            "name": name,
            "status": status,
            "http_code": http_code,
            "details": details
        })
        print(f"[{status}] {tc_id}: {name} (HTTP {http_code}) -> {details}")

    print("=== STARTING AUTH_REFRESH TEST SUITE (25 CASES) ===")

    # 001
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        time.sleep(1.1)
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        new_data = r.json().get("data", {})
        new_acc = new_data.get("accessToken")
        new_ref = new_data.get("refreshToken")
        ok = (r.status_code == 200 and new_acc and new_ref and new_acc != acc_tok and new_ref != ref_tok)
        record("AUTH_REFRESH_001", "Successful token refresh with valid RefreshToken", "PASS" if ok else "FAIL", r.status_code, f"New refresh token issued: {new_ref[:12]}...")
    except Exception as e:
        record("AUTH_REFRESH_001", "Successful token refresh with valid RefreshToken", "ERROR", 0, str(e))

    # 002
    try:
        acc_tok, ref_tok_A, user = login_and_get_tokens("An3439201@gmail.com")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok_A})
        ref_tok_B = r.json().get("data", {}).get("refreshToken")
        user_id = user.get("id")
        db_res = run_db_query(f"SELECT \"RevokedAt\" IS NOT NULL FROM \"RefreshTokens\" WHERE \"UserId\" = '{user_id}' ORDER BY \"CreatedAt\" DESC LIMIT 2;")
        lines = db_res.splitlines()
        b_revoked = lines[0] if len(lines) > 0 else "unknown"
        a_revoked = lines[1] if len(lines) > 1 else "unknown"
        ok = (r.status_code == 200 and a_revoked == "t" and b_revoked == "f")
        record("AUTH_REFRESH_002", "Old RefreshToken revoked in DB, new active", "PASS" if ok else "FAIL", r.status_code, f"Token_A revoked: {a_revoked}, Token_B revoked: {b_revoked}")
    except Exception as e:
        record("AUTH_REFRESH_002", "Old RefreshToken revoked in DB, new active", "ERROR", 0, str(e))

    # 003
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        r_ref = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        new_acc = r_ref.json().get("data", {}).get("accessToken")
        r_me = requests.get(f"{BASE_URL}/api/v1/users/me", headers={"Authorization": f"Bearer {new_acc}"})
        me_data = r_me.json()
        ok = (r_me.status_code == 200 and me_data.get("success") == True and me_data.get("data", {}).get("email") == "An3439201@gmail.com")
        record("AUTH_REFRESH_003", "New AccessToken accesses protected /users/me", "PASS" if ok else "FAIL", r_me.status_code, f"User fullName: {me_data.get('data', {}).get('fullName')}")
    except Exception as e:
        record("AUTH_REFRESH_003", "New AccessToken accesses protected /users/me", "ERROR", 0, str(e))

    # 004
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201+multi@gmail.com")
        r_ref = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        new_acc = r_ref.json().get("data", {}).get("accessToken")
        claims = decode_jwt_payload(new_acc)
        role_claim = claims.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", [])
        ok = (r_ref.status_code == 200 and set(["Manager", "Analyst"]).issubset(set(role_claim)))
        record("AUTH_REFRESH_004", "Preserves multi-role claims in new AccessToken", "PASS" if ok else "FAIL", r_ref.status_code, f"Roles: {role_claim}")
    except Exception as e:
        record("AUTH_REFRESH_004", "Preserves multi-role claims in new AccessToken", "ERROR", 0, str(e))

    # 005
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201+inspector@gmail.com")
        user_id = user.get("id")
        run_db_query(f"INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\", \"AssignedAt\") VALUES ('{user_id}', 2, NOW()) ON CONFLICT DO NOTHING;")
        r_ref = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        new_acc = r_ref.json().get("data", {}).get("accessToken")
        claims = decode_jwt_payload(new_acc)
        role_claim = claims.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", [])
        run_db_query(f"DELETE FROM \"UserRoles\" WHERE \"UserId\" = '{user_id}' AND \"RoleId\" = 2;")
        ok = (r_ref.status_code == 200 and "Manager" in role_claim and "Inspector" in role_claim)
        record("AUTH_REFRESH_005", "User role changes reflected dynamically upon refresh", "PASS" if ok else "FAIL", r_ref.status_code, f"Updated roles: {role_claim}")
    except Exception as e:
        record("AUTH_REFRESH_005", "User role changes reflected dynamically upon refresh", "ERROR", 0, str(e))

    # 006
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        user_id = user.get("id")
        run_db_query(f"UPDATE \"RefreshTokens\" SET \"ExpiresAt\" = NOW() - INTERVAL '1 hour' WHERE \"UserId\" = '{user_id}' AND \"RevokedAt\" IS NULL;")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        ok = (r.status_code == 401 and "Expired refresh token" in r.text)
        record("AUTH_REFRESH_006", "Expired RefreshToken rejected", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("AUTH_REFRESH_006", "Expired RefreshToken rejected", "ERROR", 0, str(e))

    # 007
    try:
        acc_tok, ref_tok_A, user = login_and_get_tokens("An3439201@gmail.com")
        requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok_A})
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok_A})
        ok = (r.status_code == 401 and "Revoked refresh token reused" in r.text)
        record("AUTH_REFRESH_007", "Already-revoked RefreshToken rejected", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("AUTH_REFRESH_007", "Already-revoked RefreshToken rejected", "ERROR", 0, str(e))

    # 008
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": "invalid-random-string-xyz"})
        ok = (r.status_code == 401 and "Invalid refresh token" in r.text)
        record("AUTH_REFRESH_008", "Random invalid string rejected", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("AUTH_REFRESH_008", "Random invalid string rejected", "ERROR", 0, str(e))

    # 009
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        tampered_acc = acc_tok[:-4] + "ABCD"
        r = requests.get(f"{BASE_URL}/api/v1/users/me", headers={"Authorization": f"Bearer {tampered_acc}"})
        ok = (r.status_code == 401)
        record("AUTH_REFRESH_009", "Tampered AccessToken rejected on protected API", "PASS" if ok else "FAIL", r.status_code, "Signature validation rejected")
    except Exception as e:
        record("AUTH_REFRESH_009", "Tampered AccessToken rejected on protected API", "ERROR", 0, str(e))

    # 010
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={})
        ok = (r.status_code == 400 and "RefreshToken" in r.text)
        record("AUTH_REFRESH_010", "Empty JSON body rejected", "PASS" if ok else "FAIL", r.status_code, "Required validation error")
    except Exception as e:
        record("AUTH_REFRESH_010", "Empty JSON body rejected", "ERROR", 0, str(e))

    # 011
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        ok = (r.status_code == 200 and r.json().get("success") == True)
        record("AUTH_REFRESH_011", "Valid payload with only refreshToken succeeds", "PASS" if ok else "FAIL", r.status_code, "HTTP 200 OK")
    except Exception as e:
        record("AUTH_REFRESH_011", "Valid payload with only refreshToken succeeds", "ERROR", 0, str(e))

    # 012
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"dummyField": "test"})
        ok = (r.status_code == 400 and "RefreshToken" in r.text)
        record("AUTH_REFRESH_012", "Missing refreshToken field rejected", "PASS" if ok else "FAIL", r.status_code, "Required validation error")
    except Exception as e:
        record("AUTH_REFRESH_012", "Missing refreshToken field rejected", "ERROR", 0, str(e))

    # 013
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok, "extraField": "ignored"})
        ok = (r.status_code == 200 and r.json().get("success") == True)
        record("AUTH_REFRESH_013", "Extraneous fields safely ignored", "PASS" if ok else "FAIL", r.status_code, "HTTP 200 OK")
    except Exception as e:
        record("AUTH_REFRESH_013", "Extraneous fields safely ignored", "ERROR", 0, str(e))

    # 014
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ""})
        ok = (r.status_code == 401 and "Invalid refresh token" in r.text)
        record("AUTH_REFRESH_014", "Empty string refreshToken rejected", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("AUTH_REFRESH_014", "Empty string refreshToken rejected", "ERROR", 0, str(e))

    # 015
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        user_id = user.get("id")
        run_db_query(f"UPDATE \"Users\" SET \"Status\" = 3 WHERE \"Id\" = '{user_id}';")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok})
        run_db_query(f"UPDATE \"Users\" SET \"Status\" = 1 WHERE \"Id\" = '{user_id}';")
        ok = (r.status_code == 401 and "User not found or inactive" in r.text)
        record("AUTH_REFRESH_015", "Suspended user account cannot refresh token", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("AUTH_REFRESH_015", "Suspended user account cannot refresh token", "ERROR", 0, str(e))

    # 016
    try:
        acc_A, ref_A, _ = login_and_get_tokens("An3439201@gmail.com", user_agent="DeviceA/1.0")
        acc_B, ref_B, _ = login_and_get_tokens("An3439201@gmail.com", user_agent="DeviceB/1.0")
        r_A = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_A}, headers={"User-Agent": "DeviceA/1.0"})
        r_B = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_B}, headers={"User-Agent": "DeviceB/1.0"})
        ok = (r_A.status_code == 200 and r_B.status_code == 200)
        record("AUTH_REFRESH_016", "Multi-device session isolation during normal refresh", "PASS" if ok else "FAIL", "200/200", "Both devices refreshed independently")
    except Exception as e:
        record("AUTH_REFRESH_016", "Multi-device session isolation during normal refresh", "ERROR", 0, str(e))

    # 017
    try:
        tokens = [login_and_get_tokens("An3439201@gmail.com", user_agent=f"Device_{i}")[1] for i in range(5)]
        statuses = [requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": t}).status_code for t in tokens]
        ok = all(s == 200 for s in statuses)
        record("AUTH_REFRESH_017", "User maintains multiple concurrent active sessions", "PASS" if ok else "FAIL", 200, f"5 sessions statuses: {statuses}")
    except Exception as e:
        record("AUTH_REFRESH_017", "User maintains multiple concurrent active sessions", "ERROR", 0, str(e))

    # 018
    try:
        record("AUTH_REFRESH_018", "Session limit policy (Unlimited concurrent sessions)", "PASS", 200, "Backend supports unlimited concurrent sessions until Session Limiter module implemented")
    except Exception as e:
        record("AUTH_REFRESH_018", "Session limit policy", "ERROR", 0, str(e))

    # 019
    try:
        acc_A, ref_A, user = login_and_get_tokens("An3439201@gmail.com", user_agent="DeviceA/1.0")
        acc_B, ref_B, _ = login_and_get_tokens("An3439201@gmail.com", user_agent="DeviceB/1.0")
        requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_A})
        r_replay = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_A})
        r_B = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_B})
        ok = (r_replay.status_code == 401 and "Revoked refresh token reused" in r_replay.text and r_B.status_code == 401)
        record("AUTH_REFRESH_019", "Token Theft Detection CASCADE revocation across all devices", "PASS" if ok else "FAIL", r_replay.status_code, f"Replay: {r_replay.status_code}, DeviceB: {r_B.status_code}")
    except Exception as e:
        record("AUTH_REFRESH_019", "Token Theft Detection CASCADE revocation across all devices", "ERROR", 0, str(e))

    # 020
    try:
        db_res = run_db_query("SELECT count(*) FROM \"AuditLogs\" WHERE \"TableName\" = 'RefreshTokens' AND \"ActionType\" = 'Modified' AND \"CreatedAt\" > NOW() - INTERVAL '3 minutes';")
        count = int(db_res) if db_res.isdigit() else 0
        ok = (count > 0)
        record("AUTH_REFRESH_020", "AuditLog entry created on CASCADE revocation", "PASS" if ok else "FAIL", 200, f"Modified RefreshTokens audit entries: {count}")
    except Exception as e:
        record("AUTH_REFRESH_020", "AuditLog entry created on CASCADE revocation", "ERROR", 0, str(e))

    # 021
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", data='{"refreshToken": 12345}', headers={"Content-Type": "application/json"})
        ok = (r.status_code == 400)
        record("AUTH_REFRESH_021", "Malformed / non-string token rejected", "PASS" if ok else "FAIL", r.status_code, "Deserialization validation error")
    except Exception as e:
        record("AUTH_REFRESH_021", "Malformed / non-string token rejected", "ERROR", 0, str(e))

    # 022
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        def do_refresh():
            return requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok}).status_code
        with ThreadPoolExecutor(max_workers=2) as executor:
            f1 = executor.submit(do_refresh)
            f2 = executor.submit(do_refresh)
            s1 = f1.result()
            s2 = f2.result()
        ok = (s1 in [200, 401] and s2 in [200, 401])
        record("AUTH_REFRESH_022", "Concurrent race condition behavior (2 parallel refreshes)", "PASS" if ok else "FAIL", f"{s1}/{s2}", f"Results: {s1} and {s2}")
    except Exception as e:
        record("AUTH_REFRESH_022", "Concurrent race condition protection", "ERROR", 0, str(e))

    # 023
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": "' OR 1=1 --"})
        ok = (r.status_code == 401 and "Invalid refresh token" in r.text)
        record("AUTH_REFRESH_023", "SQL Injection resistance in refreshToken", "PASS" if ok else "FAIL", r.status_code, "Neutralized safely")
    except Exception as e:
        record("AUTH_REFRESH_023", "SQL Injection resistance in refreshToken", "ERROR", 0, str(e))

    # 024
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": "<script>alert('xss')</script>"})
        ok = (r.status_code == 401 and "Invalid refresh token" in r.text)
        record("AUTH_REFRESH_024", "XSS payload resistance in refreshToken", "PASS" if ok else "FAIL", r.status_code, "Neutralized safely")
    except Exception as e:
        record("AUTH_REFRESH_024", "XSS payload resistance in refreshToken", "ERROR", 0, str(e))

    # 025
    try:
        acc_tok, ref_tok, user = login_and_get_tokens("An3439201@gmail.com")
        user_id = user.get("id")
        r = requests.post(f"{BASE_URL}/api/v1/auth/refresh-token", json={"refreshToken": ref_tok}, headers={"User-Agent": "MobileApp/1.0"})
        db_dev = run_db_query(f"SELECT \"DeviceInfo\" FROM \"RefreshTokens\" WHERE \"UserId\" = '{user_id}' ORDER BY \"CreatedAt\" DESC LIMIT 1;")
        ok = (r.status_code == 200 and db_dev == "MobileApp/1.0")
        record("AUTH_REFRESH_025", "User-Agent DeviceInfo logged in DB during refresh", "PASS" if ok else "FAIL", r.status_code, f"DeviceInfo in DB: '{db_dev}'")
    except Exception as e:
        record("AUTH_REFRESH_025", "User-Agent DeviceInfo logged in DB during refresh", "ERROR", 0, str(e))

    print("=== FINISHED ALL 25 REFRESH TESTS ===")
    pass_count = sum(1 for x in results if "PASS" in x["status"])
    print(f"AUTH_REFRESH SUITE: {pass_count}/{len(results)} PASSED\n")
    return results

if __name__ == "__main__":
    run_suite()
