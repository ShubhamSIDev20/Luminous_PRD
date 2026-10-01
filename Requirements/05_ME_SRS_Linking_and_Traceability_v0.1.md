# ME — SRS Linking and Traceability Document

**Document ID:** ME-LINK-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Purpose:** one navigable map of the ME requirement set, and the requirement-level
links between its documents.

> **How this document is produced.** §1–§3 are written by hand and are stable. **§4–§7 are
> generated** from the citations already present in the other documents, so they cannot
> drift out of step with them. Regenerate after any change to any document in the set.
>
> **§4 joins through the ICD message identifier rather than pairing requirements
> directly.** That is deliberate: the ICD is the only place where both sides of an
> interface are named together, so the message identifier is the one join key that
> already exists in the documents. Pairing requirements directly would mean inventing
> links, and invented links cannot be verified.

---

## 1. Which document answers which question

The fastest way into the set. Find the question, read only that document.

| If you need to know… | Look in | Not in |
|---|---|---|
| What the whole system must do, area by area | `GATE` §1–§3 (coverage outline) | any single SRS |
| What the Primary Board must do | `PRI` §4 | — |
| What one Secondary Board must do | `SEC` §4 | `PRI` |
| What the Web Application must do | `WEB` §4 | `WAD/SRS` (that is the old product) |
| What crosses a wire, and in which direction | `ICD` §4, §5, §6 | any SRS |
| How a byte is laid out on the wire | **nowhere yet** — reserved decisions **D-04** / **D-05**; worksheet at `ICD` §9.4 | `ICD` §4/§5 (columns present but empty by design) |
| The CAN bit rate, and whether CAN-FD is needed | `ICD` §7.3 (the arithmetic) then **D-04** | `SEC` §3.3 gives only the 250 kbps default |
| Which BTS-600 operators exist and what each does | `PRI` §4.7.2–§4.7.7 | `SEC`, `WEB` |
| Which regulation mode a step uses | `PRI` §4.7.8 — inferred from the nominal value, per BM4 | — |
| How a charge or discharge loop is actually regulated | `SEC` §4.5, §4.6 | `PRI` |
| What happens when a Secondary stops answering | `SEC` §4.10 and `ICD` §8.2 | `PRI` §4.14 covers the Primary's half |
| What happens when the Web Application disappears | `ICD` §8.1 and `WEB` §5.3 | — |
| Whether recorded test data can be silently lost | `ICD` §8.3 — **yes, today it can** | — |
| How many boards one Web Application instance can serve | `WEB` §5.1 — it depends on **D-04** | — |
| Why a value is missing | the TBD register of the document that needs it | — |
| Who has to decide something | §5 of this document | — |
| Whether a requirement can be tested yet | the annex verification sheet: `SECA` §A2, `WEBA` §A2 | — |
| Whether anything from the old Web App SRS was dropped | `WEBA` §A3 — baseline coverage map | — |

## 2. The document set

| # | Document | Tag | What it is | Read it if you are |
|---|---|---|---|---|
| 00 | `00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` (697 lines) | `GATE` | Pre-write gate: which requirements had a real source and which did not, plus the shared open-issue and conflict registers | deciding whether the set is complete enough to build from |
| 01 | `01_SRS_ME_Primary_Board_v0.1.md` (1374 lines) | `PRI` | Primary Board SRS — the system controller: programs, step sequencing, 8 channels | implementing or reviewing the Primary Board |
| 01A | `01A_SRS_ME_Primary_Annex_Traceability_and_Core_Allocation_v0.1.md` (923 lines) | `PRIA` | Primary annex: traceability plus the **core-allocation sheet** for decision D-01 | the architect resolving D-01 |
| 02 | `02_SRS_ME_Secondary_Board_v0.1.md` (1435 lines) | `SEC` | Secondary Board SRS — one regulated charge/discharge channel with local protection | implementing or reviewing a Secondary Board |
| 02A | `02A_SRS_ME_Secondary_Annex_Traceability_v0.1.md` (553 lines) | `SECA` | Secondary annex: traceability plus the **hardware-in-the-loop verification sheet** | planning bench and HIL testing |
| 03 | `03_ICD_ME_Interfaces_v0.1.md` (1009 lines) | `ICD` | Interface Control Document — every message on every interface, with semantics but no wire format | building either side of an interface, or answering D-04/D-05 |
| 04 | `04_SRS_ME_Web_Application_v0.1.md` (1242 lines) | `WEB` | Web Application SRS — the operator-facing host application | implementing or reviewing the Web Application |
| 04A | `04A_SRS_ME_Web_Application_Annex_Traceability_v0.1.md` (487 lines) | `WEBA` | Web App annex: traceability, verification sheet, and the **baseline coverage map** | checking nothing from the existing product was lost |
| 05 | `05_ME_SRS_Linking_and_Traceability_v0.1.md` | `LINK` | This document — the map and the requirement-level links | new to the set, or running a review |

### 2.1 How they fit together

```
                        GATE (00)  — what had a source, what did not
                             |  open issues · conflicts · assumptions
        +--------------------+--------------------+
        |                    |                    |
   PRI (01) <----- IF-A ---- WEB (04)        SEC (02)
     |  \                      |               ^
     |   \                     |               |
     |    +------ IF-B ---------------------+
     |                         |
     +-- IF-D --> Modbus client|
                               |
        ICD (03) specifies every arrow above:
           IF-A  Web App  <-> Primary        (90 messages)
           IF-B  Primary  <-> Secondary x8   (76 messages)
           IF-D  Primary  <-  Modbus client  (7 messages)

        01A / 02A / 04A are generated annexes of 01 / 02 / 04.
        05 (this document) links all of the above.
```

## 3. Reading order

**If you are the software architect** — you are the only reader who must read all of
it, and the set is arranged so you can answer the open decisions in one pass:
`GATE` §4–§5 (what is undecided) → `ICD` §9.4 (the D-04/D-05 worksheet) →
`PRIA` §A2 (the D-01 allocation sheet) → §5 of this document (everything still open,
consolidated) → `ICD` §7 (the arithmetic that narrows D-04).

**If you are implementing the Primary Board** — `PRI` §1–§2 for context, `PRI` §4 for
the work, `ICD` §4–§5 for both interfaces, `SEC` §1.2 to see where your
responsibility stops. Note `PRI` §4.7 is the largest single area in the set.

**If you are implementing a Secondary Board** — `SEC` end to end (it is deliberately
self-contained), then `ICD` §5 for IF-B, then `SECA` §A2 to see what you will have to
prove on hardware. Read `SEC` §1.2 and §5.2 of the ICD first: the Secondary no longer
runs the step engine, and that is the single biggest change from the old product.

**If you are implementing the Web Application** — `WEB` end to end, `ICD` §4 for IF-A,
`WEBA` §A3 to confirm what carried over from the existing product and what was
withdrawn. Read the two reading notes at the top of `WEB` first: the whole document is
conditional on Open Issue **#2**.

**If you are reviewing** — start here (§1), then each document's final sections:
open issues, conflicts, TBDs and deviations. Those four sections are where every
document states what it does *not* know.

---

## 4. Interface traceability

One row per message. `Primary` and `Secondary` are the requirements the **ICD itself**
names as realising that message. `Consumed by` lists requirements in any SRS that name
the message identifier. An empty cell is a real gap, not an omission here — §7 counts
them.

### 4.1 IF-A — Web Application ↔ Primary Board

**90 messages.**

