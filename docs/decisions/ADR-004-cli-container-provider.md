# ADR-004: CLI-based container provider (no socket SDK)

**Status**: Accepted (2026-10-05)

## Context

The host runs **podman** (no docker daemon). Options for talking to
containers: a socket SDK (Docker.DotNet-style, works against podman's
compat socket but adds a versioned REST contract to maintain), or exec'ing
the CLI that's already installed and version-locked to the daemon.

## Decision

`IContainerProvider` is implemented by `PodmanCliProvider` /
`DockerCliProvider` — thin wrappers that run `podman|docker` via `Process`
with **argument arrays** (never `sh -c`, never interpolated shell strings).
`ContainerProviderDetector` picks whichever binary exists at startup
(podman first).

Why CLI over SDK:

- The CLI is already the compatibility surface podman maintains for docker.
- No socket path negotiation (`DOCKER_HOST`, rootless vs rootful
  `$XDG_RUNTIME_DIR` sockets) in the app — the CLI resolves it.
- Human-debuggable: the exact command the app ran can be pasted into a shell.

## Consequences

- Slightly slower (process spawn per call) — fine at control-plane volume.
- Output parsing must track CLI versions; `--format json` output is used
  where available to keep that stable.
- Container controls go through the same `Execute` tier + confirm gates as
  everything else — the provider never grants a path around them.
- If latency ever matters, a socket-based concrete slots into the same
  `IContainerProvider` seam without touching consumers.
