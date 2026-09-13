from __future__ import annotations

import httpx


PATH = "/api/v1/pre-mission-assessments"


def test_pre_mission_assessment_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH)
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_create_rejects_empty_scope(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH,
        json={"regionId": "00000000-0000-0000-0000-000000000001", "plannedStart": "2099-01-01T08:00:00Z", "plannedEnd": "2099-01-01T10:00:00Z", "assetIds": []},
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 422), response.text


def test_pre_mission_assessment_routes_are_version_one(client: httpx.Client, role_headers) -> None:
    response = client.post(PATH + "/00000000-0000-0000-0000-000000000099/evaluate", json={}, headers=role_headers("manager"))
    assert response.status_code in (404, 403), response.text
