#!/usr/bin/env bash
set -euo pipefail

LANE="${1:-${ARTICULATE_PACKAGE_LANE:-legacy}}"
ACTION="${2:-up}"
CONFIGURATION="${BUILD_CONFIGURATION:-Release}"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

case "$LANE" in
  legacy)
    UMBRACO_VERSION="[17.2.2,18.0.0)"
    IMAGE_TAG="articulate-local:umbraco17"
    CONTAINER_NAME="articulate-umbraco17"
    PORT=18017
    EMAIL="admin17@localhost"
    ;;
  umbraco18)
    UMBRACO_VERSION="18.0.0-*"
    IMAGE_TAG="articulate-local:umbraco18"
    CONTAINER_NAME="articulate-umbraco18"
    PORT=18018
    EMAIL="admin18@localhost"
    ;;
  *)
    echo "Unsupported lane '$LANE'. Expected 'legacy' or 'umbraco18'." >&2
    exit 1
    ;;
esac

case "$ACTION" in
  build|run|up|compose-build|compose-up|compose-down) ;;
  *)
    echo "Unsupported action '$ACTION'. Expected 'build', 'run', 'up', 'compose-build', 'compose-up', or 'compose-down'." >&2
    exit 1
    ;;
esac

PACKAGE_SOURCE="build/$CONFIGURATION/$LANE"
if [[ "$ACTION" != "compose-down" ]]; then
  if [[ ! -d "$REPO_ROOT/$PACKAGE_SOURCE" ]]; then
    echo "Package lane folder '$REPO_ROOT/$PACKAGE_SOURCE' does not exist. Run build/build.sh with ARTICULATE_PACKAGE_LANE=$LANE first." >&2
    exit 1
  fi
  shopt -s nullglob
  ARTICULATE_PACKAGES=("$REPO_ROOT/$PACKAGE_SOURCE"/Articulate.[0-9]*.nupkg)
  THEME_PACKAGES=("$REPO_ROOT/$PACKAGE_SOURCE"/Articulate.Theme.Sample.*.nupkg)
  if [[ ${#ARTICULATE_PACKAGES[@]} -eq 0 ]]; then
    echo "Package lane folder '$REPO_ROOT/$PACKAGE_SOURCE' does not contain an Articulate .nupkg." >&2
    exit 1
  fi
  if [[ ${#THEME_PACKAGES[@]} -eq 0 ]]; then
    echo "Package lane folder '$REPO_ROOT/$PACKAGE_SOURCE' does not contain Articulate.Theme.Sample. Rebuild with PACK_SAMPLE_THEME=true." >&2
    exit 1
  fi
fi

build_image() {
  echo "Building $IMAGE_TAG from $PACKAGE_SOURCE with Umbraco $UMBRACO_VERSION"
  docker build \
    --build-arg "UMBRACO_CMS_VERSION=$UMBRACO_VERSION" \
    --build-arg "BUILD_CONFIGURATION=$CONFIGURATION" \
    --build-arg "PACKAGE_SOURCE=$PACKAGE_SOURCE" \
    -t "$IMAGE_TAG" \
    "$REPO_ROOT"
}

run_container() {
  echo "Starting $CONTAINER_NAME on http://localhost:$PORT/umbraco"
  echo "Direct HTTP mode is a boot/package smoke test. Use 'compose-up' for backoffice auth over HTTPS."
  docker rm -f "$CONTAINER_NAME" >/dev/null 2>&1 || true
  docker run -d \
    --name "$CONTAINER_NAME" \
    -p "$PORT:8080" \
    -e "ASPNETCORE_ENVIRONMENT=Container" \
    -e "ASPNETCORE_URLS=http://+:8080" \
    -e "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True" \
    -e "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite" \
    -e "Umbraco__CMS__WebRouting__UmbracoApplicationUrl=http://localhost:$PORT/" \
    -e "Umbraco__CMS__Security__BackOfficeHost=http://localhost:$PORT" \
    -e "Umbraco__CMS__Unattended__InstallUnattended=true" \
    -e "Umbraco__CMS__Unattended__UpgradeUnattended=true" \
    -e "Umbraco__CMS__Unattended__UnattendedUserName=Jane Doe" \
    -e "Umbraco__CMS__Unattended__UnattendedUserEmail=$EMAIL" \
    -e "Umbraco__CMS__Unattended__UnattendedUserPassword=@rticulate" \
    -e "Umbraco__CMS__ModelsBuilder__ModelsMode=Nothing" \
    "$IMAGE_TAG"
}

compose_command() {
  local compose_action="$1"
  local project_name="articulate-$LANE"
  local volume_prefix="articulate_$LANE"

  case "$compose_action" in
    build)
      echo "Building Compose HTTPS lane $LANE from $PACKAGE_SOURCE"
      BUILD_CONFIGURATION="$CONFIGURATION" \
      PACKAGE_SOURCE="$PACKAGE_SOURCE" \
      UMBRACO_CMS_VERSION="$UMBRACO_VERSION" \
      IMAGE_TAG="$IMAGE_TAG" \
      UMBRACO_USER_EMAIL="$EMAIL" \
      COMPOSE_VOLUME_PREFIX="$volume_prefix" \
      UMBRACO_PUBLIC_HOST="https://localhost:18443" \
      UMBRACO_PUBLIC_URL="https://localhost:18443/" \
      ARTICULATE_REDIRECT_URI="https://localhost:18443/a-new/" \
      ARTICULATE_LOGOUT_REDIRECT_URI="https://localhost:18443/" \
      docker compose -p "$project_name" build articulate
      ;;
    up)
      echo "Starting Compose HTTPS lane $LANE at https://localhost:18443/umbraco"
      echo "Only one Compose HTTPS lane can bind port 18443 at a time."
      BUILD_CONFIGURATION="$CONFIGURATION" \
      PACKAGE_SOURCE="$PACKAGE_SOURCE" \
      UMBRACO_CMS_VERSION="$UMBRACO_VERSION" \
      IMAGE_TAG="$IMAGE_TAG" \
      UMBRACO_USER_EMAIL="$EMAIL" \
      COMPOSE_VOLUME_PREFIX="$volume_prefix" \
      UMBRACO_PUBLIC_HOST="https://localhost:18443" \
      UMBRACO_PUBLIC_URL="https://localhost:18443/" \
      ARTICULATE_REDIRECT_URI="https://localhost:18443/a-new/" \
      ARTICULATE_LOGOUT_REDIRECT_URI="https://localhost:18443/" \
      docker compose -p "$project_name" up -d --build --force-recreate
      ;;
    down)
      echo "Stopping Compose HTTPS lane $LANE"
      COMPOSE_VOLUME_PREFIX="$volume_prefix" docker compose -p "$project_name" down
      ;;
  esac
}

case "$ACTION" in
  build) build_image ;;
  run) run_container ;;
  up)
    build_image
    run_container
    ;;
  compose-build) compose_command build ;;
  compose-up) compose_command up ;;
  compose-down) compose_command down ;;
esac
