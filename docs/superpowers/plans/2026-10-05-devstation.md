# DevStation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build DevStation phases 0–9+ per the approved spec: a self-hosted developer control plane (modular monolith ASP.NET 10 API + Next.js 15 web + PostgreSQL) covering projects/git, environments, health, services, containers, databases+SQL editor, jobs, deployments, logs, audit, search, command palette, and integrations.

**Architecture:** 4-project modular monolith (`DevStation.Web`, `.Application`, `.Domain`, `.Infrastructure`), each organized `Modules/<Area>/` with vertical feature slices. Provider abstractions in Application; concretes in Infrastructure. Bearer dev-token auth with `Read|Execute|Deploy|Admin` policies; `{data,error}` envelope; audit row on every action; SignalR hubs for live data.

**Tech Stack:** .NET 10 SDK (10.0.401), EF Core 10 + Npgsql, SignalR, Quartz.NET, LibGit2Sharp, Microsoft.Data.SqlClient, Testcontainers; Next.js 15 + React 19 + TS strict, Tailwind v4, TanStack Query, Zustand, RHF+Zod, @microsoft/signalr, react-icons; pnpm; podman/podman-compose; Playwright.

**Spec:** `docs/superpowers/specs/2026-10-05-devstation-design.md` — read it first; API surface, schema, safety model, and phase exit checks are defined there.

## Global Constraints

- Token auth: `Authorization: Bearer <DEVSTATION_TOKEN>`; SignalR accepts `?access_token=`. Policies: `Read` < `Execute` < `Deploy` < `Admin`; satisfied via `tier` claim ranks.
- Every response body is `{"data":...,"error":null}` or `{"data":null,"error":{"code","message"}}` — enforced by one `ApiResult` helper, never hand-written JSON.
- Destructive/action endpoints: correct policy + request payload `confirm: true` + `AuditEvent` row. Never secret values in DTOs/logs/audit metadata.
- No `sh -c`/shell strings anywhere; `ProcessStartInfo.ArgumentList` only. No MediatR, AutoMapper, MassTransit, Redux, Axios, lucide-react.
- C# nullable enabled, file-scoped namespaces, `async` + `CancellationToken` end-to-end. TS `strict`, `@/*` path alias.
- UUID PKs, `DateTimeOffset` UTC timestamps. Commit after every task (`feat:|fix:|docs:|chore:` prefix).

---

### Task 0: Repo scaffolding + dev environment

**Files:**
- Create: `.gitignore`, `.editorconfig`, `.env.example`, `mise.toml`, `Makefile`, `docker-compose.yml`, `docker/api.Dockerfile`, `docker/web.Dockerfile`, `scripts/dev.sh`, `scripts/db-reset.sh`

**Interfaces:**
- Produces: `make docker-up` starts Postgres on `localhost:5432` (`devstation`/`devstation`, db `devstation`); `DEVSTATION_TOKEN=dev-token-change-me` convention; `scripts/dev.sh` = the `make dev` implementation used by every later task.

- [ ] **Step 1: Write root files**

`.gitignore`: standard dotnet+node sets (`bin/ obj/ node_modules/ .next/ *.user .env TestResults/ playwright-report/ .idea/ .vs/`).

`.editorconfig`: 4-space C#, 2-space ts/json/yaml, `dotnet_diagnostic` defaults, `csharp_style_namespace_declarations=file_scoped:warning`.

`.env.example`:
```bash
DEVSTATION_TOKEN=dev-token-change-me
DEVSTATION_CONNECTIONSTRING=Host=localhost;Port=5432;Database=devstation;Username=devstation;Password=devstation
NEXT_PUBLIC_API_URL=http://localhost:5080
# optional integrations
GITHUB_TOKEN=
JIRA_BASE_URL=
JIRA_TOKEN=
AZURE_TENANT_ID=
AZURE_CLIENT_ID=
AZURE_CLIENT_SECRET=
CLOUDFLARE_TOKEN=
```

`mise.toml`:
```toml
[tools]
dotnet = "10"
node = "22"
```

`docker-compose.yml` (podman-compose compatible):
```yaml
services:
  postgres:
    image: docker.io/library/postgres:16-alpine
    environment:
      POSTGRES_USER: devstation
      POSTGRES_PASSWORD: devstation
      POSTGRES_DB: devstation
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U devstation -d devstation"]
      interval: 5s
      timeout: 3s
      retries: 12
  api:
    build:
      context: .
      dockerfile: docker/api.Dockerfile
    environment:
      DEVSTATION_TOKEN: ${DEVSTATION_TOKEN:-dev-token-change-me}
      ConnectionStrings__Default: Host=postgres;Port=5432;Database=devstation;Username=devstation;Password=devstation
    ports: ["5080:5080"]
    depends_on:
      postgres: { condition: service_healthy }
  web:
    build:
      context: .
      dockerfile: docker/web.Dockerfile
    environment:
      NEXT_PUBLIC_API_URL: http://localhost:5080
    ports: ["3000:3000"]
    depends_on: [api]
volumes:
  pgdata:
```

`docker/api.Dockerfile`: two-stage — `mcr.microsoft.com/dotnet/sdk:10.0` build `src/DevStation.Web` publish → `mcr.microsoft.com/dotnet/aspnet:10.0` run, `ASPNETCORE_URLS=http://+:5080`, ENV `DOTNET_gcServer=1`.
`docker/web.Dockerfile`: `node:22-alpine` — `corepack enable && pnpm i --frozen-lockfile && pnpm build` in `apps/web`, `pnpm start` (standalone output optional; keep simple server start).

