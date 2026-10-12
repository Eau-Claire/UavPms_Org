import requests
import json
import base64
import time
import subprocess
import os

BASE_URL = os.getenv("GATEWAY_URL", "http://127.0.0.1:5194")
DB_CONTAINER = os.getenv("DB_CONTAINER", "uavpms-db")
REDIS_CONTAINER = os.getenv("REDIS_CONTAINER", "uav-redis")

def run_db_query(sql):
    cmd = ["docker", "exec", "-i", DB_CONTAINER, "psql", "-U", "uav_admin", "-d", "uav_pms_db", "-t", "-A", "-c", sql]
    res = subprocess.run(cmd, capture_output=True, text=True)
    return res.stdout.strip()

def run_redis_cmd(args):
    cmd = ["docker", "exec", "-i", REDIS_CONTAINER, "redis-cli"] + args
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

def run_suite():
    results = []

    def record(tc_id, name, status, details):
        results.append({"id": tc_id, "name": name, "status": status, "details": details})
        print(f"[{status}] {tc_id}: {name} -> {details[:80]}")

    print("=== STARTING AUTH_LOGIN TEST SUITE (32 CASES) ===")

    # AUTH_LOGIN_001
    try:
        run_redis_cmd(["del", "otp:login:an3439201@gmail.com", "otp:login:an3439201@gmail.com:attempts"])
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": "12345678"})
        b1 = r1.json()
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201@gmail.com", "otp": "123456", "purpose": "Login"})
        b2 = r2.json()
        auth_res = b2.get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        ok = (r1.status_code == 200 and b1.get("message") == "OTP required" and
              r2.status_code == 200 and "accessToken" in auth_res and
              "refreshToken" in auth_res and "SystemAdmin" in roles and
              "passwordHash" not in json.dumps(b2))
        record("AUTH_LOGIN_001", "Successful 2-step SystemAdmin", "PASS" if ok else "FAIL", f"HTTP {r1.status_code}/{r2.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_001", "Successful 2-step SystemAdmin", "ERROR", str(e))

    # AUTH_LOGIN_002
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+manager@gmail.com", "password": "12345678"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201+manager@gmail.com", "otp": "123456", "purpose": "Login"})
        auth_res = r2.json().get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        ok = (r1.status_code == 200 and r2.status_code == 200 and "Manager" in roles)
        record("AUTH_LOGIN_002", "Successful Manager login", "PASS" if ok else "FAIL", f"roles={roles}")
    except Exception as e:
        record("AUTH_LOGIN_002", "Successful Manager login", "ERROR", str(e))

    # AUTH_LOGIN_003
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+inspector@gmail.com", "password": "12345678"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201+inspector@gmail.com", "otp": "123456", "purpose": "Login"})
        auth_res = r2.json().get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        ok = (r1.status_code == 200 and r2.status_code == 200 and "Inspector" in roles)
        record("AUTH_LOGIN_003", "Successful Inspector login", "PASS" if ok else "FAIL", f"roles={roles}")
    except Exception as e:
        record("AUTH_LOGIN_003", "Successful Inspector login", "ERROR", str(e))

    # AUTH_LOGIN_004
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+analyst@gmail.com", "password": "12345678"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201+analyst@gmail.com", "otp": "123456", "purpose": "Login"})
        auth_res = r2.json().get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        ok = (r1.status_code == 200 and r2.status_code == 200 and "Analyst" in roles)
        record("AUTH_LOGIN_004", "Successful Analyst login", "PASS" if ok else "FAIL", f"roles={roles}")
    except Exception as e:
        record("AUTH_LOGIN_004", "Successful Analyst login", "ERROR", str(e))

    # AUTH_LOGIN_005
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+technician@gmail.com", "password": "12345678"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201+technician@gmail.com", "otp": "123456", "purpose": "Login"})
        auth_res = r2.json().get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        ok = (r1.status_code == 200 and r2.status_code == 200 and ("Technician" in roles or "MaintenanceTechnician" in roles))
        record("AUTH_LOGIN_005", "Successful Technician login", "PASS" if ok else "FAIL", f"roles={roles}")
    except Exception as e:
        record("AUTH_LOGIN_005", "Successful Technician login", "ERROR", str(e))

    # AUTH_LOGIN_006
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+multi@gmail.com", "password": "12345678"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201+multi@gmail.com", "otp": "123456", "purpose": "Login"})
        auth_res = r2.json().get("data", {}).get("authResult", {})
        roles = auth_res.get("user", {}).get("roles", [])
        jwt_claims = decode_jwt_payload(auth_res.get("accessToken", ""))
        role_claims = jwt_claims.get("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", [])
        ok = (r1.status_code == 200 and r2.status_code == 200 and set(["Manager", "Analyst"]).issubset(set(roles)) and set(["Manager", "Analyst"]).issubset(set(role_claims)))
        record("AUTH_LOGIN_006", "Successful multi-role login", "PASS" if ok else "FAIL", f"roles={roles}")
    except Exception as e:
        record("AUTH_LOGIN_006", "Successful multi-role login", "ERROR", str(e))

    # AUTH_LOGIN_007
    try:
        r_upper = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "AN3439201@GMAIL.COM", "password": "12345678"})
        r_mixed = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@Gmail.Com", "password": "12345678"})
        ok = (r_upper.status_code == 200 and r_mixed.status_code == 200 and
              r_upper.json().get("message") == "OTP required" and r_mixed.json().get("message") == "OTP required")
        record("AUTH_LOGIN_007", "Case-insensitive email handling", "PASS" if ok else "FAIL", "Normalized lowercase handled")
    except Exception as e:
        record("AUTH_LOGIN_007", "Case-insensitive email handling", "ERROR", str(e))

    # AUTH_LOGIN_008
    try:
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": "An3439201@gmail.com", "otp": "123456", "purpose": "Login"})
        token = r2.json().get("data", {}).get("authResult", {}).get("accessToken")
        claims = decode_jwt_payload(token)
        has_sub = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" in claims
        has_email = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress" in claims
        has_role = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" in claims
        iss = claims.get("iss")
        aud = claims.get("aud")
        exp = claims.get("exp")
        ok = (has_sub and has_email and has_role and iss == "UavPms" and aud == "UavPmsClient" and exp > time.time())
        record("AUTH_LOGIN_008", "JWT Claims validation", "PASS" if ok else "FAIL", f"iss={iss}, aud={aud}")
    except Exception as e:
        record("AUTH_LOGIN_008", "JWT Claims validation", "ERROR", str(e))

    # AUTH_LOGIN_009
    try:
        db_res = run_db_query("SELECT count(*) FROM \"RefreshTokens\" WHERE \"UserId\" = '206a6a73-e49e-4b60-befd-400047334779' AND \"RevokedAt\" IS NULL AND \"ExpiresAt\" > NOW();")
        count = int(db_res) if db_res.isdigit() else 0
        record("AUTH_LOGIN_009", "RefreshToken persisted in DB", "PASS" if count > 0 else "FAIL", f"Count: {count}")
    except Exception as e:
        record("AUTH_LOGIN_009", "RefreshToken persisted in DB", "ERROR", str(e))

    # AUTH_LOGIN_010
    try:
        db_res = run_db_query("SELECT count(*) FROM \"AuditLogs\" WHERE \"TableName\" IN ('RefreshTokens', 'TrustedDevices') AND \"CreatedAt\" > NOW() - INTERVAL '5 minutes';")
        count = int(db_res) if db_res.isdigit() else 0
        record("AUTH_LOGIN_010", "AuditLog entry generated", "PASS" if count > 0 else "FAIL", f"Count: {count}")
    except Exception as e:
        record("AUTH_LOGIN_010", "AuditLog entry generated", "ERROR", str(e))

    # AUTH_LOGIN_011
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "nonexistent@gmail.com", "password": "Password@123"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_011", "Nonexistent email", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_011", "Nonexistent email", "ERROR", str(e))

    # AUTH_LOGIN_012
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": "WrongPassword123!"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_012", "Incorrect password", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_012", "Incorrect password", "ERROR", str(e))

    # AUTH_LOGIN_013
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "", "password": "12345678"})
        ok = (r.status_code == 400 and "Email is required" in r.text)
        record("AUTH_LOGIN_013", "Empty email string", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_013", "Empty email string", "ERROR", str(e))

    # AUTH_LOGIN_014
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": ""})
        ok = (r.status_code == 400 and "Password is required" in r.text)
        record("AUTH_LOGIN_014", "Empty password string", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_014", "Empty password string", "ERROR", str(e))

    # AUTH_LOGIN_015
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"password": "12345678"})
        ok = (r.status_code == 400 and "Email is required" in r.text)
        record("AUTH_LOGIN_015", "Missing email field", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_015", "Missing email field", "ERROR", str(e))

    # AUTH_LOGIN_016
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com"})
        ok = (r.status_code == 400 and "Password is required" in r.text)
        record("AUTH_LOGIN_016", "Missing password field", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_016", "Missing password field", "ERROR", str(e))

    # AUTH_LOGIN_017
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={})
        ok = (r.status_code == 400 and "Email is required" in r.text and "Password is required" in r.text)
        record("AUTH_LOGIN_017", "Empty JSON body", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_017", "Empty JSON body", "ERROR", str(e))

    # AUTH_LOGIN_018
    try:
        subcases = ["notanemail", "admin@", "@gmail.com", "admin gmail.com"]
        statuses = [requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": sc, "password": "12345678"}).status_code for sc in subcases]
        ok = all(s in [400, 401] for s in statuses)
        record("AUTH_LOGIN_018", "Invalid email format", "PASS" if ok else "FAIL", f"status codes: {statuses}")
    except Exception as e:
        record("AUTH_LOGIN_018", "Invalid email format", "ERROR", str(e))

    # AUTH_LOGIN_019
    try:
        long_email = "a" * 300 + "@example.com"
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": long_email, "password": "12345678"})
        ok = (r.status_code in [400, 401] and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_019", "Email exceeds 256 chars", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_019", "Email exceeds 256 chars", "ERROR", str(e))

    # AUTH_LOGIN_020
    try:
        long_pwd = "P" * 200
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": long_pwd})
        ok = (r.status_code in [400, 401] and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_020", "Password exceeds 128 chars", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_020", "Password exceeds 128 chars", "ERROR", str(e))

    # AUTH_LOGIN_021
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+suspended@gmail.com", "password": "12345678"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_021", "Account status Suspended", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_021", "Account status Suspended", "ERROR", str(e))

    # AUTH_LOGIN_022
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+inactive@gmail.com", "password": "12345678"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_022", "Account status Inactive", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_022", "Account status Inactive", "ERROR", str(e))

    # AUTH_LOGIN_023
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201+pending@gmail.com", "password": "12345678"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_023", "Account status Pending", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_023", "Account status Pending", "ERROR", str(e))

    # AUTH_LOGIN_024
    try:
        email = "An3439201+manager@gmail.com"
        norm_email = "an3439201+manager@gmail.com"
        run_redis_cmd(["del", f"otp:login:{norm_email}", f"otp:login:{norm_email}:attempts"])
        requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": email, "password": "12345678"})
        verify_res = [requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": email, "otp": "999999", "purpose": "Login"}).json().get("message") for _ in range(5)]
        redis_key_exists = run_redis_cmd(["exists", f"otp:login:{norm_email}"])
        ok = ("Maximum verification attempts exceeded" in verify_res[4] and redis_key_exists == "0")
        record("AUTH_LOGIN_024", "OTP Rate Limiting / 5 failed attempts", "PASS" if ok else "FAIL", f"5th msg: {verify_res[4]}")
    except Exception as e:
        record("AUTH_LOGIN_024", "OTP Rate Limiting / 5 failed attempts", "ERROR", str(e))

    # AUTH_LOGIN_025
    try:
        email = "An3439201+manager@gmail.com"
        r_new = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": email, "password": "12345678"})
        r_v = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": email, "otp": "123456", "purpose": "Login"})
        ok = (r_new.status_code == 200 and r_v.status_code == 200 and r_v.json().get("success") == True)
        record("AUTH_LOGIN_025", "Reset attempt counter on new OTP request", "PASS" if ok else "FAIL", f"HTTP {r_new.status_code}/{r_v.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_025", "Reset attempt counter on new OTP request", "ERROR", str(e))

    # AUTH_LOGIN_026
    try:
        t1_list = []
        for _ in range(5):
            t0 = time.time()
            requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": "WrongPassword123!"})
            t1_list.append((time.time() - t0) * 1000)
        t2_list = []
        for _ in range(5):
            t0 = time.time()
            requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "fake_nonexistent_user@notexist.com", "password": "WrongPassword123!"})
            t2_list.append((time.time() - t0) * 1000)
        diff = abs(sum(t1_list)/len(t1_list) - sum(t2_list)/len(t2_list))
        ok = diff < 50.0
        record("AUTH_LOGIN_026", "Timing attack prevention", "PASS" if ok else "FAIL", f"diff={diff:.1f}ms")
    except Exception as e:
        record("AUTH_LOGIN_026", "Timing attack prevention", "ERROR", str(e))

    # AUTH_LOGIN_027
    try:
        r1 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "admin' OR 1=1 --", "password": "anything"})
        r2 = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "An3439201@gmail.com", "password": "' OR '1'='1"})
        ok = (r1.status_code == 401 and r2.status_code == 401)
        record("AUTH_LOGIN_027", "SQL Injection resistance", "PASS" if ok else "FAIL", f"HTTP {r1.status_code}/{r2.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_027", "SQL Injection resistance", "ERROR", str(e))

    # AUTH_LOGIN_028
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "<script>alert('xss')</script>@gmail.com", "password": "12345678"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_028", "XSS payload resistance", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_028", "XSS payload resistance", "ERROR", str(e))

    # AUTH_LOGIN_029
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": "admin; cat /etc/passwd;@gmail.com", "password": "12345678"})
        ok = (r.status_code == 401 and "Invalid credentials" in r.text)
        record("AUTH_LOGIN_029", "Command Injection resistance", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_029", "Command Injection resistance", "ERROR", str(e))

    # AUTH_LOGIN_030
    try:
        r2m = requests.post(f"{BASE_URL}/api/v1/auth/login", data=json.dumps({"email":"admin@example.com","password":"a"*2000000}), headers={"Content-Type": "application/json"})
        ok = (r2m.status_code == 401)
        record("AUTH_LOGIN_030", "DoS oversized payload handling", "PASS" if ok else "FAIL", f"2MB HTTP {r2m.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_030", "DoS oversized payload handling", "ERROR", str(e))

    # AUTH_LOGIN_031
    try:
        r = requests.post(f"{BASE_URL}/api/v1/auth/login", data="email=admin", headers={"Content-Type": "text/plain"})
        ok = (r.status_code == 415)
        record("AUTH_LOGIN_031", "Content-Type enforcement", "PASS" if ok else "FAIL", f"HTTP {r.status_code}")
    except Exception as e:
        record("AUTH_LOGIN_031", "Content-Type enforcement", "ERROR", str(e))

    # AUTH_LOGIN_032
    try:
        r = requests.options(f"{BASE_URL}/api/v1/auth/login", headers={"Origin": "https://malicious-site.com", "Access-Control-Request-Method": "POST"})
        allow_origin = r.headers.get("Access-Control-Allow-Origin")
        ok = (allow_origin is None or allow_origin != "https://malicious-site.com")
        record("AUTH_LOGIN_032", "CORS blocks unauthorized origin", "PASS" if ok else "FAIL", f"allow_origin={allow_origin}")
    except Exception as e:
        record("AUTH_LOGIN_032", "CORS blocks unauthorized origin", "ERROR", str(e))

    pass_count = sum(1 for x in results if "PASS" in x["status"])
    print(f"\nAUTH_LOGIN SUITE: {pass_count}/{len(results)} PASSED\n")
    return results

if __name__ == "__main__":
    run_suite()
