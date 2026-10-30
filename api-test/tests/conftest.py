from __future__ import annotations

import json
import os
import random
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

import httpx
import pytest


DATA_DIR = Path(os.getenv("TEST_DATA_DIR", Path(__file__).resolve().parents[1] / "data"))
ASSESSMENT_PATH = "/api/v2/pre-mission-assessments"
GIS_BOUNDARY_WKT = "POLYGON((106.79 10.83, 106.82 10.83, 106.82 10.86, 106.79 10.86, 106.79 10.83))"


def load_json(name: str) -> dict[str, Any]:
    path = DATA_DIR / name
    if name == "roles.json" and not path.exists():
        path = DATA_DIR / "roles.example.json"
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


@pytest.fixture(scope="session")
def base_url() -> str:
    return os.getenv("TARGET_API", "http://gateway:8080").rstrip("/")


@pytest.fixture(scope="session")
def client(base_url: str) -> httpx.Client:
    with httpx.Client(base_url=base_url, timeout=float(os.getenv("TEST_TIMEOUT", "30"))) as http:
        yield http


@pytest.fixture(scope="session")
def auth_client() -> httpx.Client:
    auth_url = os.getenv("AUTH_API") or os.getenv("TARGET_API", "http://gateway:8080")
    with httpx.Client(base_url=auth_url.rstrip("/"), timeout=float(os.getenv("TEST_TIMEOUT", "30"))) as http:
        yield http


@pytest.fixture(scope="session")
def roles_data() -> dict[str, Any]:
    return load_json("roles.json")


@pytest.fixture(scope="session")
def role_data(roles_data: dict[str, Any]) -> dict[str, Any]:
    auth = load_json("auth.json")
    role_name = os.getenv("TEST_ROLE") or auth.get("defaultRole") or "default"
    return select_role_data(roles_data, role_name)


def select_role_data(roles: dict[str, Any], role_name: str) -> dict[str, Any]:
    selected = roles.get(role_name, roles.get("default", {})).copy()
    selected["role"] = role_name
    selected["email"] = selected.get("email", "")
    selected["password"] = selected.get("password", "")
    selected["token"] = selected.get("token", "")
    selected["deviceTrustToken"] = selected.get("deviceTrustToken", "")
    return selected


@pytest.fixture(scope="session")
def token_for_role(auth_client: httpx.Client, roles_data: dict[str, Any]):
    cache: dict[str, str] = {}

    def resolve(role_name: str) -> str:
        if role_name not in cache:
            cache[role_name] = login_for_role(auth_client, select_role_data(roles_data, role_name))
        return cache[role_name]

    return resolve


def login_for_role(client: httpx.Client, role_data: dict[str, Any]) -> str:
    if role_data.get("token"):
        return role_data["token"]

    email = role_data.get("email", "")
    password = role_data.get("password", "")
    if not email or not password or password == "change-me":
        pytest.fail("Provide TEST_USERNAME/TEST_PASSWORD or update api-test/data/roles.json for this role.")

    headers = {}
    if role_data.get("deviceTrustToken"):
        headers["X-Device-Trust-Token"] = role_data["deviceTrustToken"]

    client.cookies.clear()
    response = client.post("/api/v1/auth/login", json={"email": email, "password": password}, headers=headers)
    client.cookies.clear()
    assert response.status_code == 200, f"POST /api/v1/auth/login returned {response.status_code}: {response.text}"
    body = response.json()
    token = (
        body.get("token")
        or body.get("accessToken")
        or body.get("data", {}).get("token")
        or body.get("data", {}).get("accessToken")
        or body.get("data", {}).get("authResult", {}).get("accessToken")
    )

    if not token and (body.get("message") == "OTP required" or "otp" in str(body).lower()):
        token, device_trust_token = _verify_otp_for_login(client, email)
        if device_trust_token:
            role_data["deviceTrustToken"] = device_trust_token

    assert token, f"POST /api/v1/auth/login did not return an access token: {body}"
    return token


