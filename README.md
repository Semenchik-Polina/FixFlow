# FixFlow

A multi-tenant maintenance work-order platform: tenants submit requests → managers triage and assign →
technicians complete the work → managers close it.

> **Status:** early development (MVP, Phase 1). The local infrastructure, an API skeleton (logging, tracing,
> health checks) and a frontend skeleton run so far — see the [MVP plan](docs/plans/mvp.md).

## Stack

.NET 10 (Minimal APIs, EF Core, MediatR, NodaTime) · PostgreSQL · React 19 + TypeScript + Vite + MUI + TanStack Query ·
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

### API

Once, with the infrastructure running:

1. In Seq, create an API key for the API: Settings → API Keys → Add API Key, with the **Ingest** permission only.
2. Store the local secrets (kept in your user profile, outside the repository):

   ```bash
   dotnet user-secrets set "Database:Password" "<FIXFLOW_DB_PASSWORD from .env>" --project src/FixFlow.API
   dotnet user-secrets set "Seq:ApiKey" "<Seq API key>" --project src/FixFlow.API
   ```

   If you changed `FIXFLOW_DB_NAME` or `FIXFLOW_DB_USER` in `.env`, also set `ConnectionStrings:FixFlow`
   (`Host=127.0.0.1;Port=5432;Database=<name>;Username=<user>`, without the password).

Then:

```bash
dotnet run --project src/FixFlow.API --launch-profile http   # http://localhost:5250
```

In Development the API applies pending EF Core migrations on startup. To add a migration, run `dotnet tool restore`
once (it installs the pinned `dotnet-ef`), then the `dotnet ef migrations add` command from [CLAUDE.md](CLAUDE.md).

| Endpoint | Purpose |
|---|---|
| `/health/live` | 200 while the process runs |
| `/health/ready` | 200 when PostgreSQL and Blob Storage are reachable, otherwise 503 |

Logs go to the console as JSON and to Seq; traces and metrics go to Seq over OTLP.

### Frontend

Needs Node.js 24 (`web/.nvmrc`). From `web/`:

```bash
npm ci            # once, and after dependency changes
npm run dev       # http://localhost:5173
```

The dev server proxies `/api` to the API on http://localhost:5250, so the browser sees one origin and the auth cookie
works without CORS. To point it elsewhere, set `FIXFLOW_API_URL`.

The API client in `web/src/api/generated/` is generated from `src/FixFlow.API/openapi.json`, which `dotnet build`
writes. After changing an endpoint or DTO, build the API, run `npm run gen:api` and commit both files. CI fails when
either is stale.

### Tests

```bash
dotnet test FixFlow.slnx
```

The integration tests need Docker running, but not the Compose stack, `.env` or user-secrets: Testcontainers starts
its own PostgreSQL and Azurite once per run, on the image tags from `docker-compose.yml`, and removes them afterwards.

Frontend, from `web/`:

```bash
npm run lint && npm run typecheck && npm test
```

## Contributing

- Conventional commits (`feat(work-orders): ...`), short-lived branches, squash merge.
- No secrets and no real personal data anywhere in the repository — use fake data (`*.example.test`).

## License

[MIT](LICENSE)
