#!/usr/bin/env bash
set -euo pipefail

# Build and smoke-test Articulate Docker images for Umbraco 17 and 18.
# Umbraco 16 testing uses the local test site (Articulate.Tests.Website with net9.0).
# Usage:
#   ./scripts/docker-test.sh [umbraco17|umbraco18|all] [--keep] [--skip-smoke]

CHOICE="${1:-all}"
KEEP=false
SKIP_SMOKE=false
for arg in "${@:2}"; do
  case "$arg" in
    --keep|-k) KEEP=true ;;
    --skip-smoke|-s) SKIP_SMOKE=true ;;
  esac
done

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
PACK_DIR="$REPO_ROOT/build/$CONFIGURATION"

declare -A PACKAGE_LANE=(
  [umbraco17]="legacy"
  [umbraco18]="umbraco18"
)
declare -A TFM=(
  [umbraco17]="net10.0"
  [umbraco18]="net10.0"
)
declare -A UMBRACO_VERSION=(
  [umbraco17]="[17.2.2,18.0.0)"
  [umbraco18]="18.0.0-rc*"
)
declare -A SDK_IMAGE=(
  [umbraco17]="mcr.microsoft.com/dotnet/sdk:10.0"
  [umbraco18]="mcr.microsoft.com/dotnet/sdk:10.0"
)
declare -A ASPNET_IMAGE=(
  [umbraco17]="mcr.microsoft.com/dotnet/aspnet:10.0"
  [umbraco18]="mcr.microsoft.com/dotnet/aspnet:10.0"
)
declare -A IMAGE_TAG=(
  [umbraco17]="articulate-local:umbraco17"
  [umbraco18]="articulate-local:umbraco18"
)
declare -A HTTPS_PORT=(
  [umbraco17]="17017"
  [umbraco18]="18018"
)

usage() {
  echo "Usage: $0 [umbraco17|umbraco18|all] [--keep]" >&2
}

