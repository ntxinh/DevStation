# DevStation — Design Spec

Date: 2026-10-05
Status: Approved (design), pending implementation
Path: Architectural → modular monolith, option A (4-project, module-sliced)

## 1. Purpose

Self-hosted developer control plane: one web UI to observe and safely act on
projects, environments, services, containers, databases, jobs, deployments,
logs, health, and integrations. Single developer / small team. No enterprise
multi-tenancy in MVP. Philosophy: **Observe → Understand → Act**, with
confirmation and audit on every action.

## 2. Locked decisions

- **Auth**: single dev token (`Authorization: Bearer <token>`), token
  from `DevStation:Token` config / `DEVSTATION_TOKEN` env. Authorization
  tiers `Read | Execute | Deploy | Admin` enforced as ASP.NET policies now;
  OIDC-compatible seam kept (claims-based, no auth logic in feature code).
- **Layout**: monorepo `apps/web` (Next.js) + `src/` (.NET solution).
- **Runtime**: podman available, no docker daemon — compose targets
  `podman compose`; container integration auto-detects podman vs docker CLI.
- **Scope**: implement as many spec phases as possible this run; AI (phase 8)
  and MCP (phase 9) get provider seams and are implemented only if reached.

## 3. Repository structure

```text
devstation/
├── apps/
│   └── web/                      # Next.js 15 app
├── src/
│   ├── DevStation.sln
│   ├── DevStation.Web/           # endpoints, auth, middleware, hubs
│   ├── DevStation.Application/   # module services, provider interfaces, DTOs
│   ├── DevStation.Domain/        # entities, value objects, enums
│   └── DevStation.Infrastructure/# EF Core, provider concretes, SignalR impl
├── tests/
│   ├── unit/                     # DevStation.Tests.Unit (xUnit)
│   ├── integration/              # DevStation.Tests.Integration (xUnit + Testcontainers)
│   └── e2e/                      # Playwright
├── docs/
│   ├── architecture/  features/  integrations/  api/  decisions/  superpowers/specs/
├── scripts/
├── docker/
├── .github/workflows/
├── AGENTS.md  README.md  DESIGN.md  CONTRIBUTING.md
├── .editorconfig  .gitignore  .env.example
├── docker-compose.yml  Makefile  mise.toml
```

### Module organization (all four backend projects use it)

```text
Modules/
  Projects/        Environments/   Services/   Containers/
  Databases/       Jobs/           Deployments/Logs/
  Health/          Integrations/   Audit/      Search/
  Commands/        Notifications/
Shared/            # ApiEnvelope, errors, auth policies, primitives
```

Inside `DevStation.Application`, each module holds feature slices:

```text
Projects/
  CreateProject/CreateProjectHandler.cs, CreateProjectRequest.cs, ...
  GetGitStatus/GetGitStatusHandler.cs
```

Endpoints live in `DevStation.Web/Modules/<Area>Endpoints.cs` and stay thin:
bind → validate → call handler → wrap envelope.

## 4. Backend design (.NET 10, minimal APIs)

- **DI**: per-module `IServiceCollection` extension (`AddProjectsModule()`),
  composed in `Program.cs`. No MediatR; handlers are injected services.
- **Envelope**: `{ data, error }` on every response; `error = { code, message }`.
- **Errors**: `AppException(code, status, message)` + global exception handler
  middleware; no stack traces outside Development. HTTP codes per spec §44.
- **Validation**: input DTOs validated in handlers; FluentValidation only
  where complexity justifies (query execution, environment config).
- **EF Core + Npgsql**: code-first, migrations in
  `DevStation.Infrastructure/Persistence`, UUID PKs, UTC timestamps
  (`DateTimeOffset`), timezone conversion only at presentation.
- **Secrets**: entities store `SecretRef`, never values; `ISecretStore` with
  `EnvSecretStore` (dev) concrete; API DTOs never carry secret material.
- **SignalR hubs** (`/hubs/*`): `logs`, `deployments`, `health`, `jobs` —
  same Bearer auth (token via query param for EventSource/WS), same policies.

### Provider abstractions (Application) → concretes (Infrastructure)

