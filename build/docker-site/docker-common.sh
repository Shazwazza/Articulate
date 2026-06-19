#!/usr/bin/env bash

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Required command not found on PATH: $1" >&2
    exit 127
  }
}

resolve_node() {
  if [[ -n "${NODE_BIN:-}" ]]; then
    return 0
  fi

  if command -v node >/dev/null 2>&1; then
    NODE_BIN=node
    return 0
  fi

  if command -v mise >/dev/null 2>&1; then
    NODE_BIN="$(mise which node 2>/dev/null || true)"
    if [[ -n "$NODE_BIN" && -x "$NODE_BIN" ]]; then
      return 0
    fi
  fi

  echo "Required command not found on PATH: node. Set NODE_BIN to a POSIX-shell-visible Node.js executable if your shell cannot find node." >&2
  exit 127
}
