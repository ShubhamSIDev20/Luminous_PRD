# ME Requirements — Read Me First

**What this folder is:** the written requirements for the **ME** battery test system —
what the software has to do, before anybody writes code.

**Date:** 2026-07-28 · **Status:** draft, waiting on answers from the architect and the
client. Nothing here is approved yet.

---

## 1. The project in one paragraph

ME replaces the older **BTS** product. A single **Primary Board** (an NXP i.MX 8M Plus
computer) is in charge. It holds the test programs, works out step by step what should
happen, and drives up to **eight Secondary Boards** over a CAN cable. Each Secondary Board
looks after one battery: it pushes current in or pulls it out, measures what is happening,
and protects the battery and itself. A **Web Application** is what people actually use —
operators write test programs there, press start, and watch the results.

Three big changes from the old BTS product shape everything in this folder:

| # | Old BTS | New ME |
|---|---|---|
| 1 | One controller looked after **1 battery** | One Primary looks after **up to 8** |
| 2 | The **Secondary** board decided what step to do next | The **Primary** decides; the Secondary just regulates |
| 3 | Connected over a simple **serial** cable, one to one | Connected over a **shared CAN** cable, nine devices on it |

---

## 2. The documents, in plain language

Read the "Who needs it" column to find yours.

| File | What it is, in plain words | Who needs it | Size |
|---|---|---|---|
| **`README.md`** | This file. | Everyone | — |
| **`00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md`** | The homework we did **before** writing requirements. For every feature it says: *did we actually have a document that told us how this works, or are we guessing?* It also holds the master list of unanswered questions. | Anyone asking "how much of this is solid?" | 697 lines |
| **`01_SRS_ME_Primary_Board_v0.1.md`** | What the **Primary Board** software must do — **620 requirements**. This is the biggest document because the Primary now does the thinking: it stores programs, reads them, decides each step, and commands all eight channels. | Whoever builds the Primary Board software | 1374 lines |
| **`01A_..._Primary_Annex_...v0.1.md`** | A helper for the document above. Two useful tables: where every requirement came from, and a **blank sheet for the architect** to decide which of the Primary's two kinds of CPU core runs each piece of software. | The software architect | 923 lines |
| **`02_SRS_ME_Secondary_Board_v0.1.md`** | What **one Secondary Board** must do — **530 requirements**. Charging and discharging accurately, measuring, and shutting down safely on its own if anything goes wrong. | Whoever builds the Secondary Board firmware | 1435 lines |
| **`02A_..._Secondary_Annex_...v0.1.md`** | Helper for the above. Includes a **test sheet listing the 195 requirements that can only be proved on real hardware** with a real battery — you cannot check these by reading code. | The test engineer | 553 lines |
| **`03_ICD_ME_Interfaces_v0.1.md`** | The **messages** that travel between the three parts — **173 of them**. For each one: what it is for, which way it goes, what information it carries, and what happens if it goes missing. | Anyone building either end of a connection | 1009 lines |
| **`04_SRS_ME_Web_Application_v0.1.md`** | What the **Web Application** must do — **405 requirements**. Writing programs, scheduling them, showing live readings, saving results, managing users. | Whoever builds the Web Application | 1242 lines |
| **`04A_..._Web_Application_Annex_...v0.1.md`** | Helper for the above. Its most useful table **proves that nothing from the existing Web App's own requirements was accidentally dropped**. | The Web App team, and reviewers | 487 lines |
| **`05_ME_SRS_Linking_and_Traceability_v0.1.md`** | The **map**. How all the documents fit together, which document answers which question, and every unanswered question gathered into one place. | Start here if you are new. Also the review document | 791 lines |

### Three more things in this folder

All three were added after the documents above and are not part of the numbered set.