`scripts/dev.sh`:
```bash
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
```

`scripts/db-reset.sh`: `podman compose down -v postgres && podman compose up -d postgres`.

`Makefile` — targets delegate to scripts/dotnet/pnpm so CI and devs share commands:
```make
.PHONY: setup dev build test test-unit test-integration test-e2e lint format migrate db-reset docker-up docker-down
setup: ; pnpm --dir apps/web install && dotnet restore src/DevStation.sln
dev: ; ./scripts/dev.sh
build: ; dotnet build src/DevStation.sln && pnpm --dir apps/web build
test: test-unit test-integration ; pnpm --dir apps/web test -- --run
test-unit: ; dotnet test tests/unit -v minimal
test-integration: ; DOCKER_HOST=unix://$$XDG_RUNTIME_DIR/podman/podman.sock dotnet test tests/integration -v minimal
test-e2e: ; pnpm --dir apps/web exec playwright test
lint: ; dotnet format src/DevStation.sln --verify-no-changes ; pnpm --dir apps/web lint
format: ; dotnet format src/DevStation.sln ; pnpm --dir apps/web exec prettier --write .
migrate: ; dotnet ef database update --project src/DevStation.Infrastructure --startup-project src/DevStation.Web
db-reset: ; ./scripts/db-reset.sh
docker-up: ; podman compose up -d postgres
docker-down: ; podman compose down
```

- [ ] **Step 2: Verify postgres boots**

Run: `chmod +x scripts/*.sh && make docker-up && sleep 4 && podman exec devstation-postgres-1 pg_isready -U devstation` (container name may be `devstation_postgres_1` under podman-compose — check `podman ps`).
Expected: `accepting connections`.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "chore: repo scaffolding and dev environment"
```

---

### Task 1: Backend skeleton — solution, shared kernel, auth, health

**Files:**
- Create: `src/DevStation.sln`, `src/DevStation.Domain/DevStation.Domain.csproj`, `src/DevStation.Application/DevStation.Application.csproj`, `src/DevStation.Infrastructure/DevStation.Infrastructure.csproj`, `src/DevStation.Web/DevStation.Web.csproj`, `src/DevStation.Web/Program.cs`, `src/DevStation.Web/appsettings.json`, `src/DevStation.Web/appsettings.Development.json`, `src/DevStation.Web/Shared/Auth/DevTokenAuthenticationHandler.cs`, `src/DevStation.Web/Shared/Auth/Tiers.cs`, `src/DevStation.Web/Shared/Middleware/ApiExceptionMiddleware.cs`, `src/DevStation.Web/Shared/Middleware/CorrelationIdMiddleware.cs`, `src/DevStation.Application/Shared/Results/ApiEnvelope.cs`, `src/DevStation.Application/Shared/Results/AppException.cs`, `src/DevStation.Application/Shared/Results/ApiResult.cs`, `src/DevStation.Web/Modules/Health/HealthEndpoints.cs`
- Test: `tests/unit/DevStation.Tests.Unit/DevStation.Tests.Unit.csproj`, `tests/unit/DevStation.Tests.Unit/Shared/TierPolicyTests.cs`

**Interfaces:**
- Produces (every later task consumes):
  - `ApiEnvelope<T>(T? Data, ApiError? Error)`, `ApiError(string Code, string Message)`, static `ApiResult.Ok(data)` / `ApiResult.Fail(code,msg,status)` returning `IResult` with envelope.
  - `AppException(string Code, int Status, string Message)`; middleware maps it → envelope; unhandled → `500 INTERNAL_ERROR`; `Dev` env appends `detail`.
  - Auth handler reads `Authorization: Bearer` or `?access_token=` (for SignalR), compares to `config["DevStation:Token"]` (env `DEVSTATION_TOKEN`), emits claims `name=dev`, `tier=admin`. Policies registered: `Read`, `Execute`, `Deploy`, `Admin` — RequireClaim `tier` with rank map `read=1,execute=2,deploy=3,admin=4` via custom `AuthorizationHandler<TierRequirement>` checking `rank(userTier) >= rank(required)`.
  - `WebApplication.MapModuleEndpoints()` extension: `app.MapGroup("/api").RequireAuthorization("Read")` returns `RouteGroupBuilder` modules hang endpoints on.
  - `GET /health` (outside `/api`, anonymous): `{status:"ok",uptime}` raw JSON, no envelope.

- [ ] **Step 1: Write the failing test**

`TierPolicyTests.cs`:
```csharp
public class TierPolicyTests
{
    [Theory]
    [InlineData("admin","read",true)][InlineData("read","admin",false)]
    [InlineData("deploy","execute",true)][InlineData("execute","deploy",false)]
    public void Rank_comparison(string user,string required,bool expected)
        => Assert.Equal(expected, Tiers.Rank(user) >= Tiers.Rank(required));
}
```

- [ ] **Step 2: Scaffold solution**

Run: `dotnet new sln -n DevStation -o src` then `dotnet new classlib` ×3 (`Domain`,`Application`,`Infrastructure`), `dotnet new web -n DevStation.Web` (net10.0, nullable, implicit usings). `dotnet sln add` all; project refs: Application→Domain, Infrastructure→Application+Domain, Web→Application+Infrastructure. `tests/unit`: `dotnet new xunit`, refs all four.

- [ ] **Step 3: Run test — fail**

`dotnet test tests/unit` → fails (no `Tiers`).

- [ ] **Step 4: Implement shared kernel + auth + health**

`Tiers.cs`: `public static class Tiers { public static int Rank(string t)=>t switch{"read"=>1,"execute"=>2,"deploy"=>3,"admin"=>4,_=>0}; }` plus `TierRequirement`, `TierHandler`, `DevTokenAuthenticationHandler` (scheme `"DevToken"`).

`Program.cs` minimal shape:
```csharp
var b = WebApplication.CreateBuilder(args);
b.Services.AddAuthentication("DevToken").AddScheme<AuthenticationSchemeOptions, DevTokenAuthenticationHandler>("DevToken", _=>{});
b.Services.AddAuthorization(o=>{ foreach(var t in new[]{"Read","Execute","Deploy","Admin"}) o.AddPolicy(t,p=>p.Requirements.Add(new TierRequirement(t.ToLowerInvariant()))); });
// module DI + DbContext added by later tasks via extension methods
var app = b.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health",()=>Results.Ok(new{status="ok",uptime=Environment.TickCount64})).AllowAnonymous();
var api = app.MapGroup("/api").RequireAuthorization("Read");
HealthModule.Map(api); // placeholder returning module status list — replaced by real module in Phase 2
app.Run();
public partial class Program {} // for WebApplicationFactory
```

- [ ] **Step 5: Run test — pass**; then smoke: `DEVSTATION_TOKEN=x dotnet run --project src/DevStation.Web --urls http://localhost:5080` → `curl localhost:5080/health` = 200; `curl localhost:5080/api/health` = 401; with `Authorization: Bearer x` = 200.

