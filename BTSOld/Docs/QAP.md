# Quality Assurance Plan (QAP)
**Document ID:** BTS-QAP-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Quality Objectives

- Zero critical bugs in production hardware communication
- All EF Core migrations tested before merge
- CI/CD build must pass before any release
- Code reviewed before merge to main
- All protocol changes documented in PROTOCOL.md

---

## 2. Quality Activities

| Activity | Description | Frequency |
|---|---|---|
| Code review | Peer review of all PRs | Per PR |
| Build verification | GitHub Actions automated build | Every push |
| Integration test | Hardware-in-loop test | Per release |
| Protocol compliance | Verify packet encoding/decoding | Per protocol change |
| DB migration test | Apply migration to clean DB | Per migration |
| Log review | Serilog output review post-deploy | Post-deploy |
| Audit log review | Verify all operations logged | Weekly |

---

## 3. Standards and Guidelines

| Area | Standard |
|---|---|
| C# code style | Microsoft C# coding conventions |
| Blazor components | Single responsibility per component |
| Database | EF Core migrations only — no manual schema changes |
| Commits | Conventional commit messages (fix:/feat:/chore:/docs:) |
| Secrets | No secrets in source code — environment variables only |
| Logging | Serilog structured logging for all significant events |

---

## 4. Non-Conformance Process

1. Identify issue (build failure / bug / audit finding)
2. Log in GitHub Issues with severity label
3. Assign to developer
4. Fix in feature branch → PR → review → merge
5. Verify fix in CI/CD
6. Close issue with resolution note

---

## 5. Metrics

| Metric | Target | Current |
|---|---|---|
| Build success rate | > 95% | Tracked via GitHub Actions |
| Open critical bugs | 0 | Tracked via GitHub Issues |
| Documentation coverage | All components have DDD entry | 100% for v0.5 |
| Migration success | 100% on clean DB | Verified per release |