| Msg | Name | Primary req. | Secondary req. | Consumed by |
|---|---|---|---|---|
| A-DSC-01 | discover-request | — | — | SRS-WEB-W1-001 |
| A-DSC-02 | discover-reply | — | — | SRS-WEB-W1-003 |
| A-DSC-03 | set-board-network-config | — | — | SRS-WEB-W1-006 |
| A-DSC-04 | set-server-config | — | — | SRS-WEB-W1-007 |
| A-DSC-05 | network-config-applied | — | — | SRS-WEB-W1-008 |
| A-REG-01 | register | — | — | SRS-WEB-W2-001, SRS-WEB-W2-003 |
| A-REG-02 | register-response | — | — | SRS-WEB-W2-006 |
| A-REG-03 | deregister | — | — | SRS-WEB-W2-013, SRS-WEB-W2-015 |
| A-REG-04 | keep-alive | — | — | SRS-WEB-W2-009 |
| A-CFG-01 | config-is-ready | — | — | **—** |
| A-CFG-02 | read-factory-config | — | — | **—** |
| A-CFG-03 | factory-config | — | — | SRS-WEB-W14-010 |
| A-CFG-04 | write-factory-config | — | SRS-SEC-NF-032 | SRS-SEC-NF-032, SRS-WEB-W14-010, SRS-WEB-W14-011 |
| A-CFG-05 | read-manufacturing-data | — | — | **—** |
| A-CFG-06 | manufacturing-data | — | — | SRS-WEB-W14-013 |
| A-CFG-07 | write-manufacturing-data | — | — | **—** |
| A-CFG-08 | read-battery-params | — | — | **—** |
| A-CFG-09 | battery-params | — | — | **—** |
| A-CFG-10 | write-battery-params | — | — | SRS-WEB-W5-007 |
| A-CFG-11 | sync-time | — | — | SRS-WEB-W14-016 |
| A-CFG-12 | read-config-item | — | — | SRS-WEB-W14-009 |
| A-CFG-13 | write-config-item | — | — | SRS-WEB-W14-009 |
| A-CFG-14 | restore-defaults | — | SRS-SEC-S15-016 | SRS-SEC-S15-016, SRS-SEC-S15-017, SRS-WEB-W14-015 |
| A-PRG-01 | program-is-ready | SRS-PRI-P4-002 | — | SRS-PRI-P4-002, SRS-WEB-W5-001 |
| A-PRG-02 | program-metadata | SRS-PRI-P4-003 | — | SRS-PRI-P4-003, SRS-WEB-W5-002 |
| A-PRG-03 | program-step-count | — | — | SRS-WEB-W5-002 |
| A-PRG-04 | program-data | — | — | SRS-WEB-W5-003 |
| A-PRG-05 | program-transfer-result | SRS-PRI-P4-008 | — | SRS-PRI-P4-007, SRS-PRI-P4-008, SRS-PRI-P4-016, SRS-WEB-W5-009, SRS-WEB-W5-010 |
| A-PRG-06 | read-program-metadata | SRS-PRI-P4-014 | — | SRS-PRI-P4-014, SRS-WEB-W5-013 |
| A-PRG-07 | read-saved-program | SRS-PRI-P4-015 | — | SRS-PRI-P4-015, SRS-WEB-W5-014 |
| A-PRG-08 | delete-program | SRS-PRI-P4-009 | — | SRS-PRI-P4-009, SRS-WEB-W5-015 |
| A-PRG-09 | registration-format-definition | SRS-PRI-P4-013 | — | SRS-PRI-P4-013, SRS-WEB-W5-008 |
| A-CTL-01 | start | — | — | SRS-WEB-W7-003 |
| A-CTL-02 | stop | — | — | SRS-WEB-W7-004 |
| A-CTL-03 | pause | — | — | SRS-WEB-W7-005 |
| A-CTL-04 | interrupt | — | — | SRS-WEB-W7-006 |
| A-CTL-05 | continue | — | — | SRS-WEB-W7-005 |
| A-CTL-06 | reset | — | — | SRS-WEB-W7-007 |
| A-CTL-07 | clear-fault | — | SRS-SEC-S13-011 | SRS-SEC-S13-011, SRS-SEC-S13-012, SRS-WEB-W7-008 |
| A-CTL-08 | set-digital-output | — | — | SRS-WEB-W7-009 |
| A-CTL-09 | control-result | — | — | SRS-WEB-W7-010 |
| A-TLM-01 | channel-realtime | — | — | SRS-WEB-W8-001, SRS-WEB-W8-003 |
| A-TLM-02 | board-realtime-rollup | SRS-PRI-P2-012, SRS-PRI-P3-011 | — | SRS-PRI-P2-012, SRS-PRI-P3-011, SRS-WEB-W8-006 |
| A-TLM-03 | telemetry-subscribe | — | — | SRS-WEB-W8-010 |
| A-TLM-04 | calibration-live | — | — | SRS-WEB-W12-004, SRS-WEB-W8-009 |
| A-LOG-01 | registration-batch | — | — | SRS-WEB-W9-001 |
| A-LOG-02 | registration-batch-ack | — | — | SRS-WEB-W9-010 |
| A-LOG-03 | request-buffered-data | — | — | SRS-WEB-CM-011, SRS-WEB-W9-012 |
| A-LOG-04 | buffer-status | — | SRS-SEC-S12-022 | SRS-SEC-S12-022 |
| A-LOG-05 | session-open | — | — | SRS-WEB-W9-014 |
| A-LOG-06 | session-close | — | SRS-SEC-S12-017 | SRS-SEC-S12-017, SRS-WEB-W9-015 |
| A-CAL-01 | calib-is-ready | — | — | SRS-WEB-W12-002 |
| A-CAL-02 | calib-live-data-start | — | — | SRS-WEB-W12-004 |
| A-CAL-03 | cha-current-low-point | — | — | SRS-WEB-W12-005, SRS-WEB-W12-006, SRS-WEB-W12-007 |
| A-CAL-04 | cha-current-high-point | — | — | **—** |
| A-CAL-05 | cha-current-commit | — | — | **—** |
| A-CAL-06 | dch-current-low-point | — | — | **—** |
| A-CAL-07 | dch-current-high-point | — | — | **—** |
| A-CAL-08 | dch-current-commit | — | — | **—** |
| A-CAL-09 | cha-voltage-low-point | — | — | **—** |
| A-CAL-10 | cha-voltage-high-point | — | — | **—** |
| A-CAL-11 | cha-voltage-commit | — | — | **—** |
| A-CAL-12 | dch-voltage-low-point | — | — | **—** |
| A-CAL-13 | dch-voltage-high-point | — | — | **—** |
| A-CAL-14 | dch-voltage-commit | — | — | **—** |
| A-CAL-15 | temp-low-point | — | — | **—** |
| A-CAL-16 | temp-high-point | — | — | **—** |
| A-CAL-17 | temp-commit | — | — | SRS-WEB-W12-005 |
| A-CAL-18 | calib-cancel | — | — | SRS-WEB-W12-008 |
| A-CAL-19 | calib-stop | — | — | SRS-WEB-W12-009 |
| A-CAL-20 | verify-cha-current-start | — | — | SRS-WEB-W12-010 |
| A-CAL-21 | verify-dch-current-start | — | — | SRS-WEB-W12-010 |
| A-CAL-22 | verify-current-stop | — | — | SRS-WEB-W12-010 |
| A-CAL-23 | read-calibration | — | — | SRS-WEB-W12-011 |
| A-EVT-01 | channel-fault | — | SRS-SEC-S13-003 | SRS-SEC-S13-003, SRS-SEC-S13-004 |
| A-EVT-02 | channel-message | — | — | **—** |
| A-EVT-03 | channel-user-error | — | — | **—** |
| A-EVT-04 | board-event | SRS-PRI-P3-009 | — | SRS-PRI-P3-009, SRS-WEB-W16-007 |
| A-EVT-05 | read-event-log | — | — | SRS-WEB-W16-008 |
| A-EVT-06 | message-catalogue | — | — | SRS-WEB-W14-007 |
| A-NOD-01 | read-node-inventory | SRS-PRI-P3-011 | — | SRS-PRI-P3-011, SRS-WEB-W3-001, SRS-WEB-W3-006 |
| A-NOD-02 | node-inventory | SRS-PRI-P3-004 | — | SRS-PRI-P3-004, SRS-PRI-P3-011, SRS-PRI-P3-012, SRS-WEB-UI-003, SRS-WEB-W18-008, SRS-WEB-W3-001, SRS-WEB-W3-002, SRS-WEB-W3-003, SRS-WEB-W3-004 |
| A-NOD-03 | node-presence-change | SRS-PRI-P3-009 | — | SRS-PRI-P3-009, SRS-WEB-W3-007, SRS-WEB-W3-010 |
| A-NOD-04 | node-identity-conflict | SRS-PRI-P3-005 | — | SRS-PRI-P3-005, SRS-WEB-W3-011 |
| A-NOD-05 | assign-program-to-channels | — | — | SRS-WEB-W7-001 |
| A-NOD-06 | read-channel-assignment | — | — | SRS-WEB-W7-002 |
| A-NOD-07 | firmware-image-transfer | — | SRS-SEC-S16-001 | SRS-SEC-S16-001, SRS-WEB-W18-001, SRS-WEB-W18-004 |
| A-NOD-08 | firmware-update-command | — | SRS-SEC-S16-003 | SRS-SEC-S16-003, SRS-WEB-W18-005, SRS-WEB-W18-006 |
| A-NOD-09 | firmware-update-progress | — | SRS-SEC-S16-013 | SRS-SEC-S16-013, SRS-WEB-W18-007 |
| A-NOD-10 | primary-firmware-transfer | — | — | SRS-WEB-W18-001 |

