#!/usr/bin/env bash
# Multi-lane Docker smoke test orchestrator.
# Delegates all container management to docker-compose.yml; no docker run.
#
# Usage:
#   ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET='...' ./build/docker-site/test.sh [umbraco17|umbraco18|all] [--keep] [--skip-smoke]
set -euo pipefail

CHOICE="${1:-all}"
KEEP=false
SKIP_SMOKE=false
for arg in "${@:2}"; do
  case "$arg" in
    --keep|-k) KEEP=true ;;
    --skip-smoke|-s) SKIP_SMOKE=true ;;
  esac
done

SITE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SITE_DIR/../.." && pwd)"
DEV_SCRIPT="$SITE_DIR/run-dev.sh"
PROD_SCRIPT="$SITE_DIR/run-prod-smoke.sh"

declare -A LANE=([umbraco17]='legacy'    [umbraco18]='umbraco18')
declare -A TFM=( [umbraco17]='net10.0'  [umbraco18]='net10.0')
declare -A UMB_VER=([umbraco17]='[17.4.0,18.0.0)' [umbraco18]='[18.0.0-*,19.0.0)')
declare -A TAG=([umbraco17]='articulate-local:umbraco17' [umbraco18]='articulate-local:umbraco18')
declare -A HTTPS_PORT=([umbraco17]='17017' [umbraco18]='18018')
declare -A HTTP_PORT=([umbraco17]='17080'  [umbraco18]='18080')

case "$CHOICE" in
  umbraco17|umbraco18) targets=("$CHOICE") ;;
  all) targets=(umbraco17 umbraco18) ;;
  *) echo "Usage: $0 [umbraco17|umbraco18|all] [--keep] [--skip-smoke]" >&2; exit 1 ;;
esac

ensure_packages() {
  local lane="$1"
  local major_prefix="6"
  [[ "$lane" == "umbraco18" ]] && major_prefix="7"
  local lane_dir="$REPO_ROOT/build/Release"
  shopt -s nullglob
  local pkgs=("$lane_dir"/Articulate.${major_prefix}.*.nupkg)
  local samples=("$lane_dir"/Articulate.Theme.Sample.${major_prefix}.*.nupkg)
  shopt -u nullglob
  if [[ ${#pkgs[@]} -eq 0 || ${#samples[@]} -eq 0 ]]; then
    echo "Packages missing — building lane '$lane'..."
    (cd "$REPO_ROOT" && ARTICULATE_PACKAGE_LANE="$lane" PACK_SAMPLE_THEME=true bash ./build/build.sh)
  fi
}

passed=()
failed=()

for t in "${targets[@]}"; do
  lane="${LANE[$t]}"
  host_port="localhost:${HTTPS_PORT[$t]}"
  public_url="https://$host_port/"

  echo ""
  echo "================================================================"
  echo "  $t  |  Lane: $lane  |  ${TAG[$t]}"
  echo "  HTTPS: $public_url"
  echo "================================================================"

  ensure_packages "$lane"

  export COMPOSE_PROJECT_NAME="art_$t"
  export COMPOSE_VOLUME_PREFIX="art_$t"
  export IMAGE_TAG="${TAG[$t]}"
  export PACKAGE_SOURCE="build/Release"
  export TARGET_FRAMEWORK="${TFM[$t]}"
  export UMBRACO_CMS_VERSION="${UMB_VER[$t]}"
  export CADDY_HTTPS_PORT="${HTTPS_PORT[$t]}"
  export CADDY_HTTP_PORT="${HTTP_PORT[$t]}"
  export CADDY_HTTPS_HOST="$host_port"
  export UMBRACO_PUBLIC_HOST="https://$host_port"
  export UMBRACO_PUBLIC_URL="$public_url"
  export ARTICULATE_REDIRECT_URI="${public_url}a-new/"
  export ARTICULATE_LOGOUT_REDIRECT_URI="$public_url"

  if (
    set -e
    cd "$REPO_ROOT"
    docker compose build --no-cache --pull

    export UMBRACO_RUNTIME_MODE='BackofficeDevelopment'
    if [[ "$SKIP_SMOKE" == true ]]; then
      SKIP_PUBLISH=true bash "$DEV_SCRIPT"
    else
      bash "$DEV_SCRIPT"
    fi

    if [[ "$SKIP_SMOKE" == false ]]; then
      export UMBRACO_RUNTIME_MODE='Production'
      bash "$PROD_SCRIPT"
    fi
  ); then
    passed+=("$t")
    echo "PASSED: $t => $public_url"
    if [[ "$KEEP" == false ]]; then
      (cd "$REPO_ROOT" && docker compose down -v) || true
    fi
  else
    failed+=("$t")
    echo "FAILED: $t" >&2
    (cd "$REPO_ROOT" && docker compose down -v) || true
  fi
done

if [[ "$KEEP" == true && ${#passed[@]} -gt 0 ]]; then
  echo ""
  echo "Containers kept alive for inspection:"
  for t in "${passed[@]}"; do
    echo "  $t => https://localhost:${HTTPS_PORT[$t]}/  |  https://localhost:${HTTPS_PORT[$t]}/umbraco"
  done
  echo "To clean up: docker rm -f \$(docker ps -q --filter name=art_)"
fi

[[ ${#failed[@]} -eq 0 ]]
