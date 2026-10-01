# -*- coding: utf-8 -*-
"""Content for ME_Primary_SRS_V0.1.xlsx.  Edit here, then run build_me_srs.py.

Row helpers
-----------
S(level, title)                     -> a section banner row
R(id, statement, ...)               -> one requirement / information row

Every ME requirement that came from the old BTS analysis carries its BTS
requirement number in `traces`, so sheet 4 can prove nothing was lost.
"""

TITLE = "ME Battery Test System — Software Requirements Specification, Phase 1"
SUBTITLE = ("Scope: Primary Board software and Battery Manager (BM) PC application only. "
            "Secondary Board internal requirements are deliberately excluded.  "
            "Draft V0.1 — not approved.")

# ---------------------------------------------------------------- vocabularies
V_TYPE = ["Requirement", "Information", "Description", "Constraint"]
V_CATEGORY = ["Functional", "Interface", "Performance", "Safety", "Configuration",
              "Data", "Diagnostic", "Usability", "Capacity"]
V_APPLICABILITY = ["BM", "Primary", "BM + Primary"]
V_PHASE = ["Phase 1", "Phase 2"]
V_CARDINALITY = ["Per System", "Per Secondary", "Per Channel", "Per CAN Port",
                 "Per Program", "N/A"]
V_CHANGE = ["Carried Over", "Modified", "New"]
V_VERIFICATION = ["Test", "Analysis", "Inspection", "Demonstration", "HIL Test"]
V_STATUS = ["Draft", "Agreed", "Pending", "Need to check", "Deferred"]


def S(level, title):
    return ("S", level, title)


def R(rid, stmt, typ="Requirement", cat="Functional", app="Primary", phase="Phase 1",
      card="Per System", traces="", change="Carried Over", verif="Test", ref="",
      status="Draft", rem=""):
    return ("R", rid, stmt, typ, cat, app, phase, card, traces, change, verif,
            ref, status, rem)


# ================================================================== 0. READ ME
README = [
    ("H", "1.  What this document is", None),
    ("P", "This is the Software Requirements Specification (SRS) for Phase 1 of the ME "
          "Battery Test System. It states what the software must do. It deliberately "
          "does not state how to build it — no code structure, no CAN frame layouts, no "
          "thread design. Those belong in the design documents that follow.", None),
    ("P", "This workbook is the single guide for the development team. Where a number or "
          "a rule could not be found in any source document it is written as "
          "<TBD-nn> and registered on sheet '5. Open Issues and TBD'. Nothing was "
          "invented and left unmarked.", None),
    ("GAP", None, None),

    ("H", "2.  What is in scope, and what is not", None),
    ("KV", "IN scope",
     "Software running on the ME Primary Board, and the Battery Manager (BM) "
     "application running on the operator's PC."),
    ("KV", "OUT of scope",
     "The internal behaviour of the Secondary Board — its ADC handling, its "
     "analog output generation, its transistor-bank drive, its relays and its "
     "auto-ranging. Those are specified separately.\n"
     "Where the Primary must command, supervise or aggregate something the Secondary "
     "does, that Primary-side obligation IS in this document."),
    ("KV", "Why the split",
     "The reference document 'BTS_Primary_SW_Requirement_Analysis V1.7.xlsx' carried "
     "an Applicability column. Rows marked 'Secondary' were removed. Rows marked "
     "'Primary + Secondary' were NOT removed — they were rewritten to state only the "
     "Primary's half of the obligation, because in ME the Primary is the decision "
     "maker. See sheet 4 for the disposition of every single reference row."),
    ("GAP", None, None),

    ("H", "3.  How to read a requirement row", None),
    ("KV", "Requirement ID",
     "ME_SW_REQ_n. Stable — never renumber. If a requirement dies, mark it withdrawn; "
     "do not reuse its number."),
    ("KV", "Type",
     "Requirement = binding, uses 'shall'.  Information = context, not testable.  "
     "Description = explains mechanism.  Constraint = imposed limit, not negotiable."),
    ("KV", "Applicability",
     "BM = the PC application only.  Primary = the Primary Board software only.  "
     "BM + Primary = both ends must agree; usually one configures and the other honours."),
    ("KV", "Cardinality",
     "The most important new column. It answers: how many times does this exist?\n"
     "Per System = once for the whole ME unit.\n"
     "Per Secondary = once for each of the up to 8 Secondary Boards.\n"
     "Per Channel = once for each of the up to 64 channels — independently.\n"
     "Read this before writing any data structure."),
    ("KV", "Traces To (BTS)",
     "The requirement number in BTS_Primary_SW_Requirement_Analysis V1.7 that this "
     "came from. Blank means it is new to ME."),
    ("KV", "Change",
     "Carried Over = same intent as BTS, wording tidied only.\n"
     "Modified = intent changed because of the new topology — read carefully, do not "
     "assume the old implementation still applies.\n"
     "New = no equivalent existed in BTS."),
    ("KV", "Verification",
     "How this will be proved. HIL Test means it cannot be proved without real "
     "hardware and a real battery — plan bench time for these."),
    ("GAP", None, None),

    ("H", "4.  Colour key", None),
    ("KV", "Green cell in Change", "Modified — the topology change altered this requirement."),
    ("KV", "Yellow cell in Change", "New — no BTS ancestor."),
    ("KV", "Pink statement cell", "Contains at least one <TBD-nn>. Cannot be implemented "
                                  "as written until the TBD is answered."),
    ("GAP", None, None),

    ("H", "5.  Sheets in this workbook", None),
    ("KV", "0. Read Me", "This page."),
    ("KV", "1. Revision History", "Who changed what, when."),
    ("KV", "2. Topology and Terms", "The 1 : 8 : 8 model, the scope boundary drawing, "
                                    "and the vocabulary. Read this before sheet 3."),
    ("KV", "3. ME SW Requirements", "The requirements. Filter by Applicability to get "
                                    "your own work list."),
    ("KV", "4. Traceability BTS to ME", "Every row of the old analysis and what happened "
                                        "to it. This is the review sheet."),
    ("KV", "5. Open Issues and TBD", "Every unanswered question, with who is blocked."),
    ("KV", "6. References and Legend", "Source documents and column definitions."),
]

