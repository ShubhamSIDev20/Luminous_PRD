"""
build_traceability.py - regenerate the Code_Tracebility markdown from source.

Usage, from anywhere:

    python Requirements/Code_Tracebility/tools/build_traceability.py

Reads:
    ../../ME_Primary_SRS_V0.1/tools/me_srs_data.py   requirement text + TBD/OI register
    trace_status.py                                  the per-requirement verdict
    trace_packages.py                                work packages and estimates

Writes, in Requirements/Code_Tracebility/:
    README.md
    ME_Primary_Traceability_Matrix.md
    ME_Work_Packages_and_Estimates.md
    ME_Open_Issues_and_TBD_Register.md

Nothing in the generated files should ever be hand-edited - edit the source
module and re-run. The whole point of generating them is that the requirement
text can never drift from the SRS and the requirement counts can never drift
from the verdicts.
"""

import io
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
SRS_TOOLS = os.path.abspath(
    os.path.join(OUT, "..", "ME_Primary_SRS_V0.1", "tools"))

sys.path.insert(0, HERE)
sys.path.insert(0, SRS_TOOLS)

import me_srs_data as D          # noqa: E402
import trace_status as TS        # noqa: E402
import trace_packages as TP      # noqa: E402

STAMP = "2026-08-26"
SRS_VERSION = "ME_Primary_SRS_V0.1"

VERDICTS = ["DONE", "PARTIAL", "TODO", "BLOCKED", "N/A-BM", "DEFERRED", "INFO"]

BADGE = {
    "DONE":     "**DONE**",
    "PARTIAL":  "*PARTIAL*",
    "TODO":     "TODO",
    "BLOCKED":  "**BLOCKED**",
    "N/A-BM":   "N/A-BM",
    "DEFERRED": "DEFERRED",
    "INFO":     "INFO",
}


# ------------------------------------------------------------------ model ---
class Req(object):
    def __init__(self, item, section):
        (_, self.rid, self.stmt, self.typ, self.cat, self.app, self.phase,
         self.card, self.traces, self.change, self.verif, self.ref,
         self.srs_status, self.rem) = item
        self.section = section
        self.num = int(self.rid.rsplit("_", 1)[1])

        if self.typ == "Information":
            self.verdict, self.evidence, self.wp = (
                "INFO", "States a fact or a scope boundary; imposes no code "
                        "obligation on me-primary.", "-")
        elif self.app == "BM":
            v, e, w = TS.verdict_for(self.num)
            if v in ("DEFERRED",):
                self.verdict, self.evidence, self.wp = v, e, w
            else:
                self.verdict = "N/A-BM"
                self.evidence = ("Battery Manager scope - no me-primary "
                                 "obligation. Built by the Battery Manager "
                                 "engineer under %s."
                                 % TS.bm_package_for(self.num))
                self.wp = TS.bm_package_for(self.num)
        else:
            self.verdict, self.evidence, self.wp = TS.verdict_for(self.num)


def load_requirements():
    reqs, section = [], "(preamble)"
    for item in D.REQUIREMENTS:
        if item[0] == "S":
            section = item[2].strip()
            continue
        reqs.append(Req(item, section))
    return reqs


class Pkg(object):
    def __init__(self, row):
        (self.pid, self.title, self.owner, self.impl, self.hil, self.prio,
         self.blockers, self.note) = row
        self.reqs = []

    @property
    def total(self):
        return self.impl + self.hil


def load_packages(reqs):
    pkgs = {}
    order = []
    for row in TP.PACKAGES:
        p = Pkg(row)
        pkgs[p.pid] = p
        order.append(p.pid)
    for r in reqs:
        if r.wp in pkgs:
            pkgs[r.wp].reqs.append(r)
    return pkgs, order


# ----------------------------------------------------------------- helpers ---
def cell(text):
    """Make a string safe for a one-line markdown table cell."""
    if text is None:
        return ""
    s = re.sub(r"\s+", " ", str(text)).strip()
    return s.replace("|", "\\|")


def counts(reqs, predicate=None):
    out = dict((v, 0) for v in VERDICTS)
    for r in reqs:
        if predicate and not predicate(r):
            continue
        out[r.verdict] += 1
    return out


