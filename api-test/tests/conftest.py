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
def token_for_role(client: httpx.Client, roles_data: dict[str, Any]):
    cache: dict[str, str] = {}

    def resolve(role_name: str) -> str:
        if role_name not in cache:
            cache[role_name] = login_for_role(client, select_role_data(roles_data, role_name))
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

    response = client.post("/api/v1/auth/login", json={"email": email, "password": password}, headers=headers)
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
def auth_token(client: httpx.Client, role_data: dict[str, Any]) -> str:
    return login_for_role(client, role_data)


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