- [ ] **Step 6: Commit** `feat(api): skeleton, dev-token auth, health endpoint`

---

### Task 2: Persistence — DbContext, entities, initial migration

**Files:**
- Create: `src/DevStation.Domain/Entities/{Project,Environment_,Service,DatabaseConnection,JobDefinition,JobExecution,Deployment,HealthCheck,HealthCheckResult,Integration,AuditEvent,Notification,SavedQuery,QueryHistoryEntry}.cs`, `src/DevStation.Domain/Enums/{HealthStatus,JobStatus,DeploymentStatus,DeploymentState,IntegrationStatus}.cs`, `src/DevStation.Domain/ValueObjects/SecretRef.cs`, `src/DevStation.Infrastructure/Persistence/DevStationDbContext.cs`, `src/DevStation.Infrastructure/Persistence/Configurations/*.cs` (one per entity or fluent config inline in context), `src/DevStation.Infrastructure/DependencyInjection.cs`
- Modify: `src/DevStation.Web/Program.cs` (add `AddInfrastructure(config)`)
- Test: `tests/integration/DevStation.Tests.Integration/Persistence/MigrationSmokeTests.cs`

**Interfaces:**
- Produces: `DevStationDbContext` (`DbSet<T>` for all entities, table names snake_case via `UseSnakeCaseNamingConvention` if package added — otherwise explicit `ToTable`); `IServiceCollection.AddInfrastructure(IConfiguration)` registering `DbContext` (Npgsql from `ConnectionStrings:Default`) and applying `db.Database.Migrate()` on startup via hosted `MigrationHostedService`. Entity fields exactly per spec §5 + §9/12/14/18/19/21/22/27/30 definitions; `Environment_` class named `Environment` is legal but alias conflicts — name it `AppEnvironment`, table `environments`.
- Consumes: Task 1 infrastructure.

- [ ] **Step 1: Write the failing integration test**

`MigrationSmokeTests.cs` (Testcontainers Postgres):
```csharp
public class MigrationSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    public Task InitializeAsync()=>_pg.StartAsync();
    public Task DisposeAsync()=>_pg.DisposeAsync().AsTask();
    [Fact] public async Task Migrations_create_all_tables()
    {
        var opt=new DbContextOptionsBuilder<DevStationDbContext>().UseNpgsql(_pg.GetConnectionString()).Options;
        await using var db=new DevStationDbContext(opt);
        await db.Database.MigrateAsync();
        var tables=await db.Database.SqlQuery<string>($"select tablename from pg_tables where schemaname='public'").ToListAsync();
        foreach(var t in new[]{"projects","environments","services","database_connections","job_definitions","job_executions","deployments","health_checks","health_check_results","integrations","audit_events","notifications","saved_queries","query_history"}) Assert.Contains(t,tables);
    }
}
```

- [ ] **Step 2: Run — fail** (`tests/integration` csproj: xunit + `Testcontainers.PostgreSql`, `Microsoft.EntityFrameworkCore`, refs to Infrastructure).

- [ ] **Step 3: Implement entities + context + migration**

Entities per spec fields; `SecretRef { string Key; }` owned type stored as `secret_ref` column. `dotnet ef migrations add Initial --project src/DevStation.Infrastructure --startup-project src/DevStation.Web` (install `dotnet-ef` tool if missing: `dotnet tool install -g dotnet-ef`).

- [ ] **Step 4: Run integration test — pass** (needs `DOCKER_HOST` per Makefile).

- [ ] **Step 5: Commit** `feat(api): domain entities, DbContext, initial migration`

---

### Task 3: Frontend scaffold — Next.js app + providers + api client

