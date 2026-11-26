---
name: dotnet-feature
description: Implements or reviews UAV-PMS .NET 9 backend features using CQRS/MediatR, the canonical API envelope, database-side pagination, multi-role and geographic authorization, and soft delete. Use for service feature work, handlers, controllers, queries, or tests.
---

# .NET Feature Guardrails

Read `docs/MASTER_SYSTEM_SPEC.md`, `AGENTS.md`, and `readable-code-defense` first.

## Implementation order

1. Confirm the requirement, owning service, roles, geographic scope, and soft-delete behavior.
2. Put commands/queries and handlers in Application; keep controllers transport-only.
3. Validate input and authorization with explicit guard clauses before side effects.
4. Use the shared canonical `ApiResponse`; do not introduce another envelope type.
5. Query through EF Core `IQueryable`; apply filters and ordering before pagination.
6. Use 1-based pages, validate positive values, and enforce `pageSize <= 100`.
7. Execute `CountAsync`, then `Skip`/`Take`; never paginate an in-memory collection.
8. Check the union of all user roles. Never use a single-role shortcut.
9. Apply `GeographicAccessFilter` to every scoped entity query except SystemAdmin access.
10. Exclude soft-deleted rows and perform deletion with `IsDeleted = true` where required.
11. Add focused tests for denied role, combined roles, out-of-scope region, page bounds, and deletion.

## Required response shapes

- Single result: `{ success, data, error }`.
- Paged result: `{ success, data: { items, pagination }, error }`.
- Error: `{ success: false, data: null, error: { code, message } }`.
- Keep all JSON camelCase and avoid leaking exception details.

## Stop conditions

- Get human approval before touching any zone marked in `AGENTS.md`.
- Do not add mock/fake application data or an OTP bypass.
- Do not apply migrations locally; migrations run through GitHub Actions.

Finish with the `readable-code-defense` Vietnamese walkthrough.
