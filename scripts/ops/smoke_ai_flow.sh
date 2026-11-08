#!/usr/bin/env bash
# End-to-end smoke test of the server AI flow, run on the server after a deploy:
# login -> upload a known bird-nest image -> wait for the AI run -> check the stored anomaly -> clean up.
#
# Required env: SMOKE_API_URL, SMOKE_EMAIL, SMOKE_PASSWORD, SMOKE_MISSION_ID
# Optional env: SMOKE_OTP (needed when login asks for an OTP), SMOKE_TIMEOUT (seconds, default 180),
#               SMOKE_REQUIRE_DETECTION (default true), SMOKE_FIXTURE, DB_CONTAINER/DB_USER/DB_NAME
set -u

if [ -z "${SMOKE_API_URL:-}" ] || [ -z "${SMOKE_EMAIL:-}" ] || [ -z "${SMOKE_PASSWORD:-}" ] || [ -z "${SMOKE_MISSION_ID:-}" ]; then
  echo "SKIP  SMOKE_API_URL/SMOKE_EMAIL/SMOKE_PASSWORD/SMOKE_MISSION_ID not all set; AI smoke test skipped."
  exit 0
fi
API="${SMOKE_API_URL%/}"
TIMEOUT="${SMOKE_TIMEOUT:-180}"
REQUIRE_DETECTION="${SMOKE_REQUIRE_DETECTION:-true}"
FIXTURE="${SMOKE_FIXTURE:-$(dirname "$0")/../../tests/smoke/fixtures/bird_nest.jpg}"
DB_CONTAINER="${DB_CONTAINER:-uavpms-db}"
DB_USER="${DB_USER:-uavpms}"
DB_NAME="${DB_NAME:-uavpms}"

sql() { docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -tAX -v ON_ERROR_STOP=1 -c "$1"; }
json_field() { grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -1 | sed 's/.*:[[:space:]]*"//; s/"$//'; }
die() { echo "FAIL  $*"; exit 1; }

work="$(mktemp -d)"
media_id=""
cleanup() {
  if [ -n "$media_id" ]; then
    # Soft-delete everything the smoke test created so it never shows up for analysts.
    sql "UPDATE \"DetectedAnomalies\" SET \"IsDeleted\" = true, \"DeletedAt\" = now() WHERE \"MediaId\" = '$media_id';
         UPDATE \"AIAnalysisRuns\" SET \"IsDeleted\" = true, \"DeletedAt\" = now() WHERE \"MediaId\" = '$media_id';
         UPDATE \"InspectionMedia\" SET \"IsDeleted\" = true, \"DeletedAt\" = now() WHERE \"Id\" = '$media_id';" >/dev/null \
      && echo "INFO  smoke data soft-deleted (media $media_id)"
  fi
  rm -rf "$work"
}
trap cleanup EXIT

[ -f "$FIXTURE" ] || die "fixture not found: $FIXTURE"
echo "== AI smoke test against $API (mission $SMOKE_MISSION_ID)"

# 1. Login (with OTP when the account requires it).
# Services may still be warming up right after a deploy: retry only while the API is unreachable
# or answers 5xx. A real answer (success or rejected credentials) stops immediately, so a wrong
# password never hammers the account.
login=""
for attempt in 1 2 3 4 5 6; do
  code="$(curl -sS -o "$work/login.json" -w '%{http_code}' -X POST "$API/api/v1/auth/login" \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"$SMOKE_EMAIL\",\"password\":\"$SMOKE_PASSWORD\"}" 2>/dev/null || echo 000)"
  login="$(cat "$work/login.json" 2>/dev/null)"
  case "$code" in 000|5??) ;; *) break ;; esac
  echo "INFO  login attempt $attempt: API not ready (HTTP $code), retrying"
  sleep 10
