#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .env ] && set -a && . ./.env && set +a
export DEVSTATION_TOKEN="${DEVSTATION_TOKEN:-dev-token-change-me}"
export ConnectionStrings__Default="${DEVSTATION_CONNECTIONSTRING:-Host=localhost;Port=5432;Database=devstation;Username=devstation;Password=devstation}"
podman compose up -d postgres
dotnet run --project src/DevStation.Web --urls http://localhost:5080 &
API_PID=$!
( cd apps/web && pnpm dev ) &
WEB_PID=$!
trap 'kill $API_PID $WEB_PID 2>/dev/null; podman compose stop postgres' EXIT
wait
