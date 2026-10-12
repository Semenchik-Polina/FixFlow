# FixFlow

A multi-tenant maintenance work-order platform: tenants submit requests → managers triage and assign →
technicians complete the work → managers close it.

> **Status:** early development (MVP, Phase 1). Only the local infrastructure runs so far — see the [MVP plan](docs/plans/mvp.md).

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

On Windows PowerShell use `Copy-Item .env.example .env`; don't create `.env` with `>` or `Out-File`, which add a
byte-order mark that Docker Compose misreads.

### Infrastructure

```bash
docker compose up -d --wait   # PostgreSQL, Azurite, Mailpit, Seq
docker compose down           # stop; data stays in named volumes
docker compose down -v        # stop and delete all data
```

| Service | Address |
|---|---|
| PostgreSQL 18 | `localhost:5432`, database and app role from `FIXFLOW_DB_*` in `.env` |
| Azurite (Blob) | `http://localhost:10000/devstoreaccount1` |
| Mailpit | SMTP `localhost:1025` · UI http://localhost:8025 |
| Seq | http://localhost:5341, sign in as `admin` with `SEQ_FIRSTRUN_ADMINPASSWORD` |

Every port is bound to `127.0.0.1`. The database init script and Seq's first-run password apply only to empty volumes:
after changing them in `.env`, run `docker compose down -v`. If port 5432 is taken, stop the local PostgreSQL service.
PostgreSQL upgrades are described in [ARCHITECTURE.md §20](ARCHITECTURE.md#20-local-development-and-deployment).

Running the API and SPA is documented here as those pieces land (plan M0).

## Contributing

- Conventional commits (`feat(work-orders): ...`), short-lived branches, squash merge.
- No secrets and no real personal data anywhere in the repository — use fake data (`*.example.test`).

## License

[MIT](LICENSE)
