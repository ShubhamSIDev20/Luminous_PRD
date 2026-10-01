# ME_Phase1_Estimates — Read Me First

**Deliverable:** `ME_Phase1_Module_Estimates.xlsx`
**Status:** Rev 1.3, 2026-09-01. Code state as of 2026-08-26, branch `develop`.
**Scope:** Primary Board only.
**Working day:** 8 productive hours, Monday to Friday.
**Schedule:** starts Wed 02 Sep 2026, finishes Wed 05 May 2027.

---

## What this is

The Phase 1 implementation plan for the **ME Primary Board**, **broken down by
module** rather than by work package, so that someone who has not read the SRS
can look at one sheet and see what has to be built and what it costs.

It is a presentation of `Code_Tracebility/ME_Work_Packages_and_Estimates.md`,
not a second opinion about it. Every number in the workbook is read live from
that document's source data at build time.

| Sheet | What it is | Who needs it |
|---|---|---|
| `0. Read Me` | Scope, the roll-up totals, what a "day" means, and how to read a row | Everyone — start here |
| `1. Primary Board Modules` | **27 modules, 175.5 engineer-days (1,404 hours).** One row per module, with effort split into build days and board-test days, plus its **start and finish date** | The Primary Board engineer, and anyone planning the schedule |

## The numbers to take away

| | Hours | Days at 8 h | Months at 21 days |
|---|---:|---:|---:|
| Build (includes unit tests) | 906 | 113.25 | — |
| Board test (hardware-in-the-loop) | 498 | 62.25 | — |
| **Primary Board, one engineer** | **1,404** | **175.5** | **≈ 8.4** |

One engineer, working serially — nothing in this plan can be split across
people, so the days add directly into calendar days.

## The schedule

Every module carries a **Start** and **Finish** date. Work begins
**Wed 02 Sep 2026** and the last module finishes **Wed 05 May 2027**.

Three things the dates assume, all of which can move them:

- **Modules run in the listed order, one at a time.** There is one engineer.
  A module may begin on the date the previous one ends — the estimates land
  on quarter-days, so that boundary day is genuinely shared, not two people
  working at once.
- **Weekends are the only non-working days modelled.** Public holidays,
  leave and sick days are not, and every one of them pushes all later dates
  to the right. Subtract your own holiday calendar before committing to the
  finish date.
- **Each module's open question is answered before its start date arrives.**
  Most modules are waiting on one. A question answered late moves its module
  and everything after it by however long the answer took — this is the most
  likely reason the plan slips, and it is not a coding problem.

To re-plan from a different date, edit `START_DATE` in
`tools/build_phase1_estimates.py` and rebuild.

**Only 3 of the 27 modules — 11.25 of the 175.5 engineer-days — can be started
today.** Everything else waits on an open question in
`../Code_Tracebility/ME_Open_Issues_and_TBD_Register.md`. A blocked module
still carries its full estimate: being blocked stops work starting, it does
not make the work smaller.

## What a "day" means here

One engineer-day here is **8 productive hours**, AI-assisted with Claude Code,
working the way this project has worked since 2026-08-06: test first, a design
note for every real decision, both builds green before anything is claimed.

**Hours are the invariant; days are a presentation of them.** The underlying
estimate was recorded against a 6-hour day in
`../Code_Tracebility/ME_Work_Packages_and_Estimates.md`, which is why that
document reads 234 days where this one reads 175.5. It is the same work,
divided by a longer day — neither figure corrects the other. The workbook
carries a `Total hours` column so the two can always be reconciled.

**These are already AI-assisted numbers — do not discount them again for AI.**
They are calibrated against measured output, not guessed: 16 sessions between
6 and 21 August 2026 delivered ~7.5 kLOC of source, ~4.6 kLOC of tests and 39
ADRs across roughly 13 work packages — about 7 hours per package where the
specification was known and the hardware existed. Modules are priced against
that, then loaded for the two things that were *not* true of those sessions:
hardware that does not exist yet, and specifications that have not been
written yet.

`Build days` include their unit tests. `Board-test days` are
hardware-in-the-loop on the real i.MX8M Plus with a real Secondary attached —
that column is serialised on one engineer and one board and **will not shrink
with better tooling**.

## What is excluded

- **The Battery Manager web application** — a separate Phase 1 track with its
  own engineer, about **95 more days** at the same day length. Its packages
  are in `../Code_Tracebility/ME_Work_Packages_and_Estimates.md`. Several of
  them cannot finish ahead of the Primary module they pair with, so **this
  workbook is not the whole of Phase 1.**
- Phase 2 work. The only Phase 2 package in the register, `WP-B16`, is
  Battery Manager work and so is already outside this scope.
- Secondary Board firmware, M7 firmware, mechanical and electrical bring-up.
- Requirement-level rework if an open issue comes back with an answer that
  invalidates a design. That risk is named per module instead.

## How a module was defined

A module is one section of `ME_Primary_SRS_V0.1`. The work packages under it
are the ones whose requirements trace to that section — derived from the
requirement-to-package mapping, not invented. A few modules gather two or
three closely-related packages (for example, fault classification, the error
catalogue and stall detection are one module) because splitting them would be
an artefact of how the estimate was originally recorded rather than a real
boundary in the work.

Modules are listed in the order the work should be done, not by SRS number:
what unblocks other modules first, independent third-party interfaces last.

## Regenerating

Never hand-edit the `.xlsx` — it is generated and will be overwritten.

```
cd tools
python build_phase1_estimates.py
```

Requires Python 3 and `openpyxl`.

To change the working day, edit `WORKING_HOURS` at the top of
`build_phase1_estimates.py` and rebuild. Every figure and every sentence that
quotes the day length is derived from that one constant.

| File | Role |
|---|---|
| `tools/phase1_modules.py` | The module grouping and the plain-English descriptions. **The only file to edit.** |
| `tools/build_phase1_estimates.py` | Formatting and Excel generation. Reads every number from the traceability tooling. |

## What the build guarantees

The build **fails** if a Primary-owned work package is claimed by no module or
by two, or if the module totals stop matching the register totals. That is what
stops effort quietly disappearing when a package is added, renamed or
re-estimated.

The scope is enforced by `OWNER` in `build_phase1_estimates.py`, not by
omission: Battery Manager packages are out of scope **because their owner is
not `Primary`**, and the check says so explicitly. Narrowing this workbook to
one board therefore did not create a hole through which a Primary package
could escape the claim rule.

`EXCLUDED` in `phase1_modules.py` is empty, and that is the correct state
today. It is kept because deferring a Primary package must be a deliberate act
recorded in one place — deleting it from a module instead would make its days
vanish silently.

## When to regenerate

Whenever `Code_Tracebility/` is regenerated — which, per the standing rule in
`../../ME Project/CLAUDE.md`, is part of every change to `me-primary`. The
numbers here follow that document; they never lead it.

If a **new Primary work package** is added to `trace_packages.py`, this build
will fail until it is assigned to a module in `phase1_modules.py`. That failure
is the feature, not a nuisance.