**Files:**
- Create: `apps/web/package.json`, `next.config.ts`, `tsconfig.json`, `postcss.config.mjs`, `app/globals.css`, `app/layout.tsx`, `app/providers.tsx`, `app/page.tsx` (redirect → `/dashboard`), `lib/api.ts`, `lib/types.ts`, `lib/format.ts`, `stores/ui.ts`, `vitest.config.ts`, `vitest.setup.ts`, `.eslintrc.json` (or flat `eslint.config.mjs`), `playwright.config.ts`
- Test: `apps/web/lib/api.test.ts`

**Interfaces:**
- Produces:
  - `apiFetch<T>(path:string, init?:RequestInit):Promise<T>` — prepends `NEXT_PUBLIC_API_URL`, sets `Authorization: Bearer` from `localStorage["devstation.token"]`, `content-type: application/json` on bodies; unwraps `{data,error}` → returns `data`, throws `ApiError(code,message,status)` on `error` or non-2xx.
  - `useUiStore` (Zustand): `{ paletteOpen, sidebarOpen, selectedEnvironmentId, setPaletteOpen, toggleSidebar, selectEnvironment }`.
  - `Providers`: `QueryClientProvider` + `ThemeProvider`-free dark root; `<body class="dark">`.
  - Tailwind v4 via `@import "tailwindcss"` in globals.css; design tokens per DESIGN.md (bg `#0d1117`, panel `#161b22`, border `#30363d`, text `#e6edf3`, accent `#58a6ff`, status ok `#3fb950` warn `#d29922` err `#f85149` muted `#8b949e`).

- [ ] **Step 1: Write the failing test**

`lib/api.test.ts`:
```ts
import { describe,it,expect,vi } from "vitest";
import { apiFetch } from "./api";
it("unwraps data and sets auth header", async ()=>{
  localStorage.setItem("devstation.token","t");
  vi.stubGlobal("fetch",vi.fn(async()=>new Response(JSON.stringify({data:{id:"1"},error:null}),{status:200})));
  const r=await apiFetch<{id:string}>("/api/x");
  expect(r.id).toBe("1");
  expect((fetch as any).mock.calls[0][1].headers.Authorization).toBe("Bearer t");
});
it("throws ApiError on error envelope", async()=>{
  vi.stubGlobal("fetch",vi.fn(async()=>new Response(JSON.stringify({data:null,error:{code:"X",message:"bad"}}),{status:400})));
  await expect(apiFetch("/api/x")).rejects.toMatchObject({code:"X"});
});
```

- [ ] **Step 2: Scaffold app**

`mkdir -p apps/web && cd apps/web && pnpm init` then `pnpm add next@15 react@19 react-dom@19 @tanstack/react-query zustand react-hook-form zod @hookform/resolvers @microsoft/signalr react-icons` and `pnpm add -D typescript @types/react @types/node @types/react-dom tailwindcss @tailwindcss/postcss vitest jsdom @vitejs/plugin-react eslint eslint-config-next prettier @playwright/test`. `tsconfig` strict + `paths:{"@/*":["./*"]}`; `vitest.config.ts` jsdom env; package.json scripts: `dev`, `build`, `start`, `lint`, `test`, `test:e2e`.

- [ ] **Step 3: Run test — fail → Step 4: implement api.ts/types/format/stores/layout/providers — pass**

- [ ] **Step 5: Smoke** `pnpm dev` renders dark empty shell at :3000.

- [ ] **Step 6: Commit** `feat(web): Next.js scaffold, api client, providers`

---

### Task 4: App shell — nav, topbar, dashboard skeleton, token gate

**Files:**
- Create: `apps/web/components/Nav.tsx`, `components/Topbar.tsx`, `components/StatusBadge.tsx`, `components/Stat.tsx`, `components/EmptyState.tsx`, `components/ErrorState.tsx`, `components/ConfirmDialog.tsx`, `components/TokenGate.tsx`, `app/(app)/layout.tsx`, `app/(app)/dashboard/page.tsx`, `app/(app)/{projects,environments,services,containers,databases,jobs,deployments,logs,health,integrations,audit,settings}/page.tsx` (placeholder pages rendering `<ModulePlaceholder name/>`)

**Interfaces:**
- Produces: `<StatusBadge status>` (dot+label, never color-only), `<ConfirmDialog title description requireText? onConfirm>` used by ALL destructive actions later, `TokenGate` (no token in localStorage → inline token input stored then `location.reload()`), nav item list exported as `NAV_ITEMS`.
- Consumes: Task 3 api client/store.

- [ ] **Step 1: Implement shell** — left nav (spec §37 list), topbar (search input placeholder, env selector bound to `useUiStore.selectedEnvironmentId`), dashboard page with `<Stat>` cards fetching `/api/dashboard/summary` (may 404 → `<ErrorState>` until Task 5; acceptable interim), all module routes as placeholders. `Cmd+K` global keydown → `setPaletteOpen(true)` (palette body lands Task 13; bind the key now).

- [ ] **Step 2: Smoke** — `pnpm dev`, set token, navigate every nav item renders without error.

- [ ] **Step 3: Commit** `feat(web): app shell and navigation`

---

### Task 5: Phase 1 — Projects module + dashboard summary