# ============================================================ 1. REVISION HIST
REVISIONS = [
    ("V0.1", "2026-08-26", "N. Nevrekar", "—", "—",
     "First draft. Derived from BTS_Primary_SW_Requirement_Analysis V1.7 by removing "
     "Secondary-scoped requirements, rewriting Primary+Secondary requirements as "
     "Primary-only obligations, and adding the requirements created by the new "
     "1 Primary : 8 Secondaries : 64 Channels topology. Not reviewed, not approved."),
]

# ========================================================= 2. TOPOLOGY & TERMS
TOPOLOGY = [
    ("H", "1.  The ME topology in one picture", None, None),
    ("ART",
     "        +-------------------------------+\n"
     "        |   Battery Manager (BM)        |   PC application, one per system\n"
     "        |   - writes test programs      |\n"
     "        |   - assigns them to channels  |\n"
     "        |   - shows live data, reports  |\n"
     "        +---------------+---------------+\n"
     "                        |  Ethernet / TCP-IP   (1 link)\n"
     "        +---------------+---------------+\n"
     "        |   ME PRIMARY BOARD            |   <-- everything in this document\n"
     "        |   - holds up to 64 programs   |       lives here, or in BM above\n"
     "        |   - decides every step for    |\n"
     "        |     every channel             |\n"
     "        |   - commands the Secondaries  |\n"
     "        |   - logs and reports          |\n"
     "        +---------------+---------------+\n"
     "                        |  CAN bus   (shared, up to 8 Secondaries)\n"
     "     +--------+---------+---------+ ... +--------+\n"
     "     |  SEC 1 |  SEC 2  |  SEC 3  |     |  SEC 8 |   Secondary Boards\n"
     "     | ch 1-8 | ch 1-8  | ch 1-8  |     | ch 1-8 |   8 channels each\n"
     "     +--------+---------+---------+ ... +--------+\n"
     "        |||||||| \n"
     "        64 batteries, each on its own independent test program\n"
     "        ---- BELOW THIS LINE IS OUT OF SCOPE FOR THIS DOCUMENT ----",
     None, None),
    ("GAP", None, None, None),

    ("H", "2.  What changed from BTS, and why it matters", None, None),
    ("TH", "Aspect", "Old BTS", "New ME"),
    ("TR", "Channels per Primary", "1 Secondary, driving exactly 1 channel.",
     "Up to 8 Secondaries on one shared CAN bus, each driving up to 8 channels — "
     "64 channels in total."),
    ("TR", "Who decides the next program step",
     "The Secondary held and executed the program.",
     "The PRIMARY holds and executes every program. The Secondary only regulates "
     "current/voltage to the setpoint it is given. This is the single largest change "
     "in this document."),
    ("TR", "Programs in the system", "One program, for the one battery.",
     "Up to 64 programs, one per channel, all running at the same time and all "
     "unrelated to each other. A channel may be idle while its neighbour is on step 40 "
     "of a 12-hour cycle."),
    ("TR", "Link to the Secondary", "Point-to-point serial. One talker, one listener.",
     "Shared CAN bus with up to 9 nodes. Needs addressing, bus-load budgeting, "
     "per-node timeout supervision and node discovery."),
    ("TR", "Host application", "Web application in a browser.",
     "PC / desktop application (Battery Manager) over Ethernet TCP/IP."),
    ("TR", "Effect of one fault",
     "A fault stopped the test — there was only one test.",
     "A fault on one channel must NOT disturb the other 63. Fault containment is now "
     "a first-class requirement, not an afterthought."),
    ("TR", "Calibration data", "One set of calibration constants.",
     "Up to 64 sets, and they must survive a Secondary Board being swapped."),
    ("GAP", None, None, None),

    ("H", "3.  Terminology used in this document", None, None),
    ("TH", "Term", "Meaning", "Notes"),
    ("TR", "Primary Board",
     "The single controlling computer of the ME system. Called 'Main Controller' in "
     "the hardware paperwork.",
     "All 'Primary' requirements in this document run here."),
    ("TR", "Secondary Board",
     "A board that drives up to 8 channels. Called 'Digital Controller Transcard' in "
     "the hardware paperwork.",
     "Its internals are OUT of scope. Only the Primary's view of it is in scope."),
    ("TR", "Channel",
     "One battery position. Identified by the pair (Secondary ID, Channel Index).",
     "1 to 8 within a Secondary; 64 in the system."),
    ("TR", "Channel Address",
     "The system-wide identity of a channel, formed from Secondary ID (1-8) and "
     "Channel Index (1-8).",
     "Every command, measurement, log record and error must carry it."),
    ("TR", "BM / Battery Manager",
     "The PC application the operator uses. Authors programs, assigns them, starts and "
     "stops tests, displays live data, stores results.",
     "Requirements marked 'BM' are its responsibility."),
    ("TR", "Program",
     "An ordered list of Steps that defines one battery test.",
     "Authored in BM, compiled by BM, stored and executed by the Primary."),
    ("TR", "Step",
     "One line of a Program: Step No, Label, Operator, Nominal Value, Limit, Action, "
     "Registration.",
     "Unchanged in meaning from BTS."),
    ("TR", "Operator",
     "The action a Step performs — SET, PAU, CHA, DCH, INT, STO, BEG, CYC, GOTO, REG, "
     "ERR, MSG, TABLE.",
     "Phase 1 operator set is fixed on sheet 3 section 10.3."),
    ("TR", "Registration",
     "In this document, ALWAYS means: recording measured parameters to a test record. "
     "It never means device announcement.",
     "The old sources used one word for three things. Device announcement is called "
     "'Secondary Enrolment' here. See Open Issue OI-19."),
    ("TR", "Secondary Enrolment",
     "The process by which the Primary discovers a Secondary Board on the CAN bus, "
     "confirms its identity and capability, and admits it to service.",
     "New concept — BTS had nothing to discover."),
    ("TR", "MTO", "Message Time Out — the age at which a subscribed CAN message is "
                   "declared stale.", "Carried over from BTS."),
    ("TR", "<TBD-nn>",
     "A number or rule that no source document provided.",
     "Every one is listed on sheet 5 with the person who must answer it."),
]

