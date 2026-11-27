---
name: pr-review-judge
description: Independent LLM-as-Judge PR review rubric for UAV-PMS Backend (.NET 9) with a strict Pragmatic Scope Ceiling to prevent banking-level SRE over-engineering.
---

# Independent PR Review Judge (Backend — .NET 9)

Run this skill in a **separate, clean agent session** (never the same session that generated the code). Read-only: NEVER modify files.

## Inputs Required

1. `git diff` (staged or working tree for the assigned slice).
2. Linked `specs/<BE-REQ-ID>/spec.md` and `specs/<BE-REQ-ID>/acceptance.md`.

## Review Rubric (Check in Order)

1. **Scope & Small Batch Discipline**:
   - Verify only files belonging to the assigned Vertical Slice were modified (no unrelated WIP or untracked specs leaked into the diff).
   - Verify Human-Approval-Required zones (`IdentityService/Auth`, `.github/workflows/**`) match explicit approval.
2. **Correctness & Real Security (P0 / Gate 5)**:
   - Compare every acceptance criterion in `acceptance.md` for this slice against implementation and tests.
   - Block real bugs: broken Mainflow, missing/bypassable auth or RBAC/ownership checks (IDOR, open relay, exposed internal routes, client SignalR broadcast), transaction/revocation races, secret/PII/JWT/OTP leaks in logs/responses, or compile/runtime failures.
3. **Test Integrity (Anti "Perpetually Green")**:
   - Verify Gate 5 tests actually run and pass in `dotnet test` (no missing external URL setup errors that turn `ci.yml` red) and fail if the core security/business rule is broken.
4. **Code Readability & Defense Readiness (`readable-code-defense/SKILL.md`)**:
   - Linear `if/return` flow (max 2 nesting levels), no nested ternaries, no magic strings/numbers, plain Vietnamese WHY comments, and a 5–8 line defense walkthrough.
5. **Pragmatic Scope Ceiling (Anti-Banking Over-Engineering)**:
   - Context: graduation project / real operational MVP — **NOT** Tier-1 Banking SRE.
   - **Blocking ONLY for real defects** (broken Mainflow, real P0 security/RBAC/PII flaws, real runtime/CI-breaking bugs, test integrity failures, or WIP leaks).
   - **NEVER Blocking** for hypothetical enterprise SRE scenarios (e.g., live `pg_hba.conf` rewrite, spinning up a second DB cluster on a VPS, internal runner tampering, or cosmetic refactors) — place those in **Non-blocking (max 3)** or backlog.
   - Target **at most 1 fix round** per slice. When the Human Owner explicitly accepts a risk or defers an item to backlog, respect it immediately and do not re-block.

## Output Format

Return a concise verdict: **`APPROVE_FOR_HUMAN_REVIEW`** or **`CHANGES_REQUESTED`**, followed by:
- **Blocking issues** (`file:line` + why + concrete fix)
- **Non-blocking suggestions** (max 3)
- **Uncovered acceptance criteria** (if any, scoped to this slice only)
