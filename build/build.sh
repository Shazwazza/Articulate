#!/usr/bin/env bash
# Usage:
#   BUILD_CONFIGURATION=Debug ./build/build.sh
#   ENABLE_CLIENT_BUILD=true ./build/build.sh
#   RUN_TESTS=true ./build/build.sh
#   PACK_SAMPLE_THEME=true ./build/build.sh
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
CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
RELEASE_FOLDER="$BUILD_FOLDER/$CONFIGURATION"
SOLUTION_ROOT="$REPO_ROOT/src"
SOLUTION_PATH="$SOLUTION_ROOT/Articulate.sln"
TARGET_FRAMEWORKS=("net9.0" "net10.0")

# Compute CPU parallelism for MSBuild (allow override via MAXCPU)
CPU_COUNT=${MAXCPU:-}
if [[ -z "$CPU_COUNT" ]]; then
  CPU_COUNT=$( (command -v nproc >/dev/null 2>&1 && nproc --all) || getconf _NPROCESSORS_ONLN || echo 8 )
fi
MSBUILD_PARALLEL=(-m -maxcpucount:"$CPU_COUNT" -p:BuildInParallel=true -p:RestoreUseStaticGraphEvaluation=true)
DOTNET_COMMON=(--nologo -v minimal)

# Handle ENABLE_CLIENT_BUILD environment variable (default to true for Release/CI, false otherwise)
if [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" || "$CONFIGURATION" == "Release" ]]; then
  CLIENT_BUILD_DEFAULT=true
else
  CLIENT_BUILD_DEFAULT=false
fi
CLIENT_BUILD_VALUE=${ENABLE_CLIENT_BUILD:-$CLIENT_BUILD_DEFAULT}
CLIENT_BUILD_PROPERTY="-p:EnableClientBuild=$CLIENT_BUILD_VALUE"
PACK_SAMPLE_THEME_VALUE=${PACK_SAMPLE_THEME:-}
if [[ -n "${RUN_TESTS:-}" ]]; then
  RUN_TESTS_VALUE="$RUN_TESTS"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  RUN_TESTS_VALUE=true
else
  RUN_TESTS_VALUE=false
fi

echo "Using up to $CPU_COUNT parallel MSBuild nodes"
echo "Build configuration: $CONFIGURATION"

# Advise when running in WSL against Windows-mounted drives (slow)
if [[ $IS_WSL -eq 1 && "$REPO_ROOT" == /mnt/* ]]; then
  echo "WSL performance tip: You're building from '$REPO_ROOT' (a Windows-mounted path)." >&2
  echo "For much faster I/O, move the repo into your WSL distro (e.g. ~/src/Articulate6-wip) and build there." >&2
fi

if [[ -d "$RELEASE_FOLDER" ]]; then
  echo "Warning: $RELEASE_FOLDER already exists and will be deleted."
  rm -rf "$RELEASE_FOLDER"
fi

dotnet --version

# Avoid NuGet fallback folders (already disabled in Directory.Build.props, but double-sure)
export RestoreFallbackFolders=

# --- 1) Clean the solution so Release/CI builds start fresh ---
echo "1. Cleaning solution outputs..."
if [[ "${FORCE_CLEAN:-false}" == "true" ]]; then
  if ! dotnet clean "$SOLUTION_PATH" -c "$CONFIGURATION" "${DOTNET_COMMON[@]}" "$CLIENT_BUILD_PROPERTY"; then
    echo "Warning: dotnet clean failed" >&2
  fi
else
  echo "Skipping dotnet clean (FORCE_CLEAN not set). Set FORCE_CLEAN=true to force a clean."
fi

# --- 2) Solution-level restore ---
mkdir -p "$RELEASE_FOLDER"
echo "2. Restoring solution packages in parallel..."
if ! dotnet restore "$SOLUTION_PATH" "${DOTNET_COMMON[@]}" "${MSBUILD_PARALLEL[@]}" "$CLIENT_BUILD_PROPERTY"; then
  echo "dotnet restore failed" >&2
  exit 1
fi

# --- 3) Build TFMs sequentially (net9 first, then net10) to keep client build ordering deterministic ---
echo "3. Building solution for: ${TARGET_FRAMEWORKS[*]}"

for tfm in "${TARGET_FRAMEWORKS[@]}"; do
  echo "[build] -> $tfm"
  t0=$(date +%s)
  if ! dotnet build "$SOLUTION_PATH" -c "$CONFIGURATION" -f "$tfm" --no-restore "${DOTNET_COMMON[@]}" "${MSBUILD_PARALLEL[@]}" "$CLIENT_BUILD_PROPERTY"; then
    echo "dotnet build failed for $tfm" >&2
    exit 1
  fi
  t1=$(date +%s)
  echo "[build] <- $tfm done in $((t1 - t0))s"
done

# --- 4) Run tests ---
if [[ "$RUN_TESTS_VALUE" == "true" ]]; then
  echo "4. Running tests..."
  if ! dotnet test "$SOLUTION_PATH" -c "$CONFIGURATION" --no-restore --no-build "${DOTNET_COMMON[@]}"; then
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
  RESTORE_SWITCHES=(--no-restore)
  if ! dotnet pack -c "$CONFIGURATION" "$proj" "${RESTORE_SWITCHES[@]}" -o "$RELEASE_FOLDER" \
    "${DOTNET_COMMON[@]}" -p:BuildInParallel=false "$CLIENT_BUILD_PROPERTY"; then
    echo "dotnet pack failed for $proj" >&2
    exit 1
  fi
done

END_TIME=$(date +%s.%N)
ELAPSED=$(awk -v start="$START_TIME" -v end="$END_TIME" 'BEGIN {printf "%.1f", end - start}')
echo "Build pipeline completed in ${ELAPSED}s. Packages available at $RELEASE_FOLDER"