def _verify_otp_for_login(client: httpx.Client, email: str) -> tuple[str | None, str | None]:
    test_otp = "000000"
    _try_set_redis_otp(email, test_otp)

    verify_resp = client.post(
        "/api/v1/auth/otp/verify",
        json={"email": email, "otp": test_otp, "purpose": "Login"},
    )
    if verify_resp.status_code == 200:
        data = verify_resp.json()
        auth_res = data.get("data", {}).get("authResult", {})
        token = (
            data.get("token")
            or data.get("accessToken")
            or data.get("data", {}).get("token")
            or data.get("data", {}).get("accessToken")
            or (auth_res.get("accessToken") if isinstance(auth_res, dict) else None)
        )
        device_token = (
            (auth_res.get("deviceTrustToken") if isinstance(auth_res, dict) else None)
            or data.get("deviceTrustToken")
            or data.get("data", {}).get("deviceTrustToken")
        )
        return token, device_token
    else:
        print(f"[conftest] OTP verify returned {verify_resp.status_code}: {verify_resp.text}")
    return None, None


def _try_set_redis_otp(email: str, otp_code: str) -> None:
    import base64
    import hashlib
    import redis

    redis_conns = [
        os.getenv("REDIS_CONNECTION"),
        os.getenv("Redis__ConnectionString"),
        "redis:6379",
        "uav-redis:6379",
        "localhost:6379",
    ]
    default_pwd = os.getenv("REDIS_PASSWORD") or os.getenv("Redis__Password") or None
    key = f"otp:login:{email.strip().lower()}"
    attempts_key = f"{key}:attempts"
    hashed = base64.b64encode(hashlib.sha256(otp_code.encode("utf-8")).digest()).decode("ascii")

    for conn_str in redis_conns:
        if not conn_str:
            continue
        try:
            pwd = default_pwd
            endpoint = conn_str.split(",", 1)[0]
            if "password=" in conn_str:
                for part in conn_str.split(","):
                    if part.strip().startswith("password="):
                        pwd = part.split("=", 1)[1].strip()
                        break
            if ":" in endpoint:
                host, port_s = endpoint.rsplit(":", 1)
                port = int(port_s)
            else:
                host, port = endpoint, 6379
            r = redis.Redis(
                host=host,
                port=port,
                password=pwd,
                decode_responses=True,
                socket_timeout=2,
            )
            r.setex(key, 180, hashed)
            r.delete(attempts_key)
            print(f"[conftest] Set OTP in Redis for {email} via {host}:{port}")
            return
        except Exception as ex:
            print(f"[conftest] Failed to connect to Redis {conn_str}: {ex}")
            continue


@pytest.fixture(scope="session")
def auth_token(auth_client: httpx.Client, role_data: dict[str, Any]) -> str:
    return login_for_role(auth_client, role_data)


@pytest.fixture()
def auth_headers(auth_token: str) -> dict[str, str]:
    return {"Authorization": f"Bearer {auth_token}"}


@pytest.fixture()
def role_headers(token_for_role):
    def headers(role_name: str) -> dict[str, str]:
        return {"Authorization": f"Bearer {token_for_role(role_name)}"}

    return headers


@pytest.fixture(scope="session")
def data() -> dict[str, Any]:
    return {
        "auth": load_json("auth.json"),
        "mission": load_json("mission.json"),
        "inspection": load_json("inspection.json"),
    }


def assert_json_response(response: httpx.Response) -> dict[str, Any]:
    content_type = response.headers.get("content-type", "")
    assert "json" in content_type.lower(), f"Expected JSON response, got {content_type}: {response.text[:500]}"
    body = response.json()
    assert isinstance(body, dict), f"Expected JSON object, got {type(body).__name__}"
    return body


def _psycopg_kwargs(connection_string: str) -> dict[str, Any]:
    aliases = {"host": "host", "port": "port", "database": "dbname", "username": "user", "user id": "user", "password": "password"}
    values: dict[str, Any] = {}
    for part in connection_string.split(";"):
        if "=" not in part:
            continue
        key, value = part.split("=", 1)
        mapped = aliases.get(key.strip().lower())
        if mapped:
            values[mapped] = value.strip()
    return values


