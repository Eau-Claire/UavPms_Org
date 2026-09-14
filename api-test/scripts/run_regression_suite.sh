#!/usr/bin/env bash
set -euo pipefail

FILTER="${1:-}"

echo "========== 2. STARTING API-TEST AND GATEWAY CONTAINERS =========="
docker compose -f docker-compose.yml -f docker-compose.test.yml up -d --build gateway api-test

echo "Waiting for api-test service to become healthy..."
for attempt in $(seq 1 25); do
  if curl -sf http://localhost:8081/api/health >/dev/null 2>&1; then
    echo "api-test is healthy on attempt $attempt."
    break
  fi
  sleep 2
done

echo "========== 3. RUNNING REGRESSION TESTS =========="
TEST_EXIT_CODE=0
case "$FILTER" in
  ""|*[Oo][Pp][Tt][Ii][Oo][Nn][Aa][Ll]*|*[Nn][Oo][Nn][Ee]*|*[Aa][Ll][Ll]*|*[Nn][Uu][Ll][Ll]*)
    echo "Running full test suite..."
    docker exec uav-api-test pytest -v || TEST_EXIT_CODE=$?
    ;;
  *)
    echo "Running filtered tests (-k \"$FILTER\")..."
    docker exec uav-api-test pytest -v -k "$FILTER" || TEST_EXIT_CODE=$?
    ;;
esac

echo "========== 4. LATEST TEST RUN STATUS =========="
if command -v jq >/dev/null 2>&1; then
  curl -s http://localhost:8081/api/results | jq '.summary // .' || true
else
  curl -s http://localhost:8081/api/results || true
fi
echo ""

if [ "$TEST_EXIT_CODE" -ne 0 ]; then
  echo "Regression tests finished with exit code $TEST_EXIT_CODE"
  exit "$TEST_EXIT_CODE"
fi

echo "All regression tests passed successfully!"
