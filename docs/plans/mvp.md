# FixFlow MVP — Development plan

**Status:** approved 2026-10-09 · **Next task:** T1.4

## Context

When this plan was written (2026-10-09), FixFlow was greenfield: the repo held only `ARCHITECTURE.md`, three `CLAUDE.md`
files and ADR 0001, with no code.
This plan breaks Phase 1 (ARCHITECTURE §2) into milestones made of vertical slices. Each task has its own definition of done (DoD), so a
solo developer (~4 h/day, no target date) can work through it one task at a time and pick it up again after a context reset.

The MVP is done when the critical lifecycle runs locally on Docker Compose and E2E tests cover it on every PR. The lifecycle is: tenant submits → manager accepts & assigns
→ technician completes → manager closes → tenant sees Done.

How to use it: take the first unchecked task. One task = one branch/PR. Tick it here when its DoD holds, and record any new
decision in the tables below.

---

## Decisions taken while planning (2026-10-09)

| # | Decision | Recorded by |
|---|---|---|
| P1 | **Triage in one step.** Managers accept or reject straight from Submitted or UnderReview. Accept takes a priority, an optional due date and an optional technician, and creates the work order in the same transaction, so an Accepted request always has an active WO. "Start review" (→ UnderReview) is optional. The system also sends a request back to UnderReview when its WO is cancelled. | ADR 0002 (T0.1) |
| P2 | **Tenant-facing status:** Received (Submitted, UnderReview, Accepted + WO New) → Technician assigned (Assigned) → In progress (InProgress, OnHold, Completed) → Done (request Closed), plus Rejected and Cancelled. "Scheduled" comes back with scheduling in Phase 2. Tenants get email for **request rejected** and **request closed** only; nothing is sent at Completed. | ADR 0002 |
| P3 | **Email confirmation (S4) is deferred to Phase 2.** Signup logs the user straight in. Accepting an invite marks the email as confirmed. Accepted risk while the app runs locally only. | ADR 0003 |
| P4 | **MediatR is pinned to 12.x** (the last Apache-2.0 line). Dependabot ignores ≥ 13. | ADR 0004 |
| P5 | **PostgreSQL 18** (pinned to `18.6-trixie`) instead of 16, for support until Nov 2030. Patch updates come through Dependabot; major upgrades are deliberate and follow a documented procedure. Decided 2026-10-11, before T0.3. | ARCHITECTURE §20 |
| P6 | **Testcontainers reads its image tags from `docker-compose.yml`** (copied to the test output), so the tests can't drift from Compose and a Dependabot image bump runs the integration tests on its own PR. Decided 2026-10-13, in T0.7. | ARCHITECTURE §20 |
| P7 | **`IEmailSender` + `EmailMessage` are defined in Application in T0.7**, without an implementation, so the test factory can swap in the in-memory sink. T1.4 adds the MailKit sender and templates behind the same interface. Decided 2026-10-13. | this plan (T0.7, T1.4) |
| P8 | **Endpoint modules are registered in DI.** A module implements `IEndpointModule` (prefix + `Map(group)`) and is registered with `AddEndpointModule<T>()`; `MapFixFlowApi()` maps each one under `/api/v1` in its own group. Tests register test-only modules the same way, so they run through the real pipeline. Decided 2026-10-14, in T0.8. | `src/CLAUDE.md` §2 |
| P9 | **The OpenAPI document is committed** as `src/FixFlow.API/openapi.json`, written by `dotnet build`. PR diffs show contract changes (the generated client's diffs are hidden), CI fails when it is stale, and `gen:api` (T0.10) reads it without building the backend. Decided 2026-10-14. | this plan (T0.8, T0.10) |
| P10 | **The authentication scheme comes in T0.8; unknown routes need a session.** The fallback policy needs a challenge scheme, so T0.8 registers the Identity cookie schemes (`AddIdentityCookies`, 401/403 instead of redirects). T1.2 builds on them with `AddIdentityCore`, not `AddIdentity` (which would register them again). The fallback policy also covers unmatched routes: anonymous → 401, signed in → 404. Decided 2026-10-14. | this plan (T0.8, T1.2, T0.11) |
| P11 | **Concurrency conflicts surface as `ConcurrencyConflictException`** (Application → 409). T0.9's DbContext translates EF Core's `DbUpdateConcurrencyException` into it, so the API maps only Domain and Application exceptions. Decided 2026-10-14. Users are the exception (T1.2): a conflict on users only passes through as EF Core's exception, the only one Identity's store turns into `IdentityResult` `ConcurrencyFailure`. | this plan (T0.8, T0.9) |
| P12 | **Domain event handlers: `IInTransactionEventHandler<TEvent>` and `IPostCommitEventHandler<TEvent>`** (Application), the names ADR 0005 left to T0.9. A small in-house `DomainEventDispatcher` calls them, not MediatR notifications: Domain can't reference MediatR, and MediatR's publisher stops at the first failing handler, while each post-commit handler must run and fail on its own. `AddDomainEventHandlers(assembly)` registers them. Decided 2026-10-18, in T0.9. | `src/CLAUDE.md` §3 |
| P13 | **Handlers reach the DbContext through `IFixFlowDbContext`** (Application), because Application can't reference Infrastructure. Application references `Microsoft.EntityFrameworkCore` for it and for `ToPageAsync`. Each slice adds its `DbSet`s to the interface. Decided 2026-10-18, in T0.9. | `src/CLAUDE.md` §4 |
| P14 | **`ICurrentUser` arrives minimal in T0.9** (`UserId`, `OrganizationId`), because persistence needs it. `HttpCurrentUser` reads them from the claims; nothing adds the organization claim (`FixFlowClaimTypes.OrganizationId`) until T1.5, so until then no request has an organization context and organization-owned data is neither readable nor writable. T1.5 adds the roles, the claims transformation and the middleware. Decided 2026-10-18. | this plan (T0.9, T1.5) |
| P15 | **React 19 instead of React 18.** React 18 has had no release since 18.3.1 (April 2024), while every React 19 line still gets patch releases, and React Router 8 needs React ≥ 19.2.7. With no frontend code yet, switching cost nothing. Decided 2026-10-18, before T0.10. | ARCHITECTURE §5, §16 |
| P16 | **The OpenAPI document stays 3.1; the API reads numbers strictly.** A spike in T0.10 showed that orval 8 handles 3.1: nullables become `\| null` (nullable references too), enums become string unions, dates become strings, and its MSW 3 mocks compile and run. The spike also showed that the web JSON default (numbers may be sent as strings) makes every number `integer \| string` in the document and `number \| string` in the client, so the API sets `JsonNumberHandling.Strict`; a document test guards it. The generated files' header omits the orval version, so an orval update alone doesn't make the client stale. TypeScript stays on 6.0 until typescript-eslint supports 7 (Dependabot ignores TypeScript majors). Decided 2026-10-19, in T0.10. | this plan (T0.10) |
| P17 | **`dev` is the integration branch; `main` gets milestones.** Feature PRs are squash-merged into `dev`; a bigger branch whose commits are worth keeping may use a merge commit instead. `dev` merges into `main` at milestones (e.g. the end of T0) with a merge commit, never squash or rebase, so `main` keeps `dev`'s commits and the branches don't diverge. Dependabot version updates target `dev`. Decided 2026-10-20, after T0.10. | ADR 0006 |
| P18 | **The `full` profile runs the API as Production; a one-shot `migrate` service applies migrations.** The API image is `aspnet:10.0-noble` plus curl for the Compose healthcheck on `/health/ready`, so `--wait` means ready (a chiseled image would need a probe built into the API). Both images run as the base image's non-root `app` user (uid 1654). Migrations come from an EF Core bundle (the Dockerfile's `migrate` target) that `api` waits for, so auto-migration stays Development-only (T0.9). Building the bundle runs `Program` as Development, which requires a database password: the Dockerfile passes a placeholder on that one step. Decided 2026-10-20, in T0.11. | ARCHITECTURE §20 |
| P19 | **HTTPS in the `full` profile uses the host's ASP.NET Core dev certificate; Azurite stays HTTP.** `scripts/dev-cert.ps1` / `.sh` trust it and export it to the untracked `certs/` as PEM without a password, so `.env` holds no cert secret; the API serves https://localhost:8443 with it. Browser-facing SAS URLs use `Blob:PublicEndpoint` = `http://127.0.0.1:10000/devstoreaccount1`. Loopback is a potentially trustworthy origin, so an https page may call it without mixed-content blocking (Chromium, Firefox). It is also the host that `UseDevelopmentStorage=true` gives on the host, so dev and `full` need one Azurite CORS entry (T4.2) and one CSP entry (T9.1). The API reaches Azurite as `UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite`, so no account key is written down. Decided 2026-10-20, in T0.11. | this plan (T0.11, T4.2, T9.1) |
| P20 | **Failed post-commit handlers are logged with types and stack traces, but no exception messages.** Messages can hold personal data, and log masking doesn't reach exceptions. An exception that implements `ILogSafeMessage` keeps its message; a few well-known exceptions keep safe fields (socket error code, HTTP status, SQL state). Development logs exceptions as thrown. Decided 2026-10-22, after the review of `dev`. | ADR 0007 (T1.4) |
| P21 | **Slugs: 3–40 characters, `a-z`, `0-9` and single hyphens, starting and ending with a letter or digit, taken as written (never lowercased).** Slugs never change (A10), so the reserved list looks ahead. Besides today's SPA global paths and the server paths (`api`, `health`, `hubs`, plus `assets`, the SPA bundle's folder), it holds the global pages later tasks will add: `signup`, `forgot-password`, `reset-password`, `invite`, `account`, `organizations`, `new`, and Phase 2's `confirm-email` (ADR 0003). T1.7 and T3.4 name their pages from this list. A domain test checks that `paths.ts` stays inside it. Uniqueness is the database's job: the unique index (T1.2) and a 422 field error at signup (T1.3). Decided 2026-10-23, in T1.1. | `OrganizationSlug` (T1.1) |
| P22 | **`User` lives in Application, and its username is its id.** `User : IdentityUser<Guid>` comes from `Microsoft.Extensions.Identity.Stores` (no ASP.NET Core dependency), so handlers can project user columns and use `UserManager<User>`. Identity's store and options are in Infrastructure; `SignInManager` is in API. `IFixFlowDbContext` has no unfiltered set of users: users have no organization filter, so slices add queries scoped to the organization (through its memberships) or to the caller's own account. A model test keeps the interface to organization-owned sets plus `Organizations`. The username is the id, not the email: Identity puts it into the principal's name claim, where an email could reach the logs (S26). Sign-in finds the user by email. Decided 2026-10-23, in T1.2; the options considered are in `docs/plans/t1.2-identity-persistence.md`. | `src/CLAUDE.md` §5 |
| P23 | **Enums are stored as their names** (`text`), by a convention in `FixFlowDbContext.ConfigureConventions`: readable in SQL and in partial indexes (§10), and reordering an enum's members can't change stored data. A model test checks every enum column. Decided 2026-10-23, in T1.2. | `src/CLAUDE.md` §4 |
| P24 | **Data Protection keys live in a configured folder** (`DataProtection:KeysPath`). The `full` profile mounts the named volume `api-data-protection` there, so recreating the container signs no one out, and Phase 2's replicas on the same host share the keys. Unset (the API on the host, tests), the framework's default folder applies. `SetApplicationName("FixFlow")` ties every instance to one key ring. A named volume lives on one Docker host, so hosting on several machines needs a shared store (PostgreSQL through `PersistKeysToDbContext`, blob storage, or a network share), chosen with hosting at the end of Phase 2. The keys aren't encrypted at rest in the container, which is accepted while FixFlow runs locally only. Decided 2026-10-25, in T1.2. PostgreSQL was chosen first (2026-10-23), then dropped for the smaller change: it needed a package, a table and a second DbContext. | `AddFixFlowDataProtection` (T1.2) |
| P25 | **A trusted organization scope gives a request an organization context that the server derived.** `ITrustedOrganizationScope.Open(organizationId)` (Application) returns an `IDisposable`. While it is open, `ICurrentUser.OrganizationId` returns that id, so the ownership interceptor, the query filters and the audit stamps work unchanged. It takes only an id the server derived itself (the organization it just created, an invite's organization), never one from the request, and only when the caller has no organization context: `Open` throws otherwise. The scope stays open through the commit, so post-commit handlers run in it too. `TrustedOrganizationScope` (API) is a class of its own that `HttpCurrentUser` reads, so casting `ICurrentUser` can't open one, and an architecture test allow-lists the types that take it (signup and `POST /organizations`; T3.2 adds invite acceptance). Moved here from T1.5. Decided 2026-10-25, in T1.3; the options considered are in `docs/plans/t1.3-signup-login.md`. | `src/CLAUDE.md` §5 |
| P26 | **The auth flow: MediatR handlers with an `IAuthSession` port, a 422 for any failed login, and `rememberMe` for the cookie's lifetime.** Handlers use `UserManager<User>` directly and sign in through `IAuthSession` (Application), which API implements with `SignInManager<User>` (P22), so endpoints stay thin. A wrong password, an unknown email and a locked-out account get the same 422 with one fixed detail, never a 401: the SPA's 401 → `/login` redirect leaves login forms alone, and a lockout doesn't reveal that the account exists. An unknown email still hashes the password. `rememberMe: true` gives Identity's persistent cookie (14 days, sliding), false a session cookie; signup signs in with a session cookie. Signup needs an anonymous caller (422 otherwise), so `CreatedBy` never credits another user; a signed-in user creates more organizations with `POST /organizations`. Decided 2026-10-25, in T1.3. | this plan (T1.3, T1.7) |
| P27 | **Unique violations surface as `UniqueConstraintViolationException`** (Application), with the constraint's name, the way concurrency does (P11): `WriteAsync` translates PostgreSQL's `23505`. A handler maps its own constraints (`UniqueIndexes`, whose names the configurations give their indexes) to `Result.FieldError`; any other constraint stays a 500, because it is a bug. Identity's `DuplicateEmail` and a lost race on the email index give the same 422. Decided 2026-10-25, in T1.3. | `src/CLAUDE.md` §2 |
| P28 | **Login and signup are rate-limited** (S6). Signup is limited too: a taken email gives a 422, and it could be used to spam organizations. One `auth` policy on the built-in in-memory limiter (D14): a fixed window with no queue, one budget per endpoint and client address, 10 requests a minute by default (`RateLimiting:Auth`). The client address is the IPv4 address (an IPv4-mapped IPv6 address counts as IPv4) or the /64 prefix of an IPv6 address, which a client usually controls whole. A rejected request gets a 429 Problem Details with `Retry-After`. TestServer has no client address, so each test client sends its own /64 of `2001:db8::/32` through a test-only header. T1.4 adds forgot and reset password. T1.8's E2E run raises the limit, because all its traffic comes from Docker's gateway; Phase 2's proxy needs forwarded headers. Decided 2026-10-25, in T1.3. | ARCHITECTURE §15, S6 |

## Assumptions (approved with this plan)

If one turns out wrong, change it here, and in its ADR if it has one.

| # | Assumption |
|---|---|
| A1 | Request transitions are recorded in `MaintenanceRequestStatusHistory`, which mirrors `WorkOrderStatusHistory`. This closes the ADR 0001 follow-up → ADR 0002. |
| A2 | Domain events are dispatched in two phases. **In-transaction** handlers keep aggregates consistent (WO Closed/Cancelled → request Closed/UnderReview). **Post-commit** handlers run side effects such as email; their failures are logged and never fail the action. This refines "dispatched after SaveChanges" → ADR 0005. |
| A3 | Comments: the request thread is the conversation between the tenant, managers and the assigned technician; staff can mark a comment internal. WO notes are always internal. Comments are append-only plain text with no attachments. |
| A4 | Attachments: there is no scanner until Phase 2, so files are stored with `ScanStatus = Pending` and served after authorization. Phase 2 serves only Clean files and strips EXIF. HEIC files can only be downloaded (no preview). |
| A5 | Due date = a `LocalDate` in the property's zone, plus a computed `DueAt` instant (start of the next local day) that overdue queries use. |
| A6 | Request urgency uses the priority scale (D26), and WO priority defaults to it. |
| A7 | `HybridCache` runs in-process (no Redis) for membership resolution and the dashboard; Phase 2 adds Redis as L2 without code changes. With no realtime, lists stay fresh through TanStack Query refetching (on focus and on an interval). |
| A8 | `Idempotency-Key` is stored as a unique column on the created row (org, creator, key). A replay returns the original resource. |
| A9 | Managers invite Tenants, Technicians and Managers. Only Admins grant Admin, change roles and suspend members. The last active Admin can't be demoted or suspended. Invites are only for non-members; an existing tenant is linked to more units directly. |
| A10 | The org slug is suggested from the name at signup and can't change afterwards; reserved words are blocked. The org name stays editable. |
| A11 | CSRF (S2): a SameSite=Lax cookie, JSON-only bodies and same origin (Vite proxy in dev, no CORS) make antiforgery tokens unnecessary. Invite and reset tokens travel in the URL **fragment**, so they never reach server logs. |
| A12 | Libraries: MailKit (SMTP), NodaTime.Serialization.SystemTextJson, NodaTime.Testing (`FakeClock`), `Microsoft.Extensions.ApiDescription.Server` (OpenAPI document at build time), xUnit v3 with its built-in `Assert` (FluentAssertions v8+ needs a commercial licence). Integration tests stay isolated by creating their own organizations, so no DB-reset library is needed. |
| A13 | Only tenants submit requests (no submitting on someone's behalf). Only the submitter can cancel. Requests can't be edited after submission. |
| A14 | The app's database role owns the `fixflow` database, so EF Core migrations can create the per-module schemas. It is not a superuser and can't create databases or roles. Separate migration and runtime roles (the stricter reading of S22) wait until hosting. |

**Gaps noted, kept as documented (revisit if they cause trouble):**
- Reassign is only allowed from Assigned (§7). If a technician drops out mid-job, the manager must cancel and re-accept.
- Self-signup emails stay unverified until Phase 2 (P3).
- The per-account lockout (S3) can be used as a denial of service: anyone who knows an email can keep that account
  locked, and the per-IP rate limit doesn't stop many addresses. Revisit in T9.3, and with MFA in Phase 2 (T1.3 plan).
- No audit-log UI and no GDPR export/deletion tooling (S24) in the MVP.

---

## Global definition of done (applies to every task)

1. Builds with zero warnings, and new behaviour has passing tests:
   - domain: transitions × roles
   - endpoints: integration tests that include a **cross-tenant** case
   - time logic: `FakeClock`, including DST
2. CLAUDE.md §3 holds:
   - org and roles come only from `ICurrentUser`
   - every endpoint has a role policy **and** a record check
   - every command has a FluentValidation validator
   - status changes only go through the state machines
   - time comes from `IClock`; IDs are UUIDv7
   - tenant DTOs carry no internal data
3. Status codes:

   | Situation | Code |
   |---|---|
   | Role policy fails | 403 |
   | Record not visible to the caller | 404 |
   | Transition or rule not allowed, including for the actor's role | 422 |
   | Unknown or foreign id in a request body | 422 field error |
   | Stale row version | 409 |
4. An endpoint/DTO change means running `npm run gen:api` and committing the result; `npm run lint && npm run typecheck && npm test` pass.
5. A schema change means a new migration named for its intent, with the SQL reviewed (`dotnet ef migrations script --idempotent`).
6. UI:
   - loading, empty and error states
   - strings kept outside components (D27)
   - theme tokens only
   - labelled fields and keyboard-reachable dialogs
   - tenant and technician screens checked at 360 px
7. Logs carry IDs only: no names, emails, tokens or SAS URLs.
8. CI is green (including E2E from T1.8) and the task is ticked here. ADR/ARCHITECTURE change only if a decision changed. Commits follow conventional commits and are made only when the developer asks (CLAUDE.md §5).

Sizes: **S** ≈ 1 session (4 h) · **M** ≈ 2–3 sessions · **L** ≈ 4–6 sessions.

## Milestones

| | Milestone | Exit demo |
|---|---|---|
| M0 | Foundation | Fresh clone → infra up, API + Vite running, the `full` profile serves the SPA, CI green |
| M1 | Accounts & organization context | Sign up → own org → log out/in, reset password, second org + switcher; E2E in CI |
| M2 | Properties & units | Admin manages properties (IANA zone) and units |
| M3 | Members & invites | Staff and tenants (linked to units) join through emailed invites; Admin manages roles |
| M4 | Maintenance requests | Tenant submits with photos, tracks and cancels; managers see the request |
| M5 | Triage & work orders | Full lifecycle through the manager UI; reject and close emails |
| M6 | Technician view | Critical E2E flow green with real roles on a mobile viewport |
| M7 | Comments & internal notes | Conversation and internal notes, each visible to the right people |
| M8 | Manager dashboard | Counts, overdue, 10 newest requests |
| M9 | Hardening & release | Security headers, audit log, review; `v0.1.0` |

M7 and M8 don't depend on each other and can swap. Both come after the lifecycle on purpose (R2).

---

## M0 — Foundation

- [x] **T0.1 Record planning decisions** · S
  Scope:
  - ADR 0002 (triage, tenant status & emails, request history), 0003 (email confirmation deferred), 0004 (MediatR 12.x), 0005 (two-phase domain events).
  - Sync `ARCHITECTURE.md`: §2 email list, §7 request transitions table + `MaintenanceRequestStatusHistory`, §9 flow A step 5 (no manager email), §12, S4, §5/§22 MediatR, §23 Q2/Q5.
  - `src/CLAUDE.md` §3 event dispatch.
  - Note in ADR 0001 that ADR 0002 resolves its follow-ups.

  DoD:
  - Four ADRs (Context → Decision → Consequences), status Accepted.
  - Re-reading the touched sections finds no contradiction left on triage, tenant statuses, the email list, S4, MediatR or event dispatch.

- [x] **T0.2 Repository bootstrap** · S
  Scope:
  - `git init` on `main`.
  - Config files: `.gitignore` (.NET, Node, `.env`, certs), `.gitattributes` (LF), `.editorconfig`.
  - MIT `LICENSE`, README skeleton, `.env.example`.
  - gitleaks config + `.githooks/pre-commit`.
  - Dependabot for nuget, npm, actions and docker, ignoring MediatR ≥ 13.
  - Public GitHub repo; confirm with the developer before creating it or pushing.

  DoD:
  - `gitleaks detect` is clean, and the pre-commit hook blocks a planted fake key (removed afterwards).
  - The repo is public with the MIT licence detected, and GitHub accepts the Dependabot config.
  - Merges are squash-only (merge commits since allowed for `dev` → `main` and bigger feature branches: P17). Branch protection is added once CI exists (T0.5).

  Note: until their manifests exist, the Dependabot runs for github-actions (T0.5), docker-compose (T0.3),
  npm (T0.10) and docker (T0.11) fail with "not found". That is expected, not a config error.

- [x] **T0.3 Docker Compose infrastructure** · S
  Scope: `docker-compose.yml` with `postgres:18.6-trixie` (volume at `/var/lib/postgresql`, ARCHITECTURE §20), Azurite, Mailpit and Seq. Tags pinned, health checks, named volumes, ports
  bound to `127.0.0.1`, credentials only from `.env`. An init script creates a database and a non-superuser app role.
  DoD:
  - `docker compose up -d --wait` → all four services are healthy; `down -v` followed by `up` recreates them cleanly.
  - Tracked files contain no credential literals and nothing binds to `0.0.0.0`. The app role is not a superuser.
  - Mailpit (:8025) and Seq (:5341) are reachable.

- [x] **T0.4 Backend solution skeleton** · M
  Scope:
  - `FixFlow.slnx` with Domain, Application, Infrastructure, API, Contracts and Hosting, plus the Domain.Tests, Application.Tests and API.IntegrationTests projects.
  - `Directory.Build.props`: net10.0, nullable, warnings as errors, analyzers, NuGet audit.
  - Package versions pinned centrally.
  - Project references per `src/CLAUDE.md` §1.
  - Architecture tests based on reflection.

  DoD:
  - `dotnet build FixFlow.slnx -v q` → 0 warnings; `dotnet test` green.
  - The architecture tests fail on a forbidden reference, checked by temporarily adding one: Domain → anything except NodaTime/Stateless, or Application → Infrastructure/Api.
  - A known-vulnerable package fails restore.

- [x] **T0.5 CI pipeline (backend + secrets)** · M
  Scope:
  - GitHub Actions on PRs and `main`: restore/build/test (Testcontainers on the runner), NuGet audit, gitleaks.
  - Caching and cancelling superseded runs.
  - T0.10, T0.11 and T1.8 later add the frontend, image, client-staleness and E2E jobs.

  DoD:
  - Green on a PR. A compile warning, a failing test and a planted fake secret each fail the right job (checked once).
  - Branch protection requires the CI checks.

- [x] **T0.6 Observability & health** · M
  Scope, all in `FixFlow.Hosting`:
  - Serilog (JSON console + Seq) enriched with TraceId, OrganizationId and UserId.
  - A masking policy for sensitive properties (S26).
  - OpenTelemetry traces and metrics → Seq over OTLP.
  - `/health/live` and `/health/ready` (Postgres, Blob).

  DoD:
  - A request shows up in Seq as a log entry and a trace sharing the same TraceId.
  - A test logs an object with `Email`, `Token` and `Password` properties, and the output shows masked values.
  - `/health/ready` returns 503 with Postgres stopped and 200 when it's up; `/health/live` always returns 200.

- [x] **T0.7 Test infrastructure** · M
  Scope:
  - xUnit v3.
  - Postgres and Azurite Testcontainers on the same image tags as `docker-compose.yml`, shared per test run.
  - `FixFlowApiFactory` (`WebApplicationFactory`) with `FakeClock`, an in-memory email sink and a test log sink.
  - `TestData.*` builders and client helpers (cookie + `X-Organization`), extended as each slice lands.

  DoD:
  - `dotnet test` starts the containers once per run, runs a smoke test (`/health/ready` = 200) and cleans up.
  - Tests are parallel-safe by construction (each test uses its own orgs and emails).
  - The backend suite runs in under 5 minutes locally.

- [x] **T0.8 API & application conventions** · M
  Scope:
  - Problem Details: DomainException → 422, concurrency → 409, validation → 400 with field errors; no stack traces outside Development.
  - `Result<T>` → `TypedResults`.
  - A `/api/v1` root with one `MapGroup` per module.
  - JSON: camelCase, string enums, NodaTime.
  - MediatR 12.x + a FluentValidation pipeline; `IClock` registered.
  - Fallback policy = authenticated.
  - `PagedResult<T>` with pageSize ≤ 100.
  - OpenAPI document generated at build time, with schema transformers: Instant → `date-time`, LocalDate → `date`, enums as strings. Emit OpenAPI 3.0 if orval mishandles 3.1.

  DoD:
  - Integration tests on test-only endpoints check the 404, 422, 409 and 400 Problem Details shapes. An anonymous call to an endpoint not on the allow-list → 401; pageSize 101 → 400.
  - `dotnet build` writes the OpenAPI document. NodaTime types appear as formatted strings, and enums as strings.

- [x] **T0.9 Persistence baseline** · M
  Scope:
  - `FixFlowDbContext`: Npgsql, `UseNodaTime()`, snake_case, one schema per module.
  - `IOrganizationOwned` interceptor: empty → set from `ICurrentUser`; mismatch or no org context → throw.
  - Org and soft-delete global filters, applied by convention.
  - Audit interceptor (`IClock` + current user).
  - Opt-in `xmin` row version; `SaveChanges` rethrows `DbUpdateConcurrencyException` as `ConcurrencyConflictException` (P11).
  - Two-phase domain event dispatch (A2).
  - `ToPageAsync`.
  - Auto-migration in Development only.

  DoD:
  - Model tests guard every future entity:
    - each `IOrganizationOwned` entity has the org filter and a non-null `organization_id`
    - each `ISoftDeletable` entity has the soft-delete filter
    - names are snake_case
    - `Instant` maps to `timestamptz`

    They run again on the first real entities in T1.2 and T2.1.
  - A test-only entity in a derived test context shows that:
    - saving org-owned data without an org context throws
    - a pre-commit handler's change commits atomically with the change that triggered it
    - a failing post-commit handler doesn't roll back or fail the request
  - Auto-migration runs only when `ASPNETCORE_ENVIRONMENT=Development`.

- [x] **T0.10 Frontend skeleton** · M
  Scope:
  - Vite + React 19 + strict TypeScript (P15).
  - MUI theme, light only (dark theme is open question Q14, Phase 3).
  - React Router with lazy routes (`/login`, `/:orgSlug/*`); TanStack Query refetching on focus.
  - ESLint + Prettier; Vitest + RTL + MSW.
  - `api/http.ts`: sends cookies, turns Problem Details into `ApiError`, takes `X-Organization` from the URL slug only, redirects to `/login` on 401.
  - orval + `gen:api` from the committed build-time OpenAPI document, `src/FixFlow.API/openapi.json` (P9).
  - `formatInstant` and a strings module.
  - Vite proxy for `/api`; a responsive app shell.
  - CI: a frontend job, `npm audit --audit-level=high`, and a check that the generated client is up to date.

  DoD:
  - `npm run lint && npm run typecheck && npm test && npm run build` pass; the generated folder is excluded from lint.
  - Unit tests show that:
    - the org header comes only from the URL slug
    - Problem Details become an `ApiError` with field errors
    - a 401 redirects to `/login`
    - `formatInstant` formats in a given IANA zone across a DST change
  - CI fails when the committed client is out of date.

- [x] **T0.11 Containers & `full` profile** · M
  Scope:
  - Multi-stage Dockerfile: build the SPA → publish the API → copy the SPA into `wwwroot`; runs as non-root.
  - An SPA fallback that excludes `/api` and `/health`.
  - A `full` profile `api` service that waits for healthy infra.
  - HTTPS through a locally generated dev cert (untracked, with a script for both Windows and Linux).
  - A public Azurite endpoint setting so SAS URLs work from the browser.
  - CI: an image build job.

  DoD:
  - `docker compose --profile full up -d --build --wait` serves the SPA over HTTPS. A deep link `/x/requests` returns index.html, and `/api/v1/nope` returns a 401 Problem Details to an anonymous caller (P10).
  - The image runs as non-root with no secrets in its layers. Certs and `.env` are untracked. The image builds in CI.

**M0 exit:** a fresh clone plus the README gets everything running; CI is green and required.

## M1 — Accounts & organization context

- [x] **T1.1 Organization & membership domain** · S
  Scope:
  - `Organization`: name, slug rules + reserved list. The reserved list includes every global path of the SPA (`web/src/shared/routing/paths.ts`, e.g. `login`), so no slug can shadow one, plus `api`, `health` and `hubs`, which the API keeps for itself (T0.11). Slugs, like every client route parameter, contain no dots: the SPA fallback treats a dotted last segment as a file name (T0.11).
  - `OrganizationMembership`: Active/Suspended, JoinedAt, at least one role.
  - `Role`: Tenant, Technician, Manager, Admin.

  DoD: domain tests cover slug validity (format, length, reserved words), role invariants, and suspend/reactivate.

- [x] **T1.2 Identity & account persistence** · M
  Scope:
  - ASP.NET Core Identity's store in Infrastructure, and `User : IdentityUser<Guid>` in Application (P22), with UUIDv7
    ids and first/last name.
  - Schemas `identity` and `organizations`, with the §10 unique indexes.
  - `MembershipRole` is mapped as an owned collection of the membership (`OwnsMany`, key `(membership_id, role)`), so
    it can only be read through the org-filtered membership. A model test makes isolation the default: every entity
    type that isn't owned is `IOrganizationOwned` or on a short allow-list of global ones (`Organization`, Identity's
    tables) (review of T1.1).
  - Password policy (S3); lockout after 5 failed attempts.
  - Cookie (S1) that returns 401/403 instead of redirecting: S1 settings on the T0.8 cookie scheme, which already answers 401/403 (P10).
  - `RequireConfirmedEmail = false` (P3).
  - Data Protection keys: the `full` profile's container keeps them in its own filesystem (a startup warning since
    T0.11), so recreating it signs everyone out. Persist them in a folder on a named volume (P24), which Phase 2's two
    replicas on one host share.
  - Migration `AddIdentityAndOrganizations`.

  DoD:
  - Migration SQL reviewed; the §10 unique indexes exist; the T0.9 model tests pass on the new entities.
  - Integration tests:
    - `Set-Cookie` has HttpOnly, Secure and SameSite=Lax
    - an anonymous API call → 401 with no redirect
    - user ids are UUIDv7