@pytest.fixture(scope="session")
def db_connection():
    connection_string = os.getenv("DB_CONNECTION", "")
    if not connection_string:
        pytest.skip("DB_CONNECTION is required for deterministic GIS API tests")
    if os.getenv("ALLOW_API_TEST_DB_SEED", "").lower() != "true":
        pytest.skip("Set ALLOW_API_TEST_DB_SEED=true only for a disposable test database")
    import psycopg

    connection = psycopg.connect(**_psycopg_kwargs(connection_string), autocommit=True)
    try:
        yield connection
    finally:
        connection.close()


def _find_user_id(cursor, email: str) -> str | None:
    if not email or email.startswith("replace-with-"):
        return None
    cursor.execute('SELECT "Id" FROM "Users" WHERE lower("Email") = lower(%s) AND NOT "IsDeleted"', (email,))
    row = cursor.fetchone()
    return str(row[0]) if row is not None else None


@pytest.fixture(scope="session")
def gis_seed(db_connection, roles_data: dict[str, Any]) -> dict[str, str]:
    # ERD v2.2: infrastructure is only Regions -> Towers.
    ids = {
        "region": "71000000-0000-0000-0000-000000000001",
        "tower_1": "71000000-0000-0000-0000-000000000011",
        "tower_2": "71000000-0000-0000-0000-000000000012",
        "outside_tower": "71000000-0000-0000-0000-000000000014",
        "uav": "71000000-0000-0000-0000-000000000020",
        "mission_uav": "71000000-0000-0000-0000-000000000021",
    }
    inspector_email = roles_data.get("inspector", {}).get("email", "")
    if not inspector_email or inspector_email.startswith("replace-with-"):
        pytest.skip("Configure the inspector account in api-test/data/roles.json")

    with db_connection.cursor() as cursor:
        inspector_id = _find_user_id(cursor, inspector_email)
        if inspector_id is None:
            pytest.skip(f"Inspector seed user does not exist: {inspector_email}")
        ids["inspector"] = inspector_id
        for role_name in ("manager", "analyst", "technician"):
            user_id = _find_user_id(cursor, roles_data.get(role_name, {}).get("email", ""))
            if user_id is not None:
                ids[role_name] = user_id

        cursor.execute(
            """
            INSERT INTO "Regions" ("Id","Name","Code","Type","Geom","CreatedAt","IsDeleted")
            VALUES (%s,'GIS API Test Region','GIS-TEST-REGION','District',ST_GeomFromText('POLYGON((106.79 10.83,106.82 10.83,106.82 10.86,106.79 10.86,106.79 10.83))',4326),now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "Geom"=EXCLUDED."Geom","IsDeleted"=false
            """, (ids["region"],))
        towers = [
            (ids["tower_1"], "GIS-TEST-TOWER-A", 106.804, 10.844),
            (ids["tower_2"], "GIS-TEST-TOWER-B", 106.808, 10.848),
            (ids["outside_tower"], "GIS-TEST-TOWER-OUTSIDE", 107.0, 11.0),
        ]
        for tower_id, code, longitude, latitude in towers:
            cursor.execute(
                """
                INSERT INTO "Towers" ("Id","RegionId","TowerCode","PoleType","Address","Geom","CreatedAt","IsDeleted")
                VALUES (%s,%s,%s,'Suspension','GIS API Test Address',ST_SetSRID(ST_Point(%s,%s),4326),now(),false)
                ON CONFLICT ("Id") DO UPDATE SET "RegionId"=EXCLUDED."RegionId","Geom"=EXCLUDED."Geom","IsDeleted"=false
                """,
                (tower_id, ids["region"], code, longitude, latitude),
            )
        # "uav" is used by the drone technical inspection tests (its health may become Critical);
        # "mission_uav" stays Available/Healthy so assessments can become READY.
        uavs = [(ids["uav"], "GIS-TEST-UAV"), (ids["mission_uav"], "GIS-TEST-MISSION-UAV")]
        for uav_id, code in uavs:
            cursor.execute(
                """
                INSERT INTO "UAVs" ("Id","UavCode","Name","Model","SerialNumber","Category","AssignedRegionId","FlightStatus","OperationalStatus","TechnicalHealth","BatteryLevel","FlightEnduranceMinutes","OperationalRangeKm","Version","CreatedAt","IsDeleted")
                VALUES (%s,%s,%s,'API Test',%s,'Hexacopter',%s,'Idle','Available','Healthy',100,30,10,1,now(),false)
                ON CONFLICT ("Id") DO UPDATE SET "FlightStatus"='Idle',"OperationalStatus"='Available',"TechnicalHealth"='Healthy',"BatteryLevel"=100,"IsDeleted"=false
                """,
                (uav_id, code, code, code, ids["region"]),
            )
        # Region scope makes the manager allowed to assess the region and the operational
        # users eligible personnel candidates.
        for role_name in ("manager", "inspector", "analyst", "technician"):
            if role_name not in ids:
                continue
            cursor.execute(
                """
                INSERT INTO "UserGeographicScopes" ("Id","UserId","RegionId","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(),%s,%s,now(),false
                WHERE NOT EXISTS (SELECT 1 FROM "UserGeographicScopes" WHERE "UserId"=%s AND "RegionId"=%s AND NOT "IsDeleted")
                """,
                (ids[role_name], ids["region"], ids[role_name], ids["region"]),
            )

    yield ids

    with db_connection.cursor() as cursor:
        cursor.execute('DELETE FROM "MissionTargets" WHERE "MissionId" IN (SELECT "Id" FROM "Missions" WHERE "Title" LIKE \'GIS API Test %%\')')
        cursor.execute('DELETE FROM "Missions" WHERE "Title" LIKE \'GIS API Test %%\'')


