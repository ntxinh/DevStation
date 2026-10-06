# Module map

Each module follows the same shape:

```text
Application/Modules/<Area>/<Feature>/   # handler + request/response records
Application/Modules/<Area>/<Area>Module.cs  # Add<Area>Module() DI extension
Web/Modules/<Area>/<Area>Endpoints.cs   # thin minimal-API map
Infrastructure/<Area>/                  # provider concretes, stores
apps/web/app/(app)/<area>/page.tsx      # route
apps/web/features/<area>/               # components + types
```

| Module | Serves | Key handlers / providers | Persisted |
|---|---|---|---|
| Dashboard | `/api/dashboard/summary` | aggregates projects/services/containers/env-health | — (computed) |
| Projects | CRUD + git status + recent commits | `LibGit2SharpGitProvider` (allowlisted `localPath`, no shell) | `Projects` |
| Environments | CRUD + `/compare?a=&b=` | type dev/qa/prod drives deploy tier + DB write blocking | `Environments` |
| Services | CRUD + per-service health + restart | health via `IHealthCheckExecutor`; restart routes to `IContainerProvider` when `containerId` set | `Services` |
| Containers | list/inspect/logs/stats + start/stop/restart | `PodmanCliProvider` or `DockerCliProvider` via `ContainerProviderDetector`; arg arrays, no `sh -c` | — (runtime-derived) |
| Databases | connections, schema browse, query exec, history, saved queries | `PostgreSqlProvider` (Npgsql), `SqlServerProvider`; `SqlGuard` tiers + readOnly/maxRows/timeout | `DatabaseConnections`, `QueryHistory`, `SavedQueries` |
| Jobs | list, trigger, pause/resume, executions | `QuartzJobProvider` — jobs defined in `Jobs` config section, not user shell | `Jobs`, `JobExecutions` |
| Deployments | start (prod → `deploy` tier), cancel, status | `MockDeploymentProvider` default; `GitHubActionsProvider` when `Deployments:Provider=github` + token | `Deployments` |
| Logs | `/api/logs` filter + export, `/hubs/logs` tail | `LogBuffer` in-memory ring + `BufferedLoggerProvider` | — (memory) |
| Health | check CRUD, `/health/overview`, on-demand check | `HealthCheckPoller` hosted service + `SignalRHealthNotifier` | `HealthChecks`, `HealthCheckResults` |
| Integrations | list, connect/test/disconnect, GitHub repos/PRs/workflow-runs | `GitHubIntegration` + `GitHubApiClient`; `Jira/Azure/CloudflareIntegration` config stubs | `Integrations` |
| Audit | `/api/audit?action=&resource=` | `AuditWriter` (scoped, writes `AuditEvents`) | `AuditEvents` |
| Search | `/api/search?q=` | fans out across modules → grouped hits | — (computed) |
| Commands | `/api/commands` + `/{id}/execute` | `CommandRegistry`: nav commands + entity-derived commands; `needsConfirm` gates | — (computed) |
| Notifications | list, mark read / read-all | `NotificationService` + `/hubs/notifications` | `Notifications` |

## Cross-cutting

- `Shared/Results` — `ApiResult`, `AppException`, envelope.
- `Shared/Security` — `Tiers`, `SqlGuard`.
- `Shared/Providers` — every provider interface (`IGitProvider`,
  `IContainerProvider`, `IDatabaseProvider`, `IJobProvider`,
  `IDeploymentProvider`, `IIntegration`, `ISecretStore`, `IAiProvider` seam).
- `Shared/Commands/CommandRegistry` — the palette's command source; future
  MCP tools reuse the same dispatch.
- `Shared/Auditing`, `Shared/Logging`, `Shared/Persistence` (`IDevStationDb`),
  `Shared/Notifications` — ports consumed by handlers, implemented in
  Infrastructure.