**Files:**
- Create: `src/DevStation.Domain/Entities/Project.cs` (fields per spec §12), `src/DevStation.Application/Modules/Projects/{CreateProject,GetProject,UpdateProject,DeleteProject,ListProjects,GetGitStatus,GetRecentCommits}/`, `src/DevStation.Application/Modules/Dashboard/GetDashboardSummary/`, `src/DevStation.Application/Shared/Providers/IGitProvider.cs`, `src/DevStation.Infrastructure/Git/LibGit2SharpGitProvider.cs`, `src/DevStation.Web/Modules/ProjectsEndpoints.cs`, `src/DevStation.Web/Modules/DashboardEndpoints.cs`
- Create frontend: `app/(app)/projects/page.tsx` (list+create form modal), `app/(app)/projects/[id]/page.tsx` (overview + git card), `features/projects/{ProjectForm,GitStatusCard}.tsx`
- Test: `tests/unit/.../Projects/CreateProjectValidatorTests.cs`, `tests/unit/.../Git/GitProviderTests.cs` (init a temp repo via `Repository.Init`), `tests/integration/.../ProjectsEndpointTests.cs` (WebApplicationFactory + Testcontainers pg)

**Interfaces:**
- Produces: `IGitProvider` — `GitStatus GetStatus(string path)` → `{Branch, CommitSha, CommitMessage, Dirty:bool, AheadCount, BehindCount}`; `IReadOnlyList<CommitInfo> GetRecentCommits(string path,int n)` → `{Sha,Message,Author,When}`. Throws `AppException("GIT_NOT_REPO",400,...)` if path not a repo. `ProjectEndpoints` maps spec §7 routes. `GET /api/dashboard/summary` → `{projects,services,containers,environments:{healthy,degraded,unhealthy},recentActivity:AuditEvent[]}`.
- Consumes: Tasks 1–4.

- [ ] **Step 1: Failing tests** — validator (name required ≤200, repositoryUrl http(s) or empty, localPath absolute), git provider on `Repository.Init(tmp)` + a commit → `GetStatus` returns branch `master/main`, dirty=false, sha matches.

- [ ] **Step 2: Run — fail → Step 3: implement → Step 4: pass**

`LibGit2SharpGitProvider`: open `Repository(path)`; `Head.FriendlyName`, `Head.Tip.Sha/MessageShort`, `repo.RetrieveStatus().IsDirty`, `Head.TrackingDetails.AheadBy/BehindBy`. Guard: `Repository.IsValid(path)` else `GIT_NOT_REPO`. Path allowlist: `localPath` must be under config `DevStation:AllowedRepoRoots` (default `$HOME`) — reject otherwise with `GIT_PATH_FORBIDDEN`.

Endpoints thin: `group.MapPost("/projects", async (CreateProjectRequest r, CreateProjectHandler h, ClaimsPrincipal u, CancellationToken ct)=>ApiResult.Ok(await h.Handle(r,u,ct)))` pattern for all slices. `CreateProjectHandler` writes entity + `AuditEvent(action:"project.create")` (audit helper `IAuditWriter` in Application — `Record(action,resource,resourceId,user,result)`); implement `AuditWriter` in this task since first consumer (entity already exists).

Integration test: factory boots app vs Testcontainers pg (`ConfigureAppConfiguration` sets `ConnectionStrings:Default` + `DevStation:Token=test`); POST project → 200 envelope; GET list contains it; GET `/api/projects/{id}/git` on non-repo path → `GIT_NOT_REPO` envelope.

- [ ] **Step 5: Frontend** — projects list page (`useQuery(["projects"])`), create modal (RHF+Zod), detail page with `GitStatusCard` (branch, sha short, dirty badge, ahead/behind, recent commits table).

- [ ] **Step 6: Smoke** — real repo on disk (this repo), create project pointing at it, git card shows real branch.

- [ ] **Step 7: Commit** `feat: projects module with git status`

---

### Task 6: Phase 2 — Environments + health checks

**Files:**
- Create: `Application/Modules/Environments/{ListEnvironments,CreateEnvironment,UpdateEnvironment,DeleteEnvironment,CompareEnvironments}/`, `Application/Modules/Health/{GetHealthOverview,RunHealthCheck}/`, `Application/Shared/Providers/IHealthCheckExecutor.cs`, `Infrastructure/Health/{HealthCheckExecutor,HealthCheckPoller}.cs`, `Web/Modules/{EnvironmentsEndpoints,HealthEndpoints}.cs`, `Web/Hubs/HealthHub.cs`
- Frontend: `app/(app)/environments/page.tsx`, `environments/[id]/page.tsx`, `environments/compare/page.tsx`, `health/page.tsx`, `features/health/HealthBadge.tsx`
- Test: `unit/Environments/CompareTests.cs`, `unit/Health/HealthCheckExecutorTests.cs`, `integration/EnvironmentsEndpointTests.cs`

**Interfaces:**
- Produces: `IHealthCheckExecutor.Execute(HealthCheckDef,ct)→HealthResult{Status,LatencyMs,Error,CheckedAt}`; kinds: `http` (GET, expect 2xx/3xx, timeout), `tcp` (connect host:port), `database` (open conn via IDatabaseProvider), `container` (running? via IContainerProvider). `HealthCheckPoller` BackgroundService polls enabled checks every `IntervalSeconds`, persists `HealthCheckResult`, broadcasts `health.updated` over `/hubs/health`. Compare: `{version,commit,deployedAt,diff:{services:[{name,inA,inB}],variables:[{key,differs}]}}` — secrets never emitted (variables compared as `{key,presentInA,presentInB}` unless `secret=true` → `masked`).
- Consumes: Task 5 `IDatabaseProvider`/`IContainerProvider` may not exist yet — executor resolves providers via `IServiceProvider.GetService` and returns `Unknown` if absent (keeps Phase ordering safe).