| Folder | What it is, in plain words | Who needs it |
|---|---|---|
| **`ME_Primary_SRS_V0.1/`** | The **consolidated Phase 1 workbook** — 353 requirements covering the Primary Board and the Battery Manager in one spreadsheet, with a traceability sheet back to BTS and a sheet of open issues. Built by `tools/build_me_srs.py`, so the `.xlsx` is generated and the Python is the source. | Anyone who wants the whole Phase 1 requirement set in one place |
| **`Code_Tracebility/`** | **What the code actually does about those 353 requirements**, and what the rest will cost: a per-requirement verdict with the file or symbol that proves it, work packages with implement and hardware-test estimates, and the open-issue register ranked by the engineer-days it blocks. Regenerated from `tools/`, never hand-edited. | Anyone asking "how far along are we?" or "how long will the rest take?" |
| **`ME_Phase1_Estimates/`** | The **Primary Board** half of that plan, **arranged by module instead of by work package**, as a spreadsheet: 27 modules, each with what it covers in plain English, how much already exists, its build and board-test effort, and the open question blocking it, **plus a start and finish date per module** (Wed 02 Sep 2026 → Wed 05 May 2027). **175.5 days at an 8-hour day (1,404 hours)** — the same work as the 234 days in `Code_Tracebility/`, divided by a longer day. Generated from `Code_Tracebility/`, so it cannot disagree with it. Battery Manager work is excluded. | Anyone planning or reporting the Primary Board schedule; anyone who wants the estimate without reading the SRS |

**`Code_Tracebility/` is updated as part of every change to `me-primary`**, not
afterwards — see the standing rule in `../ME Project/CLAUDE.md`. Start at
`Code_Tracebility/README.md`.

### What the file names mean

- **`00`, `01`, `02`…** — the order to read them in.
- **`A` after a number** (`01A`, `02A`, `04A`) — an **annex**: a helper document belonging
  to the numbered one before it. Annexes are produced automatically from their parent, so
  they can never disagree with it.
- **`v0.1`** — draft one. Version numbers are frozen for now, by request, even though
  small corrections have been made.

---

## 3. If you only read one thing

**Read `05_ME_SRS_Linking_and_Traceability_v0.1.md`, sections 1 to 3.** It is three pages
and it tells you which document answers your question, so you do not have to open all of
them.

---

## 4. Words you will meet

| Word | What it means here |
|---|---|
| **Requirement** | One sentence saying something the software *must* do. Each has an ID like `SRS-PRI-P7-016` so it can be discussed and tested. |
| **SRS** | Software Requirements Specification — a document full of requirements for one piece of software. |
| **ICD** | Interface Control Document — a document about the messages sent *between* pieces of software. |
| **Primary Board** | The controller in charge. Called the "Main Controller" in the hardware paperwork. |
| **Secondary Board** | The board that drives one battery. Called the "Digital Controller Transcard" in the hardware paperwork. |
| **Channel** | One battery position. Eight per Primary Board. |
| **`<TBD-…>`** | A number or rule **we could not find in any document**. Each one is a question someone has to answer. There are **181** across the set — 42 for the Primary, 72 for the Secondary, 28 in the ICD, 39 for the Web App. |
| **Open issue** | A bigger question that changes how something should be built. **40 in total: 31 raised before writing started (5 of those now answered), plus 9 found while writing.** So 35 are still open. |
| **Conflict** | Two source documents that **disagree with each other**. We never picked a winner quietly — all 27 are written down so someone can decide. |
| **Reserved decision (`D-01`…`D-06`)** | Six design choices deliberately left to the architect. The requirements are written so they work whichever way each choice goes. |

---

## 5. Honest status — what is and is not settled

**What is solid.** Everything the client's hardware specification, the Battery Manager
manual and the existing software actually told us. Every requirement says where it came
from, in its own `Source` column. Nothing was invented and left unmarked.

**What is not settled yet, and it matters:**