### 4.2 IF-B — Primary Board ↔ Secondary Board

**76 messages.**

| Msg | Name | Primary req. | Secondary req. | Consumed by |
|---|---|---|---|---|
| B-NOD-01 | enumerate-request | SRS-PRI-P1-013 | — | SRS-PRI-P1-013 |
| B-NOD-02 | enumerate-reply | — | SRS-SEC-S3-007 | SRS-SEC-S3-007 |
| B-NOD-03 | heartbeat | SRS-PRI-P3-007 | — | SRS-PRI-P3-007 |
| B-NOD-04 | node-poll | SRS-PRI-P3-008 | — | SRS-PRI-P3-008 |
| B-NOD-05 | assign-node-address | — | — | **—** |
| B-NOD-06 | time-sync | — | SRS-SEC-S4-018 | SRS-SEC-S4-018 |
| B-CFG-01 | config-is-ready | — | — | **—** |
| B-CFG-02 | read-factory-config | — | — | **—** |
| B-CFG-03 | factory-config | — | — | **—** |
| B-CFG-04 | write-factory-config | — | SRS-SEC-NF-032 | SRS-SEC-NF-032 |
| B-CFG-05 | write-battery-params | — | — | **—** |
| B-CFG-06 | write-manufacturing-data | — | — | **—** |
| B-CFG-07 | read-manufacturing-data | — | — | **—** |
| B-CFG-08 | read-config-item | — | — | **—** |
| B-CFG-09 | write-config-item | — | — | **—** |
| B-CFG-10 | restore-defaults | — | SRS-SEC-S15-016 | SRS-SEC-S15-016, SRS-SEC-S15-017 |
| B-CFG-11 | config-state | — | SRS-SEC-S15-013 | SRS-SEC-S15-013, SRS-SEC-S15-018, SRS-WEB-W14-014 |
| B-DAT-01 | control-data-is-ready | — | SRS-SEC-S4-010 | SRS-SEC-S4-010 |
| B-DAT-02 | control-data | — | SRS-SEC-S4-001 | SRS-SEC-S4-001, SRS-SEC-S4-002 |
| B-DAT-03 | control-data-ack | — | SRS-SEC-S4-006 | SRS-SEC-S4-006, SRS-SEC-S4-007, SRS-SEC-S4-020 |
| B-DAT-04 | control-data-complete | — | SRS-SEC-S4-023 | SRS-SEC-S4-023 |
| B-DAT-05 | control-data-request | — | SRS-SEC-S4-024 | SRS-SEC-S4-024, SRS-SEC-S4-025 |
| B-DAT-06 | setpoint-update | — | SRS-SEC-S4-021 | SRS-SEC-S4-021 |
| B-DAT-07 | registration-type-update | — | SRS-SEC-S12-018 | SRS-SEC-S12-018 |
| B-DAT-08 | range-command | — | SRS-SEC-S7-023 | SRS-SEC-S7-023, SRS-SEC-S7-024, SRS-SEC-S7-025 |
| B-CTL-01 | start | — | SRS-SEC-S4-011 | SRS-SEC-S4-011 |
| B-CTL-02 | stop | — | SRS-SEC-S4-012 | SRS-SEC-S4-012 |
| B-CTL-03 | interrupt | — | SRS-SEC-S4-013 | SRS-SEC-S4-013 |
| B-CTL-04 | pause | — | SRS-SEC-S4-015 | SRS-SEC-S4-015 |
| B-CTL-05 | continue | — | SRS-SEC-S4-014 | SRS-SEC-S4-014 |
| B-CTL-06 | set-digital-output | — | SRS-SEC-S17-008 | SRS-SEC-S17-008 |
| B-CTL-07 | clear-fault | — | SRS-SEC-S13-011 | SRS-SEC-S13-011, SRS-SEC-S13-012 |
| B-CTL-08 | control-ack | — | — | **—** |
| B-TLM-01 | realtime | — | SRS-SEC-S12-001 | SRS-SEC-S12-001, SRS-SEC-S12-010 |
| B-TLM-02 | registration | — | SRS-SEC-S12-011 | SRS-SEC-S12-011, SRS-SEC-S12-019 |
| B-TLM-03 | registration-on-demand | — | SRS-SEC-S12-016 | SRS-SEC-S12-016 |
| B-TLM-04 | registration-final | — | SRS-SEC-S12-017 | SRS-SEC-S12-017 |
| B-TLM-05 | fault-report | — | SRS-SEC-S13-003 | SRS-SEC-S13-003, SRS-SEC-S13-004 |
| B-TLM-06 | registration-loss-report | — | SRS-SEC-S12-020 | SRS-SEC-S12-020, SRS-SEC-S12-022 |
| B-TLM-07 | diagnostics | — | — | **—** |
| B-TLM-08 | telemetry-rate-config | — | SRS-SEC-S12-008 | SRS-SEC-S12-008, SRS-SEC-S12-011 |
| B-CAL-01 | A-CAL-01 | — | — | **—** |
| B-CAL-02 | A-CAL-02 | — | — | **—** |
| B-CAL-03 | A-CAL-03 | — | — | **—** |
| B-CAL-04 | A-CAL-04 | — | — | **—** |
| B-CAL-05 | A-CAL-05 | — | — | **—** |
| B-CAL-06 | A-CAL-06 | — | — | **—** |
| B-CAL-07 | A-CAL-07 | — | — | **—** |
| B-CAL-08 | A-CAL-08 | — | — | **—** |
| B-CAL-09 | A-CAL-09 | — | — | **—** |
| B-CAL-10 | A-CAL-10 | — | — | **—** |
| B-CAL-11 | A-CAL-11 | — | — | **—** |
| B-CAL-12 | A-CAL-12 | — | — | **—** |
| B-CAL-13 | A-CAL-13 | — | — | **—** |
| B-CAL-14 | A-CAL-14 | — | — | **—** |
| B-CAL-15 | A-CAL-15 | — | — | **—** |
| B-CAL-16 | A-CAL-16 | — | — | **—** |
| B-CAL-17 | A-CAL-17 | — | — | **—** |
| B-CAL-18 | A-CAL-18 | — | — | **—** |
| B-CAL-19 | A-CAL-19 | — | — | **—** |
| B-CAL-20 | A-CAL-20 | — | — | **—** |
| B-CAL-21 | A-CAL-21 | — | — | **—** |
| B-CAL-22 | A-CAL-22 | — | — | **—** |
| B-CAL-23 | A-CAL-23 | — | — | **—** |
| B-CAL-24 | calibration-parameters | — | — | **—** |
| B-CAL-25 | calibration-live | — | SRS-SEC-S14-017 | SRS-SEC-S14-017, SRS-SEC-S14-018 |
| B-FWU-01 | update-prepare | — | SRS-SEC-S16-003 | SRS-SEC-S16-003, SRS-SEC-S16-008 |
| B-FWU-02 | update-segment | — | SRS-SEC-S16-005 | SRS-SEC-S16-005, SRS-SEC-S16-006 |
| B-FWU-03 | update-segment-ack | — | — | **—** |
| B-FWU-04 | update-activate | — | SRS-SEC-S16-007 | SRS-SEC-S16-007, SRS-SEC-S16-012 |
| B-FWU-05 | update-result | — | SRS-SEC-S16-013 | SRS-SEC-S16-013 |
| B-FWU-06 | update-abort | — | SRS-SEC-S16-011 | SRS-SEC-S16-011 |
| B-PFA-01 | power-fail-notify | — | SRS-SEC-S19-001 | SRS-SEC-S19-001, SRS-SEC-S19-006 |
| B-PFA-02 | persisted-context | — | SRS-SEC-S19-004 | SRS-SEC-S19-004, SRS-SEC-S19-010, SRS-SEC-S19-011 |
| B-PFA-03 | power-resume | — | SRS-SEC-S19-008 | SRS-SEC-S19-008 |
| B-PFA-04 | restart-report | — | SRS-SEC-S1-017, SRS-SEC-S11-005 | SRS-SEC-S1-017, SRS-SEC-S1-018, SRS-SEC-S11-005 |