- [x] **T1.3 Signup, login, logout, `/me`** · M
  Scope:
  - `POST /auth/signup`: creates the user, org and Admin membership in one transaction, then signs in.
  - `POST /auth/login`, `POST /auth/logout`.
  - `GET /me`: profile + active memberships with roles.
  - `POST /organizations`: an existing user creates another org.
  - Rate-limit login and signup (S6). Signup is limited too, because it reveals whether an email has an account.
  - The trusted organization scope, moved here from T1.5. Signup and `POST /organizations` use it, and later T3.2's
    invite acceptance. The organization's id is derived on the server, never taken from the body.
  - Plan: `docs/plans/t1.3-signup-login.md`.
  - Hand-offs from T1.2:
    - Login: `FindByEmailAsync`, then `PasswordSignInAsync(user, …, lockoutOnFailure: true)`. The username is the id
      (P22), and without `lockoutOnFailure` the T1.2 lockout does nothing. An unknown email answers like a wrong
      password.
    - Create accounts only with `UserManager.CreateAsync`, never by adding a `User` to the context: the manager sets
      the normalized email (unique index, lookups), the security stamp and the lockout. After it returns
      `ConcurrencyFailure`, the scope can't save again (a failed save ends the unit of work).
    - `UserManager.CreateAsync` saves the whole context, so signup runs in one explicit transaction:
      1. create the user
      2. add the organization, then save
      3. open the trusted organization scope
      4. add the membership, then save
      5. commit

      Planning T1.3 changed three things:
      - the trusted scope moved here from T1.5
      - the organization and the membership are saved together, because the organization's id exists before the
        insert
      - the scope opens first and stays open through the commit, so the post-commit handlers run inside it
    - A unique violation becomes a 422 field error by its constraint: `ix_users_normalized_email` → `email`,
      `ix_organizations_slug` → `slug`.

  DoD:
  - Integration tests:
    - signup → 201, a cookie and an Admin membership
    - duplicate email or slug → 422 with a field error
    - 9-character password → 400
    - 5 bad logins → locked out
    - rate limit → 429
    - logout clears the cookie
    - `/me` lists only active memberships
  - Login errors don't reveal whether the email exists.

