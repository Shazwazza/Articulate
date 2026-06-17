#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Check the current Articulate BackOffice client for one Umbraco version at a time.

Usage:
  ./build/check-backoffice-client.sh --version 17
  ./build/check-backoffice-client.sh --version 18
EOF
}

UMBRACO_VERSION=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -v|--version)
      UMBRACO_VERSION="${2:-}"
      shift 2
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
  17|18)
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
CLIENT_DIR="$REPO_ROOT/src/Articulate.Web/Client"
CLIENT_WORKSPACE="$CLIENT_DIR/v$UMBRACO_VERSION"

if ! command -v pnpm >/dev/null 2>&1; then
  echo "Required command not found on PATH: pnpm" >&2
  exit 1
fi

echo "Checking Articulate BackOffice client for Umbraco $UMBRACO_VERSION"
echo "  Client   : $CLIENT_DIR"

cd "$CLIENT_DIR"
pnpm install
cd "$CLIENT_WORKSPACE"
pnpm run check
pnpm run build