### 4.3 IF-D — Primary Board ↔ Modbus client

**7 messages.**

| Msg | Name | Primary req. | Secondary req. | Consumed by |
|---|---|---|---|---|
| D-MB-01 | read-holding-registers | — | — | **—** |
| D-MB-02 | read-input-registers | — | — | **—** |
| D-MB-03 | write-single-register | — | — | **—** |
| D-MB-04 | write-multiple-registers | — | — | **—** |
| D-MB-05 | read-coils / read-discrete-inputs | — | — | **—** |
| D-MB-06 | write-single-coil | — | — | **—** |
| D-MB-07 | diagnostic / report-slave-id | — | — | **—** |

---

## 5. Everything still open, consolidated

The four registers are spread across four documents. This is all of them in one place,
with what each blocks. **This is the section to work through with the client.**

### 5.1 Open issues

| # | Status | Primary | Secondary | Web App | ICD |
|---|---|---|---|---|---|
| **#1** | **CLOSED** | 4 | 4 | 0 | 2 |
| **#2** | open | 1 | 1 | 2 | 0 |
| **#3** | open | 2 | 2 | 0 | 0 |
| **#4** | **CLOSED** | 4 | 0 | 0 | 0 |
| **#5** | **CLOSED** | 7 | 4 | 1 | 0 |
| **#6** | open | 2 | 2 | 1 | 0 |
| **#7** | open | 2 | 5 | 1 | 0 |
| **#8** | open | 3 | 8 | 0 | 0 |
| **#9** | open | 12 | 12 | 1 | 2 |
| **#10** | open | 13 | 9 | 2 | 0 |
| **#11** | open | 9 | 3 | 6 | 1 |
| **#12** | open | 13 | 8 | 0 | 1 |
| **#13** | open | 6 | 3 | 0 | 0 |
| **#14** | open | 3 | 11 | 2 | 1 |
| **#15** | open | 3 | 3 | 2 | 2 |
| **#16** | open | 5 | 2 | 2 | 1 |
| **#17** | open | 6 | 8 | 1 | 4 |
| **#18** | open | 3 | 3 | 0 | 2 |
| **#19** | open | 3 | 2 | 1 | 2 |
| **#20** | open | 4 | 4 | 0 | 0 |
| **#21** | open | 1 | 5 | 0 | 0 |
| **#22** | open | 4 | 5 | 0 | 2 |
| **#23** | open | 7 | 5 | 0 | 1 |
| **#24** | open | 4 | 1 | 0 | 0 |
| **#25** | open | 5 | 2 | 2 | 1 |
| **#26** | open | 1 | 3 | 0 | 0 |
| **#27** | **CLOSED** | 8 | 2 | 0 | 0 |
| **#28** | open | 0 | 0 | 0 | 0 |
| **#29** | **CLOSED** | 2 | 1 | 1 | 1 |
| **#30** | open | 0 | 2 | 0 | 1 |
| **S-32** | open (new) | 0 | 0 | 0 | 0 |
| **W-33** | open (new) | 0 | 0 | 2 | 0 |
| **W-34** | open (new) | 0 | 0 | 1 | 0 |
| **W-35** | open (new) | 0 | 0 | 1 | 0 |
| **W-36** | open (new) | 0 | 0 | 6 | 0 |
| **W-37** | open (new) | 0 | 0 | 2 | 0 |
| **W-38** | open (new) | 0 | 0 | 1 | 0 |
| **W-39** | open (new) | 0 | 0 | 2 | 0 |
| **W-40** | open (new) | 0 | 0 | 3 | 0 |

*Counts are requirements whose row names the issue. A zero does not mean the issue is
irrelevant to that document — it may be discussed in prose. It means no requirement is
formally tied to it.*

### 5.2 Conflicts

| ID | Primary | Secondary | Web App | ICD | Total requirements affected |
|---|---|---|---|---|---|
| **C-01** | 0 | 1 | 1 | 0 | 2 |
| **C-02** | 0 | 1 | 1 | 0 | 2 |
| **C-03** | 0 | 1 | 1 | 0 | 2 |
| **C-04** | 0 | 1 | 1 | 0 | 2 |
| **C-05** | 2 | 2 | 1 | 1 | 6 |
| **C-06** | 1 | 2 | 1 | 1 | 5 |
| **C-07** | 0 | 1 | 0 | 0 | 1 |
| **C-08** | 0 | 1 | 0 | 0 | 1 |
| **C-09** | 0 | 0 | 0 | 0 | 0 |
| **C-10** | 2 | 0 | 0 | 0 | 2 |
| **C-11** | 1 | 3 | 0 | 0 | 4 |
| **C-12** | 1 | 1 | 0 | 1 | 3 |
| **C-13** | 2 | 0 | 2 | 0 | 4 |
| **C-14** | 3 | 1 | 2 | 1 | 7 |
| **C-15** | 0 | 1 | 0 | 0 | 1 |
| **C-16** | 1 | 1 | 0 | 0 | 2 |
| **C-17** | 0 | 0 | 0 | 0 | 0 |
| **C-18** | 0 | 0 | 0 | 0 | 0 |
| **C-19** | 0 | 0 | 0 | 0 | 0 |
| **C-20** | 0 | 0 | 0 | 0 | 0 |
| **C-21** | 0 | 0 | 0 | 0 | 0 |
| **C-22** | 0 | 0 | 2 | 0 | 2 |
| **C-23** | 0 | 0 | 1 | 0 | 1 |
| **C-24** | 0 | 0 | 3 | 0 | 3 |
| **C-25** | 0 | 0 | 0 | 0 | 0 |
| **C-26** | 0 | 0 | 2 | 0 | 2 |
| **C-27** | 0 | 0 | 1 | 0 | 1 |

