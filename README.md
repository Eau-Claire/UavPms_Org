# UAV-GridGuard PMS

Backend for UAV-based power-line inspection management. The system is organized as a moderate distributed architecture: Ocelot API Gateway, four ASP.NET Core business services, one internal gRPC service, RabbitMQ background consumers, Redis, Supabase/PostgreSQL, and the existing independent FastAPI AI service integration.

## Architecture

```text
Client / Swagger
  -> UavPms.ApiGateway (Ocelot)
      -> IdentityService REST
      -> OperationsService REST
      -> AIInspectionService REST
            -> InspectionEvaluationService gRPC
            -> RabbitMQ publish DefectDetectedEvent
      -> NotificationService REST + SignalR + Hangfire
            -> RabbitMQ consumers
      -> FastAPI AI service over HTTP when attached to the network
```

## Services

| Service | Responsibility | Project |
|---|---|---|
| API Gateway | Public routing, CORS, gateway health | `UavPms.ApiGateway` |
| IdentityService | Login, OTP, refresh token, users, roles | `Services/IdentityService` |
| OperationsService | Regions, substations, lines, towers, assets, missions, inspections, devices, monitor, audit logs | `Services/OperationsService` |
| AIInspectionService | AI upload/orchestration, callback processing, Vision Bridge, FastAPI/RabbitMQ integration | `Services/AIInspectionService` |
| NotificationService | Notifications, SignalR hub, Hangfire jobs, RabbitMQ consumers | `Services/NotificationService` |
| InspectionEvaluationService | Internal gRPC severity/risk evaluation for AI detections | `Services/InspectionEvaluationService` |

## Technology Stack

ASP.NET Core 9, Ocelot, MediatR, EF Core, PostgreSQL/Supabase, Redis, RabbitMQ, Hangfire, SignalR, JWT Bearer auth, Swagger/OpenAPI, gRPC, Docker Compose.

## Run

```bash
dotnet restore UavPms.sln
dotnet build UavPms.sln
dotnet test UavPms.UnitTests/UavPms.UnitTests.csproj
docker compose up -d --build
```

Gateway: `http://localhost:5194`

RabbitMQ management: `http://localhost:15672` (`guest` / `guest` by default)

Internal gRPC: `inspectionevaluationservice:8080`; health endpoint: `inspectionevaluationservice:8081/health`.

The FastAPI AI service source is not part of this repository. If you have its image, run it on the same Compose network with:

```bash
FASTAPI_AI_IMAGE=<your-fastapi-image> docker compose --profile fastapi-ai up -d --build
```

## Environment

Required production variables:

```env
DB_CONNECTION=
HANGFIRE_DB_CONNECTION=
JWT_SECRET=
JWT_ISSUER=UavPms
JWT_AUDIENCE=UavPmsClient
JWT_EXPIRY_MINUTES=60
SUPABASE_URL=
SUPABASE_API_KEY=
SUPABASE_BUCKET=
SENDGRID_API_KEY=
SENDGRID_FROM_EMAIL=
SENDGRID_FROM_NAME=
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

Optional:

```env
RUN_MIGRATIONS=false
MOCK_AI_ENABLED=false
MOCK_AI_CATEGORY_CODE=
MOCK_AI_CONFIDENCE=0.92
GATEWAY_HTTP_PORT=5194
RABBITMQ_MANAGEMENT_PORT=15672
```

## REST API Demo

All public routes go through Ocelot. Representative PRN232 CRUD/search/filter/sort/pagination resource:

```http
POST   /api/v1/missions
GET    /api/v1/missions?page=1&pageSize=10&search=line&status=Pending&sortBy=createdAt&sortDescending=true
GET    /api/v1/missions/{id}
PUT    /api/v1/missions/{id}
DELETE /api/v1/missions/{id}
```

Other route groups:

| Route | Service |
|---|---|
| `/api/v1/auth`, `/api/v1/users` | IdentityService |
| `/api/v1/regions`, `/substations`, `/lines`, `/towers`, `/assets`, `/missions`, `/inspections`, `/devices`, `/monitor`, `/audit-logs` | OperationsService |
| `/api/v1/ai-analysis`, `/api/internal/ai-analysis`, `/api/v1/vision`, `/api/v1/missions/{id}/ai-analysis` | AIInspectionService |
| `/api/v1/notifications`, `/hubs/notifications`, `/hangfire` | NotificationService |
| `/health`, `/health/identity`, `/health/operations`, `/health/ai-inspection`, `/health/notifications` | Gateway/downstream health |

Swagger is exposed by each REST service when running in Development. For direct local development, run a service and open `/swagger`.

## gRPC Workflow

Contract: `Protos/inspection_evaluation.proto`

Shared RabbitMQ event contracts: `Shared/UavPms.Shared.Contracts/Events`

Server: `Services/InspectionEvaluationService/Services/InspectionEvaluationGrpcService.cs`

Client: `Services/AIInspectionService/Infrastructure/Grpc/GrpcInspectionEvaluationClient.cs`

Workflow:

1. FastAPI/worker posts AI callback to `AIInspectionService`.
2. `ProcessAiAnalysisResultCommandHandler` resolves media/category and calls `InspectionEvaluationService.EvaluateDetection`.
3. gRPC returns severity, risk level, priority score, and immediate-alert decision.
4. AIInspectionService saves anomaly/alert metadata and preserves fallback behavior if gRPC is temporarily unavailable.

## RabbitMQ Workflow

RabbitMQ is preserved as the asynchronous broker.

Producer examples:

- `OperationsService` publishes `MissionCreatedEvent` after creating a mission.
- `AIInspectionService` publishes `DefectDetectedEvent` after a critical evaluated AI defect.

Consumers:

- `NotificationService.Infrastructure.Messaging.MissionCreatedConsumer`
- `NotificationService.Infrastructure.Messaging.DefectDetectedConsumer`

The consumers run as `BackgroundService`, use manual ack/nack, log processing failures, and create in-app notifications.

Demo:

1. Start `docker compose up -d --build`.
2. Open RabbitMQ management at `http://localhost:15672`.
3. Create a mission through `POST /api/v1/missions`; observe `MissionCreatedEvent`.
4. Submit an AI callback with a high-confidence emergency category; observe `DefectDetectedEvent` and NotificationService logs.

