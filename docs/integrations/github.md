# GitHub integration

Live integration — backed by `GitHubApiClient` (typed `HttpClient`,
`api.github.com`) + `GitHubIntegration`.

## Configure

Connect via `POST /api/integrations/github/connect` (admin tier):

```json
{
  "config": {},
  "secrets": { "token": "ghp_…" }
}
```

Or set the env baseline: `DEVSTATION_SECRET_INTG_GITHUB_TOKEN=ghp_…`.

`token` should be a fine-grained PAT (or classic PAT) with `repo` read access.
`test` (`POST /api/integrations/github/test`) performs a real API call; there
is no fake success — a bad token reports `error`/`notconfigured`.

## What you get

- `GET /api/integrations/github/repos` — repositories the token can see
- `GET /api/integrations/github/{owner}/{repo}/pulls` — open pull requests
- `GET /api/integrations/github/{owner}/{repo}/workflow-runs` — Actions runs

Upstream GitHub failures surface as `502 INTEGRATION_ERROR`.

## Deployments

`Deployments:Provider=github` switches the deployment provider from `mock` to
`GitHubActionsProvider` **only when a `github.token` secret exists** —
otherwise the mock stays on. The provider dispatches/releases via GitHub
Actions workflow runs.

## Notes

- Secrets go through `ISecretStore` (`intg-github-token`); nothing is written
  to `Integrations` config rows in plaintext.
- `disconnect` tombstones the secret for the process lifetime so a stale env
  var does not silently keep auth alive.
