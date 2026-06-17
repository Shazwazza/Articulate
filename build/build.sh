#!/usr/bin/env bash
# Usage:
#   BUILD_CONFIGURATION=Debug ./build/build.sh
#   ENABLE_CLIENT_BUILD=true ./build/build.sh
#   RUN_TESTS=true ./build/build.sh
#   PACK_SAMPLE_THEME=true ./build/build.sh
#   ARTICULATE_PACKAGE_LANE=v18 ./build/build.sh
# Set SKIP_CLEAN=true only when deliberately reusing outputs from the same lane.
# Release builds enable the client build by default so packaged assets carry the stamped version.

set -euo pipefail

# Detect WSL and ensure dotnet is on PATH if installed under HOME
IS_WSL=0
if [[ -n "${WSL_DISTRO_NAME:-}" ]] || grep -qi microsoft /proc/version 2>/dev/null; then
  IS_WSL=1
fi
if ! command -v dotnet >/dev/null 2>&1; then
  if [[ -d "$HOME/.dotnet" ]]; then
    export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
  fi
fi

# CI/WSL perf-friendly defaults
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export NUGET_XMLDOC_MODE=none

START_TIME=$(date +%s.%N)
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
BUILD_FOLDER="$REPO_ROOT/build"
V18_VERSION_FILE="$BUILD_FOLDER/v18-version.txt"
CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
PACKAGE_LANE="${ARTICULATE_PACKAGE_LANE:-v17}"
case "$PACKAGE_LANE" in
  v17|v18) ;;
  *)
    echo "Unsupported ARTICULATE_PACKAGE_LANE '$PACKAGE_LANE'. Expected 'v17' or 'v18'." >&2
    exit 1
    ;;
esac
RELEASE_ROOT="$BUILD_FOLDER/$CONFIGURATION"
RELEASE_FOLDER="$RELEASE_ROOT/$PACKAGE_LANE"
SOLUTION_ROOT="$REPO_ROOT/src"
SOLUTION_PATH="$SOLUTION_ROOT/Articulate.sln"
BACKOFFICE_OUTPUT="$SOLUTION_ROOT/Articulate.Web/wwwroot/App_Plugins/Articulate/BackOffice"

# Compute CPU parallelism for MSBuild (allow override via MAXCPU)
CPU_COUNT=${MAXCPU:-}
if [[ -z "$CPU_COUNT" ]]; then
  CPU_COUNT=$( (command -v nproc >/dev/null 2>&1 && nproc --all) || getconf _NPROCESSORS_ONLN || echo 8 )
fi
RESTORE_ARGS=(-m -maxcpucount:"$CPU_COUNT" -p:RestoreUseStaticGraphEvaluation=true)
BUILD_ARGS=(-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false)
DOTNET_COMMON=(--nologo -v minimal)

