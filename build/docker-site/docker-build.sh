#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<EOF
Usage: $0 [tag]

Builds a Docker image from the unified Dockerfile.

Arguments:
  tag     Image tag to produce. Default: articulate:chiseled

Behavior:
  - Builds the repository's multi-stage chiseled Docker image.

Examples:
  ./build/docker-site/docker-build.sh
  ./build/docker-site/docker-build.sh articulate:chiseled

EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
  usage
  exit 0
fi

TAG=${1:-articulate:chiseled}

echo "Building Docker target: chiseled -> ${TAG}"

# Build with docker buildx if available
if docker buildx version >/dev/null 2>&1; then
  echo "Using docker buildx"
  docker buildx build --progress=plain -f Dockerfile --target chiseled -t ${TAG} .
else
  echo "Using docker build"
  docker build -f Dockerfile --target chiseled -t ${TAG} .
fi

echo "Built ${TAG} (target chiseled)"
