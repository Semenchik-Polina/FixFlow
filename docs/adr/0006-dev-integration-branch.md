# ADR 0006: Integrate on `dev`, merge milestones into `main`

- **Status:** Accepted
- **Date:** 2026-10-20
- **Amends:** `ARCHITECTURE.md` §3 (D22), §20; plan T0.2

## Context

D22 described the repo as trunk-based with short-lived branches, and T0.2 made merges squash-only.

In practice, work is integrated on a `dev` branch, and `main` receives it at milestones (PR #1 brought in T0.2–T0.5).
That keeps `main` at finished milestones, which is worth keeping. Parts of the setup didn't fit it:

- Squash-merging `dev` into `main` collapsed a whole milestone into one commit, and gave `main` a commit `dev` didn't
  have, so `dev` had to be synced back by hand.
- Squash-only merges also collapse a bigger feature branch whose commits are worth keeping on their own.
- Dependabot targets the default branch unless told otherwise, so its updates landed on `main` only (#7).

## Decision

1. `dev` is the integration branch. Feature branches start from `dev`, are short-lived, and are deleted on merge.
2. Feature PRs into `dev` are squash-merged by default; the PR title is the conventional commit.
   A bigger branch whose commits are each focused conventional commits may be merged with **Create a merge commit**
   instead, to keep them.
3. `dev` merges into `main` at milestones (e.g. the end of T0) through a PR merged with **Create a merge commit** —
   never squash or rebase, which both rewrite `dev`'s commits.
4. Rebase merge stays disabled.
5. Dependabot version updates target `dev`.
6. `main` stays the default branch and holds finished milestones only.

## Consequences

- `main` gets `dev`'s commits unchanged plus one merge commit per milestone, so the branches don't diverge and `dev`
  needs no syncing after a release.
- The repo must allow merge commits. Choosing squash or a merge commit for a feature PR is a call made in the merge
  dialog; a branch merged with a merge commit needs a tidy history first (interactive rebase), since all its commits
  land on `dev` and later on `main`.
- History on `dev` and `main` is not strictly linear once merge commits appear.
- CI runs on every PR, into either branch. The push job runs on `main` only, so each milestone is checked again there.
- Dependabot reads its config from the default branch, so `target-branch: dev` takes effect once it reaches `main`.
  Security updates ignore `target-branch` and still target `main`; merge `main` into `dev` after one.
- The repo page shows `main`, so visitors see the last milestone rather than work in progress.
