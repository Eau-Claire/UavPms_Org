#!/usr/bin/env bash
# Copy the media files the database references as storage keys ("bucket/object", see StorageKeys) from the
# legacy Supabase public storage to MinIO, keeping the same bucket/object, so the URLs the services now build
# ({MINIO_PUBLIC_BASE_URL}/{bucket}/{object}) resolve.
#
# Usage: copy_media_to_minio.sh <dry-run|apply>
#   dry-run : download every referenced file from Supabase into a temp dir and report what is missing.
#   apply   : same, then create missing buckets (anonymous read on objects) and upload the files to MinIO.
# Environment:
#   LEGACY_SUPABASE_URL (required)  e.g. https://xyz.supabase.co
#   MINIO_ENDPOINT, MINIO_ACCESS_KEY, MINIO_SECRET_KEY (required for apply), MINIO_USE_SSL (default false)
#   DB_CONTAINER (uavpms-db), DB_USER (uavpms), DB_NAME (uavpms), AWS_CLI_IMAGE (amazon/aws-cli:2.17.0)
set -euo pipefail

MODE="${1:-dry-run}"
case "$MODE" in dry-run|apply) ;; *) echo "Usage: $0 <dry-run|apply>"; exit 2 ;; esac

: "${LEGACY_SUPABASE_URL:?LEGACY_SUPABASE_URL is required}"
DB_CONTAINER="${DB_CONTAINER:-uavpms-db}"
DB_USER="${DB_USER:-uavpms}"
DB_NAME="${DB_NAME:-uavpms}"
AWS_CLI_IMAGE="${AWS_CLI_IMAGE:-amazon/aws-cli:2.17.0}"
LEGACY_SUPABASE_URL="${LEGACY_SUPABASE_URL%/}"

# Values that are neither URLs nor local paths are storage keys.
KEYS="$(docker exec "$DB_CONTAINER" psql -X -v ON_ERROR_STOP=1 -U "$DB_USER" -d "$DB_NAME" -tA -c "
  SELECT DISTINCT k FROM (
      SELECT \"FileUrl\" AS k FROM \"InspectionMedia\"
      UNION ALL SELECT \"FileUrl\" FROM \"MaintenanceProofs\"
      UNION ALL SELECT \"ImageUrl\" FROM \"DetectedAnomalies\"
      UNION ALL SELECT \"CropUrl\" FROM \"DetectedAnomalies\"
  ) x
  WHERE k IS NOT NULL AND k <> '' AND k !~ '^(https?://|/)'
  ORDER BY 1;")"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

total=0; downloaded=0; missing=0
while IFS= read -r key; do
  key="${key%$'\r'}"
  [ -z "$key" ] && continue
  total=$((total + 1))
  # The key is the URL path (percent-encoded); MinIO decodes the path, so store the object under the decoded name.
  object="$(printf '%b' "${key//%/\\x}")"
  case "$object" in */../*|../*|*/..) echo "SKIP unsafe key: $key"; missing=$((missing + 1)); continue ;; esac
  dest="$WORK/$object"
  mkdir -p "$(dirname "$dest")"
  if curl -sfL --max-time 300 -o "$dest" "$LEGACY_SUPABASE_URL/storage/v1/object/public/$key"; then
    downloaded=$((downloaded + 1))
  else
    echo "MISSING on Supabase: $key"
    rm -f "$dest"
    missing=$((missing + 1))
  fi
done <<< "$KEYS"
find "$WORK" -mindepth 1 -type d -empty -delete

echo "Storage keys referenced: $total, downloaded: $downloaded, missing: $missing"
if [ "$total" -gt 0 ]; then
  echo "Size to copy: $(du -sh "$WORK" | cut -f1)"
  echo "Buckets: $(ls "$WORK" | tr '\n' ' ')"
fi

if [ "$MODE" != "apply" ]; then
  echo "Mode is dry-run: nothing was uploaded."
  exit 0
fi
[ "$downloaded" -eq 0 ] && { echo "Nothing to upload."; exit 0; }

: "${MINIO_ENDPOINT:?MINIO_ENDPOINT is required}"
: "${MINIO_ACCESS_KEY:?MINIO_ACCESS_KEY is required}"
: "${MINIO_SECRET_KEY:?MINIO_SECRET_KEY is required}"
SCHEME=http; [ "${MINIO_USE_SSL:-false}" = "true" ] && SCHEME=https
S3_URL="$SCHEME://$MINIO_ENDPOINT"

# On Git Bash (Windows) give docker a Windows host path and stop MSYS rewriting the in-container paths;
# both are no-ops on the Linux server.
WORK_HOST="$(cygpath -w "$WORK" 2>/dev/null || echo "$WORK")"
aws_s3() {
  MSYS2_ARG_CONV_EXCL='*' docker run --rm -v "$WORK_HOST:/work:ro" \
    -e AWS_ACCESS_KEY_ID="$MINIO_ACCESS_KEY" -e AWS_SECRET_ACCESS_KEY="$MINIO_SECRET_KEY" -e AWS_DEFAULT_REGION=us-east-1 \
    "$AWS_CLI_IMAGE" --endpoint-url "$S3_URL" "$@"
}

for bucket in $(ls "$WORK"); do
  if ! aws_s3 s3api head-bucket --bucket "$bucket" >/dev/null 2>&1; then
    aws_s3 s3api create-bucket --bucket "$bucket" >/dev/null
    aws_s3 s3api put-bucket-policy --bucket "$bucket" --policy \
      "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":[\"*\"]},\"Action\":[\"s3:GetObject\"],\"Resource\":[\"arn:aws:s3:::$bucket/*\"]}]}"
    echo "Created bucket $bucket (anonymous read on objects)"
  fi
  aws_s3 s3 cp --recursive --only-show-errors "/work/$bucket" "s3://$bucket"
  echo "Uploaded bucket $bucket"
done
echo "Done: $downloaded file(s) copied to MinIO at $S3_URL"
