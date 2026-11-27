---
name: readable-code-defense
description: Enforces clear, linear, human-defendable code (no clever one-liners, no helper bloat, plain business comments, and a short defense walkthrough) for both Frontend (Angular/TS) and Backend (.NET/C#).
---

# Readable & Defendable Code Standard (FE + BE)

Load this skill for **every** coding and review task. The team must be able to open any file during a live code defense / audit and explain every line in plain language without guessing.

## 1. Anti "Clever Code" Rules (FE & BE)

- **No nested ternaries**: Replace `a ? b : c ? d : e` with explicit `if / else if / return`.
- **No dense one-liner chains**: Never chain > 3 array/LINQ/RxJS operators with complex callbacks on one expression. Extract well-named intermediate variables (`const approvedDefects = ...;`).
- **Linear flow with early returns (Max 2-level `if` nesting)**: Structure every handler/method from top to bottom like a business checklist and keep `if` nesting to **at most 2 levels** using guard clauses (`if (!valid) return;`):
  1. Validate input & permissions (guard clauses with `return` / error).
  2. Perform core action (API call / DB query).
  3. Update state / return explicit result.
- **No Magic Strings or Magic Numbers**: Never scatter raw status strings (`'PendingAcceptance'`, `'approved'`, `'Emergency'`) or unexplained numbers (`30`, `20971520`) across logic. Always reference named enums or constants (e.g., `MissionStatus.PendingAcceptance`, `MAX_IMAGE_UPLOAD_BYTES`).
- **Keep functions digestible**: Aim for ~15–40 lines per function. Do NOT cram 150 lines into one function, and do NOT shatter a simple 10-line flow into 5 tiny 2-line helpers.

## 2. Anti Helper Bloat Rules

- **Rule of Two**: Only extract a local helper function if it is used in **2+ places** OR encapsulates a clearly named, non-trivial business rule (e.g. `parseDateInputToIso`).
- **Reuse shared utilities first**:
  - **Frontend (`UavPms_FEE`)**: Reuse `src/app/shared/utils/` for JSON payload parsing and `dd/MM/yyyy` date helpers. Do NOT copy-paste `record()`, `stringValue()`, `optionalString()`, `numberValue()`, or `parseDate()` into feature files.
  - **Backend (`UavPms_BEE`)**: Reuse shared `ApiResponse`, pagination, and `GeographicAccessFilter` utilities. Do NOT duplicate pagination or role-check logic across controllers/handlers.
- **No premature abstraction**: Avoid generic factories, multi-layer wrapper interfaces, or meta-programming when a direct, readable service/method works cleanly.

## 3. Plain-Language Naming & Comments

- **Business names, not cryptic abbreviations**: Use `canEditMission`, `isEmergencyDefect`, `releaseDroneOnComplete` — never `chkMsn`, `tmp2`, `resObj`, `fn`.
- **Explain WHY, not academic jargon**:
  - Write short, plain Vietnamese (or simple English) comments only for non-obvious business rules or edge cases (e.g., `// Xóa cache để sau lỗi 403 không hiển thị lại dữ liệu cũ`).
  - **Forbidden**: Overly academic jargon comments ("idempotent monadic projection", "polymorphic boundary invariant") or useless comments that repeat syntax (`// set loading to true`).

## 4. Mandatory Defense Walkthrough (Output After Every Coding Batch)

Whenever you finish writing or modifying code, include a short **"Tóm tắt luồng để bảo vệ code"** (5–8 bullet lines in plain Vietnamese):
1. File nào vừa sửa và vai trò chính của nó.
2. Luồng dữ liệu đi qua các bước nào (từ nút bấm / endpoint → xử lý → kết quả).
3. Những edge case quan trọng đã chặn và nằm ở hàm nào (để người phát triển mở đúng dòng giải thích khi bị hỏi).
