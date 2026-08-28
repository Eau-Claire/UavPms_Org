from __future__ import annotations

import httpx

from tests.conftest import assert_json_response


def test_list_missions(client: httpx.Client, auth_headers: dict[str, str], data: dict) -> None:
    response = client.get("/api/v1/missions", params=data["mission"]["listParams"], headers=auth_headers)
    assert response.status_code == 200, f"GET /api/v1/missions returned {response.status_code}: {response.text}"
    assert_json_response(response)


def test_list_missions_search_filter(client: httpx.Client, auth_headers: dict[str, str], data: dict) -> None:
    params = {**data["mission"]["listParams"], "search": data["mission"]["search"]}
    response = client.get("/api/v1/missions", params=params, headers=auth_headers)
    assert response.status_code == 200, f"GET /api/v1/missions search returned {response.status_code}: {response.text}"


def test_my_missions(client: httpx.Client, auth_headers: dict[str, str]) -> None:
    response = client.get("/api/v1/missions/my", headers=auth_headers)
    assert response.status_code == 200, f"GET /api/v1/missions/my returned {response.status_code}: {response.text}"


def test_create_mission_validates_required_fields(client: httpx.Client, auth_headers: dict[str, str], data: dict) -> None:
    response = client.post("/api/v1/missions", json=data["mission"]["invalidCreatePayload"], headers=auth_headers)
    assert response.status_code in {400, 422}, (
        f"POST /api/v1/missions empty payload returned {response.status_code}: {response.text}"
    )


def test_missions_require_authentication(client: httpx.Client) -> None:
    response = client.get("/api/v1/missions")
    assert response.status_code == 401, f"GET /api/v1/missions without token returned {response.status_code}: {response.text}"
