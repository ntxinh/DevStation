# ADR-001: Modular monolith over microservices

**Status**: Accepted (2026-10-05)
**Deciders**: project design spec

## Context

DevStation is a single-developer/small-team control plane. A microservice
fleet would buy independent scaling we don't need and pay for it with
deployment topology, distributed debugging, and cross-service versioning —
the exact operational toil the product exists to reduce.

## Decision

One deployable API (`DevStation.Web`) + one web app + Postgres. Internally,
code is sliced **by module** (`Modules/<Area>/<Feature>/`) across four
projects with strict inward dependencies:

```
Domain ← Application ← Infrastructure ← Web
```

- Handlers are plain DI services (no MediatR); one folder per feature.
- Modules compose via `Add<Area>Module()` + `<Area>Endpoints.Map(api)` in
  `Program.cs` — adding/removing a module is a two-line change.
- Shared contracts (`Shared/Providers`, `Shared/Results`, `Shared/Security`)
  keep modules from talking to each other's internals.

## Consequences

- Single binary, single `dotnet run`, trivial local dev and CI.
- Cross-module queries (dashboard summary, search) are joins/method calls,
  not network hops.
- If a module ever needs to split out, its provider seams + envelope API are
  already the extraction boundary.
- The monolith can grow hot spots; module discipline (no cross-module
  `using`s into other feature folders) is enforced by review, not the
  compiler — AGENTS.md documents it.
