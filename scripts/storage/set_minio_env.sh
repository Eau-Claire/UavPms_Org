#!/usr/bin/env bash
# Upsert the MINIO_* settings into a docker compose .env file (default ./.env), keeping every other line.
# Values come from the environment: MINIO_ENDPOINT, MINIO_ACCESS_KEY, MINIO_SECRET_KEY (required),
# MINIO_BUCKET (uav-media), MINIO_USE_SSL (false), MINIO_PUBLIC_BASE_URL (optional, empty = endpoint).
# The previous file is kept as .env.bak.<UTC timestamp>; the new file is chmod 600.
set -euo pipefail

ENV_FILE="${1:-.env}"
: "${MINIO_ENDPOINT:?MINIO_ENDPOINT is required}"
: "${MINIO_ACCESS_KEY:?MINIO_ACCESS_KEY is required}"
: "${MINIO_SECRET_KEY:?MINIO_SECRET_KEY is required}"
MINIO_BUCKET="${MINIO_BUCKET:-uav-media}"
MINIO_USE_SSL="${MINIO_USE_SSL:-false}"
MINIO_PUBLIC_BASE_URL="${MINIO_PUBLIC_BASE_URL:-}"

case "$MINIO_ENDPOINT" in
  http://*|https://*) echo "ERROR: MINIO_ENDPOINT is host:port without scheme (e.g. 116.118.3.58:9000)"; exit 1 ;;
  *:9001) echo "ERROR: 9001 is the MinIO console; the S3 API is usually on 9000"; exit 1 ;;
esac
case "$MINIO_USE_SSL" in true|false) ;; *) echo "ERROR: MINIO_USE_SSL must be true or false"; exit 1 ;; esac

# docker compose reads single-quoted values literally but has no escape for a quote inside them.
quote() {
  if [[ "$1" =~ ^[A-Za-z0-9._:/@+=-]*$ ]]; then printf '%s' "$1"
  elif [[ "$1" == *"'"* ]]; then echo "ERROR: a MINIO_* value contains a single quote, which a compose .env cannot hold; generate another key." >&2; exit 1
  else printf "'%s'" "$1"; fi
}
for value in "$MINIO_ENDPOINT" "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY" "$MINIO_BUCKET" "$MINIO_PUBLIC_BASE_URL"; do
  quote "$value" >/dev/null
done

touch "$ENV_FILE"
cp -p "$ENV_FILE" "$ENV_FILE.bak.$(date -u +%Y%m%d%H%M%S)"
tmp="$(mktemp "$ENV_FILE.XXXXXX")"
grep -v -E '^(MINIO_ENDPOINT|MINIO_ACCESS_KEY|MINIO_SECRET_KEY|MINIO_BUCKET|MINIO_USE_SSL|MINIO_PUBLIC_BASE_URL)=' "$ENV_FILE" > "$tmp" || true
{
  printf 'MINIO_ENDPOINT=%s\n' "$(quote "$MINIO_ENDPOINT")"
  printf 'MINIO_ACCESS_KEY=%s\n' "$(quote "$MINIO_ACCESS_KEY")"
  printf 'MINIO_SECRET_KEY=%s\n' "$(quote "$MINIO_SECRET_KEY")"
  printf 'MINIO_BUCKET=%s\n' "$(quote "$MINIO_BUCKET")"
  printf 'MINIO_USE_SSL=%s\n' "$MINIO_USE_SSL"
  printf 'MINIO_PUBLIC_BASE_URL=%s\n' "$(quote "$MINIO_PUBLIC_BASE_URL")"
} >> "$tmp"
chmod 600 "$tmp"
mv "$tmp" "$ENV_FILE"

echo "Updated $ENV_FILE:"
grep -E '^MINIO_' "$ENV_FILE" | sed -E 's/^(MINIO_(ACCESS|SECRET)_KEY)=.*/\1=***/'
