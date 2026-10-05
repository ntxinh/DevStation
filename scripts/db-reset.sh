#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
podman compose down -v postgres
podman compose up -d postgres