- [ ] **T1.4 Password reset & profile** · M
  Scope:
  - `IEmailSender` (MailKit → Mailpit) with templates.
  - The sender catches MailKit failures and rethrows an exception that implements `ILogSafeMessage`, with the original
    as its inner exception. The message comes from the SMTP status and error code (`SmtpCommandException.StatusCode`,
    `ErrorCode`) or names the kind of failure (connect, authentication, TLS), so logs show why a send failed (P20).
  - `POST /auth/forgot-password`, which always returns 202.
  - `POST /auth/reset-password`: 1 h, single use, token in the URL fragment.
  - Signed-in users can change their password and edit their name.
  - Rate-limit both auth endpoints.
  - Settings are read lazily, so the build-time OpenAPI document needs none. That includes the health checks: they
    take their connection strings through a `Func<IServiceProvider, string>`, like `AddFixFlowPersistence`, and the
    `BuildTimeDocument.IsGenerating` checks around them in `Program.cs` go. Otherwise each new setting needs one
    more check (review of `dev`, 2026-10-21).

  DoD:
  - Integration tests:
    - unknown email → 202 and no mail; known email → the mail is captured
    - a reset works once; reused or expired tokens fail
    - a reset ends the user's other sessions
    - changing the password requires the current one; a name edit shows up in `/me`
    - rate limit → 429
  - With SMTP down, the call still returns 202 and the failure is logged with the SMTP status, without the address.

