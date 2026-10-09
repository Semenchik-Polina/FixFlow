# ADR 0003: Defer email confirmation to Phase 2

- **Status:** Accepted
- **Date:** 2026-10-09
- **Amends:** S4 (`ARCHITECTURE.md` §17)

## Context

S4 required email confirmation for every account. Through Phase 2 the app runs only on the developer's machine (D20),
and Mailpit catches all mail (D21), so nobody else can sign up or receive mail.

A confirmation flow is a slice of its own: tokens, an unconfirmed account state, a resend endpoint, UI pages and extra
E2E steps. While the app is local it protects nothing.

Invited users already prove they control their address: the invite token reaches them only through an emailed link.

## Decision

1. **Self-signup signs the user in straight away.** Identity runs with `RequireConfirmedEmail = false`, and the email
   is stored as unconfirmed.
2. **Accepting an invite as a new user marks the email as confirmed** (`EmailConfirmed = true`).
3. **Phase 2 requires confirmation for self-signup**, before any hosting goes live (§20). Accounts that are still
   unconfirmed at that point must confirm at their next sign-in.

## Consequences

- **Accepted risk while the app is local:** someone can sign up with an address they don't control. This is limited
  because:
  - password-reset mail goes to the real owner, and a reset ends the account's other sessions, so the owner can take
    the account back
  - invite tokens are emailed only to the invited address, so someone holding an account under another person's
    address can't accept the invites sent to it
  - the MVP has no way to change an account's email (profile edits cover the name only). Adding one later needs
    confirmation of the new address.
- Signup and E2E tests skip a confirmation step. Phase 2 adds it back.
- The MVP security review (plan T9.3) lists S4 email confirmation as deferred to Phase 2.
- The rest of S4 is unchanged: invite and reset tokens are single-use and expire after 72 h and 1 h.
