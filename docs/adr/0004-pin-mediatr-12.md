# ADR 0004: Pin MediatR to the 12.x line

- **Status:** Accepted
- **Date:** 2026-10-09
- **Amends:** `ARCHITECTURE.md` §5, §22

## Context

MediatR carries the application layer: commands and queries, the FluentValidation pipeline behaviour, and in-process
domain-event notifications (§4, §12, ADR 0005).

From version 13, MediatR is distributed under a commercial licence, with a licence key and a free community tier.
The 12.x line is the last one released under Apache-2.0.

FixFlow is a public, MIT-licensed repository (D23). Its dependencies should be under permissive open-source licences,
and the repo should hold no licence keys.

## Decision

1. Pin MediatR to the latest 12.x release in central package management (plan T0.4).
2. Dependabot ignores MediatR versions ≥ 13 (plan T0.2).
3. Use only the core abstractions:
   - `IRequest` / `IRequestHandler`
   - `INotification` / `INotificationHandler`
   - `IPipelineBehavior`
   - `ISender` / `IPublisher`

## Consequences

- MediatR gets no upstream fixes or new features. Its 12.x packages target netstandard2.0, so they run on .NET 10.
- If 12.x gets a vulnerability or stops working on a future .NET, MediatR is replaced by a small in-house mediator or
  a permissively licensed alternative, recorded in a new ADR. Keeping to the core abstractions keeps that swap cheap.
- The same licensing rule kept FluentAssertions v8+ out: tests use xUnit's built-in `Assert` (plan A12).
