# WebAppME (BatteryTestingSystem) — AI Memory Index
> Updated: 2026-08-11T00:00:00Z | Session #2

## What Is This?
A battery testing management system: an ASP.NET Core 8 app combining a Blazor Server UI, REST API, and MCP server to control test hardware (devices/boards/channels), run test programs, manage battery sessions, and export results.
**Stack:** C# / .NET 8 | ASP.NET Core (Blazor Server + MVC) | EF Core 8 + SQLite/SQLCipher | Hangfire | Serilog

---

## 🗂️ Memory Files

| File | Purpose | Read When |
|------|---------|-----------|
| [AGENT.md](AGENT.md) | Rules + live project state | Every session — mandatory |
| [SESSION.md](SESSION.md) | Session INDEX — links to `sessions/*.md` | Every session — mandatory |
| [TASKS.md](TASKS.md) | Task INDEX — links to `tasks/*.md` | Picking up work |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System design + components | Understanding the system |
| [CODEBASE_MAP.md](CODEBASE_MAP.md) | Annotated code tree + key functions | Navigating code |
| [DECISIONS.md](DECISIONS.md) | ADR INDEX — links to `DECISIONS/*.md` | Before changing architecture |
| [CONTEXT/api.md](CONTEXT/api.md) | API INDEX — links to `CONTEXT/api/*.md` | Working on REST endpoints |
| [CONTEXT/auth.md](CONTEXT/auth.md) | Auth/Identity/JWT flow | Auth-related work |
| [CONTEXT/database.md](CONTEXT/database.md) | Schema, entities, encryption, repositories | DB/schema work |
| [CONTEXT/frontend.md](CONTEXT/frontend.md) | Blazor Server structure | UI work |
| [CONTEXT/background-operations.md](CONTEXT/background-operations.md) | Hangfire background jobs, session-file creation/storage, DBC upload & parameter push | Scheduler/export jobs, session file logic, DBC work |

---

## 📋 Current Status
**Active Task:** None — see backlog
**Last Session:** 2026-08-11 — Fixed 2 device-multiplexing response bugs (buffer truncation + session board-number identity), updated `HardwareSimulator` for multi-channel-per-socket testing, committed & pushed (`3339f86`)
**Open Tasks:** 4 backlog | 0 active | 0 blocked | 1 done

Backlog highlights: wire up JWT bearer validation (T-1), add `[Authorize]` to `DeviceController` (T-2), review duplicate `AddControllers` call (T-3), verify `IRepository` namespace typo (T-4). See [TASKS.md](TASKS.md).
Still open: an actual hardware-in-the-loop run of `HardwareSimulator` against a live server (plan `docs/superpowers/plans/2026-08-07-device-connection-multiplexing.md`, Task 5 Step 4) has not been executed yet.

---

## ⚡ Quick Start For New Agents
1. Read this file ✓
2. Read `AGENT.md` → understand rules + current state
3. Read `SESSION.md` → find "📍 Current Session", then open that linked `sessions/{file}`
4. Read `TASKS.md` → pick a row, open its linked `tasks/{file}` for full detail
5. Load relevant `CONTEXT/` file for your task (api, auth, database, frontend, background-operations)
6. Start — create your own `sessions/{new file}` immediately (Step 3)