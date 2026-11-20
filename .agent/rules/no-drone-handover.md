# Architectural Rule: DroneHandover Is Deprecated / Removed

- The **DroneHandover** workflow (manual pre-flight UAV handover / return handover) has been **removed from business scope and UI**.
- **NEVER** include `DroneHandover`, `DeliverDroneHandoverCommandHandler`, `ReturnDroneHandoverCommandHandler`, `Handover()`, or `ReturnHandover()` in:
  - Detailed Design diagrams (Class Diagrams, Sequence Diagrams, Activity Diagrams)
  - Class Specifications or SDD / Report documentation (especially **MF03 – Execute Mission and Perform Field Inspection**)
  - New frontend or backend feature implementations
- **Why legacy references may still appear when scanning code:** `OperationsService` still retains internal/legacy `DroneHandover` entity/handlers/tests for backward compatibility until a dedicated cleanup migration removes them. Treat those backend files and `missions-api.ts` handover methods as deprecated legacy code, NOT active MF03 design components.