# ============================================ 3. REQUIREMENTS (from me_srs_reqs)
import json  # noqa: E402
import os  # noqa: E402
import re  # noqa: E402

import me_srs_reqs  # noqa: E402

REQUIREMENTS = me_srs_reqs.REQUIREMENTS
_HERE = os.path.dirname(os.path.abspath(__file__))

# ============================================================ 4. TRACEABILITY
# Every BTS row not claimed by any ME requirement must appear here with a reason.
# Anything missing from both is reported as an error by the builder, so a row can
# never be lost by being forgotten.
_DROP = {
    # --- 3.0 Relays: relay drive is Secondary Board scope -------------------
    "SW_REQ_18": "Relay drive for charge/discharge change-over is internal Secondary "
                 "Board behaviour. The Primary-side consequence is kept as ME_SW_REQ_23.",
    # --- 8.1 Voltage measurement hardware ----------------------------------
    "SW_REQ_115": "External 24-bit ADC selection and use is Secondary Board scope.",
    "SW_REQ_116": "100 V DC measurement range is a Secondary Board hardware property.",
    "SW_REQ_117": "Bipolar ADC configuration is Secondary Board scope. The Primary-side "
                  "consequence is kept as ME_SW_REQ_141.",
    "SW_REQ_118": "-2.5 V to +2.5 V bipolar input range is a Secondary Board hardware "
                  "property.",
    "SW_REQ_129": "Pre-test voltage measurement is performed by the Secondary. The "
                  "Primary-side consequence is kept as ME_SW_REQ_141 and ME_SW_REQ_143.",
    # --- 8.2 Current measurement hardware ----------------------------------
    "SW_REQ_138": "External 24-bit ADC for current is Secondary Board scope.",
    "SW_REQ_139": "The 75 mV shunt signal is a Secondary Board hardware property.",
    "SW_REQ_140": "Single-transistor-bank shunt polarity is a Secondary Board hardware "
                  "property.",
    "SW_REQ_141": "Dual-transistor-bank shunt polarity is a Secondary Board hardware "
                  "property.",
    "SW_REQ_142": "Bipolar ADC configuration for reverse current is Secondary Board scope.",
    "SW_REQ_158": "Charging auto-ranging gain of 2 is applied in Secondary Board hardware.",
    "SW_REQ_159": "Charging auto-ranging gain of 4 is applied in Secondary Board hardware.",
    "SW_REQ_160": "Charging auto-ranging gain of 8 is applied in Secondary Board hardware.",
    "SW_REQ_161": "Discharging auto-ranging gain of 2 is applied in Secondary Board "
                  "hardware.",
    "SW_REQ_162": "Discharging auto-ranging gain of 4 is applied in Secondary Board "
                  "hardware.",
    "SW_REQ_163": "Discharging auto-ranging gain of 8 is applied in Secondary Board "
                  "hardware.",
    # --- 9 Analog output ----------------------------------------------------
    "SW_REQ_166": "Analog output generation is Secondary Board scope. The Primary "
                  "commands engineering values only - see ME_SW_REQ_166.",
    "SW_REQ_167": "18-bit analog output resolution is a Secondary Board hardware "
                  "property.",
    "SW_REQ_169": "Single-transistor-bank construction is Secondary Board information.",
    "SW_REQ_170": "Charging analog output range is Secondary Board scope.",
    "SW_REQ_171": "Discharging analog output range is Secondary Board scope.",
    "SW_REQ_172": "Contactor drive from the Secondary card is Secondary Board scope.",
    "SW_REQ_173": "Relay state during charging is Secondary Board scope.",
    "SW_REQ_174": "Relay state during discharging is Secondary Board scope.",
    "SW_REQ_175": "An unanswered question about which relay output to use - a Secondary "
                  "Board design decision. Recorded as Open Issue OI-06.",
    "SW_REQ_180": "Dual-transistor-bank construction is Secondary Board information.",
    "SW_REQ_181": "Charging analog output range is Secondary Board scope.",
    "SW_REQ_182": "Discharging analog output range is Secondary Board scope.",
    # --- open questions that became structured requirements or open issues ---
    "SW_REQ_65": "An unanswered question - 'How to indicate error and what shall be the "
                 "action?'. Answered generically by ME_SW_REQ_82; the code list is Open "
                 "Issue OI-11.",
    "SW_REQ_96": "An unanswered question - 'How exception shall be sent?'. Answered "
                 "generically by ME_SW_REQ_82.",
    "SW_REQ_122": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_127": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_132": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_135": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_146": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_151": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_156": "An unanswered question - 'How do we indicate this error?'. Answered "
                  "generically by ME_SW_REQ_82.",
    "SW_REQ_84": "A note reading 'Padding bytes not required.' Kept as information in "
                 "ME_SW_REQ_101.",
}
_DISP = {"Carried Over": "Carried Over", "Modified": "Modified",
         "New": "Superseded by New"}


