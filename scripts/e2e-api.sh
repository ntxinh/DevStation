#!/usr/bin/env bash
# Boots the API for Playwright e2e runs.
# Ensures a throwaway Postgres (container "devstation-e2e-pg", host port 55432 —
# chosen to avoid clashing with anything already on 5432), then runs the API on
# :5080 with migrations applied on startup. Set DEVSTATION_E2E_DB to skip the
# container and use an existing Postgres (e.g. a CI service container).
set -euo pipefail
cd "$(dirname "$0")/.."

if [ -z "${DEVSTATION_E2E_DB:-}" ]; then
  RT="$(command -v podman || command -v docker)"
  if "$RT" ps --format '{{.Names}}' | grep -qx devstation-e2e-pg; then
    echo "e2e postgres already running"
  elif "$RT" container exists devstation-e2e-pg 2>/dev/null; then
    "$RT" start devstation-e2e-pg >/dev/null
  else
    "$RT" run -d --name devstation-e2e-pg \
      -e POSTGRES_USER=devstation -e POSTGRES_PASSWORD=devstation \
      -e POSTGRES_DB=devstation -p 127.0.0.1:55432:5432 \
      docker.io/library/postgres:16-alpine >/dev/null
  fi
  for _ in $(seq 1 30); do
    "$RT" exec devstation-e2e-pg pg_isready -U devstation -d devstation >/dev/null 2>&1 && break
    sleep 1
  done
fi

export DEVSTATION_TOKEN="${DEVSTATION_TOKEN:-dev-token-change-me}"
export ConnectionStrings__Default="${DEVSTATION_E2E_DB:-Host=localhost;Port=55432;Database=devstation;Username=devstation;Password=devstation}"
exec dotnet run --project src/DevStation.Web --urls http://localhost:5080