def tbd_ids(text):
    return sorted(set(re.findall(r"TBD-\d\d", text or "")))


# ------------------------------------------------------------------ matrix ---
def write_matrix(reqs, pkgs):
    L = []
    A = L.append
    A("# ME Primary - Requirement to Code Traceability Matrix")
    A("")
    A("> GENERATED FILE - do not hand-edit.")
    A("> Regenerate with `python Requirements/Code_Tracebility/tools/"
      "build_traceability.py`.")
    A("> Source of requirement text: `%s` (read live, never copied)."
      % SRS_VERSION)
    A("> Source of verdicts: `tools/trace_status.py`.")
    A("> Code state as of: %s, branch `develop`." % STAMP)
    A("")
    A("One row per requirement, in SRS order. `Verdict` is where the "
      "`ME Project/me-primary` code actually stands; `Evidence` either names "
      "the file or symbol that proves it or names the gap that denies it; `WP` "
      "is the work package in "
      "[ME_Work_Packages_and_Estimates.md](ME_Work_Packages_and_Estimates.md) "
      "that owns the remaining work.")
    A("")
    A("`Pkg est` is the effort of the WHOLE owning work package in "
      "engineer-days (implement + hardware verification), not a per-requirement "
      "figure. Requirements in a package are not independently schedulable, so "
      "dividing that number by the row count would be a fiction. Use the work "
      "package document for planning and this column only for cost context "
      "while reading a row.")
    A("")

    # ---- summary
    all_c = counts(reqs)
    prim = [r for r in reqs if r.app != "BM"]
    prim_c = counts(prim)
    A("## Status summary")
    A("")
    A("| Verdict | All %d requirements | Of the %d that bind the Primary |"
      % (len(reqs), len(prim)))
    A("|---|---:|---:|")
    for v in VERDICTS:
        A("| %s | %d | %d |" % (BADGE[v], all_c[v], prim_c[v]))
    A("| **Total** | **%d** | **%d** |" % (len(reqs), len(prim)))
    A("")

    done = prim_c["DONE"]
    partial = prim_c["PARTIAL"]
    open_work = prim_c["TODO"] + prim_c["BLOCKED"] + partial
    A("Read that as: of the %d requirements that place an obligation on the "
      "Primary board, **%d are fully met and evidenced**, **%d are partially "
      "met with the gap named**, and **%d are untouched or blocked**. "
      "%d requirements carry no code obligation at all - %d are Information "
      "rows and %d are Battery Manager scope - and %d are Phase 2."
      % (len(prim), done, partial,
         prim_c["TODO"] + prim_c["BLOCKED"],
         all_c["INFO"] + all_c["N/A-BM"], all_c["INFO"], all_c["N/A-BM"],
         all_c["DEFERRED"]))
    A("")
    # Which TOP-LEVEL SRS sections have no code behind them at all? Group
    # every sub-section (5.1, 7.1.1.2, ...) under its leading number so the
    # count is over the 24 real sections, not over their sub-headings.
    top, order_top = {}, []
    for r in reqs:
        key = r.section.split()[0].split(".")[0]
        if key not in top:
            top[key] = []
            order_top.append(key)
        top[key].append(r)
    untouched = []
    for key in order_top:
        rows = [r for r in top[key]
                if r.app != "BM" and r.verdict not in ("INFO", "DEFERRED")]
        if rows and not any(r.verdict in ("DONE", "PARTIAL") for r in rows):
            untouched.append("%s.0" % key)
    A("The honest headline is that **%d of %d Primary-binding requirements "
      "still need work** (%.0f%%). What is already done is not the easy part - "
      "it is the entire host link, the frame layer, the addressing model, the "
      "storage model and a working single-channel execution path, all "
      "host-tested and much of it hardware-verified. What remains is breadth: "
      "%d of the %d SRS sections have no code behind them at all (%s)."
      % (open_work, len(prim), 100.0 * open_work / len(prim),
         len(untouched), len(top), ", ".join(untouched)))
    A("")

    # ---- by category
    A("### By category")
    A("")
    cats = []
    for r in reqs:
        if r.cat not in cats:
            cats.append(r.cat)
    A("| Category | " + " | ".join(BADGE[v] for v in VERDICTS) + " | Total |")
    A("|---" * (len(VERDICTS) + 2) + "|")
    for c in sorted(cats):
        rows = [r for r in reqs if r.cat == c]
        cc = counts(rows)
        A("| %s | %s | %d |"
          % (c, " | ".join(str(cc[v]) for v in VERDICTS), len(rows)))
    A("")

    # ---- the matrix
    A("## Matrix")
    A("")
    section = None
    for r in reqs:
        if r.section != section:
            section = r.section
            A("")
            A("### %s" % section)
            A("")
            rows = [x for x in reqs if x.section == section]
            sc = counts(rows)
            live = ", ".join("%s %d" % (v, sc[v]) for v in VERDICTS if sc[v])
            A("*%d requirement(s) - %s*" % (len(rows), live))
            A("")
            A("| Req | App | Cat | SRS | Verdict | Evidence / gap | WP | Pkg est |")
            A("|---|---|---|---|---|---|---|---:|")
        est = ""
        if r.wp in pkgs:
            est = "%dd" % pkgs[r.wp].total
        A("| `%s` | %s | %s | %s | %s | %s | %s | %s |"
          % (r.rid, cell(r.app), cell(r.cat), cell(r.srs_status),
             BADGE[r.verdict], cell(r.evidence), r.wp, est))
    A("")
    return "\n".join(L) + "\n"


