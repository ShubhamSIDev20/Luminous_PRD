# Annex to ME-SRS-SEC-001 — Traceability Matrix and HIL Verification Sheet

**Document ID:** ME-SRS-SEC-001-A · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Parent:** `02_SRS_ME_Secondary_Board_v0.1.md`

This annex holds §6 (Traceability Matrix) of the Secondary Board SRS, and adds a
hardware-in-the-loop verification sheet. It is **generated from the parent document**,
so the two cannot drift apart; regenerate it after any change to the parent.

> **Why there is no core-allocation sheet.** The Primary annex's §A2 exists to let
> the architect resolve **D-01** — the A53/M7 function split — in one pass. The
> Secondary Board is single-core, so no such decision exists. §A2 here is the
> equivalent single-pass artefact for *this* board: the register of requirements that
> cannot be closed without hardware.

Total requirements: **530** · hardware-critical: **195** · carrying an unsourced value: **41**

---

## A1. Traceability Matrix

### A1.1 Coverage roll-up by checklist area

Coverage ratings are carried from `GATE` §2.

| Area | Title | Coverage | Requirements | of which ⚠ |
|---|---|---|---|---|
| **S1** | S1 Startup, init, self-test, safe default outputs | ◐ Partial | 26 | 7 |
| **S2** | S2 Operating mode / state machine | ● Strong / ◐ | 20 | 4 |
| **S3** | S3 Node identity & CAN addressing | ◆ New to ME | 12 | 0 |
| **S4** | S4 Command & control-data reception, validation, rejection | ● Strong | 26 | 4 |
| **S5** | S5 Charge regulation | ● Strong | 37 | 25 |
| **S6** | S6 Discharge regulation | ● Strong | 14 | 13 |
| **S7** | S7 Setpoint application, ramping, slew limiting, switch-over | ● Strong (most conflicted) | 35 | 22 |
| **S8** | S8 Measurement acquisition & derived quantities | ● Strong | 40 | 16 |
| **S9** | S9 Local protection & interlocks | ● Strong | 41 | 28 |
| **S10** | S10 Autonomous safe state on fault or comms loss | ◐ Partial | 11 | 9 |
| **S11** | S11 Watchdog & timing supervision | ◐ Partial | 11 | 7 |
| **S12** | S12 Telemetry & registration reporting | ● Strong / conflicted rate | 25 | 2 |
| **S13** | S13 Fault reporting, codes, latch & clear | ◐ Partial | 17 | 3 |
| **S14** | S14 Calibration | ● Strong | 30 | 3 |
| **S15** | S15 Non-volatile configuration & persistence | ● Strong / ◐ policy | 20 | 3 |
| **S16** | S16 Firmware update over CAN | ○ Absent / conflicted | 18 | 4 |
| **S17** | S17 Local indication & digital outputs | ◐ Partial | 15 | 4 |
| **S18** | S18 Diagnostics & self-monitoring | ◐ Partial | 15 | 0 |
| **S19** | S19 Power-fail detection, backup & resume (added — see §9) | ● Strong / ○ policy | 15 | 7 |
| **CM** | §3.3 Communication interfaces | — | 12 | 1 |
| **HW** | §3.1 Hardware interfaces | — | 26 | 11 |
| **NF** | §5 Non-functional requirements | — | 53 | 22 |
| **SW** | §3.2 Software interfaces | — | 6 | 0 |
| **UI** | §3.4 User interfaces | — | 5 | 0 |
| | **Total** | | **530** | **195** |

### A1.2 Source → requirement

Reverse mapping. A requirement citing several sources appears under each, once. The
forward mapping (requirement → source) is the `Source` column of the parent.

