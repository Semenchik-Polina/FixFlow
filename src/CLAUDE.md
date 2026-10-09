# Backend (src/) — Agent Guide

Backend-specific rules. The root `CLAUDE.md` rules (tenant isolation, authorization, time, IDs) still apply.
Background: `ARCHITECTURE.md` §4, §7–§15.

---

## 1. Project map

| Project | Responsibility | May reference |
|---|---|---|
| `FixFlow.Domain` | Entities, value objects, state machines, domain events, domain errors | NodaTime, Stateless only |
| `FixFlow.Application` | Commands/queries, handlers, validators, authorization handlers, interfaces (`IBlobStorage`, `IEmailSender`, ...) | Domain |
| `FixFlow.Infrastructure` | EF Core `DbContext`, configurations, migrations, interceptors, Identity stores, Blob, Email, Redis, MassTransit | Application, Domain |
| `FixFlow.Api` | Endpoint mapping, SignalR hubs, auth setup, Problem Details, DI composition, serves SPA in prod | all of the above |
| `FixFlow.Contracts` | Integration event records shared with workers | nothing |
| `FixFlow.Hosting` | Shared Serilog, OpenTelemetry, health checks, resilience setup | — |
| `FixFlow.Workers.*` | Phase 2 background workers | Contracts, Hosting, Application/Infrastructure as needed |

Never add a reference that points "inward → outward" (e.g. Domain → Application, Application → Infrastructure).

---

## 2. Feature layout

One use case = one folder, grouped by module:

```
FixFlow.Application/WorkOrders/
├─ AssignTechnician/
│  ├─ AssignTechnicianCommand.cs      # record + result DTO
│  ├─ AssignTechnicianHandler.cs
│  └─ AssignTechnicianValidator.cs
├─ GetWorkOrders/                     # query + handler + response DTO
├─ WorkOrderOps.cs                    # OperationAuthorizationRequirement definitions
├─ WorkOrderAuthorizationHandler.cs
└─ WorkOrderQueryExtensions.cs        # .VisibleTo(user)

FixFlow.Api/Endpoints/WorkOrderEndpoints.cs   # MapGroup("/api/v1/work-orders")
```

- Endpoints are thin: bind → `sender.Send(command)` → map result to `TypedResults`. No business logic in endpoints.
- Every endpoint declares `.RequireAuthorization(<policy>)`; anonymous ones use `.AllowAnonymous()` explicitly.
- Every endpoint declares its responses (`Produces<T>()`, `ProducesProblem(...)`) so the OpenAPI client is accurate.

---

## 3. Domain

- Aggregates (`MaintenanceRequest`, `WorkOrder`, `Property`, ...) have private setters and are changed only via methods
  (`workOrder.Assign(technicianId, actor, clock)`).
- Creation through static factory methods that enforce invariants; EF uses a private parameterless constructor.
- Status changes go through the aggregate's Stateless state machine; each transition records history and raises a
  domain event. Allowed transitions/roles: `ARCHITECTURE.md` §7.
- Business rule violations throw `DomainException` (mapped to **422** Problem Details);
  concurrency conflicts map to **409**; not found / not authorized to see → **404**.
- Domain events: past-tense records (`WorkOrderAssigned`), collected on the aggregate, dispatched in two phases during
  `SaveChanges` (ADR 0005):
  - **in-transaction** handlers keep other aggregates consistent (WO Closed → request Closed) and commit or roll back
    with the change. They change tracked aggregates only — never email, HTTP, Blob or other I/O;
  - **post-commit** handlers run side effects (email, cache invalidation): catch and log failures (IDs only), never
    fail the action, never write business data.

---

## 4. Persistence (EF Core + PostgreSQL)

- One `FixFlowDbContext`; schema per module (`requests`, `workorders`, `properties`, `identity`, ...).
- Entity configuration in `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` (`IEntityTypeConfiguration<T>`).
  No data annotations on domain entities.
- snake_case naming for tables/columns (`UseSnakeCaseNamingConvention()`).
- Organization-owned entities implement `IOrganizationOwned`; `OrganizationId` is set by a `SaveChanges` interceptor
  and filtered by a global query filter. Soft delete (`DeletedAt`) is a second global filter.