| Interface | Concretes | Notes |
|---|---|---|
| `IGitProvider` | `LibGit2SharpGitProvider` | status, branch, commit, ahead/behind, recent commits; allowlisted repo paths; no shell |
| `IContainerProvider` | `PodmanCliProvider`, `DockerCliProvider` (auto-detect) | CLI via `Process` with arg arrays, never `sh -c`; list/inspect/start/stop/restart/logs |
| `IDatabaseProvider` | `PostgreSqlProvider` (Npgsql), `SqlServerProvider` (Microsoft.Data.SqlClient) | schema browse + query exec; read-only mode per connection; timeout + max-rows enforced |
| `IJobProvider` | `QuartzJobProvider` | Quartz.NET hosted in API; jobs defined in config/code, not user shell |
| `IDeploymentProvider` | `MockDeploymentProvider`, `GitHubActionsProvider` | states Queued/Running/Succeeded/Failed/Cancelled |
| `ISecretStore` | `EnvSecretStore` | env-var backed; Key Vault/OS keyring later |
| `IIntegration` | `GitHubIntegration` (Phase 7), Jira/Azure/Cloudflare stubs behind config | HttpClient, typed options, token via ISecretStore |
| `IAiProvider` | seam only this run | OpenAI/Ollama later; tool-calling must route through same action layer |

### Safety model

- Action endpoints require `Execute`/`Deploy`/`Admin` policy AND
  `confirm: true` in payload for destructive ops (container stop, prod
  deploy, prod DB write, dangerous SQL).
- Every action writes `AuditEvent` (timestamp, user, action, resource,
  resourceId, environment, result, metadata sans secrets).
- SQL execution guardrails: per-connection `readOnly`, keyword screening
  (`DROP|TRUNCATE|ALTER|GRANT|DELETE|UPDATE|INSERT` → confirmation tier),
  `MaxRows` (default 1000), `TimeoutSeconds` (default 30), cancellation.
- No arbitrary shell execution anywhere; commands palette dispatches to
  typed backend commands only.

## 5. Database schema (core tables)

`Projects, Environments, Services, DatabaseConnections, Jobs, JobExecutions,
Deployments, HealthChecks, HealthCheckResults, Integrations, AuditEvents,
Notifications, SavedQueries, QueryHistory`

Containers are runtime-derived (not persisted; cached snapshots allowed in
memory only).

## 6. Frontend design (`apps/web`)

- Next.js 15 App Router, React 19, TS strict, Tailwind v4, dark theme only.
- **Server state**: TanStack Query (`queryClient`, typed `apiFetch` client
  sending `Authorization: Bearer` with the token from localStorage).
  **UI state**: Zustand (palette open, sidebar, selected environment, prefs).
- Forms: React Hook Form + Zod.
- Live data: `@microsoft/signalr` client per hub, feeding query-cache updates.
- Shell: left nav (Dashboard, Projects, Environments, Services, Containers,
  Databases, Jobs, Deployments, Logs, Health, Integrations, Audit Log,
  Settings), top bar (global search, env selector), `Cmd+K` palette.
- Command palette: keyboard-navigable; commands call `/api/commands`
  (server-defined command list, permission-checked); destructive commands
  open confirmation dialog before dispatch.
- UX rules per spec §37: dense tables, status badges (never color-only),
  loading/empty/error states, visible focus rings, semantic HTML.

## 7. API surface (v1)

```
GET    /health
GET    /api/projects[?q=]            POST /api/projects
GET    /api/projects/{id}            PATCH/DELETE /api/projects/{id}
GET    /api/projects/{id}/git        GET /api/projects/{id}/commits
GET    /api/environments             POST /api/environments
GET    /api/environments/{id}        PATCH/DELETE /api/environments/{id}
GET    /api/environments/compare?a=&b=
GET    /api/services                 POST /api/services
POST   /api/services/{id}/restart    GET /api/services/{id}/health
GET    /api/containers               GET /api/containers/{id}
POST   /api/containers/{id}/start|stop|restart
GET    /api/containers/{id}/logs     GET /api/containers/{id}/stats
GET    /api/databases/connections    POST /api/databases/connections
GET    /api/databases/{id}/schema    POST /api/databases/{id}/query
GET    /api/databases/{id}/history   GET/POST /api/databases/saved-queries
GET    /api/jobs                     POST /api/jobs/{id}/trigger|pause|resume
GET    /api/jobs/{id}/executions
GET    /api/deployments              POST /api/deployments
GET    /api/deployments/{id}         POST /api/deployments/{id}/cancel
GET    /api/logs?source=&severity=&q=
GET    /api/health/overview          POST /api/health/{id}/check
GET    /api/integrations             POST /api/integrations/{id}/connect|test
GET    /api/audit                    GET /api/search?q=
GET    /api/commands                 POST /api/commands/{id}/execute
```

