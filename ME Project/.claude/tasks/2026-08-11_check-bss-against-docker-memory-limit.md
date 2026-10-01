# T-27: Check .bss against the Docker memory limit
> Created: 2026-08-11 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-11_check-bss-against-docker-memory-limit.md`

---

## Description
`readelf` reports **261 MB** of `.bss` (`NOBITS`); binary is 4.1 MB. Linux
reserves rather than commits, but Docker limits count RSS.

## Why / Context
Run `docker stats` with several circuits loaded.

## Progress Log
- **2026-08-11** (Session #4): Opened after the 4-thread base landed with
  `.bss` sized for 64 circuits.

## Related
- none