### 5.3 Unresolved values, by document

| Namespace | Document | Count | Register |
|---|---|---|---|
| `<TBD-nn>` | `01_SRS_ME_Primary_Board_v0.1.md` | **42** | §8.3 |
| `<TBD-Snn>` | `02_SRS_ME_Secondary_Board_v0.1.md` | **72** | §8.3 |
| `<TBD-Inn>` | `03_ICD_ME_Interfaces_v0.1.md` | **28** | §9.3 |
| `<TBD-Wnn>` | `04_SRS_ME_Web_Application_v0.1.md` | **39** | §8.3 |

**Total unresolved values across the set: 181.**

### 5.4 Reserved decisions

| ID | Decision | Bears on | Worksheet |
|---|---|---|---|
| **D-01** | Which core runs what on the Primary (A53 cluster vs Cortex-M7) | `PRI` — every requirement carries `Alloc: TBD` | `PRIA` §A2 |
| **D-02** | Which Primary core owns the CAN controller | `PRI` | with D-01 |
| **D-03** | What the control data sent to a Secondary actually contains | `SEC` §4.4, §4.5.4; `ICD` §5.5 | `ICD` §9.4 |
| **D-04** | CAN details: 2.0B or FD, bit rate, addressing, framing, priority | all of `ICD` §5; `SEC` §3.3; `WEB` §5.1 capacity | `ICD` §9.4 |
| **D-05** | IF-A transport, ports, framing, serialization | all of `ICD` §4 and §6; `WEB` §3.3 | `ICD` §9.4 |
| **D-06** | Inter-core communication on the Primary | `PRI` | with D-01 |

> **D-04 is on the critical path for three documents at once.** `ICD` §7.3 shows the
> registration-rate question and the CAN-FD question are one decision; `WEB` §5.1 shows
> the Web Application's capacity swings tenfold on the answer; `SEC` `<TBD-S41>` cannot
> be closed without it. Answer D-04 first.

---

## 6. Shared concept dictionary

One row per idea that appears in more than one document. `Defined in` is the single
authority; the counts are documents that use it. This table exists because we already
lost time to one word meaning three things — see conflict **C-21**.

| Concept | Defined in | PRI | SEC | ICD | WEB |
|---|---|---|---|---|---|
| Program operator catalog (`CHA`, `DCH`, `RCH`, `PAU`, `CYC`, …) | `PRI` §4.7.2–§4.7.7 | 51 | 0 | 0 | 0 |
| Regulation mode inferred from the nominal value | `PRI` §4.7.8 (SRS-PRI-P7-130 to -132) | 41 | 0 | 1 | 4 |
| Registration — *a saved measurement row* | `PRI` §1.4; `SEC` §1.4 | 5 | 16 | 6 | 1 |
| Session registration — *a board announcing itself* | `WEB` §1.4; `ICD` §4.2 | 0 | 0 | 12 | 13 |
| Limit standard — *a named set of bounds* | `WEB` §1.4, §4.13 | 0 | 0 | 0 | 23 |
| Fault and message code space | unresolved — conflict **C-14** | 15 | 12 | 12 | 9 |
| Channel identity (as seen by the Web Application) | `ICD` CS-03, CS-04 | 2 | 0 | 9 | 0 |
| Node identity (as seen on the CAN bus) | `ICD` CS-01, CS-02; **D-04** | 5 | 15 | 15 | 4 |
| Control data content | reserved — **D-03** | 8 | 29 | 18 | 1 |
| Safe state | `SEC` §1.4, §4.9.3 | 22 | 38 | 12 | 1 |
| Calibration command set (23 commands) | `ICD` §4.8 / §5.8 | 0 | 2 | 80 | 14 |
| Unit convention (A, V, W, Ah, Wh, °C, ms) | `ICD` CS-39, CS-40 | 0 | 0 | 5 | 3 |
| Presence / channel inventory | `PRI` §4.3; `WEB` §4.3 | 9 | 1 | 13 | 14 |
| Program version identity | `WEB` §4.4 (SRS-WEB-W4-006) | 3 | 0 | 1 | 2 |

> **The three meanings of "registration".** A *registration* is a saved measurement
> row (`PRI`/`SEC`/`BM`). A *session registration* is a board announcing itself
> (`WEB`/`WAD`). A *limit standard* is a named set of bounds, which the old product
> called a `RegistrationStandard`. They are unrelated. Conflict **C-21** asks for a
> naming decision before any of this reaches code or a database schema.

---

## 7. Gap report

What linking the documents together revealed. These are defects in the set, listed so
they can be fixed rather than discovered later.

| # | Finding | Count | Severity | Where |
|---|---|---|---|---|
| G-01 | ~~The Primary SRS names no ICD message anywhere.~~ **CLOSED** by the §8 backfill: 18 requirements now carry the message reference the ICD already stated, so the link to either interface it owns reads from inside the SRS. | 16 message ids, 18 requirements | resolved | `01_SRS_ME_Primary_Board_v0.1.md` |
| G-02 | ~~The Secondary SRS names no ICD message anywhere.~~ **CLOSED** by the §8 backfill: 60 requirements now carry the message reference the ICD already stated, so the link to IF-B reads from inside the SRS. | 46 message ids, 60 requirements | resolved | `02_SRS_ME_Secondary_Board_v0.1.md` |
| G-03 | Messages that no requirement in any SRS names. Each is either genuinely unclaimed or claimed only in prose. | 64 of 173 | **MED** | listed below |
| G-04 | Messages the ICD does not tie to any Primary or Secondary requirement. | 111 of 173 | **MED** | listed below |
| G-05 | The Web Application SRS does name ICD messages, so IF-A is linked from one side only. | 68 message ids in `WEB` | **LOW** | acceptable until G-01 is fixed |

### 7.0 How to read G-03 and G-04

The two counts look alarming and mostly are not. A message needs its own SRS
requirement only where the SRS has something to say that the ICD does not. Three
categories are **expected** to appear in the lists below and need no action:

| Category | Count | Why it is expected |
|---|---|---|
| Calibration commands (`A-CAL-*`, `B-CAL-*`) | 48 | The SRS specifies the calibration *procedure* once (`SEC` §4.14, `WEB` §4.12); the ICD enumerates each command so it can receive its own encoding. One requirement per command would add nothing. |
| Modbus function codes (`D-MB-*`) | 7 | These are standard Modbus functions, not ME behaviour. The real work is the register map — `ICD` `<TBD-I19>`. |
| Acknowledgements, replies and result reports | 10 | Their behaviour comes from the common-semantics rules `ICD` CS-07 to CS-11, not from a per-message requirement. |

**After excluding those three categories, 18 of the 64 G-03 entries remain** — these are the ones worth reviewing, because each is a message that crosses a real interface with no requirement in any SRS obliging anyone to implement it.

**Of those 18, 0 are already tied to a Primary or Secondary requirement by the ICD** — they appear here only because the link is recorded in the ICD and not in the SRS. **Applying the §8 backfill closes them.** They are a consequence of G-01 and G-02, not independent findings. That leaves **18** genuinely unclaimed messages.

