# MF01 Inspection Mission Lifecycle

This document is the implementation contract for MF01. The agreed business source is
`docs/requirements/mainflows/MF01_v2.0.md`. EVNSPC is the system boundary; a Region is an
EVNSPC operational/local power-company area, never a generic North/Central/South zone.

## Existing architecture

The solution is a .NET 9 microservice backend. `OperationsService` uses ASP.NET Core
controllers, MediatR handlers, EF Core/Npgsql/PostGIS, repositories/unit of work, JWT role
authorization, `UserGeographicScope`, an EF audit trail, notification records and an outbox.
Mission GIS membership was already persisted as `MissionTarget` rows. No frontend project is
present in this repository.

## Domain ownership and authorization

- `UserGeographicScope` remains durable management scope. It is not changed by assignments.
- `Mission.RegionId` is explicitly selected and retained as historical mission data.
- `MissionAssignment` grants access only to its mission and supports cross-Region personnel.
- SystemAdmin is the explicit global EVNSPC bypass. Managers require an exact Region scope.
- Every MF01 write rechecks the active caller, mission state, and applicable scope/assignment.
- Submitted asset IDs are loaded again and checked for active status, mission Region and the
  confirmed boundary before snapshot replacement.

## Data model

`Mission` adds Region, optional schedule, type (`Scheduled` or `AdHoc`), trigger reason,
planned interval, PostGIS boundary, actual timestamps (existing `StartedAt`/`EndedAt`) and an
optimistic concurrency token. `InspectionSchedule`, `MissionAssignment`, `MissionCheckIn`,
and `DroneHandover` are new tables. Existing `MissionTarget` is the MF01 MissionAsset snapshot
to avoid duplicating an equivalent established entity.

The rollout migration deliberately leaves `Missions.RegionId` nullable at database level for
legacy rows. It does not invent or destructively rewrite production Regions. New MF01 creates
always require a valid Region. Operators should map legacy missions, verify the mapping, then
tighten the column in a later deployment.

## Lifecycle

The backend transition sequence is:

`Draft → Assigned/Preparing → Ready → InProgress → Completed`

`Draft`, `Assigned`, `Preparing`, and `Ready` may transition to `Cancelled`. Normal scope,
assignment, drone and schedule mutations are rejected after execution starts. Readiness is
calculated from active assignments, check-ins for every active assignee, an assigned idle UAV,
an accepted active handover and at least one persisted mission target. Clients cannot set
Ready or start by updating a status string.

## API additions

Routes retain the existing `/api/v1/missions` style:

- `POST /api/v1/missions` accepts the MF01 Region/type/schedule/planned interval contract.
- `POST /api/v1/missions/{id}/scope/resolve`
- `PUT /api/v1/missions/{id}/assets`
- `POST|DELETE /api/v1/missions/{id}/assignments[/{assignmentId}]`
- `PUT /api/v1/missions/{id}/drone`
- `POST /api/v1/missions/{id}/drone-handover`
- `POST /api/v1/missions/{id}/check-in`
- `POST /api/v1/missions/{id}/start|complete|cancel`

Boundary input is WKT with SRID 4326. Candidate resolution only returns active assets in the
mission Region. Mission details include Region, schedule, type, boundary, planned/actual times,
team roles and check-ins.

## Audit and notification

MF01 operations create semantic audit actions including `MISSION_CREATED`, scope/assets
changes, assignment changes, drone assignment/replacement/handover, check-in/readiness,
start/completion and cancellation. Assignment/removal/cancellation create notifications in
the existing notification infrastructure within the same database transaction.

## Development data

The disposable MF01 fixture now uses synthetic EVNSPC-style Ninh Thuan, Dong Nai and Ca Mau
operating areas. No migration renames or deletes production Region data.

## Commands

```bash
dotnet ef database update --project Services/OperationsService/UavPms.OperationsService.Infrastructure --startup-project Services/OperationsService/UavPms.OperationsService.API
dotnet test Services/OperationsService/UavPms.OperationsService.Tests/UavPms.OperationsService.Tests.csproj
dotnet build UavPms.sln
python -m unittest discover -s api-test/fixtures/mf01
```

For production, run the manual GitHub Actions workflow **Apply MF01 Mission Lifecycle
Migration** after the MF01 commit has been deployed to `main`. Enter the exact confirmation
`APPLY_MF01_MISSION_LIFECYCLE`. The workflow follows the existing OperationsService migration
pattern, verifies prerequisite migrations, applies pending EF migrations, restores
`RUN_MIGRATIONS=false`, checks all MF01 tables/columns, reports unmapped legacy missions, and
only audits suspicious Region names without modifying them.

## Known gaps

- Schedule CRUD/recurrence generation is not yet exposed; MF01 creation validates schedules
  already present in the database.
- Reassignment is currently remove plus add, and drone replacement is the drone PUT action;
  dedicated composite routes can be added when clients require them.
- There is no frontend source in this repository, so no mission UI could be changed here.
- Legacy create payloads remain accepted temporarily for compatibility; they use the former
  all-at-once handler. New clients should always send `RegionId`, `MissionType`, `PlannedStart`
  and `PlannedEnd`.
