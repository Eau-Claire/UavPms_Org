-- UAV PMS API regression users
-- PostgreSQL / Npgsql schema seed script.
--
-- Run only against local, test, or staging databases.
-- Replace every CHANGE_ME_* value before running this file.
--
-- The script creates trusted-device tokens so API tests can login without OTP
-- by sending X-Device-Trust-Token from api-test/data/roles.json.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

WITH seed_roles AS (
  SELECT *
  FROM (VALUES
    ('SystemAdmin', 'Full system administrator for regression tests.'),
    ('Manager', 'Manager role for operational regression tests.'),
    ('Inspector', 'Inspector role for mission and inspection regression tests.'),
    ('Analyst', 'Analyst role for monitoring and AI regression tests.'),
    ('Technician', 'Technician role for maintenance regression tests.')
  ) AS value("RoleName", "Description")
),
updated_roles AS (
  UPDATE "Roles" r
  SET "Description" = sr."Description"
  FROM seed_roles sr
  WHERE r."RoleName" = sr."RoleName"
  RETURNING r."RoleName"
)
INSERT INTO "Roles" ("RoleName", "Description")
SELECT sr."RoleName", sr."Description"
FROM seed_roles sr
WHERE NOT EXISTS (
  SELECT 1
  FROM "Roles" r
  WHERE r."RoleName" = sr."RoleName"
);

WITH seed_users AS (
  SELECT *
  FROM (VALUES
    (
      'CHANGE_ME_SYSTEMADMIN_EMAIL',
      'regression_systemadmin',
      'CHANGE_ME_TEST_PASSWORD',
      'Regression System Admin',
      '+84900000002',
      'SystemAdmin',
      'CHANGE_ME_SYSTEMADMIN_DEVICE_TOKEN'
    ),
    (
      'CHANGE_ME_MANAGER_EMAIL',
      'regression_manager',
      'CHANGE_ME_TEST_PASSWORD',
      'Regression Manager',
      '+84900000001',
      'Manager',
      'CHANGE_ME_MANAGER_DEVICE_TOKEN'
    ),
    (
      'CHANGE_ME_INSPECTOR_EMAIL',
      'regression_inspector',
      'CHANGE_ME_TEST_PASSWORD',
      'Regression Inspector',
      '+84900000003',
      'Inspector',
      'CHANGE_ME_INSPECTOR_DEVICE_TOKEN'
    ),
    (
      'CHANGE_ME_ANALYST_EMAIL',
      'regression_analyst',
      'CHANGE_ME_TEST_PASSWORD',
      'Regression Analyst',
      '+84900000004',
      'Analyst',
      'CHANGE_ME_ANALYST_DEVICE_TOKEN'
    ),
    (
      'CHANGE_ME_TECHNICIAN_EMAIL',
      'regression_technician',
      'CHANGE_ME_TEST_PASSWORD',
      'Regression Technician',
      '+84900000005',
      'Technician',
      'CHANGE_ME_TECHNICIAN_DEVICE_TOKEN'
    )
  ) AS value("Email", "Username", "Password", "FullName", "Phone", "RoleName", "DeviceTrustToken")
),
updated_users AS (
  UPDATE "Users" u
  SET
    "Username" = su."Username",
    "PasswordHash" = crypt(su."Password", gen_salt('bf', 10)),
    "FullName" = su."FullName",
    "Phone" = su."Phone",
    "Status" = 'Active',
    "IsEmailVerified" = true,
    "IsDeleted" = false,
    "DeletedAt" = NULL,
    "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
  FROM seed_users su
  WHERE u."Email" = su."Email"
  RETURNING u."Id", u."Email"
),
inserted_users AS (
  INSERT INTO "Users" (
    "Id", "Username", "Email", "PasswordHash", "FullName", "Phone", "Status",
    "IsEmailVerified", "CreatedAt", "UpdatedAt", "IsDeleted"
  )
  SELECT
    gen_random_uuid(),
    su."Username",
    su."Email",
    crypt(su."Password", gen_salt('bf', 10)),
    su."FullName",
    su."Phone",
    'Active',
    true,
    NOW() AT TIME ZONE 'UTC',
    NULL,
    false
  FROM seed_users su
  WHERE NOT EXISTS (
    SELECT 1
    FROM "Users" u
    WHERE u."Email" = su."Email"
  )
  RETURNING "Id", "Email"
),
selected_users AS (
  SELECT u."Id", su."Email", su."RoleName", su."DeviceTrustToken"
  FROM seed_users su
  JOIN "Users" u ON u."Email" = su."Email"
),
cleared_roles AS (
  DELETE FROM "UserRoles" ur
  USING selected_users su
  WHERE ur."UserId" = su."Id"
  RETURNING ur."UserId"
),
inserted_roles AS (
  INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
  SELECT
    su."Id",
    r."Id",
    NOW() AT TIME ZONE 'UTC'
  FROM selected_users su
  JOIN "Roles" r ON r."RoleName" = su."RoleName"
  WHERE NOT EXISTS (
    SELECT 1
    FROM "UserRoles" ur
    WHERE ur."UserId" = su."Id"
      AND ur."RoleId" = r."Id"
  )
  RETURNING "UserId"
),
cleared_devices AS (
  DELETE FROM "TrustedDevices" td
  USING selected_users su
  WHERE td."UserId" = su."Id"
    AND td."DeviceTokenHash" = encode(digest(su."DeviceTrustToken", 'sha256'), 'base64')
  RETURNING td."UserId"
)
INSERT INTO "TrustedDevices" (
  "Id",
  "UserId",
  "DeviceTokenHash",
  "ExpiresAt",
  "LastUsedAt",
  "UserAgent",
  "CreatedAt",
  "UpdatedAt",
  "IsDeleted"
)
SELECT
  gen_random_uuid(),
  su."Id",
  encode(digest(su."DeviceTrustToken", 'sha256'), 'base64'),
  (NOW() AT TIME ZONE 'UTC') + INTERVAL '365 days',
  NOW() AT TIME ZONE 'UTC',
  'api-regression-sql-seed',
  NOW() AT TIME ZONE 'UTC',
  NULL,
  false
FROM selected_users su;

COMMIT;

SELECT
  u."Email",
  u."FullName",
  u."Status",
  u."IsEmailVerified",
  r."RoleName",
  CASE
    WHEN td."Id" IS NULL THEN 'missing'
    ELSE 'ready'
  END AS "TrustedDevice"
FROM "Users" u
JOIN "UserRoles" ur ON ur."UserId" = u."Id"
JOIN "Roles" r ON r."Id" = ur."RoleId"
LEFT JOIN "TrustedDevices" td ON td."UserId" = u."Id"
WHERE u."Username" LIKE 'regression_%'
ORDER BY u."Email";
