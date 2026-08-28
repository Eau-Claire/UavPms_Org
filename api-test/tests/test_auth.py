from __future__ import annotations

import httpx


def test_login_valid_credentials(client: httpx.Client, auth_token: str) -> None:
    assert auth_token


def test_login_invalid_credentials(client: httpx.Client, data: dict) -> None:
    response = client.post("/api/v1/auth/login", json=data["auth"]["invalidLogin"])
    assert response.status_code in {400, 401}, (
        f"POST /api/v1/auth/login invalid credentials returned {response.status_code}: {response.text}"
    )


def test_otp_verify_rejects_invalid_code(client: httpx.Client, data: dict) -> None:
    otp = data["auth"]["otp"]
    response = client.post(
        "/api/v1/auth/otp/verify",
        json={"email": otp["email"], "otp": otp["invalidCode"], "purpose": otp["purpose"]},
    )
    assert response.status_code in {400, 401, 404, 500}, (
        f"POST /api/v1/auth/otp/verify returned {response.status_code}: {response.text}"
    )