def _build_trace():
    claims = {}
    for item in REQUIREMENTS:
        if item[0] != "R":
            continue
        rid, traces, change = item[1], item[8], item[9]
        for n in re.findall(r"SW_REQ_(\d+)", traces or ""):
            claims.setdefault("SW_REQ_" + n, []).append((rid, change))
    rows, missing = [], []
    for bts in json.load(open(os.path.join(_HERE, "bts_index.json"),
                              encoding="utf-8")):
        req = bts["req"]
        got = claims.get(req)
        if got:
            order = {"Carried Over": 0, "Modified": 1, "New": 2}
            change = sorted({c for _, c in got}, key=lambda c: order.get(c, 3))[0]
            disp = _DISP.get(change, change)
            note = ("Requirement preserved. Wording tidied only."
                    if disp == "Carried Over" else
                    "Intent changed by the new topology - read the ME statement, do not "
                    "assume the BTS behaviour still holds."
                    if disp == "Modified" else
                    "Absorbed into a new ME requirement that did not exist in BTS.")
            rows.append((req, bts["app"] or "(blank)", bts["status"] or "(blank)", disp,
                         ", ".join(r for r, _ in got), note))
        elif req in _DROP:
            reason = _DROP[req]
            disp = ("Dropped - Secondary scope"
                    if "Secondary Board" in reason else "Moved to Open Issues")
            rows.append((req, bts["app"] or "(blank)", bts["status"] or "(blank)", disp,
                         "-", reason))
        else:
            missing.append(req)
            rows.append((req, bts["app"] or "(blank)", bts["status"] or "(blank)",
                         "!! UNACCOUNTED FOR", "-",
                         "This BTS row is neither traced by an ME requirement nor listed "
                         "as deliberately dropped. Fix me_srs_data.py."))
    return rows, missing


TRACE, TRACE_MISSING = _build_trace()