# --------------------------------------------------------------- packages ---
def write_packages(reqs, pkgs, order):
    L = []
    A = L.append
    A("# ME - Work Packages and Effort Estimates")
    A("")
    A("> GENERATED FILE - do not hand-edit.")
    A("> Regenerate with `python Requirements/Code_Tracebility/tools/"
      "build_traceability.py`.")
    A("> Estimates and notes live in `tools/trace_packages.py`; requirement "
      "counts are derived from `tools/trace_status.py` and cannot drift.")
    A("> Code state as of: %s, branch `develop`." % STAMP)
    A("")

    A("## Estimation basis")
    A("")
    A(TP.__doc__.split("ESTIMATION BASIS", 1)[1]
      .split("--------------------------------------------------------------------------", 1)[1]
      .strip())
    A("")

    prim = [pkgs[p] for p in order if pkgs[p].owner == "Primary"]
    bm = [pkgs[p] for p in order if pkgs[p].owner == "BM"]

    def rollup(group, label, exclude_phase2=True):
        active = [p for p in group if not (exclude_phase2 and p.prio == "Phase 2")]
        impl = sum(p.impl for p in active)
        hil = sum(p.hil for p in active)
        A("### %s" % label)
        A("")
        A("| | Engineer-days |")
        A("|---|---:|")
        A("| Implementation (includes host unit tests) | %d |" % impl)
        A("| Hardware-in-the-loop verification | %d |" % hil)
        A("| **Total, one engineer, serial** | **%d** |" % (impl + hil))
        A("| Working months at 21 days | %.1f |" % ((impl + hil) / 21.0))
        A("")
        return impl, hil

    A("## Roll-up")
    A("")
    pi, ph = rollup(prim, "Primary board - 1 engineer")
    bi, bh = rollup(bm, "Battery Manager web application - 1 engineer")
    p2 = sum(p.impl + p.hil for p in pkgs.values() if p.prio == "Phase 2")
    A("### Both boards")
    A("")
    A("The two engineers work in parallel, so the Phase 1 calendar is set by "
      "the longer of the two tracks, not by their sum.")
    A("")
    A("| | Engineer-days |")
    A("|---|---:|")
    A("| Primary track | %d |" % (pi + ph))
    A("| Battery Manager track | %d |" % (bi + bh))
    A("| **Total effort, Phase 1** | **%d** |" % (pi + ph + bi + bh))
    A("| **Calendar-critical path (the longer track)** | **%d days ~ %.1f "
      "months** |" % (max(pi + ph, bi + bh), max(pi + ph, bi + bh) / 21.0))
    A("| Phase 2, excluded from the above | %d |" % p2)
    A("")
    A("> **The critical path is the Primary track and it is not close.** "
      "Pairing is the thing to watch: %d of the Battery Manager packages "
      "explicitly pair with a Primary package and cannot be finished ahead of "
      "it. If the Battery Manager engineer runs out of unblocked work, the "
      "useful order is WP-B01, WP-B02, WP-B04 and WP-B06 - none of them waits "
      "on the Primary."
      % len([p for p in bm if "Pairs with" in p.note or "pair" in p.note]))
    A("")

    A("## Packages")
    A("")
    for label, group in (("Primary board", prim),
                         ("Battery Manager web application", bm)):
        A("### %s" % label)
        A("")
        A("| WP | Title | Reqs | Impl | HIL | Total | Prio | Blocked by |")
        A("|---|---|---:|---:|---:|---:|---|---|")
        for p in group:
            A("| `%s` | %s | %d | %d | %d | **%d** | %s | %s |"
              % (p.pid, cell(p.title), len(p.reqs), p.impl, p.hil, p.total,
                 p.prio, cell(p.blockers)))
        A("")

    orphan = [pkgs[x] for x in order if not pkgs[x].reqs]
    if orphan:
        A("## Packages that trace no requirement exclusively")
        A("")
        A("Every requirement in this matrix has exactly ONE owning package, so "
          "a `BM + Primary` requirement sits with whichever side carries its "
          "remaining work. The packages below are therefore real, estimated "
          "work with no requirement row of their own - the other half of a "
          "shared requirement, or debt from the T-register rather than from the "
          "SRS. They are listed here so nobody reads a zero in the `Reqs` "
          "column as a package that can be deleted.")
        A("")
        A("| WP | Total | Why it has no rows |")
        A("|---|---:|---|")
        for p in orphan:
            why = p.note.split("**")[-2] if "**" in p.note else p.note
            A("| `%s` | %dd | %s |" % (p.pid, p.total, cell(why)))
        A("")

    A("## Package detail")
    A("")
    for p in [pkgs[x] for x in order]:
        A("### %s - %s" % (p.pid, p.title))
        A("")
        A("| | |")
        A("|---|---|")
        A("| Owner | %s engineer |" % ("Primary board" if p.owner == "Primary"
                                       else "Battery Manager"))
        A("| Effort | %d d implement + %d d hardware = **%d engineer-days** |"
          % (p.impl, p.hil, p.total))
        A("| Priority | %s |" % p.prio)
        A("| Blocked by | %s |" % (p.blockers if p.blockers else "nothing"))
        A("| Requirements | %d |" % len(p.reqs))
        A("")
        A(p.note)
        A("")
        if p.reqs:
            cc = counts(p.reqs)
            live = ", ".join("%s %d" % (v, cc[v]) for v in VERDICTS if cc[v])
            A("**Requirements covered** (%s):" % live)
            A("")
            A("| Req | Verdict | Statement |")
            A("|---|---|---|")
            for r in sorted(p.reqs, key=lambda x: x.num):
                stmt = cell(r.stmt)
                if len(stmt) > 220:
                    stmt = stmt[:217] + "..."
                A("| `%s` | %s | %s |" % (r.rid, BADGE[r.verdict], stmt))
            A("")
    return "\n".join(L) + "\n"


