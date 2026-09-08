#!/usr/bin/env bash
# Invoked by the protected workflow with a hashed, generated seed file.
set -euo pipefail
mf01_database="${1:-}"
mf01_seed_file="${2:-}"
[[ "$mf01_database" =~ ^mf01_test[a-zA-Z0-9_]*$ ]] || { echo 'Expected database named mf01_test*'; exit 1; }
[[ -s "$mf01_seed_file" ]] || { echo 'Seed file missing'; exit 1; }
[[ "$(docker inspect uavpms-db --format '{{.State.Status}}')" == running ]] || { echo 'uavpms-db is not running'; exit 1; }
# Connect to the selected database directly: absence is an error, never a
# fallback to the application database. Migrations remain a separate operation.
docker exec uavpms-db psql -X -v ON_ERROR_STOP=1 -U uavpms -d "$mf01_database" -tAc \
  "SELECT current_database(), postgis_version();"
# ON_ERROR_STOP and the generator's explicit transaction ensure SQL/verification
# failures roll back every insert. The password is already hashed on the runner.
docker exec -i uavpms-db psql -X -q -v ON_ERROR_STOP=1 -U uavpms -d "$mf01_database" < "$mf01_seed_file"
echo "MF01 seed transaction committed in $mf01_database."
