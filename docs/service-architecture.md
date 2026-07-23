# UAV-PMS Microservice Architecture

## Target Shape

The backend is now organized as a moderate service-oriented architecture:

```text
Frontend
  |
  v
UavPms.ApiGateway (Ocelot)
  |
  +--> IdentityService
  +--> OperationsService
  +--> AIInspectionService
        +--> InspectionEvaluationService (gRPC)
  +--> NotificationService
  |
  +--> FastAPI AI service (independent)
```

Ocelot remains the only public HTTP entry point in Docker deployment. Existing client paths are preserved through gateway routing.

## Boundaries

### IdentityService

Owns authentication and account APIs:

- `/api/v{version}/auth`
- `/api/v{version}/users`
- Login, OTP, refresh tokens, trusted devices, profile/user management.

This boundary was selected because auth/JWT/OTP behavior is security-sensitive and should deploy independently from inspection operations.

### OperationsService

Owns the core power-grid and mission operations APIs:

- Regions, substations, transmission lines, towers, assets.
- Missions, inspections, devices, monitoring, audit logs.
- Static inspection image serving under `/images/...`.

These modules stay together because mission/inspection workflows share transactional data and should not be split into distributed writes yet.

### AIInspectionService

Owns .NET-side AI orchestration and integration APIs:

- `/api/v{version}/ai-analysis`
- `/api/internal/ai-analysis`
- `/api/v{version}/vision`
- `/api/v{version}/missions/{missionId}/ai-analysis`

It preserves RabbitMQ AI request publication, callback processing, Vision Bridge ingestion, and optional `MockAIAnalysisConsumer`.

### NotificationService

Owns user notifications and background notification workloads:

- `/api/v{version}/notifications`
- `/hubs/notifications`
- `/hangfire`
- Mission/defect RabbitMQ consumers.
- Hangfire recurring jobs and scheduled notification/email jobs.

This isolates realtime and background processing from synchronous operations APIs.

### InspectionEvaluationService

Owns a small synchronous gRPC business capability:

- `uavpms.inspectionevaluation.InspectionEvaluation/EvaluateDetection`
- Calculates severity, risk level, priority score, and immediate-alert decision for AI detections.

This service is internal-only. AIInspectionService calls it directly over the Docker network; Ocelot does not route gRPC traffic.

### FastAPI AI Service

The existing FastAPI AI service remains independent. Gateway keeps `/ai-service/{everything}` available for deployments that attach the Python service to the same network.

## Database Decision

The existing database/schema is preserved. The refactor does not redesign tables or introduce distributed transactions.

Each .NET service runs independently and owns its API/runtime/config/Dockerfile/source tree. For this migration step, services still connect to the existing database because the current workflows share tables and preserving behavior is more important than forcing premature data splits.

## Deployment

`docker-compose.yml` runs:

- `gateway` on `${GATEWAY_HTTP_PORT:-5194}:8080`
- `identityservice`
- `operationsservice`
- `aiinspectionservice`
- `notificationservice`
- `inspectionevaluationservice`
- `rabbitmq`
- `redis`

The previous `webapi` monolith container is no longer part of compose. The old global layered projects have been removed from the active solution and source tree. Unit tests now reference the owning service projects directly.

## Service Source Ownership

Each service has its own local implementation folders:

```text
Services/<ServiceName>/
  Controllers/
  Application/
  Domain/
  Infrastructure/
  Program.cs
  appsettings.json
  Dockerfile
  UavPms.<ServiceName>.csproj
```

The service `.csproj` files compile only local source and NuGet packages. They do not contain `ProjectReference` entries to legacy monolith projects and do not source-link files from old global layers.

Domain models now live under service-local namespaces, for example `UavPms.IdentityService.Domain` and `UavPms.OperationsService.Domain`. The services keep local bounded-context models so EF Core relationships continue to compile against the existing database schema. Business use-case folders are sliced by service ownership:

- `IdentityService/Application/Features`: `Auth`, `Users`
- `OperationsService/Application/Features`: `Assets`, `AuditLogs`, `Devices`, `Inspections`, `Missions`, `Monitor`, `Regions`, `Substations`, `Towers`, `TransmissionLines`
- `AIInspectionService/Application/Features`: `AIAnalysis`, `VisionBridge`
- `NotificationService/Application/Features`: `Notifications`
- `InspectionEvaluationService`: gRPC endpoint and evaluation rules under `Services/InspectionEvaluationService`

Cross-domain background consumers are compiled only in their owning services:

- `MissionCreatedConsumer` and `DefectDetectedConsumer` are owned by `NotificationService`.
- `MockAIAnalysisConsumer` is owned by `AIInspectionService`.

RabbitMQ producer-consumer paths:

- `OperationsService` publishes `MissionCreatedEvent`; `NotificationService` consumes it.
- `AIInspectionService` publishes `DefectDetectedEvent` after gRPC-evaluated critical detections; `NotificationService` consumes it.

REST services expose `/health` on their HTTP port. `InspectionEvaluationService` uses port `8080` for internal gRPC over HTTP/2 and port `8081` for HTTP/1 health checks.

EF Core migration history for the current shared PostgreSQL database is retained under `Services/OperationsService/Infrastructure/Migrations`. This keeps deployment migration support while removing the old global Infrastructure project.

## Gateway Routes

Ocelot routes public paths to the owning service:

- Health: `/health` for the gateway, plus `/health/identity`, `/health/operations`, `/health/ai-inspection`, `/health/notifications`
- Identity: `/api/v{version}/auth...`, `/api/v{version}/users...`
- Operations: `/regions`, `/substations`, `/lines`, `/towers`, `/assets`, `/missions`, `/inspections`, `/devices`, `/monitor`, `/audit-logs`, `/images`
- AI Inspection: `/ai-analysis`, `/api/internal/ai-analysis`, `/vision`, mission AI analysis routes
- Notifications: `/notifications`, `/hubs/notifications`, `/hangfire`
- FastAPI: `/ai-service/{everything}`

The mission AI route is declared before the general mission route so `/api/v1/missions/{id}/ai-analysis` resolves to `AIInspectionService`.

## Running Locally

Run individual services:

```bash
dotnet run --project Services/IdentityService/UavPms.IdentityService.csproj
dotnet run --project Services/OperationsService/UavPms.OperationsService.csproj
dotnet run --project Services/AIInspectionService/UavPms.AIInspectionService.csproj
dotnet run --project Services/NotificationService/UavPms.NotificationService.csproj
dotnet run --project Services/InspectionEvaluationService/UavPms.InspectionEvaluationService.csproj
dotnet run --project UavPms.ApiGateway/UavPms.ApiGateway.csproj
```

Run the deploy topology:

```bash
docker compose up -d --build
```

## Notes

- Authentication/authorization behavior remains inside the services and uses the same JWT settings.
- FastAPI AI capability is not rewritten or merged into .NET.
- RabbitMQ is still used where the repository already used it.
- The architecture intentionally uses 4 .NET REST business services, one focused internal gRPC service, plus the existing FastAPI service, avoiding tiny services around every entity.
- Remaining migration risk: services still share one PostgreSQL database instance and should only access tables owned by their domain until a future schema split is justified.
