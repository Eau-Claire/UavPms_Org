from __future__ import annotations

import uuid
import httpx


PATH_V1 = "/api/v1/pre-mission-assessments"
PATH_V2 = "/api/v2/pre-mission-assessments"


def test_pre_mission_assessment_v1_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH_V1)
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_v2_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH_V2)
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_v1_returns_sunset_headers(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V1, headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    assert response.headers.get("sunset") == "true" or response.headers.get("Sunset") == "true"
    assert response.headers.get("deprecation") == "true" or response.headers.get("Deprecation") == "true"


def test_pre_mission_assessment_v2_list_as_manager(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V2, headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    assert isinstance(response.json(), list)


def test_pre_mission_assessment_v2_rejects_inspector_role(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V2, headers=role_headers("inspector"))
    assert response.status_code == 403, response.text


def test_pre_mission_assessment_v2_create_rejects_empty_scope(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "assetIds": [],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 422), response.text


def test_pre_mission_assessment_v2_create_rejects_inverted_time_window(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T12:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "assetIds": [str(uuid.uuid4())],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 422), response.text


def test_pre_mission_assessment_v2_create_rejects_unknown_region(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": "00000000-0000-0000-0000-000000000099",
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "assetIds": [str(uuid.uuid4())],
            "boundaryWkt": "POLYGON((105.8 21.0, 105.9 21.0, 105.9 21.1, 105.8 21.1, 105.8 21.0))",
            "idempotencyKey": f"test-idem-{uuid.uuid4()}",
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 404), response.text


def test_pre_mission_assessment_v2_get_not_found(client: httpx.Client, role_headers) -> None:
    fake_id = "00000000-0000-0000-0000-000000000099"
    response = client.get(f"{PATH_V2}/{fake_id}", headers=role_headers("manager"))
    assert response.status_code == 404, response.text


def test_pre_mission_assessment_v2_evaluate_not_found(client: httpx.Client, role_headers) -> None:
    fake_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"{PATH_V2}/{fake_id}/evaluate", json={}, headers=role_headers("manager"))
    assert response.status_code == 404, response.text


def test_pre_mission_assessment_v2_re_evaluate_not_found(client: httpx.Client, role_headers) -> None:
    fake_id = "00000000-0000-0000-0000-000000000099"
    response = client.post(f"{PATH_V2}/{fake_id}/re-evaluate", json={}, headers=role_headers("manager"))
    assert response.status_code == 404, response.text


def test_pre_mission_assessment_v2_create_mission_not_found(client: httpx.Client, role_headers) -> None:
    fake_id = "00000000-0000-0000-0000-000000000099"
    payload = {
        "assessmentId": fake_id,
        "title": "Regression Mission Creation",
        "inspectorId": str(uuid.uuid4()),
        "droneId": str(uuid.uuid4()),
        "idempotencyKey": f"test-mission-idem-{uuid.uuid4()}",
    }
    response = client.post(f"{PATH_V2}/{fake_id}/create-mission", json=payload, headers=role_headers("manager"))
    assert response.status_code == 404, response.text