# -------------------------------------------------------------- TBD / OI ---
def write_register(reqs, pkgs):
    L = []
    A = L.append
    A("# ME - Open Issue and TBD Register, with the code it blocks")
    A("")
    A("> GENERATED FILE - do not hand-edit.")
    A("> Regenerate with `python Requirements/Code_Tracebility/tools/"
      "build_traceability.py`.")
    A("> Register content is read live from `%s`; the 'blocks' columns are "
      "derived from `tools/trace_status.py`." % SRS_VERSION)
    A("")
    A("The SRS records these as open questions. This file adds what each one "
      "actually costs: which requirements cannot be implemented until it is "
      "answered, and which work package therefore cannot start. **Every hour "
      "spent closing a row here buys back multiple engineer-days below it, and "
      "several of these rows are a single conversation.**")
    A("")

    blocked = [r for r in reqs if r.verdict == "BLOCKED"]
    A("%d requirements are currently BLOCKED rather than merely unbuilt."
      % len(blocked))
    A("")

    # index TBD -> requirements that name it in their evidence
    by_tbd = {}
    for r in reqs:
        for t in tbd_ids(r.evidence):
            by_tbd.setdefault(t, []).append(r)
    by_oi = {}
    for r in reqs:
        for o in sorted(set(re.findall(r"OI-\d\d", r.evidence or ""))):
            by_oi.setdefault(o, []).append(r)

    # work packages blocked by each id
    def wps_for(ident):
        return sorted(set(p.pid for p in pkgs.values()
                          if ident in (p.blockers or "")))

    A("## Highest-leverage rows")
    A("")
    A("Ranked by how much work they unblock, not by their own difficulty.")
    A("")
    A("| Rank | Id | Question | Owner | Days unblocked | WPs |")
    A("|---:|---|---|---|---:|---|")
    lever = []
    for row in D.ISSUES:
        ident, kind, area, question, impact, affects, owner, sev, state = row
        wps = wps_for(ident)
        days = sum(pkgs[w].total for w in wps if w in pkgs)
        lever.append((days, ident, question, owner, wps))
    lever.sort(reverse=True)
    for i, (days, ident, question, owner, wps) in enumerate(lever[:12], 1):
        q = cell(question)
        if len(q) > 150:
            q = q[:147] + "..."
        A("| %d | `%s` | %s | %s | %d | %s |"
          % (i, ident, q, cell(owner), days,
             ", ".join("`%s`" % w for w in wps) or "-"))
    A("")
    A("> Note how the ranking falls out. `OI-11` (the error-code catalogue) and "
      "`OI-05` (what a channel's safe state is) are each one decision that "
      "gates several packages, and neither needs any engineering to answer - "
      "they need somebody to write the answer down. `OI-31`/`TBD-28` (the "
      "control decision interval) is the opposite: it gates the least work by "
      "day count but it is the one that can invalidate the architecture, "
      "because it decides whether a containerised Linux A53 can host a "
      "64-channel control loop at all. Answer it early even though it unblocks "
      "fewer days.")
    A("")

    A("## Full register")
    A("")
    A("| Id | Kind | Area | Question | Affects | Owner | Sev | Blocks these WPs | Days |")
    A("|---|---|---|---|---|---|---|---|---:|")
    for row in D.ISSUES:
        ident, kind, area, question, impact, affects, owner, sev, state = row
        wps = wps_for(ident)
        days = sum(pkgs[w].total for w in wps if w in pkgs)
        A("| `%s` | %s | %s | %s | %s | %s | %s | %s | %s |"
          % (ident, cell(kind), cell(area), cell(question), cell(affects),
             cell(owner), cell(sev),
             ", ".join("`%s`" % w for w in wps) or "-",
             days if days else ""))
    A("")

    A("## Requirements held up, by register entry")
    A("")
    for ident in sorted(set(list(by_tbd.keys()) + list(by_oi.keys()))):
        rows = by_tbd.get(ident) or by_oi.get(ident) or []
        if not rows:
            continue
        A("- **`%s`** blocks %d requirement(s): %s"
          % (ident, len(rows),
             ", ".join("`%s`" % r.rid for r in sorted(rows, key=lambda x: x.num))))
    A("")
    return "\n".join(L) + "\n"