- [ ] **T1.5 Organization context & `ICurrentUser`** · L
  Scope, per §8:
  - `OrganizationContextMiddleware`: slug → org → the caller's active membership, through `HybridCache` (60 s, tag `user:{userId}:memberships`).
  - `OrganizationMembership` raises domain events when it is created, its roles change, or it is suspended or
    reactivated. A post-commit handler invalidates `user:{userId}:memberships`, so no endpoint handler has to
    remember to (review of T1.1).
  - Claims transformation and `ICurrentUser`.
  - Every endpoint needs an org unless it is marked org-less (`/auth/*`, `/me*`, `/organizations`, invite lookup/accept).
    Org-less endpoints get no organization context; otherwise T1.3's trusted scope throws when it opens.
  - Membership resolution reads through T1.3's `OwnMemberships`. It is the one query that turns off the organization
    filter, and it carries the comment justifying that.
  - Cross-tenant test helpers: orgs A and B, a user per role, a mismatched header.

  DoD:
  - Integration tests:
    - a member of A sending `X-Organization: b` → 404
    - unknown slug → 404; suspended membership → 404
    - missing header on an org endpoint → 400
    - org-less endpoints work without the header
    - a user with different roles in A and B gets the right roles for the header sent
  - A suspension takes effect on the very next request (tag invalidation, no 60 s wait).
  - Application never uses `HttpContext`. OrganizationId appears in the Seq log scope.

