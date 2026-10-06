# DevStation

A self-hosted developer control plane: one web UI to observe and safely act on
your projects, environments, services, containers, databases, scheduled jobs,
deployments, logs, health checks, and external integrations.

Philosophy: **Observe → Understand → Act** — every state-changing action goes
through a typed command layer, asks for confirmation when destructive, and is
written to the audit log.

Single developer / small team. No multi-tenancy. One token, four authorization
tiers (read → execute → deploy → admin).

## Stack

- **API** — .NET 10 minimal APIs, EF Core + Postgres, SignalR live hubs
  (`src/DevStation.{Web,Application,Domain,Infrastructure}`)
- **Web** — Next.js 15 + React 19 + Tailwind v4 (`apps/web`)
- **DB** — Postgres 16 (only hard dependency; podman-compose or docker-compose)
- **Jobs** — Quartz.NET in-process

## Quickstart

Prereqs: `mise` (or dotnet 10 + node 22 + pnpm 9), podman or docker.

```sh
cp .env.example .env        # set DEVSTATION_TOKEN (and integration keys if wanted)
mise install                # dotnet 10 + node 22
make setup                  # pnpm install + dotnet restore
make dev                    # postgres + api (:5080) + web (:3000), one command
```

Open http://localhost:3000, paste your `DEVSTATION_TOKEN`, done.

`make dev` sources `.env` if present; the API runs `dotnet run` on :5080 and
applies EF migrations on startup, so a fresh clone just works.

### Alternative: everything in containers

```sh
DEVSTATION_TOKEN=your-token podman compose up --build
# web :3000 → api :5080 → postgres :5432
```

## Configuration

| Variable | Purpose |
|---|---|
| `DEVSTATION_TOKEN` | Bearer token for the API and the web UI login gate |
| `DEVSTATION_CONNECTIONSTRING` | Overrides `ConnectionStrings:Default` |
| `NEXT_PUBLIC_API_URL` | API base URL the web app calls (default `http://localhost:5080`) |
| `OTEL_ENABLED=true` | Opt-in OpenTelemetry (console exporter, ASP.NET Core traces + metrics) |
| `GITHUB_TOKEN` / `JIRA_*` / `AZURE_*` / `CLOUDFLARE_TOKEN` | Integration credentials (see `docs/integrations/`) |

Additional tokens with lower privileges:
`DevStation:TokenTiers:<token> = read|execute|deploy|admin` in config.

## Feature tour

| Area | What you get |
|---|---|
| Dashboard | Project/service/container counts, environment health at a glance |
| Projects | Registered repos; branch, commit, ahead/behind, recent commits (LibGit2Sharp, no shell) |
| Environments | Dev/QA/prod registry, health checks, side-by-side compare |
| Services | Registered services, health polling, restart with confirm |
| Containers | Live podman/docker list, inspect, logs, stats, start/stop/restart |
| Databases | Postgres + SQL Server connections, schema browser, SQL editor with per-connection read-only mode, dangerous-SQL guardrails, query history, saved queries |
| Jobs | Quartz-defined jobs, trigger/pause/resume, execution history |
| Deployments | Mock provider by default; GitHub Actions provider when a token is configured |
| Logs | Structured app log buffer + SignalR live tail at `/hubs/logs` |
| Health | Http/TCP checks with polling, results history, live badge updates |
| Search + Palette | `⌘K` — server-defined commands (nav, queries, confirmed actions) + cross-module search |
| Audit | Every action recorded (who, what, resource, result) — filterable |
| Integrations | GitHub (repos/PRs/workflow runs); Jira, Azure DevOps, Cloudflare config stubs |

## Repo layout

```text
apps/web         Next.js UI (features/ per module, lib/api.ts client)
src/             .NET 10 solution (Web, Application, Domain, Infrastructure)
tests/unit       xUnit — handlers, guardrails, providers
tests/integration  xUnit + Testcontainers — endpoints vs real Postgres
tests/e2e        Playwright — dashboard, projects, query, palette, audit
docker/          api + web Dockerfiles
docs/            architecture, api reference, integrations, ADRs
scripts/         dev.sh, db-reset.sh, e2e-api.sh
```

## Commands

```sh
make dev               # one-command boot: postgres + api + web
make test              # unit + integration + vitest
make test-e2e          # playwright (spins up its own postgres + api + web)
make lint              # dotnet format --verify + eslint
make migrate           # apply EF migrations
make db-reset          # drop/recreate dev postgres volume
```

Integration tests use Testcontainers. With podman:
`export DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock`
(the Makefile target already sets this).

## Safety model

- `Authorization: Bearer <DEVSTATION_TOKEN>` on every `/api/*` request and hub.
- Tiers: `read` (all GETs) → `execute` (CRUD, runs) → `deploy` (prod deploys)
  → `admin` (integrations config). The main token is admin.
- Destructive actions need `confirm=true`; production SQL writes are blocked;
  dangerous SQL (`DROP`, `ALTER`, …) needs confirmation; row/timeout caps
  apply per connection.
- No arbitrary shell execution anywhere — the palette dispatches typed
  commands only.

See `AGENTS.md` for architecture rules, `CONTRIBUTING.md` for workflow, and
`docs/` for the full reference.