- [ ] **Step 1–7:** TDD same rhythm (failing compare/executor tests → implement → pass → frontend env list + detail + compare page + health grid + SignalR subscribe → smoke → commit `feat: environments and health checks`).

---

### Task 7: Phase 3 — Services + containers

**Files:**
- Create: `Application/Modules/Services/{ListServices,CreateService,RestartService,GetServiceHealth}/`, `Application/Modules/Containers/{ListContainers,GetContainer,StartContainer,StopContainer,RestartContainer,GetContainerLogs,GetContainerStats}/`, `Application/Shared/Providers/IContainerProvider.cs`, `Infrastructure/Containers/{ContainerCliRunner,PodmanCliProvider,DockerCliProvider,ContainerProviderDetector}.cs`, `Web/Modules/{ServicesEndpoints,ContainersEndpoints}.cs`
- Frontend: `services/page.tsx`, `containers/page.tsx`, `containers/[id]/page.tsx` (info, logs viewer, start/stop/restart buttons → ConfirmDialog)
- Test: `unit/Containers/CliArgTests.cs`, `unit/Containers/ContainerOutputParserTests.cs`, `integration/ContainersProviderTests.cs` (real podman: pull `hello-world`, list — skip if no podman)

**Interfaces:**
- `IContainerProvider`: `Task<IReadOnlyList<ContainerInfo>> ListAsync(ct)` `{Id,Name,Image,State,Status,Ports[]}`; `InspectAsync(id)` adds env(minus `*PASSWORD*|*SECRET*|*TOKEN*|*KEY*` filtered), mounts, networks; `Start/Stop/RestartAsync(id,ct)`; `GetLogsAsync(id,tail,ct)→IAsyncEnumerable<string>` or `string[]`; `GetStatsAsync` optional (`stats --no-stream --format json`).
- `ContainerCliRunner.RunAsync(string exe, string[] args, ct)` — `ProcessStartInfo{FileName=exe}`, `ArgumentList.Add`, no shell; returns stdout; non-zero → `AppException("CONTAINER_CLI_ERROR",502,stderr truncated 500)`.
- Detector: `podman` on PATH → podman provider; else `docker`; else `NullContainerProvider` (empty lists, `CONTAINER_RUNTIME_UNAVAILABLE` on actions). Service `restart` = TCP/HTTP health recheck + audit (no real process control beyond containers — restart supported only when service maps to a container via `service.containerId`).

- [ ] **Steps:** TDD arg-builder (never produces shell strings; id validated `^[a-zA-Z0-9_.-]+$`), parser for `podman ps --format json`, integration vs real podman hello-world, frontend list + controls with ConfirmDialog on stop/restart, logs page. Commit `feat: services and container management`.

---

### Task 8: Phase 4 — Databases + SQL editor

**Files:**
- Create: `Application/Modules/Databases/{ListConnections,CreateConnection,UpdateConnection,DeleteConnection,GetSchema,ExecuteQuery,ListQueryHistory,ListSavedQueries,SaveQuery,DeleteSavedQuery}/`, `Application/Shared/Providers/IDatabaseProvider.cs`, `Application/Shared/Security/SqlGuard.cs`, `Infrastructure/Databases/{PostgreSqlProvider,SqlServerProvider,DatabaseProviderResolver}.cs`, `Infrastructure/Secrets/EnvSecretStore.cs` (+`ISecretStore` in Application), `Web/Modules/DatabasesEndpoints.cs`
- Frontend: `databases/page.tsx` (connection tree + editor), `features/databases/{ConnectionForm,SchemaTree,SqlEditor,ResultGrid,QueryHistoryPanel}.tsx`
- Test: `unit/Databases/SqlGuardTests.cs` (core), `integration/Databases/ExecuteQueryTests.cs` (real pg testcontainer: SELECT works, DROP blocked, row cap, timeout, readonly)

**Interfaces:**
- `IDatabaseProvider`: `TestConnectionAsync(DatabaseConnection,ct)→bool`; `GetSchemaAsync→{schemas→tables→{columns{ name,type,nullable,pk},indexes,views,procedures}}`; `ExecuteQueryAsync(conn,sql,maxRows,timeout,ct)→{columns[],rows[][],rowCount,durationMs,truncated}`. Resolver maps `conn.Kind→provider`.
- `SqlGuard.Classify(sql)→{Tier,Reason}`: strip comments/strings → `SELECT|WITH|EXPLAIN`→`Read`; `INSERT|UPDATE|DELETE|MERGE|CALL`→`Write`; `DROP|TRUNCATE|ALTER|CREATE|GRANT|REVOKE`→`Dangerous`; multiple statements → flag. Endpoint rules: `conn.ReadOnly` && tier>Read → `403 SQL_READONLY`; tier Dangerous → require `confirm:true`; production-type env (`envType=="production"`) && tier>Read → `403` always. Query params `CommandTimeout=timeout`, `maxRows` enforced via `LIMIT`/reader row cap; cancel via ct → `CommandCancel`.
- `ISecretStore`: `Set/Get/Delete(key)`; `EnvSecretStore` reads `DEVSTATION_SECRET_<KEY>` env; connections store `PasswordRef` (SecretRef), never raw.
- QueryHistory row per execution (sql truncated 4k, no param values), duration, rows, ok/error.