| Question | Why it blocks work |
|---|---|
| **How the CAN messages are formatted** (`D-04`) | Until this is answered we do not know how fast measurements can be recorded — and the Web Application's capacity changes **tenfold** depending on the answer. This is the one to answer first. |
| **What exactly the Primary sends a Secondary** (`D-03`) | Decides how much thinking stays on the Secondary board. |
| **How the Web App and Primary talk** (`D-05`) | Ports, message layout. |
| **Which Primary CPU core runs what** (`D-01`) | The Primary has two different kinds of core on one chip. |
| **Is there an emergency stop, and what is "safe"?** (open issue #8) | Affects a large number of safety requirements on the Secondary board. |
| **Which Web Application is this?** (open issue #2) | The whole Web App document assumes it is the existing one, improved. If it is a brand-new build, that document's evidence weakens. |

**Three problems we found and wrote down rather than quietly fixing:**

1. **Recorded test data can currently be lost without anyone knowing.** The existing design
   sends saved measurements without asking for confirmation, so a lost message leaves a
   silent hole in a test record that may have taken days. See `03_ICD…` §8.3.
2. **The word "registration" means three different things** in the source documents — a
   saved measurement, a device announcing itself, and a set of safety limits. All three
   readings make sense in the same sentence, which is dangerous. See `05_…` §6.
3. **The existing Web App's own two documents disagree with each other.** Both are marked
   approved, but one is missing an entire feature (the program scheduler) that the other
   fully describes. See `04_SRS…` conflict C-22.

---

## 6. How these documents were written

From the material in `../Reference Documents/`:

- the client's **hardware specification** for the Secondary Board (highest authority),
- the **Battery Manager manual**, chapter 12 — the definitive description of the BTS-600
  test program language,
- the **NXP datasheet** for the Primary Board's processor,
- the **existing BTS software's** own documents and source code, treated as *evidence of
  what was done before*, never as a rule for ME.

Where sources disagreed, the order of authority is **hardware spec → Battery Manager →
older documents**. Where the hardware spec disagreed *with itself* — which happens,
because its comment columns were written two years after its data columns — we could not
resolve it by rank, so it became a listed conflict.

---

## 7. For whoever maintains this folder

The four annex-style documents are **generated**, not typed:

The scripts are in **`../tools/`** — one level up, alongside this `Requirements`
folder and `Reference Documents`. They need Python 3 and nothing else, and can be run
from any working directory because the paths inside them are absolute.

| Generated file | Regenerate with |
|---|---|
| `01A_…Primary_Annex…` | `../tools/make_annex.py` |
| `02A_…Secondary_Annex…` | `../tools/make_sec_annex.py` |
| `04A_…Web_Application_Annex…` | `../tools/make_web_annex.py` |
| `05_…Linking_and_Traceability…` | `../tools/make_linking.py` — sections 4–7 only; sections 1–3 are written by hand *inside the script*, so edit them there, not in the output |

There is one more tool that **changes** documents rather than generating them:

| Tool | What it does |
|---|---|
| `../tools/apply_backfill.py` | Adds the ICD message reference to each Primary and Secondary requirement the ICD already names — 78 of them. Run with no arguments for a dry run, `--apply` to write. It is **idempotent**: it recalculates the list from the ICD each time and rewrites it, so re-run it after any ICD change and it will correct itself. Already applied. |
| `../tools/refresh_readme.py` | Corrects **every number in this file** — the line counts in the table above, the requirement and message counts, and the totals in sections 4 and 5. Dry run by default, `--apply` to write. **The words are hand-written and are never touched; only the numbers are.** |

> **Why that last tool exists.** The figures quoted in this README are copied from nine
> other documents, so they go stale the moment anything is edited. Rather than trusting
> anyone to remember, `refresh_readme.py` recalculates them. It finds each number by the
> sentence around it, so **if you reword a sentence containing a number, the tool will say
> that anchor is broken and exit with an error** instead of silently skipping it. If that
> happens, either restore the wording or update the pattern in the script's `CHECKS` list.

**Re-run them after editing any numbered document**, or the helpers will quietly fall out
of step with it.

`../tools/review_all.py` audits the whole set and prints anything wrong: duplicate
requirement IDs, gaps in numbering, cross-references pointing at things that do not exist,
`<TBD-…>` tags that were used but never registered, files over the 2000-line limit, and a
check that the `CHA` / `DCH` / `RCH` operators still follow the Battery Manager model.
**Run it before circulating any revision.** It currently reports one item — the gate
document starting at section 0 — which is intentional and can be ignored.

> The four numbered documents (`00`, `01`, `02`, `03`, `04`) are **written by hand**. There
> is no generator for them; edit them directly.

---

*This folder describes what the software must do. It deliberately does not say how to
build it — no code structure, no message layouts, no hardware choices. Those come next,
and several of them are waiting on the answers in section 5.*
