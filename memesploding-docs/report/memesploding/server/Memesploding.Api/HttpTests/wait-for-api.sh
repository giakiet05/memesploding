#!/usr/bin/env bash
set -euo pipefail

API_HEALTH_URL="${API_HEALTH_URL:-http://api:5217/openapi/v1.json}"
WAIT_TIMEOUT_SECONDS="${WAIT_TIMEOUT_SECONDS:-180}"
WAIT_INTERVAL_SECONDS="${WAIT_INTERVAL_SECONDS:-2}"

start_ts=$(date +%s)

echo "Waiting for API to be ready at: ${API_HEALTH_URL}" >&2

while true; do
  if curl -sf "${API_HEALTH_URL}" > /dev/null; then
    echo "API is ready." >&2
    exit 0
  fi

  now_ts=$(date +%s)
  elapsed=$((now_ts - start_ts))
  if [ "${elapsed}" -ge "${WAIT_TIMEOUT_SECONDS}" ]; then
    echo "Timed out after ${WAIT_TIMEOUT_SECONDS}s waiting for API." >&2
    exit 1
  fi

  sleep "${WAIT_INTERVAL_SECONDS}"
done
