# Production pole manifest import

The workflow is manual-only and requires `IMPORT_PRODUCTION_POLE_MANIFEST`, the production environment, a clean deployment checkout on `main`, and the checked-in CSV. It does not fetch, deploy, reset data, change user permissions, or dispatch itself.

## Existing schema and identities

EF maps `Asset` to `AssetComponents`, `AssetCode` to `ComponentCode`, and `AssetType` to `ComponentType`. There is no separate `Assets` table or second component entity to populate.

- Regions use the unique `Code`: `North`, `Central`, `South`.
- Management units use the unique provider `Code`; EVNHCMC is a separate TP.HCM unit.
- Substations use `(RegionAssetId, SubstationName)`, with provider/city DEMO names. This model has no unique substation-name constraint, so the transaction locks the domain tables and rejects pre-existing ambiguous matches.
- Lines use the existing unique `Code` as `provider:LINE_CODE`; their unique names include the same identity and a DEMO/unverified label.
- Towers use the existing unique `TowerCode = POLE_ID`.
- Assets/components use the existing unique `ComponentCode = POLE_ID` and link to that tower, line, and management unit.

North/EVNNPC includes Hà Nội and Hải Phòng; Central/EVNCPC includes Huế and Đà Nẵng; South/EVNSPC includes Biên Hòa. TP.HCM belongs to the separate EVNHCMC unit within South. City organization is represented by the substations, without inventing administrative boundary polygons.

## Data provenance and preservation

All manifest coordinates are demo/mock and unverified, including IDs without `MOCK` in their text. CSV numbers are loaded as PostgreSQL `numeric` without rounding, then passed to `ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)`. PostGIS point ordinates use double precision; no coordinate correction, offset, geocoding, or simplification is performed.

Only four additional points are created: `DEMO-COVERAGE-HT`, `DEMO-COVERAGE-KH`, `DEMO-COVERAGE-LD`, and `DEMO-COVERAGE-CM`. They represent Hà Tĩnh, Khánh Hòa, Lâm Đồng, and Cà Mau. Their existing component-type field explicitly says “DEMO coverage marker only; not a field pole”; their line names also identify demo/unverified data. No new metadata column is added.

Line geometries connect manifest points in stable pole-code order for demo display. They are not verified electrical topology or surveyed line routes. A line with only one point has no fabricated line geometry.

The import touches only Regions, ManagementUnits, Substations, TransmissionLines, Towers, AssetComponents, and its own staging table. Existing status, health, inspection history, audit/identity rows, and geographic scopes are preserved. A collision with soft-deleted data aborts for review instead of resurrecting it. Imports never give existing users broader scopes; newly imported data becomes visible only under existing explicit scope/assignment rules or SystemAdmin access.

A unique staging table is created and removed within one transaction. SQL failure/disconnection rolls it back with the application changes. The EXIT trap removes only the copied CSV. No permanent application table is dropped, truncated, or used for cleanup.

## Verification

Validation was performed against the generated EF schema in a disposable local PostGIS environment before the Docker restriction was applied. The check built the schema from current EF mappings, ran the SQL twice, verified protected rows and all CSV coordinates, tested duplicate/invalid-coordinate rollback, and removed the disposable environment. It never invoked SSH or a production workflow.

For the current 50-row manifest, each successful import covers 54 towers and 54 assets/components:

| Group | Manifest poles | DEMO coverage markers | Total |
| --- | ---: | ---: | ---: |
| NPC | 18 | 1 | 19 |
| CPC | 17 | 1 | 18 |
| SPC | 8 | 2 | 10 |
| EVNHCMC | 7 | 0 | 7 |

On an empty domain schema, the first import inserts 54 towers/assets and the second inserts zero and updates the same 54. Inserted/updated counts are reported separately for towers and assets. Verification totals and relationship checks refer to this import's natural keys; unrelated production demo rows remain in place and are not included. Invalid coordinate, staging duplicate-key, missing relationship, and invalid provider/region mapping counts are zero for this manifest.

## Authorization and frontend

The access predicate remains default geographic/organization scope OR an active explicit mission/ticket assignment. SystemAdmin is global only when authenticated. Assignment access to an asset allows its tower and ancestor line/station/region records, but does not grant their other towers or assets. Explicit mission line targets apply to that line's resources. Generic direct-ID reads execute the same predicate, even for tracked entities. Infrastructure segments require authorized endpoint assets; anomalies and alerts require an authorized related asset.

The frontend loads `/gis/infrastructure` and uses returned asset IDs for mission selection. It starts empty, contains no GIS mock constants or fallback datasets, distinguishes loading/empty/403/failure, and uses Vietnam-wide bounds `[8.15, 102.0]` to `[23.5, 110.0]`. Backend authorization remains authoritative. No region polygons are invented.