# ----------------------------------------------------------------- readme ---
def write_readme(reqs, pkgs, order):
    prim = [r for r in reqs if r.app != "BM"]
    pc = counts(prim)
    ac = counts(reqs)
    p_pkgs = [pkgs[p] for p in order if pkgs[p].owner == "Primary"
              and pkgs[p].prio != "Phase 2"]
    b_pkgs = [pkgs[p] for p in order if pkgs[p].owner == "BM"
              and pkgs[p].prio != "Phase 2"]
    p_total = sum(p.total for p in p_pkgs)
    b_total = sum(p.total for p in b_pkgs)
    blocked = len([r for r in reqs if r.verdict == "BLOCKED"])

    L = []
    A = L.append
    A("# Code Traceability - ME Primary and Battery Manager")
    A("")
    A("> GENERATED FILE - do not hand-edit. Regenerate with")
    A("> `python Requirements/Code_Tracebility/tools/build_traceability.py`.")
    A("> Code state as of: %s, branch `develop`." % STAMP)
    A("")
    A("This folder answers three questions about `%s` that the SRS workbook "
      "itself cannot:" % SRS_VERSION)
    A("")
    A("1. **Which requirements does the code already meet, and what proves it?**")
    A("2. **How long will the rest take to implement and test?**")
    A("3. **Which of it cannot be started yet, and who is holding it up?**")
    A("")

    A("## Where things stand")
    A("")
    A("| | |")
    A("|---|---:|")
    A("| Requirements in the SRS | %d |" % len(reqs))
    A("| Of those, binding on the Primary board | %d |" % len(prim))
    A("| Fully met and evidenced | %d |" % pc["DONE"])
    A("| Partially met, gap named | %d |" % pc["PARTIAL"])
    A("| Not started | %d |" % pc["TODO"])
    A("| Blocked on an open question | %d |" % pc["BLOCKED"])
    A("| Battery Manager scope | %d |" % ac["N/A-BM"])
    A("| Information rows, no code obligation | %d |" % ac["INFO"])
    A("| Phase 2 | %d |" % ac["DEFERRED"])
    A("")
    A("| Effort remaining, Phase 1 | Engineer-days | Working months |")
    A("|---|---:|---:|")
    A("| Primary board (1 engineer) | %d | %.1f |" % (p_total, p_total / 21.0))
    A("| Battery Manager (1 engineer) | %d | %.1f |" % (b_total, b_total / 21.0))
    A("| **Calendar critical path** | **%d** | **%.1f** |"
      % (max(p_total, b_total), max(p_total, b_total) / 21.0))
    A("")
    A("The two tracks run in parallel with one engineer each, so the Phase 1 "
      "calendar is the longer track, not the sum. **The Primary board is the "
      "critical path.**")
    A("")
    A("%d requirements are BLOCKED rather than merely unbuilt - they cannot be "
      "started until an entry in the open-issue register is answered. That is "
      "the cheapest schedule risk in this document to retire, and it is not "
      "engineering work." % blocked)
    A("")

    A("## The files")
    A("")
    A("| File | What it is | When to read it |")
    A("|---|---|---|")
    A("| [ME_Primary_Traceability_Matrix.md](ME_Primary_Traceability_Matrix.md) "
      "| One row per requirement: verdict, the file or symbol that proves it, "
      "and the owning work package. | Before touching any module - find the "
      "requirements it carries. |")
    A("| [ME_Work_Packages_and_Estimates.md](ME_Work_Packages_and_Estimates.md) "
      "| %d work packages with implement and hardware-test estimates, "
      "priorities, dependencies and per-package requirement lists. | Planning, "
      "sequencing, and answering 'how long'. |" % len(pkgs))
    A("| [ME_Open_Issues_and_TBD_Register.md](ME_Open_Issues_and_TBD_Register.md) "
      "| Every TBD and open issue, ranked by the engineer-days it blocks. | "
      "Before a requirements review or a client call - this is the agenda. |")
    A("| `tools/` | The generator. `trace_status.py` holds the verdicts, "
      "`trace_packages.py` the estimates. | When code changes. |")
    A("")

    A("## Verdict vocabulary")
    A("")
    A("| Verdict | Means | Bar for claiming it |")
    A("|---|---|---|")
    A("| **DONE** | Implemented and proven. | A host unit test asserts it, or "
      "a hardware run showed it. Both, for anything on the wire. |")
    A("| *PARTIAL* | Some of it is built. | The evidence column MUST name the "
      "remaining gap. A PARTIAL with a vague gap is a TODO wearing a "
      "disguise. |")
    A("| TODO | Nothing in the code addresses it. | - |")
    A("| **BLOCKED** | Cannot be started. | An entry in the open-issue register "
      "must be named. Blocked work still carries its full estimate. |")
    A("| N/A-BM | Battery Manager scope. | The SRS Applicability column says "
      "`BM`. |")
    A("| DEFERRED | Phase 2. | The SRS Phase column says `Phase 2`. |")
    A("| INFO | An Information row. | The SRS Type column says "
      "`Information`. |")
    A("")

    A("## How to keep this current")
    A("")
    A("These files are regenerated, never edited. The workflow, every time "
      "`me-primary` changes:")
    A("")
    A("1. Edit the verdict and evidence for the requirements the change "
      "touched, in `tools/trace_status.py`. Evidence should name a file and a "
      "symbol - `ME Project/me-primary/src/exec/step_engine.c` "
      "`me_exec_tick()` - not a feeling.")
    A("2. If the change closes or resizes a work package, edit "
      "`tools/trace_packages.py`.")
    A("3. Run `python Requirements/Code_Tracebility/tools/"
      "build_traceability.py`.")
    A("4. Commit the source module change and the regenerated markdown "
      "together, on `develop`.")
    A("")
    A("Two rules that keep the document worth trusting:")
    A("")
    A("- **Do not promote a verdict to DONE on the strength of a native "
      "build.** A passing `build-native.ps1` exercises zero aarch64 codegen, "
      "zero Torizon, zero Docker and zero sockets - that is ADR-3, and it is "
      "the project's own rule. Protocol logic can be DONE from a host test; "
      "anything involving a socket, a thread, the RPMsg link or real hardware "
      "needs a hardware run.")
    A("- **Write the gap, not the intention.** `PARTIAL - only the TIME "
      "cut-off type is decoded` is useful in six months. `PARTIAL - mostly "
      "done` is not.")
    A("")

    A("## Relationship to the other trackers")
    A("")
    A("| Tracker | Scope | Authority |")
    A("|---|---|---|")
    A("| This folder | SRS requirement to code, and effort to close the gap. | "
      "Authoritative on **requirement coverage and estimates**. |")
    A("| `ME Project/.claude/TASKS.md` | Session-level tasks, T-numbers. | "
      "Authoritative on **what is being worked on right now**. |")
    A("| `ME Project/.claude/DECISIONS.md` | ADR log, ADR-numbers. | "
      "Authoritative on **why the code is shaped the way it is**. |")
    A("| `ME Project/Docs/ME_Primary_Implementation_Reference.md` | Per-file "
      "code reference. | Authoritative on **how a module behaves**. |")
    A("")
    A("They overlap deliberately and they are cross-referenced by name, not "
      "merged. A T-number is a unit of work in flight; a WP-number here is a "
      "unit of requirement coverage. WP-P32 exists precisely to close the "
      "leftover T-numbers, which is the seam between the two.")
    A("")
    return "\n".join(L) + "\n"