ensure_packages() {
  local lane="$1"
  local lane_dir="$PACK_DIR/$lane"
  shopt -s nullglob
  local articulate_packages=("$lane_dir"/Articulate.[0-9]*.nupkg)
  local sample_packages=("$lane_dir"/Articulate.Theme.Sample.*.nupkg)
  shopt -u nullglob

  if [[ ${#articulate_packages[@]} -gt 0 && ${#sample_packages[@]} -gt 0 ]]; then
    return 0
  fi

  echo "Required Docker packages missing under $lane_dir. Building lane '$lane'..."
  (cd "$REPO_ROOT" && ARTICULATE_PACKAGE_LANE="$lane" PACK_SAMPLE_THEME=true bash ./build/build.sh)
}

cleanup_target() {
  local target="$1"
  if [[ "$KEEP" == false ]]; then
    docker rm -f "art-caddy-$target" >/dev/null 2>&1 || true
    docker rm -f "art-$target" >/dev/null 2>&1 || true
    docker network rm "art-net-$target" >/dev/null 2>&1 || true
  fi
}

build_and_test() {
  local target="$1"
  local lane="${PACKAGE_LANE[$target]}"
  local tfm="${TARGET_FRAMEWORK:-${TFM[$target]}}"
  local umb_ver="${UMBRACO_CMS_VERSION:-${UMBRACO_VERSION[$target]}}"
  local sdk_image="${DOTNET_SDK_IMAGE:-${SDK_IMAGE[$target]}}"
  local aspnet_image="${DOTNET_ASPNET_IMAGE:-${ASPNET_IMAGE[$target]}}"
  local img_tag="${IMAGE_TAG[$target]}"
  local port="${HTTPS_PORT[$target]}"
  local docker_pkg_src="build/$CONFIGURATION/$lane"
  local caddyfile="$REPO_ROOT/build/docker-site/Caddyfile"

  echo
  echo "==========================================================="
  echo "  Target: $target  |  Lane: $lane  |  TFM: $tfm"
  echo "  Umbraco: $umb_ver"
  echo "  Image: $img_tag"
  echo "  HTTPS smoke test: https://localhost:$port/"
  echo "==========================================================="
  echo

  docker build --no-cache --pull \
    --build-arg "DOTNET_SDK_IMAGE=$sdk_image" \
    --build-arg "DOTNET_ASPNET_IMAGE=$aspnet_image" \
    --build-arg "TARGET_FRAMEWORK=$tfm" \
    --build-arg "UMBRACO_CMS_VERSION=$umb_ver" \
    --build-arg "PACKAGE_SOURCE=$docker_pkg_src" \
    --build-arg "PACKAGE_LANE=$lane" \
    -t "$img_tag" \
    "$REPO_ROOT"

  local container_name="art-$target"
  local caddy_name="art-caddy-$target"
  local network_name="art-net-$target"

  docker network create "$network_name" >/dev/null 2>&1 || true

  # Base environment variables for both phases
  local base_env=(
    '-e' "ASPNETCORE_ENVIRONMENT=Container"
    '-e' "ASPNETCORE_URLS=http://+:8080"
    '-e' "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True"
    '-e' "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite"
    '-e' "Umbraco__CMS__Global__UseHttps=true"
    '-e' "Umbraco__CMS__WebRouting__UmbracoApplicationUrl=https://localhost:$port/"
    '-e' "Umbraco__CMS__Security__BackOfficeHost=https://localhost:$port"
    '-e' "Umbraco__CMS__Unattended__InstallUnattended=true"
    '-e' "Umbraco__CMS__Unattended__UpgradeUnattended=true"
    '-e' "Umbraco__CMS__Unattended__PackageMigrationsUnattended=true"
    '-e' "Umbraco__CMS__Unattended__UnattendedUserName=${UMBRACO_USER_NAME:-Smoke Test}"
    '-e' "Umbraco__CMS__Unattended__UnattendedUserEmail=${UMBRACO_USER_EMAIL:-smoke@$target.localhost}"
    '-e' "Umbraco__CMS__Unattended__UnattendedUserPassword=${UMBRACO_USER_PASSWORD:-@rticulate!}"
    '-e' "Umbraco__CMS__ModelsBuilder__ModelsMode=Nothing"
  )

  echo "Phase 1: BackofficeDevelopment (install, publish, confirm)"
  echo "────────────────────────────────────────────────────────────"
  docker run -d --rm --name "$container_name" --network "$network_name" "${base_env[@]}" "$img_tag" >/dev/null

  docker run -d --rm --name "$caddy_name" --network "$network_name" \
    -p "$port:18443" \
    -v "$caddyfile:/etc/caddy/Caddyfile:ro" \
    -e "ARTICULATE_HOST=$container_name" \
    -e "HTTPS_HOST=localhost:$port" \
    caddy:2-alpine \
    caddy run --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null

  local umbraco_ok=false
  local i=0

  while [[ $i -lt 45 ]]; do
    if curl -kfsS "https://localhost:$port/umbraco" >/dev/null 2>&1; then
      umbraco_ok=true
      echo "  /umbraco => HTTP 200 OK"
      break
    fi
    i=$((i + 1))
    sleep 2
  done

  if [[ "$umbraco_ok" == false ]]; then
    echo "  FAILED: /umbraco not ready after 90s" >&2
    echo "--- Umbraco logs (last 60 lines) ---" >&2
    docker logs "$container_name" 2>&1 | tail -n 60 >&2 || true
    cleanup_target "$target"
    return 2
  fi

  if [[ "$SKIP_SMOKE" == true ]]; then
    echo "  smoke.mjs tests SKIPPED (--skip-smoke flag)"
  elif [[ -n "${ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET:-}" ]]; then
    export UMBRACO_PUBLIC_URL="https://localhost:$port/"
    export UMBRACO_RUNTIME_MODE='BackofficeDevelopment'

    echo "  Running smoke.mjs publish (publish Articulate content)..."
    if ! cd "$REPO_ROOT" && node build/docker-site/smoke.mjs publish; then
      echo "  smoke.mjs publish FAILED" >&2
      cleanup_target "$target"
      return 2
    fi
    echo "  smoke.mjs publish OK"

    echo "  Running smoke.mjs confirm (verify published)..."
    if ! cd "$REPO_ROOT" && node build/docker-site/smoke.mjs confirm; then
      echo "  smoke.mjs confirm FAILED" >&2
      cleanup_target "$target"
      return 2
    fi
    echo "  smoke.mjs confirm OK"
  else
    echo "  smoke.mjs tests SKIPPED (ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET not set)"
  fi

  echo ""
  echo "Phase 2: Production (recreate, smoke test, theme API)"
  echo "────────────────────────────────────────────────────────────"
  docker rm -f "$container_name" >/dev/null 2>&1 || true
  sleep 2

  # Phase 2 env: add Production mode flag
  local phase2_env=("${base_env[@]}" '-e' 'Umbraco__CMS__Runtime__Mode=Production')
  docker run -d --rm --name "$container_name" --network "$network_name" "${phase2_env[@]}" "$img_tag" >/dev/null
  sleep 5

  if [[ "$SKIP_SMOKE" == true ]]; then
    echo "  Phase 2 smoke tests SKIPPED (--skip-smoke flag)"
  elif [[ -n "${ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET:-}" ]]; then
    export UMBRACO_RUNTIME_MODE='Production'

    echo "  Running smoke.mjs smoke (verify / returns 200)..."
    if ! cd "$REPO_ROOT" && node build/docker-site/smoke.mjs smoke; then
      echo "  smoke.mjs smoke FAILED" >&2
      cleanup_target "$target"
      return 2
    fi
    echo "  smoke.mjs smoke OK"

    echo "  Running smoke.mjs theme (verify theme API)..."
    if ! cd "$REPO_ROOT" && node build/docker-site/smoke.mjs theme; then
      echo "  smoke.mjs theme FAILED" >&2
      cleanup_target "$target"
      return 2
    fi
    echo "  smoke.mjs theme OK"
  fi

  echo ""
  if [[ "$KEEP" == true ]]; then
    echo "  PASSED: $target ($img_tag) => https://localhost:$port/"
  else
    echo "  PASSED: $target ($img_tag)"
  fi
  cleanup_target "$target"
  return 0
}

case "$CHOICE" in
  umbraco17|umbraco18)
    targets=("$CHOICE")
    ;;
  all)
    targets=(umbraco17 umbraco18)
    ;;
  *)
    usage
    exit 1
    ;;
esac

lanes=()
for target in "${targets[@]}"; do
  lane="${PACKAGE_LANE[$target]}"
  if [[ ! " ${lanes[*]} " =~ " $lane " ]]; then
    lanes+=("$lane")
  fi
done

for lane in "${lanes[@]}"; do
  ensure_packages "$lane"
done

exit_code=0
for target in "${targets[@]}"; do
  if build_and_test "$target"; then
    :
  else
    result=$?
    if [[ $exit_code -eq 0 ]]; then
      exit_code=$result
    fi
  fi
done

if [[ "$KEEP" == true && $exit_code -eq 0 ]]; then
  echo
  echo "Containers kept alive for manual inspection:"
  docker ps --filter name=art-
  for target in "${targets[@]}"; do
    echo "  $target => https://localhost:${HTTPS_PORT[$target]}/  |  https://localhost:${HTTPS_PORT[$target]}/umbraco"
  done
  echo "To clean up: docker rm -f \$(docker ps -q --filter name=art-)"
fi

exit "$exit_code"