| Source | Count | Requirements |
|---|---|---|
| CODE-S — legacy Secondary firmware | 195 | SRS-SEC-HW-011, SRS-SEC-HW-022, SRS-SEC-HW-023, SRS-SEC-CM-001, SRS-SEC-CM-002, SRS-SEC-CM-003, SRS-SEC-CM-004, SRS-SEC-CM-006, SRS-SEC-UI-003, SRS-SEC-S1-001, SRS-SEC-S1-002, SRS-SEC-S1-004, SRS-SEC-S1-005, SRS-SEC-S1-006, SRS-SEC-S1-007, SRS-SEC-S1-008, SRS-SEC-S1-009, SRS-SEC-S1-010, SRS-SEC-S1-011, SRS-SEC-S1-012, SRS-SEC-S1-017, SRS-SEC-S1-023, SRS-SEC-S2-002, SRS-SEC-S2-003, SRS-SEC-S2-005, SRS-SEC-S2-008, SRS-SEC-S2-009, SRS-SEC-S2-010, SRS-SEC-S2-011, SRS-SEC-S2-015, SRS-SEC-S2-016, SRS-SEC-S3-001, SRS-SEC-S3-004, SRS-SEC-S4-003, SRS-SEC-S4-004, SRS-SEC-S4-005, SRS-SEC-S4-006, SRS-SEC-S4-008, SRS-SEC-S4-009, SRS-SEC-S4-010, SRS-SEC-S4-011, SRS-SEC-S4-012, SRS-SEC-S4-013, SRS-SEC-S4-014, SRS-SEC-S4-015, SRS-SEC-S4-016, SRS-SEC-S4-017, SRS-SEC-S4-018, SRS-SEC-S4-019, SRS-SEC-S4-023, SRS-SEC-S4-024, SRS-SEC-S5-001, SRS-SEC-S5-002, SRS-SEC-S5-003, SRS-SEC-S5-004, SRS-SEC-S5-005, SRS-SEC-S5-007, SRS-SEC-S5-008, SRS-SEC-S5-009, SRS-SEC-S5-010, SRS-SEC-S5-012, SRS-SEC-S5-014, SRS-SEC-S5-015, SRS-SEC-S5-016, SRS-SEC-S5-018, SRS-SEC-S5-019, SRS-SEC-S5-020, SRS-SEC-S5-021, SRS-SEC-S5-022, SRS-SEC-S5-023, SRS-SEC-S5-027, SRS-SEC-S5-028, SRS-SEC-S5-029, SRS-SEC-S5-031, SRS-SEC-S5-035, SRS-SEC-S5-036, SRS-SEC-S5-037, SRS-SEC-S6-001, SRS-SEC-S6-002, SRS-SEC-S6-003, SRS-SEC-S6-004, SRS-SEC-S6-005, SRS-SEC-S6-007, SRS-SEC-S6-009, SRS-SEC-S6-014, SRS-SEC-S7-022, SRS-SEC-S7-027, SRS-SEC-S7-028, SRS-SEC-S7-029, SRS-SEC-S7-033, SRS-SEC-S8-003, SRS-SEC-S8-008, SRS-SEC-S8-010, SRS-SEC-S8-011, SRS-SEC-S8-012, SRS-SEC-S8-017, SRS-SEC-S8-019, SRS-SEC-S8-020, SRS-SEC-S8-021, SRS-SEC-S8-022, SRS-SEC-S8-023, SRS-SEC-S8-024, SRS-SEC-S8-026, SRS-SEC-S8-027, SRS-SEC-S8-032, SRS-SEC-S9-001, SRS-SEC-S9-002, SRS-SEC-S9-003, SRS-SEC-S9-004, SRS-SEC-S9-005, SRS-SEC-S9-006, SRS-SEC-S9-007, SRS-SEC-S9-009, SRS-SEC-S9-010, SRS-SEC-S9-011, SRS-SEC-S9-017, SRS-SEC-S9-019, SRS-SEC-S9-020, SRS-SEC-S9-021, SRS-SEC-S9-023, SRS-SEC-S9-024, SRS-SEC-S9-025, SRS-SEC-S9-026, SRS-SEC-S9-036, SRS-SEC-S9-037, SRS-SEC-S9-038, SRS-SEC-S10-004, SRS-SEC-S11-001, SRS-SEC-S12-001, SRS-SEC-S12-002, SRS-SEC-S12-003, SRS-SEC-S12-004, SRS-SEC-S12-005, SRS-SEC-S12-006, SRS-SEC-S12-007, SRS-SEC-S12-008, SRS-SEC-S12-009, SRS-SEC-S12-010, SRS-SEC-S12-011, SRS-SEC-S12-012, SRS-SEC-S12-013, SRS-SEC-S12-014, SRS-SEC-S12-015, SRS-SEC-S12-016, SRS-SEC-S12-017, SRS-SEC-S12-018, SRS-SEC-S12-019, SRS-SEC-S12-021, SRS-SEC-S12-023, SRS-SEC-S13-003, SRS-SEC-S13-005, SRS-SEC-S13-006, SRS-SEC-S13-016, SRS-SEC-S14-001, SRS-SEC-S14-008, SRS-SEC-S14-009, SRS-SEC-S14-010, SRS-SEC-S14-011, SRS-SEC-S14-012, SRS-SEC-S14-014, SRS-SEC-S14-015, SRS-SEC-S14-016, SRS-SEC-S14-017, SRS-SEC-S14-018, SRS-SEC-S14-019, SRS-SEC-S14-020, SRS-SEC-S14-021, SRS-SEC-S14-022, SRS-SEC-S14-023, SRS-SEC-S14-024, SRS-SEC-S14-025, SRS-SEC-S14-026, SRS-SEC-S14-028, SRS-SEC-S15-001, SRS-SEC-S15-003, SRS-SEC-S15-004, SRS-SEC-S15-006, SRS-SEC-S15-007, SRS-SEC-S15-008, SRS-SEC-S15-009, SRS-SEC-S15-012, SRS-SEC-S15-013, SRS-SEC-S16-005, SRS-SEC-S17-006, SRS-SEC-S17-008, SRS-SEC-S17-009, SRS-SEC-S17-014, SRS-SEC-S18-005, SRS-SEC-S19-003, SRS-SEC-S19-004, SRS-SEC-S19-006, SRS-SEC-S19-008, SRS-SEC-NF-001, SRS-SEC-NF-002, SRS-SEC-NF-003 |
| HW — Digital Controller Specs (client hardware spec) | 179 | SRS-SEC-HW-001, SRS-SEC-HW-002, SRS-SEC-HW-003, SRS-SEC-HW-004, SRS-SEC-HW-005, SRS-SEC-HW-006, SRS-SEC-HW-007, SRS-SEC-HW-008, SRS-SEC-HW-009, SRS-SEC-HW-010, SRS-SEC-HW-012, SRS-SEC-HW-013, SRS-SEC-HW-014, SRS-SEC-HW-015, SRS-SEC-HW-016, SRS-SEC-HW-017, SRS-SEC-HW-018, SRS-SEC-HW-019, SRS-SEC-HW-020, SRS-SEC-HW-021, SRS-SEC-HW-022, SRS-SEC-HW-023, SRS-SEC-HW-024, SRS-SEC-HW-025, SRS-SEC-HW-026, SRS-SEC-SW-001, SRS-SEC-SW-005, SRS-SEC-SW-006, SRS-SEC-CM-003, SRS-SEC-CM-006, SRS-SEC-CM-007, SRS-SEC-CM-008, SRS-SEC-UI-002, SRS-SEC-UI-004, SRS-SEC-S1-001, SRS-SEC-S1-011, SRS-SEC-S1-015, SRS-SEC-S1-017, SRS-SEC-S1-019, SRS-SEC-S1-020, SRS-SEC-S2-006, SRS-SEC-S2-018, SRS-SEC-S3-004, SRS-SEC-S3-007, SRS-SEC-S3-009, SRS-SEC-S4-006, SRS-SEC-S4-007, SRS-SEC-S4-011, SRS-SEC-S4-012, SRS-SEC-S4-013, SRS-SEC-S4-014, SRS-SEC-S4-015, SRS-SEC-S4-017, SRS-SEC-S5-001, SRS-SEC-S5-002, SRS-SEC-S5-012, SRS-SEC-S5-013, SRS-SEC-S5-014, SRS-SEC-S5-016, SRS-SEC-S5-022, SRS-SEC-S5-024, SRS-SEC-S5-025, SRS-SEC-S5-026, SRS-SEC-S6-010, SRS-SEC-S6-011, SRS-SEC-S6-013, SRS-SEC-S7-001, SRS-SEC-S7-002, SRS-SEC-S7-003, SRS-SEC-S7-004, SRS-SEC-S7-005, SRS-SEC-S7-008, SRS-SEC-S7-013, SRS-SEC-S7-014, SRS-SEC-S7-015, SRS-SEC-S7-016, SRS-SEC-S7-017, SRS-SEC-S7-021, SRS-SEC-S7-022, SRS-SEC-S7-027, SRS-SEC-S7-028, SRS-SEC-S7-031, SRS-SEC-S7-033, SRS-SEC-S7-034, SRS-SEC-S7-035, SRS-SEC-S8-001, SRS-SEC-S8-002, SRS-SEC-S8-004, SRS-SEC-S8-007, SRS-SEC-S8-009, SRS-SEC-S8-010, SRS-SEC-S8-018, SRS-SEC-S8-028, SRS-SEC-S8-029, SRS-SEC-S8-030, SRS-SEC-S8-031, SRS-SEC-S8-033, SRS-SEC-S8-034, SRS-SEC-S8-038, SRS-SEC-S8-039, SRS-SEC-S9-001, SRS-SEC-S9-002, SRS-SEC-S9-003, SRS-SEC-S9-004, SRS-SEC-S9-008, SRS-SEC-S9-009, SRS-SEC-S9-010, SRS-SEC-S9-012, SRS-SEC-S9-013, SRS-SEC-S9-030, SRS-SEC-S9-040, SRS-SEC-S9-041, SRS-SEC-S10-001, SRS-SEC-S10-002, SRS-SEC-S11-001, SRS-SEC-S11-002, SRS-SEC-S11-008, SRS-SEC-S11-009, SRS-SEC-S11-011, SRS-SEC-S12-023, SRS-SEC-S13-001, SRS-SEC-S13-002, SRS-SEC-S13-016, SRS-SEC-S14-001, SRS-SEC-S14-002, SRS-SEC-S14-003, SRS-SEC-S14-004, SRS-SEC-S14-005, SRS-SEC-S14-006, SRS-SEC-S14-007, SRS-SEC-S14-010, SRS-SEC-S14-013, SRS-SEC-S14-026, SRS-SEC-S15-001, SRS-SEC-S15-002, SRS-SEC-S15-005, SRS-SEC-S15-011, SRS-SEC-S15-012, SRS-SEC-S15-020, SRS-SEC-S16-002, SRS-SEC-S16-007, SRS-SEC-S16-009, SRS-SEC-S16-010, SRS-SEC-S16-015, SRS-SEC-S17-001, SRS-SEC-S17-002, SRS-SEC-S17-003, SRS-SEC-S17-008, SRS-SEC-S17-013, SRS-SEC-S17-014, SRS-SEC-S18-001, SRS-SEC-S18-002, SRS-SEC-S18-004, SRS-SEC-S18-005, SRS-SEC-S18-006, SRS-SEC-S18-007, SRS-SEC-S18-011, SRS-SEC-S18-012, SRS-SEC-S18-013, SRS-SEC-S18-015, SRS-SEC-S19-001, SRS-SEC-S19-005, SRS-SEC-S19-006, SRS-SEC-S19-015, SRS-SEC-NF-007, SRS-SEC-NF-008, SRS-SEC-NF-010, SRS-SEC-NF-012, SRS-SEC-NF-016, SRS-SEC-NF-028, SRS-SEC-NF-029, SRS-SEC-NF-030, SRS-SEC-NF-032, SRS-SEC-NF-035, SRS-SEC-NF-038, SRS-SEC-NF-039, SRS-SEC-NF-040, SRS-SEC-NF-041, SRS-SEC-NF-042 |
| Derived from other requirements | 132 | SRS-SEC-SW-003, SRS-SEC-CM-004, SRS-SEC-UI-001, SRS-SEC-S1-003, SRS-SEC-S1-012, SRS-SEC-S1-013, SRS-SEC-S1-014, SRS-SEC-S1-016, SRS-SEC-S1-018, SRS-SEC-S1-021, SRS-SEC-S2-001, SRS-SEC-S2-002, SRS-SEC-S2-004, SRS-SEC-S2-005, SRS-SEC-S2-006, SRS-SEC-S2-007, SRS-SEC-S2-011, SRS-SEC-S2-012, SRS-SEC-S2-014, SRS-SEC-S2-017, SRS-SEC-S3-005, SRS-SEC-S3-006, SRS-SEC-S3-007, SRS-SEC-S4-020, SRS-SEC-S4-021, SRS-SEC-S4-022, SRS-SEC-S4-025, SRS-SEC-S5-006, SRS-SEC-S5-011, SRS-SEC-S5-013, SRS-SEC-S5-017, SRS-SEC-S5-030, SRS-SEC-S5-032, SRS-SEC-S5-033, SRS-SEC-S5-034, SRS-SEC-S5-037, SRS-SEC-S6-006, SRS-SEC-S6-008, SRS-SEC-S7-018, SRS-SEC-S7-019, SRS-SEC-S7-032, SRS-SEC-S8-013, SRS-SEC-S8-017, SRS-SEC-S8-025, SRS-SEC-S9-014, SRS-SEC-S9-015, SRS-SEC-S9-016, SRS-SEC-S9-018, SRS-SEC-S9-022, SRS-SEC-S9-024, SRS-SEC-S9-027, SRS-SEC-S9-028, SRS-SEC-S9-029, SRS-SEC-S9-032, SRS-SEC-S9-035, SRS-SEC-S9-037, SRS-SEC-S10-003, SRS-SEC-S10-004, SRS-SEC-S10-005, SRS-SEC-S10-006, SRS-SEC-S10-007, SRS-SEC-S10-008, SRS-SEC-S11-002, SRS-SEC-S11-003, SRS-SEC-S11-004, SRS-SEC-S11-005, SRS-SEC-S11-006, SRS-SEC-S11-007, SRS-SEC-S12-015, SRS-SEC-S12-020, SRS-SEC-S13-004, SRS-SEC-S13-007, SRS-SEC-S13-008, SRS-SEC-S13-009, SRS-SEC-S13-010, SRS-SEC-S13-011, SRS-SEC-S13-012, SRS-SEC-S13-013, SRS-SEC-S13-014, SRS-SEC-S14-012, SRS-SEC-S14-025, SRS-SEC-S14-027, SRS-SEC-S15-002, SRS-SEC-S15-010, SRS-SEC-S15-011, SRS-SEC-S15-014, SRS-SEC-S15-015, SRS-SEC-S15-016, SRS-SEC-S15-017, SRS-SEC-S15-018, SRS-SEC-S16-003, SRS-SEC-S16-004, SRS-SEC-S16-005, SRS-SEC-S16-006, SRS-SEC-S16-008, SRS-SEC-S16-011, SRS-SEC-S16-012, SRS-SEC-S16-013, SRS-SEC-S16-014, SRS-SEC-S17-004, SRS-SEC-S17-010, SRS-SEC-S17-011, SRS-SEC-S18-003, SRS-SEC-S18-006, SRS-SEC-S18-007, SRS-SEC-S18-010, SRS-SEC-S18-011, SRS-SEC-S18-014, SRS-SEC-S19-002, SRS-SEC-S19-005, SRS-SEC-S19-007, SRS-SEC-S19-009, SRS-SEC-S19-010, SRS-SEC-S19-011, SRS-SEC-S19-012, SRS-SEC-NF-004, SRS-SEC-NF-006, SRS-SEC-NF-007, SRS-SEC-NF-008, SRS-SEC-NF-017, SRS-SEC-NF-018, SRS-SEC-NF-019, SRS-SEC-NF-020, SRS-SEC-NF-021, SRS-SEC-NF-022, SRS-SEC-NF-024, SRS-SEC-NF-025, SRS-SEC-NF-026, SRS-SEC-NF-031, SRS-SEC-NF-032, SRS-SEC-NF-052, SRS-SEC-NF-053 |
| ICD — ME-ICD-001 (interface message) | 60 | SRS-SEC-S1-017, SRS-SEC-S1-018, SRS-SEC-S3-007, SRS-SEC-S4-001, SRS-SEC-S4-002, SRS-SEC-S4-006, SRS-SEC-S4-007, SRS-SEC-S4-010, SRS-SEC-S4-011, SRS-SEC-S4-012, SRS-SEC-S4-013, SRS-SEC-S4-014, SRS-SEC-S4-015, SRS-SEC-S4-018, SRS-SEC-S4-020, SRS-SEC-S4-021, SRS-SEC-S4-023, SRS-SEC-S4-024, SRS-SEC-S4-025, SRS-SEC-S7-023, SRS-SEC-S7-024, SRS-SEC-S7-025, SRS-SEC-S11-005, SRS-SEC-S12-001, SRS-SEC-S12-008, SRS-SEC-S12-010, SRS-SEC-S12-011, SRS-SEC-S12-016, SRS-SEC-S12-017, SRS-SEC-S12-018, SRS-SEC-S12-019, SRS-SEC-S12-020, SRS-SEC-S12-022, SRS-SEC-S13-003, SRS-SEC-S13-004, SRS-SEC-S13-011, SRS-SEC-S13-012, SRS-SEC-S14-017, SRS-SEC-S14-018, SRS-SEC-S15-013, SRS-SEC-S15-016, SRS-SEC-S15-017, SRS-SEC-S15-018, SRS-SEC-S16-001, SRS-SEC-S16-003, SRS-SEC-S16-005, SRS-SEC-S16-006, SRS-SEC-S16-007, SRS-SEC-S16-008, SRS-SEC-S16-011, SRS-SEC-S16-012, SRS-SEC-S16-013, SRS-SEC-S17-008, SRS-SEC-S19-001, SRS-SEC-S19-004, SRS-SEC-S19-006, SRS-SEC-S19-008, SRS-SEC-S19-010, SRS-SEC-S19-011, SRS-SEC-NF-032 |
| UNSOURCED — open issue | 41 | SRS-SEC-CM-012, SRS-SEC-UI-005, SRS-SEC-S1-024, SRS-SEC-S1-025, SRS-SEC-S1-026, SRS-SEC-S2-019, SRS-SEC-S2-020, SRS-SEC-S3-010, SRS-SEC-S3-011, SRS-SEC-S3-012, SRS-SEC-S4-026, SRS-SEC-S7-020, SRS-SEC-S8-040, SRS-SEC-S9-033, SRS-SEC-S10-010, SRS-SEC-S10-011, SRS-SEC-S11-010, SRS-SEC-S12-025, SRS-SEC-S13-015, SRS-SEC-S13-017, SRS-SEC-S14-029, SRS-SEC-S14-030, SRS-SEC-S15-019, SRS-SEC-S16-016, SRS-SEC-S16-017, SRS-SEC-S16-018, SRS-SEC-S17-015, SRS-SEC-S19-013, SRS-SEC-S19-014, SRS-SEC-NF-013, SRS-SEC-NF-014, SRS-SEC-NF-015, SRS-SEC-NF-023, SRS-SEC-NF-024, SRS-SEC-NF-033, SRS-SEC-NF-034, SRS-SEC-NF-044, SRS-SEC-NF-045, SRS-SEC-NF-046, SRS-SEC-NF-047, SRS-SEC-NF-048 |
| NEW — ME | 38 | SRS-SEC-HW-026, SRS-SEC-SW-004, SRS-SEC-CM-005, SRS-SEC-CM-009, SRS-SEC-CM-010, SRS-SEC-CM-011, SRS-SEC-S3-001, SRS-SEC-S3-002, SRS-SEC-S3-003, SRS-SEC-S3-005, SRS-SEC-S3-006, SRS-SEC-S3-008, SRS-SEC-S5-006, SRS-SEC-S6-006, SRS-SEC-S7-031, SRS-SEC-S8-013, SRS-SEC-S8-022, SRS-SEC-S10-009, SRS-SEC-S11-003, SRS-SEC-S11-006, SRS-SEC-S11-007, SRS-SEC-S12-020, SRS-SEC-S12-021, SRS-SEC-S12-022, SRS-SEC-S12-024, SRS-SEC-S13-013, SRS-SEC-S15-015, SRS-SEC-S15-018, SRS-SEC-S16-011, SRS-SEC-S16-014, SRS-SEC-S18-008, SRS-SEC-S18-009, SRS-SEC-S18-013, SRS-SEC-S18-014, SRS-SEC-NF-005, SRS-SEC-NF-013, SRS-SEC-NF-014, SRS-SEC-NF-027 |
| BM — BM_Manual Ch.12 (BTS-600) | 37 | SRS-SEC-UI-002, SRS-SEC-UI-003, SRS-SEC-S2-008, SRS-SEC-S2-010, SRS-SEC-S4-015, SRS-SEC-S5-001, SRS-SEC-S5-002, SRS-SEC-S5-003, SRS-SEC-S5-004, SRS-SEC-S5-005, SRS-SEC-S5-007, SRS-SEC-S5-008, SRS-SEC-S6-001, SRS-SEC-S6-007, SRS-SEC-S7-006, SRS-SEC-S7-007, SRS-SEC-S7-023, SRS-SEC-S7-024, SRS-SEC-S7-025, SRS-SEC-S7-026, SRS-SEC-S7-030, SRS-SEC-S7-033, SRS-SEC-S8-024, SRS-SEC-S9-023, SRS-SEC-S9-030, SRS-SEC-S9-034, SRS-SEC-S12-018, SRS-SEC-S12-023, SRS-SEC-S13-005, SRS-SEC-S13-016, SRS-SEC-S17-003, SRS-SEC-S17-005, SRS-SEC-S17-006, SRS-SEC-S17-007, SRS-SEC-NF-003, SRS-SEC-NF-009, SRS-SEC-NF-017 |
| Safety analysis (derived) | 33 | SRS-SEC-S1-003, SRS-SEC-S1-013, SRS-SEC-S2-007, SRS-SEC-S2-012, SRS-SEC-S4-025, SRS-SEC-S5-017, SRS-SEC-S5-033, SRS-SEC-S7-018, SRS-SEC-S7-019, SRS-SEC-S9-014, SRS-SEC-S9-015, SRS-SEC-S9-016, SRS-SEC-S9-022, SRS-SEC-S9-027, SRS-SEC-S9-028, SRS-SEC-S9-029, SRS-SEC-S9-035, SRS-SEC-S10-003, SRS-SEC-S10-006, SRS-SEC-S10-007, SRS-SEC-S13-004, SRS-SEC-S13-010, SRS-SEC-S13-012, SRS-SEC-S15-014, SRS-SEC-S15-017, SRS-SEC-S16-003, SRS-SEC-S16-012, SRS-SEC-S17-010, SRS-SEC-S17-011, SRS-SEC-S19-002, SRS-SEC-S19-009, SRS-SEC-NF-006, SRS-SEC-NF-018 |
| LSRS — legacy combined SRS | 32 | SRS-SEC-HW-003, SRS-SEC-HW-010, SRS-SEC-HW-013, SRS-SEC-HW-014, SRS-SEC-S1-022, SRS-SEC-S4-018, SRS-SEC-S6-010, SRS-SEC-S6-011, SRS-SEC-S6-012, SRS-SEC-S7-009, SRS-SEC-S7-010, SRS-SEC-S7-011, SRS-SEC-S7-012, SRS-SEC-S7-016, SRS-SEC-S7-021, SRS-SEC-S7-033, SRS-SEC-S8-004, SRS-SEC-S8-005, SRS-SEC-S8-006, SRS-SEC-S8-014, SRS-SEC-S8-015, SRS-SEC-S8-016, SRS-SEC-S8-033, SRS-SEC-S8-035, SRS-SEC-S8-036, SRS-SEC-S8-037, SRS-SEC-S8-039, SRS-SEC-S9-038, SRS-SEC-S9-039, SRS-SEC-S17-009, SRS-SEC-S17-012, SRS-SEC-S17-014 |
| Open-issue register | 11 | SRS-SEC-CM-009, SRS-SEC-S1-015, SRS-SEC-S2-017, SRS-SEC-S9-022, SRS-SEC-S9-031, SRS-SEC-S9-034, SRS-SEC-S13-009, SRS-SEC-S13-011, SRS-SEC-S15-016, SRS-SEC-S17-013, SRS-SEC-NF-011 |
| Design-constraint register | 10 | SRS-SEC-SW-006, SRS-SEC-S2-001, SRS-SEC-S2-013, SRS-SEC-S2-018, SRS-SEC-NF-035, SRS-SEC-NF-036, SRS-SEC-NF-037, SRS-SEC-NF-038, SRS-SEC-NF-041, SRS-SEC-NF-042 |
| Assumption register | 9 | SRS-SEC-HW-003, SRS-SEC-SW-004, SRS-SEC-UI-004, SRS-SEC-S1-020, SRS-SEC-S4-005, SRS-SEC-S9-016, SRS-SEC-S9-017, SRS-SEC-NF-022, SRS-SEC-NF-027 |
| Reserved decision D-0x | 9 | SRS-SEC-CM-005, SRS-SEC-S4-001, SRS-SEC-S4-002, SRS-SEC-S4-004, SRS-SEC-S4-022, SRS-SEC-S4-023, SRS-SEC-S5-033, SRS-SEC-S5-034, SRS-SEC-S12-010 |
| Project brief | 8 | SRS-SEC-SW-001, SRS-SEC-SW-003, SRS-SEC-S4-001, SRS-SEC-S4-002, SRS-SEC-S5-032, SRS-SEC-S9-015, SRS-SEC-S16-001, SRS-SEC-S19-010 |
| Team engineering policy | 5 | SRS-SEC-NF-016, SRS-SEC-NF-043, SRS-SEC-NF-049, SRS-SEC-NF-050, SRS-SEC-NF-051 |
| Client remark (HW remark columns, 10-Feb-2026) | 2 | SRS-SEC-S8-038, SRS-SEC-S9-040 |
| Conflict register | 2 | SRS-SEC-S16-001, SRS-SEC-NF-012 |
| ME-ICD-001 | 1 | SRS-SEC-SW-002 |
| Note — condition has no HW error code (conflict C-19) | 1 | SRS-SEC-S8-036 |
| PSPE — prior partition proposal (prior art only) | 1 | SRS-SEC-S4-002 |

