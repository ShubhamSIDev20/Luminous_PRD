# ADR-24: Git version control adopted; `main`/`develop` branch workflow
> Date: 2026-08-14/15 | Session: #9/#10 | Status: Accepted
> File: `DECISIONS/2026-08-14_git-version-control-main-develop-workflow.md`

---

**Context:** Project had no `.git` through session #8 (declined 2026-08-06).
The developer requested version control once the codebase reached 248 files.

**Decision:** `git init`, `.gitignore` for build artifacts and machine-local
settings, initial commit (103 files). Subsequent work lands via feature
branches merged to `main` through GitHub PRs (`add-me-primary-board-app`,
`feature/step-execution-design` — both merged, then deleted once GitHub
confirmed full ancestry). **`develop`** created off `main` 2026-08-15 as the
ongoing branch for future work — `main` stays the stable/merged snapshot.

**Consequence:** New coding sessions commit to `develop` (or a fresh feature
branch off it), not `main` directly.

**Related**
- Supersedes: the no-git-repo state (developer declined 2026-08-06)
- Relates to: T-55, T-56
