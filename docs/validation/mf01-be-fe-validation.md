# MF01 — Mission Management validation

Date: 2026-09-08. Verdict: **NOT COMPLETE; authorization blockers remain.**

## Scope and baseline

- Requirement: `docs/requirements/mainflows/MF01.md`, the user-supplied Inspection Mission Management specification present in the workspace. The older `docs/cleanup/mf01-repository-cleanup-audit.md` describes upload/AI cleanup and is not the acceptance baseline for this review.
- BE: `f8b2dd1`, review branch `fix/mf01-mission-validation`.
- FE: pulled `origin/main` (`f0dcce9`) into existing `fix/gis-map-production`; resulting merge `21f2203`. Pull completed before any possible source edits.
- This commit records validation findings, not implementation of the missing flow. Existing user changes to requirement files were preserved and excluded from this commit.
- Evidence: source/controller/handler/repository/configuration inspection, existing automated tests, FE production build. No production mutations, JWT-based production authorization tests, browser E2E, AXE audit, or real PostGIS transaction/spatial integration tests were performed. Passing existing tests does not establish MF01 acceptance.

## Blocking findings

### P0 — Mission access does not enforce assignment or management scope

BE evidence:

- `Services/OperationsService/UavPms.OperationsService.API/Controllers/MissionController.cs`: GET details permits Inspector; PUT and DELETE permit Manager and Inspector. PUT/DELETE exclude SystemAdmin despite management privileges elsewhere.
- `...Application/Features/Missions/Queries/GetMissionDetails/GetMissionDetailsQueryHandler.cs`: returns mission and all targets without current-user authorization.
- `...Infrastructure/Repositories/MissionRepository.cs`: details and paged list query missions without caller scope. `/my` correctly filters `InspectorId`, but that does not protect direct-ID reads.
- `...Application/Features/Missions/Commands/UpdateMission/UpdateMissionCommandHandler.cs` and `DeleteMission/DeleteMissionCommandHandler.cs`: no assignment/manager authorization before mutation.
- `...Infrastructure/Repositories/GenericRepository.cs`: geographic predicates cover infrastructure entity types, not Mission. The DbContext global filter only excludes soft-deleted records. There is no hidden mission authorization here.

Consequence from source: an Inspector permitted by the controller can request another mission by ID, including targets, or submit update/delete for it. Managers can list/manage missions outside their assigned geographic scope. Add reusable application authorization and negative tests for forged IDs; do not rely on `/my` or FE filtering.

### P0 — General GIS access is not a mission management authorization rule

`...Infrastructure/Authorization/GeographicAccessFilter.cs` grants geographic scope OR active mission/line/ticket access. `AssetRepository.GetAssetsByIdsAsync` and `GetAssetsIntersectingAsync` do apply this filter. Thus creation revalidates asset IDs and rejects missing/inaccessible targets before writes; it is incorrect to say there is no filtering at all.

However, visibility through an operational assignment is broader than MF01's permission to create/manage missions within an assigned management scope. A manager can reuse targets visible only through an active mission/ticket. Rejected IDs produce generic not-found rather than the specified management-scope 403. A dedicated management-scope predicate is needed while preserving the general GIS policy for other flows.

### P0 — No mission-scoped GIS contract

`GisController`/`GisInfrastructureQuery` have no mission ID. General GIS returns the union of geographic and active assignments; it cannot guarantee that a view of mission A contains only mission A's persisted targets. Mission detail does return persisted `MissionTargets`, but lacks the authorization above. Implement an authenticated mission-specific read using assignment plus exact target membership, including anomaly/alert filtering. Do not derive historical scope from a polygon or all towers on the line.

### P1 — Lifecycle and assignment updates bypass domain rules

- Create validator accepts `Completed`; no restriction to the initial mission state.
- Validators accept `In Progress`, while the enum uses `Executing`. `Enum.TryParse` cannot map that string; create falls back to Pending and update silently keeps the previous status.
- Update directly sets Status rather than invoking `Mission.Start/Complete/Cancel`; it does not reject completed/cancelled mutations. Delete also has no state restriction.
- Update checks only assignee existence, not Inspector eligibility, unlike create. It does not check UAV availability as create does.
- No API for updating confirmed target membership, or independent assignment/unassignment, was found.
- No mission concurrency token/version check was found. `UpdatedAt` is not configured as a concurrency token. Mission codes use second-resolution timestamps despite a unique DB index, so simultaneous creates can collide.