| Msg | Name | Interface | Closed by §8 backfill? |
|---|---|---|---|
| A-CFG-01 | config-is-ready | IF-A | **no — needs a requirement** |
| A-CFG-02 | read-factory-config | IF-A | **no — needs a requirement** |
| A-CFG-05 | read-manufacturing-data | IF-A | **no — needs a requirement** |
| A-CFG-07 | write-manufacturing-data | IF-A | **no — needs a requirement** |
| A-CFG-08 | read-battery-params | IF-A | **no — needs a requirement** |
| A-CFG-09 | battery-params | IF-A | **no — needs a requirement** |
| A-EVT-02 | channel-message | IF-A | **no — needs a requirement** |
| A-EVT-03 | channel-user-error | IF-A | **no — needs a requirement** |
| B-NOD-05 | assign-node-address | IF-B | **no — needs a requirement** |
| B-CFG-01 | config-is-ready | IF-B | **no — needs a requirement** |
| B-CFG-02 | read-factory-config | IF-B | **no — needs a requirement** |
| B-CFG-03 | factory-config | IF-B | **no — needs a requirement** |
| B-CFG-05 | write-battery-params | IF-B | **no — needs a requirement** |
| B-CFG-06 | write-manufacturing-data | IF-B | **no — needs a requirement** |
| B-CFG-07 | read-manufacturing-data | IF-B | **no — needs a requirement** |
| B-CFG-08 | read-config-item | IF-B | **no — needs a requirement** |
| B-CFG-09 | write-config-item | IF-B | **no — needs a requirement** |
| B-TLM-07 | diagnostics | IF-B | **no — needs a requirement** |

### 7.1 G-03 — full list: messages named by no requirement

| Msg | Name | Interface |
|---|---|---|
| A-CFG-01 | config-is-ready | IF-A |
| A-CFG-02 | read-factory-config | IF-A |
| A-CFG-05 | read-manufacturing-data | IF-A |
| A-CFG-07 | write-manufacturing-data | IF-A |
| A-CFG-08 | read-battery-params | IF-A |
| A-CFG-09 | battery-params | IF-A |
| A-CAL-04 | cha-current-high-point | IF-A |
| A-CAL-05 | cha-current-commit | IF-A |
| A-CAL-06 | dch-current-low-point | IF-A |
| A-CAL-07 | dch-current-high-point | IF-A |
| A-CAL-08 | dch-current-commit | IF-A |
| A-CAL-09 | cha-voltage-low-point | IF-A |
| A-CAL-10 | cha-voltage-high-point | IF-A |
| A-CAL-11 | cha-voltage-commit | IF-A |
| A-CAL-12 | dch-voltage-low-point | IF-A |
| A-CAL-13 | dch-voltage-high-point | IF-A |
| A-CAL-14 | dch-voltage-commit | IF-A |
| A-CAL-15 | temp-low-point | IF-A |
| A-CAL-16 | temp-high-point | IF-A |
| A-EVT-02 | channel-message | IF-A |
| A-EVT-03 | channel-user-error | IF-A |
| B-NOD-05 | assign-node-address | IF-B |
| B-CFG-01 | config-is-ready | IF-B |
| B-CFG-02 | read-factory-config | IF-B |
| B-CFG-03 | factory-config | IF-B |
| B-CFG-05 | write-battery-params | IF-B |
| B-CFG-06 | write-manufacturing-data | IF-B |
| B-CFG-07 | read-manufacturing-data | IF-B |
| B-CFG-08 | read-config-item | IF-B |
| B-CFG-09 | write-config-item | IF-B |
| B-CTL-08 | control-ack | IF-B |
| B-TLM-07 | diagnostics | IF-B |
| B-CAL-01 | A-CAL-01 | IF-B |
| B-CAL-02 | A-CAL-02 | IF-B |
| B-CAL-03 | A-CAL-03 | IF-B |
| B-CAL-04 | A-CAL-04 | IF-B |
| B-CAL-05 | A-CAL-05 | IF-B |
| B-CAL-06 | A-CAL-06 | IF-B |
| B-CAL-07 | A-CAL-07 | IF-B |
| B-CAL-08 | A-CAL-08 | IF-B |
| B-CAL-09 | A-CAL-09 | IF-B |
| B-CAL-10 | A-CAL-10 | IF-B |
| B-CAL-11 | A-CAL-11 | IF-B |
| B-CAL-12 | A-CAL-12 | IF-B |
| B-CAL-13 | A-CAL-13 | IF-B |
| B-CAL-14 | A-CAL-14 | IF-B |
| B-CAL-15 | A-CAL-15 | IF-B |
| B-CAL-16 | A-CAL-16 | IF-B |
| B-CAL-17 | A-CAL-17 | IF-B |
| B-CAL-18 | A-CAL-18 | IF-B |
| B-CAL-19 | A-CAL-19 | IF-B |
| B-CAL-20 | A-CAL-20 | IF-B |
| B-CAL-21 | A-CAL-21 | IF-B |
| B-CAL-22 | A-CAL-22 | IF-B |
| B-CAL-23 | A-CAL-23 | IF-B |
| B-CAL-24 | calibration-parameters | IF-B |
| B-FWU-03 | update-segment-ack | IF-B |
| D-MB-01 | read-holding-registers | IF-D |
| D-MB-02 | read-input-registers | IF-D |
| D-MB-03 | write-single-register | IF-D |
| D-MB-04 | write-multiple-registers | IF-D |
| D-MB-05 | read-coils / read-discrete-inputs | IF-D |
| D-MB-06 | write-single-coil | IF-D |
| D-MB-07 | diagnostic / report-slave-id | IF-D |

### 7.2 G-04 — messages with no Primary or Secondary requirement cited

