# FixFlow

A multi-tenant maintenance work-order platform: tenants submit requests → managers triage and assign →
technicians complete the work → managers close it.

> **Status:** early development (MVP, Phase 1). Nothing is runnable yet — see the [MVP plan](docs/plans/mvp.md).

## Stack

.NET 10 (Minimal APIs, EF Core, MediatR, NodaTime) · PostgreSQL · React 18 + TypeScript + Vite + MUI + TanStack Query ·
Azure Blob Storage (Azurite locally) · Docker Compose. Details in [ARCHITECTURE.md](ARCHITECTURE.md).

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — decisions, domain model, conventions, security requirements
- [docs/adr/](docs/adr/) — architecture decision records
- [docs/plans/mvp.md](docs/plans/mvp.md) — MVP development plan

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) LTS
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- [gitleaks](https://github.com/gitleaks/gitleaks#installing) — required by the pre-commit hook

### First-time setup

```bash
git config core.hooksPath .githooks   # enable the secret-scanning pre-commit hook
cp .env.example .env                  # then replace every value in .env
```

Running the infrastructure, API and SPA is documented here as those pieces land (plan M0).

## Contributing

- Conventional commits (`feat(work-orders): ...`), short-lived branches, squash merge.
- No secrets and no real personal data anywhere in the repository — use fake data (`*.example.test`).

## License

[MIT](LICENSE)