### A1.3 Requirements carrying an unsourced value

Each records a value or policy that no source supplies. All are legitimate as
placeholders; none can be verified until resolved (SRS-SEC-NF-052).

| ID | Area | Records | Open issue / conflict |
|---|---|---|---|
| SRS-SEC-CM-012 | CM | <TBD-S08> | — |
| SRS-SEC-UI-005 | UI | <TBD-S09> | #21 |
| SRS-SEC-S1-024 | S1 | <TBD-S10> | #12 |
| SRS-SEC-S1-025 | S1 | <TBD-S11> | #12 |
| SRS-SEC-S1-026 | S1 | <TBD-S12> | — |
| SRS-SEC-S2-019 | S2 | <TBD-S13> | #13 |
| SRS-SEC-S2-020 | S2 | <TBD-S14> | — |
| SRS-SEC-S3-010 | S3 | <TBD-S15> | #22 |
| SRS-SEC-S3-011 | S3 | <TBD-S16> | #21 |
| SRS-SEC-S3-012 | S3 | <TBD-S17> | #14 |
| SRS-SEC-S4-026 | S4 | <TBD-S18> | — |
| SRS-SEC-S7-020 | S7 | <TBD-S24> | #21 |
| SRS-SEC-S8-040 | S8 | <TBD-S32> | — |
| SRS-SEC-S9-033 | S9 | <TBD-S02> | #8 |
| SRS-SEC-S10-010 | S10 | <TBD-S37> | #8 |
| SRS-SEC-S10-011 | S10 | <TBD-S38> | — |
| SRS-SEC-S11-010 | S11 | <TBD-S39> | #12 |
| SRS-SEC-S12-025 | S12 | <TBD-S43> | — |
| SRS-SEC-S13-015 | S13 | <TBD-S44> | #17 |
| SRS-SEC-S13-017 | S13 | <TBD-S46> | #17 |
| SRS-SEC-S14-029 | S14 | <TBD-S47> | #15 |
| SRS-SEC-S14-030 | S14 | <TBD-S48> | — |
| SRS-SEC-S15-019 | S15 | <TBD-S49> | #22 |
| SRS-SEC-S16-016 | S16 | <TBD-S52> | #9 |
| SRS-SEC-S16-017 | S16 | <TBD-S53> | #9 |
| SRS-SEC-S16-018 | S16 | <TBD-S54> | #9 |
| SRS-SEC-S17-015 | S17 | <TBD-S56> | #21 |
| SRS-SEC-S19-013 | S19 | <TBD-S58> | #23 |
| SRS-SEC-S19-014 | S19 | <TBD-S59> | #23 |
| SRS-SEC-NF-013 | NF | <TBD-S61> | — |
| SRS-SEC-NF-014 | NF | <TBD-S71> | — |
| SRS-SEC-NF-015 | NF | <TBD-S62> | — |
| SRS-SEC-NF-023 | NF | <TBD-S63> | — |
| SRS-SEC-NF-024 | NF | <TBD-S64> | — |
| SRS-SEC-NF-033 | NF | <TBD-S65> | — |
| SRS-SEC-NF-034 | NF | <TBD-S66> | — |
| SRS-SEC-NF-044 | NF | <TBD-S67> | #26 |
| SRS-SEC-NF-045 | NF | <TBD-S68> | — |
| SRS-SEC-NF-046 | NF | <TBD-S69> | — |
| SRS-SEC-NF-047 | NF | <TBD-S01> | — |
| SRS-SEC-NF-048 | NF | <TBD-S70> | — |

