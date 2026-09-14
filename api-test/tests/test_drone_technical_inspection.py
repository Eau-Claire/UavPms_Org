from __future__ import annotations

import uuid
import httpx
import pytest

PATH = "/api/v1/drone-technical-inspections"


# ==========================================
# 1. Auth & Role-Based Access Tests
# ==========================================

def test_drone_technical_inspection_requires_authentication(client: httpx.Client) -> None:
    response = client.get(f"{PATH}/drone/{uuid.uuid4()}/latest")
    assert response.status_code == 401, response.text

    response = client.post(PATH, json={})
    assert response.status_code == 401, response.text


def test_drone_technical_inspection_submit_rejects_inspector_role(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH,
        json={"droneId": str(uuid.uuid4()), "sourceType": "manual", "metrics": []},
        headers=role_headers("inspector"),
    )
    assert response.status_code == 403, response.text


def test_drone_technical_inspection_submit_rejects_analyst_role(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH,
        json={"droneId": str(uuid.uuid4()), "sourceType": "manual", "metrics": []},
        headers=role_headers("analyst"),
    )
    assert response.status_code == 403, response.text


# ==========================================
# 2. Payload Validation & Not Found Tests
# ==========================================

def test_drone_technical_inspection_get_latest_not_found(client: httpx.Client, role_headers) -> None:
    fake_drone_id = "00000000-0000-0000-0000-000000000099"
    response = client.get(f"{PATH}/drone/{fake_drone_id}/latest", headers=role_headers("technician"))
    assert response.status_code == 404, response.text


def test_drone_technical_inspection_submit_validates_payload(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH,
        json={
            "droneId": "00000000-0000-0000-0000-000000000099",
            "sourceType": "manual",
            "metrics": [
                {
                    "metricCode": "BATTERY_LEVEL",
                    "subsystem": "Battery",
                    "numericValue": 95.0,
                    "severity": "None",
                    "isRequired": True,
                },
                {
                    "metricCode": "PROPULSION_STATUS",
                    "subsystem": "Motor",
                    "boolValue": True,
                    "severity": "None",
                    "isRequired": True,
                }
            ],
            "notes": "Automated regression inspection",
        },
        headers=role_headers("manager"),
    )
    # The drone does not exist, so it should cleanly return 404 (DRONE_NOT_FOUND) or 400
    assert response.status_code in (400, 404), response.text


def test_drone_technical_inspection_rejects_multiple_value_types(client: httpx.Client, role_headers) -> None:
    # A single metric cannot provide both numericValue and boolValue
    response = client.post(
        PATH,
        json={
            "droneId": str(uuid.uuid4()),
            "metrics": [
                {
                    "metricCode": "CONFLICTING_METRIC",
                    "subsystem": "General",
                    "numericValue": 10.0,
                    "boolValue": True,
                    "valueText": "Conflict",
                }
            ],
        },
        headers=role_headers("manager"),
    )
    assert response.status_code in (400, 404, 422), response.text


# ==========================================
# 3. Deterministic End-to-End Flow With Seed
# ==========================================

def test_drone_technical_inspection_submit_and_query_latest(gis_seed: dict, client: httpx.Client, role_headers) -> None:
    drone_id = gis_seed["uav"]

    # 1. Technician submits passing inspection
    submit_payload = {
        "droneId": drone_id,
        "sourceType": "manual",
        "sourceVersion": "1.0",
        "notes": "Full pre-flight readiness inspection",
        "metrics": [
            {
                "metricCode": "BATTERY_HEALTH",
                "subsystem": "Battery",
                "numericValue": 98.5,
                "unit": "%",
                "passed": True,
                "critical": True,
                "severity": "None",
                "isRequired": True,
            },
            {
                "metricCode": "MOTOR_SPIN",
                "subsystem": "Propulsion",
                "boolValue": True,
                "passed": True,
                "critical": True,
                "severity": "None",
                "isRequired": True,
            },
            {
                "metricCode": "CAMERA_GIMBAL",
                "subsystem": "Payload",
                "valueText": "Calibrated",
                "passed": True,
                "critical": False,
                "severity": "None",
                "isRequired": False,
            }
        ]
    }
    submit_res = client.post(PATH, json=submit_payload, headers=role_headers("technician"))
    assert submit_res.status_code == 200, submit_res.text
    inspection = submit_res.json()
    assert inspection["status"] == "Passed"
    assert inspection["health"] == "Healthy"
    assert len(inspection["metrics"]) == 3

    # 2. Retrieve latest inspection for drone
    latest_res = client.get(f"{PATH}/drone/{drone_id}/latest", headers=role_headers("technician"))
    assert latest_res.status_code == 200, latest_res.text
    latest = latest_res.json()
    assert latest["id"] == inspection["id"]
    assert latest["health"] == "Healthy"
    assert latest["status"] == "Passed"

    # 3. Submit critical failing inspection
    failing_payload = {
        "droneId": drone_id,
        "sourceType": "manual",
        "notes": "Motor failure detected during pre-flight test",
        "metrics": [
            {
                "metricCode": "MOTOR_SPIN",
                "subsystem": "Propulsion",
                "boolValue": False,
                "passed": False,
                "critical": True,
                "severity": "Critical",
                "isRequired": True,
            }
        ]
    }
    fail_res = client.post(PATH, json=failing_payload, headers=role_headers("technician"))
    assert fail_res.status_code == 200, fail_res.text
    failed_insp = fail_res.json()
    assert failed_insp["status"] == "Failed"
    assert failed_insp["health"] == "Critical"

    # 4. Confirm latest inspection reflects critical health
    latest_fail_res = client.get(f"{PATH}/drone/{drone_id}/latest", headers=role_headers("technician"))
    assert latest_fail_res.status_code == 200, latest_fail_res.text
    assert latest_fail_res.json()["health"] == "Critical"
