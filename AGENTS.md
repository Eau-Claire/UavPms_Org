# UAV-PMS Backend Agent Map

## Purpose and authority

- This repository is the .NET 9 backend for UAV Powerline Monitoring System.
- Read `docs/MASTER_SYSTEM_SPEC.md` before planning or changing behavior.
- Read `docs/contracts/HANDOFF.md` before changing any BE-FE contract.
- Apply `.agents/skills/readable-code-defense/SKILL.md` to every code task and review.
- Use `.agents/skills/dotnet-feature/SKILL.md` for backend feature work.
- Use `.agents/skills/pr-review-judge/SKILL.md` for independent PR review.
- Keep work in the requested batch; do not opportunistically rewrite business code.
- `docs/MASTER_SYSTEM_SPEC.md` wins when older documents conflict.

## Repository map

- `UavPms.sln`: solution entry point targeting .NET 9.
- `UavPms.ApiGateway/`: Ocelot gateway, routing, CORS, rate limits, unified Swagger.
- `UavPms.ApiGateway/ocelot.json`: deployed downstream route contract.
- `Services/IdentityService/`: authentication, OTP, users, roles, trusted devices, scopes.
- `Services/OperationsService/`: assets, readiness, missions, telemetry, maintenance, reports.
- `Services/AIInspectionService/`: media upload, AI runs, anomalies, review integration.
- `Services/NotificationService/`: notifications, SignalR, email and scheduled jobs.
- `Services/InspectionEvaluationService/`: internal gRPC readiness evaluation.
- Each service follows Domain, Application, Infrastructure and API boundaries where present.
- Test projects sit beside their owning service; Operations also has integration tests.
- `Shared/UavPms.Shared.Contracts/`: cross-service constants, events and shared contracts.
- `Protos/`: versioned internal gRPC definitions.
- `docs/contracts/`: OpenAPI contract and short BE-FE handoff log.
- `specs/`: feature specs, acceptance criteria, task plans and the RTM.
- `scripts/`: repeatable operational and contract-export scripts.

## Architectural boundaries

- API projects own transport concerns: HTTP, authentication, serialization and status codes.
- Application projects own CQRS requests, handlers, validation and business orchestration.
- Domain projects own entities, value rules and business state.
- Infrastructure projects own EF Core, repositories, messaging, storage and external systems.
- The gateway routes traffic; do not move service business rules into Ocelot middleware.
- The four REST services publish Swagger; InspectionEvaluationService publishes gRPC only.
- OperationsService owns shared-database EF Core migrations.
- Prefer shared contracts only for genuine cross-service boundaries, not convenience dumping.

## Quick commands

```powershell
dotnet restore UavPms.sln
dotnet build UavPms.sln
dotnet test Services/IdentityService/UavPms.IdentityService.Tests/UavPms.IdentityService.Tests.csproj
dotnet test Services/OperationsService/UavPms.OperationsService.Tests/UavPms.OperationsService.Tests.csproj
dotnet test Services/OperationsService/UavPms.OperationsService.IntegrationTests/UavPms.OperationsService.IntegrationTests.csproj
dotnet test Services/AIInspectionService/UavPms.AIInspectionService.Tests/UavPms.AIInspectionService.Tests.csproj
dotnet test Services/NotificationService/UavPms.NotificationService.Tests/UavPms.NotificationService.Tests.csproj
dotnet test Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.Tests/UavPms.InspectionEvaluationService.Tests.csproj
```

## Migration checks

```powershell
dotnet ef migrations list --project Services/OperationsService/UavPms.OperationsService.Infrastructure/UavPms.OperationsService.Infrastructure.csproj --startup-project Services/OperationsService/UavPms.OperationsService.API/UavPms.OperationsService.API.csproj
dotnet test Services/OperationsService/UavPms.OperationsService.Tests/UavPms.OperationsService.Tests.csproj --filter Migration
```

- Migration checks are read-only locally; do not apply a database migration from a workstation.
- Every EF Core migration must be applied through GitHub Actions after human approval.
- Never run handwritten DROP/ALTER SQL against a shared or production database.
- Export a running gateway contract with `./scripts/export-openapi.ps1`.

## Mandatory invariants

- Zero Mock/Fake Data in application flows, demos, integration paths and environment fallbacks.
- Zero OTP Bypass in every environment; never accept a fixed or master OTP.
- Preserve one camelCase `ApiResponse` envelope across REST endpoints.
- Push pagination filtering, ordering, `Skip` and `Take` into `IQueryable`.
- Clamp `pageSize` to 100 or reject larger values consistently.
- Authorize against the union of all assigned roles; never assume one role per user.
- Apply `GeographicAccessFilter` to geographically scoped business queries.
- Preserve soft delete and audit history for primary entities.
- Store backend timestamps in UTC and return unambiguous ISO-8601 values.
- Do not expose secrets, OTP values, tokens or sensitive settings in logs or responses.
- Do not weaken tests to make a change pass; fix production behavior or the test fixture.

## Human approval required

- `UavPms.ApiGateway/ocelot.json`: routes, auth, rate limits and downstream exposure.
- `Services/IdentityService/` Auth or OTP behavior: issuance, verification, JWT and trust flows.
- Any `Migrations/` content: generated migration, snapshot, SQL or migration ownership.
- `.github/workflows/`: CI permissions, secrets, deployment and migration execution.
- Stop and request approval before editing any of these zones, even for a small change.
- Approval is scoped to the named change; it does not authorize adjacent cleanup.

## Delivery discipline

- Start from a spec under `specs/` and link requirements into `specs/RTM.md`.
- Keep functions linear with guard clauses and at most two levels of nesting.
- Comment why a non-obvious business rule exists; do not narrate syntax.
- Add or update focused tests without mock/fake business data.
- Run the narrow service tests first, then `dotnet build UavPms.sln`.
- When REST contracts change, export OpenAPI and update `docs/contracts/HANDOFF.md` if needed.
- End each coding batch with the required Vietnamese defense walkthrough.
