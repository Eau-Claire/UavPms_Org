from __future__ import annotations

import httpx
import pytest
import uuid

from tests.conftest import assert_json_response


def _mission_payload(gis_seed: dict, title: str, target_ids: list[str] | None = None) -> dict:
    return {
        "name": title,
        "description": "Deterministic GIS API automation mission",
        "scheduledAt": "2026-09-03T08:00:00+07:00",
        "inspectorId": gis_seed["inspector"],
        "droneId": gis_seed["uav"],
        "targetAssetIds": target_ids if target_ids is not None else [gis_seed["active_asset_1"], gis_seed["active_asset_2"]],
    }


@pytest.fixture()
def created_gis_mission(gis_seed: dict, client: httpx.Client, role_headers) -> dict:
    payload = _mission_payload(gis_seed, f"GIS API Test Created {uuid.uuid4()}")
    response = client.post("/api/v1/missions", json=payload, headers=role_headers("manager"))
    assert response.status_code == 200, f"POST /api/v1/missions returned {response.status_code}: {response.text}"
    return assert_json_response(response)["data"]


def test_list_missions_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.get("/api/v1/missions", params=data["mission"]["listParams"], headers=role_headers("manager"))
    assert response.status_code == 200, f"GET /api/v1/missions returned {response.status_code}: {response.text}"
    assert_json_response(response)


def test_list_missions_search_filter_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    params = {**data["mission"]["listParams"], "search": data["mission"]["search"]}
    response = client.get("/api/v1/missions", params=params, headers=role_headers("manager"))
    assert response.status_code == 200, f"GET /api/v1/missions search returned {response.status_code}: {response.text}"


def test_my_missions_as_inspector(client: httpx.Client, role_headers) -> None:
    response = client.get("/api/v1/missions/my", headers=role_headers("inspector"))
    assert response.status_code == 200, f"GET /api/v1/missions/my returned {response.status_code}: {response.text}"


def test_create_mission_validates_required_fields_as_manager(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.post("/api/v1/missions", json=data["mission"]["invalidCreatePayload"], headers=role_headers("manager"))
    assert response.status_code in {400, 422}, (
        f"POST /api/v1/missions empty payload returned {response.status_code}: {response.text}"
    )


def test_missions_require_authentication(client: httpx.Client) -> None:
    response = client.get("/api/v1/missions")
    assert response.status_code == 401, f"GET /api/v1/missions without token returned {response.status_code}: {response.text}"


def test_mission_list_rejects_inspector_role(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.get("/api/v1/missions", params=data["mission"]["listParams"], headers=role_headers("inspector"))
    assert response.status_code == 403, f"GET /api/v1/missions as Inspector returned {response.status_code}: {response.text}"


def test_create_mission_persists_multiple_targets(gis_seed: dict, created_gis_mission: dict, db_connection) -> None:
    targets = created_gis_mission["targets"]
    assert [target["assetId"] for target in targets] == [gis_seed["active_asset_1"], gis_seed["active_asset_2"]]
    assert [target["sequence"] for target in targets] == [1, 2]
    assert all(target["inspectionStatus"] == "Pending" for target in targets)
    with db_connection.cursor() as cursor:
        cursor.execute('SELECT "AssetId", "Sequence", "InspectionStatus" FROM "MissionTargets" WHERE "MissionId"=%s ORDER BY "Sequence"', (created_gis_mission["id"],))
        persisted = cursor.fetchall()
    assert [(str(row[0]), row[1], row[2]) for row in persisted] == [
        (gis_seed["active_asset_1"], 1, "Pending"),
        (gis_seed["active_asset_2"], 2, "Pending"),
    ]


def test_mission_detail_returns_targets(gis_seed: dict, client: httpx.Client, role_headers, created_gis_mission: dict) -> None:
    response = client.get(f'/api/v1/missions/{created_gis_mission["id"]}', headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    targets = assert_json_response(response)["data"]["targets"]
    assert [target["assetId"] for target in targets] == [gis_seed["active_asset_1"], gis_seed["active_asset_2"]]
    assert [target["sequence"] for target in targets] == [1, 2]
    assert all(target["inspectionStatus"] == "Pending" for target in targets)


def test_create_mission_rejects_duplicate_targets_without_rows(gis_seed: dict, client: httpx.Client, role_headers, db_connection) -> None:
    title = f"GIS API Test Duplicate {uuid.uuid4()}"
    payload = _mission_payload(gis_seed, title, [gis_seed["active_asset_1"], gis_seed["active_asset_1"]])
    response = client.post("/api/v1/missions", json=payload, headers=role_headers("manager"))
    assert response.status_code == 400, response.text
    with db_connection.cursor() as cursor:
        cursor.execute('SELECT count(*) FROM "Missions" WHERE "Title"=%s', (title,))
        assert cursor.fetchone()[0] == 0


def test_create_mission_rejects_empty_targets(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    response = client.post("/api/v1/missions", json=_mission_payload(gis_seed, "GIS API Test Empty", []), headers=role_headers("manager"))
    assert response.status_code in {400, 422}, response.text
    assert "MISSION_TARGET_REQUIRED" in response.text


@pytest.mark.parametrize("target_id, expected_error", [
    ("00000000-0000-0000-0000-000000000099", "ASSET_NOT_FOUND"),
])
def test_create_mission_rejects_missing_asset(gis_seed: dict, client: httpx.Client, role_headers, target_id: str, expected_error: str) -> None:
    response = client.post("/api/v1/missions", json=_mission_payload(gis_seed, "GIS API Test Missing", [target_id]), headers=role_headers("manager"))
    assert response.status_code == 404, response.text
    assert expected_error in response.text


def test_create_mission_rejects_inactive_asset(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    response = client.post("/api/v1/missions", json=_mission_payload(gis_seed, "GIS API Test Inactive", [gis_seed["inactive_asset"]]), headers=role_headers("manager"))
    assert response.status_code == 400, response.text
    assert "ASSET_NOT_AVAILABLE" in response.text


def test_create_mission_rejects_invalid_inspector_and_drone(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    payload = _mission_payload(gis_seed, "GIS API Test Invalid Inspector")
    payload["inspectorId"] = "00000000-0000-0000-0000-000000000099"
    inspector_response = client.post("/api/v1/missions", json=payload, headers=role_headers("manager"))
    assert inspector_response.status_code == 404, inspector_response.text

    payload = _mission_payload(gis_seed, "GIS API Test Invalid Drone")
    payload["droneId"] = "00000000-0000-0000-0000-000000000099"
    drone_response = client.post("/api/v1/missions", json=payload, headers=role_headers("manager"))
    assert drone_response.status_code == 404, drone_response.text


def test_failed_target_validation_leaves_no_partial_mission(gis_seed: dict, client: httpx.Client, role_headers, db_connection) -> None:
    title = f"GIS API Test Atomic {uuid.uuid4()}"
    payload = _mission_payload(gis_seed, title, [gis_seed["active_asset_1"], "00000000-0000-0000-0000-000000000099"])
    response = client.post("/api/v1/missions", json=payload, headers=role_headers("manager"))
    assert response.status_code == 404, response.text
    with db_connection.cursor() as cursor:
        cursor.execute('SELECT count(*) FROM "Missions" WHERE "Title"=%s', (title,))
        assert cursor.fetchone()[0] == 0
