# AGENTS.md — rules for anyone (human or agent) working on DevStation

Read this before touching code. It encodes the architecture's invariants.

## Architecture invariants

1. **Four projects, module-sliced.** `Domain` (entities/VOs/enums, no deps) ←
   `Application` (feature slices in `Modules/<Area>/<Feature>/`, provider
   interfaces in `Shared/Providers/`) ← `Infrastructure` (EF Core, provider
   concretes, secrets, auditing) ← `Web` (thin endpoints in
   `Modules/<Area>Endpoints.cs`, auth, middleware, hubs). Dependencies only
   point inward. Domain never references EF, HTTP, or DI.
2. **No MediatR.** Handlers are plain DI services: `XxxHandler.Handle(...)`.
   One feature = one folder = one handler + request/response records.
3. **Endpoints stay thin**: bind → call handler → `ApiResult.Ok(...)`. No logic.
4. **Envelope everywhere**: every API response is `{ data, error }` where
   `error = { code, message }`. Throw `AppException(code, status, message)`;
   `ApiExceptionMiddleware` renders it. Never leak stack traces.
5. **Auth**: Bearer dev token; tiers `read < execute < deploy < admin`
   (`Tiers.Rank`). Every `/api/*` endpoint sits on the `Read`-gated module
   group; mutations add `.RequireAuthorization("Execute")` (or
   `"Deploy"`/`"Admin"`). SignalR hubs require `Read`.
6. **Confirm + audit**: destructive actions require `?confirm=true` (or a
   confirm flag) enforced by `ConfirmGate.Require`; every action writes an
   `AuditEvent` via `IAuditWriter` — action names are `area.verb`
   (`project.create`, `container.restart`, `database.query`).
7. **No shell execution.** Containers/git/integrations go through typed
   providers with argument arrays — never `sh -c`, never string-concatenated
   commands.
8. **Secrets**: entities store `SecretRef`, never values. All secret reads go
   through `ISecretStore`; API DTOs never carry secret material.
9. **UTC everywhere**: `DateTimeOffset`, conversion only at presentation.
   `IDevStationDb` is the persistence port; handlers never see `DbContext`.

## Provider pattern

Interfaces live in `Application/Shared/Providers/`, concretes in
`Infrastructure/<Area>/`. Concrete choice happens in
`DependencyInjection.AddInfrastructure` — container provider auto-detects
podman vs docker (`ContainerProviderDetector`), deployment provider picks
`github` only when configured + token present, else `mock`.

## Forbidden

- New deps for what stdlib/installed packages already do.
- MediatR, AutoMapper, FluentValidation (except where spec'd), Swagger/NSwag.
- `System.Net.Http` raw `HttpClient` construction — use `IHttpClientFactory`.
- Reflection-based dispatch, service locators, `IServiceProvider` in handlers.
- Secrets in logs, audit metadata, DTOs, or git.
- Business logic in `DevStation.Web` endpoint bodies.
- Color-only status indication, missing loading/empty/error states (frontend).

## How to add a module

1. `Domain`: entity + enums if new persistence is needed; EF config in
   `Infrastructure/Persistence`, `dotnet ef migrations add`.
2. `Application/Modules/<Area>/<Feature>/`: handler + request/response records.
   `Modules/<Area>/<Area>Module.cs` extension `Add<Area>Module()` registering
   handlers (scoped).
3. `Web/Modules/<Area>/<Area>Endpoints.cs`: map on the `/api` group; add
   `.RequireAuthorization("Execute")` on mutations, `ConfirmGate` for
   destructive ones, `IAuditWriter` record in the handler.
4. `Program.cs`: add `.Add<Area>Module()` and `<Area>Endpoints.Map(api)`.
5. Web: page under `app/(app)/<area>/page.tsx`, feature components in
   `features/<area>/`, nav entry in `components/Nav.tsx` (spec order).
6. Commands/search if the entity is palette-reachable
   (`Shared/Commands/CommandRegistry.cs`, `SearchAllHandler`).

## How to add a provider concrete

Implement the `I<Area>Provider` interface in `Infrastructure/<Area>/`, then
select it in `AddInfrastructure` — by config key (`Deployments:Provider`) or
auto-detection (`ContainerProviderDetector`). Keep the interface in
Application free of implementation concerns.

## How to add an integration

Subclass `IntegrationBase` in `Infrastructure/Integrations/`: declare
`configKeys`/`secretKeys`, implement `TestAsync`. Secrets read as
`intg-<type>-<key>` via `ISecretStore`; `EnvSecretStore` maps key → env
`DEVSTATION_SECRET_<KEY>` (uppercased, non-alnum → `_`), e.g.
`intg-github-token` → `DEVSTATION_SECRET_INTG_GITHUB_TOKEN`.
Register as singleton + `IIntegration` in `AddInfrastructure`.
Endpoints under `/api/integrations/{type}` are `Admin`-gated.

## Frontend rules

- Server state → TanStack Query (`apiFetch` in `lib/api.ts` adds the Bearer
  token from localStorage). UI state → Zustand (`stores/`). Forms → RHF + zod.
- Dark theme only; design tokens in `globals.css` (`bg`, `panel`, `border`,
  `fg`, `muted`, `accent`, `ok`, `warn`, `err`) — see DESIGN.md.
- Statuses render through `StatusBadge` (dot + text, never color alone).
- Every query-bearing page handles loading, empty (`EmptyState`), and error
  (`ErrorState`) states. Focus rings are mandatory.

## Testing

- `make test` — xUnit unit + Testcontainers integration + vitest.
- `make test-e2e` — Playwright (`tests/e2e`, config at
  `apps/web/playwright.config.ts`; `scripts/e2e-api.sh` boots a throwaway
  postgres on :55432 + the API).
- New handler → unit test. New endpoint → integration test where feasible.
  New page flow → consider an e2e spec.

## Naming

- Handlers: `<Verb><Noun>Handler` in `Modules/<Area>/<Verb><Noun>/`.
- Audit actions: `<area>.<verb>` lowercase (`dbconnection.test`).
- Routes: kebab-free resource names (`/api/databases/connections/{id}/execute`).
- React components: PascalCase files; pages under `app/(app)/`.