- [ ] **Steps:** SqlGuard tests table (comment tricks `"-- DROP"` inside string literal, `;` stacking, CTE writes), pg integration suite, frontend editor (textarea + lightweight keyword highlight overlay, Run/Cancel button wired to `AbortController`, ms/rows footer, results grid, history sidebar, saved queries). Commit `feat: database module with guarded SQL editor`.

---

### Task 9: Phase 5 — Jobs (Quartz)

**Files:**
- `Application/Modules/Jobs/{ListJobs,GetJob,TriggerJob,PauseJob,ResumeJob,ListExecutions}/`, `Application/Shared/Providers/IJobProvider.cs`, `Infrastructure/Jobs/{QuartzJobProvider,AuditingJobListener}.cs`, `Web/Modules/JobsEndpoints.cs`, `Web/Hubs/JobsHub.cs`
- Frontend: `jobs/page.tsx`, `jobs/[id]/page.tsx`
- Test: `unit/Jobs/...`, `integration/Jobs/QuartzProviderTests.cs` (in-memory Quartz: schedule, trigger, listener records execution row, pause/resume)

**Interfaces:** `IJobProvider`: `ListAsync→JobInfo{id,name,description,schedule(cron),timezone,status,lastRun,nextRun,duration,successCount,failureCount}`; `TriggerAsync(id)`, `PauseAsync(id)`, `ResumeAsync(id)`; executions persisted by listener → `job_executions`; `job.status` broadcast on `/hubs/jobs`. DevStation job registrations come from `appsettings` `Jobs:` array (name, cron, type key → registered `IJob` impl — seed with `HealthSweepJob` that runs the poller's checks; NO user-defined shell).

- [ ] Steps: TDD → implement → UI (table + detail with Run Now/Pause + execution history) → commit `feat: jobs module with Quartz provider`.

---

### Task 10: Phase 6 — Deployments

**Files:**
- `Application/Modules/Deployments/{ListDeployments,GetDeployment,StartDeployment,CancelDeployment}/`, `Application/Shared/Providers/IDeploymentProvider.cs`, `Infrastructure/Deployments/{MockDeploymentProvider,GitHubActionsDeploymentProvider}.cs`, `Web/Modules/DeploymentsEndpoints.cs`, `Web/Hubs/DeploymentsHub.cs`
- Frontend: `deployments/page.tsx`, `deployments/[id]/page.tsx`, `features/deployments/DeployDialog.tsx`
- Test: `unit/Deployments/MockProviderTests.cs`, `unit/Deployments/DeployPolicyTests.cs` (production env → Deploy policy + confirm), `integration/DeploymentsEndpointTests.cs`

**Interfaces:** `IDeploymentProvider`: `StartAsync(projectId,environmentId,commit,ct)→deploymentId`; `CancelAsync(id)`; `GetStatusAsync(id)→{state,logs[],startedAt,finishedAt,commit,version}`. `MockDeploymentProvider`: in-memory state machine — Queued→(2s)Running→(5s)Succeeded (configurable fail% seed), emits SignalR `deployment.updated` + synthetic log lines into LogBuffer (Task 11 seam — write to `ILogSink` interface created here). Production env deploy requires `Deploy` policy + `confirm:true`. `GitHubActionsDeploymentProvider`: `workflow_dispatch` via HttpClient + run polling; enabled only when integration configured.

- [ ] Steps: TDD → UI (history table, deploy dialog w/ env+commit pickers + prod warning, live progress via hub, logs pane) → commit `feat: deployments module`.

---

### Task 11: Phase 7/8a — Logs module + audit + search

**Files:**
- `Application/Modules/Logs/{GetLogs,StreamLogs}/`, `Application/Shared/ILogSink.cs` + `Infrastructure/Logs/{LogBuffer,HubBroadcaster}.cs`, `Application/Modules/Audit/ListAuditEvents/`, `Application/Modules/Search/SearchAll/`, `Web/Modules/{LogsEndpoints,AuditEndpoints,SearchEndpoints}.cs`, `Web/Hubs/LogsHub.cs`, `Infrastructure/Logging/BufferedLoggerProvider.cs`
- Frontend: `logs/page.tsx` (live tail, filters: source/severity/env/text, pause/resume stream, export → download assembled blob), `audit/page.tsx`
- Test: `unit/Logs/LogBufferTests.cs`, `unit/Audit/RedactionTests.cs` (SecretRef/metadata never leak), `integration/SearchEndpointTests.cs`

**Interfaces:** `ILogSink.Write(LogEntry{Ts,Level,Source,EnvironmentId?,Message,Meta?})`; `LogBuffer` = bounded channel (10k ring) + pub to LogsHub; `BufferedLoggerProvider` feeds `ILogger` output into sink (source=`api`). `GET /api/logs` paginated history from buffer + persisted deployments/jobs/containers sources. `GET /api/search?q=` → grouped `{projects[],environments[],services[],jobs[],deployments[],databases[],integrations[]}` via `ILIKE`/`EF.Functions.ILike` name/description match, top 5 each.

- [ ] Steps: TDD → UI → commit `feat: logs, audit, global search`.

---

### Task 12: Phase 8b — Command palette + notifications

**Files:**
- `Application/Modules/Commands/{ListCommands,ExecuteCommand}/` + `Application/Shared/Commands/{DevStationCommand,CommandRegistry}.cs`, `Application/Modules/Notifications/{ListNotifications,MarkRead}/`, `Infrastructure/Notifications/NotificationService.cs` (raise on: deployment failed, health check unhealthy, job failure, container stopped), `Web/Modules/{CommandsEndpoints,NotificationsEndpoints}.cs`, `Web/Hubs/NotificationsHub.cs`
- Frontend: `components/CommandPalette.tsx`, `components/NotificationsBell.tsx`, palette store slice
- Test: `unit/Commands/CommandRegistryTests.cs` (tier gating: read commands visible to all, execute/deploy filtered by claim), `integration/CommandsEndpointTests.cs`

**Interfaces:** `DevStationCommand{id,title,verb(navigation|query|action),target(route|endpoint),requiredTier,needsConfirm,paramSchema?}`; registry seeds navigation commands (Go to X), entity commands (Restart service {id} → `POST /services/{id}/restart`), deploy commands. `POST /api/commands/{id}/execute` validates tier + `confirm`, delegates to the underlying handler service (NOT raw fetch — call handlers in-process). Palette UI: cmdk-style overlay built by hand (input + list + arrow keys + enter), grouped, ⌘K open/esc close, destructive → ConfirmDialog.

- [ ] Steps: TDD → UI → commit `feat: command palette and notifications`.

---

### Task 13: Phase 9 — Integrations

**Files:**
- `Application/Modules/Integrations/{ListIntegrations,ConnectIntegration,TestIntegration}/`, `Application/Shared/Providers/IIntegration.cs`, `Infrastructure/Integrations/{GitHubIntegration,JiraIntegration,AzureIntegration,CloudflareIntegration}.cs` (Jira/Azure/Cloudflare: config-gated stubs implementing interface + `NOT_CONFIGURED` when creds absent), `Infrastructure/GitHub/{GitHubApiClient}.cs`, `Web/Modules/IntegrationsEndpoints.cs` (+ `/api/integrations/github/{repos,prs,workflow-runs}` routes)
- Frontend: `integrations/page.tsx` (cards w/ status, connect form storing token via secret endpoint), `features/integrations/GitHubPanel.tsx`
- Test: `unit/Integrations/GitHubParsingTests.cs` (recorded JSON fixtures), `integration/IntegrationsEndpointTests.cs` (connect stores SecretRef, GET returns `hasCredentials:true` not token)

**Interfaces:** `IIntegration{Name,Type,Status,Configure(cfg,ct),TestAsync(ct)→bool}`; credentials via `ISecretStore`, `SecretRef` on entity. `GitHubApiClient`: `ListReposAsync`, `ListPullRequestsAsync(owner,repo)`, `ListWorkflowRunsAsync` — HttpClient + `Authorization: Bearer` from secret store, `Accept: application/vnd.github+json`.

- [ ] Steps: TDD → UI → commit `feat: integrations framework + GitHub`.

---

### Task 14: Phase 10 — E2E, observability, docs, polish

**Files:**
- `tests/e2e/{dashboard.spec.ts,projects.spec.ts,databases.spec.ts,palette.spec.ts}` (Playwright, baseURL :3000, token pre-seeded into localStorage via `addInitScript`)
- `Web/Shared/Middleware/RequestTimingMiddleware.cs`, OpenTelemetry packages + `AddOpenTelemetry().WithTracing().WithMetrics()` gated on config flag
- Docs: `README.md`, `AGENTS.md`, `DESIGN.md`, `CONTRIBUTING.md`, `docs/architecture/overview.md`, `docs/api/reference.md`, `docs/integrations/{github,jira,azure,cloudflare}.md`, `docs/decisions/ADR-001..004.md`, update root files
- `.github/workflows/ci.yml` per spec §10

**Interfaces:** Produces CI green; `make dev` one-command boot documented; AGENTS.md encodes all Global Constraints + how to add a module/provider/integration.

- [ ] Steps: write e2e (login token → dashboard → create project → create env → run safe query → palette nav → audit row appears), wire OTel, write all docs, CI yaml, run `make test` full, fix everything, commit `feat: e2e suite, observability, docs, ci`.

---

### Task 15 (stretch): AI seam + MCP read-only tools

Only if Tasks 0–14 complete. `IAiProvider.CompleteAsync(prompt,ct)` + `OllamaProvider` (local `localhost:11434` `/api/generate`); AI endpoints `POST /api/ai/{explain-error,explain-sql,generate-sql}` returning suggestions only — no tool execution. MCP: `GET /api/mcp/tools` listing registry-mapped read-only commands. No write tools.

---

## Self-review notes (done at write time)

- Spec §7 routes all mapped to tasks (logs query on T11, commands T12, integrations T13). `/api/dashboard/summary` added T5 — spec §11 requires it though §7 omits it; consistent.
- `ILogSink` defined T10-consumed, created T10 (mock provider), formalized T11 — noted in both tasks to avoid double-definition: **owner = Task 10 creates the interface, Task 11 implements buffer/hub.**
- `IAuditWriter` first used T5 — owner T5.
- `IDatabaseProvider` referenced by health executor T6 before T8 builds it: executor uses `GetService<IDatabaseProvider>()` null-tolerant; T8 registers it.
- SignalR hubs: health T6, jobs T9, deployments T10, logs T11, notifications T12 — all mapped in Program.cs via `AddInfrastructure` growth per task.
- SQL Server provider: implemented T8, exercised only against configured server (no testcontainer).
