# ADR-003: Dev-token auth with tiered policies

**Status**: Accepted (2026-10-05)

## Context

DevStation is self-hosted, single-tenant, run on a developer's own machine or
a small team's box. Full OIDC/OAuth buys user management nobody asked for in
v1 and drags in identity plumbing (client config, token validation, refresh)
before the product's core loop is proven.

## Decision

Authentication = shared Bearer token.

- `DevStation:Token` / `DEVSTATION_TOKEN` is the admin token; the web UI's
  `TokenGate` stores it in localStorage and `apiFetch` sends it on every
  request (SignalR via `access_token` query param).
- Authorization = **tiers**, `read < execute < deploy < admin`, as ASP.NET
  policies (`TierRequirement`/`TierHandler`); the whole `/api` group requires
  `read`, mutations escalate.
- Extra tokens with lower tiers come from
  `DevStation:TokenTiers:<token> = <tier>` — a read-only dashboard token is a
  config entry, not a user record.
- The token grant is claims-based (`Claim("tier", …)`), so feature code never
  sees the scheme — swapping in OIDC later means replacing the authentication
  handler, not the authorization model. That seam is deliberate.

## Consequences

- Zero-login onboarding: paste a token once.
- Anyone with the admin token can do anything — acceptable for the stated
  scope; do **not** expose the API beyond loopback/trusted networks.
- CORS is restricted to loopback origins; auth is still enforced per request.
- Real users/RBAC → swap `DevTokenAuthenticationHandler` for OIDC; policies,
  tiers, confirm gates, and audit all carry over unchanged.
