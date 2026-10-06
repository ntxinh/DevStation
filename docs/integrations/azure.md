# Azure integration

Config + credential plumbing only for now. Uses **client credentials**
(service principal) — not Azure DevOps PATs.

## Configure

`POST /api/integrations/azure/connect` (admin):

```json
{
  "config": { "subscriptionId": "<sub-guid>" },
  "secrets": {
    "tenantId": "<tenant-guid>",
    "clientId": "<app-registration-id>",
    "clientSecret": "<secret>"
  }
}
```

Env baseline (normalized): `DEVSTATION_SECRET_INTG_AZURE_TENANTID`,
`…_CLIENTID`, `…_CLIENTSECRET`.

## Test

`POST /api/integrations/azure/test` acquires a token from
`login.microsoftonline.com/{tenantId}` for the `management.azure.com` scope
and lists subscriptions — a real ARM round-trip. All three secrets must be
present or it reports `notconfigured`.

## Status

Stub-level: credentials + verification are real; no Azure resources are
surfaced in the UI yet.
