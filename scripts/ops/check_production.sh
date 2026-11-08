#!/usr/bin/env bash
# Production health checks for the AI pipeline. Runs on the server (needs docker access).
# Usage: check_production.sh [--strict]
#   default : configuration checks fail the run; queue backlog/stuck runs are warnings
#   --strict: every check fails the run (used by the scheduled health check)
set -u

STRICT=false
[ "${1:-}" = "--strict" ] && STRICT=true

DB_CONTAINER="${DB_CONTAINER:-uavpms-db}"
DB_USER="${DB_USER:-uavpms}"
DB_NAME="${DB_NAME:-uavpms}"
AI_CONTAINER="${AI_CONTAINER:-uav-ai-inspection-service}"
RABBIT_CONTAINER="${RABBIT_CONTAINER:-uav-rabbitmq}"
BRIDGE_CONTAINER="${BRIDGE_CONTAINER:-bridge-ai-bridge-1}"
STUCK_MINUTES="${STUCK_MINUTES:-30}"

failures=0
warnings=0
ok()   { echo "OK    $*"; }
bad()  { echo "FAIL  $*"; failures=$((failures + 1)); }
warn() { if $STRICT; then bad "$*"; else echo "WARN  $*"; warnings=$((warnings + 1)); fi; }
sql()  { docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -tAX -v ON_ERROR_STOP=1 -c "$1"; }

echo "== AI pipeline health ($(date -u +%FT%TZ), strict=$STRICT)"

# 1. The mock AI consumer must be off; it competes with the bridge for requests.
mock="$(docker exec "$AI_CONTAINER" printenv MockAI__Enabled 2>/dev/null || echo unset)"
if [ "$mock" = "false" ]; then ok "MockAI disabled"; else bad "MockAI__Enabled=$mock (set MOCK_AI_ENABLED=false in the BE .env)"; fi

# 2. Exactly one consumer (the bridge) per server request queue.
# Right after a deploy the bridge may still be reconnecting, so give it up to a minute.
queues=""
for attempt in 1 2 3 4 5 6 7; do
  queues="$(docker exec "$RABBIT_CONTAINER" rabbitmqctl -q list_queues name messages consumers 2>/dev/null)"
  [ "$(awk '$1=="ai.analysis.server.image.requested" {print $3}' <<<"$queues")" = "1" ] && break
  [ "$attempt" -lt 7 ] && sleep 10
done
if [ -z "$queues" ]; then
  bad "cannot list RabbitMQ queues in $RABBIT_CONTAINER"
else
  for q in ai.analysis.server.image.requested ai.analysis.server.video.requested; do
    c="$(awk -v q="$q" '$1==q {print $3}' <<<"$queues")"
    if [ "$c" = "1" ]; then ok "$q has 1 consumer"; else bad "$q has ${c:-no} consumers (expected exactly 1: the bridge)"; fi
  done
  for q in ai.analysis.result.dead-letter ai.analysis.dead-letter; do
    n="$(awk -v q="$q" '$1==q {print $2}' <<<"$queues")"
    if [ "${n:-0}" = "0" ]; then ok "$q is empty"; else warn "$q holds $n message(s)"; fi
  done
fi

# 3. Bridge container is running.
if [ "$(docker inspect -f '{{.State.Running}}' "$BRIDGE_CONTAINER" 2>/dev/null)" = "true" ]; then
  ok "bridge container running"
else
  bad "bridge container $BRIDGE_CONTAINER is not running"
fi

# 4. Every AiLabel:* mapping points to an existing defect category (or NONE).
broken="$(sql "SELECT s.\"Key\" || ' -> ' || coalesce(s.\"Value\", '') FROM \"SystemSettings\" s
  WHERE s.\"Key\" LIKE 'AiLabel:%' AND NOT s.\"IsDeleted\" AND upper(coalesce(s.\"Value\", '')) <> 'NONE'
  AND NOT EXISTS (SELECT 1 FROM \"DefectCategories\" c WHERE c.\"CategoryCode\" = s.\"Value\")" 2>&1)"
if [ $? -ne 0 ]; then bad "cannot read AiLabel mappings: $broken"
elif [ -n "$broken" ]; then bad "AiLabel mappings without a DefectCategory: $(tr '\n' ' ' <<<"$broken")"
else ok "AiLabel mappings resolve to DefectCategories"; fi

# 5. Codes the bridge sends (categories.json) exist as categories or AiLabel keys.
mapping="$(docker exec "$BRIDGE_CONTAINER" sh -c 'cat "${CATEGORY_MAP:-/config/categories.json}"' 2>/dev/null)"
if [ -z "$mapping" ]; then
  warn "cannot read bridge categories.json"
else
  codes="$(grep -o ':[[:space:]]*"[^"]*"' <<<"$mapping" | sed 's/^:[[:space:]]*"//; s/"$//' | sort -u)"
  missing=""
  for code in $codes; do
    found="$(sql "SELECT 1 FROM \"DefectCategories\" WHERE \"CategoryCode\" = '${code//\'/}'
      UNION SELECT 1 FROM \"SystemSettings\" WHERE \"Key\" = 'AiLabel:${code//\'/}' AND NOT \"IsDeleted\" LIMIT 1")"
    [ -z "$found" ] && missing="$missing $code"
  done
  if [ -z "$missing" ]; then ok "bridge category codes are known to the BE"; else bad "bridge sends unknown codes:$missing"; fi
fi

# 6. No analysis run stuck in Pending/Processing.
stuck="$(sql "SELECT count(*) FROM \"AIAnalysisRuns\" WHERE NOT \"IsDeleted\"
  AND \"Status\" IN ('Pending','Processing') AND \"CreatedAt\" < now() - interval '$STUCK_MINUTES minutes'" 2>&1)"
if [ "$stuck" = "0" ]; then ok "no runs stuck over $STUCK_MINUTES min"; else warn "$stuck run(s) stuck in Pending/Processing over $STUCK_MINUTES min"; fi

echo "== $failures failure(s), $warnings warning(s)"
[ "$failures" -eq 0 ]