## Background Jobs

NotificationService owns Hangfire jobs:

- `CleanupJob`
- `DailySummaryJob`
- `PushNotificationsJob`
- `ScheduledNotificationJob`

Dashboard: `/hangfire` when Hangfire connection is configured.

RabbitMQ consumers are also meaningful background workers and are suitable for live demonstration through service logs.

## PRN232 Compliance Checklist

| Requirement | Implementation |
|---|---|
| ASP.NET Core REST API | Four REST services under `Services/*Service/Controllers` |
| RESTful CRUD | `OperationsService/Controllers/MissionController.cs` |
| Layered architecture | Each service has `Application`, `Domain`, `Infrastructure`, `Controllers` |
| JWT authentication | `Program.cs` in each REST service, `[Authorize]` controllers |
| Searching | `GET /api/v1/missions?search=...` and other list endpoints |
| Filtering | `GET /api/v1/missions?status=...`, audit/table/action filters |
| Sorting | `GET /api/v1/missions?sortBy=createdAt&sortDescending=true` |
| Pagination | `page`, `pageSize`, `PaginationMetaData` responses |
| EF Core | Service-local `Infrastructure/Persistence/ApplicationDbContext.cs` |
| Relational database | PostgreSQL/Supabase connection via `DB_CONNECTION` |
| Dependency Injection | `Application/DependencyInjection.cs`, `Infrastructure/DependencyInjection.cs` |
| Configuration management | `appsettings.json`, Docker env vars |
| Logging | Serilog console logging |
| Global exception handling | `Middlewares/GlobalExceptionHandler.cs` |
| Background processing | RabbitMQ `BackgroundService` consumers, Hangfire jobs |
| Message broker | RabbitMQ producer/consumer workflows |
| Independent gRPC service | `Services/InspectionEvaluationService` |
| REST to gRPC interaction | AI callback handler calls `IInspectionEvaluationClient` |
| Docker containerization | Dockerfile per service |
| Docker Compose deployment | `docker-compose.yml` |
| Swagger/OpenAPI | `ConfigureSwaggerOptions.cs` in REST services |
| End-to-end workflow | AI callback -> gRPC evaluation -> DB save -> RabbitMQ event -> notification consumer |
| README | This document |

## Team Responsibilities

| Member | Responsibility |
|---|---|
| Member 1 | Identity, JWT, users, roles |
| Member 2 | Operations CRUD, EF Core, PostgreSQL schema |
| Member 3 | AIInspectionService, FastAPI integration, gRPC integration |
| Member 4 | NotificationService, RabbitMQ consumers, Hangfire, deployment |

## Troubleshooting

- If `docker compose config` fails, check `.env` quoting first, especially secret values.
- If Hangfire does not start, configure `HANGFIRE_DB_CONNECTION` or `DB_CONNECTION`.
- If RabbitMQ consumers do not start, verify `rabbitmq` is healthy and `RabbitMQ__HostName=rabbitmq`.
- If gRPC evaluation is unavailable, AIInspectionService logs a warning and uses its fallback critical-alert rule to preserve existing AI callback behavior.
- If Docker restore fails without `network: host`, run the compose build path, which is configured with host networking for restore.
