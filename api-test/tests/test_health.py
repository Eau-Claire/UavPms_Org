from __future__ import annotations

import httpx


def test_get_health_gateway(client: httpx.Client) -> None:
    response = client.get("/health")
    assert response.status_code == 200, f"GET /health returned {response.status_code}: {response.text}"


def test_get_gateway_swagger_json(client: httpx.Client) -> None:
    response = client.get("/swagger/gateway/swagger.json")
    assert response.status_code == 200, (
        f"GET /swagger/gateway/swagger.json returned {response.status_code}: {response.text}"
    )
    body = response.json()
    assert body.get("openapi")
    assert "UAV PMS" in body.get("info", {}).get("title", "")
