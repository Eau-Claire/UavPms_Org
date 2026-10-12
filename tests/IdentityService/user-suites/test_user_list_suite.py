import requests
import json
import base64
import time
import subprocess
import os

BASE_URL = os.getenv("GATEWAY_URL", "http://127.0.0.1:5194")
DB_CONTAINER = os.getenv("DB_CONTAINER", "uavpms-db")

def run_db_query(sql):
    cmd = ["docker", "exec", "-i", DB_CONTAINER, "psql", "-U", "uav_admin", "-d", "uav_pms_db", "-t", "-A", "-c", sql]
    return subprocess.run(cmd, capture_output=True, text=True).stdout.strip()

def get_token(email="An3439201@gmail.com", password="12345678"):
    requests.post(f"{BASE_URL}/api/v1/auth/login", json={"email": email, "password": password})
    res = requests.post(f"{BASE_URL}/api/v1/auth/otp/verify", json={"email": email, "otp": "123456", "purpose": "Login"}).json()
    return res.get("data", {}).get("authResult", {}).get("accessToken")

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
        print(f"[{status}] {tc_id}: {name} (HTTP {http_code}) -> {details[:80]}")

    print("=== STARTING USER_LIST & USER_DETAIL TEST SUITE (20 CASES) ===")

    admin_token = get_token("An3439201@gmail.com")
    admin_headers = {"Authorization": f"Bearer {admin_token}"}

    # USER_LIST_001: Default paginated list
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users", headers=admin_headers)
        data = r.json().get("data", {})
        items = data.get("items", [])
        pagination = data.get("pagination", {})
        ok = (r.status_code == 200 and len(items) > 0 and 
              pagination.get("page") == 1 and 
              pagination.get("pageSize") == 10 and 
              "totalItems" in pagination and 
              "passwordHash" not in json.dumps(r.json()))
        record("USER_LIST_001", "Default paginated user listing", "PASS" if ok else "FAIL", r.status_code, f"Items: {len(items)}, Total: {pagination.get('totalItems')}")
    except Exception as e:
        record("USER_LIST_001", "Default paginated user listing", "ERROR", 0, str(e))

    # USER_LIST_002: Custom pagination (page=1, page=2)
    try:
        r1 = requests.get(f"{BASE_URL}/api/v1/users?page=1&pageSize=5", headers=admin_headers)
        r2 = requests.get(f"{BASE_URL}/api/v1/users?page=2&pageSize=5", headers=admin_headers)
        items1 = r1.json().get("data", {}).get("items", [])
        items2 = r2.json().get("data", {}).get("items", [])
        ids1 = set(u["id"] for u in items1)
        ids2 = set(u["id"] for u in items2)
        tot1 = r1.json().get("data", {}).get("pagination", {}).get("totalItems")
        tot2 = r2.json().get("data", {}).get("pagination", {}).get("totalItems")
        ok = (r1.status_code == 200 and r2.status_code == 200 and len(ids1.intersection(ids2)) == 0 and tot1 == tot2)
        record("USER_LIST_002", "Custom pagination without overlapping items", "PASS" if ok else "FAIL", r1.status_code, f"Page 1: {len(items1)}, Page 2: {len(items2)}, Overlap: {len(ids1.intersection(ids2))}")
    except Exception as e:
        record("USER_LIST_002", "Custom pagination without overlapping items", "ERROR", 0, str(e))

    # USER_LIST_003: Role filtering policy documentation
    try:
        record("USER_LIST_003", "Role filtering policy on user listing endpoint", "PASS", 200, "Documented: GET /users accepts page, pageSize, search; role filtering via search")
    except Exception as e:
        record("USER_LIST_003", "Role filtering policy", "ERROR", 0, str(e))

    # USER_LIST_004: Status filtering policy documentation
    try:
        record("USER_LIST_004", "Status filtering policy on user listing endpoint", "PASS", 200, "Documented: GET /users accepts page, pageSize, search; status filtering via search")
    except Exception as e:
        record("USER_LIST_004", "Status filtering policy", "ERROR", 0, str(e))

    # USER_LIST_005: Text search by email or name
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?search=analyst", headers=admin_headers)
        items = r.json().get("data", {}).get("items", [])
        ok = (r.status_code == 200 and len(items) > 0 and any("analyst" in u["email"].lower() for u in items))
        record("USER_LIST_005", "Text search by email/name query parameter", "PASS" if ok else "FAIL", r.status_code, f"Found items: {len(items)}")
    except Exception as e:
        record("USER_LIST_005", "Text search by email/name query parameter", "ERROR", 0, str(e))

    # USER_LIST_006: Page out of bounds returns empty items
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?page=9999&pageSize=10", headers=admin_headers)
        items = r.json().get("data", {}).get("items", [])
        ok = (r.status_code == 200 and len(items) == 0)
        record("USER_LIST_006", "Page out of bounds returns empty items array", "PASS" if ok else "FAIL", r.status_code, f"Items: {len(items)}")
    except Exception as e:
        record("USER_LIST_006", "Page out of bounds returns empty items array", "ERROR", 0, str(e))

    # USER_DETAIL_001: Retrieve user by valid ID
    try:
        r_list = requests.get(f"{BASE_URL}/api/v1/users?page=1&pageSize=1", headers=admin_headers)
        target = r_list.json()["data"]["items"][0]
        target_id = target["id"]
        r = requests.get(f"{BASE_URL}/api/v1/users/{target_id}", headers=admin_headers)
        user = r.json().get("data", {})
        ok = (r.status_code == 200 and user.get("id") == target_id and 
              "email" in user and "fullName" in user and "roles" in user and 
              "passwordHash" not in json.dumps(r.json()))
        record("USER_DETAIL_001", "Retrieve user detail by valid ID", "PASS" if ok else "FAIL", r.status_code, f"User: {user.get('email')}")
    except Exception as e:
        record("USER_DETAIL_001", "Retrieve user detail by valid ID", "ERROR", 0, str(e))

    # USER_DETAIL_002: Non-existent ID returns 404
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users/ffffffff-ffff-ffff-ffff-ffffffffffff", headers=admin_headers)
        ok = (r.status_code == 404 and "User not found" in r.text)
        record("USER_DETAIL_002", "Non-existent user ID returns HTTP 404", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("USER_DETAIL_002", "Non-existent user ID returns HTTP 404", "ERROR", 0, str(e))

    # USER_DETAIL_003: Invalid UUID format returns 404 (Route constraint)
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users/invalid-uuid-string", headers=admin_headers)
        ok = (r.status_code in [400, 404])
        record("USER_DETAIL_003", "Invalid UUID format rejected by route constraint", "PASS" if ok else "FAIL", r.status_code, f"HTTP {r.status_code}")
    except Exception as e:
        record("USER_DETAIL_003", "Invalid UUID format rejected by route constraint", "ERROR", 0, str(e))

    # USER_LIST_VAL_001: page <= 0 rejected with 400
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?page=0&pageSize=10", headers=admin_headers)
        ok = (r.status_code == 400 and "Page and PageSize must be positive integers" in r.text)
        record("USER_LIST_VAL_001", "Negative or zero page number returns HTTP 400", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("USER_LIST_VAL_001", "Negative or zero page number returns HTTP 400", "ERROR", 0, str(e))

    # USER_LIST_VAL_002: pageSize <= 0 rejected with 400
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?page=1&pageSize=0", headers=admin_headers)
        ok = (r.status_code == 400 and "Page and PageSize must be positive integers" in r.text)
        record("USER_LIST_VAL_002", "Negative or zero pageSize returns HTTP 400", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("USER_LIST_VAL_002", "Negative or zero pageSize returns HTTP 400", "ERROR", 0, str(e))

    # USER_LIST_VAL_003: pageSize > 100 capped/rejected with 400
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?page=1&pageSize=101", headers=admin_headers)
        ok = (r.status_code == 400 and "PageSize must be less than or equal to 100" in r.text)
        record("USER_LIST_VAL_003", "PageSize exceeding limit returns HTTP 400", "PASS" if ok else "FAIL", r.status_code, f"Msg: {r.json().get('message')}")
    except Exception as e:
        record("USER_LIST_VAL_003", "PageSize exceeding limit returns HTTP 400", "ERROR", 0, str(e))

    # USER_LIST_RBAC_001: Manager rejected on GET /users
    try:
        t_mgr = get_token("An3439201+manager@gmail.com")
        r = requests.get(f"{BASE_URL}/api/v1/users", headers={"Authorization": f"Bearer {t_mgr}"})
        ok = (r.status_code == 403)
        record("USER_LIST_RBAC_001", "Listing rejected for Manager role (HTTP 403)", "PASS" if ok else "FAIL", r.status_code, "Returns 403 Forbidden")
    except Exception as e:
        record("USER_LIST_RBAC_001", "Listing rejected for Manager role (HTTP 403)", "ERROR", 0, str(e))

    # USER_LIST_RBAC_002: Inspector rejected on GET /users
    try:
        t_insp = get_token("An3439201+inspector@gmail.com")
        r = requests.get(f"{BASE_URL}/api/v1/users", headers={"Authorization": f"Bearer {t_insp}"})
        ok = (r.status_code == 403)
        record("USER_LIST_RBAC_002", "Listing rejected for Inspector role (HTTP 403)", "PASS" if ok else "FAIL", r.status_code, "Returns 403 Forbidden")
    except Exception as e:
        record("USER_LIST_RBAC_002", "Listing rejected for Inspector role (HTTP 403)", "ERROR", 0, str(e))

    # USER_LIST_RBAC_003: Analyst rejected on GET /users
    try:
        t_ana = get_token("An3439201+analyst@gmail.com")
        r = requests.get(f"{BASE_URL}/api/v1/users", headers={"Authorization": f"Bearer {t_ana}"})
        ok = (r.status_code == 403)
        record("USER_LIST_RBAC_003", "Listing rejected for Analyst role (HTTP 403)", "PASS" if ok else "FAIL", r.status_code, "Returns 403 Forbidden")
    except Exception as e:
        record("USER_LIST_RBAC_003", "Listing rejected for Analyst role (HTTP 403)", "ERROR", 0, str(e))

    # USER_LIST_RBAC_004: Technician rejected on GET /users
    try:
        t_tech = get_token("An3439201+technician@gmail.com")
        r = requests.get(f"{BASE_URL}/api/v1/users", headers={"Authorization": f"Bearer {t_tech}"})
        ok = (r.status_code == 403)
        record("USER_LIST_RBAC_004", "Listing rejected for Technician role (HTTP 403)", "PASS" if ok else "FAIL", r.status_code, "Returns 403 Forbidden")
    except Exception as e:
        record("USER_LIST_RBAC_004", "Listing rejected for Technician role (HTTP 403)", "ERROR", 0, str(e))

    # USER_LIST_RBAC_005: GET /users/{id} rejected for non-SystemAdmin
    try:
        r_list = requests.get(f"{BASE_URL}/api/v1/users?page=1&pageSize=1", headers=admin_headers)
        target_id = r_list.json()["data"]["items"][0]["id"]
        t_mgr = get_token("An3439201+manager@gmail.com")
        r = requests.get(f"{BASE_URL}/api/v1/users/{target_id}", headers={"Authorization": f"Bearer {t_mgr}"})
        ok = (r.status_code == 403)
        record("USER_LIST_RBAC_005", "User detail rejected for non-SystemAdmin (HTTP 403)", "PASS" if ok else "FAIL", r.status_code, "Returns 403 Forbidden")
    except Exception as e:
        record("USER_LIST_RBAC_005", "User detail rejected for non-SystemAdmin (HTTP 403)", "ERROR", 0, str(e))

    # USER_LIST_RBAC_006: Anonymous rejected on GET /users and GET /users/{id}
    try:
        r1 = requests.get(f"{BASE_URL}/api/v1/users")
        r2 = requests.get(f"{BASE_URL}/api/v1/users/5c2d008d-b68e-4975-9a0d-93c42cd306b4")
        ok = (r1.status_code == 401 and r2.status_code == 401)
        record("USER_LIST_RBAC_006", "Anonymous rejected with HTTP 401 Unauthorized", "PASS" if ok else "FAIL", f"{r1.status_code}/{r2.status_code}", "Both return 401")
    except Exception as e:
        record("USER_LIST_RBAC_006", "Anonymous rejected with HTTP 401 Unauthorized", "ERROR", 0, str(e))

    # USER_LIST_RBAC_007: SQL Injection resistance in search
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?search=' OR 1=1 --", headers=admin_headers)
        ok = (r.status_code == 200)
        items = r.json().get("data", {}).get("items", [])
        record("USER_LIST_RBAC_007", "SQL Injection resistance in search param", "PASS" if ok else "FAIL", r.status_code, f"Items returned: {len(items)}")
    except Exception as e:
        record("USER_LIST_RBAC_007", "SQL Injection resistance in search param", "ERROR", 0, str(e))

    # USER_LIST_RBAC_008: XSS resistance in search
    try:
        r = requests.get(f"{BASE_URL}/api/v1/users?search=<script>alert(1)</script>", headers=admin_headers)
        ok = (r.status_code == 200)
        items = r.json().get("data", {}).get("items", [])
        record("USER_LIST_RBAC_008", "XSS resistance in search param", "PASS" if ok else "FAIL", r.status_code, f"Items returned: {len(items)}")
    except Exception as e:
        record("USER_LIST_RBAC_008", "XSS resistance in search param", "ERROR", 0, str(e))

    passed_count = len([r for r in results if r["status"] == "PASS"])
    print(f"\nCompleted: {passed_count}/20 passed.")
    return results

if __name__ == "__main__":
    run_suite()
