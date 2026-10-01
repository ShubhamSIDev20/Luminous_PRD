# Code Traceability - ME Primary and Battery Manager

> GENERATED FILE - do not hand-edit. Regenerate with
> `python Requirements/Code_Tracebility/tools/build_traceability.py`.
> Code state as of: 2026-08-26, branch `develop`.

This folder answers three questions about `ME_Primary_SRS_V0.1` that the SRS workbook itself cannot:

1. **Which requirements does the code already meet, and what proves it?**
2. **How long will the rest take to implement and test?**
3. **Which of it cannot be started yet, and who is holding it up?**

## Where things stand

| | |
|---|---:|
| Requirements in the SRS | 353 |
| Of those, binding on the Primary board | 302 |
| Fully met and evidenced | 19 |
| Partially met, gap named | 42 |
| Not started | 167 |
| Blocked on an open question | 52 |
| Battery Manager scope | 50 |
| Information rows, no code obligation | 15 |
| Phase 2 | 8 |

| Effort remaining, Phase 1 | Engineer-days | Working months |
|---|---:|---:|
| Primary board (1 engineer) | 234 | 11.1 |
| Battery Manager (1 engineer) | 127 | 6.0 |
| **Calendar critical path** | **234** | **11.1** |

The two tracks run in parallel with one engineer each, so the Phase 1 calendar is the longer track, not the sum. **The Primary board is the critical path.**

52 requirements are BLOCKED rather than merely unbuilt - they cannot be started until an entry in the open-issue register is answered. That is the cheapest schedule risk in this document to retire, and it is not engineering work.

## The files

| File | What it is | When to read it |
|---|---|---|
| [ME_Primary_Traceability_Matrix.md](ME_Primary_Traceability_Matrix.md) | One row per requirement: verdict, the file or symbol that proves it, and the owning work package. | Before touching any module - find the requirements it carries. |
| [ME_Work_Packages_and_Estimates.md](ME_Work_Packages_and_Estimates.md) | 49 work packages with implement and hardware-test estimates, priorities, dependencies and per-package requirement lists. | Planning, sequencing, and answering 'how long'. |
| [ME_Open_Issues_and_TBD_Register.md](ME_Open_Issues_and_TBD_Register.md) | Every TBD and open issue, ranked by the engineer-days it blocks. | Before a requirements review or a client call - this is the agenda. |
| `tools/` | The generator. `trace_status.py` holds the verdicts, `trace_packages.py` the estimates. | When code changes. |

## Verdict vocabulary

| Verdict | Means | Bar for claiming it |
|---|---|---|
| **DONE** | Implemented and proven. | A host unit test asserts it, or a hardware run showed it. Both, for anything on the wire. |
| *PARTIAL* | Some of it is built. | The evidence column MUST name the remaining gap. A PARTIAL with a vague gap is a TODO wearing a disguise. |
| TODO | Nothing in the code addresses it. | - |
| **BLOCKED** | Cannot be started. | An entry in the open-issue register must be named. Blocked work still carries its full estimate. |
| N/A-BM | Battery Manager scope. | The SRS Applicability column says `BM`. |
| DEFERRED | Phase 2. | The SRS Phase column says `Phase 2`. |
| INFO | An Information row. | The SRS Type column says `Information`. |

## How to keep this current

These files are regenerated, never edited. The workflow, every time `me-primary` changes:

1. Edit the verdict and evidence for the requirements the change touched, in `tools/trace_status.py`. Evidence should name a file and a symbol - `ME Project/me-primary/src/exec/step_engine.c` `me_exec_tick()` - not a feeling.
2. If the change closes or resizes a work package, edit `tools/trace_packages.py`.
3. Run `python Requirements/Code_Tracebility/tools/build_traceability.py`.
4. Commit the source module change and the regenerated markdown together, on `develop`.

Two rules that keep the document worth trusting:

- **Do not promote a verdict to DONE on the strength of a native build.** A passing `build-native.ps1` exercises zero aarch64 codegen, zero Torizon, zero Docker and zero sockets - that is ADR-3, and it is the project's own rule. Protocol logic can be DONE from a host test; anything involving a socket, a thread, the RPMsg link or real hardware needs a hardware run.
- **Write the gap, not the intention.** `PARTIAL - only the TIME cut-off type is decoded` is useful in six months. `PARTIAL - mostly done` is not.

## Relationship to the other trackers

| Tracker | Scope | Authority |
|---|---|---|
| This folder | SRS requirement to code, and effort to close the gap. | Authoritative on **requirement coverage and estimates**. |
| `ME Project/.claude/TASKS.md` | Session-level tasks, T-numbers. | Authoritative on **what is being worked on right now**. |
| `ME Project/.claude/DECISIONS.md` | ADR log, ADR-numbers. | Authoritative on **why the code is shaped the way it is**. |
| `ME Project/Docs/ME_Primary_Implementation_Reference.md` | Per-file code reference. | Authoritative on **how a module behaves**. |

They overlap deliberately and they are cross-referenced by name, not merged. A T-number is a unit of work in flight; a WP-number here is a unit of requirement coverage. WP-P32 exists precisely to close the leftover T-numbers, which is the seam between the two.

