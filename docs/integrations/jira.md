# Jira integration

Config + credential plumbing only for now (no issue board UI yet) — but
`test` performs a real API call, so a connected Jira is genuinely verified.

## Configure

`POST /api/integrations/jira/connect` (admin):

```json
{
  "config": {
    "baseUrl": "https://your-org.atlassian.net",
    "email": "you@example.com"
  },
  "secrets": { "token": "<api-token>" }
}
```

- `baseUrl` — **required**, must be `http(s)`.
- `email` — optional. When set, `test` uses Basic auth `email:token`
  (Jira Cloud). Without `email`, Bearer auth is used (Data Center PATs).

Env baseline: `DEVSTATION_SECRET_INTG_JIRA_TOKEN`.

## Test

`POST /api/integrations/jira/test` → `GET {baseUrl}/rest/api/2/myself`. A
non-2xx (or unreachable host) reports `error`, missing credentials report
`notconfigured` — no fake success.

## Status

Stub-level: connection status, credential storage, and verification are real;
no Jira-specific resources are surfaced in the UI yet.