**41 requirements carry an unsourced value.**

### A1.4 TBD tag → requirement

Forward index into the parent's §8.3 register. Answering one tag closes every
requirement listed against it.

| Tag | Requirements |
|---|---|
| `<TBD-S01>` | SRS-SEC-NF-047 |
| `<TBD-S02>` | SRS-SEC-S9-033, SRS-SEC-S17-010 |
| `<TBD-S03>` | SRS-SEC-HW-004 |
| `<TBD-S04>` | SRS-SEC-HW-005 |
| `<TBD-S05>` | SRS-SEC-HW-012, SRS-SEC-S17-014 |
| `<TBD-S06>` | SRS-SEC-HW-020 |
| `<TBD-S07>` | SRS-SEC-HW-021 |
| `<TBD-S08>` | SRS-SEC-CM-012 |
| `<TBD-S09>` | SRS-SEC-UI-005 |
| `<TBD-S10>` | SRS-SEC-S1-024 |
| `<TBD-S11>` | SRS-SEC-S1-025 |
| `<TBD-S12>` | SRS-SEC-S1-026 |
| `<TBD-S13>` | SRS-SEC-S2-019 |
| `<TBD-S14>` | SRS-SEC-S2-020 |
| `<TBD-S15>` | SRS-SEC-S3-010 |
| `<TBD-S16>` | SRS-SEC-S3-011 |
| `<TBD-S17>` | SRS-SEC-S3-012 |
| `<TBD-S18>` | SRS-SEC-S4-026 |
| `<TBD-S19>` | SRS-SEC-S5-006, SRS-SEC-S6-006, SRS-SEC-S7-031 |
| `<TBD-S20>` | SRS-SEC-S5-031 |
| `<TBD-S21>` | SRS-SEC-S6-013 |
| `<TBD-S22>` | SRS-SEC-S7-008 |
| `<TBD-S23>` | SRS-SEC-S7-016 |
| `<TBD-S24>` | SRS-SEC-S7-020 |
| `<TBD-S25>` | SRS-SEC-S7-033 |
| `<TBD-S26>` | SRS-SEC-S7-034 |
| `<TBD-S27>` | SRS-SEC-S7-035 |
| `<TBD-S28>` | SRS-SEC-S8-022, SRS-SEC-S8-022 |
| `<TBD-S29>` | SRS-SEC-S8-037 |
| `<TBD-S30>` | SRS-SEC-S8-038 |
| `<TBD-S31>` | SRS-SEC-S8-039 |
| `<TBD-S32>` | SRS-SEC-S8-040 |
| `<TBD-S33>` | SRS-SEC-S9-022 |
| `<TBD-S34>` | SRS-SEC-S9-031, SRS-SEC-NF-011 |
| `<TBD-S35>` | SRS-SEC-S9-040 |
| `<TBD-S36>` | SRS-SEC-S9-041 |
| `<TBD-S37>` | SRS-SEC-S10-010 |
| `<TBD-S38>` | SRS-SEC-S10-011 |
| `<TBD-S39>` | SRS-SEC-S11-010 |
| `<TBD-S40>` | SRS-SEC-S11-011 |
| `<TBD-S41>` | SRS-SEC-S12-023, SRS-SEC-S12-024, SRS-SEC-S12-025, SRS-SEC-NF-012 |
| `<TBD-S42>` | SRS-SEC-S12-024, SRS-SEC-S12-025 |
| `<TBD-S43>` | SRS-SEC-S12-025 |
| `<TBD-S44>` | SRS-SEC-S13-015 |
| `<TBD-S45>` | SRS-SEC-S13-016 |
| `<TBD-S46>` | SRS-SEC-S13-017 |
| `<TBD-S47>` | SRS-SEC-S14-029 |
| `<TBD-S48>` | SRS-SEC-S14-030 |
| `<TBD-S49>` | SRS-SEC-S15-019 |
| `<TBD-S50>` | SRS-SEC-S15-020 |
| `<TBD-S51>` | SRS-SEC-S16-015 |
| `<TBD-S52>` | SRS-SEC-S16-016 |
| `<TBD-S53>` | SRS-SEC-S16-017 |
| `<TBD-S54>` | SRS-SEC-S16-018 |
| `<TBD-S55>` | SRS-SEC-S17-013 |
| `<TBD-S56>` | SRS-SEC-S17-015 |
| `<TBD-S57>` | SRS-SEC-S18-015 |
| `<TBD-S58>` | SRS-SEC-S19-013 |
| `<TBD-S59>` | SRS-SEC-S19-014 |
| `<TBD-S60>` | SRS-SEC-S19-015 |
| `<TBD-S61>` | SRS-SEC-NF-013 |
| `<TBD-S62>` | SRS-SEC-NF-015 |
| `<TBD-S63>` | SRS-SEC-NF-023 |
| `<TBD-S64>` | SRS-SEC-NF-024 |
| `<TBD-S65>` | SRS-SEC-NF-033 |
| `<TBD-S66>` | SRS-SEC-NF-034 |
| `<TBD-S67>` | SRS-SEC-NF-044 |
| `<TBD-S68>` | SRS-SEC-NF-045 |
| `<TBD-S69>` | SRS-SEC-NF-046 |
| `<TBD-S70>` | SRS-SEC-NF-048 |
| `<TBD-S71>` | SRS-SEC-NF-014 |
| `<TBD-S72>` | SRS-SEC-NF-016 |

