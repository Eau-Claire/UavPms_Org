from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path

import httpx

from tests.conftest import DATA_DIR


def test_upload_inspection_image_request_is_handled(
    client: httpx.Client,
    auth_headers: dict[str, str],
    data: dict,
) -> None:
    inspection = data["inspection"]
    image_path = DATA_DIR / inspection["sampleImage"]
    if image_path.exists():
        file_bytes = image_path.read_bytes()
    else:
        file_bytes = b"fake-image-data"

    files = {"file": ("sample.jpg", file_bytes, "image/jpeg")}
    form = {
        "missionId": inspection["fakeMissionId"],
        "assetId": inspection["fakeAssetId"],
        "capturedAt": datetime.now(timezone.utc).isoformat(),
    }
    response = client.post("/api/v1/inspections/upload", data=form, files=files, headers=auth_headers)
    assert response.status_code in {200, 400, 404, 422}, (
        f"POST /api/v1/inspections/upload returned {response.status_code}: {response.text}"
    )


def test_get_inspection_report_fake_id_is_handled(
    client: httpx.Client,
    auth_headers: dict[str, str],
    data: dict,
) -> None:
    fake_id = data["inspection"]["fakeMissionId"]
    response = client.get(f"/api/v1/inspections/report/{fake_id}", headers=auth_headers)
    assert response.status_code in {200, 404}, (
        f"GET /api/v1/inspections/report/{{id}} returned {response.status_code}: {response.text}"
    )


def test_inspection_report_requires_authentication(client: httpx.Client, data: dict) -> None:
    fake_id = data["inspection"]["fakeMissionId"]
    response = client.get(f"/api/v1/inspections/report/{fake_id}")
    assert response.status_code == 401, (
        f"GET /api/v1/inspections/report/{{id}} without token returned {response.status_code}: {response.text}"
    )
