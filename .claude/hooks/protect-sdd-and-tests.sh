#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if ! command -v node >/dev/null 2>&1; then
  echo "BLOCKED BY HARNESS: node is required to run protect-sdd-and-tests.mjs" >&2
  exit 2
fi

exec node "$SCRIPT_DIR/protect-sdd-and-tests.mjs"