---

## A2. Hardware-in-the-Loop Verification Sheet

Every requirement below is marked **⚠ hardware-critical** in the parent: it governs
hardware register access, a real-time control loop, interrupt timing, a safety
interlock, boot behaviour or a power sequence. Per SRS-SEC-NF-049 and SRS-SEC-NF-050,
none may be closed by simulation or code review alone, and each requires explicit
engineering approval before implementation.

Enter a test-case identifier and an outcome against each row. `Blocked by` is
pre-filled where the requirement carries an unresolved `<TBD-Snn>`; those rows cannot
be tested until the tag is answered.

**195 of 530 requirements (37%) are hardware-critical.**


### S1 — S1 Startup, init, self-test, safe default outputs

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S1-001 | place the analog reference output at zero before enabling the transistor-bank drive, on every appli… | — |  |  |
| SRS-SEC-S1-002 | place the charge and discharge contactor relays in their default de-energised positions before enab… | — |  |  |
| SRS-SEC-S1-003 | complete SRS-SEC-S1-001 and SRS-SEC-S1-002 before any other initialization step that could affect t… | — |  |  |
| SRS-SEC-S1-009 | initialize its watchdog during initialization and shall raise a distinct fault if that initializati… | — |  |  |
| SRS-SEC-S1-013 | not permit charge or discharge regulation while its calibration data is failing its integrity check | — |  |  |
| SRS-SEC-S1-021 | not accept any regulation command until initialization has completed successfully | — |  |  |
| SRS-SEC-S1-022 | measure the battery voltage and verify its polarity before energising the transistor bank for the f… | — |  |  |

### S2 — S2 Operating mode / state machine

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S2-006 | enter Fault autonomously on detection of any condition listed in §4.9, without waiting for a comman… | — |  |  |
| SRS-SEC-S2-007 | be in the safe state whenever it is in Initializing, Ready, Paused, Interrupted, Fault or Firmware… | — |  |  |
| SRS-SEC-S2-012 | reject a command to enter Calibration while Regulating, and shall report the rejection | — |  |  |
| SRS-SEC-S2-017 | not leave the Fault state while any fault that latches under §4.13 remains uncleared | — |  |  |