| Msg | Name | Interface | Source given in the ICD |
|---|---|---|---|
| A-DSC-01 | discover-request | IF-A | `WAD/ICD` §4.2 Q1 |
| A-DSC-02 | discover-reply | IF-A | `WAD/ICD` §4.2 (`BroadcastDeviceInfo`) |
| A-DSC-03 | set-board-network-config | IF-A | `WAD/ICD` §4.2 Q4 (`BroadcastIpConfig`) |
| A-DSC-04 | set-server-config | IF-A | `WAD/ICD` §4.2 Q5 (`BroadcastServerConfig`) |
| A-DSC-05 | network-config-applied | IF-A | NEW — ME (the legacy protocol has no confirmation, so a failed reconfiguration is silent) |
| A-REG-01 | register | IF-A | `WAD/ICD` §3.4; `CODE-P` `DEVICE_REG_DATA_PACKET 0xDD` |
| A-REG-02 | register-response | IF-A | `WAD/ICD` §3.4 (status `0x01`/`0x02`/`0x00`) |
| A-REG-03 | deregister | IF-A | `WAD` device endpoints (delete-device) |
| A-REG-04 | keep-alive | IF-A | `CODE-P` `WEBAPP_RECONNECT_TIMEOUT_MS` 300 000; NEW — ME |
| A-CFG-01 | config-is-ready | IF-A | `CODE-P` `en_isReadyforConfigQuery 0x01`; CS-36 |
| A-CFG-02 | read-factory-config | IF-A | `CODE-P` `en_readFactoryDataQuery 0x02` |
| A-CFG-03 | factory-config | IF-A | `CODE-S` `BTS_FactoryData_t` (109 B); HW *Configuration* |
| A-CFG-05 | read-manufacturing-data | IF-A | `CODE-P` `en_readManufacturingDataQuery 0x03` |
| A-CFG-06 | manufacturing-data | IF-A | HW *Configuration* #1–4, #44–47 |
| A-CFG-07 | write-manufacturing-data | IF-A | `CODE-P` `MANUFACTURING_DATA_PACKET 0x03` |
| A-CFG-08 | read-battery-params | IF-A | `CODE-P` `en_readBatteryInfoQuery 0x04` |
| A-CFG-09 | battery-params | IF-A | BM §12.3 (`INTERN[17]`…`INTERN[27]`); `CODE-S` `BTS_BatteryData_t` (43 B) |
| A-CFG-10 | write-battery-params | IF-A | `CODE-P` `en_writeBatteryInfoQuery 0x05` |
| A-CFG-11 | sync-time | IF-A | `CODE-P` `en_syncTimeQuery 0x06`; `LSRS` SW_REQ_21 |
| A-CFG-12 | read-config-item | IF-A | HW *Configuration* (parameter column gives per-item identifiers) |
| A-CFG-13 | write-config-item | IF-A | HW *Configuration*; `SRS-PRI-P18-*` |
| A-PRG-03 | program-step-count | IF-A | `CODE-P` `en_programStepsQuery 0x03`; `WAD/ICD` §3.6 `0x03` |
| A-PRG-04 | program-data | IF-A | `CODE-P` `en_programDataQuery 0x04`, 1 MB program buffer; BM §12.4 step record |
| A-CTL-01 | start | IF-A | `CODE-P` `en_startProgram 1`; BM §2.4; `SRS-PRI-P19-*` |
| A-CTL-02 | stop | IF-A | `CODE-P` `en_stopProgram 2`; BM §12.4.5 `STO` |
| A-CTL-03 | pause | IF-A | `CODE-P` `en_pauseProgram 3`; BM §12.4.8.3 `PAU` |
| A-CTL-04 | interrupt | IF-A | BM §12.4.5 `INT`; `CODE-S` `csInt` |
| A-CTL-05 | continue | IF-A | `CODE-P` `en_continueProgram 4`; BM §2 |
| A-CTL-06 | reset | IF-A | `CODE-P` `en_reset 6`; BM §2.10 `RESET` |
| A-CTL-08 | set-digital-output | IF-A | `CODE-S` `CONTROL_CMD_Q_ID_DO_SELECTION`; Open Issue **#19** |
| A-CTL-09 | control-result | IF-A | CS-07, CS-08; NEW — ME (the legacy protocol acknowledges per device, not per channel) |
| A-TLM-01 | channel-realtime | IF-A | `WAD/ICD` §5.2 (79 B, 25 fields); `SRS-PRI-P10-*` |
| A-TLM-03 | telemetry-subscribe | IF-A | **NEW — ME**. Necessary because eight channels streaming at the legacy rate is eight times |
| A-TLM-04 | calibration-live | IF-A | `WAD/ICD` §5.3 (21 B); `CODE-S` `LIVE_DATA_SEND_TIME 500U` |
| A-LOG-01 | registration-batch | IF-A | `WAD/ICD` §6.2, §6.3; BM §12.4.1 |
| A-LOG-02 | registration-batch-ack | IF-A | **NEW — ME**. The legacy session-store interface is fire-and-forget UDP with **no acknowle |
| A-LOG-03 | request-buffered-data | IF-A | `SRS-PRI-P16-*`; HW pending item "Data save on Primary in case of PC disconnect"; Open Iss |
| A-LOG-05 | session-open | IF-A | `WAD/DBD` session model; `WAD/ICD` §6.1 |
| A-CAL-01 | calib-is-ready | IF-A | `CODE-P` `en_isReadyForCalibQuery 0x01` |
| A-CAL-02 | calib-live-data-start | IF-A | `CODE-P` `en_liveCalibData 0x02` |
| A-CAL-03 | cha-current-low-point | IF-A | `CODE-P` `en_chaCurrentLowPoint 0x03` |
| A-CAL-04 | cha-current-high-point | IF-A | `CODE-P` `en_chaCurrentHighPoint 0x04` |
| A-CAL-05 | cha-current-commit | IF-A | `CODE-P` `en_chaCurCalGainOffset 0x05` |
| A-CAL-06 | dch-current-low-point | IF-A | `CODE-P` `en_disChaCurrentLowPoint 0x06` |
| A-CAL-07 | dch-current-high-point | IF-A | `CODE-P` `en_disChaCurrentHighPoint 0x07` |
| A-CAL-08 | dch-current-commit | IF-A | `CODE-P` `en_disChaCurCalGainOffset 0x08` |
| A-CAL-09 | cha-voltage-low-point | IF-A | `CODE-P` `en_chaVoltageLowPoint 0x09` |
| A-CAL-10 | cha-voltage-high-point | IF-A | `CODE-P` `en_chaVoltageHighPoint 0x0A` |
| A-CAL-11 | cha-voltage-commit | IF-A | `CODE-P` `en_chaVolCalGainOffset 0x0B` |
| A-CAL-12 | dch-voltage-low-point | IF-A | `CODE-P` `en_disChaVoltageLowPoint 0x0C` |
| A-CAL-13 | dch-voltage-high-point | IF-A | `CODE-P` `en_disChaVoltageHighPoint 0x0D` |
| A-CAL-14 | dch-voltage-commit | IF-A | `CODE-P` `en_disChaVolCalGainOffset 0x0E` |
| A-CAL-15 | temp-low-point | IF-A | `CODE-P` `en_tempCalLowPoint 0x0F`; `CODE-S` `TEMP_LOW_RES_VALUE 18.52` |
| A-CAL-16 | temp-high-point | IF-A | `CODE-P` `en_tempCalHighPoint 0x10`; `CODE-S` `TEMP_HIGH_RES_VALUE 390.48` |
| A-CAL-17 | temp-commit | IF-A | `CODE-P` `en_tempCalGainOffset 0x11` |
| A-CAL-18 | calib-cancel | IF-A | `CODE-P` `en_cancleCalibration 0x12` |
| A-CAL-19 | calib-stop | IF-A | `CODE-P` `en_stopCalibration 0x13` |
| A-CAL-20 | verify-cha-current-start | IF-A | `CODE-P` `en_verifyCurrentCalChargeMode 0x14` |
| A-CAL-21 | verify-dch-current-start | IF-A | `CODE-P` `en_verifyCurrentCalDischargeMode 0x15` |
| A-CAL-22 | verify-current-stop | IF-A | `CODE-P` `en_stopVerifyCurrentCal 0x16` |
| A-CAL-23 | read-calibration | IF-A | `CODE-P` `en_prevCalibData 0x17`; `CODE-S` `readCalibParameters()` |
| A-EVT-02 | channel-message | IF-A | BM §12.4.8.6 `MSG`; BM default messages 1–6 |
| A-EVT-03 | channel-user-error | IF-A | BM §12.4.8.5 `ERR`; `CODE-S` `userExId` |
| A-EVT-05 | read-event-log | IF-A | `SRS-PRI-P22-*`; Open Issue **#17** |
| A-EVT-06 | message-catalogue | IF-A | BM §12.4.8.6 (*Maintenance → BTS-600 Messages*, user-extensible); conflict **C-14** |
| A-NOD-05 | assign-program-to-channels | IF-A | `SRS-PRI-P6-*`; BM §12.4.8.16 (SYNCLine/SYNCProgram); Open Issues **#15**, **#16** |
| A-NOD-06 | read-channel-assignment | IF-A | `SRS-PRI-P6-*`; **NEW — ME** |
| A-NOD-10 | primary-firmware-transfer | IF-A | `SRS-PRI-P20-*`; SOC HAB provides the integrity basis |
| B-NOD-05 | assign-node-address | IF-B | `<TBD-S15>`; Open Issue **#22**; **D-04**. **Conditional:** this message exists only if th |
| B-CFG-01 | config-is-ready | IF-B | `CODE-S` `CONFIG_DATA_Q_ID_IS_READY 0x01` |
| B-CFG-02 | read-factory-config | IF-B | `CODE-S` `CONFIG_DATA_Q_ID_READ_FACT_DATA 0x02` |
| B-CFG-03 | factory-config | IF-B | `CODE-S` `BTS_FactoryData_t` (109 B) |
| B-CFG-05 | write-battery-params | IF-B | `CODE-S` `CONFIG_DATA_Q_ID_WRITE_BATT_DATA 0x04`, `BTS_BatteryData_t` (43 B) |
| B-CFG-06 | write-manufacturing-data | IF-B | `CODE-S` `CONFIG_DATA_Q_ID_WRITE_MANUFACT_DATA 0x05` |
| B-CFG-07 | read-manufacturing-data | IF-B | `CODE-S` `CONFIG_DATA_Q_ID_READ_MANUFACT_DATA 0x06` |
| B-CFG-08 | read-config-item | IF-B | HW *Configuration* (per-item parameter identifiers) |
| B-CFG-09 | write-config-item | IF-B | HW *Configuration*; `SRS-SEC-S15-*` |
| B-CTL-08 | control-ack | IF-B | `CODE-S` `CONTROL_CMD_Q_RESP_ACK`/`_NACK`; CS-07 |
| B-TLM-07 | diagnostics | IF-B | `SRS-SEC-S18-*`; HW *Configuration* #23–42; HW remarks #30, #31, #34 |
| B-CAL-01 | A-CAL-01 | IF-B | none |
| B-CAL-02 | A-CAL-02 | IF-B | requested interval |
| B-CAL-03 | A-CAL-03 | IF-B | applied reference current; measurement range |
| B-CAL-04 | A-CAL-04 | IF-B | applied reference current; measurement range |
| B-CAL-05 | A-CAL-05 | IF-B | measurement range; calibration timestamp |
| B-CAL-06 | A-CAL-06 | IF-B | applied reference current; measurement range |
| B-CAL-07 | A-CAL-07 | IF-B | applied reference current; measurement range |
| B-CAL-08 | A-CAL-08 | IF-B | measurement range; calibration timestamp |
| B-CAL-09 | A-CAL-09 | IF-B | applied reference voltage |
| B-CAL-10 | A-CAL-10 | IF-B | applied reference voltage |
| B-CAL-11 | A-CAL-11 | IF-B | calibration timestamp |
| B-CAL-12 | A-CAL-12 | IF-B | applied reference voltage |
| B-CAL-13 | A-CAL-13 | IF-B | applied reference voltage |
| B-CAL-14 | A-CAL-14 | IF-B | calibration timestamp |
| B-CAL-15 | A-CAL-15 | IF-B | applied reference resistance or temperature |
| B-CAL-16 | A-CAL-16 | IF-B | applied reference resistance or temperature |
| B-CAL-17 | A-CAL-17 | IF-B | calibration timestamp |
| B-CAL-18 | A-CAL-18 | IF-B | none |
| B-CAL-19 | A-CAL-19 | IF-B | none |
| B-CAL-20 | A-CAL-20 | IF-B | commanded current |
| B-CAL-21 | A-CAL-21 | IF-B | commanded current |
| B-CAL-22 | A-CAL-22 | IF-B | none |
| B-CAL-23 | A-CAL-23 | IF-B | which parameter set to return |
| B-CAL-24 | calibration-parameters | IF-B | `CODE-S` `btsFact_n_calib_t`, `readCalibParameters()`; HW *Calibration* (25 parameters) |
| B-FWU-03 | update-segment-ack | IF-B | CS-26; **NEW — ME** |
| D-MB-01 | read-holding-registers | IF-D | `LSRS` SW_REQ_24, 27, 33 |
| D-MB-02 | read-input-registers | IF-D | `LSRS` SW_REQ_33 |
| D-MB-03 | write-single-register | IF-D | `LSRS` SW_REQ_33; Open Issue **#29** |
| D-MB-04 | write-multiple-registers | IF-D | `LSRS` SW_REQ_33 |
| D-MB-05 | read-coils / read-discrete-inputs | IF-D | `LSRS` SW_REQ_27 (digital input reporting) |
| D-MB-06 | write-single-coil | IF-D | `LSRS` SW_REQ_15; Open Issue **#19** |
| D-MB-07 | diagnostic / report-slave-id | IF-D | `LSRS` SW_REQ_33 |

