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


def _polygon(min_lon: float, min_lat: float, max_lon: float, max_lat: float) -> dict:
    return {"geometry": {"type": "Polygon", "coordinates": [[[min_lon, min_lat], [max_lon, min_lat], [max_lon, max_lat], [min_lon, max_lat], [min_lon, min_lat]]]}}


def test_spatial_query_returns_only_selectable_assets(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    response = client.post("/api/v1/assets/spatial-query", json=_polygon(106.80, 10.84, 106.81, 10.85), headers=role_headers("manager"))
    assert response.status_code == 200, f"POST /api/v1/assets/spatial-query returned {response.status_code}: {response.text}"
    payload = assert_json_response(response)["data"]
    assets = payload["assets"]
    asset_ids = {str(asset["assetId"]) for asset in assets}
    assert gis_seed["active_asset_1"] in asset_ids
    assert gis_seed["active_asset_2"] in asset_ids
    assert gis_seed["inactive_asset"] not in asset_ids
    for asset in assets:
        assert {"assetId", "assetCode", "name", "latitude", "longitude", "status"} <= asset.keys()


def test_spatial_query_empty_area_returns_empty_result(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    response = client.post("/api/v1/assets/spatial-query", json=_polygon(105.0, 9.0, 105.1, 9.1), headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    payload = assert_json_response(response)["data"]
    assert payload["total"] == 0
    assert payload["assets"] == []


def test_spatial_query_rejects_invalid_geojson(client: httpx.Client, role_headers) -> None:
    invalid_payloads = [
        {"geometry": {"type": "Point", "coordinates": []}},
        {"geometry": {"type": "Polygon", "coordinates": []}},
        {"geometry": {"type": "Polygon", "coordinates": [[[106.8, 10.84], [106.81, 10.84], [106.81, 10.85], [106.8, 10.85]]]}},
    ]
    for payload in invalid_payloads:
        response = client.post("/api/v1/assets/spatial-query", json=payload, headers=role_headers("manager"))
        assert response.status_code == 400, f"Invalid geometry returned {response.status_code}: {response.text}"
        assert assert_json_response(response)["message"] == "INVALID_GEOMETRY"