### S4 — S4 Command & control-data reception, validation, rejection

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S4-006 | reject a commanded setpoint that exceeds the configured maximum charge current, maximum discharge c… | — |  |  |
| SRS-SEC-S4-012 | accept a stop command that causes it to stop regulating and enter the safe state | — |  |  |
| SRS-SEC-S4-013 | accept an interrupt command that causes it to stop regulating and enter the safe state while retain… | — |  |  |
| SRS-SEC-S4-025 | enter the safe state and report a fault when control data it has requested has not arrived after th… | — |  |  |

### S5 — S5 Charge regulation

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S5-001 | regulate charge current to a commanded constant-current setpoint | — |  |  |
| SRS-SEC-S5-002 | regulate battery voltage to a commanded constant-voltage setpoint during charge | — |  |  |
| SRS-SEC-S5-003 | regulate charge power to a commanded constant-power setpoint | — |  |  |
| SRS-SEC-S5-004 | regulate charge in a combined constant-current-then-constant-voltage mode, given both a current set… | — |  |  |
| SRS-SEC-S5-005 | transfer from constant-current to constant-voltage regulation, in the combined mode, when the measu… | — |  |  |
| SRS-SEC-S5-006 | effect the transfer of SRS-SEC-S5-005 without a discontinuity in the analog reference output that w… | `<TBD-S19>` |  |  |
| SRS-SEC-S5-007 | limit the charge current to the commanded current setpoint while regulating in constant-voltage mode | — |  |  |
| SRS-SEC-S5-008 | regulate charge in a combined constant-current-then-constant-power mode, given both a current setpo… | — |  |  |
| SRS-SEC-S5-009 | regulate charge in a combined constant-power-then-constant-voltage mode, given both a power setpoin… | — |  |  |
| SRS-SEC-S5-010 | regulate charge in a combined constant-current, constant-power then constant-voltage mode, given al… | — |  |  |
| SRS-SEC-S5-011 | select, in any combined mode, the most restrictive of the active regulation loops at every control… | — |  |  |
| SRS-SEC-S5-012 | energise the charge contactor before commanding a non-zero charge reference output | — |  |  |
| SRS-SEC-S5-013 | de-energise the charge contactor after the reference output has returned to zero when charge regula… | — |  |  |
| SRS-SEC-S5-014 | implement each regulation loop as a proportional-integral-derivative controller acting on the analo… | — |  |  |
| SRS-SEC-S5-017 | not accept a change to controller parameters while it is regulating | — |  |  |
| SRS-SEC-S5-018 | bound each controller's integral term so that it cannot accumulate beyond the range of the analog r… | — |  |  |
| SRS-SEC-S5-019 | bound each controller's output to the configured analog reference output range | — |  |  |
| SRS-SEC-S5-020 | reset every regulation loop, including its integral term, when regulation stops | — |  |  |
| SRS-SEC-S5-021 | reset the measurement filters associated with charge when charge regulation begins | — |  |  |
| SRS-SEC-S5-022 | detect that a regulation loop's output has been driven outside its permitted range and shall raise… | — |  |  |
| SRS-SEC-S5-025 | raise a fault when a commanded current setpoint has not been reached within a configurable time, de… | — |  |  |
| SRS-SEC-S5-026 | raise a fault when a commanded voltage setpoint has not been reached within a configurable time | — |  |  |
| SRS-SEC-S5-027 | raise a fault when a commanded power setpoint has not been reached within a configurable time | — |  |  |
| SRS-SEC-S5-032 | stop regulating and enter the safe state when the Primary Board commands it to stop, without waitin… | — |  |  |
| SRS-SEC-S5-033 | enforce every safety limit supplied to it with a unit of control data, independently of any evaluat… | — |  |  |

### S6 — S6 Discharge regulation

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S6-001 | regulate discharge current to a commanded constant-current setpoint | — |  |  |
| SRS-SEC-S6-002 | regulate battery voltage to a commanded constant-voltage setpoint during discharge | — |  |  |
| SRS-SEC-S6-003 | regulate discharge power to a commanded constant-power setpoint | — |  |  |
| SRS-SEC-S6-004 | regulate discharge in a combined constant-current-then-constant-voltage mode | — |  |  |
| SRS-SEC-S6-005 | regulate discharge in the combined constant-current-then-constant-power, constant-power-then-consta… | — |  |  |
| SRS-SEC-S6-006 | transfer between the active loops of a combined discharge mode without a discontinuity in the analo… | `<TBD-S19>` |  |  |
| SRS-SEC-S6-007 | stop discharge regulation and enter the safe state when the measured battery voltage falls to a com… | — |  |  |
| SRS-SEC-S6-008 | apply the requirements of §4.5.2, §4.5.3 and §4.5.4 to discharge regulation, using the controller p… | — |  |  |
| SRS-SEC-S6-009 | reset the measurement filters associated with discharge when discharge regulation begins | — |  |  |
| SRS-SEC-S6-010 | The Secondary Board shall, when configured for a **dual** transistor bank, produce a negative analo… | — |  |  |
| SRS-SEC-S6-011 | The Secondary Board shall, when configured for a **single** transistor bank, produce a positive ana… | — |  |  |
| SRS-SEC-S6-012 | The Secondary Board shall, when configured for a single transistor bank, energise the discharge con… | — |  |  |
| SRS-SEC-S6-014 | measure and report discharged capacity and discharged energy separately from charged capacity and c… | — |  |  |