---

## 8. Interface backfill — applied

G-01 and G-02 were closed by adding the ICD message identifier to the `Source` column
of each requirement the ICD already names. This is recorded here as the audit trail of
that change.

> **The ICD abbreviates runs of requirement ids.** A Source cell reading
> `` `SRS-SEC-S15-016`, `-017` `` names *two* requirements. Both are expanded below;
> reading only full identifiers would have missed 29 of them.

**Status: applied.** 78 of the 78 requirements below now carry the reference. The edit added no information and changed no requirement's meaning — every reference was already stated in the ICD; it simply was not readable from the SRS side.

Re-apply at any time with `tools/apply_backfill.py --apply`. It is idempotent: it recomputes the reference list from the ICD and rewrites it, so it stays correct if the ICD changes.

| Document | Requirement | Reference added |
|---|---|---|
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P1-013 | B-NOD-01 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P2-012 | A-TLM-02 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-004 | A-NOD-02 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-005 | A-NOD-04 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-007 | B-NOD-03 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-008 | B-NOD-04 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-009 | A-EVT-04, A-NOD-03 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-011 | A-NOD-01, A-NOD-02, A-TLM-02 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P3-012 | A-NOD-02 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-002 | A-PRG-01 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-003 | A-PRG-02 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-007 | A-PRG-05 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-008 | A-PRG-05 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-009 | A-PRG-08 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-013 | A-PRG-09 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-014 | A-PRG-06 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-015 | A-PRG-07 |
| `01_SRS_ME_Primary_Board_v0.1.md` | SRS-PRI-P4-016 | A-PRG-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-NF-032 | A-CFG-04, B-CFG-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S1-017 | B-PFA-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S1-018 | B-PFA-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S11-005 | B-PFA-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-001 | B-TLM-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-008 | B-TLM-08 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-010 | B-TLM-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-011 | B-TLM-02, B-TLM-08 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-016 | B-TLM-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-017 | A-LOG-06, B-TLM-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-018 | B-DAT-07 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-019 | B-TLM-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-020 | B-TLM-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S12-022 | A-LOG-04, B-TLM-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S13-003 | A-EVT-01, B-TLM-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S13-004 | A-EVT-01, B-TLM-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S13-011 | A-CTL-07, B-CTL-07 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S13-012 | A-CTL-07, B-CTL-07 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S14-017 | B-CAL-25 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S14-018 | B-CAL-25 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S15-013 | B-CFG-11 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S15-016 | A-CFG-14, B-CFG-10 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S15-017 | A-CFG-14, B-CFG-10 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S15-018 | B-CFG-11 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-001 | A-NOD-07 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-003 | A-NOD-08, B-FWU-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-005 | B-FWU-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-006 | B-FWU-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-007 | B-FWU-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-008 | B-FWU-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-011 | B-FWU-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-012 | B-FWU-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S16-013 | A-NOD-09, B-FWU-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S17-008 | B-CTL-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-001 | B-PFA-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-004 | B-PFA-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-006 | B-PFA-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-008 | B-PFA-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-010 | B-PFA-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S19-011 | B-PFA-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S3-007 | B-NOD-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-001 | B-DAT-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-002 | B-DAT-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-006 | B-DAT-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-007 | B-DAT-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-010 | B-DAT-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-011 | B-CTL-01 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-012 | B-CTL-02 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-013 | B-CTL-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-014 | B-CTL-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-015 | B-CTL-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-018 | B-NOD-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-020 | B-DAT-03 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-021 | B-DAT-06 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-023 | B-DAT-04 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-024 | B-DAT-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S4-025 | B-DAT-05 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S7-023 | B-DAT-08 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S7-024 | B-DAT-08 |
| `02_SRS_ME_Secondary_Board_v0.1.md` | SRS-SEC-S7-025 | B-DAT-08 |

---

*Generated from the eight documents of the ME set. 173 messages · 78 requirements linked · 175 gaps found.*