SignalR: `/hubs/logs`, `/hubs/deployments`, `/hubs/health`, `/hubs/jobs`.

## 8. Infrastructure & tooling

- `docker-compose.yml`: `postgres` (16), `api`, `web`; podman-compose
  compatible; postgres is the only hard dependency.
- `Makefile`: setup, dev, build, test, test-unit, test-integration,
  test-e2e, lint, format, migrate, db-reset, docker-up, docker-down.
- `mise.toml`: dotnet 10, node 22. Package manager: pnpm.
- `.env.example`: `DEVSTATION_TOKEN`, connection string, integration keys —
  all placeholder values.

## 9. Testing

- **Unit** (xUnit): handlers, validators, SQL guardrails, provider arg
  builders, authorization policies, command dispatch.
- **Integration** (xUnit + Testcontainers→Podman via
  `DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock`): EF migrations,
  CRUD endpoints, query guardrails vs real Postgres, SignalR hubs.
  SQL Server integration tests skipped unless a server is configured.
- **Frontend** (Vitest): stores, api client, palette reducer, key components.
- **E2E** (Playwright): boot → dashboard renders → create project → create
  environment → view containers page → safe query → command palette → audit
  log entry. Browser containers mocked/seeded where runtime absent.

## 10. CI

`.github/workflows/ci.yml`: lint (eslint + dotnet format --verify) →
typecheck → vitest → `dotnet build` → unit tests → integration tests →
e2e (Playwright, services via compose) → docker image build. No production
deploy job.

## 11. Docs

- `README.md` (quickstart), `AGENTS.md` (agent conventions: architecture
  rules, forbidden deps, how to add module/integration/AI tool),
  `DESIGN.md` (visual language), `CONTRIBUTING.md`.
- `docs/architecture/` (C4-style overview + module map),
  `docs/api/` (endpoint reference), `docs/integrations/`,
  `docs/decisions/ADR-001-modular-monolith.md`, `ADR-002-provider-abstraction.md`,
  `ADR-003-dev-token-auth.md`, `ADR-004-cli-container-provider.md`.

## 12. Phase plan for this run

| # | Content | Exit check |
|---|---------|-----------|
| 0 | Repo, both shells, Postgres+EF, auth, error handling, compose, Makefile, mise, CI, docs, `/health`, dashboard skeleton | `make dev` boots web+api+db; `/health` returns ok |
| 1 | Projects CRUD + git status + dashboard data | create project, see branch/commit/ahead-behind |
| 2 | Environments CRUD + health checks + compare | QA vs Prod compare renders |
| 3 | Services + container provider + controls | list podman containers; start/stop with confirm |
| 4 | DB connections + schema + SQL editor + guardrails + history | run SELECT, see rows/ms; dangerous SQL blocked |
| 5 | Jobs + Quartz + trigger/pause + executions | trigger job, see execution row |
| 6 | Deployments + mock provider + GH Actions provider | mock deploy flows through states |
| 7 | Logs module + SignalR streaming + audit log UI | live log line appears |
| 8 | Search + command palette + notifications | Cmd+K runs a read command |
| 9 | GitHub integration (repos/PRs/workflows); Jira/Azure/Cloudflare config stubs | repos list with token |
| 10 | Polish: e2e pass, a11y sweep, docs final | CI green |

AI and MCP land after 10 only if time remains; seams (`IAiProvider`,
command layer = future MCP tools) are already in place.

## 13. Non-goals this run

- Real OIDC/OAuth login, multi-user RBAC, email notifications.
- Jira/Azure/Cloudflare live calls without credentials (mocked/stubbed).
- AI provider implementation, MCP server (unless reached).
- Kubernetes; SQL Server testcontainer (configured-only).