# ==================================================== 5. OPEN ISSUES AND TBD
_TBD = [
    ("TBD-01", "Digital I/O", "Required time-stamp resolution for a digital input "
     "transition.", "ME_SW_REQ_5", "SW architect"),
    ("TBD-02", "Digital I/O", "Time within which Primary digital outputs must reach their "
     "safe state on link loss or fault.", "ME_SW_REQ_21", "Client / Safety"),
    ("TBD-03", "Safety", "Switch-over dead time between charging and discharging on one "
     "channel. BTS used 500 ms on the Secondary; whether the Primary must add margin for "
     "bus latency is unknown.", "ME_SW_REQ_23", "HW + SW architect"),
    ("TBD-04", "Timebase", "Maximum permitted time offset between the Primary and any "
     "Secondary Board.", "ME_SW_REQ_30", "SW architect"),
    ("TBD-05", "Modbus", "MODBUS register map layout for up to 64 channels on RS485_1.",
     "ME_SW_REQ_36", "Client / SW architect"),
    ("TBD-06", "Modbus", "Permitted data-bit, stop-bit and parity combinations on RS485_2. "
     "BTS wrote 'as per configuration XXX'.", "ME_SW_REQ_41", "Client"),
    ("TBD-07", "Modbus", "MODBUS function codes to be supported on RS485_2. BTS wrote "
     "'function codes XXX'.", "ME_SW_REQ_43", "Client"),
    ("TBD-08", "Modbus", "MODBUS RTU response timeout on RS485_2. BTS wrote 'XXX ms'.",
     "ME_SW_REQ_44", "Client"),
    ("TBD-09", "Modbus", "Maximum number of devices on the RS485_2 bus. BTS wrote 'up to "
     "XXX devices'.", "ME_SW_REQ_45", "Client"),
    ("TBD-10", "Modbus", "Lowest configurable RS485_2 slave address. BTS wrote 'XXX'.",
     "ME_SW_REQ_46", "Client"),
    ("TBD-11", "Modbus", "Highest configurable RS485_2 slave address. BTS wrote 'XXX'.",
     "ME_SW_REQ_46", "Client"),
    ("TBD-12", "Host link", "Number of concurrent Battery Manager sessions permitted.",
     "ME_SW_REQ_59", "Client / SW architect"),
    ("TBD-13", "Host link", "Minimum test record buffering time while the Battery Manager "
     "link is down.", "ME_SW_REQ_61", "Client"),
    ("TBD-14", "CAN", "Which CAN port is reserved for the internal Secondary bus.",
     "ME_SW_REQ_65", "HW + SW architect"),
    ("TBD-15", "Safety", "Voltage threshold that counts as 'near 0 V' for Battery Open "
     "detection. BTS wrote only 'near 0V'.", "ME_SW_REQ_143", "Client"),
    ("TBD-16", "Calibration", "Battery voltage calibration procedure, number of points and "
     "acceptance criteria. BTS section 8.1.5 was empty.", "ME_SW_REQ_145", "Client"),
    ("TBD-17", "Capacity", "Maximum Program size per channel, and therefore total Program "
     "storage for 64 channels.", "ME_SW_REQ_237", "SW architect"),
    ("TBD-18", "Addressing", "Agreed valid range of the Channel Address. Two ME documents "
     "disagree - the nibble encoding admits 15x15, storage and validation cap at 8x8.",
     "ME_SW_REQ_247", "SW architect"),
    ("TBD-19", "Supervision", "Time after which a silent Secondary Board is declared lost.",
     "ME_SW_REQ_255", "SW architect"),
    ("TBD-20", "CAN", "Internal CAN bus arbitration bit rate.", "ME_SW_REQ_263",
     "HW + SW architect"),
    ("TBD-21", "CAN", "Internal CAN bus data bit rate.", "ME_SW_REQ_263",
     "HW + SW architect"),
    ("TBD-22", "CAN", "Maximum permitted worst-case internal CAN bus load, as a percentage.",
     "ME_SW_REQ_264", "SW architect"),
    ("TBD-23", "Performance", "Worst-case time from a Primary setpoint decision to its "
     "delivery at the Secondary.", "ME_SW_REQ_266", "SW architect"),
    ("TBD-24", "Safety", "Time after which a channel's measurement data is declared stale.",
     "ME_SW_REQ_267", "SW architect"),
    ("TBD-25", "Host link", "Byte order of the CRC and of multi-byte fields on the host "
     "link. Two ME documents disagree.", "ME_SW_REQ_277", "SW architect + Web App team"),
    ("TBD-26", "Data", "Permitted registration types and the permitted registration "
     "interval range.", "ME_SW_REQ_286", "Client"),
    ("TBD-27", "Control", "Behaviour of a Start command sent to a channel that has already "
     "completed its test.", "ME_SW_REQ_295", "Client"),
    ("TBD-28", "Performance", "Control decision interval per channel. This is the single "
     "most architecturally significant unknown in this document.", "ME_SW_REQ_305",
     "Client + SW architect"),
    ("TBD-29", "Performance", "Permitted jitter on the control decision interval.",
     "ME_SW_REQ_306", "SW architect"),
    ("TBD-30", "Performance", "Maximum aggregate live data rate for all channels.",
     "ME_SW_REQ_307", "SW architect"),
    ("TBD-31", "Performance", "Minimum processor headroom required under worst case.",
     "ME_SW_REQ_308", "SW architect"),
    ("TBD-32", "Performance", "Memory budget available to the Primary software on the "
     "target platform.", "ME_SW_REQ_309", "SW architect"),
    ("TBD-33", "Startup", "Time from power-on to ready-to-accept-commands.",
     "ME_SW_REQ_319", "Client"),
    ("TBD-34", "Diagnostics", "Minimum log history to be retained.", "ME_SW_REQ_321",
     "Client"),
    ("TBD-35", "Power Fail", "Minimum guaranteed warning time between power-fail detection "
     "and supply collapse, within which the Primary must complete its snapshot save for "
     "every populated channel. Depends on the Primary Board's power-supervisory circuit.",
     "ME_SW_REQ_341", "HW architect"),
    ("TBD-36", "Programming", "Maximum number of cut-off conditions configurable on a "
     "single Step. Was a fixed compile-time constant in the BTS Secondary firmware.",
     "ME_SW_REQ_347", "SW architect"),
    ("TBD-37", "Programming", "Number of consecutive evaluations a cut-off condition must "
     "remain true before it is treated as met.", "ME_SW_REQ_349", "SW architect"),
]

