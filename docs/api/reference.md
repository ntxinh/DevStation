# API reference

Base: `http://localhost:5080`. Every endpoint except `GET /health` requires
`Authorization: Bearer <DEVSTATION_TOKEN>` (SignalR hubs accept
`?access_token=` for browser clients).

**Envelope**: all responses are `{ "data": …, "error": null }` or
`{ "data": null, "error": { "code": "…", "message": "…" } }`.

**Tiers**: `read` (whole `/api` group) < `execute` < `deploy` < `admin`.
Destructive endpoints also require `?confirm=true` (or `confirm` in body);
missing confirm → `400 CONFIRMATION_REQUIRED`.

| Method & path | Tier | Notes |
|---|---|---|
| `GET /health` | anon | liveness `{status, uptime}` |
| **Dashboard** |||
| `GET /api/dashboard/summary` | read | `{projects, services, containers, environmentHealth}` |
| **Projects** |||
| `GET /api/projects?q=` | read | name/tag search |
| `POST /api/projects` | execute | `{name, description?, repositoryUrl?, localPath?, defaultBranch?, technology?, tags[]}` |
| `GET /api/projects/{id}` | read | |
| `PATCH /api/projects/{id}` | execute | partial update |
| `DELETE /api/projects/{id}?confirm=true` | execute | confirm-gated |
| `GET /api/projects/{id}/git` | read | branch/commit/dirty/ahead/behind; error when `localPath` missing |
| `GET /api/projects/{id}/commits?n=` | read | recent commits |
| **Environments** |||
| `GET /api/environments` | read | |
| `GET /api/environments/compare?a=&b=` | read | two envs side-by-side |
| `GET /api/environments/{id}` | read | |
| `POST /api/environments` | execute | `{name, type (dev|qa|production…), baseUrl?}` |
| `PATCH /api/environments/{id}` | execute | |
| `DELETE /api/environments/{id}?confirm=true` | execute | confirm-gated |
| **Services** |||
| `GET /api/services?environmentId=` | read | |
| `POST /api/services` | execute | |
| `GET /api/services/{id}/health` | execute | on-demand check |
| `POST /api/services/{id}/restart?confirm=true` | execute | confirm-gated; restarts backing container |
| **Containers** |||
| `GET /api/containers` | read | live podman/docker list |
| `GET /api/containers/{id}` | read | inspect |
| `GET /api/containers/{id}/logs?tail=` | read | |
| `GET /api/containers/{id}/stats` | read | cpu/mem snapshot |
| `POST /api/containers/{id}/start` | execute | |
| `POST /api/containers/{id}/stop?confirm=true` | execute | confirm-gated |
| `POST /api/containers/{id}/restart` | execute | |
| **Databases** |||
| `GET /api/databases/connections` | read | passwords never returned |
| `POST /api/databases/connections` | execute | `{name, kind (postgresql|sqlserver), host, port, database, username, password?, environmentId?, readOnly, maxRows, timeoutSeconds}` |
| `PATCH /api/databases/connections/{id}` | execute | `unlinkEnvironment` clears link |
| `DELETE /api/databases/connections/{id}?confirm=true` | execute | confirm-gated |
| `POST /api/databases/connections/{id}/test` | execute | connectivity probe |
| `GET /api/databases/connections/{id}/schema` | read | schemas → tables → columns/indexes |
| `POST /api/databases/connections/{id}/execute?confirm=` | execute | `{sql}`; read statements run free, writes need confirm, dangerous tier needs `confirm=true`; prod-linked conns block writes; `maxRows`/`timeoutSeconds` enforced |
| `GET /api/databases/history?connectionId=&limit=` | read | |
| `GET /api/databases/saved-queries?connectionId=` | read | |
| `POST /api/databases/saved-queries` | execute | |
| `DELETE /api/databases/saved-queries/{id}` | execute | |
| **Jobs** |||
| `GET /api/jobs` | read | definitions + last execution |
| `GET /api/jobs/{id}` | read | definition + executions |
| `POST /api/jobs/{id}/trigger?confirm=true` | execute | confirm-gated |
| `POST /api/jobs/{id}/pause` / `…/resume` | execute | |
| **Deployments** |||
| `GET /api/deployments?environmentId=&projectId=` | read | |
| `GET /api/deployments/{id}` | read | |
| `POST /api/deployments?environmentId=&confirm=true` | execute/deploy | confirm-gated; `deploy` tier when target env is production |
| `POST /api/deployments/{id}/cancel` | execute | |
| **Logs / Audit / Search** |||
| `GET /api/logs?source=&severity=&q=` | read | buffered app log |
| `GET /api/logs/export?source=&severity=&q=` | read | text export |
| `GET /api/audit?action=&resource=` | read | filterable event list |
| `GET /api/search?q=` | read | grouped hits across modules |
| **Commands (⌘K palette)** |||
| `GET /api/commands` | read | server-defined command list with `verb`/`requiredTier`/`needsConfirm` |
| `POST /api/commands/{id}/execute` | read | `{confirm}`; per-command tier enforced server-side |
| **Notifications** |||
| `GET /api/notifications?unread=&take=` | read | |
| `POST /api/notifications/{id}/read` | read | |
| `POST /api/notifications/read-all` | read | |
| **Health checks** |||
| `GET /api/health/overview` | read | checks + latest results |
| `POST /api/health` | execute | create check |
| `POST /api/health/{id}/check` | execute | run now |
| `DELETE /api/health/{id}?confirm=true` | execute | confirm-gated |
| **Integrations** |||
| `GET /api/integrations` | read | per-integration status/config-key info |
| `POST /api/integrations/{type}/connect` | admin | `{type}` ∈ `github|jira|azure|cloudflare`; config + secrets |
| `POST /api/integrations/{type}/test` | admin | connectivity test |
| `POST /api/integrations/{type}/disconnect` | admin | clears secrets (tombstoned) |
| `GET /api/integrations/github/repos` | read | requires configured GitHub token; upstream errors → `502 INTEGRATION_ERROR` |
| `GET /api/integrations/github/{owner}/{repo}/pulls` | read | |
| `GET /api/integrations/github/{owner}/{repo}/workflow-runs` | read | |

## SignalR hubs

`/hubs/health`, `/hubs/jobs`, `/hubs/deployments`, `/hubs/logs`,
`/hubs/notifications` — Bearer auth (query `access_token`), `read` tier.

## Error codes

`VALIDATION_ERROR` 400 · `CONFIRMATION_REQUIRED` 400 · `UNAUTHORIZED` 401 ·
`FORBIDDEN`/`HTTP_403` 403 · `NOT_FOUND` 404 · `INTEGRATION_ERROR` 502 ·
`HTTP_<n>` fallback.
