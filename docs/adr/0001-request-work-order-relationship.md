# ADR 0001: One request → many work orders; requests close when their work orders are done

- **Status:** Accepted
- **Date:** 2026-10-09
- **Amends:** D8 (`ARCHITECTURE.md` §3)

## Context

The domain model had gaps that would surface as soon as the `MaintenanceRequest` and `WorkOrder` aggregates were written:

- D8 said a request "can be rejected or merged", but the request lifecycle had no merge state or transition.
- §7 said closing a work order "closes the linked request", but the request lifecycle ended at `Accepted`.
  Tenants are promised a "Done" status (Q2).
- The request ↔ work order relationship was unspecified. The entity table allowed work orders created
  "directly by a manager", while the Phase 1 scope says "create from request" and D25 says triage always comes first.

## Decision

1. **No merge in MVP.** A duplicate request is rejected with a reason. Merging is revisited in Phase 2.
2. **One request → many work orders.** `WorkOrder.RequestId` is required; the foreign key lives on the work order,
   so a request can have several work orders (e.g. a plumber and an electrician for one leak).
   Creating a work order without a request is deferred; it would make `RequestId` nullable through a new migration.
3. **Requests gain a `Closed` state**, driven by their work orders:

   | Transition | Allowed | Rules |
   |---|---|---|
   | Accepted → Closed | System | At least one work order is Closed and none is still open (the rest are Cancelled) |
   | Accepted → UnderReview | System | Every work order of the request is Cancelled; the manager re-triages (reject, or accept again) |
   | Closed → Accepted (reopen) | Manager, Admin | Reason required; used to add a further work order |

   Work orders can only be created while the request is `Accepted`.
4. System transitions go through the `MaintenanceRequest` state machine (non-negotiable rule 3). They run in a
   handler reacting to `WorkOrderStatusChanged`, which re-evaluates the request from the current state of all its
   work orders.

### MVP restriction: one active work order per request (1:1)

The MVP ships a subset of this design; Phase 2 lifts the restriction.

- A request has at most one work order that is not `Cancelled`. The `MaintenanceRequest` aggregate allows creating a
  work order only while it is `Accepted` and has no active work order.
- A partial unique index `work_orders (organization_id, request_id) WHERE status <> 'Cancelled'` backs this up.
- The transitions above reduce to: the request closes when its work order is Closed, and returns to UnderReview when
  it is Cancelled (the manager can then accept again and create a replacement work order).
- `Closed` is final: no request reopen in MVP. Rework before closure uses the work order's own reopen
  (Completed → InProgress).

**Lifting it in Phase 2:** a new migration drops the partial unique index, the aggregate drops the "no active work
order" check, and the reopen transition is enabled. No schema redesign is needed.

## Consequences

- Index `work_orders (organization_id, request_id)` — unique and partial in MVP, plain from Phase 2.
- **MVP domain tests:**
  - work order Closed → request Closed
  - work order Cancelled → request UnderReview → accepted again → a replacement work order can be created
  - creating a second work order while one is active → 422 (the unique index is only a backstop)
  - creating a work order on a Closed request → 422
- **Phase 2 domain tests** (added when the restriction is lifted):
  - one work order Closed and another Cancelled → request Closed
  - one Closed, one still open → request stays Accepted
  - all Cancelled → request back to UnderReview
  - reopen without a reason → 422; reopen by Tenant or Technician → denied
- **Phase 2 concurrency:** two work orders of one request can change at the same time, so the re-evaluation handler
  must be idempotent. `xmin` on `MaintenanceRequest` turns the race into a 409, and the handler retries. This cannot
  happen with 1:1.
- Record-level authorization is unchanged: a tenant still sees work orders that belong to their units.

## Open follow-ups

- **Tenant-facing status with several work orders (Q2), Phase 2 — still open.** In MVP the status maps directly from
  the single active work order (mapping in ADR 0002). What the tenant sees when one work order is done and another is
  in progress must be decided before the restriction is lifted.
- **Request status history — resolved by ADR 0002.** Every work order transition writes `WorkOrderStatusHistory`, but
  there was no equivalent entity for request transitions, although the MVP requires an audit trail of status changes.
  ADR 0002 adds `MaintenanceRequestStatusHistory`.
- **"Scheduled" tenant status (Q2) — resolved by ADR 0002.** Q2 listed "Scheduled", but scheduling is Phase 2.
  ADR 0002 drops it from the MVP; it comes back with scheduling.