- [ ] **T1.6 Authorization framework** · M
  Scope:
  - Named role policies (`CanManageOrganization`, `CanManageProperties`, `CanManageMembers`, `CanSubmitRequests`, `CanTriageRequests`, `CanWorkOnJobs`, …) based on the active org's roles.
  - The operation-requirement + `VisibleTo(user)` pattern.
  - An endpoint-coverage test (S7).

  DoD:
  - The coverage test fails for an endpoint without a policy, or with an `AllowAnonymous` that isn't on the list.
  - Policy tests per role. A user who is a Manager in A and a Tenant in B, calling a manager endpoint with `X-Organization: b` → 403.

- [ ] **T1.7 Auth & organization shell UI** · L
  Scope:
  - Pages: login, signup (slug suggested from the name), forgot password, reset password.
  - `useCurrentUser` and `useCurrentOrganization`.
  - Routing after login: one org → `/{slug}`, several → a picker, none → create an org.
  - `/:orgSlug` layout: org switcher, user menu, nav based on role, route guards (UX only).
  - The org slug is part of query keys, or the cache resets when switching.
    Either way, a response is never cached under another org's key. `web/src/api/http.ts` reads `X-Organization` from
    `window.location` when the fetch starts, so a refetch of the old page's queries during a switch goes out with
    the new slug. Take the slug from the org the query belongs to, or cancel the old org's queries before the URL
    changes. Callers still can't set the header themselves (review of `dev`, 2026-10-21).
  - The 401 handler in `http.ts` recognizes the login page through a helper in `shared/routing/paths.ts`, not with
    `pathname !== globalPaths.login`. That exact match misses `/login/` and `/LOGIN`, which the router renders as
    the login page, so a 401 there reloads the page and clears the form (review of `dev`, 2026-10-21).
  - An "organization not found" page.

  DoD:
  - Component tests:
    - validation, and server field errors shown through `setError`
    - switching orgs drops org-scoped cache
    - a query of org A that refetches during a switch to B never goes out with B's slug, or its result is dropped
    - a 401 on `/login/` or `/LOGIN` doesn't redirect
  - Manual checks: two tabs in different orgs work independently; login and signup can be used with the keyboard alone.

