from __future__ import annotations

import uuid
import httpx


PATH = "/api/v1/drone-technical-inspections"


def test_drone_technical_inspection_requires_authentication(client: httpx.Client) -> None:
    response = client.get(f"{PATH}/drone/{uuid.uuid4()}/latest")
    assert response.status_code == 401, response.text

    response = client.post(PATH, json={})
    assert response.status_code == 401, response.text


def test_drone_technical_inspection_submit_rejects_inspector_role(client: httpx.Client, role_headers) -> None:
    response = client.post(
        PATH,
        json={
            "droneId": str(uuid.uuid4()),
            "sourceType": "manual",
            "metrics": [],
        },
        headers=role_headers("inspector"),
    )
    assert response.status_code == 403, response.text


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
        headers=role_headers("technician"),
    )
    # The drone does not exist, so it should cleanly return 404 (DRONE_NOT_FOUND) or 400
    assert response.status_code in (400, 404), response.text
