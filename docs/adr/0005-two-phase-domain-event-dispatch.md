# ADR 0005: Dispatch domain events in two phases (in-transaction and post-commit)

- **Status:** Accepted
- **Date:** 2026-10-09
- **Amends:** D12 (`ARCHITECTURE.md` §3), §12, `src/CLAUDE.md` §3

## Context

§12 and `src/CLAUDE.md` said domain events are "dispatched after `SaveChanges`". The handlers fall into two kinds
with opposite needs:

- **Consistency between aggregates.** A Closed work order closes its request, and a Cancelled one sends the request
  back to UnderReview (ADR 0001, ADR 0002). The request change must commit together with the work order change. Done
  in a second save, a crash or an `xmin` conflict between the saves leaves a Closed work order on an Accepted request.
- **Side effects.** Tenant emails (ADR 0002) and cache invalidation must not happen if the change rolls back, and
  their failure (for example SMTP being down) must not fail or undo the user's action.

Dispatching after `SaveChanges` suits side effects but cannot make the sync atomic. The transactional outbox that
would cover both only arrives in Phase 2 (D12).

## Decision

1. Aggregates collect domain events. `FixFlowDbContext` dispatches them in-process during `SaveChangesAsync`, in two
   phases.
2. **In-transaction phase**, before anything is written:
   - Collected events go to in-transaction handlers.
   - These handlers change only tracked aggregates, and only through their domain methods and state machines.
   - Events raised by these handlers are dispatched in the same phase until none are left. The number of rounds is
     bounded, so a cycle throws.
   - Then all changes are written in one database transaction. Any exception means nothing is committed, and it maps
     to 422 or 409 as usual.
3. **Post-commit phase**, after the transaction commits:
   - All events of the unit of work go to post-commit handlers.
   - Each handler's exceptions are caught and logged with the event type and IDs only, and never rethrown.
     Refined by ADR 0007: the log also has the exception's types and stack traces, without its messages.
   - Post-commit handlers don't write business data.
   - When a command spans an explicit transaction (several saves), the post-commit phase runs once, after that
     transaction commits.
4. A handler's phase is explicit in its type: two handler interfaces, named in plan T0.9. In-transaction handlers
   never send email or call HTTP, Blob or other external services.
5. **Phase 2:** integration events are written to the outbox during the in-transaction phase, and email moves to the
   `Notifications` worker. Post-commit handlers remain for in-process work such as cache invalidation.

## Consequences

- The request sync commits or rolls back with the work order change. A stale request row turns the whole action into
  a 409.
- Side effects are best-effort in the MVP. If the process crashes between the commit and the post-commit phase, the
  email is lost while the action stands. This is accepted while the app runs locally; the Phase 2 outbox removes it.
- Failed email sends are logged and counted (§18), without addresses.
- **Tests** (plan T0.9, using a test-only entity):
  - a change made by an in-transaction handler commits atomically with the change that triggered it
  - a failing in-transaction handler rolls back both
  - a failing post-commit handler neither rolls back the change nor fails the request