- [ ] **T1.8 E2E harness** · M
  Scope:
  - Playwright in `tests/web-e2e/` (Chromium, against the `full` profile over HTTPS).
  - Mailpit API helper and fake-data helpers (`*.example.test`).
  - Flows: signup → org home → logout/login, and password reset through the Mailpit link.
  - CI job: export the dev cert (`scripts/dev-cert.sh`, P19) → bring up compose `full` with `--wait` → run Playwright → upload the report and traces on failure.

  DoD:
  - Green locally and in CI. Each test signs up its own org, and the suite runs with 2 workers. 3 CI runs in a row pass.
  - A forced failure uploads the report artifact. The E2E job is a required check.

**M1 exit:** accounts, orgs and org switching work, and the cross-tenant test helpers exist; the E2E gate is active.

## M2 — Properties & units

- [ ] **T2.1 Property & unit domain + persistence** · M
  Scope:
  - `Property`: name, an address value object, an IANA `TimeZoneId` validated against Tzdb, soft delete.
  - `Unit`: name/number unique per property among non-deleted units, soft delete.
  - Schema `properties`; migration `AddPropertiesAndUnits`.

  DoD:
  - Domain tests: `Europe/Warsaw` is accepted; a Windows id and a fixed offset are rejected; required fields are enforced.
  - The model tests now cover real org-owned entities.
  - A DbContext test shows org A's data is invisible in org B's context, and soft-deleted rows are hidden.

- [ ] **T2.2 Property & unit endpoints** · M
  Scope:
  - `/properties`: list (paged, filter by name), get, create, update, archive.
  - `/properties/{id}/units`: list, create, update, archive.
  - Manager + Admin only; responses include `timeZone`.

  DoD: integration tests:
  - happy paths
  - field-level 400s; invalid zone → 400
  - another org's ids → 404 on every route
  - Technician/Tenant → 403

- [ ] **T2.3 Properties & units UI** · M
  Scope: a properties DataGrid (server paging), a create/edit dialog with time-zone autocomplete, a property detail page listing
  its units, and archive confirmations.
  DoD: component tests cover validation and server error mapping. E2E: the admin creates a property and a unit.

## M3 — Members & invites

- [ ] **T3.1 Invite & tenant-unit domain** · M
  Scope:
  - `Invite` fields: email, roles, unit (Tenant only), token **hash** only, `ExpiresAt` = +72 h.
  - States Pending/Accepted/Revoked; resending rotates the token.
  - Rules for who can grant which role (A9).
  - The `TenantUnit` link.

  DoD:
  - Domain tests (`FakeClock`):
    - an invite is valid until exactly 72 h
    - accepting after expiry, revocation or a previous acceptance → DomainException
    - Tenant without a unit → error
    - Manager granting Admin → error
  - A test shows the raw token is never persisted.

- [ ] **T3.2 Invite endpoints & acceptance** · L
  Scope:
  - Create, list pending, revoke, resend.
  - Invite email with the token in the URL fragment.
  - Org-less lookup: `POST` with the token in the body → org name, roles, and whether an account exists.
  - Accept as a new user → registers, email confirmed, membership (+ unit link), signed in.
  - Accept as an existing user → must be signed in as the invited email.
  - The membership cache is invalidated; acceptance is rate-limited.

  DoD:
  - Integration tests:
    - every role combination allowed by A9
    - acceptance as a new user and as an existing user
    - signed in with a different email → 422
    - unknown token → 404; expired, revoked or used token → 422
    - another org's invite id → 404
    - rate limit → 429
  - A log-sink assertion confirms no token appears in any log line during the flow.

- [ ] **T3.3 Member management & org settings** · M
  Scope:
  - Members list (paged): roles, status, tenant units.
  - Admin only: change roles, suspend/reactivate (not yourself; last-Admin protection), rename the org.
  - Manager/Admin: link and unlink tenants and units.
  - Membership cache invalidated on every change.

  DoD: integration tests:
  - a role change takes effect on the next request
  - a suspended member gets 404 in that org only
  - last Admin → 422
  - Manager changing roles → 403
  - another org's member → 404
  - a rename shows up in `/me` with the slug unchanged

- [ ] **T3.4 Members & invites UI + accept page** · L
  Scope:
  - Members DataGrid.
  - Invite dialog: roles per A9, unit picker for Tenants.
  - Pending invites with resend/revoke.
  - Role editing and suspension.
  - Tenants shown on the unit detail page.
  - Accept-invite page: register or sign in → accept → `/{slug}`.

  DoD:
  - Component tests:
    - a unit is required for Tenants
    - the Admin option is hidden for Managers
    - the accept page shows the right form for a new versus an existing account
  - E2E: the admin invites a technician and a tenant; both accept through the Mailpit links and see the nav for their role.

- [ ] **T3.5 Development seed** · S
  Scope: a seeder that runs in Development only and creates:
  - two orgs
  - properties in two zones (one with DST), with units
  - one user per role, plus one technician who belongs to both orgs

  All data is fake; the seed password comes from user-secrets.
  DoD:
  - A fresh database is seeded on first run. A test shows it never runs outside Development. gitleaks is clean.
  - The README lists the seeded accounts (emails only).

## M4 — Maintenance requests

