# Acceptance criteria: <name>

## Functional

- [ ] AC-01: Given <context>, when <action>, then <observable result>.
- [ ] AC-02: The response uses the canonical camelCase `ApiResponse` envelope.

## Authorization and security

- [ ] AC-03: Allowed multi-role combinations succeed.
- [ ] AC-04: Disallowed roles and out-of-scope regions return the defined error.
- [ ] AC-05: Soft-deleted data is neither returned nor physically deleted.

## Data and pagination

- [ ] AC-06: Filters, order, `Skip`, and `Take` execute through `IQueryable`.
- [ ] AC-07: Pagination is 1-based and `pageSize` never exceeds 100.

## Verification

- [ ] Focused service tests pass.
- [ ] `dotnet build UavPms.sln` passes.
- [ ] OpenAPI and handoff are updated when the contract changes.
