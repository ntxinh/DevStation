# Cloudflare integration

Config + credential plumbing only for now.

## Configure

`POST /api/integrations/cloudflare/connect` (admin):

```json
{
  "config": { "accountId": "<account-id>" },
  "secrets": { "token": "<api-token>" }
}
```

`token` is a Cloudflare **API token** (not the global API key). Env baseline:
`DEVSTATION_SECRET_INTG_CLOUDFLARE_TOKEN`.

## Test

`POST /api/integrations/cloudflare/test` →
`GET https://api.cloudflare.com/client/v4/user/tokens/verify` — the canonical
least-privilege probe; it verifies the token without needing account-scope
permissions.

## Status

Stub-level: credential storage + verification are real; no zones/workers are
surfaced in the UI yet.