_OI = [
    ("OI-01", "Scope", "Is the host application a PC/desktop application or a browser-based "
     "web application?",
     "This document was written for a PC application, as instructed. Every ME design "
     "document instead calls it 'the Web Application', and the name 'Battery Manager' "
     "appears in none of them. If it is in fact a web application, the deployment, "
     "session and access-control requirements of section 22 change.",
     "Section 22, ME_SW_REQ_271", "Client", "High"),
    ("OI-02", "Scope", "Is 'Battery Manager' the agreed product name for the host "
     "application in ME?",
     "The BTS reference calls it Battery Manager. The ME design documents call it the Web "
     "Application. Two names for one thing across a project's documents reliably produces "
     "requirements that are read as applying to different systems.",
     "Whole document", "Client / Project lead", "High"),
    ("OI-03", "Scope", "Is the ME Primary a single-core Linux application processor, or "
     "does it have a separate real-time core?",
     "The ME design describes a Cortex-A53 running containerised Linux and explicitly not "
     "a bare-metal or RTOS target. No document mentions a second real-time core. If the "
     "control loop of 64 channels must run under Linux, ME_SW_REQ_305 and ME_SW_REQ_306 "
     "may not be achievable without one.",
     "ME_SW_REQ_305, ME_SW_REQ_306, ME_SW_REQ_311", "SW architect", "High"),
    ("OI-04", "Requirements", "BTS V1.7 uses the requirement number SW_REQ_19 twice - once "
     "in section 3.0 Relays and once in section 4.0 RTC.",
     "Any traceability built on the BTS numbers is ambiguous at that number. Both rows are "
     "dispositioned separately in sheet 4, but the source should be corrected.",
     "Sheet 4", "Requirements owner", "Low"),
    ("OI-05", "Safety", "Is there an emergency stop, and what is the defined safe state of "
     "a channel mid-charge and mid-discharge?",
     "A large number of safety requirements in sections 17, 18 and 20 depend on the answer. "
     "No source document defines 'safe'.",
     "ME_SW_REQ_21, ME_SW_REQ_294, ME_SW_REQ_296, ME_SW_REQ_300", "Client / Safety",
     "High"),
    ("OI-06", "Safety", "Does the Primary sequence the charge/discharge change-over, or "
     "does it issue a mode command and rely on the Secondary to sequence it safely?",
     "Decides where the 500 ms dead time is enforced, and therefore whether a bus delay can "
     "break it. Also decides which relay output is used, which BTS SW_REQ_175 left open.",
     "ME_SW_REQ_23", "HW + SW architect", "High"),
    ("OI-07", "Modbus", "How are 64 channels exposed through a MODBUS register map?",
     "64 channels times the parameter list of ME_SW_REQ_35 does not fit a flat register "
     "map. Either the map is paged, or the operator selects channels, or only a subset is "
     "published. The HMI integrator needs this fixed early.",
     "ME_SW_REQ_36, ME_SW_REQ_37", "Client / SW architect", "Medium"),
    ("OI-08", "Host link", "May more than one operator be connected to one Primary at the "
     "same time?",
     "If yes, two operators can issue conflicting commands to the same channel and the "
     "arbitration rule must be specified. If no, a stale session must be detectable and "
     "recoverable or the machine becomes unusable after a PC crash.",
     "ME_SW_REQ_59", "Client", "Medium"),
    ("OI-09", "CAN", "Which CAN port carries the internal bus, and is it physically "
     "distinct from the user-configurable ports?",
     "If a user-configurable port shares a controller or a transceiver with the internal "
     "bus, ME_SW_REQ_270 cannot be honoured.", "ME_SW_REQ_65, ME_SW_REQ_270",
     "HW architect", "High"),
    ("OI-10", "CAN", "Do user-configurable CAN ports use 11-bit identifiers, 29-bit "
     "identifiers, or both?",
     "BTS V1.7 contradicted itself: SW_REQ_50 required both, SW_REQ_74 required 11-bit, and "
     "SW_REQ_78 deferred 29-bit to a later phase. Both readings are carried in this "
     "document rather than one being quietly dropped.",
     "ME_SW_REQ_67, ME_SW_REQ_91, ME_SW_REQ_95", "Client / SW architect", "Medium"),
    ("OI-11", "Diagnostics", "What is the catalogue of error codes, and how is each "
     "reported?",
     "BTS V1.7 asked 'How do we indicate this error?' in nine separate places and never "
     "answered. Nine requirements in this document depend on one answer.",
     "ME_SW_REQ_82, ME_SW_REQ_320", "SW architect", "High"),
    ("OI-12", "Capacity", "What sampling and registration rates are simultaneously "
     "achievable across 64 channels?",
     "BTS permitted 1 ms sampling for one channel. 64 channels at 1 ms is 64000 samples per "
     "second across a shared bus. Until the bus load calculation is done, the configuration "
     "limits in section 8 cannot be trusted and the Battery Manager cannot validate an "
     "operator's configuration.",
     "ME_SW_REQ_140, ME_SW_REQ_264, ME_SW_REQ_307", "SW architect", "High"),
    ("OI-13", "Calibration", "What is the battery voltage calibration procedure?",
     "BTS V1.7 section 8.1.5 contained two requirement numbers with completely empty "
     "statements. There is nothing to carry forward.", "ME_SW_REQ_145", "Client", "Medium"),
    ("OI-14", "Calibration", "How is calibration data bound to a Secondary Board so a board "
     "swap cannot apply the wrong constants?",
     "A field-service hazard that the single-Secondary BTS could not have. Wrong constants "
     "produce measurements that look plausible.", "ME_SW_REQ_162", "SW architect", "High"),
    ("OI-15", "Persistence", "What is persisted to non-volatile storage, in what format, "
     "and how is a partial write handled?",
     "The current ME design holds programs, configuration and calibration in volatile "
     "memory and records power-fail persistence as not yet designed. For 64 channels this "
     "is a large amount of operator work to lose on a power blip. Section 23.0 now "
     "specifies what must be saved and when; the medium, on-media format and "
     "partial-write handling are still open.",
     "ME_SW_REQ_163, ME_SW_REQ_313, ME_SW_REQ_325, ME_SW_REQ_342", "SW architect", "High"),
    ("OI-16", "Programming", "What does the REG operator do, and what are the permitted "
     "Registration values?",
     "BTS V1.7 marked its operator list 'Done (Except REG)', so REG is the one operator with "
     "no working precedent, and the Registration field's permitted values are stated "
     "nowhere.", "ME_SW_REQ_174, ME_SW_REQ_286", "Client", "High"),
    ("OI-17", "Programming", "What does the SET operator do?",
     "BTS V1.7 said only 'BTS shall support SET Operator', with no applicability and no "
     "description. It cannot be implemented or tested from that.", "ME_SW_REQ_177",
     "Client", "High"),
    ("OI-18", "Programming", "Should the misspelled error identifiers of BTS V1.7 be "
     "corrected in ME?",
     "OPERATOR_CHA_MANDETORY_PARAMETERS_MISSING_ERROR is carried verbatim so existing test "
     "material and log parsers still match. Correcting it is cheap now and expensive later.",
     "ME_SW_REQ_193", "Requirements owner", "Low"),
    ("OI-19", "Terminology", "The word 'registration' is used for three different things "
     "across the source documents.",
     "It means a recorded measurement, a device announcing itself, and a set of safety "
     "limits. All three readings make sense in the same sentence, which is how a "
     "misimplementation happens. This document uses 'Registration' only for recorded "
     "measurements and 'Secondary Enrolment' for device announcement.",
     "Sheet 2, ME_SW_REQ_275", "Requirements owner", "Medium"),
    ("OI-20", "Programming", "What exactly do the DCH, INT, ERR and MSG operators and "
     "actions do?",
     "BTS V1.7 covered each in a single line saying only that it is supported. DCH in "
     "particular is as complex as CHA but got one line to CHA's fifteen.",
     "ME_SW_REQ_199, ME_SW_REQ_200, ME_SW_REQ_230, ME_SW_REQ_231", "Client", "High"),
    ("OI-21", "Programming", "When an STO operator carries surplus parameters, is an error "
     "raised, or are they ignored, or both?",
     "BTS SW_REQ_230 and SW_REQ_231 specified opposite behaviours for the same case. This "
     "document merged them into raise-and-proceed, which satisfies both readings but was "
     "not stated by either.", "ME_SW_REQ_206", "Client", "Medium"),
    ("OI-22", "Programming", "Is 16-way cycle nesting required for Phase 1?",
     "BTS V1.7 marked it Pending and never built it, and the ME design records that the old "
     "cycle-table implementation has not been ported. It is unbuilt in both systems, so "
     "committing to it in Phase 1 is a real cost.", "ME_SW_REQ_216, ME_SW_REQ_217",
     "Client / Project lead", "Medium"),
    ("OI-23", "Capacity", "What is the largest Program that must be supported per channel?",
     "Decides the total Program storage for 64 channels, which the ME design already flags "
     "as needing verification against the platform's memory limit.",
     "ME_SW_REQ_237, ME_SW_REQ_309", "Client / SW architect", "Medium"),
    ("OI-24", "Addressing", "Is the Channel Address range 8x8 or 15x15?",
     "Two ME design documents disagree. The encoding admits 15x15; storage and validation "
     "cap at 8x8. If the product may ever exceed 8 boards, this must be settled before any "
     "wire format is frozen.", "ME_SW_REQ_247", "SW architect", "Medium"),
    ("OI-25", "Supervision", "Is hot-swapping a Secondary Board a supported field "
     "operation?",
     "Affects whether re-enrolment must revalidate identity and calibration, and whether "
     "the other 56 channels must keep running through it.",
     "ME_SW_REQ_257, ME_SW_REQ_258", "Client", "Medium"),
    ("OI-26", "Supervision", "How and when are channels beyond the first enrolled?",
     "The current ME implementation registers exactly one circuit and is, in its own words, "
     "functionally single-circuit until the bus-side enrolment handshake exists. This is "
     "the largest gap between the design as built and this specification.",
     "ME_SW_REQ_251, ME_SW_REQ_260", "SW architect", "High"),
    ("OI-27", "CAN", "Is the internal bus Classic CAN or CAN FD, and at what bit rates?",
     "One line of one ME design document names a CAN FD frame; nothing else qualifies it, "
     "and no bit rate appears anywhere. This is the first question to answer, because it "
     "sets the ceiling on the sampling and registration rates of all 64 channels.",
     "ME_SW_REQ_262, ME_SW_REQ_263, ME_SW_REQ_264", "HW + SW architect", "High"),
    ("OI-28", "Host link", "Is the host link CRC big-endian or little-endian on the wire?",
     "One ME document records big-endian as hardware-verified in both directions; another "
     "records a captured server response whose CRC was little-endian. An unresolved byte "
     "order is an interoperability failure waiting to happen in the field.",
     "ME_SW_REQ_277", "SW architect + Web App team", "High"),
    ("OI-29", "Control", "Does every host command carry a meaningful per-channel address, "
     "or are some commands device-scoped?",
     "The ME design notes that device-scoped commands such as Sync Time and Reset are "
     "currently gated as though they were channel-scoped, and that this is safe only by "
     "accident.", "ME_SW_REQ_291", "SW architect + Web App team", "Medium"),
    ("OI-30", "Control", "Should a Start command re-run a completed test, or be refused "
     "until the Program is re-sent?",
     "The current ME design re-runs it, and its author flagged the choice as open. With 64 "
     "channels an accidental restart of a completed multi-day test is a costly mistake.",
     "ME_SW_REQ_295", "Client", "Medium"),
    ("OI-31", "Performance", "How often must the Primary re-evaluate the control decision "
     "of each channel?",
     "The single most architecturally significant unknown in this document. It decides "
     "whether a Linux application processor can host the control loop of 64 channels at all, "
     "and therefore whether the whole software architecture is viable.",
     "ME_SW_REQ_305", "Client + SW architect", "High"),
    ("OI-32", "Performance", "What is the memory budget on the target platform?",
     "The current ME design reserves a large fixed block for 64 channels' program buffers "
     "and records verifying it against the platform limit as an open item.",
     "ME_SW_REQ_309", "SW architect", "Medium"),
    ("OI-33", "Security", "Is user authentication and action logging required?",
     "No source document mentions access control. A 64-channel machine is a shared "
     "laboratory resource where one person's mis-click destroys another person's week.",
     "ME_SW_REQ_340", "Client", "Medium"),
    ("OI-34", "Safety", "Is fully-automatic resume without operator confirmation after a "
     "power fail acceptable from a safety standpoint, given a battery's state may have "
     "drifted during the outage?",
     "ME_SW_REQ_314 deliberately forbids silent resume after an unexpected restart, "
     "because resuming a partly completed charge on a battery whose state is no longer "
     "known is a hazard. The client has directed that a power fail with a valid saved "
     "snapshot (ME_SW_REQ_344) should resume automatically regardless, and has flagged "
     "that this decision may change after discussing the safety trade-off with the "
     "client's own stakeholders.",
     "ME_SW_REQ_314, ME_SW_REQ_344, ME_SW_REQ_345", "Client", "High"),
]

