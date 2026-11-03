# Frontend (web/) — Agent Guide

Frontend-specific rules. The root `CLAUDE.md` rules still apply (cookie auth, never edit generated files).
Background: `ARCHITECTURE.md` §16. Run all commands from `web/`.

---

## 1. Structure

```
web/src/
├─ app/                 # providers, router.tsx, theme.ts, queryClient.ts, realtime.ts
│  └─ shell/            # the /:orgSlug layout: per-org query client, app bar, switcher, nav, RequireRoles
├─ features/
│  ├─ auth/             # login, signup, password reset, invite acceptance
│  ├─ organizations/    # picker, create organization, "organization not found"
│  ├─ dashboard/
│  ├─ requests/
│  ├─ work-orders/
│  └─ properties/
│     ├─ pages/         # route components
│     ├─ components/    # feature-only components
│     └─ hooks/         # feature-only hooks (wrapping generated ones if needed)
├─ shared/              # reusable components, hooks, utils (e.g. formatInstant)
│  ├─ forms/            # rules (zod/mini), applyServerErrors, FormTextField, PasswordField
│  ├─ routing/paths.ts  # globalPaths, orgSlugFromPath, isPublicPage
│  └─ session/          # useCurrentUser, useCurrentOrganization, useGlobalQueryClient, hasAnyRole
└─ api/
   ├─ http.ts           # fetch mutator used by the generated client
   └─ generated/        # orval output — DO NOT EDIT, DO NOT READ
```

- A feature never imports from another feature's internals; move shared code to `shared/`.
- One component per file; file name = component name (`WorkOrderStatusChip.tsx`).
- UI text lives outside components (D27): `shared/strings.ts` for shared text, `features/<feature>/strings.ts` for the rest.
- React 19: pass `ref` as a plain prop (no `forwardRef`); `useRef` always gets an initial value.

---

## 2. API access

- Use **only** the generated client/hooks from `api/generated`. No direct `fetch`/`axios` in components.
- After any backend endpoint/DTO change: `dotnet build ../src/FixFlow.API` first (it writes
  `src/FixFlow.API/openapi.json`, which orval reads), then `npm run gen:api`, then fix type errors. Commit
  `openapi.json` and `api/generated` with the change; CI fails when either is stale.
- `api/http.ts` sends cookies (`credentials: 'include'`), parses Problem Details into a typed `ApiError`,
  adds `X-Organization: {orgSlug}` taken **only** from the current URL, and redirects to `/login` on 401.
- To read the shape of an endpoint, read the backend endpoint/DTO or the OpenAPI document — not the generated files.

---

## 3. Server state (TanStack Query)

- All API data goes through TanStack Query. Never fetch in `useEffect` or copy query data into `useState`.
- Use generated query keys; after a mutation, invalidate the affected keys (list + detail).
- Optimistic updates only for simple, reversible changes (e.g. status chip); always roll back on error.
- Realtime: `app/realtime.ts` owns the single SignalR connection and maps server events to
  `queryClient.invalidateQueries(...)`. Components don't subscribe to SignalR directly.
- UI-only state: `useState` / context. No Redux/Zustand.

---

## 4. UI (MUI)

- Theme lives in `app/theme.ts` (light only; no dark mode or theme toggle — `ARCHITECTURE.md` Q14). Use theme tokens via `sx` / `styled` — no hard-coded colors,
  spacing or font sizes.
- Lists: `@mui/x-data-grid` with server-side pagination/sorting. Dates: `@mui/x-date-pickers`.
- Icons: `@mui/icons-material` only.
- Import MUI components and icons by path (`@mui/material/Button`, `@mui/icons-material/Menu`), never from the
  package root; lint enforces it.
- Every page handles loading (skeletons), empty and error states.
- Technician screens are mobile-first; check layouts at 360 px width.
- Accessibility: labelled form fields, buttons with text or `aria-label`, keyboard-reachable dialogs.

---

## 5. Forms

- React Hook Form + Zod (`zodResolver`). Import `z` from `zod/mini`, which tree-shakes; plain `zod` adds ~45 kB
  gzipped to every form page. One schema per form, colocated with the form, built from `shared/forms/rules.ts`.
- Client validation mirrors the server; the server is authoritative. `applyServerErrors(error, setError, fields)`
  maps Problem Details field errors back to form fields (and everything else to the form's `root` error).
- Disable the submit button while pending; send an `Idempotency-Key` for create actions.

---

## 6. Routing, auth and roles

- Routes defined in `app/router.tsx`, lazy-loaded per feature. `/login` and the organization picker are global;
  all app routes live under `/:orgSlug/...`.
- Global pages are listed in `shared/routing/paths.ts` (`globalPaths`). The router and the `X-Organization` header both
  read that list, so a new global page goes there first (and into the backend's reserved slugs).
- `createRoutes(queryClient)` returns fresh route objects on each call: a router updates the lazy routes it loads, so
  routers must not share them.
- `useCurrentUser()` (`shared/session`, backed by `GET /api/v1/me`) provides the user and their memberships
  (organization + roles). `useCurrentOrganization()` derives the active organization and its roles from the URL slug.
- **One query client per organization** (plan P35): the `/:orgSlug` layout renders `OrganizationQueryScope` keyed by
  the slug, so `useQueryClient()` and the generated hooks in org pages use that organization's client, and a switch
  starts with an empty one. Keys need no slug. Org-less data (`/me`) lives in the global client: pass
  `useGlobalQueryClient()` as the generated hook's `queryClient` argument when an org page reads it.
- Organization switcher in the app header navigates to the other organization's home `/{otherSlug}`.
- A 401 sends the browser to `/login`, except on the signed-out pages (`isPublicPage`), which never call `/me`.
- The SignalR connection is per active organization (`?org={slug}`); reconnect on switch.
- Role-based route guards (`RequireRoles` layout routes) and nav items (`roles` in `app/shell/navItems.ts`) are
  **UX only** — the API enforces security.
- Never store tokens or user data in `localStorage`; it may hold harmless UI preferences only.

---

## 7. Dates and time

- API timestamps are UTC ISO strings. Display with `shared/utils/formatInstant(value, property.timeZone)`
  (wraps `Intl.DateTimeFormat`). Never format with the browser's zone for building-related times.
- Inputs for appointments/schedules are local date/time in the property's zone; send them as the API expects
  (local value + zone), don't convert to UTC on the client.

---

## 8. Tests

- Unit/component: Vitest + React Testing Library; query by role/label, not by class or test id when avoidable.
- API mocking: MSW handlers generated by orval; override per test when needed (`server.use(...)` from `src/test/server.ts`).
  Unhandled requests fail the test.
- Helpers in `src/test/`: `renderWithProviders(ui)` (theme + a non-retrying query client) and `renderRoute(path)`
  (the app's routes in a memory router); `fixtures.ts` has `givenSignedIn(aMembership('acme', roles))` for `/me` and
  `problem(...)` / `validationProblem(...)` for error answers. Org pages need `givenSignedIn`, or their layout shows
  "Organization not found". A test that depends on the `X-Organization` header needs a browser router, because
  `http.ts` reads the slug from `window.location`.
- E2E: Playwright in `tests/web-e2e/`, its own package with its own guide (`tests/web-e2e/CLAUDE.md`).
- Run focused: `npx vitest src/features/work-orders`.
- Before done: `npm run lint && npm run typecheck && npm test`.