# -------------------------------------------------------------------- main ---
def main():
    reqs = load_requirements()
    pkgs, order = load_packages(reqs)

    orphans = [r for r in reqs
               if r.wp not in pkgs and r.verdict not in ("INFO",)]
    if orphans:
        raise SystemExit("requirements point at an unknown work package: %s"
                         % ", ".join("%s->%s" % (r.rid, r.wp) for r in orphans))
    empty = [p.pid for p in pkgs.values() if not p.reqs]
    if empty:
        print("note: work packages with no requirements traced to them: %s"
              % ", ".join(sorted(empty)))

    files = {
        "README.md": write_readme(reqs, pkgs, order),
        "ME_Primary_Traceability_Matrix.md": write_matrix(reqs, pkgs),
        "ME_Work_Packages_and_Estimates.md": write_packages(reqs, pkgs, order),
        "ME_Open_Issues_and_TBD_Register.md": write_register(reqs, pkgs),
    }
    for name, text in files.items():
        path = os.path.join(OUT, name)
        with io.open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        print("wrote %-40s %6d lines" % (name, text.count("\n")))

    prim = [r for r in reqs if r.app != "BM"]
    pc = counts(prim)
    print("")
    print("%d requirements, %d bind the Primary: DONE %d, PARTIAL %d, "
          "TODO %d, BLOCKED %d"
          % (len(reqs), len(prim), pc["DONE"], pc["PARTIAL"], pc["TODO"],
             pc["BLOCKED"]))
    for owner in ("Primary", "BM"):
        act = [p for p in pkgs.values()
               if p.owner == owner and p.prio != "Phase 2"]
        print("%-8s %2d packages, %3d impl + %3d hil = %3d engineer-days"
              % (owner, len(act), sum(p.impl for p in act),
                 sum(p.hil for p in act), sum(p.total for p in act)))


if __name__ == "__main__":
    main()