def unique_planned_window() -> tuple[str, str]:
    # Every mission books its personnel and UAV for the assessment window, so each
    # assessment gets its own far-future window to avoid RESOURCE_BOOKING_CONFLICT.
    start = datetime(2090, 1, 1, 8, tzinfo=timezone.utc) + timedelta(hours=4 * random.randint(0, 100_000))
    end = start + timedelta(hours=2)
    return start.strftime("%Y-%m-%dT%H:%M:%SZ"), end.strftime("%Y-%m-%dT%H:%M:%SZ")


@pytest.fixture()
def create_ready_assessment(gis_seed: dict[str, str], client: httpx.Client, role_headers):
    missing = [role_name for role_name in ("analyst", "technician") if role_name not in gis_seed]
    if missing:
        pytest.skip(f"A READY assessment needs eligible {', '.join(missing)} account(s) in api-test/data/roles.json")

    def create(tower_ids: list[str] | None = None) -> dict[str, Any]:
        planned_start, planned_end = unique_planned_window()
        payload = {
            "regionId": gis_seed["region"],
            "plannedStart": planned_start,
            "plannedEnd": planned_end,
            "towerIds": tower_ids if tower_ids is not None else [gis_seed["tower_1"], gis_seed["tower_2"]],
            "boundaryWkt": GIS_BOUNDARY_WKT,
            "idempotencyKey": f"gis-api-test-{uuid.uuid4()}",
        }
        response = client.post(ASSESSMENT_PATH, json=payload, headers=role_headers("manager"))
        assert response.status_code == 200, f"POST {ASSESSMENT_PATH} returned {response.status_code}: {response.text}"
        assessment = response.json()
        assert assessment["status"] == "READY", f"Assessment is not READY: {assessment}"
        return assessment

    return create


@pytest.fixture()
def create_gis_mission(gis_seed: dict[str, str], client: httpx.Client, role_headers, create_ready_assessment):
    # Missions are only created from a READY assessment (POST /api/v1/missions was removed).
    def create(title: str, tower_ids: list[str] | None = None) -> dict[str, Any]:
        assessment = create_ready_assessment(tower_ids)
        payload = {
            "assessmentId": assessment["id"],
            "title": title,
            "description": "Deterministic GIS API automation mission",
            "personnel": [{"userId": gis_seed["inspector"], "role": "INSPECTOR", "isRequired": True}],
            "droneIds": [gis_seed["mission_uav"]],
            "idempotencyKey": f"gis-api-test-mission-{uuid.uuid4()}",
        }
        path = f'{ASSESSMENT_PATH}/{assessment["id"]}/create-mission'
        response = client.post(path, json=payload, headers=role_headers("manager"))
        assert response.status_code == 200, f"POST {path} returned {response.status_code}: {response.text}"
        return response.json()

    return create
