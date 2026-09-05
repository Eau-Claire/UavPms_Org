# MF01 Repository Cleanup Audit

This document records the cleanup targets for a later, separately reviewed
refactor. It is not an authorization to delete code.

## Confirmed targets from source inspection

- `Asset`, `Mission`, `Tower`, and related repositories are duplicated across
  IdentityService, AIInspectionService, NotificationService, and
  OperationsService. Their ownership and read-model boundaries must be made
  explicit before consolidation.
- Asset table assumptions were inconsistent across services. OperationsService
  uses `AssetComponents`; the other services were corrected in commit
  `ac5e2cf`. Any further schema change must preserve this compatibility fix.
- MF01 has one OperationsService upload handler at
  `POST /api/v1/inspections/upload`. It must not be removed or renamed until
  frontend, gateway, tests, and external callers are checked.
- AI analysis currently exposes existing-media analysis, status/detection
  queries, review, and the vision callback routes in
  `AIAnalysisController` and `VisionBridgeController`. The absolute route
  attributes in these controllers require a route inventory and gateway check.
- Legacy AI model artifacts such as `AIAnalysisRequest.BatchId` and historical
  migration/designer files remain in the repository. They require caller,
  database, and production migration-history verification before removal.
- Background consumers are registered in service `Program.cs` files. Their
  RabbitMQ exchanges, routing keys, queues, retries, and dead-letter behavior
  must be mapped before any consumer is consolidated or deleted.

## Required evidence before deletion

For every candidate removal, verify controllers/routes, frontend calls,
MediatR registrations, DI registration, producers, consumers, reflection-based
registration, scheduled workers, tests, API gateway routes, and external
integration contracts.

Produce a RabbitMQ topology report containing event, producer, exchange,
routing key, queue, consumer, handler, retry/dead-letter behavior, and whether
multiple consumers are intentional.

Produce an AI/Inspection route inventory identifying absolute routes, duplicate
routes, upload paths, deprecated paths, and canonical-flow bypasses.

Classify each suspicious `return null` as an optional result, validation
failure, domain conflict, infrastructure failure, or programming/configuration
error. Do not replace null globally.

## Likely modules for the next phase

- `Services/*Service/*API/Controllers`
- `Services/*Service/*Application/Features`
- `Services/*Service/*Infrastructure/DependencyInjection.cs`
- `Services/*Service/*Infrastructure/Messaging`
- `Services/*Service/*Infrastructure/Persistence/Configurations`
- `Services/OperationsService/UavPms.OperationsService.Infrastructure/Migrations`
- `UavPms.ApiGateway` route configuration
- frontend repository usage and deployment configuration

## Must not be deleted yet

- Any RabbitMQ consumer or queue before topology and runtime callers are
  confirmed.
- `InspectionController` upload endpoint before frontend, gateway, tests, and
  external callers are checked.
- `BatchId` or related migrations before current database schema and production
  migration history are verified.
- Cross-service entities/repositories before ownership and read-model needs are
  documented.
- Existing migrations that may already have been applied to shared or
  production databases.
- Any `return null` path without classifying its contract and failure semantics.

## Separation from diagnostics

The read-only server diagnostics workflow is intentionally separate from this
cleanup preparation. It performs no deployment, restart, migration, pull,
database write, or file modification. Cleanup must begin only after production
logs and the required caller/topology evidence are available.
