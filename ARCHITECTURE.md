# FixFlow — Architecture

> FixFlow is a web app for property managers to handle maintenance requests from tenants through to completion.

This document is the single source of truth for FixFlow's technical architecture: decisions, structure, conventions, security requirements, risks and dependencies. Significant changes to anything here should be recorded as an ADR in `docs/adr/` and reflected in this file.

---

## Table of contents

1. [Product overview](#1-product-overview)
2. [Scope and roadmap](#2-scope-and-roadmap)
3. [Key decisions](#3-key-decisions)
4. [Architecture overview](#4-architecture-overview)
5. [Technology stack](#5-technology-stack)
6. [Solution structure](#6-solution-structure)
7. [Domain model](#7-domain-model)
8. [Identity, tenancy and authorization](#8-identity-tenancy-and-authorization)
9. [Key flows](#9-key-flows)
10. [Persistence conventions](#10-persistence-conventions)
11. [Time handling](#11-time-handling)
12. [Messaging](#12-messaging)
13. [Caching and realtime (Redis)](#13-caching-and-realtime-redis)
14. [File storage](#14-file-storage)
15. [API conventions](#15-api-conventions)
16. [Frontend architecture](#16-frontend-architecture)
17. [Security requirements](#17-security-requirements)
18. [Observability](#18-observability)
19. [Testing strategy](#19-testing-strategy)
20. [Local development and deployment](#20-local-development-and-deployment)
21. [Technical risks](#21-technical-risks)
22. [Dependencies](#22-dependencies)
23. [Product decisions and open questions](#23-product-decisions-and-open-questions)

---

## 1. Product overview

### Roles

| Role | Responsibilities |
|---|---|
| **Tenant** | Submits maintenance requests, comments, tracks status, rates completed work |
| **Technician** | Works assigned work orders: updates status, uploads photos, logs time and materials |
| **Manager** | Triages requests, creates and assigns work orders, approves costs, manages properties, units and invites |
| **Admin** | Everything a Manager can do, plus organization settings and user/role management |

A person has **one account** and may belong to **several organizations**, holding **one or more roles in each** (e.g. Technician at one organization, Manager + Technician at another).

### Terminology

- **Organization** — the SaaS customer (e.g. a property management company) and the data boundary. Everything is scoped to it.
- **Tenant** — the renter role only. "Tenant isolation", "multi-tenancy" and "cross-tenant tests" are the standard technical terms for isolation *between organizations*.

### Core lifecycle (MVP)

```
Tenant submits request → Manager triages → Work order created & assigned
→ Technician works it → Technician completes → Manager closes → Tenant sees result
```

---

## 2. Scope and roadmap

### Phase 1 — MVP (one complete lifecycle)

- Organizations, properties, units; tenants linked to units
- Authentication (cookie-based), invites, fixed roles, resource-based authorization
- Maintenance requests: create with photos, list, detail, comments
- Work orders: create from request, assign technician, priority, due date, internal notes, status workflow
- Technician view: assigned jobs, status updates, before/after photos
- Manager dashboard: counts by status, overdue, recent requests
- Email notifications (in-process, post-commit — ADR 0005) **only** for: invites, password reset, request rejected
  (tenant), request closed (tenant, "Done"). Nothing is sent when a work order is completed. **No email** for
  "request submitted" (managers) or "work order assigned" (technician) — managers see new requests on the dashboard,
  technicians in their job list (ADR 0002)
- Audit trail of status changes
- E2E tests (Playwright) for the critical lifecycle (submit request → assign → complete → close), run against the
  Compose `full` profile on every PR
- Runs **locally only** (Docker Compose); no cloud hosting

### Phase 2 — Production-ready

- Transactional outbox + RabbitMQ (MassTransit) + `Notifications` worker
- Redis: `HybridCache` L2 cache and SignalR backplane
- Realtime in-app notifications (SignalR)
- SLA rules per priority with breach detection (`Scheduler` worker)
- Scheduling: tenant time windows, technician confirmation, calendar view
- Cost tracking (labor, materials) with manager approval
- Advanced filtering/search, tenant feedback after closure
- Several work orders per request (1:N) and reopening closed requests (ADR 0001)
- Email confirmation for self-signup (ADR 0003)
- `Media` worker: thumbnails, EXIF stripping, malware scan
- **End of Phase 2:** decide whether to host a public demo (and where) — see §20

### Phase 3 — Advanced

- Preventive (recurring) maintenance
- Asset/equipment registry, inventory of spare parts
- Vendors and quotes, invoicing and payments (Stripe)
- Reports and analytics, CSV/PDF export
- Technician PWA with offline support
- AI triage (Claude API): auto-categorization, urgency suggestion, thread summaries
- QR codes per unit/asset, public API and webhooks
- **In Phase 3:** decide whether to add a dark theme (Q14)

### Explicitly out of scope for MVP

Realtime, SLAs, scheduling, costs, Redis, RabbitMQ, multiple workers, payments, AI, mobile apps, custom permissions, per-organization branding, MFA, email confirmation, cloud hosting, localization (English only), dark theme (light only).

### Planning assumptions

- Solo developer, **~4 hours per day**, **no fixed target date** — plan by milestones and vertical slices, not dates.
- Public GitHub repository (see D23).

---

## 3. Key decisions

Each decision below should have a corresponding ADR in `docs/adr/NNNN-title.md` (Context → Decision → Consequences).

| # | Area | Decision |
|---|---|---|
| D1 | Architecture style | **Modular monolith** (Clean Architecture) + background workers |
| D2 | Tenancy | **Multi-tenant SaaS**, single shared database, `OrganizationId` on every organization-owned row |
| D3 | Tenant isolation | EF Core **global query filters** + cross-tenant integration tests; Postgres RLS optional later |
| D4 | Accounts | **One global account per person + organization memberships** with roles per membership |
| D5 | Login & org context | Global login, email globally unique. Active organization from the URL slug, sent as `X-Organization` header and validated against the membership **on every request** |
| D6 | Authentication | ASP.NET Core Identity, **HttpOnly cookies**, SPA served from the same site. No tokens in `localStorage` |
| D7 | Authorization | **Fixed roles** (Tenant, Technician, Manager, Admin) + **resource-based policies**; multiple roles per account |
| D8 | Request vs work order | **Separate entities; one active work order per request in MVP, 1:N in Phase 2** (ADR 0001). A request is accepted or rejected in triage and closes when its work order is closed; a work order is the unit of work. Merging duplicates is Phase 2 |
| D9 | Database | **PostgreSQL** with EF Core (Npgsql) |
| D10 | Primary keys | **UUIDv7** (`Guid.CreateVersion7()`) |
| D11 | Time | **UTC `timestamptz`** for instants, **IANA time zone per property**, **NodaTime** for SLA and scheduling |
| D12 | Events | Domain events in-process in two phases — in-transaction handlers and post-commit handlers (MVP, ADR 0005); integration events through a **transactional outbox** → **RabbitMQ via MassTransit** (Phase 2) |
| D13 | Redis | Used for exactly two things: **distributed cache** (`HybridCache` L2) and **SignalR backplane** |
| D14 | Rate limiting | Built-in ASP.NET Core rate limiter (in-memory). Not in Redis |
| D15 | File storage | Azure Blob Storage API via **Azurite** (emulator) behind `IBlobStorage`; presigned (SAS) direct uploads. Real cloud storage only if/when hosting is decided |
| D16 | UI kit | **MUI** |
| D17 | Runtime | **.NET 10 (LTS)** |
| D18 | API style | **Minimal APIs** grouped per module, REST, `/api/v1`, Problem Details |
| D19 | Local orchestration | **Docker Compose** for infrastructure; API, workers and Vite run on the host (optional `full` profile runs everything in containers) |
| D20 | Hosting | **Local only** through Phase 2. "Production-like" = Compose `full` profile with 2 API replicas behind nginx. Public demo/hosting decided at the end of Phase 2 (no paid cloud accounts) |
| D21 | Email | **Mailpit** (catches all mail). A real provider only if/when hosting is decided |
| D22 | Repo | **Monorepo** (`src/`, `web/`, `tests/`, `docs/`). Short-lived feature branches merge into **`dev`**; `dev` merges into `main` at milestones with a merge commit (ADR 0006) |
| D23 | Visibility | **Public GitHub repository**, MIT licence. No secrets and no real personal data in code, seeds or fixtures |
| D24 | Organizations | **Public self-signup**: whoever creates an organization becomes its first Admin |
| D25 | Request triage | A manager **always** accepts or rejects a request before a work order exists; accepting creates the work order in the same transaction (ADR 0002) |
| D26 | Categories & priorities | **Fixed lists** in MVP. Categories: Plumbing, Electrical, HVAC, Appliances, Structural, Other. Priorities: Low, Normal, High, Emergency |
| D27 | Language | **English only**; UI strings kept out of components so localization can be added later |
| D28 | API client | **orval** (TanStack Query hooks + MSW mocks) |
| D29 | Naming & errors | snake_case DB naming; business rule violation → 422, concurrency conflict → 409, not found / not authorized → 404 |

---

## 4. Architecture overview

### Style

- **One deployable API** made of modules with explicit boundaries: `Identity`, `Organizations`, `Properties`, `Requests`, `WorkOrders`, `Scheduling`, `Notifications`, `Billing` (later).
- Modules communicate through **MediatR** commands/queries and **domain events**. A module never queries another module's tables directly.
- **Background workers** handle asynchronous side-effects and communicate with the API only through RabbitMQ.
- Not microservices: one developer, one domain, one database. Modules can be extracted later if needed.

### Container diagram

```
                    ┌─────────────────────────────┐
                    │  React SPA (Vite + TS + MUI)│
                    └──────┬───────────────┬──────┘
                    HTTPS REST        WebSocket (SignalR)
                           ▼               ▼
            ┌──────────────────────────────────────────┐
            │        FixFlow.API (ASP.NET Core)        │
            │  Endpoints → MediatR → Domain → EF Core  │
            │  SignalR hubs · Outbox · Auth            │
            │  MassTransit consumer: RealtimePusher    │
            └───┬──────────┬──────────┬─────────┬──────┘
                │          │          │         │
       ┌────────▼───┐ ┌────▼─────┐ ┌──▼─────┐ ┌─▼────────────┐
       │ PostgreSQL │ │  Redis   │ │RabbitMQ│ │ Azure Blob   │
       │ data +     │ │ L2 cache │ │ Mass-  │ │ (Azurite     │
       │ outbox     │ │ backplane│ │ Transit│ │  locally)    │
       └────────────┘ └──────────┘ └──┬─────┘ └──────▲───────┘
                                      │              │
              ┌───────────────────────┼──────────────┤
              ▼                       ▼              │
   ┌────────────────────┐  ┌─────────────────────┐   │
   │ Notifications      │  │ Scheduler           │   │
   │ worker: email/push │  │ worker: SLA checks, │   │
   │                    │  │ preventive maint.   │   │
   └────────────────────┘  └─────────────────────┘   │
                     ┌────────────────────┐          │
                     │ Media worker       │──────────┘
                     │ thumbnails, EXIF,  │
                     │ malware scan       │
                     └────────────────────┘
```

The API hosts the `RealtimePusher` consumer, so workers never push to SignalR directly. Workers publish events; the API translates them into SignalR messages.

### Layering (per module, Clean Architecture)

```
Api (endpoints, hubs)  →  Application (commands, queries, validators)  →  Domain (entities, rules, events)
                                  ↑
                     Infrastructure (EF Core, Redis, MassTransit, Blob, Email)
```

- `Domain` has no dependencies on frameworks (NodaTime is the only allowed exception).
- `Application` depends on `Domain` and defines interfaces such as `IBlobStorage`, `IEmailSender`, `IClock`.
- `Infrastructure` implements those interfaces.
- Handlers use `DbContext` directly (no generic repository layer).

---

## 5. Technology stack

| Layer | Technology |
|---|---|
| Frontend | React 19, TypeScript, Vite, **MUI**, TanStack Query, React Router, React Hook Form + Zod, `@microsoft/signalr` |
| API client | TypeScript types/clients generated from OpenAPI (orval) |
| Backend | .NET 10, ASP.NET Core Minimal APIs, SignalR |
| Application | MediatR **12.x** (last Apache-2.0 line, ADR 0004), FluentValidation |
| Domain | Stateless (request and work-order state machines), NodaTime |
| Persistence | EF Core + Npgsql, `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`, PostgreSQL 18+ (upgrades: §20) |
| Identity | ASP.NET Core Identity (cookie auth) |
| Messaging | RabbitMQ + MassTransit (with EF Core outbox) |
| Cache / realtime scale-out | Redis 7+, `HybridCache`, SignalR StackExchangeRedis backplane |
| Files | Azure Blob Storage SDK (`Azure.Storage.Blobs`) against Azurite |
| Email | SMTP to Mailpit |
| Jobs | MassTransit scheduled messages / Quartz.NET (Scheduler worker) |
| Observability | Serilog, OpenTelemetry (traces + metrics), Seq |
| Testing | xUnit, Testcontainers, Vitest, React Testing Library, Playwright |
| Orchestration | Docker Compose |
| CI | GitHub Actions (build, tests, lint, security scans, container image build). No deployment until hosting is decided |

---

## 6. Solution structure

```
FixFlow/
├─ ARCHITECTURE.md
├─ docs/
│  └─ adr/                          # architecture decision records
├─ src/
│  ├─ FixFlow.Hosting/              # shared OpenTelemetry, Serilog, health checks, resilience setup
│  ├─ FixFlow.API/                  # endpoints, hubs, auth, DI composition, serves SPA in prod
│  ├─ FixFlow.Application/          # commands, queries, validators, interfaces (per module folders)
│  ├─ FixFlow.Domain/               # entities, value objects, state machines, domain events
│  ├─ FixFlow.Infrastructure/       # EF Core, Identity stores, Redis, MassTransit, Blob, Email
│  ├─ FixFlow.Contracts/            # integration event DTOs shared with workers
│  ├─ FixFlow.Workers.Notifications/
│  ├─ FixFlow.Workers.Scheduler/
│  └─ FixFlow.Workers.Media/
├─ web/                             # React SPA
│  └─ src/
│     ├─ app/                       # providers, router, theme
│     ├─ features/                  # requests/, work-orders/, properties/, dashboard/, auth/
│     ├─ shared/                    # components, hooks, utils
│     └─ api/                       # generated OpenAPI client
└─ tests/
   ├─ FixFlow.Domain.Tests/
   ├─ FixFlow.Application.Tests/
   ├─ FixFlow.API.IntegrationTests/
   └─ web-e2e/                      # Playwright
```

Workers are added in Phase 2; until then the solution contains only Hosting, Api, Application, Domain, Infrastructure and Contracts.

---

## 7. Domain model

### Core entities

| Entity | Notes |
|---|---|
| `Organization` | Customer of the SaaS and the multi-tenancy boundary. Has a unique `Slug` used in URLs and the `X-Organization` header |
| `User` | Global identity user (email globally unique). Not organization-owned |
| `OrganizationMembership` | Links a user to an organization; `Status` Active/Suspended |
| `MembershipRole` | Roles of a membership (one or more per membership) |
| `Property` | Building/site. Has address and **IANA `TimeZoneId`** |
| `Unit` | Apartment/space within a property |
| `TenantUnit` | Links tenant users to units (a tenant may rent several units) |
| `MaintenanceRequest` | Submitted by a tenant for a unit: title, description, category, urgency, attachments |
| `MaintenanceRequestStatusHistory` | Append-only log of request transitions (from, to, by, at, reason); mirrors `WorkOrderStatusHistory` (ADR 0002) |
| `WorkOrder` | Created by accepting a request, in the same transaction (`RequestId` required; at most one non-cancelled work order per request in MVP, 1:N in Phase 2). Direct creation by a manager is deferred. Priority, assignee, due date, SLA due, status |
| `WorkOrderStatusHistory` | Append-only log of transitions (from, to, by, at, reason) |
| `Comment` | On a request or work order. Flag `IsInternal` (never visible to tenants) |
| `Attachment` | Blob reference: key, content type, size, scan status, owner entity |
| `Notification` | In-app notification per user |
| `AuditLog` | Append-only record of security-relevant and business changes |
| *Phase 2+* | `Schedule`, `CostItem`, `SlaPolicy`, `BusinessHours`, `Feedback`, `PreventiveSchedule`, `Asset`, `Vendor`, `Quote`, `Invoice` |

### Maintenance request lifecycle

```
Submitted ──► UnderReview                         start review (optional)
Submitted | UnderReview ──► Accepted ──► Closed   accept; closes when its work order is Closed (system)
Submitted | UnderReview ──► Rejected              reject (reason required)
Submitted | UnderReview ──► Cancelled             cancel (submitter only)
Accepted ──► UnderReview                          system: its work order is Cancelled
```

Triage takes one step (ADR 0002): a manager accepts or rejects straight from Submitted or UnderReview, and accepting
creates the work order in the same transaction, so an Accepted request always has an active work order.

| Transition | Allowed | Rules |
|---|---|---|
| Submitted → UnderReview (start review) | Manager, Admin | Optional |
| Submitted/UnderReview → Accepted | Manager, Admin | Priority (defaults to the request's urgency), optional due date, optional technician (active member with the Technician role). Creates the work order in the same transaction: New, then Assigned if a technician is given |
| Submitted/UnderReview → Rejected | Manager, Admin | Reason required; the tenant is emailed |
| Submitted/UnderReview → Cancelled | The tenant who submitted it | Only before Accepted |
| Accepted → Closed | System | Its work order is Closed; commits with the work order change; the tenant is emailed ("Done") |
| Accepted → UnderReview | System | Its work order is Cancelled; the manager re-triages (accept again or reject). No email |

Rejected, Cancelled and Closed are final in MVP; there is no reopen of a request (ADR 0001). System transitions run in
an in-transaction handler reacting to `WorkOrderStatusChanged` (ADR 0005) and can't be triggered from an endpoint.

Rules live in **one place**: the `MaintenanceRequest` aggregate, using a Stateless state machine. Every transition,
including submission, writes a `MaintenanceRequestStatusHistory` row and raises `RequestSubmitted` or
`RequestStatusChanged`; for a system transition, *by* is the user whose work order action caused it.
`MaintenanceRequest` uses optimistic concurrency (see §10).

> **Phase 2 (1:N, ADR 0001):** with several work orders per request, the request closes when at least one work order
> is Closed and none is still open (the rest are Cancelled), and returns to UnderReview only when all are Cancelled.
> The handler re-evaluates idempotently; concurrent evaluations are safe thanks to `xmin`. A Closed request can be
> reopened (Closed → Accepted; Manager, Admin; reason required) to add a further work order.

Merging duplicate requests is out of MVP: reject with a reason instead.

### Tenant-facing status

Tenants see a simplified status (ADR 0002):

| Tenant sees | Request status | Work order status |
|---|---|---|
| Received | Submitted, UnderReview | — |
| Received | Accepted | New |
| Technician assigned | Accepted | Assigned |
| In progress | Accepted | InProgress, OnHold, Completed |
| Done | Closed | Closed |
| Rejected | Rejected | — |
| Cancelled | Cancelled | — |

The assigned technician's first name is shown from "Technician assigned" on. "Scheduled" comes back with scheduling in
Phase 2. Tenants are emailed only when their request is rejected or closed.

### Work order lifecycle

```
            ┌──────────── Cancelled ◄───────────┐
            │                ▲                  │
New ──► Assigned ──► InProgress ◄──► OnHold     │
                         │                      │
                         ▼                      │
                     Completed ──► Closed       │
                         │                      │
                         └──► InProgress (reopen)
```

| Transition | Allowed roles | Rules |
|---|---|---|
| New → Assigned | Manager, Admin | Technician must belong to the same organization |
| Assigned → Assigned (reassign) | Manager, Admin | No notification in MVP; in-app to old and new technician in Phase 2 |
| Assigned → InProgress | Assigned Technician, Manager | |
| InProgress ↔ OnHold | Assigned Technician, Manager | Reason required for OnHold |
| InProgress → Completed | Assigned Technician, Manager | Completion note required; photos optional |
| Completed → Closed | Manager, Admin | May close the linked request (see request lifecycle). MVP (1:1): always closes it in the same transaction, and the tenant gets the "Done" email |
| Completed → InProgress (reopen) | Manager, Admin | Reason required |
| New/Assigned/OnHold → Cancelled | Manager, Admin | Reason required |

Rules live in **one place**: the `WorkOrder` aggregate, using a Stateless state machine. Every transition writes a `WorkOrderStatusHistory` row and raises `WorkOrderStatusChanged`. `WorkOrder` uses optimistic concurrency (see §10).

---

## 8. Identity, tenancy and authorization

### Accounts and organizations (memberships)

```
User (global identity: email, password, MFA, profile)
 └─ OrganizationMembership (UserId, OrganizationId, Status: Active | Suspended, JoinedAt)
     └─ MembershipRole (MembershipId, Role)      // Tenant, Technician, Manager, Admin
```

- **One global account per person.** Email is globally unique (ASP.NET Identity default).
- A user can be a member of **several organizations**, with **different roles in each** (e.g. Technician at Acme, Manager + Technician at Brightway).
- The user owns their global profile; organizations manage only **memberships** (invite, change roles, suspend).
- Business entities keep `OrganizationId`; they reference users by `UserId` (always within an organization context).

### Active organization resolution (per request)

The active organization is **not** stored in the auth cookie. It is resolved on every request:

1. The SPA URL contains the organization slug: `/{orgSlug}/...`.
2. The SPA's HTTP client sends `X-Organization: {orgSlug}` on every API call (taken **only** from the current URL).
3. `OrganizationContextMiddleware` resolves slug → organization → the user's **active** membership
   (cached in `HybridCache`, TTL ~60 s, invalidated on membership/role changes).
4. No active membership → **404**. Otherwise `IClaimsTransformation` adds `OrganizationId` and that membership's roles to the
   request principal, and `ICurrentUser` exposes them.
5. Endpoints that don't belong to an organization (`/api/v1/me`, `/api/v1/me/organizations`, auth endpoints) don't require the header.

Consequences:

- Multiple tabs in different organizations work independently.
- Suspending a membership revokes access to that organization on the next request (after cache invalidation), without affecting other organizations.
- Global query filters and authorization handlers read only from `ICurrentUser`; **`OrganizationId` is never taken from request bodies or route values.**
- SignalR: the client connects with `?org={orgSlug}`; the hub validates the membership the same way. One connection per active organization.

### Login and onboarding

- **Global login** at `/login` (no slug). After sign-in: one membership → redirect to `/{orgSlug}`; several → organization picker.
- The header contains an **organization switcher** (navigates to `/{otherSlug}/...`).
- Creating an organization (signup flow) makes the creator its first Admin.
- Managers invite tenants (linked to a unit), technicians and other managers by email via **single-use, 72-hour invite tokens**:
  - existing user → accepting the invite adds a membership;
  - new user → registers, then the membership is created.
- Per-organization security policies (e.g. MFA required for Managers/Admins) are enforced when the user accesses that organization in that role.

Out of MVP: a combined cross-organization view ("my jobs in all organizations"), leaving an organization self-service, ownership transfer.

### Authorization — two layers + a safety net

1. **Fixed roles (endpoint level):** named policies such as `CanAssignWorkOrders` (Manager, Admin) or `CanSubmitRequests` (Tenant) guard endpoints.
2. **Resource-based policies (record level):** after loading a record, an `AuthorizationHandler<OperationAuthorizationRequirement, TResource>` checks the user's relationship to it:

| Role | Rule |
|---|---|
| Tenant | Request/work order belongs to one of the tenant's units |
| Technician | `WorkOrder.AssignedTechnicianId == currentUserId` |
| Manager, Admin | Same organization (enforced also by the global filter) |

3. **Global query filter:** `OrganizationId == currentUser.OrganizationId` on every organization-owned entity.

Conventions:

- Checks are written against **operations** (`WorkOrderOps.View`, `WorkOrderOps.UpdateStatus`), not raw `IsInRole` calls, so the logic can later move to a permission model without touching endpoints.
- **Lists** apply the same rules as query filters (`.VisibleTo(user)`), never by loading and filtering in memory.
- Unauthorized access to an existing record returns **404**, not 403, so IDs can't be probed.
- Tenant-facing DTOs never contain internal notes or internal comments.

---

## 9. Key flows

### A. Tenant submits a request

1. SPA requests upload slots: `POST /api/v1/uploads` → API returns short-lived, write-only SAS URLs.
2. SPA uploads photos directly to Blob Storage.
3. SPA calls `POST /api/v1/requests` with details, blob keys and an `Idempotency-Key` header.
4. Handler validates, saves `MaintenanceRequest` + attachments and the `RequestSubmitted` event **in one transaction** (outbox from Phase 2).
5. MVP: no notification — managers see new requests on the dashboard and in the triage list (ADR 0002).
   Phase 2: managers are notified in-app (SignalR to `org:{orgId}:managers`, no email) and the Media worker processes the photos.
6. A post-commit handler invalidates the cache tag `org:{orgId}` (ADR 0005).

### B. Work order status change

1. `POST /api/v1/work-orders/{id}/transitions` with `{ "to": "InProgress", "reason": "..." }` and the row version.
2. Resource authorization → state machine validates the transition for the user's roles.
3. In one transaction (ADR 0005): the history row, plus the request sync — Closed → the request is Closed,
   Cancelled → the request returns to UnderReview.
4. After commit: if the request was closed, the tenant gets the "Done" email; the cache is invalidated.
   Phase 2: realtime notifications, and the Scheduler re-evaluates the SLA.

### C. Reads

- Dashboard and lookups go through `HybridCache`.
- Lists and details read from Postgres with projections, `AsNoTracking`, and pagination.

---

## 10. Persistence conventions

- **PostgreSQL**, EF Core code-first migrations, one `DbContext` with schema-per-module (`requests.*`, `workorders.*`, ...).
- **Primary keys:** UUIDv7 via `Guid.CreateVersion7()`. Sortable, index-friendly, non-guessable, can be generated before insert.
- **Organization column:** every organization-owned table has a non-nullable `OrganizationId`, set automatically on save from the request's active organization (`ICurrentUser`) and covered by a global query filter.
- **Concurrency:** Postgres `xmin` mapped as row version on aggregates that are edited concurrently (`WorkOrder`, `MaintenanceRequest`). Conflicts return `409 Conflict`.
- **Soft delete** (`DeletedAt`) for business entities; also a global filter. Audit and history tables are append-only.
- **Auditing:** `CreatedAt/By`, `UpdatedAt/By` set by an EF interceptor.
- **Indexes (initial):**
  - `work_orders (organization_id, status, sla_due_at)`
  - `work_orders (organization_id, assigned_technician_id, status)`
  - `work_orders (organization_id, request_id)` — MVP (1:1): unique, partial `WHERE status <> 'Cancelled'`; Phase 2 drops the uniqueness for 1:N
  - `maintenance_requests (organization_id, unit_id, created_at desc)`
  - `users (normalized_email)` unique (Identity default)
  - `organization_memberships (user_id, organization_id)` unique
  - `organizations (slug)` unique
- **Seed data:** two demo organizations, properties, units, one user per role, and one technician who is a member of both (development only).

---

## 11. Time handling

### Rules

| Kind of value | Storage |
|---|---|
| Something that **happened**, or a computed deadline (`CreatedAt`, `CompletedAt`, `SlaDueAt`) | NodaTime `Instant` → Postgres **`timestamptz` (UTC)** |
| A **future wall-clock intention** (appointment window, recurring schedule, business hours) | Local date/time (`LocalDate`, `LocalTime`, `LocalDateTime`) + the **property's IANA time zone**; optionally a cached computed `Instant` for querying |

### Details

- Each `Property` stores an **IANA time zone id** (e.g. `Europe/Kyiv`), validated against `DateTimeZoneProviders.Tzdb`. Never fixed offsets, never Windows ids.
- The **property's** time zone is authoritative for SLAs, business hours and appointments, not the server's or the viewer's.
- **NodaTime** is used in the domain for SLA and scheduling. DST gaps and overlaps are resolved explicitly (`InZoneStrictly` for user input, `InZoneLeniently` for computed times).
- `IClock` is injected everywhere; tests use `FakeClock`. No `DateTime.Now` / `DateTime.UtcNow` in domain or application code.
- **API:** timestamps are ISO-8601 UTC (`2026-10-07T13:00:00Z`); responses include the property's `timeZone`.
- **Frontend:** formats with `Intl.DateTimeFormat(..., { timeZone: property.timeZone })`, showing building-local time by default.
- **SLA example:** Urgent = 8 business hours, Mon–Fri 08:00–18:00 local. Created Friday 16:00 → due Monday 14:00 local → stored as UTC `Instant`.
- SLA and scheduling calculations require tests covering weekends, holidays and both DST transitions.

---

## 12. Messaging

### Approach

- **MVP:** domain events dispatched in-process (`DomainEventDispatcher`, not MediatR) in two phases during
  `SaveChanges` (ADR 0005):
  - **In-transaction** handlers keep aggregates consistent (work order Closed/Cancelled → request Closed/UnderReview)
    and commit or roll back with the change that raised the event.
  - **Post-commit** handlers run side effects (tenant emails, cache invalidation). Their failures are logged with
    types and stack traces, without messages (ADR 0007), and never fail the action.
  - **How an event flows.** The phase belongs to the handler (its interface), not to the event. Every event goes
    through both phases, and an event can have handlers in either phase, both, or neither:
    1. `SaveChangesAsync` takes the events off the tracked aggregates and calls their in-transaction handlers. Events
       those handlers raise are dispatched the same way, round by round, until none are left.
    2. The change and everything those handlers did are written in one transaction.
    3. After the commit, every event from step 1, including those the handlers raised, goes to its post-commit
       handlers.

    For example, a work order is Closed. In the transaction, its handler closes the request, which raises the
    request's Closed event. After the commit, the work order event invalidates the cache and the request event sends
    the tenant the "Done" email. The aggregate doesn't know what reacts to its events, so a new reaction is a new
    handler, with no change to the domain.
      round 1: WorkOrderClosed          → in-transaction handler closes the request → raises RequestClosed
      round 2: RequestClosed            → (no in-transaction handlers)
      returns: [WorkOrderClosed, RequestClosed]
      after commit: post-commit handlers of both → cache invalidation + tenant email
- **Phase 2:** integration events written to the **MassTransit EF Core transactional outbox** during the in-transaction phase, in the same transaction as the business change, then delivered to **RabbitMQ**. Email moves to the `Notifications` worker.

### Integration events (initial catalog, in `FixFlow.Contracts`)

| Event | Consumers |
|---|---|
| `RequestSubmitted` | RealtimePusher (in-app to managers — **no email**), Media |
| `RequestRejected` | Notifications (tenant), RealtimePusher |
| `RequestClosed` | Notifications (tenant "Done" email; feedback request), RealtimePusher |
| `WorkOrderCreated` | Notifications, RealtimePusher, Scheduler (start SLA) |
| `WorkOrderAssigned` | RealtimePusher (in-app to technician — **no email**) |
| `WorkOrderStatusChanged` | Notifications, RealtimePusher, Scheduler, cache invalidation |
| `WorkOrderCompleted` | Analytics later. **No tenant email** at Completed (ADR 0002) |
| `CommentAdded` | Notifications, RealtimePusher |
| `AttachmentUploaded` | Media (thumbnails, EXIF strip, malware scan) |
| `SlaBreached` | Notifications (managers), RealtimePusher |

### Rules

- Events are **immutable records**, versioned by name (`WorkOrderAssignedV2`) when contracts change incompatibly.
- Events carry IDs and minimal data; consumers load what they need.
- **Consumers are idempotent** (MassTransit inbox / processed-message table).
- Retries with exponential backoff, then **dead-letter queue**; DLQ depth is monitored.
- Correlation and trace IDs propagate through MassTransit (OpenTelemetry).

---

## 13. Caching and realtime (Redis)

Redis is used for **exactly two purposes**. Other uses (rate limiting, locks, presence) require a new ADR.

### 13.1 Distributed cache — `HybridCache`

- L1 in-memory per instance + L2 Redis, with stampede protection.
- Cached items:

| Key | TTL (safety net) | Tags |
|---|---|---|
| `org:{orgId}:dashboard` | 60 s | `org:{orgId}`, `org:{orgId}:workorders` |
| `org:{orgId}:properties` | 10 min | `org:{orgId}`, `org:{orgId}:properties` |
| `org:{orgId}:technicians` | 10 min | `org:{orgId}`, `org:{orgId}:users` |
| `org:{orgId}:categories` | 10 min | `org:{orgId}` |
| `membership:{userId}:{orgSlug}` | 60 s | `user:{userId}:memberships` (invalidated on invite acceptance, role change, suspension) |

- Invalidation by tag in domain/integration event handlers (`RemoveByTagAsync`).
- **Never cache** permission-sensitive detail data or per-user views without the user id in the key.
- Every key is prefixed with the organization id.

### 13.2 SignalR backplane

- `AddSignalR().AddStackExchangeRedis(...)` with channel prefix `fixflow`.
- Hub: `/hubs/notifications`, cookie-authenticated.
- Groups (joined **server-side only**, after authorization):
  - `user:{userId}`
  - `org:{orgId}:managers`
  - `workorder:{workOrderId}` (when viewing a work order the user is authorized to see)
- Clients cannot join arbitrary groups.

### Degradation

If Redis is unavailable, the cache falls back to L1/database and realtime works within a single instance; the SPA falls back to polling via TanStack Query refetch. Correctness never depends on Redis.

---

## 14. File storage

- **Azurite** (Azure Blob Storage emulator, Docker Compose service) in all environments until hosting is decided. Code uses the official Azure Blob SDK behind `IBlobStorage`, so switching to real Azure Blob (or another provider) needs no business-code changes.
- Containers: `attachments` (private), `thumbnails` (private). **No public containers.**
- **Upload:** API issues a SAS URL valid for **5 minutes**, write-only, for one specific blob name (`{orgId}/{yyyy}/{mm}/{uuid}`). The client uploads directly.
- **Download:** API authorizes the user, then issues a short-lived read SAS URL.
- **Validation:** allowed types `jpg`, `png`, `heic`, `pdf` (checked by magic bytes, not only extension); max **10 MB per file**, **10 files per request**.
- **Processing (Media worker, Phase 2):** EXIF stripping (removes GPS), thumbnail generation, ClamAV scan. Attachment `ScanStatus`: `Pending → Clean | Infected`; only `Clean` files are served.
- Lifecycle management policy moves old blobs to Cool tier.

---

## 15. API conventions

- REST over HTTPS, base path **`/api/v1`**, JSON (camelCase), enums as strings.
- **Minimal APIs** grouped per module (`MapGroup("/api/v1/work-orders")`), each with its authorization policy.
- Errors as **RFC 9457 Problem Details**; validation errors include field-level details. No stack traces outside Development.
- **Pagination:** `?page=1&pageSize=25` (max 100); responses include `totalCount`. Cursor pagination may be introduced for large feeds.
- **Filtering/sorting:** explicit query parameters (`status`, `propertyId`, `assigneeId`, `from`, `to`, `sort`).
- **Idempotency:** `Idempotency-Key` header on create endpoints (request submission, comments).
- **Concurrency:** row version sent with updates; mismatch → `409 Conflict`.
- **State transitions** are explicit commands (`POST /work-orders/{id}/transitions`), not generic `PATCH status`.
- OpenAPI document generated by ASP.NET Core; TypeScript client generated with orval.
- Built-in **rate limiter** (in-memory) on login, password reset, invite acceptance and request submission.

---

## 16. Frontend architecture

- **Vite + React 19 + TypeScript (strict)**, **MUI** components and theming (light theme only; dark theme is open question Q14).
- **Feature-based folders** (`features/requests`, `features/work-orders`, ...), each with pages, components, hooks and API calls.
- **Server state:** TanStack Query (query keys per resource, invalidation on mutations and on SignalR events).
- **Forms:** React Hook Form + Zod schemas (mirroring server validation; server remains authoritative).
- **Routing:** React Router with role-aware route guards (UX only; the server enforces security).
- **Realtime:** a single SignalR connection created after login; incoming events update or invalidate TanStack Query caches.
- **Dates:** formatted in the property's time zone via `Intl.DateTimeFormat`.
- **Auth:** cookie-based; the SPA calls `GET /api/v1/me` on load (user + list of memberships with roles). All app routes live under `/:orgSlug/...`; the HTTP client sends `X-Organization` from the current URL; an organization switcher in the header changes the slug.
- Responsive layout; the technician view is mobile-first.
- **Production-like hosting** (Compose `full` profile): the built SPA is served by `FixFlow.API` (same origin → no CORS, cookies work for REST and SignalR). In development, Vite proxies `/api` and `/hubs` to the API.

---

## 17. Security requirements

### Authentication

- **S1.** ASP.NET Core Identity with cookie auth: `HttpOnly`, `Secure`, `SameSite=Lax` (or `Strict`). No tokens in `localStorage`/`sessionStorage`.
- **S2.** Antiforgery protection for state-changing requests if cookies can be sent cross-site.
- **S3.** Password policy: minimum 10 characters; Identity hashing; lockout after 5 failed attempts.
- **S4.** Invite and password-reset tokens are single-use and time-limited (72 h / 1 h). Email confirmation is required
  from Phase 2 (ADR 0003); in MVP, self-signup emails stay unconfirmed and accepting an invite confirms the email.
- **S5.** MFA (TOTP) available for all users and required for Manager and Admin roles (Phase 2).
- **S6.** Rate limiting (built-in, in-memory) on login, password reset, invite acceptance and request submission.

### Authorization and tenancy

- **S7.** Every endpoint has an explicit authorization policy; anonymous endpoints are allow-listed.
- **S8.** Resource-based authorization on every record access (see §8).
- **S9.** `OrganizationId` comes only from the server-validated membership for the request (`ICurrentUser`), never from bodies or route values; global query filters on all organization-owned entities; automated cross-tenant tests must fail to read or write other organizations' data.
- **S10.** SignalR hubs require authentication; group membership is assigned server-side only.
- **S11.** Role-specific DTOs; internal notes and internal comments are never serialized for tenants.

### Input and output

- **S12.** Server-side validation (FluentValidation) of every command.
- **S13.** Parameterized queries only (EF Core); no string-concatenated SQL.
- **S14.** No `dangerouslySetInnerHTML`; any rich text/Markdown is sanitized.
- **S15.** Problem Details without stack traces or internal details in non-development environments.

### Files

- **S16.** Type allow-list with magic-byte check, size and count limits.
- **S17.** Private containers; short-lived, single-blob SAS URLs; downloads only after authorization.
- **S18.** EXIF stripping and malware scanning before files are served.

### Transport, infrastructure and secrets

- **S19.** HTTPS only, HSTS, security headers (CSP, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `frame-ancestors 'none'`).
- **S20.** CORS disabled in production (same origin); restricted to the Vite origin in development.
- **S21.** The repository is **public**: no secrets, real credentials or real personal data in code, config, seeds or fixtures. Secrets via `dotnet user-secrets` / an untracked `.env` (only `.env.example` committed); **gitleaks** in CI and as a pre-commit hook.
- **S22.** Postgres, Redis and RabbitMQ are exposed only on the Compose network (and `localhost` for development), with authentication enabled; least-privilege database users per service. TLS between services is required once hosting is introduced.
- **S23.** Dependency scanning: Dependabot, `dotnet list package --vulnerable`, `npm audit` in CI.

### Privacy and audit

- **S24.** Personal data (names, contacts, addresses, photos of homes) handled per GDPR principles: minimization, export and deletion on request, defined retention.
- **S25.** Append-only audit log for status changes, assignments, cost approvals, role changes, invites and logins.
- **S26.** No personal data, tokens, cookies or SAS URLs in logs; Serilog destructuring policies mask sensitive fields. Masking doesn't reach exceptions: failed post-commit handlers are logged with types and stack traces but no messages, except in Development (ADR 0007).

---

## 18. Observability

- **Logging:** Serilog, structured JSON, enriched with `TraceId`, `OrganizationId`, `UserId` (ids only, no personal data).
- **Tracing and metrics:** OpenTelemetry across API, EF Core, HTTP, Redis and MassTransit; trace context propagates through messages.
- **Local:** **Seq** (Docker Compose) receives structured logs, and OTLP traces and metrics, from the API and workers.
- **Production-like (Compose `full` profile):** also Seq. A hosted backend is chosen together with hosting (end of Phase 2).
- **Health checks:** `/health/live` and `/health/ready` (Postgres, Redis, RabbitMQ, Blob).
- **Key metrics/alerts:** API error rate and p95 latency, outbox backlog, DLQ depth, SLA breaches, failed email sends.

---

## 19. Testing strategy

| Level | Tooling | Focus |
|---|---|---|
| Domain unit tests | xUnit | State machine transitions × roles, SLA calculation (weekends, holidays, DST), value objects |
| Application tests | xUnit | Handlers, validators, authorization handlers |
| API integration tests | xUnit + `WebApplicationFactory` + **Testcontainers** (Postgres, Redis, RabbitMQ, Azurite) | Endpoints end-to-end, **cross-tenant access tests** (member of A but not B, suspended membership, different roles per organization, mismatched `X-Organization`), auth policies, concurrency conflicts, outbox delivery |
| Frontend unit/component | Vitest + React Testing Library | Forms, components, hooks |
| E2E | Playwright (Chromium) against the Compose `full` profile | 2–3 critical flows: submit request → assign → complete → close |

- Time-dependent tests use NodaTime `FakeClock`.
- Integration tests are the main safety net. They run in CI on every pull request, and so do the E2E tests.
- The E2E harness arrives with the first UI slice (signup/login). Each later MVP slice extends the critical flow.
- E2E isolation: each test signs up its own organization (public self-signup, D24), so tests don't share state.
  Invited users (tenant, technician) get their invite link from the Mailpit HTTP API.
- E2E data is fake only (`*.example.test` emails, generated names), per D23.

---

## 20. Local development and deployment

### Local

- **Docker Compose** (`docker-compose.yml` at the repo root) runs the infrastructure:

| Service | Image | Ports (host) | Phase |
|---|---|---|---|
| `postgres` | `postgres:18` (exact tag: see below) | 5432 | MVP |
| `azurite` | `mcr.microsoft.com/azure-storage/azurite` | 10000 (blob) | MVP |
| `mailpit` | `axllent/mailpit` | 1025 (SMTP), 8025 (UI) | MVP |
| `seq` | `datalust/seq` | 5341 (UI + ingestion/OTLP) | MVP |
| `redis` | `redis:7` | 6379 | Phase 2 |
| `rabbitmq` | `rabbitmq:3-management` | 5672, 15672 (UI) | Phase 2 |
| `clamav` | `clamav/clamav` | 3310 | Phase 2 |

- All services have health checks and named volumes; image versions are pinned.
- Default workflow — infrastructure in containers, code on the host (fast rebuilds, debugger):
  ```bash
  docker compose up -d
  dotnet run --project src/FixFlow.API
  cd web && npm run dev
  ```
- `docker compose --profile full up` also builds and runs the API and workers as containers (used for E2E tests and demoing).
  The API runs as Production and serves the SPA at https://localhost:8443 with the host's exported ASP.NET Core dev
  certificate; a one-shot `migrate` service applies the migrations before it starts.
  From Phase 2: `docker compose --profile full up --scale api=2` runs two API replicas behind an `nginx` reverse proxy
  (sticky sessions off) to verify the SignalR Redis backplane and stateless API behavior.
- Connection strings for local development live in `appsettings.Development.json` (non-secret, pointing to `127.0.0.1`,
  where Compose publishes the ports); the database password comes only from `Database:Password` (user-secrets). A `.env.example` documents Compose variables.
- Migrations applied automatically in Development; demo seed data loaded.
- Secrets via `dotnet user-secrets`.

### PostgreSQL versions and upgrades

- **Version:** PostgreSQL 18 on the Debian image, pinned to an exact patch and Debian release in `docker-compose.yml`
  (started on `postgres:18.6-trixie`). Testcontainers reads the tag from `docker-compose.yml`, so the tests always
  run on the Compose image and a Dependabot bump is tested on its own PR.
- **Data volume:** mounted at `/var/lib/postgresql`, not `/var/lib/postgresql/data`. From 18 the image keeps each major
  version's cluster in `/var/lib/postgresql/<major>/docker`, so the old and new clusters can sit side by side in one
  volume during an upgrade.
- **Patch updates** (18.6 → 18.7) arrive as Dependabot PRs. The on-disk format doesn't change: pull the image and recreate the container.
- **Major upgrades** (18 → 19) are deliberate, and Dependabot ignores them. The new image starts an **empty** cluster
  next to the old one, so the data has to be carried over:
  1. Run the backend tests against the new major first: change the tag in `docker-compose.yml` on the upgrade branch and
     run `dotnet test` before recreating the local containers (Testcontainers follows the tag).
  2. Local data is synthetic: `docker compose down -v` and start again; the init script, migrations and seed rebuild it.
  3. To keep local data instead: `pg_dump -Fc` the app database inside the container, copy the file out with
     `docker compose cp` (don't redirect binary output through PowerShell), switch the tag, and `pg_restore` it after the
     init script has recreated the role and database. `pg_upgrade --link` inside the shared volume is the faster route for large data.
  4. Change the tag in Compose and the major version in this section in the same PR.
- **Debian release:** changing it (e.g. `trixie` → its successor) can change glibc collation rules and invalidate text
  indexes. Treat it like a major upgrade: run `REINDEX DATABASE` and `ALTER DATABASE ... REFRESH COLLATION VERSION` afterwards.
- **Hosting** (end of Phase 2) needs a provider that offers PostgreSQL 18 or newer.

### Environments

| Environment | Purpose | Infrastructure |
|---|---|---|
| Local (dev) | Day-to-day development, debugging | Compose infrastructure + API/Vite on the host |
| Local (production-like) | E2E tests, multi-replica checks, demos | Compose `full` profile: all services in containers, SPA served by the API, nginx in front |

There is **no cloud environment** through Phase 2.

### Hosting (deferred)

Decided at the **end of Phase 2**, with the constraint of **no paid cloud accounts**. Candidates: Oracle Cloud Always Free VM
running the Compose stack, Cloudflare Tunnel for temporary demos, or free tiers of managed services
(Neon/Supabase, Upstash, CloudAMQP). The architecture stays provider-neutral: containers, `IBlobStorage`, SMTP email, OTLP telemetry.

### CI (GitHub Actions)

1. On PR: restore → build (warnings as errors) → backend tests (Testcontainers) → frontend lint, type-check, tests →
   build container images → `docker compose --profile full up` → Playwright E2E → gitleaks → dependency audit.
   If E2E fails, the Playwright HTML report and traces are uploaded as a workflow artifact.
2. On merge to `main`: the same checks. Images are built for E2E, which also verifies the Dockerfiles; they are not published or deployed.
3. GitHub Actions is free for public repositories.

### Conventions

- `.editorconfig`, nullable reference types enabled, `TreatWarningsAsErrors`.
- ESLint + Prettier for the SPA.
- Conventional commits; short-lived feature branches off `dev`, squash-merged (a merge commit for a bigger branch whose
  commits are worth keeping); `dev` → `main` at milestones with a merge commit (ADR 0006).

---

## 21. Technical risks

| # | Risk | Impact | Likelihood | Mitigation |
|---|---|---|---|---|
| R1 | Cross-tenant data leakage | Critical | Medium | Global filters, org id only from the per-request validated membership, cross-tenant tests, optional RLS |
| R2 | Scope creep / over-engineering before the core flow works | High | High | Strict MVP scope (§2); broker, Redis and workers only in Phase 2 behind interfaces |
| R3 | Lost or duplicate messages | High | Medium | Transactional outbox, idempotent consumers, retries + DLQ with alerts |
| R4 | Stale cache data | Medium | Medium | Tag invalidation on events, short TTLs, no caching of permission-sensitive data |
| R5 | Realtime messages delivered to the wrong user | High | Low–Med | Server-side group membership, org-scoped group names, hub tests |
| R6 | Invalid status transitions or edits by the wrong role | Medium | Medium | Single state machine in the domain, transition × role tests, optimistic concurrency |
| R7 | Time zone / DST / business-hours bugs in SLAs and scheduling | Medium | High | UTC instants, IANA zone per property, NodaTime, DST-focused tests |
| R8 | Malicious or oversized uploads; EXIF location leaks; storage cost | Medium | Medium | S16–S18, lifecycle policies |
| R9 | Operational complexity of many services for a solo developer | Medium | High | One-command Docker Compose setup, health checks, workers introduced only in Phase 2 |
| R10 | Hard-to-debug flows across API → broker → worker | Medium | High | OpenTelemetry propagation, correlation ids, Seq |
| R11 | Email deliverability (notifications in spam) once hosted | Low (until hosting) | Medium | Deferred: transactional provider + SPF/DKIM/DMARC when hosted; in-app notifications as the reliable channel |
| R16 | Secrets or personal data leaked through the public repository | High | Medium | gitleaks in CI + pre-commit, `.env` untracked, synthetic seed data only |
| R12 | Query performance degradation as data grows | Medium | Low–Med | Indexes (§10), pagination, projections, query plan review |
| R13 | Cookie auth complexity with SPA + SignalR | Medium | Medium | Same-origin hosting decided up front (§16) |
| R14 | Wrong organization context (stale/mismatched `X-Organization` header) | High | Low–Med | Header derived only from the URL slug; membership validated per request; integration tests with mismatched org headers |
| R15 | Revoked membership or changed roles still effective due to caching | Medium | Low | Short cache TTL + explicit invalidation on membership/role changes; tests for suspended memberships |

---

## 22. Dependencies

### Infrastructure / external services

| Dependency | Purpose | Needed from | Local |
|---|---|---|---|
| PostgreSQL 18+ | System of record, outbox | MVP | Compose |
| Azurite (Azure Blob Storage emulator) | Attachments, thumbnails | MVP | Compose |
| Mailpit (SMTP) | Invites, password reset, notifications | MVP | Compose |
| Seq | Logs and traces | MVP | Compose |
| GitHub + GitHub Actions | Public repo, CI | MVP | — |
| Redis 7+ | HybridCache L2, SignalR backplane | Phase 2 | Compose |
| RabbitMQ 3.13+ | Integration events | Phase 2 | Compose |
| ClamAV | Malware scanning | Phase 2 | Container |
| Stripe, Claude API, push provider | Payments, AI triage, mobile push | Phase 3 | Test modes |

### Key packages

**Backend**

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`, `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime`
- `MediatR` (pinned to 12.x — ADR 0004), `FluentValidation`
- `Stateless`, `NodaTime`
- `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`
- `Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Caching.StackExchangeRedis`
- `Microsoft.AspNetCore.SignalR.StackExchangeRedis`
- `Azure.Storage.Blobs`
- `Serilog.AspNetCore`, `OpenTelemetry.*`
- `AspNetCore.HealthChecks.*` (NpgSql, Redis, RabbitMQ, Azure Storage)
- `Serilog.Sinks.Seq`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `Serilog.Enrichers.Sensitive` (log masking, S26)
- Tests: `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `NodaTime.Testing`, `Testcontainers.PostgreSql`,
  `Testcontainers.Redis`, `Testcontainers.RabbitMq`, `Testcontainers.Azurite`

**Frontend**

- `react`, `react-dom`, `react-router`
- `@mui/material`, `@mui/icons-material`, `@mui/x-date-pickers`, `@mui/x-data-grid`
- `@tanstack/react-query`, `react-hook-form`, `zod`, `@hookform/resolvers`
- `@microsoft/signalr`
- `orval`, `vitest`, `@testing-library/react`, `@playwright/test`

### Build order

```
Domain model + state machines
  → Persistence (EF Core, migrations, organization filter, UUIDv7, NodaTime mapping)
    → Identity (global login, memberships + roles, org context middleware, invites, org switcher, resource policies)
      → Properties/Units → Requests (+ Azurite uploads) → Work Orders → Technician view → Dashboard   [MVP]
        → Outbox + RabbitMQ + Notifications worker
        → Redis HybridCache + SignalR backplane + realtime UI
        → SLA (Scheduler worker), Scheduling, Costs, Media worker                                     [Phase 2]
          → Preventive maintenance, Assets, Vendors/Quotes, Billing, Analytics, AI                     [Phase 3]
```

**E2E:** the Playwright harness is added with the first UI slice (signup/login) and runs in CI from then on.
Each MVP slice extends the critical flow. It is complete (submit → assign → complete → close) once the Technician view lands.

---

## 23. Product decisions and open questions

### Decided (MVP)

| # | Question | Decision |
|---|---|---|
| Q1 | How do tenants join? | Manager invite linked to a unit (QR self-signup in Phase 3) |
| Q2 | What does a tenant see after a request becomes a work order? | Simplified status: Received → Technician assigned → In progress → Done, plus Rejected and Cancelled (mapping in §7, ADR 0002); public comments; assigned technician's first name. "Scheduled" comes back with scheduling in Phase 2 |
| Q5 | Notification channels for MVP | Email only, for: invites, password reset, request rejected (tenant), request closed (tenant). Nothing at work order Completed. **No email** for request submitted (managers) or work order assigned (technician). In-app notifications in Phase 2 (ADR 0002) |
| Q6 | Permission to enter the unit and preferred times on requests? | "Permission to enter" flag + free-text availability in MVP; structured time windows in Phase 2 |
| Q10 | Manager dashboard contents | Counts by status, overdue work orders, 10 newest requests |
| Q11 | MFA | Phase 2 |
| Q12 | Public demo / hosting | Decided at the end of Phase 2; no paid cloud accounts |

### Open (not needed for MVP)

| # | Question | Proposed default |
|---|---|---|
| Q3 | Are technicians always internal, or can external vendors log in? | *Internal only; vendors in Phase 3* |
| Q4 | SLA targets; business hours or 24/7? | *Emergency 24/7, others business hours; configurable per organization (Phase 2)* |
| Q7 | Cost approval threshold? | *Phase 2; per-organization threshold* |
| Q8 | Can managers be restricted to specific properties? | *No; add `ManagerPropertyAssignment` later if needed* |
| Q9 | Data retention periods for closed work orders and photos? | *Keep work orders indefinitely; photos 3 years, then archive* |
| Q13 | Can users opt out of non-critical emails? | *Yes, per user (Phase 2)* |
| Q14 | Should the app offer a dark theme? | *Light only until then; decide in Phase 3. Components use theme tokens only, so a dark palette can be added later without touching them* |
