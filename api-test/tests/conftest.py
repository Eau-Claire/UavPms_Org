from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

import httpx
import pytest


DATA_DIR = Path(os.getenv("TEST_DATA_DIR", Path(__file__).resolve().parents[1] / "data"))


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
    assert token, f"POST /api/v1/auth/login did not return an access token: {body}"
    return token


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
        "asset": load_json("asset.json"),
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


@pytest.fixture(scope="session")
def gis_seed(db_connection, roles_data: dict[str, Any]) -> dict[str, str]:
    ids = {
        "region": "71000000-0000-0000-0000-000000000001",
        "substation": "71000000-0000-0000-0000-000000000002",
        "line": "71000000-0000-0000-0000-000000000003",
        "tower": "71000000-0000-0000-0000-000000000004",
        "active_asset_1": "71000000-0000-0000-0000-000000000011",
        "active_asset_2": "71000000-0000-0000-0000-000000000012",
        "inactive_asset": "71000000-0000-0000-0000-000000000013",
        "outside_asset": "71000000-0000-0000-0000-000000000014",
        "uav": "71000000-0000-0000-0000-000000000020",
    }
    inspector_email = roles_data.get("inspector", {}).get("email", "")
    if not inspector_email or inspector_email.startswith("replace-with-"):
        pytest.skip("Configure the inspector account in api-test/data/roles.json")

    with db_connection.cursor() as cursor:
        cursor.execute('SELECT "Id" FROM "Users" WHERE lower("Email") = lower(%s) AND NOT "IsDeleted"', (inspector_email,))
        inspector = cursor.fetchone()
        if inspector is None:
            pytest.skip(f"Inspector seed user does not exist: {inspector_email}")
        ids["inspector"] = str(inspector[0])
        cursor.execute(
            """
            INSERT INTO "Regions" ("Id","RegionName","Code","Type","Geom","CreatedAt","IsDeleted")
            VALUES (%s,'GIS API Test Region','GIS-TEST-REGION','District',ST_GeomFromText('POLYGON((106.79 10.83,106.82 10.83,106.82 10.86,106.79 10.86,106.79 10.83))',4326),now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "Geom"=EXCLUDED."Geom","IsDeleted"=false
            """, (ids["region"],))
        cursor.execute(
            """
            INSERT INTO "Substations" ("Id","RegionAssetId","SubstationName","VoltageLevel","Geom","CreatedAt","IsDeleted")
            VALUES (%s,%s,'GIS API Test Substation','110kV',ST_SetSRID(ST_Point(106.8,10.84),4326),now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "IsDeleted"=false
            """, (ids["substation"], ids["region"]))
        cursor.execute(
            """
            INSERT INTO "TransmissionLines" ("Id","SubstationAssetId","LineName","Code","VoltageLevel","Status","IsCriticalEdge","Geom","CreatedAt","IsDeleted")
            VALUES (%s,%s,'GIS API Test Line','GIS-TEST-LINE','110kV','Active',false,ST_GeomFromText('LINESTRING(106.80 10.84,106.81 10.85)',4326),now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "Status"='Active',"IsDeleted"=false
            """, (ids["line"], ids["substation"]))
        cursor.execute(
            """
            INSERT INTO "Towers" ("Id","LineAssetId","TowerCode","Geom","CreatedAt","IsDeleted")
            VALUES (%s,%s,'GIS-TEST-TOWER',ST_SetSRID(ST_Point(106.805,10.845),4326),now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "IsDeleted"=false
            """,
            (ids["tower"], ids["line"]),
        )
        assets = [
            (ids["active_asset_1"], "GIS-ASSET-A", "Active", 106.804, 10.844),
            (ids["active_asset_2"], "GIS-ASSET-B", "Operational", 106.808, 10.848),
            (ids["inactive_asset"], "GIS-ASSET-INACTIVE", "Inactive", 106.806, 10.846),
            (ids["outside_asset"], "GIS-ASSET-OUTSIDE", "Active", 107.0, 11.0),
        ]
        for asset_id, code, status, longitude, latitude in assets:
            cursor.execute(
                """
                INSERT INTO "AssetComponents" ("Id","TowerId","ComponentType","ComponentCode","Status","CurrentHealthScore","RiskLevel","PowerLineId","Location","CreatedAt","IsDeleted")
                VALUES (%s,%s,'Insulator',%s,%s,100,'Low Risk',%s,ST_SetSRID(ST_Point(%s,%s),4326),now(),false)
                ON CONFLICT ("Id") DO UPDATE SET "Status"=EXCLUDED."Status","Location"=EXCLUDED."Location","PowerLineId"=EXCLUDED."PowerLineId","IsDeleted"=false
                """,
                (asset_id, ids["tower"], code, status, ids["line"], longitude, latitude),
            )
        cursor.execute(
            """
            INSERT INTO "UAVs" ("Id","UavCode","Model","Status","BatteryLevel","CreatedAt","IsDeleted")
            VALUES (%s,'GIS-TEST-UAV','API Test','Idle',100,now(),false)
            ON CONFLICT ("Id") DO UPDATE SET "Status"='Idle',"BatteryLevel"=100,"IsDeleted"=false
            """,
            (ids["uav"],),
        )

    yield ids

    with db_connection.cursor() as cursor:
        cursor.execute('DELETE FROM "MissionTargets" WHERE "MissionId" IN (SELECT "Id" FROM "Missions" WHERE "Title" LIKE \'GIS API Test %%\')')
        cursor.execute('DELETE FROM "Missions" WHERE "Title" LIKE \'GIS API Test %%\'')
