# MF01 mock data by region

Synthetic data for the Mission Management acceptance cases. Coordinates and operating-area polygons are invented; these are not actual grid assets or official administrative boundaries.

Generate without database access:

```bash
python api-test/scripts/generate_mf01_mock.py --output /tmp/uavpms-mf01-mock
python -m unittest discover -s api-test/fixtures/mf01
```

Default: 3 regions, 3 management units, 18 substations, 54 lines, 1,620 towers with one asset each, 54 UAVs, 54 missions, 540 persisted targets, and 15 users. `--towers-per-line 100` generates 5,400 towers/assets. IDs are deterministic UUIDs. Code/email prefixes identify mock records. SQL uses insert-on-conflict-do-nothing: reruns do not duplicate IDs and preserve edits; use a fresh database for an exact reset or a different volume.

`manifest.json` contains users, regional hierarchy/asset IDs, mission assignments/targets, and test polygons. `seed.sql` supplies data using the existing application schema. No generated passwords or tokens are committed. This generator does not change FE responses or hide API errors.

## Load only into a disposable test database

Apply the current application migrations to a dedicated PostGIS database named `mf01_test*`, including current AssetComponents, MissionTargets (AssetId/Sequence/InspectionStatus), UserGeographicScopes and identity tables. Seed the existing SystemAdmin, Manager and Inspector roles first. Configure local BE services to use that database; configure FE to call that local gateway. SQL refuses other database names and wraps all inserts in one transaction with `ON_ERROR_STOP`.

Run `psql "$DB_CONNECTION" -f /tmp/uavpms-mf01-mock/seed.sql` from a psql session/script where the `mf01_password` variable is set to your chosen local test password (for example with `\prompt 'Local test password: ' mf01_password` inside an interactive psql session, followed by `\i /tmp/uavpms-mf01-mock/seed.sql`). PostgreSQL pgcrypto hashes that supplied password. Existing fixture accounts keep their password on rerun. Login still follows the application's OTP/trusted-device flow; this seed does not bypass authentication.

Database insertion and end-to-end login have not been exercised by the offline generator tests. The application schema must be migrated first; a schema mismatch fails the transaction. No production data was seeded as part of adding these files.

## Region and assignment scenarios

- `MF01-NINHTHUAN-MANAGER`, `MF01-DONGNAI-MANAGER`, `MF01-CAMAU-MANAGER`: each has exactly one EVNSPC-style Region scope. Email is the lowercase key plus `@mf01.example.test`.
- Three Inspectors per region have **no default geographic scope**. Their access derives from mission assignments; this is deliberate so broad region grants cannot hide assignment bugs.
- `MF01-MANAGER-NO-SCOPE` and `MF01-INSPECTOR-UNASSIGNED`: no scopes or missions. `MF01-ADMIN` has SystemAdmin role for global comparison.
- Each region has 540 assets, including 18 inactive assets excluded from seeded mission targets. Different voltage levels and asset types exercise filtering. Mission states include Pending, Executing, Completed and Cancelled; existing backend authorization bugs may fail the intended acceptance checks.
- `cases.mixedRegionAssetIds`: send all three IDs as one scoped manager; expect whole request rejected, no partial mission.
- `cases.crossRegionPolygon`: covers all three regions; a regional manager's preview should return only eligible own-region assets (522 for default data), never other regions.
- `cases.emptyPolygon`: no targets. `cases.invalidPolygon`: self-intersecting polygon, expect 400.
- Inspectors have multiple missions with disjoint target subsets. Compare each mission's exact `targetAssetIds` against its GIS: access to mission A must not expose mission B's assets. General GIS union visibility is a different test.
- Completed/cancelled missions support immutable-state and historical snapshot cases. Check geographic scoping through hierarchy even if a Region has no geometry (existing unit tests cover this variant).

The fixture follows the current single-Inspector mission schema; multiple-assignment and planned-end data must be added when those contracts are implemented. It does not seed anomalies/alerts or fix the MF01 blockers documented in `docs/validation/mf01-be-fe-validation.md`.

## GitHub Actions workflow

Workflow: `.github/workflows/seed-mf01-test-data.yml` (**Seed MF01 Test Data**).

1. Merge the workflow/generator into the default branch so the Run workflow button is available.
2. Provision/migrate a dedicated `mf01_test*` PostGIS database in the existing `uavpms-db` container and seed application roles. This workflow deliberately reports an error for missing databases/schema; it does not clone production data, create databases, migrate, or restart services.
3. In the existing `production` GitHub environment, reuse `SERVER_HOST`, `SERVER_USER`, `SERVER_SSH_KEY`, `SERVER_PORT`. Set `MF01_SEED_PASSWORD` (12–72 UTF-8 bytes). `SERVER_FINGERPRINT` can provide the SSH server SHA256 fingerprint for both transfer and execution.
4. Actions → Seed MF01 Test Data → Run workflow. Set database (default `mf01_test`) and towers per line: 30 / 100 / 500, corresponding to 1,620 / 5,400 / 27,000 towers/assets.
5. Approve the production environment job. The environment is reused for server access credentials/reviewers; the target database remains the explicitly selected test database. No remote step precedes that approval.
6. On success, download the manifest artifact for fixture user/mission/region IDs. Login with the configured test password and the existing authentication flow. Existing fixture passwords are not reset on rerun.

The runner hashes the password before file transfer. Only `manifest.json` is uploaded as an artifact. Seed files are removed from remote staging even on failure when SSH remains reachable. All row-presence verification happens before SQL COMMIT; partial failure rolls back. The workflow reads its selected commit via checkout and transfers its generated payload, so it does not pull/reset the running deployment checkout or depend on the server reaching GitHub.

Implementation uses the published inputs of [scp-action v1.0.0](https://github.com/appleboy/scp-action/blob/v1.0.0/action.yml) and [ssh-action v1.2.0](https://github.com/appleboy/ssh-action/blob/v1.2.0/action.yml).
