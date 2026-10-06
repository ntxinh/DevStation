# ADR-002: Provider abstraction for all external capabilities

**Status**: Accepted (2026-10-05)

## Context

Everything DevStation touches outside its own database — git repos, the
container runtime, user databases, the scheduler, deployment targets,
integration APIs, secret storage — is a swappable capability: podman vs
docker, mock vs GitHub Actions, env vars vs a future keyring. Tests also need
to run without any of these hosts present.

## Decision

Each capability is an interface in `Application/Shared/Providers/`
(`IGitProvider`, `IContainerProvider`, `IDatabaseProvider`, `IJobProvider`,
`IDeploymentProvider`, `ISecretStore`, `IIntegration`, `IAiProvider` seam)
with concretes in `Infrastructure/<Area>/`. Selection happens in
`AddInfrastructure`:

- **Auto-detection** where the environment decides —
  `ContainerProviderDetector` probes for podman then docker.
- **Config key** where intent decides — `Deployments:Provider = mock|github`
  (github only wins when its secret exists; otherwise mock stays).
- **Seams not implementations** for capabilities not yet built (`IAiProvider`)
  — the interface + call sites exist, the concrete lands later.

Rules: providers take argument arrays (never `sh -c`), secrets flow through
`ISecretStore`, and handlers depend only on the interface.

## Consequences

- Swapping a concrete is one DI registration — e.g. a future
  `K8sDeploymentProvider` adds a file + a config branch.
- Unit tests fake providers directly; integration tests exercise the real
  ones against Testcontainers.
- Slight indirection cost when there is exactly one concrete (e.g.
  `EnvSecretStore` today) — accepted: the seam documents the swap point and
  keeps secrets out of entities uniformly.
