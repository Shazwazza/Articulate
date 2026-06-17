#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Run Articulate.Tests.Website against one Umbraco version at a time.

Usage:
  ./build/run-umbraco-test-site.sh --version 17 [--configuration Debug]
  ./build/run-umbraco-test-site.sh --version 18 [--configuration Release]
  ./build/run-umbraco-test-site.sh --version 18 --reset-site-data
EOF
}

UMBRACO_VERSION=""
CONFIGURATION="${BUILD_CONFIGURATION:-Debug}"
RESET_SITE_DATA=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v|--version)
      UMBRACO_VERSION="${2:-}"
      shift 2
      ;;
    -c|--configuration)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    --reset-site-data)
      RESET_SITE_DATA=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

case "$UMBRACO_VERSION" in
  17)
    PACKAGE_VERSION="17.4.0"
    ;;
  18)
    PACKAGE_VERSION="18.0.0-rc2"
    ;;
  "")
    echo "Missing required --version argument." >&2
    usage >&2
    exit 1
    ;;
  *)
    echo "Unsupported Umbraco version '$UMBRACO_VERSION'. Expected 17 or 18." >&2
    exit 1
    ;;
esac

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/src/Articulate.Tests.Website/Articulate.Tests.Website.csproj"
SITE_DATA_DIR="$REPO_ROOT/src/Articulate.Tests.Website/umbraco"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Required command not found on PATH: dotnet" >&2
  exit 1
fi

echo "Running Articulate.Tests.Website for Umbraco $UMBRACO_VERSION"
echo "  Package   : $PACKAGE_VERSION"
echo "  Config    : $CONFIGURATION"

if [[ "$RESET_SITE_DATA" == "true" ]]; then
  echo "  Reset data: $SITE_DATA_DIR"
  rm -rf "$SITE_DATA_DIR"
fi

dotnet run -c "$CONFIGURATION" --project "$PROJECT_PATH" "-p:ArticulatePackageLane=v$UMBRACO_VERSION" "-p:UmbracoCmsPackageVersion=$PACKAGE_VERSION"
