# Contributing

## Setup

```sh
cp .env.example .env && mise install && make setup && make dev
```

See README.md for the feature tour and config reference; AGENTS.md for the
architecture rules (read it — it is the contract).

## Workflow

1. Branch off `main` (or work in a `.worktrees/` worktree).
2. Keep diffs focused; match surrounding style — boring > clever.
3. Before pushing:

```sh
make lint      # dotnet format --verify + eslint
make test      # unit + integration + vitest
make test-e2e  # playwright, when you touched a page flow
```

`dotnet format src/DevStation.sln` + `pnpm --dir apps/web exec prettier
--write .` (`make format`) autofix style.

4. Commits: `feat:`, `fix:`, `chore:` … conventional prefixes, one line.
5. PRs: describe the observable behavior change; paste test output if CI is
   not green yet.

## Tests

- Unit (`tests/unit`, xUnit): handlers, guardrails, provider arg building,
  auth tiers. No I/O.
- Integration (`tests/integration`, xUnit + Testcontainers): endpoints and
  persistence against real Postgres containers. Podman:
  `DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock`.
- Frontend (`apps/web`, vitest + jsdom): stores, api client, components.
- E2E (`tests/e2e`, playwright): page flows against the real stack —
  `scripts/e2e-api.sh` boots a throwaway Postgres on :55432 and the API on
  :5080; `reuseExistingServer` means a running `make dev` is picked up
  instead.

## Rules that will bounce a PR

- Logic in endpoint bodies, or HTTP/EF concerns leaking into Domain.
- Missing confirm gate or audit record on a new destructive action.
- Secret material in DTOs, logs, audit metadata, or committed files.
- New dependencies duplicating stdlib or already-installed packages.
- Frontend pages missing loading/empty/error states, or status shown by
  color alone.