ISSUES = (
    [(i, "TBD", area, q,
      "A requirement in this document cannot be implemented or tested until this number or "
      "rule is supplied.", affects, owner, "High" if i in (
          "TBD-22", "TBD-28", "TBD-25", "TBD-20", "TBD-21") else "Medium", "Open")
     for i, area, q, affects, owner in _TBD]
    + [(i, "Open Issue", area, q, why, affects, owner, prio, "Open")
       for i, area, q, why, affects, owner, prio in _OI]
)

# =================================================== 6. REFERENCES AND LEGEND
REFERENCES = [
    ("H", "1.  Source documents used to write this SRS", None, None),
    ("TH", "Document", "What it gave us", "How it was treated"),
    ("TR", "BTS_Primary_SW_Requirement_Analysis V1.7.xlsx",
     "281 numbered requirements for the old BTS product, each with an Applicability "
     "column. Located in ME Workspace\\Reference Documents.",
     "The primary source of requirement CONTENT. Its Applicability column decided what "
     "belongs in this document. Every one of its rows is dispositioned on sheet 4."),
    ("TR", "ME_Primary_BTS_Block_Diagram.md",
     "The ME Primary software structure - channel counts, the Channel Address scheme, the "
     "live data field list, what is deliberately not built yet.",
     "Evidence of the CURRENT ME design. Used to ground new requirements in reality and to "
     "find gaps, never treated as a requirement in itself."),
    ("TR", "ME_Primary_Comm_Block_Diagram.md",
     "The host link - board as TCP client on port 9999, UDP 10000 and 10001, the "
     "enrolment handshake, the connection state machine.",
     "Evidence of the current ME design. Section 15 is grounded in it."),
    ("TR", "ME_Primary_Implementation_Reference.md",
     "File-by-file implementation detail, the frame command groups, the CRC definition, "
     "and 19 recorded open items.",
     "Evidence of the current ME design, and the source of several entries on sheet 5."),
    ("TR", "specs\\ and plans\\ under ME Project\\Docs",
     "The enrolment and idle-state design, and the thread architecture plan.",
     "Evidence of the current ME design."),
    ("TR", "Old BTS Documents\\*.xlsx frame formats",
     "Config, Control, Program, Calibration, Measured Parameter and Device Registration "
     "frame formats.",
     "Not used directly. Frame layouts belong in the Interface Control Document, not in "
     "this SRS."),
    ("GAP", None, None, None),
    ("H", "2.  Order of authority when sources disagreed", None, None),
    ("TR", "1. Client instruction",
     "The topology stated by the project owner - one Primary, up to 8 Secondaries, 8 "
     "channels each, PC-based Battery Manager.", "Overrides everything below."),
    ("TR", "2. BTS V1.7 Applicability column",
     "Decides whether a requirement belongs to BM, the Primary, or the Secondary.",
     "Used as the scope filter for this whole document."),
    ("TR", "3. Current ME design documents",
     "Concrete numbers, port assignments and addressing already decided and partly built.",
     "Used where BTS was silent. Never used to overrule a BTS requirement."),
    ("TR", "4. BTS V1.7 requirement statements",
     "The behaviour of the old product.",
     "Carried forward unless the new topology makes it wrong, in which case the row is "
     "marked Modified and the reason is given."),
    ("TR", "Unresolvable disagreements",
     "Where two sources of equal authority disagreed, no winner was picked quietly.",
     "Both readings are carried and the disagreement is registered on sheet 5. There are "
     "several - OI-10, OI-21, OI-24 and OI-28 are the ones that block work."),
    ("GAP", None, None, None),
    ("H", "3.  Column legend for sheet 3", None, None),
    ("TH", "Column", "Meaning", "Allowed values"),
    ("TR", "Requirement ID", "Stable identity. Never renumber; never reuse.",
     "ME_SW_REQ_n"),
    ("TR", "Type", "Whether the row binds anybody.",
     "Requirement (binding, uses 'shall') / Information (context) / Description "
     "(mechanism) / Constraint (imposed limit)"),
    ("TR", "Category", "What kind of concern this is. Use it to route a review.",
     "Functional / Interface / Performance / Safety / Configuration / Data / Diagnostic / "
     "Usability / Capacity"),
    ("TR", "Applicability", "Which team owns it. Filter this column to get your work list.",
     "BM / Primary / BM + Primary"),
    ("TR", "Phase", "Whether it is committed for Phase 1.", "Phase 1 / Phase 2"),
    ("TR", "Cardinality",
     "How many independent instances of this exist. The most important column in a "
     "64-channel system - read it before designing any data structure.",
     "Per System / Per Secondary / Per Channel / Per CAN Port / Per Program / N/A"),
    ("TR", "Traces To (BTS)", "The BTS V1.7 requirement this came from.",
     "SW_REQ_n, or blank when the requirement is new to ME"),
    ("TR", "Change", "How much it changed.",
     "Carried Over (same intent) / Modified (intent changed - do not assume the old "
     "implementation applies) / New (no BTS ancestor)"),
    ("TR", "Verification", "How it will be proved.",
     "Test / Analysis / Inspection / Demonstration / HIL Test (needs real hardware and a "
     "real battery - book bench time)"),
    ("TR", "Status", "Maturity of the requirement itself, not of the implementation.",
     "Draft / Agreed / Pending / Need to check / Deferred"),
]