- Auditing fields (`CreatedAt/By`, `UpdatedAt/By`) are set by an interceptor using `IClock` and the current user.
- Concurrency: `xmin` row version on `WorkOrder` and `MaintenanceRequest`.
- NodaTime mapping via `UseNodaTime()`: `Instant` ↔ `timestamptz`, `LocalTime` ↔ `time`, `LocalDate` ↔ `date`.
- Queries: `AsNoTracking()`, project to DTOs with `Select`, always paginate lists. No `ToList()` before filtering.

### Migrations

- Name migrations by intent: `AddWorkOrderSlaDueAt`, not `Update3`.
- After adding, review the SQL: `dotnet ef migrations script --idempotent` (only the new part).
- Never edit or delete a migration that is already merged; add a corrective one.
- Don't read `*.Designer.cs` or `*ModelSnapshot.cs` — they're generated.

---

## 5. Authorization pattern

```csharp
// endpoint: role-level
group.MapPost("/{id:guid}/assign", Assign).RequireAuthorization(Policies.CanAssignWorkOrders);

// handler: record-level
var wo = await db.WorkOrders.FirstOrDefaultAsync(w => w.Id == cmd.Id, ct);
if (wo is null || !(await authz.AuthorizeAsync(user, wo, WorkOrderOps.Assign)).Succeeded)
    return Result.NotFound();

// lists
var items = await db.WorkOrders.VisibleTo(user).Where(...).Select(...).ToPageAsync(page, ct);
```

- Write checks against operations (`WorkOrderOps.*`), not scattered `IsInRole` calls.
- Tenant-facing responses use separate DTOs without internal fields.

### Organization context (memberships)

- `User` is global; access to an organization is an `OrganizationMembership` (Active/Suspended) with `MembershipRole`s.
- `OrganizationContextMiddleware` (Api) reads `X-Organization: {slug}`, loads the user's **active** membership
  (via `HybridCache`, key `membership:{userId}:{slug}`), returns **404** if none, and an `IClaimsTransformation`
  adds `OrganizationId` + that membership's roles to the principal.
- Handlers, interceptors, query filters and authorization handlers read the org and roles **only** from `ICurrentUser`.
  Never read them from `User` entities, request bodies or route values.
- Organization-less endpoints (`/api/v1/me*`, auth) are explicitly marked and must not touch organization-owned data.
- Any change to memberships/roles must invalidate the tag `user:{userId}:memberships`.
- SignalR hub validates `?org={slug}` the same way on connect.

---

## 6. Messaging (Phase 2)

- Integration events live in `FixFlow.Contracts`: immutable records, past tense, carry IDs + minimal data.
  Breaking change → new type (`...V2`).
- Publish only via the MassTransit EF Core outbox (same transaction as the business change). Never publish directly
  from a handler without the outbox.
- Consumers must be idempotent and must not assume ordering.

---

## 7. Logging

- Structured logging with message templates (`"Work order {WorkOrderId} assigned"`), never string interpolation.
- Log IDs (`WorkOrderId`, `OrganizationId`, `UserId`) — never names, emails, phone numbers, addresses, tokens or SAS URLs.

---

## 8. Tests

| Change | Test project | Notes |
|---|---|---|
| Domain rule / transition | `tests/FixFlow.Domain.Tests` | Cover every transition × role (allowed and denied) |
| Handler / validator / auth handler | `tests/FixFlow.Application.Tests` | |
| Endpoint | `tests/FixFlow.Api.IntegrationTests` | `WebApplicationFactory` + Testcontainers; include a **cross-tenant** test (other org's record → 404; member of A but not B; suspended membership; wrong `X-Organization`) |
| Time logic | Domain tests | `FakeClock`; weekends, holidays, both DST transitions |

- Test naming: `Method_Scenario_ExpectedResult`.
- Use the shared fixtures/builders (`TestData.WorkOrder()...`) instead of building entities by hand.
- Run focused: `dotnet test <project> --filter "FullyQualifiedName~<Name>"`.
