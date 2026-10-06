# MCP tool surface

DevStation exposes a **read-only, MCP-style tool catalog over HTTP** — this
is *not* a stdio MCP server. A real stdio MCP host is a separate deployment
concern (a thin adapter process can wrap these two endpoints); the API
itself only serves tool descriptors and in-process invocation.

Both endpoints live under the standard `/api` group: `read` tier +
`Authorization: Bearer <token>`, normal `{data, error}` envelope.

## `GET /api/mcp/tools`

Returns a JSON array of `{name, description, inputSchema}` descriptors:

- Every `CommandRegistry` command with verb `navigation`/`query` at `read`
  tier — command ids normalized (`:`/`.` → `_`), e.g. `nav.dashboard` →
  `nav_dashboard`. Invoking a navigation tool returns `{route}` for the
  caller to open.
- The explicit read tools below, each dispatching to the same scoped handler
  the corresponding REST endpoint uses (in-process, no HTTP hop):

| Tool | Args | Routes to |
|---|---|---|
| `list_projects` | `q?` | `ListProjectsHandler` |
| `get_project` | `id` | `GetProjectHandler` |
| `get_git_status` | `id` | `GetGitStatusHandler` |
| `list_environments` | — | `ListEnvironmentsHandler` |
| `get_environment_health` | `id` | `GetEnvironmentHandler` |
| `list_services` | `environmentId?` | `ListServicesHandler` |
| `list_containers` | — | `ListContainersHandler` |
| `list_jobs` | — | `ListJobsHandler` |
| `list_deployments` | `environmentId? projectId? take?` | `ListDeploymentsHandler` |
| `get_logs` | `source? severity? q? take?` | `GetLogsHandler` |
| `run_health_check` | `id` | `RunHealthCheckHandler` — **execute tier required** |

`inputSchema` is a JSON-Schema-ish `{type:"object", properties, required}`.

**No write tools, ever.** Action-tier commands (`*.restart`, `job.trigger`,
`deploy:*`) are filtered out of the catalog and rejected by the invoker —
calling `POST /api/mcp/tools/service_restart_<id>/invoke` returns
`404 MCP_TOOL_UNKNOWN`.

## `POST /api/mcp/tools/{name}/invoke`

Body: `{ "args": { … } }`. Response: the handler's DTO inside `data`, or the
usual error envelope (`MCP_TOOL_UNKNOWN` 404 for unknown/write-ish names,
`VALIDATION_ERROR` 400 for malformed args).

`run_health_check` is the one exception to read-only: it persists a
`health_check_results` row (same as `POST /api/health/{id}/check`), so the
invoker requires the **execute** tier for that tool name — a read-tier
token gets an enveloped `403 FORBIDDEN`.