- [ ] **T4.1 Request aggregate & state machine** · M
  Scope:
  - `MaintenanceRequest` fields: unit, property, submitter, title, description, category, urgency, permission to enter, availability text, status, reasons, `xmin`.
  - Stateless machine per P1; system transitions can only be called by the WO sync.
  - `MaintenanceRequestStatusHistory`.
  - Events `RequestSubmitted` and `RequestStatusChanged`.

  DoD: domain tests:
  - the full transition × role matrix, including denied cases
  - rejecting requires a reason
  - only the submitter can cancel, and only before Accepted
  - every transition writes history and raises an event

- [ ] **T4.2 Uploads & attachments** · L
  Scope:
  - `IBlobStorage` with an Azure SDK implementation:
    - 5-minute write SAS for `{orgId}/{yyyy}/{mm}/{uuid}`
    - short read SAS
    - blob properties and range reads
  - Private containers and Azurite CORS, created at dev startup. The `full` profile runs as Production (P18), so it needs them created too.
  - Browser-facing SAS URLs use `Blob:PublicEndpoint` when it is set; the `full` profile sets it (P19).
  - `POST /uploads`.
  - `Attachment`: owner is a request or a WO (exactly one), kind General/Before/After, `ScanStatus` Pending.
  - Checks when attaching:
    - org prefix matches
    - blob exists
    - ≤ 10 MB
    - magic bytes for jpg/png/heic/pdf
    - ≤ 10 per owner
    - not already attached
  - `GET /attachments/{id}/content`: authorize through the owner → 302 to a read SAS.

  DoD:
  - Integration tests against Azurite:
    - a disallowed type or size is refused when the slot is issued
    - a text file renamed `.jpg` → 422 at attach
    - an 11th file → 422
    - another org's blob key → 422
    - an unauthorized download → 404; an authorized one → 302 to a SAS that expires within 5 minutes
  - Browser uploads work in dev and in the `full` profile.
  - No SAS URL appears in logs, and the containers deny anonymous access.

- [ ] **T4.3 Request submission & queries** · L
  Scope:
  - `POST /requests`: Tenant only, own units only, `Idempotency-Key` required, photo keys, rate-limited.
  - `GET /requests` with `VisibleTo` (tenants see their own units, staff see everything); filter by status, property or unit; paged.
  - `GET /requests/{id}`, with separate tenant and staff DTOs.
  - `POST /requests/{id}/cancel` and `GET /my-units`.
  - Archiving a property or unit with open requests → 422.

  DoD:
  - Integration tests:
    - 201 with a Location header
    - an idempotent replay → same id, no duplicate; missing key → 400
    - a unit that isn't the tenant's → 422
    - list visibility per role
    - another unit's or another org's request → 404
    - the tenant JSON contains no internal fields (shape test)
    - cancel by another tenant of the unit → 422, cancel after Accepted → 422
    - rate limit → 429
  - Unit tests for the authorization handler (Application.Tests).

- [ ] **T4.4 Tenant request UI** · L
  Scope:
  - My requests: cards on mobile with a tenant status chip.
  - New request form with direct-to-blob uploads and progress (upload code lives in `shared/`).
  - Request detail: timeline, photos (HEIC as a download), cancel.

  DoD:
  - Component tests:
    - the Zod schema mirrors the server limits (types, 10 MB, 10 files)
    - a failed upload can be retried or removed
    - a submission keeps one `Idempotency-Key` across retries
  - Times show in the property's zone; checked at 360 px.
  - E2E: a tenant submits with a photo and sees "Received".

- [ ] **T4.5 Manager request list & detail (read-only)** · M
  Scope: a requests DataGrid (default filter: needs triage; filter by property; server paging) and a detail page with all
  request data, photos and history.
  DoD: component tests cover filters and states. E2E: the manager sees the tenant's request.

## M5 — Triage & work orders

- [ ] **T5.1 Work order aggregate & state machine** · L
  Scope:
  - `WorkOrder` fields: request, property, unit, title, priority, `DueDate` + `DueAt`, assignee, status, completion note, `xmin`.
  - Stateless machine per §7: reasons required for hold, reopen and cancel; a note required to complete; reassign only from Assigned.
  - `WorkOrderStatusHistory`.
  - Events `WorkOrderCreated`, `WorkOrderAssigned` and `WorkOrderStatusChanged`.

  DoD:
  - Domain tests:
    - every §7 transition × {Tenant, assigned Technician, other Technician, Manager, Admin}, allowed and denied
    - required reasons and note
    - history and an event every time
  - `FakeClock` tests: `DueAt` and the overdue check across both DST transitions, in two zones.
  - ADR 0001 MVP rules:
    - WO Closed → request Closed
    - WO Cancelled → request UnderReview → re-accept → replacement WO
    - a second active WO → 422
    - a WO on a Closed request → 422

- [ ] **T5.2 Work order persistence & request sync** · M
  Scope:
  - Schema `workorders` with the §10 indexes, using `due_at` in place of the Phase 2 `sla_due_at`.
  - A partial unique index on `(organization_id, request_id) WHERE status <> 'Cancelled'`.
  - `xmin` on both aggregates.
  - The in-transaction request-sync handler.
  - Migration `AddWorkOrders`.

  DoD:
  - The partial unique index rejects a second active WO inserted directly. Two parallel transitions on one WO → one gets a 409.
  - Closing the WO and closing the request commit together; a forced failure rolls back both.

- [ ] **T5.3 Triage endpoints** · M
  Scope:
  - `start-review`.
  - `accept`: priority, optional due date, optional technician; creates the WO atomically and returns its id.
  - `reject`: requires a reason; the tenant email goes out post-commit.
  - Commands carry the row version.
  - `GET /technicians`: active members with the Technician role.

  DoD: integration tests:
  - accept leaves the request Accepted with a WO in New or Assigned, in one transaction
  - a repeat accept → 422
  - a technician from another org, or without the role → 422
  - stale row version → 409
  - the reject email is captured and contains only the reason
  - cross-tenant → 404 on every route
  - Tenant/Technician → 403

- [ ] **T5.4 Work order endpoints & tenant status** · L
  Scope:
  - `GET /work-orders` with `VisibleTo` (staff see all, technicians see their assigned jobs); filter by status, technician, property or overdue; paged.
  - `GET /work-orders/{id}`, `POST …/assign`, `POST …/transitions`.
  - `PUT /work-orders/{id}` for priority and due date.
  - The tenant request DTO gets the P2 status mapping and the technician's first name.
  - Closing sends one "Done" email.

  DoD:
  - Integration tests:
    - transitions per role
    - a technician who isn't assigned → 404
    - Close sends exactly one email; Completed sends none
    - Cancel → request UnderReview, no email
    - one test per row of the P2 mapping
    - the tenant DTO exposes only the technician's first name
    - cross-tenant → 404 on every route
    - stale version → 409
  - Unit tests for the authorization handler.

- [ ] **T5.5 Manager triage & work order UI** · L
  Scope:
  - Triage actions on the request detail page: Start review; Accept (dialog with priority, due date, technician); Reject with a reason.
  - Work orders DataGrid with overdue rows highlighted and filters.
  - WO detail: assign/reassign, transitions with reason/note dialogs, edit priority and due date, timeline.
  - A 409 shows "changed by someone else, reload".

  DoD:
  - Component tests:
    - the accept dialog validates its fields
    - only valid actions show for each status and role
    - a 409 shows the reload prompt
  - E2E: the whole lifecycle with the manager doing the technician steps → the tenant sees "Done" and gets the Mailpit email.

## M6 — Technician view