### P1 — Frontend masks authorization and spatial failures

FE source paths are relative to sibling `UavPms_FE`:

- `src/app/features/missions/pages/mission-detail/mission-detail.ts`: `catchError` turns **any 403 or 404** into `getDemoMission(id)`, then loads detections. This presents fabricated mission content when the server denies access or the mission is absent. It does not by itself prove backend data leakage, but breaks correct authorization/error UX.
- `src/app/features/missions/pages/mission-create/mission-create.ts`: spatial-query empty results and errors both fall back to client-side point-in-polygon selection over loaded towers. Server-authoritative empty results must remain empty; API failures must not become successful candidate selection.
- The create page initializes InspectorId to the current user, including managers, and retains it even when the assignable list does not contain that user. An assignable-list failure can also silently retain this invalid default.

### P1 — Frontend routes and Inspector mission list disagree with BE

- `src/app/app.routes.ts`: create route is `/missions/new` and permits Inspector; BE create allows SystemAdmin/Manager only.
- `/missions/create` is not registered and is captured by `/missions/:id`, which can then hit the demo fallback above.
- `src/app/features/missions/pages/mission-list/mission-list.ts` always calls list; `MissionsApi` has no `/missions/my` method. BE list excludes Inspector, despite FE allowing access to the mission list.

### P1 — Required scheduling and multiple assignments are absent

BE request/entity and FE `MissionCreateRequest` have one scheduled start and one InspectorId. No planned end field, end-after-start validation, or inspector collection/assignment relation exists. `StartedAt`/`EndedAt` are actual lifecycle timestamps, not a planned end. Existing UAV assignment is a valid established domain requirement and should be retained.

### P2 — Display/persistence contract loses mission settings

FE sends inspection types/checklist in `routeData`, but `MissionConfiguration` ignores RouteData and create/detail DTOs return an empty string. These settings do not survive reload. FE `normalizeMission` also omits scheduled start and reads `assignedToUsername`/`managerUsername` while BE returns email fields. The mission's asset membership itself is persisted correctly.

## Business-rule coverage

| Rule | Result | Evidence / remaining work |
| --- | --- | --- |
| BR01 Creator | Partial | BE create role attribute exists; no reusable handler permission check; FE permits Inspector. |
| BR02 Management scope | Partial / blocker | Assets are filtered and rechecked, but general visibility is used; mission reads/mutations lack scope checks. |
| BR03 Assignment | Partial | One eligible Inspector on create; no multiple assignments, unassignment, or eligibility on update. |
| BR04 Hierarchy | Partial | Region/Substation/Line/Tower entities and scope joins exist; mission create accepts asset IDs, without explicit hierarchy-consistency validation or a hierarchy resolver contract. |
| BR05 Polygon | Partial | Read-only `POST /api/v1/assets/spatial-query`, SRID 4326, SQL Intersects, general access filter; lacks management-specific policy. |
| BR06 Confirmation | Present with gaps | Preview does not create; FE retains target IDs until Save. Local fallbacks can replace server results. |
| BR07 Manual selection | Partial | Add/remove/order/deduplicate targets exist. No complete mission hierarchy selection/resolution contract was found. |
| BR08 Snapshot | Present | Ordered MissionTargets persisted, unique `(MissionId, AssetId)` and FK constraints. Detail reads this snapshot. |
| BR09 Assigned visibility | Blocked | `/my` exists but direct-ID access is unrestricted; FE does not call `/my`. |
| BR10 Mission GIS | Missing | No mission-specific GIS request; general GIS scope is a union. |
| BR11 General GIS | Partial | Geographic SQL filtering and tests exist; broader manager/assignment semantics must not substitute for BR02/BR10. |
| BR12 Lifecycle | Partial | Existing Pending/Executing/Completed/Failed/Cancelled enum and domain methods; mutation handlers bypass state machine. |
| BR13 Edit restrictions | Missing | Update/delete lack final-state restrictions. |
| BR14 Audit | Present mechanism; coverage incomplete | DbContext creates audit rows for changed BaseEntity records, including Mission/targets. Assignment lifecycle itself is incomplete. |

## Validation / exception coverage

