# ADR 0002: One-step triage, tenant-facing status and request history

- **Status:** Accepted
- **Date:** 2026-10-09
- **Amends:** D25 (`ARCHITECTURE.md` §3), §2, §7, §9, §23 Q2/Q5
- **Resolves:** ADR 0001 follow-ups "Request status history" and "'Scheduled' tenant status"

## Context

Planning the MVP slices (`docs/plans/mvp.md`, P1, P2 and A1) exposed four gaps:

- The request lifecycle forced Submitted → UnderReview → Accepted, and a work order was then created in a separate
  step. Routine triage took two or three actions, and an Accepted request without a work order was a valid state
  that every query and screen would have to handle.
- Q2 promised tenants a "Scheduled" status, but scheduling is Phase 2.
- The email list said "work order completed/closed (tenant)", which could mean one email or two. A Completed work
  order has not been checked by a manager yet and can still be reopened, so telling the tenant "done" at that point
  can be wrong.
- Work order transitions are recorded in `WorkOrderStatusHistory`, but nothing records request transitions, although
  the MVP requires an audit trail of status changes.

## Decision

1. **Triage in one step.** Managers accept or reject straight from Submitted or UnderReview. "Start review" is
   optional and only tells other managers that someone is on it.

   | Transition | Allowed | Rules |
   |---|---|---|
   | Submitted → UnderReview (start review) | Manager, Admin | Optional |
   | Submitted/UnderReview → Accepted | Manager, Admin | Takes a priority (defaults to the request's urgency), an optional due date and an optional technician (an active member with the Technician role in the same organization). Creates the work order in the same transaction and returns its id |
   | Submitted/UnderReview → Rejected | Manager, Admin | Reason required |
   | Submitted/UnderReview → Cancelled | The tenant who submitted it | Only before Accepted |
   | Accepted → Closed | System | Its work order is Closed |
   | Accepted → UnderReview | System | Its work order is Cancelled; the manager re-triages (accept again or reject) |

   - Accepting creates the work order as New. If a technician is given, it is then assigned (New → Assigned) in the
     same transaction, so its history shows both steps.
   - Invariant: an Accepted request always has exactly one active work order (ADR 0001, MVP 1:1).
   - Rejected, Cancelled and Closed are final in the MVP.
   - System transitions can only be called by the handler that keeps the request in sync with its work order
     (ADR 0005).

2. **Tenant-facing status.** Tenants see a simplified status:

   | Tenant sees | Request status | Work order status |
   |---|---|---|
   | Received | Submitted, UnderReview | — |
   | Received | Accepted | New |
   | Technician assigned | Accepted | Assigned |
   | In progress | Accepted | InProgress, OnHold, Completed |
   | Done | Closed | Closed |
   | Rejected | Rejected | — |
   | Cancelled | Cancelled | — |

   The assigned technician's first name is shown from "Technician assigned" onwards. "Scheduled" comes back with
   scheduling in Phase 2.

3. **Tenant emails.** Tenants get exactly two emails about a request: **request rejected** (carrying only the reason)
   and **request closed** ("Done"). Both are sent by post-commit handlers (ADR 0005). Nothing is sent when a request
   is submitted, a work order is assigned, completed or cancelled. Invites and password reset are unaffected.

4. **Request history.** `MaintenanceRequestStatusHistory` (from, to, by, at, reason) is append-only and mirrors
   `WorkOrderStatusHistory`. Every request transition, including submission, writes a row and raises a domain event
   (`RequestSubmitted`, then `RequestStatusChanged`). For a system transition, *by* is the user whose work order
   action caused it.

## Consequences

- Routine triage is one action, and no query or screen has to handle "Accepted without a work order".
- "Awaiting triage" means Submitted or UnderReview (dashboard and the manager's default filter).
- A tenant can still cancel while the request is UnderReview.
- After a work order is cancelled, the tenant sees "Received" again and gets no email. This can look like a step
  back, but it never tells the tenant something that isn't true.
- Reassigning is only allowed from Assigned (§7). If a technician drops out mid-job, the manager cancels the work order
  and accepts the request again.
- **Domain tests:**
  - the request transition × role matrix, including accepting from both Submitted and UnderReview, and the denied cases
  - rejecting without a reason fails
  - every transition writes a history row and raises an event
- **Integration tests:**
  - one test per row of the tenant status mapping
  - closing sends exactly one email to the tenant; Completed sends none
  - the reject email contains only the reason
- What the tenant sees when a request has several work orders stays open until Phase 2 lifts the 1:1 restriction
  (ADR 0001).
