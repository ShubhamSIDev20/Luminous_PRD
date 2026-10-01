# T-22 — System Flow & Block Diagram Document

> **Status:** ✅ Done
> **Started / Completed:** 2026-08-18
> **Session:** #12
> **Agent:** Claude (Opus 5, 1M context)
> **Type:** Documentation

---

## Goal

Produce a single college-report-style block/flow diagram document covering the entire
operational lifecycle of the system, from a hardware channel first requesting registration
through to session-wise storage of UDP data — with every action driven from the background
service accounted for.

## Requirements (as given)

- Location: `docs/flowDocs/`
- Start with first device registration → user allows it → it registers
- Then the actions a user can perform: transfer program, battery, manufacturing, control commands
- Everything from the background service; each and every action considered
- Block-diagram document, "like a college project"
- Explicitly cover: after registration transfer program/battery/DBC (optional) → start program
  → registration data arriving on UDP 10001 → how it is stored session-wise

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Diagram style | ASCII box diagrams in code fences | User-selected. Renders identically in VS Code, Notepad, print, and pasted into Word — no renderer dependency. Matches the requested "college project" look. |
| File layout | One master document | User-selected. Keeps the narrative continuous; nothing to keep in sync across files. |
| Transfer order documented | Battery → DBC → Program | The code's real order (`TransferDialog.razor:372-445`), which contradicts the order in the request. Flagged to the user before writing. |
| `TimeSyn`/`SystemReset` collision | Documented as an observation, not fixed | Behavioural change to a hardware command needs firmware confirmation first. Raised as T-21. |

## Deliverable

`docs/flowDocs/00-SYSTEM-FLOW.md` — 13 chapters plus an 8-section appendix. Every chapter
contains one ASCII block diagram, a step narrative, and a "files involved" line citing the
source file and line range the diagram was derived from.

## Outcome

Complete. No source files modified. One protocol inconsistency surfaced (T-21).
Full detail in [sessions/2026-08-18_0000_system-flow-block-diagram-doc.md](../sessions/2026-08-18_0000_system-flow-block-diagram-doc.md).
