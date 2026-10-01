# T-60: Run the isolcpus kernel provisioning runbook on board 172.16.18.167
> Created: 2026-08-19 | Status: Active
> File: `tasks/2026-08-19_isolcpus-kernel-provisioning.md`

---

## Description
Run the isolcpus kernel provisioning runbook on board `172.16.18.167` — CPU3
pin works today but isn't kernel-isolated yet.

## Why / Context
ADR-30 pins the Core Logic thread to CPU3 via `pthread_attr_setaffinity_np()`,
but true exclusivity requires a one-time kernel-level `isolcpus`/`nohz_full`/
`rcu_nocbs` provisioning step on the board itself — this can't live in a
Dockerfile/Compose file/CI. Hardware-verified 2026-08-19 that the pin works
even without this step, but it isn't yet kernel-isolated (`NOT isolated`
warning in the log).

## Progress Log
- **2026-08-19**: Task opened following ADR-30 (PR #6 merged). Runbook exists
  at `Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md`.

## Related
- Depends on: none
- Relates to: ADR-30, T-24
