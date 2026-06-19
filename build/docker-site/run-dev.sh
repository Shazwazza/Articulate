#!/usr/bin/env bash
set -euo pipefail

source "$(cd "$(dirname "$0")" && pwd)/docker-common.sh"

wait_for_umbraco_ready() {
  # The chiseled image has no shell/healthcheck, so probe the public Umbraco URL directly.
  local public_base="${UMBRACO_PUBLIC_URL:-https://localhost:18443/}"
  wait_for_url "${public_base%/}/umbraco"
}

wait_for_url() {
  local url="$1"
  local deadline=$((SECONDS + 300))
  local curl_tls_args=()

  case "$url" in
    https://localhost:*|https://localhost/*|https://127.0.0.1:*|https://127.0.0.1/*|https://[::1]:*|https://[::1]/*)
      curl_tls_args=(-k)
      ;;
  esac

  while (( SECONDS < deadline )); do
    local code
    code="$(curl "${curl_tls_args[@]}" -sS -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || true)"
    if [[ "$code" == "200" || "$code" == "302" ]]; then
      return 0
    fi
    sleep 2
  done

  echo "Timed out waiting for $url to become reachable." >&2
  exit 1
}

need_cmd docker
need_cmd curl

PROJECT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$PROJECT_DIR"

export UMBRACO_RUNTIME_MODE="${UMBRACO_RUNTIME_MODE:-BackofficeDevelopment}"
SKIP_PUBLISH="${SKIP_PUBLISH:-false}"
RESET_DOCKER_VOLUMES="${RESET_DOCKER_VOLUMES:-false}"

if [[ "$SKIP_PUBLISH" != "true" ]]; then
  resolve_node

  if [[ -z "${ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET:-}" ]]; then
    echo "ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET must be set." >&2
    exit 1
  fi

  export ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET
  export ARTICULATE_DEV_AUTOMATION_CLIENT_ID="${ARTICULATE_DEV_AUTOMATION_CLIENT_ID:-articulate-dev-automation}"
  export UMBRACO_PUBLIC_URL="${UMBRACO_PUBLIC_URL:-https://localhost:18443/}"
fi

case "$RESET_DOCKER_VOLUMES" in
  true|1|yes)
    echo "Resetting Docker containers and volumes before dev run."
    docker compose down -v
    ;;
  false|0|no)
    ;;
  *)
    echo "Unsupported RESET_DOCKER_VOLUMES value: $RESET_DOCKER_VOLUMES" >&2
    exit 1
    ;;
esac

docker compose up -d
if [[ -z "$(docker compose ps -q articulate)" ]]; then
  echo "Could not find the articulate service container id." >&2
  exit 1
fi

wait_for_umbraco_ready

smoke_script="$PROJECT_DIR/build/docker-site/smoke.mjs"
if [[ "$SKIP_PUBLISH" != "true" ]]; then
  "$NODE_BIN" "$smoke_script" publish
  "$NODE_BIN" "$smoke_script" confirm
fi
