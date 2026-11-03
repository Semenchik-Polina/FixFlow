# E2E tests (tests/web-e2e/) — Agent Guide

E2E-specific rules. The root `CLAUDE.md` rules still apply (no secrets, no real personal data).
Background: `ARCHITECTURE.md` §19, plan P36 in `docs/plans/mvp.md`. Run all commands from `tests/web-e2e/`.

---

## 1. What this package is

- Playwright (Chromium) in its own npm package, not part of `web/`: it can't import from `web/src`, so tests assert the
  UI text literally. Its lint, type-check and prettier copy `web`'s tooling at the same pinned versions.
- It runs against the Compose `full` profile at https://localhost:8443 (README "Full profile"); nothing here starts the
  stack. That profile runs the built API image with the SPA inside, so a change in `src/` or `web/` reaches these tests
  only after `docker compose --profile full up -d --build --wait`.
- Set `FIXFLOW_AUTH_PERMIT_LIMIT=1000` in `.env` before starting the profile: every request reaches the API from
  Docker's gateway address, so all tests share one auth rate-limit budget (10 a minute by default).
- Only the flows `docs/plans/mvp.md` lists as a task's "E2E:" item belong here; each milestone adds its own. Everything
  else is a component or integration test.

---

## 2. Layout

```
tests/web-e2e/
├─ playwright.config.ts   # 2 workers, fullyParallel, retries: 0, traces and screenshots on failure only
├─ tests/                 # one *.spec.ts per flow
└─ support/
   ├─ fixtures.ts         # test, expect, and the fixtures signedUpAccount and mailpit
   ├─ testData.ts         # aNewAccount(), email(), organizationSlug(), name(prefix), password()
   ├─ mailpit.ts          # Mailpit.messageTo(recipient), resetLinkIn(body)
   └─ auth.ts             # signIn(page, account), signOut(page)
```

---

## 3. Writing tests

- Import `test` and `expect` from `../support/fixtures`, not from `@playwright/test`, so the fixtures are there.
- Every test signs up its own organization: tests share no state and never reset or clean anything up.
- A test whose subject isn't signup takes `signedUpAccount`, which signs up through the API with Playwright's `request`.
  That has its own cookies, so the page starts signed out; sign in with `signIn(page, account)`. Only the signup flow
  goes through the signup UI.
- Fake data comes from `support/testData.ts` (`*.example.test`, random hex), which mirrors the integration tests'
  `TestData`. No faker, no literal passwords.
- Mail: `mailpit.messageTo(recipient)` waits for exactly one message to that address. Read only by recipient: the
  workers share one Mailpit, and its volume keeps mail from earlier runs.
- Locators by role or label, as in `web/` (`web/CLAUDE.md` §8). Password fields need `{ exact: true }`, since their
  "Show password" toggle matches too.
- Wait through web-first assertions (`expect(locator).toBeVisible()`, `expect(page).toHaveURL(...)`), never fixed
  sleeps. Await every Playwright call; lint's `no-floating-promises` catches a missing `await`.
- `retries: 0` is deliberate: a flaky test fails the run. Fix the cause; don't add retries.

---

## 4. Commands

```bash
npm ci && npx playwright install chromium     # once
npx playwright test                           # or one flow: npx playwright test tests/password-reset.spec.ts
npx playwright show-report                    # the HTML report, with a trace for each failed test
npm run lint && npm run typecheck             # before done
```

CI runs the suite as a required check (the E2E job) against images built from the PR.
