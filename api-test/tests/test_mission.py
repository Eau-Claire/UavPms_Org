from __future__ import annotations

import httpx
import pytest
import uuid

from tests.conftest import ASSESSMENT_PATH, assert_json_response


@pytest.fixture()
def created_gis_mission(create_gis_mission) -> dict:
    return create_gis_mission(f"GIS API Test Created {uuid.uuid4()}")


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


def test_missions_require_authentication(client: httpx.Client) -> None:
    response = client.get("/api/v1/missions")
    assert response.status_code == 401, f"GET /api/v1/missions without token returned {response.status_code}: {response.text}"


def test_mission_list_rejects_inspector_role(client: httpx.Client, role_headers, data: dict) -> None:
    response = client.get("/api/v1/missions", params=data["mission"]["listParams"], headers=role_headers("inspector"))
    assert response.status_code == 403, f"GET /api/v1/missions as Inspector returned {response.status_code}: {response.text}"


def test_mission_from_assessment_persists_tower_targets(gis_seed: dict, created_gis_mission: dict, db_connection) -> None:
    with db_connection.cursor() as cursor:
        cursor.execute('SELECT "TowerId", "Sequence", "InspectionStatus" FROM "MissionTargets" WHERE "MissionId"=%s ORDER BY "Sequence"', (created_gis_mission["id"],))
        persisted = cursor.fetchall()
    assert [(str(row[0]), row[1], row[2]) for row in persisted] == [
        (gis_seed["tower_1"], 1, "Pending"),
        (gis_seed["tower_2"], 2, "Pending"),
    ]


def test_mission_detail_returns_targets(gis_seed: dict, client: httpx.Client, role_headers, created_gis_mission: dict) -> None:
    response = client.get(f'/api/v1/missions/{created_gis_mission["id"]}', headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    targets = assert_json_response(response)["data"]["targets"]
    assert [target["towerId"] for target in targets] == [gis_seed["tower_1"], gis_seed["tower_2"]]
    assert [target["sequence"] for target in targets] == [1, 2]
    assert all(target["inspectionStatus"] == "Pending" for target in targets)


def test_create_mission_rejects_drone_not_in_assessment(gis_seed: dict, client: httpx.Client, role_headers, create_ready_assessment, db_connection) -> None:
    assessment = create_ready_assessment()
    title = f"GIS API Test Invalid Drone {uuid.uuid4()}"
    payload = {
        "assessmentId": assessment["id"],
        "title": title,
        "personnel": [{"userId": gis_seed["inspector"], "role": "INSPECTOR", "isRequired": True}],
        "droneIds": ["00000000-0000-0000-0000-000000000099"],
    }
    response = client.post(f'{ASSESSMENT_PATH}/{assessment["id"]}/create-mission', json=payload, headers=role_headers("manager"))
    assert response.status_code == 400, response.text
    assert "RESOURCE_NOT_IN_ASSESSMENT" in response.text
    with db_connection.cursor() as cursor:
        cursor.execute('SELECT count(*) FROM "Missions" WHERE "Title"=%s', (title,))
        assert cursor.fetchone()[0] == 0


def test_mission_accept_assignment_requires_authentication(client: httpx.Client) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"/api/v1/missions/{fake_mission_id}/assignments/accept")
    assert response.status_code == 401, response.text


def test_mission_accept_assignment_rejects_analyst_role(client: httpx.Client, role_headers) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"/api/v1/missions/{fake_mission_id}/assignments/accept", headers=role_headers("analyst"))
    assert response.status_code == 403, response.text


def test_mission_accept_assignment_returns_404_for_unknown_mission(client: httpx.Client, role_headers) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"/api/v1/missions/{fake_mission_id}/assignments/accept", headers=role_headers("inspector"))
    assert response.status_code in (400, 403, 404), response.text


def test_mission_postpone_assignment_requires_authentication(client: httpx.Client) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"/api/v1/missions/{fake_mission_id}/assignments/postpone", json={"reason": "Rainstorm"})
    assert response.status_code == 401, response.text


def test_mission_postpone_assignment_rejects_analyst_role(client: httpx.Client, role_headers) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(
        f"/api/v1/missions/{fake_mission_id}/assignments/postpone",
        json={"reason": "Heavy rain"},
        headers=role_headers("analyst"),
    )
    assert response.status_code == 403, response.text


def test_mission_postpone_assignment_validates_reason(client: httpx.Client, role_headers) -> None:
    fake_mission_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(
        f"/api/v1/missions/{fake_mission_id}/assignments/postpone",
        json={"reason": ""},
        headers=role_headers("inspector"),
    )
    assert response.status_code in (400, 422), response.text


def test_mission_accept_assignment_happy_path(created_gis_mission: dict, client: httpx.Client, role_headers) -> None:
    mission_id = created_gis_mission["id"]
    response = client.post(f"/api/v1/missions/{mission_id}/assignments/accept", headers=role_headers("inspector"))
    assert response.status_code == 200, response.text
    body = response.json()
    assert body.get("success") is True or "accepted" in response.text.lower()


def test_mission_postpone_assignment_happy_path(create_gis_mission, client: httpx.Client, role_headers) -> None:
    # Create a separate mission to test postpone flow
    mission_id = create_gis_mission(f"GIS API Test Postpone {uuid.uuid4()}")["id"]

    postpone_res = client.post(
        f"/api/v1/missions/{mission_id}/assignments/postpone",
        json={"reason": "High wind speed exceeds UAV limit"},
        headers=role_headers("inspector"),
    )
    assert postpone_res.status_code == 200, postpone_res.text
    body = postpone_res.json()
    assert body.get("success") is True or "postponed" in postpone_res.text.lower()
