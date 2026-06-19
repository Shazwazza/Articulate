#!/usr/bin/env bash
set -euo pipefail

source "$(cd "$(dirname "$0")" && pwd)/docker-common.sh"

need_cmd docker
resolve_node

PROJECT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$PROJECT_DIR"

export UMBRACO_RUNTIME_MODE="${UMBRACO_RUNTIME_MODE:-Production}"
export UMBRACO_PUBLIC_HOST="${UMBRACO_PUBLIC_HOST:-https://localhost:18443}"
export UMBRACO_PUBLIC_URL="${UMBRACO_PUBLIC_URL:-https://localhost:18443/}"

if [[ -z "${ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET:-}" ]]; then
  echo "ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET must be set." >&2
  exit 1
fi
export ARTICULATE_DEV_AUTOMATION_CLIENT_ID="${ARTICULATE_DEV_AUTOMATION_CLIENT_ID:-articulate-dev-automation}"

docker compose up -d --force-recreate

smoke_script="$PROJECT_DIR/build/docker-site/smoke.mjs"
"$NODE_BIN" "$smoke_script" smoke
"$NODE_BIN" "$smoke_script" theme