# Handle ENABLE_CLIENT_BUILD environment variable (default to true for Release/CI, false otherwise)
if [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" || "$CONFIGURATION" == "Release" ]]; then
  CLIENT_BUILD_DEFAULT=true
else
  CLIENT_BUILD_DEFAULT=false
fi
CLIENT_BUILD_VALUE=${ENABLE_CLIENT_BUILD:-$CLIENT_BUILD_DEFAULT}
CLIENT_BUILD_PROPERTY="-p:EnableClientBuild=$CLIENT_BUILD_VALUE"

if [[ "$PACKAGE_LANE" == "v18" ]]; then
  RESOLVED_CLIENT_VERSION="18"
else
  RESOLVED_CLIENT_VERSION="17"
fi
PACKAGE_VERSION="${ARTICULATE_PACKAGE_VERSION:-}"
if [[ "$PACKAGE_LANE" == "v18" && -z "$PACKAGE_VERSION" ]]; then
  V18_BASE_VERSION="$(tr -d '[:space:]' < "$V18_VERSION_FILE")"
  if [[ ! "$V18_BASE_VERSION" =~ ^7\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "Invalid v18 base version '$V18_BASE_VERSION' in $V18_VERSION_FILE." >&2
    exit 1
  fi
  if ! command -v nbgv >/dev/null 2>&1; then
    echo "The v18 lane requires ARTICULATE_PACKAGE_VERSION or the nbgv CLI." >&2
    exit 1
  fi
  V17_VERSION="$(nbgv get-version -v SemVer2)"
  case "$V17_VERSION" in
    6.1.*)
      V17_PATCH_AND_SUFFIX="${V17_VERSION#6.1.}"
      V17_SUFFIX=""
      if [[ "$V17_PATCH_AND_SUFFIX" == *-* ]]; then
        V17_SUFFIX="${V17_PATCH_AND_SUFFIX#*-}"
      fi
      PACKAGE_VERSION="$V18_BASE_VERSION"
      if [[ -n "$V17_SUFFIX" ]]; then
        PACKAGE_VERSION="${PACKAGE_VERSION}.${V17_SUFFIX}"
      fi
      ;;
    *)
      echo "Expected NBGV to produce a 6.1.x version, got '$V17_VERSION'." >&2
      exit 1
      ;;
  esac
fi
LANE_PROPERTIES=(
  "-p:ArticulatePackageLane=$PACKAGE_LANE"
  "-p:UmbracoClientVersion=$RESOLVED_CLIENT_VERSION"
)
if [[ -n "$PACKAGE_VERSION" ]]; then
  LANE_PROPERTIES+=("-p:ArticulatePackageVersion=$PACKAGE_VERSION")
fi
PACK_SAMPLE_THEME_VALUE=${PACK_SAMPLE_THEME:-}
if [[ -n "${RUN_TESTS:-}" ]]; then
  RUN_TESTS_VALUE="$RUN_TESTS"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  RUN_TESTS_VALUE=true
else
  RUN_TESTS_VALUE=false
fi

echo "Restore parallelism: up to $CPU_COUNT MSBuild nodes"
echo "Build configuration: $CONFIGURATION"
echo "Package lane: $PACKAGE_LANE"
echo "Package version: ${PACKAGE_VERSION:-NBGV}"
echo "Package output: $RELEASE_FOLDER"

# Advise when running in WSL against Windows-mounted drives (slow)
if [[ $IS_WSL -eq 1 && "$REPO_ROOT" == /mnt/* ]]; then
  echo "WSL performance tip: You're building from '$REPO_ROOT' (a Windows-mounted path)." >&2
  echo "For much faster I/O, move the repo into your WSL distro (e.g. ~/src/Articulate6-wip) and build there." >&2
fi

# Remove only packages from the same major version line to avoid stale artifacts without
# wiping the other lane's output when both are built locally.
MAJOR_VERSION_PREFIX="6"
if [[ "$PACKAGE_LANE" == "v18" ]]; then
  MAJOR_VERSION_PREFIX="7"
fi
mkdir -p "$RELEASE_FOLDER"
find "$RELEASE_FOLDER" -maxdepth 1 -type f \( \
  -name "Articulate.${MAJOR_VERSION_PREFIX}.*.nupkg" -o \
  -name "Articulate.${MAJOR_VERSION_PREFIX}.*.snupkg" -o \
  -name "Articulate.Theme.Sample.${MAJOR_VERSION_PREFIX}.*.nupkg" \
\) -delete 2>/dev/null || true

dotnet --version

# Match the repository-wide NuGet fallback-folder setting.
export RestoreFallbackFolders=

# --- 1) Clean the solution so Release/CI builds start fresh ---
echo "1. Cleaning solution outputs..."
if [[ "${SKIP_CLEAN:-false}" != "true" ]]; then
  dotnet build-server shutdown >/dev/null 2>&1 || true
  find "$SOLUTION_ROOT" -type d \( -name bin -o -name obj \) ! -path '*/node_modules/*' -prune -exec rm -rf {} + 2>/dev/null || true
  rm -rf "$BUILD_FOLDER/ClientAssets"
  rm -rf "$BACKOFFICE_OUTPUT"
else
  echo "Skipping clean because SKIP_CLEAN=true"
fi

# --- 2) Solution-level restore ---
echo "2. Restoring solution packages in parallel..."
if ! dotnet restore "$SOLUTION_PATH" "${DOTNET_COMMON[@]}" "${RESTORE_ARGS[@]}" "$CLIENT_BUILD_PROPERTY" "${LANE_PROPERTIES[@]}"; then
  echo "dotnet restore failed" >&2
  exit 1
fi

# --- 3) Build sequentially because the solution references shared projects through multiple paths. ---
echo "3. Building solution for net10.0"
if ! dotnet build "$SOLUTION_PATH" -c "$CONFIGURATION" --no-restore "${DOTNET_COMMON[@]}" "${BUILD_ARGS[@]}" "$CLIENT_BUILD_PROPERTY" "${LANE_PROPERTIES[@]}"; then
  echo "dotnet build failed" >&2
  exit 1
fi

# --- 4) Run tests ---
if [[ "$RUN_TESTS_VALUE" == "true" ]]; then
  echo "4. Running tests..."
  if ! dotnet test "$SOLUTION_PATH" -c "$CONFIGURATION" --no-restore --no-build "${DOTNET_COMMON[@]}" "${BUILD_ARGS[@]}" "${LANE_PROPERTIES[@]}"; then
    echo "dotnet test failed" >&2
    exit 1
  fi
else
  echo "4. Skipping tests (set RUN_TESTS=true to enable locally)"
fi

# --- 5) Pack primary projects ---
echo "5. Packing projects..."
ARTICULATE_WEB_PROJECT="$SOLUTION_ROOT/Articulate.Web/Articulate.Web.csproj"
ARTICULATE_THEME_SAMPLE_PROJECT="$SOLUTION_ROOT/Articulate.Theme.Sample/Articulate.Theme.Sample.csproj"

PACK_PROJECTS=(
  "$ARTICULATE_WEB_PROJECT"
)
if [[ "$PACK_SAMPLE_THEME_VALUE" == "true" || ( -z "$PACK_SAMPLE_THEME_VALUE" && "${CI:-}" != "true" && "${GITHUB_ACTIONS:-}" != "true" ) ]]; then
  PACK_PROJECTS+=("$ARTICULATE_THEME_SAMPLE_PROJECT")
fi

for proj in "${PACK_PROJECTS[@]}"; do
  echo "[pack] -> $(basename "$proj")"
  if ! dotnet pack -c "$CONFIGURATION" "$proj" --no-build --no-restore -o "$RELEASE_FOLDER" \
    "${DOTNET_COMMON[@]}" -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false "$CLIENT_BUILD_PROPERTY" "${LANE_PROPERTIES[@]}"; then
    echo "dotnet pack failed for $proj" >&2
    exit 1
  fi
done

END_TIME=$(date +%s.%N)
ELAPSED=$(awk -v start="$START_TIME" -v end="$END_TIME" 'BEGIN {printf "%.1f", end - start}')
echo "Build pipeline completed in ${ELAPSED}s. Packages available at $RELEASE_FOLDER"
