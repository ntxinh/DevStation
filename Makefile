.PHONY: setup dev build test test-unit test-integration test-e2e lint format migrate db-reset docker-up docker-down
setup: ; pnpm --dir apps/web install && dotnet restore src/DevStation.sln
dev: ; ./scripts/dev.sh
build: ; dotnet build src/DevStation.sln && pnpm --dir apps/web build
test: test-unit test-integration ; pnpm --dir apps/web test -- --run
test-unit: ; dotnet test tests/unit/DevStation.Tests.Unit -v minimal
test-integration: ; DOCKER_HOST=unix://$$XDG_RUNTIME_DIR/podman/podman.sock dotnet test tests/integration -v minimal
test-e2e: ; pnpm --dir apps/web exec playwright test
lint: ; dotnet format src/DevStation.sln --verify-no-changes ; pnpm --dir apps/web lint
format: ; dotnet format src/DevStation.sln ; pnpm --dir apps/web exec prettier --write .
migrate: ; dotnet ef database update --project src/DevStation.Infrastructure --startup-project src/DevStation.Web
db-reset: ; ./scripts/db-reset.sh
docker-up: ; podman compose up -d postgres
docker-down: ; podman compose down
