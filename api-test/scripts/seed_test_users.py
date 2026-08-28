from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any
from uuid import uuid4

import bcrypt
import psycopg


DEFAULT_ROLES = {
    "SystemAdmin": "Full system administrator for regression tests.",
    "Manager": "Manager role for operational regression tests.",
    "Inspector": "Inspector role for mission and inspection regression tests.",
    "Analyst": "Analyst role for monitoring and AI regression tests.",
    "Technician": "Technician role for maintenance regression tests.",
}


def main() -> None:
    parser = argparse.ArgumentParser(description="Seed UAV PMS API regression users and trusted devices.")
    parser.add_argument("--roles-file", default=str(default_roles_file()))
    parser.add_argument("--connection", default=os.getenv("DB_CONNECTION") or os.getenv("ConnectionStrings__DefaultConnection"))
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not args.connection:
        raise SystemExit("Missing DB connection. Set DB_CONNECTION or pass --connection.")

    roles_path = Path(args.roles_file)
    roles_data = json.loads(roles_path.read_text(encoding="utf-8"))

    with psycopg.connect(normalize_connection_string(args.connection)) as conn:
        with conn.cursor() as cur:
            role_ids = ensure_roles(cur)
            generated: dict[str, Any] = {}
            for key, spec in roles_data.items():
                if not isinstance(spec, dict):
                    continue
                email = spec["email"].strip().lower()
                username = spec.get("username") or email.split("@", 1)[0].replace(".", "_")
                password = spec["password"]
                full_name = spec.get("fullName") or f"Regression {key}"
                phone = spec.get("phone") or f"+8499{abs(hash(email)) % 10000000:07d}"
                requested_roles = spec.get("roles") or [key]
                device_token = spec.get("deviceTrustToken") or f"uavpms-regression-{key}-device"

                user_id = upsert_user(cur, email, username, password, full_name, phone)
                assign_roles(cur, user_id, requested_roles, role_ids)
                upsert_trusted_device(cur, user_id, device_token)

                generated[key] = {
                    **spec,
                    "email": email,
                    "username": username,
                    "password": password,
                    "deviceTrustToken": device_token,
                    "roles": requested_roles,
                }
                print(f"seeded {email} -> {', '.join(requested_roles)}")

            if args.dry_run:
                conn.rollback()
                print("dry-run complete; rolled back.")
            else:
                conn.commit()
                roles_path.write_text(json.dumps(generated, indent=2) + "\n", encoding="utf-8")
                print(f"updated {roles_path}")


def ensure_roles(cur: psycopg.Cursor) -> dict[str, int]:
    for role_name, description in DEFAULT_ROLES.items():
        cur.execute(
            """
            UPDATE "Roles"
            SET "Description" = %s
            WHERE "RoleName" = %s
            """,
            (description, role_name),
        )
        cur.execute(
            """
            INSERT INTO "Roles" ("RoleName", "Description")
            SELECT %s, %s
            WHERE NOT EXISTS (
                SELECT 1 FROM "Roles" WHERE "RoleName" = %s
            )
            """,
            (role_name, description, role_name),
        )

    cur.execute('SELECT "Id", "RoleName" FROM "Roles"')
    return {row[1]: row[0] for row in cur.fetchall()}


def upsert_user(cur: psycopg.Cursor, email: str, username: str, password: str, full_name: str, phone: str) -> str:
    password_hash = bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt(rounds=10)).decode("utf-8")
    now = datetime.now(timezone.utc)
    user_id = str(uuid4())
    cur.execute(
        """
        UPDATE "Users"
        SET
            "Username" = %s,
            "PasswordHash" = %s,
            "FullName" = %s,
            "Phone" = %s,
            "Status" = 'Active',
            "IsEmailVerified" = true,
            "IsDeleted" = false,
            "DeletedAt" = NULL,
            "UpdatedAt" = %s
        WHERE "Email" = %s
        RETURNING "Id"
        """,
        (username, password_hash, full_name, phone, now, email),
    )
    row = cur.fetchone()
    if row:
        return str(row[0])

    cur.execute(
        """
        INSERT INTO "Users" (
            "Id", "Username", "Email", "PasswordHash", "FullName", "Phone", "Status",
            "IsEmailVerified", "CreatedAt", "IsDeleted"
        )
        SELECT %s, %s, %s, %s, %s, %s, 'Active', true, %s, false
        WHERE NOT EXISTS (
            SELECT 1 FROM "Users" WHERE "Email" = %s
        )
        RETURNING "Id"
        """,
        (user_id, username, email, password_hash, full_name, phone, now, email),
    )
    return str(cur.fetchone()[0])


def assign_roles(cur: psycopg.Cursor, user_id: str, role_names: list[str], role_ids: dict[str, int]) -> None:
    missing = [role for role in role_names if role not in role_ids]
    if missing:
        raise SystemExit(f"Missing role ids after seed: {', '.join(missing)}")

    cur.execute('DELETE FROM "UserRoles" WHERE "UserId" = %s', (user_id,))
    for role_name in role_names:
        cur.execute(
            """
            INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
            SELECT %s, %s, %s
            WHERE NOT EXISTS (
                SELECT 1
                FROM "UserRoles"
                WHERE "UserId" = %s AND "RoleId" = %s
            )
            """,
            (user_id, role_ids[role_name], datetime.now(timezone.utc), user_id, role_ids[role_name]),
        )


def upsert_trusted_device(cur: psycopg.Cursor, user_id: str, device_token: str) -> None:
    token_hash = sha256_base64(device_token)
    now = datetime.now(timezone.utc)
    expires = now + timedelta(days=365)
    cur.execute(
        """
        DELETE FROM "TrustedDevices"
        WHERE "UserId" = %s AND "DeviceTokenHash" = %s
        """,
        (user_id, token_hash),
    )
    cur.execute(
        """
        INSERT INTO "TrustedDevices" (
            "Id", "UserId", "DeviceTokenHash", "ExpiresAt", "LastUsedAt",
            "UserAgent", "CreatedAt", "IsDeleted"
        )
        VALUES (%s, %s, %s, %s, %s, %s, %s, false)
        """,
        (str(uuid4()), user_id, token_hash, expires, now, "api-regression-seed", now),
    )


def sha256_base64(value: str) -> str:
    return base64.b64encode(hashlib.sha256(value.encode("utf-8")).digest()).decode("ascii")


def normalize_connection_string(value: str) -> str:
    if ";" not in value or "=" not in value:
        return value

    pairs: dict[str, str] = {}
    for part in value.strip().strip(";").split(";"):
        if not part or "=" not in part:
            continue
        key, raw = part.split("=", 1)
        pairs[key.strip().lower()] = raw.strip()

    mapping = {
        "host": "host",
        "port": "port",
        "database": "dbname",
        "username": "user",
        "user id": "user",
        "password": "password",
        "searchpath": "options",
    }
    conninfo: list[str] = []
    for source_key, target_key in mapping.items():
        if source_key not in pairs:
            continue
        raw_value = pairs[source_key]
        if source_key == "searchpath":
            raw_value = f"-c search_path={raw_value}"
        conninfo.append(f"{target_key}={quote_conninfo_value(raw_value)}")
    return " ".join(conninfo)


def quote_conninfo_value(value: str) -> str:
    escaped = value.replace("\\", "\\\\").replace("'", "\\'")
    return f"'{escaped}'"


def default_roles_file() -> Path:
    data_dir = Path(__file__).resolve().parents[1] / "data"
    local = data_dir / "roles.json"
    return local if local.exists() else data_dir / "roles.example.json"


if __name__ == "__main__":
    main()
