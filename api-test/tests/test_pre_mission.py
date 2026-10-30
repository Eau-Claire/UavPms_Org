from __future__ import annotations

import uuid
import httpx

from tests.conftest import GIS_BOUNDARY_WKT, unique_planned_window

# Legacy controller (deprecated API version 0.9); /api/v1 is now served by the v2 controller.
PATH_LEGACY = "/api/v0.9/pre-mission-assessments"
PATH_V2 = "/api/v2/pre-mission-assessments"


# ==========================================
# 1. Legacy API Contract & Deprecation Tests
# ==========================================

def test_pre_mission_assessment_legacy_requires_authentication(client: httpx.Client) -> None:
    response = client.get(PATH_LEGACY)
    assert response.status_code == 401, response.text


def test_pre_mission_assessment_legacy_returns_sunset_headers(client: httpx.Client, role_headers) -> None:
    response = client.get(PATH_LEGACY, headers=role_headers("manager"))
    assert response.status_code == 200, response.text
    sunset = response.headers.get("sunset") or response.headers.get("Sunset")
    deprecation = response.headers.get("deprecation") or response.headers.get("Deprecation")
    assert sunset == "true"
    assert deprecation == "true"


def test_pre_mission_assessment_legacy_create_rejects_empty_scope(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_LEGACY,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "towerIds": [],
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


def test_pre_mission_assessment_v2_create_rejects_inspector_role(client: httpx.Client, role_headers) -> None:
    # Inspectors may read assessments but only Manager/SystemAdmin may create them.
    response = client.get(PATH_V2, headers=role_headers("inspector"))
    assert response.status_code == 200, response.text

    response = client.post(
        PATH_V2,
        json={"regionId": str(uuid.uuid4()), "towerIds": [str(uuid.uuid4())]},
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

def test_pre_mission_assessment_v2_create_rejects_empty_tower_scope(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "towerIds": [],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 422), response.text


def test_pre_mission_assessment_v2_create_rejects_duplicate_towers(client: httpx.Client, role_headers) -> None:
    tower_id = str(uuid.uuid4())
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T08:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "towerIds": [tower_id, tower_id],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code == 400, response.text
    assert "INVALID_PROPOSED_SCOPE" in response.text


def test_pre_mission_assessment_v2_create_rejects_inverted_time_window(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH_V2,
        json={
            "regionId": str(uuid.uuid4()),
            "plannedStart": "2099-01-01T12:00:00Z",
            "plannedEnd": "2099-01-01T10:00:00Z",
            "towerIds": [str(uuid.uuid4())],
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
            "towerIds": [str(uuid.uuid4())],
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
            "towerIds": [str(uuid.uuid4())],
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
# 5. Deterministic Tower Scope Checks With Seed
# ==========================================

def test_pre_mission_assessment_v2_create_rejects_unknown_tower(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    planned_start, planned_end = unique_planned_window()
    response = client.post(
        PATH_V2,
        json={
            "regionId": gis_seed["region"],
            "plannedStart": planned_start,
            "plannedEnd": planned_end,
            "towerIds": [gis_seed["tower_1"], "00000000-0000-0000-0000-000000000099"],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code == 403, response.text
    assert "TOWER_OUTSIDE_MANAGEMENT_SCOPE" in response.text


def test_pre_mission_assessment_v2_create_rejects_tower_outside_boundary(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    planned_start, planned_end = unique_planned_window()
    response = client.post(
        PATH_V2,
        json={
            "regionId": gis_seed["region"],
            "plannedStart": planned_start,
            "plannedEnd": planned_end,
            "towerIds": [gis_seed["tower_1"], gis_seed["outside_tower"]],
            "boundaryWkt": GIS_BOUNDARY_WKT,
        },
        headers=role_headers("manager"),
    )
    assert response.status_code == 400, response.text
    assert "TOWER_OUTSIDE_BOUNDARY" in response.text


# ==========================================
# 6. Deterministic End-to-End Flow With Seed
# ==========================================

def test_pre_mission_assessment_v2_full_lifecycle(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    # A. Create Assessment (creation evaluates immediately: READY or NOT_READY)
    planned_start, planned_end = unique_planned_window()
    idem_key = f"assessment-lifecycle-{uuid.uuid4()}"
    create_payload = {
        "regionId": gis_seed["region"],
        "plannedStart": planned_start,
        "plannedEnd": planned_end,
        "towerIds": [gis_seed["tower_1"], gis_seed["tower_2"]],
        "boundaryWkt": GIS_BOUNDARY_WKT,
        "idempotencyKey": idem_key,
    }
    create_res = client.post(PATH_V2, json=create_payload, headers=role_headers("manager"))
    assert create_res.status_code == 200, create_res.text
    assessment = create_res.json()
    assessment_id = assessment["id"]
    assert assessment["status"] in ("READY", "NOT_READY")
    assert [tower["towerId"] for tower in assessment["towers"]] == [gis_seed["tower_1"], gis_seed["tower_2"]]

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
