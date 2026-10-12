# FixFlow — Agent Guide

Guidance for AI coding agents working on **FixFlow**: a multi-tenant maintenance work-order platform
(tenants submit requests → managers assign → technicians complete).

Terms: **organization** = the SaaS customer and data boundary; **tenant** = the renter role only.
"Tenant isolation" / "cross-tenant" are the technical terms for isolation between organizations.

Stack: .NET 10 (Minimal APIs, EF Core, PostgreSQL, MediatR, NodaTime) · React 18 + TS + Vite + MUI + TanStack Query ·
Redis (cache + SignalR backplane) · RabbitMQ/MassTransit (Phase 2) · Azure Blob (Azurite locally) · Docker Compose.

> This file is loaded into every session — keep it short. Details live in `ARCHITECTURE.md`;
> read only the section you need (section numbers below), not the whole file.

---

## 1. Where to find context

| Need | Read |
|---|---|
| Decisions & rationale | `ARCHITECTURE.md` §3 (decision table D1–D29), `docs/adr/` |
| Scope (what's MVP vs later) | `ARCHITECTURE.md` §2 — **don't build Phase 2/3 features unless asked** |
| Entities & status lifecycles | `ARCHITECTURE.md` §7 |
| Tenancy, roles, authorization | `ARCHITECTURE.md` §8 |
| Persistence / time conventions | `ARCHITECTURE.md` §10, §11 |
| Messaging / Redis / files | `ARCHITECTURE.md` §12, §13, §14 |
| API / frontend conventions | `ARCHITECTURE.md` §15, §16 |
| Security requirements (S1–S26) | `ARCHITECTURE.md` §17 |
| Area-specific rules | `src/CLAUDE.md` (backend), `web/CLAUDE.md` (frontend) — loaded automatically when working in that folder |
| Current multi-step work | `docs/plans/*.md` |

---

## 2. Commands

```bash
# Infrastructure (Postgres, Azurite, Mailpit, Seq; Redis/RabbitMQ in Phase 2)
docker compose up -d

# Backend
dotnet build FixFlow.slnx
dotnet test FixFlow.slnx
dotnet test tests/FixFlow.Domain.Tests --filter "FullyQualifiedName~WorkOrder"   # prefer focused runs
dotnet run --project src/FixFlow.API

# Migrations
dotnet ef migrations add <Name> --project src/FixFlow.Infrastructure --startup-project src/FixFlow.API

# Frontend (from web/)
npm run dev
npm run lint && npm run typecheck && npm test
npm run gen:api          # regenerate API client after any endpoint/DTO change
npx playwright test      # E2E, needs `docker compose --profile full up`
```

Local UIs: Mailpit http://localhost:8025 · Seq http://localhost:5341 · RabbitMQ http://localhost:15672

---

## 3. Non-negotiable rules

Breaking these is a bug even if tests pass.

1. **Tenant isolation.** Every organization-owned entity has `OrganizationId` + global query filter.
   `OrganizationId` and roles come **only** from `ICurrentUser` — resolved per request from the `X-Organization`
   header and validated against the user's active membership — never from request bodies or route values.
   A user can belong to several organizations with different roles in each (`ARCHITECTURE.md` §8).
   Never use `IgnoreQueryFilters()` without an explicit comment justifying it.
2. **Authorization on every endpoint.** Role policy on the endpoint **and** resource-based check on the record
   (`WorkOrderOps.*` etc.). Lists use `.VisibleTo(user)`. Unauthorized record → **404**, not 403.
3. **Status changes only through the domain state machine** (`WorkOrder`, `MaintenanceRequest`).
   No direct assignment of `Status` from handlers.
4. **Time:** NodaTime `Instant` for moments (UTC `timestamptz`); local time + property IANA zone for wall-clock
   intentions. Inject `IClock` — **never** `DateTime.Now/UtcNow` in Domain/Application.
5. **IDs:** UUIDv7 (`Guid.CreateVersion7()`).
6. **Tenant-facing DTOs** never include internal notes/comments.
7. **Public repo: no secrets and no real personal data** in code, config, seeds, tests or commits. Local secrets via `dotnet user-secrets` / untracked `.env`.
8. **No tokens in `localStorage`.** Auth is cookie-based.
9. **Redis is only for `HybridCache` and the SignalR backplane.** Any other use needs an ADR.
10. **Never edit generated files** (`web/src/api/generated/**`) or migrations that are already merged — add new ones.

---

## 4. Code conventions

**Backend** (details: `src/CLAUDE.md`)
- Clean Architecture: `Api → Application → Domain`; `Infrastructure` implements `Application` interfaces.
  Domain has no framework dependencies (NodaTime allowed).
- One feature = one folder per module: command/query + handler + validator + endpoint mapping.
- Handlers use `DbContext` directly (no generic repositories). Reads: `AsNoTracking` + projections + pagination.
- Validation with FluentValidation; errors as Problem Details.
- Nullable enabled, warnings as errors. Follow `.editorconfig`.

**Frontend** (details: `web/CLAUDE.md`)
- Feature folders: `web/src/features/<feature>/`. Shared UI in `web/src/shared/`.
- Server state = TanStack Query (generated hooks); UI state = `useState`/context. No Redux/Zustand for API data.
- MUI components and theme tokens — no ad-hoc CSS colors.
- Forms: React Hook Form + Zod. Dates formatted in the **property's** time zone.

**Naming & style:** match surrounding code; don't introduce new libraries or patterns without asking.

---

## 5. Workflow for agents

1. **Clarify scope first.** If a request touches a Phase 2/3 feature, a non-negotiable rule, or a decision in
   `ARCHITECTURE.md` §3 — stop and ask before implementing.
2. **Work in vertical slices:** domain → persistence → endpoint → tests → `gen:api` → UI. One slice per task.
3. **Tests are part of the change:**
   - state transitions × roles → domain tests
   - new endpoint → integration test incl. a **cross-tenant access test**
   - time logic → tests with `FakeClock`, incl. DST/weekend cases
4. **Verify before reporting done:** build + relevant tests + lint/typecheck for touched areas. Report failures honestly.
5. **Keep docs in sync:** a changed decision → new ADR in `docs/adr/` + update the relevant `ARCHITECTURE.md` row.
   Don't update docs for routine code changes.
6. **Commits:** conventional commits (`feat(work-orders): ...`); small and focused. Ask before commit. Don't commit/push unless asked.

---

## 6. Context management

Keep the context window for the task, not for exploration noise.

- **Search, then read targeted ranges.** Grep for symbols; open the specific file/lines. Don't read whole
  directories or `ARCHITECTURE.md` end-to-end.
- **Never read** (large, generated or irrelevant):
  `**/bin/**`, `**/obj/**`, `node_modules/`, `web/dist/`, `web/src/api/generated/**`,
  `**/Migrations/*.Designer.cs`, `**/Migrations/*ModelSnapshot.cs`, `package-lock.json`, `*.min.*`, `TestResults/`.
- **Run focused commands.** Filter tests (`--filter`, `vitest <path>`), and trim noisy output
  (e.g. `dotnet build -v q`, show only failures). Don't paste full logs into the conversation.
- **Delegate broad exploration** (e.g. "where is X used across the solution?") to a sub-agent and keep only the
  conclusion.
- **Multi-session or large tasks:** create/update `docs/plans/<task>.md` with goal, checklist and decisions made,
  so work can resume after a context reset without re-deriving it. Don't delete the plan when the task is merged.
- **Don't re-derive settled decisions** — they are in `ARCHITECTURE.md` §3 and `docs/adr/`.
- **Keep this file lean.** Area-specific detail belongs in `src/CLAUDE.md` / `web/CLAUDE.md`;
  long explanations belong in `ARCHITECTURE.md` or ADRs.

---

## 7. Definition of done

- [ ] Builds with no warnings; relevant tests pass (new tests added for new behavior)
- [ ] Non-negotiable rules (§3) respected — especially tenant isolation and authorization
- [ ] API client regenerated if endpoints/DTOs changed; frontend lint + typecheck pass
- [ ] Migration added (not edited) if the schema changed
- [ ] No secrets, no personal data in logs
- [ ] ADR/`ARCHITECTURE.md` updated only if a decision changed
