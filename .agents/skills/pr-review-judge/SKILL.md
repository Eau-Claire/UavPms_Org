---
name: pr-review-judge
description: Performs an independent merge-gate review of UAV-PMS pull requests for scope, OWASP and RBAC security, test integrity, and readable-code-defense compliance. Use when reviewing a PR, diff, branch, or proposed backend change.
---

# Independent PR Review Judge

Read `AGENTS.md`, `docs/MASTER_SYSTEM_SPEC.md`, and `readable-code-defense` before judging.
Review only the diff plus the minimum surrounding code needed to prove each finding.

## Rubric (100 points)

- Scope and contract — 25: requested behavior only, correct service boundary, spec/RTM/OpenAPI aligned.
- Security — 30: OWASP risks, input validation, secrets, auth, multi-role RBAC, IDOR, geographic scope.
- Test integrity — 25: meaningful assertions, failure paths, no weakened/skipped tests, no fake production flow.
- Readability — 20: linear flow, useful names, no nested ternary/helper bloat, explainable business comments.

## Merge gate

1. List findings first, ordered P0 to P3, with exact file and line evidence.
2. Treat OTP bypass, authorization bypass, cross-region access, secret exposure, or destructive migration risk as P0.
3. Treat missing negative authorization tests or in-memory pagination as at least P1.
4. Verify Human-Approval-Required files have explicit approval evidence.
5. Do not invent findings. State residual risk when evidence cannot be obtained.
6. Recommend merge only with no P0/P1 findings and a score of at least 85.

## Output

Report: verdict, score by rubric, findings, test evidence, residual risks, and required fixes.
If no actionable finding exists, say so explicitly; do not manufacture feedback.