- [ ] **T6.1 Technician jobs & work order photos** · M
  Scope:
  - An active-jobs filter on `GET /work-orders`, sorted by priority, then due date.
  - `POST /work-orders/{id}/attachments` for Before/After photos: the assigned technician or staff, while Assigned, InProgress or OnHold.

  DoD: integration tests:
  - a technician sees only their own jobs
  - another technician's job → 404; another org's → 404
  - photo limits are enforced
  - the tenant DTO contains no WO photos or notes

- [ ] **T6.2 Technician mobile UI** · L
  Scope:
  - My jobs: mobile-first cards.
  - Job detail: description, unit address, permission to enter, availability, photos.
  - Camera capture for before/after photos.
  - Action bar: Start, Hold + reason, Resume, Complete + note; timeline.
  - Users with several roles see both navs.

  DoD:
  - Component tests: hold needs a reason; complete needs a note.
  - E2E, with the technician steps on a mobile viewport:
    1. the tenant submits
    2. the manager accepts and assigns
    3. the technician starts, adds an after photo and completes
    4. the manager closes
    5. the tenant sees "Done" and gets the email

    Required on every PR.

**M6 exit:** the critical lifecycle from §2 is green in CI with real roles.

## M7 — Comments & internal notes

- [ ] **T7.1 Comments backend** · M
  Scope:
  - `Comment`: belongs to exactly one of a request or a WO; author; plain text ≤ 4 000 characters; `IsInternal`; `Idempotency-Key`.
  - Rules per A3.
  - `GET/POST /requests/{id}/comments` and `GET/POST /work-orders/{id}/notes`.

  DoD: integration tests:
  - a tenant never receives internal comments (list and detail)
  - a tenant posting an internal comment → 422
  - a technician who isn't assigned → 404
  - cross-tenant → 404
  - an idempotent replay creates no duplicate
  - length validation

- [ ] **T7.2 Comments UI** · M
  Scope:
  - A conversation panel on the request and job detail pages.
  - For staff: an "internal" toggle and distinct styling for internal comments.
  - A WO notes panel.
  - Plain-text rendering only (S14).

  DoD:
  - Component tests: tenants have no toggle; staff see internal items clearly marked.
  - E2E: the technician replies publicly → the tenant sees it; the manager's internal note never appears for the tenant.

## M8 — Manager dashboard

- [ ] **T8.1 Dashboard query & cache** · M
  Scope:
  - `GET /dashboard` (Manager/Admin): WO counts by status, requests awaiting triage, overdue WOs, the 10 newest requests.
  - `HybridCache` key `org:{orgId}:dashboard` (60 s, tagged), invalidated post-commit by request and WO events.

  DoD:
  - Integration tests:
    - counts match a known scenario
    - overdue honours each property's zone (`FakeClock`)
    - the next call after a transition reflects it
    - another org's data is never counted
    - Technician/Tenant → 403
  - `EXPLAIN` shows the dashboard queries use the §10 indexes.

- [ ] **T8.2 Dashboard UI & landing pages** · S
  Scope:
  - Dashboard cards, plus overdue and newest lists that link to filtered lists.
  - Periodic refetch.
  - A landing page per role: staff → dashboard, technician → My jobs, tenant → My requests.

  DoD: component tests cover loading, empty and error states. E2E checks the new-request count.

## M9 — Hardening & release

- [ ] **T9.1 Security headers & transport (S19, S20)** · M
  Scope:
  - HTTPS redirect; HSTS outside dev.
  - CSP:
    - `default-src 'self'`
    - the blob endpoint in `img-src` and `connect-src`
    - `frame-ancestors 'none'`
    - `style-src 'unsafe-inline'` for MUI/Emotion unless a nonce is wired up
  - nosniff, Referrer-Policy and Permissions-Policy.
  - CORS stays off.

  DoD: an integration test checks the headers on API and SPA responses. E2E fails on any CSP console violation, and the suite passes.

- [ ] **T9.2 Audit log (S25)** · M
  Scope:
  - An append-only `AuditLog`: org (nullable, for global events), actor, action, target ids, time — IDs only.
  - Entries for logins, invites, role changes, suspensions and assignments.
  - A DB trigger rejects UPDATE and DELETE.

  DoD: an integration test per event type; UPDATE and DELETE fail; no personal data beyond IDs.

- [ ] **T9.3 Security & privacy review** · M
  Scope: go through S1–S26 and mark each Done, Phase 2 or N/A, citing the test that proves it. Check:
  - rate limits and endpoint coverage
  - the justification on every `IgnoreQueryFilters()`
  - tenant-DTO shape tests
  - log masking (search Seq for emails, tokens and SAS URLs after an E2E run)
  - NuGet and npm audits, and gitleaks

  DoD:
  - The checklist is added to this plan with evidence, and no High finding is open.
  - Deferred items are listed: S2 (N/A), S4, S5, S18, S22 TLS, S24 tooling.

- [ ] **T9.4 UX & accessibility pass** · M
  Scope:
  - Loading, empty and error states on every page.
  - Keyboard flow through dialogs, labels, contrast in both themes.
  - Tenant and technician pages at 360 px.
  - A 404 page.

  DoD: Lighthouse accessibility ≥ 90 on login, the request form, job detail and the dashboard. Issues found are fixed or logged.

- [ ] **T9.5 MVP release** · S
  Scope:
  - README: setup, seeded accounts, scripts, architecture links.
  - A demo script.
  - A fresh-clone check.
  - Tag `v0.1.0` with GitHub release notes.
  - Phase 2 backlog created as GitHub issues.

  DoD: a fresh clone gets to a running app by following only the README. `main` is green, the release is published, and this plan is marked complete.

---

## Scope coverage (§2 Phase 1 → tasks)

| Phase 1 item | Tasks |
|---|---|
| Organizations, properties, units; tenants linked to units | T1.1–T1.3, T2.1–T2.3, T3.1, T3.3 |
| Cookie auth, invites, fixed roles, resource-based authorization | T1.2–T1.6, T3.1–T3.4 |
| Requests: create with photos, list, detail, comments | T4.1–T4.5, T7.1–T7.2 |
| Work orders: create from request, assign, priority, due date, internal notes, status workflow | T5.1–T5.5, T7.1 |
| Technician view: assigned jobs, status updates, before/after photos | T6.1–T6.2 |
| Manager dashboard | T8.1–T8.2 |
| Emails: invites, password reset, request rejected, request closed (P2) | T3.2, T1.4, T5.3, T5.4 |
| Audit trail of status changes | T4.1, T5.1 (+ T9.2 for security events) |
| E2E critical lifecycle on every PR | T1.8 → T6.2 |
| Runs locally only (Docker Compose) | T0.3, T0.11 |

**Out of scope for the MVP:**
- Realtime/SignalR, Redis, RabbitMQ/outbox, workers.
- SLAs and scheduling.
- Cost, time and materials logging; tenant ratings; advanced search.
- 1:N work orders and reopening requests.
- MFA and email confirmation.
- nginx/multi-replica and hosting.
- Localization.
- Audit-log UI and GDPR export/deletion tooling.

## Verification

- **The plan itself:** every bullet of §2 Phase 1 maps to at least one task (table above). The decisions behind P1–P4 and A1–A13 get recorded in T0.1 before any code exists.
- **Each milestone:** its exit demo can be run by hand. From M1 the Playwright suite runs these demos in CI on every PR, and at M6 it covers the full §2 flow: submit → accept & assign → complete → close → tenant sees Done + email.
- **Each task:** its DoD plus the global DoD, checked with `dotnet build -v q`, focused `dotnet test --filter`,
  `npm run lint && npm run typecheck && npm test`, and green CI.
