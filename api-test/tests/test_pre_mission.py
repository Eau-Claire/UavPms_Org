from __future__ import annotations

import uuid
import httpx
import pytest

from tests.conftest import assert_json_response

PATH_V1 = "/api/v1/pre-mission-assessments"
PATH_V2 = "/api/v2/pre-mission-assessments"


# ==========================================
# 1. API v1 Contract & Deprecation Tests
# ==========================================

def test_pre_mission_assessment_v1_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH_V1)
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_v1_returns_sunset_headers(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V1, headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    sunset = response.headers.get("sunset") or response.headers.get("Sunset")
    deprecation = response.headers.get("deprecation") or response.headers.get("Deprecation")
    assert sunset == "true"
    assert deprecation == "true"


def test_pre_mission_assessment_v1_create_rejects_empty_scope(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V1,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "assetIds": [],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 422), response.text


# ==========================================
# 2. API v2 Auth & Role-Based Access Tests
# ==========================================

def test_pre_mission_assessment_v2_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH_V2)
    assert response.status_code == 401, response.text

    response = client.post(PATH_V2, json={})
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_v2_rejects_inspector_role(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V2, headers=role_headers("inspector"))
    assert response.status_code == 403, response.text

    response = client.post(
        PATH_V2,
        json={"regionId": str(uuid.uuid4()), "assetIds": [str(uuid.uuid4())]},
        headers=role_headers("inspector"),
    )
    assert response.status_code == 403, response.text


def test_pre_mission_assessment_v2_rejects_analyst_role(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V2, headers=role_headers("analyst"))
    assert response.status_code == 403, response.text


def test_pre_mission_assessment_v2_list_as_manager(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_V2, headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    data = response.json()
    assert isinstance(data, list)


# ==========================================
# 3. API v2 Payload Validation Tests
# ==========================================

def test_pre_mission_assessment_v2_create_rejects_empty_asset_scope(client: httpx.Client, role_headers) -> None:
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


def test_pre_mission_assessment_v2_create_rejects_invalid_wkt(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "assetIds": [str(uuid.uuid4())],
            "boundaryWkt": "INVALID_POLYGON_STRING",
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 404, 422), response.text


# ==========================================
# 4. API v2 Not Found & Lifecycle State Tests
# ==========================================

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
        "droneIds": [str(uuid.uuid4())],
        "personnel": [
            {"userId": str(uuid.uuid4()), "role": "INSPECTOR", "isRequired": True}
        ],
        "idempotencyKey": f"test-mission-idem-{uuid.uuid4()}",
    }
    response = client.post(f"{PATH_V2}/{fake_id}/create-mission", json=payload, headers=role_headers("manager"))
    assert response.status_code == 404, response.text


# ==========================================
# 5. Deterministic End-to-End Flow With Seed
# ==========================================

def test_pre_mission_assessment_v2_full_lifecycle(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    # A. Create Assessment
    idem_key = f"assessment-lifecycle-{uuid.uuid4()}"
    create_payload = {
        "regionId": gis_seed["region"],
        "plannedStart": "2026-09-20T08:00:00Z",
        "plannedEnd": "2026-09-20T12:00:00Z",
        "assetIds": [gis_seed["active_asset_1"], gis_seed["active_asset_2"]],
        "boundaryWkt": "POLYGON((106.79 10.83, 106.82 10.83, 106.82 10.86, 106.79 10.86, 106.79 10.83))",
        "idempotencyKey": idem_key,
    }
    create_res = client.post(PATH_V2, json=create_payload, headers=role_headers("manager"))
    assert create_res.status_code == 200, create_res.text
    assessment = create_res.json()
    assessment_id = assessment["id"]
    assert assessment["status"] == "Draft"
    assert len(assessment["assets"]) == 2

    # B. Test Idempotency (duplicate creation returns existing record)
    dup_res = client.post(PATH_V2, json=create_payload, headers=role_headers("manager"))
    assert dup_res.status_code == 200, dup_res.text
    assert dup_res.json()["id"] == assessment_id

    # C. Get Assessment By ID
    get_res = client.get(f"{PATH_V2}/{assessment_id}", headers=role_headers("manager"))
    assert get_res.status_code == 200, get_res.text
    assert get_res.json()["id"] == assessment_id

    # D. Evaluate Assessment
    eval_res = client.post(f"{PATH_V2}/{assessment_id}/evaluate", headers=role_headers("manager"))
    assert eval_res.status_code == 200, eval_res.text
    evaluated = eval_res.json()
    assert "personnelCandidates" in evaluated
    assert "droneCandidates" in evaluated
    assert "siteFeasibilityStatus" in evaluated

    # E. Re-evaluate Assessment
    reval_res = client.post(f"{PATH_V2}/{assessment_id}/re-evaluate", headers=role_headers("manager"))
    assert reval_res.status_code == 200, reval_res.text