### S7 — S7 Setpoint application, ramping, slew limiting, switch-over

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S7-001 | apply a configurable ramp when changing the current setpoint, default **10 ms** | — |  |  |
| SRS-SEC-S7-002 | apply a configurable ramp when changing the voltage setpoint, default **10 ms** | — |  |  |
| SRS-SEC-S7-003 | reach a commanded setpoint from zero within **10 ms** | — |  |  |
| SRS-SEC-S7-004 | return the analog reference output to zero within **10 ms** of being commanded to stop | — |  |  |
| SRS-SEC-S7-005 | traverse from 0.1 % to 100 % of maximum current within the range **0.1 ms to 10 ms** | — |  |  |
| SRS-SEC-S7-006 | execute a commanded ramp from a start value to an end value over a commanded duration, as a regulat… | — |  |  |
| SRS-SEC-S7-007 | not apply automatic current-range changing while executing a ramp | — |  |  |
| SRS-SEC-S7-009 | bring the analog reference output to zero before initiating any change between charge mode and disc… | — |  |  |
| SRS-SEC-S7-010 | switch the mode contactors only after the analog reference output has reached zero | — |  |  |
| SRS-SEC-S7-011 | observe a configurable switch-over delay after switching the mode contactors, before applying any n… | — |  |  |
| SRS-SEC-S7-012 | apply the reference output for the new mode only after the switch-over delay has elapsed | — |  |  |
| SRS-SEC-S7-013 | observe a configurable charge-contactor on time, default **100 ms**; off time, default **100 ms**;… | — |  |  |
| SRS-SEC-S7-014 | observe a configurable discharge-contactor on time, default **100 ms**; off time, default **100 ms*… | — |  |  |
| SRS-SEC-S7-015 | complete a transition between charge and discharge at maximum current within **10 ms** | — |  |  |
| SRS-SEC-S7-016 | record `<TBD-S23>` (the required charge↔discharge transition time and its relationship to the conta… | `<TBD-S23>` |  |  |
| SRS-SEC-S7-017 | The Secondary Board shall, when configured for a dual transistor bank, perform the charge↔discharge… | — |  |  |
| SRS-SEC-S7-018 | not energise the charge contactor and the discharge contactor simultaneously | — |  |  |
| SRS-SEC-S7-019 | enter the safe state and report a fault if it detects that both mode contactors are commanded or co… | — |  |  |
| SRS-SEC-S7-025 | not change the current range while regulating a single unit of control data | — |  |  |
| SRS-SEC-S7-026 | not apply automatic range changing while regulating constant power | — |  |  |
| SRS-SEC-S7-030 | complete a range transition within **50 ms** | — |  |  |
| SRS-SEC-S7-031 | not allow a range transition to disturb the regulated quantity beyond `<TBD-S19>` | `<TBD-S19>` |  |  |

### S8 — S8 Measurement acquisition & derived quantities

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S8-001 | acquire battery voltage, battery current, battery temperature, heatsink temperature, transistor-ban… | — |  |  |
| SRS-SEC-S8-002 | acquire the voltage at the battery power connection separately from the four-wire sense voltage | — |  |  |
| SRS-SEC-S8-003 | acquire battery voltage and battery current at an interval of **1 ms** or shorter | — |  |  |
| SRS-SEC-S8-004 | measure battery voltage over a range of at least **0 to 100 V** | — |  |  |
| SRS-SEC-S8-005 | measure battery voltage bipolarly, so that a reversed battery connection produces a negative readin… | — |  |  |
| SRS-SEC-S8-006 | measure the shunt differential voltage bipolarly, so that reverse current direction is measurable | — |  |  |
| SRS-SEC-S8-009 | measure battery temperature over a range of at least **−50 °C to +150 °C** | — |  |  |
| SRS-SEC-S8-013 | not allow the filter length of the regulated quantity to introduce a delay that prevents the timing… | — |  |  |
| SRS-SEC-S8-016 | not permit a test to run while the configured sampling rate is invalid | — |  |  |
| SRS-SEC-S8-028 | detect arithmetic overflow and underflow in every measurement and derived-quantity computation, inc… | — |  |  |
| SRS-SEC-S8-030 | detect a reversed battery power connection | — |  |  |
| SRS-SEC-S8-031 | detect a reversed battery sense connection | — |  |  |
| SRS-SEC-S8-032 | require a reversed-polarity indication to persist for a configurable count of consecutive measureme… | — |  |  |
| SRS-SEC-S8-033 | detect a reversed connection before energising the transistor bank, and shall not energise it while… | — |  |  |
| SRS-SEC-S8-034 | detect a difference between the power-connection voltage and the sense-connection voltage exceeding… | — |  |  |
| SRS-SEC-S8-035 | detect an open or absent battery before energising the transistor bank, and shall not energise it w… | — |  |  |

### S9 — S9 Local protection & interlocks

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S9-001 | enforce a configurable maximum battery voltage and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-002 | enforce a configurable minimum battery voltage | — |  |  |
| SRS-SEC-S9-003 | enforce a configurable maximum charge current and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-004 | enforce a configurable maximum discharge current and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-005 | enforce a configurable maximum power and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-006 | enforce a configurable maximum battery temperature and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-007 | enforce a configurable minimum battery temperature | — |  |  |
| SRS-SEC-S9-008 | enforce a configurable maximum heatsink temperature and shall enter the safe state on breach | — |  |  |
| SRS-SEC-S9-009 | enforce a configurable maximum transistor-bank charge-mode voltage (LNT) and shall enter the safe s… | — |  |  |
| SRS-SEC-S9-010 | enforce a configurable maximum transistor-bank discharge-mode voltage (ZNT) and shall enter the saf… | — |  |  |
| SRS-SEC-S9-011 | enforce a configurable maximum system rectifier output voltage | — |  |  |
| SRS-SEC-S9-012 | detect that the current-measurement converter has reached its conversion limit and shall enter the… | — |  |  |
| SRS-SEC-S9-013 | detect that the voltage-measurement converter has reached its conversion limit and shall enter the… | — |  |  |
| SRS-SEC-S9-014 | enforce every protective limit at every measurement interval while regulating | — |  |  |
| SRS-SEC-S9-015 | enforce every protective limit independently of, and in addition to, any limit the Primary Board en… | — |  |  |
| SRS-SEC-S9-016 | enforce its configured absolute ratings even when the Primary Board commands a setpoint or supplies… | — |  |  |
| SRS-SEC-S9-018 | reject a factory configuration whose protective limits are mutually inconsistent, and shall not reg… | — |  |  |
| SRS-SEC-S9-022 | not apply a consecutive-detection count to a condition whose immediate action is required for safet… | `<TBD-S33>` |  |  |
| SRS-SEC-S9-025 | The Secondary Board shall, on declaring any fault of §4.9.1, set the analog reference output to zero | — |  |  |
| SRS-SEC-S9-026 | The Secondary Board shall, on declaring any fault of §4.9.1, place the mode contactors in their def… | — |  |  |
| SRS-SEC-S9-027 | set the analog reference output to zero before de-energising the mode contactors, so that no contac… | — |  |  |
| SRS-SEC-S9-028 | enter the safe state on a fault without requiring any message from the Primary Board | — |  |  |
| SRS-SEC-S9-029 | enter the safe state on a fault even when the CAN link is unavailable | — |  |  |
| SRS-SEC-S9-030 | activate its error-indication output on declaring a fault | — |  |  |
| SRS-SEC-S9-031 | enter the safe state within `<TBD-S34>` of detecting a condition requiring it | `<TBD-S34>` |  |  |
| SRS-SEC-S9-034 | enter the safe state on activation of an external interlock input, if such an input is required | — |  |  |
| SRS-SEC-S9-035 | treat the external interlock as taking precedence over any command from the Primary Board, if such… | — |  |  |
| SRS-SEC-S9-037 | enter the safe state on activation of the thermostat input | — |  |  |

### S10 — S10 Autonomous safe state on fault or comms loss

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S10-001 | detect the loss of communication with the Primary Board while regulating | — |  |  |
| SRS-SEC-S10-002 | declare loss of Primary communication when no valid message addressed to it has been received withi… | — |  |  |
| SRS-SEC-S10-003 | take a defined protective action on declaring loss of Primary communication, without waiting for an… | — |  |  |
| SRS-SEC-S10-004 | retain the measurements accumulated up to the moment of communication loss | — |  |  |
| SRS-SEC-S10-005 | retain the reason it entered the safe state, and shall report it when communication is restored | — |  |  |
| SRS-SEC-S10-006 | not resume regulation automatically when communication is restored; it shall await an explicit comm… | — |  |  |
| SRS-SEC-S10-007 | remain able to enforce every protective limit of §4.9 while Primary communication is lost | — |  |  |
| SRS-SEC-S10-008 | continue to acquire measurements while Primary communication is lost, so that the retained record i… | — |  |  |
| SRS-SEC-S10-010 | record `<TBD-S37>` (whether loss of Primary communication trips the channel immediately or permits… | `<TBD-S37>` |  |  |

### S11 — S11 Watchdog & timing supervision

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S11-001 | service a hardware watchdog cyclically while it is operating correctly | — |  |  |
| SRS-SEC-S11-002 | be reset by the watchdog if any function on which safe operation depends stops running | — |  |  |
| SRS-SEC-S11-003 | not service the watchdog from a context that would continue to run if the regulation loop had stopp… | — |  |  |
| SRS-SEC-S11-004 | enter the safe state as a consequence of a watchdog reset, by the initialization requirements of §4… | — |  |  |
| SRS-SEC-S11-006 | detect that the regulation loop has failed to execute within its required interval, and shall treat… | — |  |  |
| SRS-SEC-S11-007 | detect that the measurement acquisition has failed to complete within its required interval, and sh… | — |  |  |
| SRS-SEC-S11-010 | record `<TBD-S39>` (the required watchdog period, and whether a windowed watchdog with a minimum as… | `<TBD-S39>` |  |  |

### S12 — S12 Telemetry & registration reporting

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S12-020 | not discard a registration record silently; if it cannot transmit one, it shall report the loss | — |  |  |
| SRS-SEC-S12-023 | record `<TBD-S41>` (the required registration interval) — see conflict **C-05**. The five sourced v… | `<TBD-S41>` |  |  |

### S13 — S13 Fault reporting, codes, latch & clear

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S13-009 | retain a latched fault until it is explicitly cleared, even after the condition that caused it has… | — |  |  |
| SRS-SEC-S13-010 | not resume regulation while any latched fault is uncleared | — |  |  |
| SRS-SEC-S13-012 | refuse to clear a fault whose underlying condition is still present, and shall report the refusal | — |  |  |

### S14 — S14 Calibration

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S14-021 | enforce every protective limit of §4.9 while in calibration mode | — |  |  |
| SRS-SEC-S14-025 | not commit a calibration parameter set that fails its own validity check, and shall retain the prev… | — |  |  |
| SRS-SEC-S14-027 | not enter calibration mode while regulating, and shall not accept a regulation command while in cal… | — |  |  |

### S15 — S15 Non-volatile configuration & persistence

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S15-014 | not write to persistent storage while regulating, except where a requirement of §4.19 requires it | — |  |  |
| SRS-SEC-S15-015 | leave a persisted block either fully updated or unchanged if supply is lost during a write | — |  |  |
| SRS-SEC-S15-017 | not permit a restore-to-defaults command to overwrite calibration data unless the command explicitl… | — |  |  |

### S16 — S16 Firmware update over CAN

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S16-003 | not accept a firmware update while regulating | — |  |  |
| SRS-SEC-S16-004 | be in the safe state throughout a firmware update | — |  |  |
| SRS-SEC-S16-011 | remain able to accept a further firmware update if an update is interrupted before completion | — |  |  |
| SRS-SEC-S16-012 | not activate a partially received or failed firmware image | — |  |  |

### S17 — S17 Local indication & digital outputs

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S17-001 | drive the charge contactor relay output only as required by §4.5 and §4.7 | — |  |  |
| SRS-SEC-S17-002 | drive the discharge contactor relay output only as required by §4.6 and §4.7 | — |  |  |
| SRS-SEC-S17-010 | set every spare digital output to its defined default state on entering the safe state, unless a re… | `<TBD-S02>` |  |  |
| SRS-SEC-S17-011 | not permit a commanded spare-digital-output state to affect the charge contactor, the discharge con… | — |  |  |

### S19 — S19 Power-fail detection, backup & resume (added — see §9)

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-S19-001 | detect an impending loss of supply before the supply falls below the level required for correct ope… | — |  |  |
| SRS-SEC-S19-002 | place the analog reference output at zero on detecting an impending loss of supply | — |  |  |
| SRS-SEC-S19-003 | persist its regulation context on detecting an impending loss of supply | — |  |  |
| SRS-SEC-S19-005 | complete the persistence of SRS-SEC-S19-003 within the supply hold-up time available to it | — |  |  |
| SRS-SEC-S19-009 | not resume regulation after a loss of supply without an explicit command | — |  |  |
| SRS-SEC-S19-011 | verify the integrity of the persisted context before restoring it, and shall report it as unusable… | — |  |  |
| SRS-SEC-S19-012 | verify, before resuming, that the battery connection is present and correctly polarised | — |  |  |

### CM — §3.3 Communication interfaces

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-CM-007 | declare loss of Primary communication when no valid message addressed to it has been received withi… | — |  |  |

### HW — §3.1 Hardware interfaces

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-HW-002 | drive the transistor-bank base drive through an analog reference output | — |  |  |
| SRS-SEC-HW-003 | be capable of producing a bipolar analog reference output when configured for a dual transistor ban… | — |  |  |
| SRS-SEC-HW-004 | record `<TBD-S03>` (the analog reference output range) — see conflict **C-02** | `<TBD-S03>` |  |  |
| SRS-SEC-HW-005 | record `<TBD-S04>` (the analog reference output resolution) — see conflict **C-01** | `<TBD-S04>` |  |  |
| SRS-SEC-HW-006 | drive a charge-mode contactor through a relay output | — |  |  |
| SRS-SEC-HW-007 | drive a discharge-mode contactor through a relay output | — |  |  |
| SRS-SEC-HW-016 | measure the transistor-bank voltage in charge mode (LNT) | — |  |  |
| SRS-SEC-HW-017 | measure the transistor-bank voltage in discharge mode (ZNT) | — |  |  |
| SRS-SEC-HW-018 | measure heatsink temperature | — |  |  |
| SRS-SEC-HW-023 | service a hardware watchdog such that an unserviced watchdog causes a board reset | — |  |  |
| SRS-SEC-HW-024 | detect an impending loss of supply and signal it to software before the supply voltage falls below… | — |  |  |

### NF — §5 Non-functional requirements

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-SEC-NF-001 | execute its regulation loop at an interval of **1 ms** or shorter | — |  |  |
| SRS-SEC-NF-002 | acquire battery voltage and battery current at an interval of **1 ms** or shorter | — |  |  |
| SRS-SEC-NF-003 | evaluate the transistor-bank voltage limits and the temperature limits at an interval of **100 ms**… | — |  |  |
| SRS-SEC-NF-004 | evaluate the current, voltage and power protective limits at every measurement interval | — |  |  |
| SRS-SEC-NF-005 | meet SRS-SEC-NF-001 to SRS-SEC-NF-004 while simultaneously servicing CAN traffic, reporting telemet… | — |  |  |
| SRS-SEC-NF-006 | meet SRS-SEC-NF-001 to SRS-SEC-NF-004 with no dependence on the Primary Board's timing | — |  |  |
| SRS-SEC-NF-007 | reach a commanded setpoint from zero within **10 ms** | — |  |  |
| SRS-SEC-NF-008 | return the analog reference output to zero within **10 ms** of a stop command or a fault detection | — |  |  |
| SRS-SEC-NF-009 | complete a range transition within **50 ms** | — |  |  |
| SRS-SEC-NF-010 | detect loss of Primary communication within the configured timeout, default **100 ms** | — |  |  |
| SRS-SEC-NF-011 | enter the safe state within `<TBD-S34>` of detecting a condition requiring it | `<TBD-S34>` |  |  |
| SRS-SEC-NF-012 | produce a registration record at the interval resolved by `<TBD-S41>` | `<TBD-S41>` |  |  |
| SRS-SEC-NF-013 | bound the jitter of its regulation loop interval to `<TBD-S61>` | `<TBD-S61>` |  |  |
| SRS-SEC-NF-014 | bound the latency from a measurement being acquired to the regulation loop acting on it to `<TBD-S7… | `<TBD-S71>` |  |  |
| SRS-SEC-NF-016 | not allow any interrupt service routine to execute for longer than `<TBD-S72>`, and every interrupt… | `<TBD-S72>` |  |  |
| SRS-SEC-NF-017 | default to the safe state in every condition in which its correct operation cannot be established | — |  |  |
| SRS-SEC-NF-018 | not depend on the Primary Board, the CAN link or the Web Application to reach the safe state | — |  |  |
| SRS-SEC-NF-019 | enforce its configured absolute ratings under every commanded condition | — |  |  |
| SRS-SEC-NF-020 | not break load current with a contactor | — |  |  |
| SRS-SEC-NF-021 | not energise the transistor bank while the battery connection is absent, reversed or unverified | — |  |  |
| SRS-SEC-NF-022 | treat every fault whose safe handling is undefined as requiring the safe state | — |  |  |
| SRS-SEC-NF-049 | Every requirement marked **** in this document shall be verified by hardware-in-the-loop test on th… | — |  |  |

---

### A2.1 Hardware-critical requirements blocked by an unresolved value

**17 of the 195 hardware-critical requirements cannot be tested until a `<TBD-Snn>` is answered.**

| ID | Blocked by |
|---|---|
| SRS-SEC-HW-004 | `<TBD-S03>` |
| SRS-SEC-HW-005 | `<TBD-S04>` |
| SRS-SEC-S5-006 | `<TBD-S19>` |
| SRS-SEC-S6-006 | `<TBD-S19>` |
| SRS-SEC-S7-016 | `<TBD-S23>` |
| SRS-SEC-S7-031 | `<TBD-S19>` |
| SRS-SEC-S9-022 | `<TBD-S33>` |
| SRS-SEC-S9-031 | `<TBD-S34>` |
| SRS-SEC-S10-010 | `<TBD-S37>` |
| SRS-SEC-S11-010 | `<TBD-S39>` |
| SRS-SEC-S12-023 | `<TBD-S41>` |
| SRS-SEC-S17-010 | `<TBD-S02>` |
| SRS-SEC-NF-011 | `<TBD-S34>` |
| SRS-SEC-NF-012 | `<TBD-S41>` |
| SRS-SEC-NF-013 | `<TBD-S61>` |
| SRS-SEC-NF-014 | `<TBD-S71>` |
| SRS-SEC-NF-016 | `<TBD-S72>` |

---

*Generated from `02_SRS_ME_Secondary_Board_v0.1.md`. 530 requirements, 195 hardware-critical.*