done
grep -q '"success"[[:space:]]*:[[:space:]]*true' <<<"$login" \
  || die "login rejected (HTTP $code): $(head -c 200 <<<"$login") - check the SMOKE_EMAIL/SMOKE_PASSWORD secrets"
token="$(json_field accessToken <<<"$login")"
if [ -z "$token" ] && grep -q 'OTP required' <<<"$login"; then
  [ -n "${SMOKE_OTP:-}" ] || die "login requires an OTP but SMOKE_OTP is not set"
  verify="$(curl -sS -X POST "$API/api/v1/auth/otp/verify" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$SMOKE_EMAIL\",\"otp\":\"$SMOKE_OTP\",\"purpose\":\"Login\"}")" || die "OTP request failed"
  token="$(json_field accessToken <<<"$verify")"
fi
[ -n "$token" ] || die "no access token (login response: $(head -c 300 <<<"$login"))"
echo "OK    logged in"

# 2. Upload a unique copy of the fixture so checksum dedup never short-circuits. A JPEG comment
#    segment (FF FE, length 0x001C = 2 + 26 bytes) is inserted right after SOI: still a valid JPEG.
{
  head -c 2 "$FIXTURE"
  printf '\377\376\000\034'
  printf 'uavpms-smoke-%013d' "$(date +%s%3N)"
  tail -c +3 "$FIXTURE"
} >"$work/smoke.jpg"
upload="$(curl -sS -X POST "$API/api/v1/missions/$SMOKE_MISSION_ID/media" \
  -H "Authorization: Bearer $token" \
  -F "file=@$work/smoke.jpg;type=image/jpeg" \
  -F "capturedAt=$(date -u +%FT%TZ)")" || die "upload request failed"
media_id="$(json_field mediaId <<<"$upload")"
[ -n "$media_id" ] || die "upload did not return a mediaId: $(head -c 300 <<<"$upload")"
grep -q '"isDuplicate"[[:space:]]*:[[:space:]]*true' <<<"$upload" && die "upload was treated as a duplicate"
echo "OK    uploaded media $media_id"

# 3. Wait for the AI run to reach a terminal state.
deadline=$((SECONDS + TIMEOUT))
status=""
while [ $SECONDS -lt $deadline ]; do
  status="$(sql "SELECT \"Status\" || '|' || coalesce(\"ModelName\", '') || '|' || coalesce(\"ErrorMessage\", '')
    FROM \"AIAnalysisRuns\" WHERE \"MediaId\" = '$media_id' ORDER BY \"CreatedAt\" DESC LIMIT 1")"
  case "$status" in Completed\|*|Failed\|*) break ;; esac
  sleep 5
done
[ -n "$status" ] || die "no AI run was created for the upload within ${TIMEOUT}s (outbox/consumer issue)"
case "$status" in
  Completed\|*) echo "OK    run completed (model $(cut -d'|' -f2 <<<"$status"))" ;;
  Failed\|*)    die "run failed: ${status#Failed|}" ;;
  *)            die "run still '${status%%|*}' after ${TIMEOUT}s (bridge did not answer)" ;;
esac

# 4. Check the stored anomalies.
anomalies="$(sql "SELECT c.\"CategoryCode\" || ' (' || round(a.\"ConfidenceScore\"::numeric, 2) || ', ' || a.\"ValidationStatus\" || ')'
  FROM \"DetectedAnomalies\" a JOIN \"DefectCategories\" c ON c.\"Id\" = a.\"CategoryId\" WHERE a.\"MediaId\" = '$media_id'")"
if [ -n "$anomalies" ]; then
  echo "OK    anomalies stored: $(tr '\n' ' ' <<<"$anomalies")"
  grep -qv 'InReview' <<<"$anomalies" && die "anomaly not in InReview"
elif [ "$REQUIRE_DETECTION" = "true" ]; then
  die "no anomaly stored for the bird-nest fixture (model missed it, or labels/categories are misconfigured)"
else
  echo "WARN  no anomaly stored"
fi

echo "== AI smoke test passed"
