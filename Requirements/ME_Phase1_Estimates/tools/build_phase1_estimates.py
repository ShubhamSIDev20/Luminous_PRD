#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate ME_Phase1_Module_Estimates.xlsx.

Module names and descriptions come from `phase1_modules.py`.
Every number comes from the traceability tooling:

    ../../Code_Tracebility/tools/trace_packages.py   effort, priority, blockers
    ../../Code_Tracebility/tools/trace_status.py     what is already built
    ../../ME_Primary_SRS_V0.1/tools/me_srs_reqs.py   the requirements

so this workbook cannot disagree with ME_Work_Packages_and_Estimates.md
about a day or a requirement count.

The build FAILS if a work package is claimed by no module or by two, and if
the module totals do not add up to the register totals. That is what stops
effort quietly disappearing when a package is added or renamed.

Re-runnable: deletes and rewrites the workbook every time. Never hand-edit
the .xlsx.

    python build_phase1_estimates.py
"""
import datetime
import math
import os
import re
import sys
from collections import defaultdict

from openpyxl import Workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))          # .../Requirements
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ROOT, "Code_Tracebility", "tools"))
sys.path.insert(0, os.path.join(ROOT, "ME_Primary_SRS_V0.1", "tools"))

import phase1_modules as MOD          # noqa: E402
import trace_packages as PKG          # noqa: E402
import trace_status as ST             # noqa: E402
import me_srs_reqs as SRS             # noqa: E402

OUT = os.path.join(os.path.dirname(HERE), "ME_Phase1_Module_Estimates.xlsx")

CODE_DATE = "2026-08-26"
DOC_DATE = "2026-09-01"
REV = "1.3"

# Scope. This workbook covers the Primary Board only (developer,
# 2026-09-01). OWNER is the `owner` field in trace_packages.py, and it is
# what the consistency check scopes itself to: packages owned by anyone
# else are out of scope by definition, not by omission.
OWNER = "Primary"

# ------------------------------------------------------------ the day length
# `trace_packages.py` records effort in days of BASIS_HOURS, the working day
# this project measured itself against. The developer plans against a
# WORKING_HOURS day (2026-09-01), so the same work finishes in fewer days.
#
# What is fixed is the HOURS of work. Days are a presentation of them, and
# lengthening the day shortens the count - it does not add effort. Every
# figure shown in this workbook is therefore rescaled by BASIS/WORKING, while
# the consistency check below still runs against the unscaled register so the
# two documents can never quietly disagree about the underlying estimate.
BASIS_HOURS = 6.0
WORKING_HOURS = 8.0
SCALE = BASIS_HOURS / WORKING_HOURS
DAYS_PER_MONTH = 21


# ---------------------------------------------------------------- schedule
# Work starts 2026-09-02 (developer, 2026-09-01) and runs Monday to Friday;
# Saturday and Sunday are off. One engineer, so modules run back to back in
# the order they are listed - there is nobody to run two at once.
START_DATE = datetime.date(2026, 9, 2)
WEEKEND = (5, 6)                      # Saturday, Sunday
DATE_FMT = "ddd dd mmm yyyy"          # e.g. Wed 02 Sep 2026


def working_day(index):
    """The calendar date of the index-th working day from START_DATE.

    Index 0 is START_DATE itself when that is a weekday, otherwise the next
    weekday. Weekends are skipped and never consume a working day.
    """
    day = START_DATE
    while day.weekday() in WEEKEND:
        day += datetime.timedelta(days=1)
    remaining = index
    while remaining > 0:
        day += datetime.timedelta(days=1)
        if day.weekday() not in WEEKEND:
            remaining -= 1
    return day


def schedule(rows):
    """Assign each module a start and finish date, running them serially.

    Effort lands on quarter-days, so a module can begin on the same date the
    previous one ends - that is a real shared day, not an overlap of two
    engineers. Fractions are carried, never rounded per module, so the last
    finish date reflects the true total rather than an accumulation of
    round-ups.
    """
    cursor = 0.0
    for m in rows:
        first = int(math.floor(cursor))
        cursor += m.total
        last = max(first, int(math.ceil(cursor)) - 1)
        m.start_date = working_day(first)
        m.end_date = working_day(last)
    return rows


def to_days(register_days):
    """Register days (at BASIS_HOURS) -> days at WORKING_HOURS."""
    return register_days * SCALE


def to_hours(register_days):
    """Register days -> hours of work. This is the invariant."""
    return register_days * BASIS_HOURS


def a_(hours):
    """The correct indefinite article for '<n>-hour day'.

    Written out because the day length is a constant someone will change:
    "a 8-hour day" is the kind of error that survives a dozen readings.
    """
    return "an" if int(hours) in (8, 11, 18) else "a"

# ------------------------------------------------------------------ palette
INK = "1F2937"
NAVY = "1B3A5C"
NAVY_D = "12283F"
BAND = "DCE6F1"
ROW_A = "FFFFFF"
ROW_B = "F5F8FC"
TOTAL_BG = "CFE0F0"
READY_BG = "DDF0DC"
WAIT_BG = "FDECDC"
NOTE_BG = "FFF9E6"

HDR = Font(name="Calibri", size=10, bold=True, color="FFFFFF")
BASE = Font(name="Calibri", size=10, color=INK)
BASE_B = Font(name="Calibri", size=10, bold=True, color=INK)
SMALL = Font(name="Calibri", size=9, color="5A6472")
TITLE = Font(name="Calibri", size=16, bold=True, color="FFFFFF")
SUB = Font(name="Calibri", size=10, color="D6E2EE")
H2 = Font(name="Calibri", size=12, bold=True, color=NAVY)

_thin = Side(style="thin", color="BFCBD9")
BOX = Border(left=_thin, right=_thin, top=_thin, bottom=_thin)

WRAP = Alignment(vertical="top", horizontal="left", wrap_text=True)
CTR = Alignment(vertical="top", horizontal="center", wrap_text=True)
MIDL = Alignment(vertical="center", horizontal="left", wrap_text=True)
MIDC = Alignment(vertical="center", horizontal="center", wrap_text=True)


def fill(hexcolor):
    return PatternFill("solid", fgColor=hexcolor)


def put(ws, r, c, val, font=BASE, align=WRAP, bg=None, border=BOX):
    cell = ws.cell(row=r, column=c, value=val)
    cell.font = font
    cell.alignment = align
    if bg:
        cell.fill = fill(bg)
    if border:
        cell.border = border
    return cell


def autofit_rows(ws, first_row=3, line=13.5, floor=18):
    """Grow any row whose wrapped text would not fit its height.

    Row heights were hand-set per block, which silently clips as soon as a
    sentence is edited - the failure looks like a missing last line and is
    easy to miss. This pass measures every wrapped cell (spanning its merge)
    and raises the row if needed. It never shrinks a row, so deliberate
    breathing space is preserved.

    Excel's column width unit is the width of a '0'; running prose averages
    narrower, hence CPL_FACTOR.
    """
    CPL_FACTOR = 1.12
    spans = {(rng.min_row, rng.min_col): (rng.min_col, rng.max_col)
             for rng in ws.merged_cells.ranges}

    for r in range(first_row, ws.max_row + 1):
        needed = 0
        for c in range(1, ws.max_column + 1):
            cell = ws.cell(row=r, column=c)
            text = cell.value
            if not isinstance(text, str) or not text.strip():
                continue
            if not cell.alignment or not cell.alignment.wrap_text:
                continue
            c0, c1 = spans.get((r, c), (c, c))
            width = sum(ws.column_dimensions[get_column_letter(x)].width or 8.43
                        for x in range(c0, c1 + 1))
            per_line = max(width * CPL_FACTOR - 1, 4)
            lines = sum(max(1, -(-len(seg) // int(per_line)))
                        for seg in text.split("\n"))
            needed = max(needed, lines * line)
        if needed:
            current = ws.row_dimensions[r].height or 0
            ws.row_dimensions[r].height = max(current, needed, floor)


def banner(ws, ncols, title, subtitle):
    """Two merged rows of dark navy at the top of a sheet."""
    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=ncols)
    c = ws.cell(row=1, column=1, value=title)
    c.font = TITLE
    c.fill = fill(NAVY_D)
    c.alignment = Alignment(vertical="center", horizontal="left", indent=1)
    ws.row_dimensions[1].height = 30

    ws.merge_cells(start_row=2, start_column=1, end_row=2, end_column=ncols)
    c = ws.cell(row=2, column=1, value=subtitle)
    c.font = SUB
    c.fill = fill(NAVY)
    c.alignment = Alignment(vertical="center", horizontal="left", indent=1,
                            wrap_text=True)
    ws.row_dimensions[2].height = 30
    for col in range(1, ncols + 1):
        ws.cell(row=1, column=col).fill = fill(NAVY_D)
        ws.cell(row=2, column=col).fill = fill(NAVY)


# ------------------------------------------------------------- derived data
def requirement_index():
    """rid -> (srs_section, phase). Section is the level-1 heading it sits under."""
    out = {}
    section = None
    for row in SRS.REQUIREMENTS:
        if row[0] == "S":
            if row[1] == 1:
                section = row[2]
        else:
            out[row[1]] = (section, row[6])
    return out


def verdict_of(rid):
    """(verdict, owning work package) for one requirement, from trace_status."""
    n = int(rid.split("_")[-1])
    if n in ST.SPECIFIC:
        return ST.SPECIFIC[n][0], ST.SPECIFIC[n][2]
    for first, last, verd, _evidence, wp in ST.RANGES:
        if first <= n <= last:
            return verd, wp
    raise KeyError("no verdict for %s" % rid)


REQ_INDEX = requirement_index()
PACKAGES = {p[0]: p for p in PKG.PACKAGES}

# wp -> counts of each verdict
WP_VERDICTS = defaultdict(lambda: defaultdict(int))
for _rid in REQ_INDEX:
    _v, _w = verdict_of(_rid)
    WP_VERDICTS[_w][_v] += 1

PRIO_RANK = {"P1": 0, "P2": 1, "P3": 2, "Phase 2": 9}
BUILT_VERDICTS = ("DONE", "PARTIAL")


_ID = re.compile(r"^(OI-\d+|TBD-\d+|T-\d+|REQ_\d+)")


def merge_blockers(strings):
    """One de-duplicated blocker list for a module.

    A module can gather several work packages, and two of them often name the
    same open issue. Listing 'OI-11 (error catalogue); OI-11' helps nobody, so
    each register ID appears once, keeping whichever mention carries the
    fuller explanation.
    """
    seen = {}
    order = []
    for raw in strings:
        raw = (raw or "").strip()
        if not raw or raw.lower() == "none":
            continue
        for part in re.split(r"[;,]\s*(?=OI-|TBD-|T-\d|REQ_)|;\s*", raw):
            part = part.strip().rstrip(",").strip()
            # "none beyond WP-P01" names a dependency, not an open question;
            # it would read as a blocker in a column of OI/TBD numbers.
            if not part or part.lower().startswith("none"):
                continue
            m = _ID.match(part)
            key = m.group(1) if m else part.lower()
            if key not in seen:
                seen[key] = part
                order.append(key)
            elif len(part) > len(seen[key]):
                seen[key] = part
    return ", ".join(seen[k] for k in order)


class Row(object):
    """One module, with every number derived from the work packages it owns."""

    def __init__(self, mid, name, section, wps, blurb):
        self.mid = mid
        self.name = name
        self.section = section
        self.wps = wps
        self.blurb = blurb

        # Unscaled register days - what the consistency check compares.
        self.impl_reg = sum(PACKAGES[w][3] for w in wps)
        self.hil_reg = sum(PACKAGES[w][4] for w in wps)
        self.total_reg = self.impl_reg + self.hil_reg

        # Hours of work: the invariant, independent of the day length.
        self.impl_hours = to_hours(self.impl_reg)
        self.hil_hours = to_hours(self.hil_reg)
        self.total_hours = self.impl_hours + self.hil_hours

        # Days at the working day length - what the workbook shows.
        self.impl = to_days(self.impl_reg)
        self.hil = to_days(self.hil_reg)
        self.total = self.impl + self.hil

        prios = [PACKAGES[w][5] for w in wps]
        self.priority = sorted(prios, key=lambda p: PRIO_RANK.get(p, 5))[0]

        self.blockers = merge_blockers(PACKAGES[w][6] for w in wps)

        self.reqs = sum(sum(WP_VERDICTS[w].values()) for w in wps)
        self.built = sum(WP_VERDICTS[w][v] for w in wps for v in BUILT_VERDICTS)

    @property
    def ready(self):
        return "Ready" if not self.blockers else "Waiting"

    @property
    def waiting_on(self):
        if not self.blockers:
            return "-"
        return self.blockers


def build_rows(defs):
    return [Row(*d) for d in defs]


PRIMARY = schedule(build_rows(MOD.PRIMARY_MODULES))


# ------------------------------------------------------------- self-checks
def check():
    """Fail the build rather than publish a number that has quietly drifted.

    This workbook covers the Primary Board only (developer, 2026-09-01), so
    the guarantee is scoped to Primary-owned packages: every one of them is
    claimed by exactly one module, and the module totals equal the register
    totals. Battery Manager packages are out of scope BY OWNER, which the
    check asserts explicitly - dropping a sheet must not become a quiet way
    for a Primary package to escape the claim rule.
    """
    claimed = defaultdict(list)
    for row in PRIMARY:
        for w in row.wps:
            claimed[w].append(row.mid)

    excluded = {e[0] for e in MOD.EXCLUDED}
    problems = []

    for wp, owners in claimed.items():
        if len(owners) > 1:
            problems.append("%s is claimed by %s" % (wp, " and ".join(owners)))
        if PACKAGES[wp][2] != OWNER:
            problems.append("%s is owned by '%s', not '%s', but is claimed by "
                            "%s on a %s-only sheet"
                            % (wp, PACKAGES[wp][2], OWNER,
                               " and ".join(owners), OWNER))

    for wp, pkg in PACKAGES.items():
        if pkg[2] != OWNER or wp in excluded:
            continue
        if wp not in claimed:
            problems.append("%s is claimed by no module - its effort would "
                            "vanish from the totals" % wp)

    for wp in excluded:
        if wp in claimed:
            problems.append("%s is excluded but still claimed by a module" % wp)

    reg_impl = sum(p[3] for p in PKG.PACKAGES
                   if p[2] == OWNER and p[0] not in excluded)
    reg_hil = sum(p[4] for p in PKG.PACKAGES
                  if p[2] == OWNER and p[0] not in excluded)

    # Compared unscaled, so the check still pins this workbook to the
    # register regardless of the working day length chosen above.
    got_impl = sum(r.impl_reg for r in PRIMARY)
    got_hil = sum(r.hil_reg for r in PRIMARY)

    for label, want, got in (("Primary build days", reg_impl, got_impl),
                             ("Primary board-test days", reg_hil, got_hil)):
        if want != got:
            problems.append("%s: register says %d, modules add to %d"
                            % (label, want, got))

    if problems:
        print("BUILD FAILED - the module grouping and the work-package "
              "register disagree:\n")
        for p in problems:
            print("  * " + p)
        sys.exit(1)

    print("Consistency check: %d work packages, each claimed exactly once; "
          "totals match the register." % len(claimed))


# --------------------------------------------------------------- sheet 0
def sheet_readme(wb):
    ws = wb.create_sheet("0. Read Me")
    ws.sheet_view.showGridLines = False
    widths = [3, 30, 11, 12, 12, 11, 12, 14]
    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = w

    banner(ws, 8, "ME Primary Board - Phase 1 Implementation Estimates",
           "Module-by-module effort for the Primary Board.   Rev %s, %s.   "
           "Code state as of %s."
           % (REV, DOC_DATE, CODE_DATE))

    r = 4
    put(ws, r, 2, "What this workbook is", H2, MIDL, border=None)
    r += 1
    ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
    put(ws, r, 2,
        "Every Primary Board module of Phase 1 that still has to be built, "
        "what it covers in plain English, how much of it already exists, "
        "and what it will cost in engineer-days. This workbook covers the "
        "Primary Board only. Phase 2 work, the Battery Manager web "
        "application, Secondary Board firmware, M7 firmware and hardware "
        "bring-up are all outside these totals - see the exclusions at the "
        "foot of this sheet.",
        BASE, WRAP, border=None)
    ws.row_dimensions[r].height = 62

    r += 2
    put(ws, r, 2, "The bottom line", H2, MIDL, border=None)
    r += 1

    p_impl = sum(x.impl for x in PRIMARY)
    p_hil = sum(x.hil for x in PRIMARY)
    p_hours = sum(x.total_hours for x in PRIMARY)
    p_total = p_impl + p_hil

    # Column 2 carries the row label; the six figures sit in columns 3-8.
    head = ["Track", "Modules", "Build\ndays", "Board-test\ndays",
            "Total\ndays", "Total\nhours", "Months\nat %d days" % DAYS_PER_MONTH]
    for c, h in enumerate(head, start=2):
        put(ws, r, c, h, HDR, MIDL if c == 2 else MIDC, NAVY)
    ws.row_dimensions[r].height = 30

    # One track, so a separate TOTAL row would just repeat this one.
    r += 1
    put(ws, r, 2, "Primary Board  (1 engineer)", BASE_B, MIDL, TOTAL_BG)
    for c, v in enumerate((len(PRIMARY), p_impl, p_hil, p_total, p_hours,
                           round(p_total / DAYS_PER_MONTH, 1)), start=3):
        cell = put(ws, r, c, v, BASE_B, MIDC, TOTAL_BG)
        if c in (4, 5, 6, 8):
            cell.number_format = "General"
    ws.row_dimensions[r].height = 20

    r += 1
    put(ws, r, 2, "Calendar time", BASE_B, MIDL, TOTAL_BG)
    ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=8)
    put(ws, r, 3,
        "%g days  ~  %.1f months, one engineer working serially."
        % (p_total, p_total / DAYS_PER_MONTH),
        BASE_B, MIDL, TOTAL_BG)
    for c in range(4, 9):
        ws.cell(row=r, column=c).fill = fill(TOTAL_BG)
        ws.cell(row=r, column=c).border = BOX

    r += 1
    put(ws, r, 2, "Start and finish", BASE_B, MIDL, TOTAL_BG)
    ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=8)
    put(ws, r, 3,
        "%s  to  %s,  working Monday to Friday."
        % (PRIMARY[0].start_date.strftime("%a %d %b %Y"),
           PRIMARY[-1].end_date.strftime("%a %d %b %Y")),
        BASE_B, MIDL, TOTAL_BG)
    for c in range(4, 9):
        ws.cell(row=r, column=c).fill = fill(TOTAL_BG)
        ws.cell(row=r, column=c).border = BOX
    ws.row_dimensions[r].height = 34

    r += 2
    put(ws, r, 2, "The thing to act on", H2, MIDL, border=None)
    r += 1
    ready_days = sum(x.total for x in PRIMARY if x.ready == "Ready")
    ready_n = sum(1 for x in PRIMARY if x.ready == "Ready")
    all_days = p_total
    ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
    put(ws, r, 2,
        "Only %d of the %d modules can be started today - %g days out of %g. "
        "Every other module is waiting on a question nobody has answered "
        "yet: what 'safe state' means, what the control-loop rate is, how "
        "the Modbus register map is laid out for 64 channels, and so on. "
        "Those modules still carry their full estimate, because being "
        "blocked stops the work starting, it does not make it smaller. "
        "Answering the open questions is therefore the highest-value thing "
        "anyone can do for this schedule - it does not remove days, but it "
        "is what lets the days be spent. The questions are listed with "
        "owners in Requirements/Code_Tracebility/"
        "ME_Open_Issues_and_TBD_Register.md."
        % (ready_n, len(PRIMARY), ready_days, all_days),
        BASE, WRAP, WAIT_BG)
    ws.row_dimensions[r].height = 90

    r += 2
    put(ws, r, 2, "How to read a row", H2, MIDL, border=None)
    r += 1
    for term, meaning in [
        ("Build days",
         "Writing the module with Claude Code: the design note, the tests, "
         "the code, and both builds passing. Unit tests are included in this "
         "number, not added on top of it."),
        ("Board-test days",
         "Proving the module on the real i.MX8M Plus board with a real "
         "Secondary Board attached. This is the number that AI does not "
         "shrink, because it needs hardware, one engineer and one board at a "
         "time."),
        ("Total hours",
         "The same effort expressed in hours. The hours do not change; the "
         "day columns are simply those hours divided by %s %g-hour working "
         "day." % (a_(WORKING_HOURS), WORKING_HOURS)),
        ("Start and Finish",
         "When the module is worked on, assuming it is picked up in the "
         "order listed and that work runs Monday to Friday from %s. There is "
         "one engineer, so the modules run one after another - a module can "
         "begin on the date the previous one ends, because the estimates "
         "land on quarter-days and that last day is genuinely shared."
         % PRIMARY[0].start_date.strftime("%d %b %Y")),
        ("Already built",
         "How many of the module's requirements the current code already "
         "meets in full or in part. A high number means the module is "
         "finishing work, not starting it."),
        ("Priority",
         "P1 is needed for a working Phase 1 system. P2 matters for "
         "production use. P3 is a third-party interface that no ME board "
         "needs in order to run a test."),
        ("Start now?",
         "'Ready' means the specification is complete enough to begin. "
         "'Waiting' means an open question has to be answered first - the "
         "next column names it."),
        ("Waiting on",
         "The open issue (OI-nn) or to-be-decided item (TBD-nn) that gates "
         "the module. The full register is in "
         "Requirements/Code_Tracebility/ME_Open_Issues_and_TBD_Register.md."),
    ]:
        put(ws, r, 2, term, BASE_B, WRAP)
        ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=8)
        put(ws, r, 3, meaning, BASE, WRAP)
        for c in range(4, 9):
            ws.cell(row=r, column=c).border = BOX
        ws.row_dimensions[r].height = 32
        r += 1

    r += 1
    put(ws, r, 2, "What one day means", H2, MIDL, border=None)
    r += 1
    ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
    put(ws, r, 2,
        "One engineer-day here is %g productive hours, working with Claude "
        "Code the way this project has worked since August 2026: test first, "
        "a written design note for every real decision, and both builds "
        "green before anything is called done. These are already AI-assisted "
        "figures, so do not discount them a second time for AI. "
        "They are calibrated against measured output, not guessed: 16 "
        "sessions between 6 and 21 August 2026 delivered roughly 7,500 lines "
        "of source, 4,600 lines of tests and 39 design decisions across about "
        "13 work packages. Modules are priced against that, then loaded for "
        "the two things that were not true of those sessions: hardware that "
        "does not exist yet, and specifications that have not been written "
        "yet."
        % WORKING_HOURS,
        BASE, WRAP, NOTE_BG)
    ws.row_dimensions[r].height = 116

    r += 1
    ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
    put(ws, r, 2,
        "What is fixed is the HOURS of work, shown in the 'Total hours' "
        "column above - %s hours for the Primary Board. Days are a "
        "presentation of those hours, so the day length changes the day count "
        "and nothing else. The underlying estimate was recorded against a "
        "%g-hour day in Code_Tracebility/ME_Work_Packages_and_Estimates.md, "
        "which is why that document shows %d days where this one shows %g: "
        "the same work, divided by a longer day. Neither figure is a "
        "correction of the other."
        % ("{:,}".format(int(p_hours)), BASIS_HOURS,
           int(p_hours / BASIS_HOURS), p_total),
        BASE, WRAP, NOTE_BG)
    ws.row_dimensions[r].height = 66

    r += 2
    put(ws, r, 2, "Four things to know before quoting a date", H2, MIDL,
        border=None)
    r += 1
    for n, text in enumerate([
        "One engineer. Nothing in this plan can be split across people, so "
        "the days add directly into calendar days.",
        "The dates count weekends as the only non-working days. Public "
        "holidays, leave and sick days are not modelled and will push every "
        "date after them to the right. Subtract your own holiday calendar "
        "before committing to a finish date.",
        "A blocked module still costs its full estimate, and the dates "
        "assume its open question is answered before its start date "
        "arrives. Most modules on this sheet are waiting on one. Every "
        "question answered late moves its module, and everything after it, "
        "by however long the answer took.",
        "Board-test days will not come down. Hardware-in-the-loop testing "
        "needs the board, a real Secondary, an M7 build and a running "
        "Battery Manager on the same network. A passing build on the PC "
        "proves nothing about the target.",
    ], start=1):
        put(ws, r, 2, str(n), BASE_B, MIDC)
        ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=8)
        put(ws, r, 3, text, BASE, WRAP)
        for c in range(4, 9):
            ws.cell(row=r, column=c).border = BOX
        ws.row_dimensions[r].height = 40
        r += 1

    r += 1
    put(ws, r, 2, "Excluded from these totals", H2, MIDL, border=None)
    r += 1
    for wp, title, owner, why in MOD.EXCLUDED:
        put(ws, r, 2, wp, BASE_B, WRAP)
        put(ws, r, 3, title, BASE, WRAP)
        ws.merge_cells(start_row=r, start_column=4, end_row=r, end_column=8)
        put(ws, r, 4, why, BASE, WRAP)
        for c in range(5, 9):
            ws.cell(row=r, column=c).border = BOX
        ws.row_dimensions[r].height = 20
        r += 1
    # Phase 1 only: WP-B16 is Phase 2 and must not inflate this figure.
    bm_days = to_days(sum(p[3] + p[4] for p in PKG.PACKAGES
                          if p[2] == "BM" and p[5] != "Phase 2"))
    for label, why in [
        ("Battery Manager web application",
         "A separate Phase 1 track with its own engineer, and not counted "
         "here. It is about %d more days at the same day length. Its "
         "packages are listed in "
         "Code_Tracebility/ME_Work_Packages_and_Estimates.md. Several of "
         "them cannot finish ahead of the Primary module they pair with, so "
         "do not read this sheet as the whole of Phase 1."
         % round(bm_days)),
        ("Secondary Board firmware, M7 firmware, mechanical and electrical "
         "bring-up",
         "Outside the scope of this software plan entirely."),
    ]:
        put(ws, r, 2, label, BASE_B, WRAP)
        ws.merge_cells(start_row=r, start_column=3, end_row=r, end_column=8)
        put(ws, r, 3, why, BASE, WRAP)
        for c in range(4, 9):
            ws.cell(row=r, column=c).border = BOX
        # Clear whichever wraps taller: the label (col 2, ~30 chars wide) or
        # the reason (cols 3-8 merged, ~76 chars wide).
        lines = max(-(-len(label) // 30), -(-len(why) // 76), 1)
        ws.row_dimensions[r].height = max(20, 15 * lines)
        r += 1
    r += 1

    ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
    put(ws, r, 2,
        "Generated from Requirements/ME_Phase1_Estimates/tools/"
        "build_phase1_estimates.py. Effort and blockers are read from "
        "Code_Tracebility/tools/trace_packages.py; build state is read from "
        "trace_status.py. Do not hand-edit this workbook - it is overwritten "
        "on every build.",
        SMALL, WRAP, border=None)
    autofit_rows(ws)
    return ws


# --------------------------------------------------------------- module sheet
def sheet_modules(wb, title, subtitle, rows, with_hil, reqs_note):
    ws = wb.create_sheet(title)
    ws.sheet_view.showGridLines = False

    cols = ["#", "Module", "What it covers", "SRS\nsection", "Reqs",
            "Already\nbuilt", "Build\ndays"]
    widths = [6, 27, 60, 11, 7, 8, 8]
    if with_hil:
        cols += ["Board-test\ndays"]
        widths += [10]
    cols += ["Total\ndays", "Total\nhours", "Start", "Finish", "Priority",
             "Start\nnow?", "Waiting on", "Work packages"]
    widths += [8, 9, 16, 16, 9, 9, 28, 15]

    ncols = len(cols)
    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = w

    banner(ws, ncols, title.split(". ", 1)[-1], subtitle)

    hr = 4
    for c, h in enumerate(cols, start=1):
        put(ws, hr, c, h, HDR, MIDC, NAVY)
    ws.row_dimensions[hr].height = 32

    r = hr
    for i, m in enumerate(rows):
        r += 1
        bg = ROW_A if i % 2 == 0 else ROW_B
        put(ws, r, 1, m.mid, BASE_B, CTR, bg)
        put(ws, r, 2, m.name, BASE_B, WRAP, bg)
        put(ws, r, 3, m.blurb, BASE, WRAP, bg)
        put(ws, r, 4, m.section, BASE, CTR, bg)
        put(ws, r, 5, m.reqs if m.reqs else "-", BASE, CTR, bg)
        put(ws, r, 6, m.built if m.reqs else "-", BASE, CTR, bg)
        c = 7
        put(ws, r, c, m.impl, BASE, CTR, bg).number_format = "General"; c += 1
        if with_hil:
            put(ws, r, c, m.hil, BASE, CTR, bg).number_format = "General"; c += 1
        put(ws, r, c, m.total, BASE_B, CTR, bg).number_format = "General"; c += 1
        put(ws, r, c, m.total_hours, BASE, CTR, bg); c += 1
        # Real dates, not text, so the sheet sorts and filters by them.
        for when in (m.start_date, m.end_date):
            put(ws, r, c, when, BASE, CTR, bg).number_format = DATE_FMT
            c += 1
        put(ws, r, c, m.priority, BASE, CTR, bg); c += 1
        put(ws, r, c, m.ready, BASE_B, CTR,
            READY_BG if m.ready == "Ready" else WAIT_BG); c += 1
        put(ws, r, c, m.waiting_on, BASE, WRAP, bg); c += 1
        put(ws, r, c, ", ".join(m.wps), SMALL, WRAP, bg)
        # Height must clear the tallest wrapped cell in the row, which is
        # either the description (column 3) or the blocker list near the end.
        lines = max(-(-len(m.blurb) // 58), -(-len(m.waiting_on) // 28), 2)
        ws.row_dimensions[r].height = max(30, 13.5 * lines)

    last_module_row = r

    r += 1
    put(ws, r, 1, "", BASE_B, CTR, TOTAL_BG)
    put(ws, r, 2, "TOTAL", BASE_B, MIDL, TOTAL_BG)
    put(ws, r, 3, "%d modules" % len(rows), BASE_B, MIDL, TOTAL_BG)
    put(ws, r, 4, "", BASE, CTR, TOTAL_BG)
    put(ws, r, 5, sum(x.reqs for x in rows), BASE_B, CTR, TOTAL_BG)
    put(ws, r, 6, sum(x.built for x in rows), BASE_B, CTR, TOTAL_BG)
    c = 7
    put(ws, r, c, sum(x.impl for x in rows),
        BASE_B, CTR, TOTAL_BG).number_format = "General"; c += 1
    if with_hil:
        put(ws, r, c, sum(x.hil for x in rows),
            BASE_B, CTR, TOTAL_BG).number_format = "General"; c += 1
    put(ws, r, c, sum(x.total for x in rows),
        BASE_B, CTR, TOTAL_BG).number_format = "General"; c += 1
    put(ws, r, c, sum(x.total_hours for x in rows),
        BASE_B, CTR, TOTAL_BG); c += 1
    put(ws, r, c, rows[0].start_date,
        BASE_B, CTR, TOTAL_BG).number_format = DATE_FMT; c += 1
    put(ws, r, c, rows[-1].end_date,
        BASE_B, CTR, TOTAL_BG).number_format = DATE_FMT; c += 1
    for cc in range(c, ncols + 1):
        put(ws, r, cc, "", BASE, CTR, TOTAL_BG)
    ws.row_dimensions[r].height = 22

    ready = sum(x.total for x in rows if x.ready == "Ready")
    n_ready = sum(1 for x in rows if x.ready == "Ready")
    total_days = sum(x.total for x in rows)

    r += 2
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=ncols)
    put(ws, r, 1,
        "Reading order.  Modules are listed in the order the work should be "
        "done, not by SRS number: what unblocks other modules comes first, "
        "and the independent third-party interfaces come last.",
        SMALL, MIDL, NOTE_BG, border=None)
    ws.row_dimensions[r].height = 16

    r += 1
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=ncols)
    put(ws, r, 1,
        "Ready to start today.  %d of %d modules, worth %g of the %g days on "
        "this sheet. Every other module needs an open question answered "
        "first - the 'Waiting on' column names it. Those modules are not "
        "cheaper for being blocked; they simply cannot be started."
        % (n_ready, len(rows), ready, total_days),
        SMALL, MIDL, NOTE_BG, border=None)
    ws.row_dimensions[r].height = 28

    r += 1
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=ncols)
    put(ws, r, 1,
        "About the dates.  One engineer, so modules run one after another in "
        "the order listed, Monday to Friday from %s. A module can begin on "
        "the date the previous one ends because the estimates land on "
        "quarter-days and that day is genuinely shared. Weekends are the "
        "only non-working days modelled - public holidays and leave are not, "
        "and will move every later date to the right."
        % rows[0].start_date.strftime("%d %b %Y"),
        SMALL, MIDL, NOTE_BG, border=None)
    ws.row_dimensions[r].height = 28

    r += 1
    ws.merge_cells(start_row=r, start_column=1, end_row=r, end_column=ncols)
    put(ws, r, 1, reqs_note, SMALL, MIDL, NOTE_BG, border=None)
    ws.row_dimensions[r].height = 40

    autofit_rows(ws, first_row=hr)
    ws.freeze_panes = ws.cell(row=hr + 1, column=3)
    ws.auto_filter.ref = "A%d:%s%d" % (hr, get_column_letter(ncols),
                                       last_module_row)
    return ws


def main():
    check()
    wb = Workbook()
    wb.remove(wb.active)

    sheet_readme(wb)
    sheet_modules(
        wb, "1. Primary Board Modules",
        "%d modules, one engineer, %g days at %g hours a day.   Build days "
        "include unit tests; board-test days are hardware-in-the-loop on the "
        "i.MX8M Plus."
        % (len(PRIMARY), sum(x.total for x in PRIMARY), WORKING_HOURS),
        PRIMARY, with_hil=True,
        reqs_note=(
            "About the 'Reqs' column.  It counts the requirements this "
            "module's work packages own, which is very close to - but not "
            "always exactly - the count of its SRS section, because a few "
            "requirements are implemented by a module other than the one "
            "their section number suggests. M27 owns no requirement of its "
            "own: it is code debt the team has already written down."))

    if os.path.exists(OUT):
        os.remove(OUT)
    wb.save(OUT)
    print("Wrote %s" % OUT)
    print("  Day length      : %g productive hours (register basis %g)"
          % (WORKING_HOURS, BASIS_HOURS))
    print("  Primary Board   : %2d modules, %6.2f days = %5.0f h "
          "(%.2f build + %.2f board test)"
          % (len(PRIMARY), sum(x.total for x in PRIMARY),
             sum(x.total_hours for x in PRIMARY),
             sum(x.impl for x in PRIMARY), sum(x.hil for x in PRIMARY)))
    print("  Scope           : %s-owned packages only; %d Battery Manager "
          "packages are out of scope"
          % (OWNER, sum(1 for p in PKG.PACKAGES if p[2] == "BM")))


if __name__ == "__main__":
    main()
