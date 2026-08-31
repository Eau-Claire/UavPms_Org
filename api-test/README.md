# UAV PMS API Regression Tester

This folder contains an independent FastAPI service that runs HTTP API regression tests against the existing UAV PMS backend. It is observational only: it never starts, stops, restarts, blocks, or controls the backend containers.

docker compose -f docker-compose.yml -f docker-compose.test.yml up -d --build

## Architecture

- Backend/gateway keeps running through the normal `docker-compose.yml`.
- `api-test` runs from `docker-compose.test.yml` on port `8081`.
- Tests call `TARGET_API` over HTTP, usually `http://gateway:8080` inside Docker.
- Results are stored in SQLite under `/app/reports/results.sqlite3`.
- Pytest logs and JUnit XML files are stored under `/app/reports`.
- Test failures are recorded in the dashboard and do not fail or restart backend services.

## Folder Structure

- `app/main.py`: FastAPI dashboard and API.
- `app/runner.py`: protected pytest runner with one run at a time.
- `app/storage.py`: SQLite result history.
- `tests/`: HTTP regression tests.
- `data/`: JSON test data and per-role credentials template.
- `reports/`: local report output when running outside Docker.

## Role Data

Copy `api-test/data/roles.example.json` to `api-test/data/roles.json` on your server, then fill each role. `roles.json` is ignored by git so server credentials stay local.

```json
{
  "systemAdmin": {
    "email": "systemadmin-test@example.test",
    "password": "your-test-password",
    "token": "",
    "deviceTrustToken": "your-random-device-token",
    "roles": ["SystemAdmin"]
  }
}
```

Choose the role with:

```powershell
$env:TEST_ROLE="admin"
```

Environment variables override the file:

- `TEST_USERNAME`
- `TEST_PASSWORD`
- `TEST_TOKEN`
- `DEVICE_TRUST_TOKEN`

Use only non-production test accounts and staging/test data.

## Seed Test Users

Install the Python dependencies, then seed users, roles, and trusted devices into a test/staging database:

```powershell
cd api-test
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:DB_CONNECTION="Host=localhost;Port=5432;Database=uavpms;Username=postgres;Password=postgres"
python scripts\seed_test_users.py
```

The script:

- ensures `SystemAdmin`, `Manager`, `Inspector`, `Analyst`, and `Technician` roles exist;
- upserts the users from `data/roles.json`;
- sets `Status='Active'` and `IsEmailVerified=true`;
- creates matching `TrustedDevices` records.

Because login accepts `X-Device-Trust-Token`, the regression tests can bypass OTP by sending the `deviceTrustToken` from `roles.json`.

You can also run the PostgreSQL-only SQL script directly:

```powershell
psql "host=localhost port=5432 dbname=uavpms user=postgres password=postgres" -f scripts/seed_test_users.sql
```

or copy [scripts/seed_test_users.sql](scripts/seed_test_users.sql) to `scripts/seed_test_users.local.sql`, replace every `CHANGE_ME_*` value, then paste/run the local file in pgAdmin. `seed_test_users.local.sql` is ignored by git.

For local/test OTP verification, you can also set a known Redis OTP:

```powershell
$env:REDIS_CONNECTION="localhost:6379"
python scripts\set_test_otp.py --email manager-test@example.test --otp 000000 --purpose Login
```

Then call `/api/v1/auth/otp/verify` with that OTP. Use this only on local/test Redis, never production.

## Environment Variables

- `TARGET_API`: backend gateway URL. Default in Docker: `http://gateway:8080`.
- `AUTH_API`: auth/identity service URL used only for login. Default in Docker: `http://identityservice:8080`.
- `TESTER_PORT`: FastAPI dashboard port inside the container. Default: `8081`.
- `API_TEST_PORT`: host port in Compose. Default: `8081`.
- `TEST_TIMEOUT`: HTTP request timeout and backend startup wait window. Default: `30`.
- `PYTEST_TIMEOUT`: maximum pytest subprocess time. Default: `300`.
- `BACKEND_HEALTH_PATH`: backend health path. Default: `/health`.
- `BACKEND_BUILD_ID`: optional build/version label shown in results.
- `AUTO_RUN_ON_START`: run once after startup/backend health check. Default: `true`.
- `TEST_ROLE`: role key from `data/roles.json`. Default: `default`.

## Run With Docker

Start the normal backend stack plus the test service:

```powershell
docker compose -f docker-compose.yml -f docker-compose.test.yml up -d --build api-test
```

Open:

```text
http://server:8081
```

For local machine access, use:

```text
http://localhost:8081
```

## Manual Test Trigger

Use the dashboard button or call:

```powershell
Invoke-RestMethod -Method Post http://localhost:8081/api/run
```

If a run is already active, the service returns HTTP `409` and does not start a duplicate run.

## Local Development

```powershell
cd api-test
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:TARGET_API="http://localhost:5194"
$env:REPORTS_DIR="$PWD\reports"
python -m app.main
```

Run tests directly:

```powershell
cd api-test
$env:TARGET_API="http://localhost:5194"
pytest tests -q
```

## Test Data

- `auth.json`: default role and negative auth payloads.
- `roles.json`: credentials/tokens per role.
- `mission.json`: mission query/filter and validation payloads.
- `asset.json`: asset query/filter and validation payloads.
- `inspection.json`: inspection IDs and optional sample image path.
- `inspection/`: place small upload images here when needed.

The initial tests are conservative smoke/regression checks based on existing routes. They avoid creating persistent production data by focusing on reads, auth checks, validation failures, fake IDs, and upload handling.

## Safety Guarantees

The tester is isolated from backend lifecycle. A failed pytest run is stored as a failed test run, while the FastAPI dashboard remains alive. `docker-compose.test.yml` does not use `--abort-on-container-exit`, `--exit-code-from`, or any setting that makes backend deployment depend on regression test results.