| Requirement | Assessment |
| --- | --- |
| EX01 authentication | Controller authentication present; runtime JWT/401 not exercised in this review. |
| EX02 permission | Create role gate present; update/delete roles and application authorization incomplete. |
| EX03 unauthorized target | Inaccessible IDs cause rejection before writes, but general visibility policy and not-found response differ from strict management-scope 403. |
| EX04–05 inspector existence/eligibility | Create checks both; update checks existence only. Active-user eligibility is not checked by create handler. |
| EX06 empty scope | Create rejects empty targets; no empty-draft flow. |
| EX07 invalid polygon | Shell closure/range/validity checked. Null nested coordinates can throw; extra rings/holes are silently ignored rather than supported or rejected. |
| EX08–09 empty/cross-boundary preview | BE query read-only and filtered; FE repopulates empty results. Management-specific boundary remains missing. |
| EX10 duplicate assets | Create rejects duplicates; DB unique relation; FE store deduplicates. |
| EX11 duplicate inspectors | Single InspectorId cannot duplicate, but required multi-assignment flow is absent. |
| EX12 immutable state | Mutation handler checks absent. |
| EX13 unassigned mission | Direct-ID authorization absent. |
| EX14 out-of-mission asset | General asset access filter exists, but no mission-specific membership endpoint. |
| EX15 concurrent update | No concurrency/version strategy found. |
| EX16 spatial failure | BE query does not persist; FE turns errors into local selection. |
| EX17 atomic persistence | Create wraps Mission + targets in transaction; real DB rollback/constraint behavior not tested here. Event publish occurs after commit: publisher failure can report an error after mission has already been persisted; no outbox/idempotency found in this handler. |

Geometry uses existing GeoJSON for request and PostGIS/NTS SRID 4326; there is no need for a second representation. Geometry is optional for audit/display, but final target membership is mandatory and already persisted.

## Checks performed

- `dotnet test Services/OperationsService/UavPms.OperationsService.Tests/UavPms.OperationsService.Tests.csproj --no-restore -m:1 --verbosity minimal`: **100 passed**. Build emits existing obsolete `HasCheckConstraint` warning.
- FE `npm test -- --watch=false`: **96 passed / 16 files**.
- FE `npm run build`: **passed**, existing stylesheet-budget and Leaflet CommonJS warnings.
- Existing BE mission tests cover successful create, empty/duplicate/missing/inactive targets, missing UAV, request alias mapping, target ordering and `/my` handler behavior. Geographic tests cover general scope filtering and SQL translation, not real PostGIS polygon execution or complete mission authorization.
- Existing FE create tests cover missing targets, successful selected-ID submit and backend validation message. They do not cover authorization fallback, failed/empty spatial preview, Inspector list routing, or planned-end validation.

## Required acceptance tests before claiming complete

1. Forged mission ID: unassigned Inspector cannot GET/PUT/DELETE; out-of-scope Manager cannot list/read/mutate it.
2. Mixed authorized and unauthorized asset IDs reject the whole create/update with no writes. Assignment-only visibility must not grant manager creation rights.
3. Polygon crossing two regions returns authorized candidates only; null/malformed/self-intersecting geometry rejected; valid holes are supported or explicitly rejected; empty/error response never becomes local FE success.
4. One Inspector assigned to two missions sees only the selected mission's persisted assets/anomalies/alerts in that mission's GIS.
5. Unknown/ineligible/duplicate inspectors rejected on both create and reassignment; assignment collection round-trips.
6. Planned end must exceed start; real create metadata/inspection settings survive reload.
7. Completed/cancelled scope and assignments immutable; legal transitions update lifecycle timestamps; concurrent update conflicts rather than silently overwrites.
8. Real PostgreSQL transaction rollback leaves no partial mission/target/assignment/audit rows; concurrent code generation and post-commit event failure have defined behavior.
9. FE Inspector opens `/my`, cannot enter creation; 403/404 show accurate errors; `/missions/create` resolves intentionally rather than as a mission ID.
10. Browser E2E for create → confirm scope → assign → Inspector detail/GIS, plus keyboard/focus/AXE checks.

## Implementation order

1. Close direct mission authorization and separate management scope from general GIS access; add negative tests.
2. Define mission-scoped GIS and enforce snapshot membership server-side.
3. Align planned schedule, assignment collection and lifecycle/concurrency contracts across BE, DB and FE using current domain status names.
4. Remove FE authorization/spatial fallbacks, fix routes/Inspector list, and preserve real metadata.
5. Run PostGIS integration and browser acceptance tests, then reassess Definition of Done. No MF01 completion claim is justified by the current green unit tests alone.
