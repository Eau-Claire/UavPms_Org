from __future__ import annotations

import httpx

from tests.conftest import assert_json_response


def test_list_assets_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.get("/api/v1/assets", params=data["asset"]["listParams"], headers=role_headers("manager"))
    assert response.status_code == 200, f"GET /api/v1/assets returned {response.status_code}: {response.text}"
    assert_json_response(response)


def test_list_assets_by_type_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    params = {**data["asset"]["listParams"], "assetType": data["asset"]["assetType"]}
    response = client.get("/api/v1/assets", params=params, headers=role_headers("manager"))
    assert response.status_code == 200, f"GET /api/v1/assets by type returned {response.status_code}: {response.text}"


def test_get_asset_fake_id_is_handled_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    fake_id = data["asset"]["fakeId"]
    response = client.get(f"/api/v1/assets/{fake_id}", headers=role_headers("manager"))
    assert response.status_code in {200, 404}, f"GET /api/v1/assets/{{id}} returned {response.status_code}: {response.text}"


def test_create_asset_validates_required_fields_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.post("/api/v1/assets", json=data["asset"]["invalidCreatePayload"], headers=role_headers("manager"))
    assert response.status_code in {400, 422}, (
        f"POST /api/v1/assets empty payload returned {response.status_code}: {response.text}"
    )


def test_assets_require_authentication(client: httpx.Client) -> None:
    response = client.get("/api/v1/assets")
    assert response.status_code == 401, f"GET /api/v1/assets without token returned {response.status_code}: {response.text}"


def test_create_asset_rejects_inspector_role(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.post("/api/v1/assets", json=data["asset"]["invalidCreatePayload"], headers=role_headers("inspector"))
    assert response.status_code == 403, f"POST /api/v1/assets as Inspector returned {response.status_code}: {response.text}"
