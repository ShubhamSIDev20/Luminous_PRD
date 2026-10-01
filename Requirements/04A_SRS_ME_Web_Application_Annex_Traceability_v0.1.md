# Annex to ME-SRS-WEB-001 — Traceability, Verification Sheet and Baseline Coverage Map

**Document ID:** ME-SRS-WEB-001-A · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Parent:** `04_SRS_ME_Web_Application_v0.1.md`

This annex holds §6 (Traceability Matrix) of the Web Application SRS and adds two
sheets the reviewer needs. It is **generated from the parent document**, so the two
cannot drift apart; regenerate it after any change to the parent.

> **§A2** is the verification sheet: every requirement whose failure would silently
> lose data or silently grant access. **§A3** is the baseline coverage map: it proves
> mechanically that no requirement of the developer's SRS or RTM was dropped without
> being either carried forward or explicitly withdrawn in parent §4.19.

Total requirements: **405** · data-integrity/access critical: **84** · carrying an unsourced value: **18**

---

## A1. Traceability Matrix

### A1.1 Coverage roll-up by area

| Area | Title | Coverage | Requirements | of which ⚠ |
|---|---|---|---|---|
| **W1** | W1 Board discovery & network configuration | ● Strong | 13 | 0 |
| **W2** | W2 Session registration & connection management | ●◆ Strong / new unit | 18 | 2 |
| **W3** | W3 Channel inventory & presence | ◆ New to ME | 15 | 2 |
| **W4** | W4 Program authoring, storage & validation | ● Strong / ◐ validation | 28 | 2 |
| **W5** | W5 Program transfer to the Primary Board | ● Strong | 17 | 4 |
| **W6** | W6 Program scheduling | ○ Absent from WAD/SRS | 19 | 5 |
| **W7** | W7 Channel assignment & execution control | ●◆ Strong / new semantics | 21 | 3 |
| **W8** | W8 Real-time monitoring | ● Strong | 15 | 3 |
| **W9** | W9 Session data recording | ● Strong / ◐ at scale | 24 | 11 |
| **W10** | W10 Session self-containment | ● Strong | 14 | 6 |
| **W11** | W11 Reporting & export | ◐ Partial | 12 | 2 |
| **W12** | W12 Calibration workflow | ● Strong / conflicted numbering | 20 | 4 |
| **W13** | W13 Battery, battery type & limit standard management | ● Strong / ◐ data model | 14 | 2 |
| **W14** | W14 Configuration & code-message management | ● Strong | 19 | 4 |
| **W15** | W15 User, role & access management | ● Strong | 17 | 4 |
| **W16** | W16 Audit & event logging | ● Strong | 12 | 3 |
| **W17** | W17 System services & external integration | ◐ Absent from WAD/SRS | 16 | 4 |
| **W18** | W18 Firmware image management & distribution | ◆ New to ME | 11 | 3 |
| **W19** | W19 DBC CAN management — EXCLUDED from ME scope | — withdrawn | 3 | 0 |
| **CM** | §3.3 Communication interfaces | — | 16 | 3 |
| **HW** | §3.1 Hardware interfaces | — | 4 | 0 |
| **NF** | §5 Non-functional requirements | — | 55 | 16 |
| **SW** | §3.2 Software interfaces | — | 10 | 0 |
| **UI** | §3.4 User interfaces | — | 12 | 1 |
| | **Total** | | **405** | **84** |

### A1.2 Source → requirement

Reverse mapping. A requirement citing several sources appears under each, once.

| Source | Count | Requirements |
|---|---|---|
| ICD — ME-ICD-001 | 108 | SRS-WEB-SW-001, SRS-WEB-SW-002, SRS-WEB-CM-001, SRS-WEB-CM-002, SRS-WEB-CM-003, SRS-WEB-CM-004, SRS-WEB-CM-005, SRS-WEB-CM-006, SRS-WEB-CM-007, SRS-WEB-CM-008, SRS-WEB-CM-011, SRS-WEB-CM-012, SRS-WEB-CM-013, SRS-WEB-CM-016, SRS-WEB-UI-003, SRS-WEB-UI-007, SRS-WEB-W1-001, SRS-WEB-W1-003, SRS-WEB-W1-006, SRS-WEB-W1-007, SRS-WEB-W1-008, SRS-WEB-W1-009, SRS-WEB-W2-001, SRS-WEB-W2-003, SRS-WEB-W2-006, SRS-WEB-W2-009, SRS-WEB-W2-011, SRS-WEB-W2-013, SRS-WEB-W2-015, SRS-WEB-W3-001, SRS-WEB-W3-002, SRS-WEB-W3-003, SRS-WEB-W3-004, SRS-WEB-W3-006, SRS-WEB-W3-007, SRS-WEB-W3-010, SRS-WEB-W3-011, SRS-WEB-W3-012, SRS-WEB-W5-001, SRS-WEB-W5-002, SRS-WEB-W5-003, SRS-WEB-W5-007, SRS-WEB-W5-008, SRS-WEB-W5-009, SRS-WEB-W5-010, SRS-WEB-W5-011, SRS-WEB-W5-012, SRS-WEB-W5-013, SRS-WEB-W5-014, SRS-WEB-W5-015, SRS-WEB-W7-001, SRS-WEB-W7-002, SRS-WEB-W7-003, SRS-WEB-W7-004, SRS-WEB-W7-005, SRS-WEB-W7-006, SRS-WEB-W7-007, SRS-WEB-W7-008, SRS-WEB-W7-009, SRS-WEB-W7-010, SRS-WEB-W7-017, SRS-WEB-W8-001, SRS-WEB-W8-003, SRS-WEB-W8-006, SRS-WEB-W8-009, SRS-WEB-W8-010, SRS-WEB-W8-013, SRS-WEB-W9-001, SRS-WEB-W9-010, SRS-WEB-W9-011, SRS-WEB-W9-012, SRS-WEB-W9-013, SRS-WEB-W9-014, SRS-WEB-W9-015, SRS-WEB-W12-001, SRS-WEB-W12-002, SRS-WEB-W12-004, SRS-WEB-W12-005, SRS-WEB-W12-006, SRS-WEB-W12-007, SRS-WEB-W12-008, SRS-WEB-W12-009, SRS-WEB-W12-010, SRS-WEB-W12-011, SRS-WEB-W12-015, SRS-WEB-W14-004, SRS-WEB-W14-006, SRS-WEB-W14-007, SRS-WEB-W14-009, SRS-WEB-W14-010, SRS-WEB-W14-011, SRS-WEB-W14-013, SRS-WEB-W14-014, SRS-WEB-W14-015, SRS-WEB-W14-016, SRS-WEB-W16-007, SRS-WEB-W16-008, SRS-WEB-W18-001, SRS-WEB-W18-004, SRS-WEB-W18-005, SRS-WEB-W18-006, SRS-WEB-W18-007, SRS-WEB-W18-008, SRS-WEB-NF-008, SRS-WEB-NF-012, SRS-WEB-NF-017, SRS-WEB-NF-020, SRS-WEB-NF-050 |
| Derived from other requirements | 84 | SRS-WEB-HW-001, SRS-WEB-SW-004, SRS-WEB-UI-010, SRS-WEB-W1-005, SRS-WEB-W1-009, SRS-WEB-W1-011, SRS-WEB-W1-012, SRS-WEB-W2-011, SRS-WEB-W2-014, SRS-WEB-W2-016, SRS-WEB-W3-005, SRS-WEB-W3-006, SRS-WEB-W3-008, SRS-WEB-W3-009, SRS-WEB-W3-013, SRS-WEB-W4-008, SRS-WEB-W4-009, SRS-WEB-W4-014, SRS-WEB-W4-023, SRS-WEB-W4-026, SRS-WEB-W5-001, SRS-WEB-W5-010, SRS-WEB-W5-016, SRS-WEB-W5-017, SRS-WEB-W6-007, SRS-WEB-W6-013, SRS-WEB-W6-017, SRS-WEB-W6-019, SRS-WEB-W7-016, SRS-WEB-W7-017, SRS-WEB-W7-020, SRS-WEB-W8-005, SRS-WEB-W8-013, SRS-WEB-W8-014, SRS-WEB-W9-006, SRS-WEB-W9-016, SRS-WEB-W10-006, SRS-WEB-W10-013, SRS-WEB-W11-005, SRS-WEB-W11-006, SRS-WEB-W11-007, SRS-WEB-W11-010, SRS-WEB-W11-011, SRS-WEB-W12-003, SRS-WEB-W12-013, SRS-WEB-W12-014, SRS-WEB-W12-016, SRS-WEB-W12-017, SRS-WEB-W13-005, SRS-WEB-W13-007, SRS-WEB-W13-013, SRS-WEB-W13-014, SRS-WEB-W14-005, SRS-WEB-W14-012, SRS-WEB-W14-019, SRS-WEB-W15-006, SRS-WEB-W15-008, SRS-WEB-W15-013, SRS-WEB-W15-015, SRS-WEB-W15-017, SRS-WEB-W16-004, SRS-WEB-W16-005, SRS-WEB-W17-006, SRS-WEB-W17-013, SRS-WEB-W17-014, SRS-WEB-W18-008, SRS-WEB-W18-010, SRS-WEB-NF-004, SRS-WEB-NF-005, SRS-WEB-NF-006, SRS-WEB-NF-012, SRS-WEB-NF-014, SRS-WEB-NF-015, SRS-WEB-NF-019, SRS-WEB-NF-023, SRS-WEB-NF-024, SRS-WEB-NF-025, SRS-WEB-NF-031, SRS-WEB-NF-041, SRS-WEB-NF-045, SRS-WEB-NF-050, SRS-WEB-NF-052, SRS-WEB-NF-053, SRS-WEB-NF-054 |
| WAD/RTM — developer's traceability matrix (v0.5) | 83 | SRS-WEB-SW-005, SRS-WEB-SW-006, SRS-WEB-SW-007, SRS-WEB-SW-008, SRS-WEB-CM-001, SRS-WEB-CM-006, SRS-WEB-CM-009, SRS-WEB-UI-005, SRS-WEB-UI-006, SRS-WEB-UI-009, SRS-WEB-UI-011, SRS-WEB-W1-002, SRS-WEB-W1-004, SRS-WEB-W2-004, SRS-WEB-W2-007, SRS-WEB-W2-009, SRS-WEB-W2-010, SRS-WEB-W2-012, SRS-WEB-W4-001, SRS-WEB-W4-004, SRS-WEB-W5-007, SRS-WEB-W6-001, SRS-WEB-W6-002, SRS-WEB-W6-005, SRS-WEB-W6-006, SRS-WEB-W6-008, SRS-WEB-W6-009, SRS-WEB-W6-010, SRS-WEB-W6-011, SRS-WEB-W6-014, SRS-WEB-W6-015, SRS-WEB-W6-018, SRS-WEB-W7-003, SRS-WEB-W7-004, SRS-WEB-W7-005, SRS-WEB-W7-007, SRS-WEB-W7-016, SRS-WEB-W8-001, SRS-WEB-W8-002, SRS-WEB-W8-007, SRS-WEB-W8-009, SRS-WEB-W8-015, SRS-WEB-W9-008, SRS-WEB-W9-019, SRS-WEB-W11-003, SRS-WEB-W12-008, SRS-WEB-W12-011, SRS-WEB-W12-012, SRS-WEB-W13-003, SRS-WEB-W13-008, SRS-WEB-W14-001, SRS-WEB-W14-003, SRS-WEB-W14-008, SRS-WEB-W14-016, SRS-WEB-W15-001, SRS-WEB-W15-002, SRS-WEB-W15-004, SRS-WEB-W15-005, SRS-WEB-W15-009, SRS-WEB-W15-010, SRS-WEB-W15-011, SRS-WEB-W15-012, SRS-WEB-W15-016, SRS-WEB-W16-001, SRS-WEB-W16-009, SRS-WEB-W16-010, SRS-WEB-W17-001, SRS-WEB-W17-002, SRS-WEB-W17-003, SRS-WEB-W17-004, SRS-WEB-W17-005, SRS-WEB-W17-006, SRS-WEB-W17-007, SRS-WEB-W17-008, SRS-WEB-W17-009, SRS-WEB-W17-011, SRS-WEB-NF-001, SRS-WEB-NF-002, SRS-WEB-NF-013, SRS-WEB-NF-018, SRS-WEB-NF-039, SRS-WEB-NF-042, SRS-WEB-NF-043 |
| WAD/SRS — developer's SRS (BTS-SRS-001 v0.6) | 80 | SRS-WEB-HW-002, SRS-WEB-CM-009, SRS-WEB-CM-010, SRS-WEB-UI-001, SRS-WEB-UI-002, SRS-WEB-UI-005, SRS-WEB-UI-008, SRS-WEB-UI-011, SRS-WEB-W1-001, SRS-WEB-W1-002, SRS-WEB-W1-003, SRS-WEB-W1-004, SRS-WEB-W1-006, SRS-WEB-W1-007, SRS-WEB-W1-011, SRS-WEB-W2-001, SRS-WEB-W2-004, SRS-WEB-W2-005, SRS-WEB-W4-001, SRS-WEB-W4-008, SRS-WEB-W4-015, SRS-WEB-W4-016, SRS-WEB-W4-017, SRS-WEB-W4-018, SRS-WEB-W4-021, SRS-WEB-W4-022, SRS-WEB-W5-001, SRS-WEB-W5-004, SRS-WEB-W5-005, SRS-WEB-W7-003, SRS-WEB-W7-015, SRS-WEB-W8-001, SRS-WEB-W8-002, SRS-WEB-W8-007, SRS-WEB-W8-008, SRS-WEB-W8-009, SRS-WEB-W9-001, SRS-WEB-W9-002, SRS-WEB-W9-003, SRS-WEB-W9-005, SRS-WEB-W9-018, SRS-WEB-W10-001, SRS-WEB-W10-002, SRS-WEB-W10-003, SRS-WEB-W10-008, SRS-WEB-W10-009, SRS-WEB-W10-010, SRS-WEB-W10-011, SRS-WEB-W10-012, SRS-WEB-W11-001, SRS-WEB-W11-002, SRS-WEB-W11-003, SRS-WEB-W11-009, SRS-WEB-W12-004, SRS-WEB-W12-012, SRS-WEB-W12-013, SRS-WEB-W13-001, SRS-WEB-W13-003, SRS-WEB-W13-006, SRS-WEB-W13-008, SRS-WEB-W15-001, SRS-WEB-W15-002, SRS-WEB-W15-003, SRS-WEB-W15-004, SRS-WEB-W15-005, SRS-WEB-W15-015, SRS-WEB-W16-001, SRS-WEB-W16-009, SRS-WEB-W16-010, SRS-WEB-W16-012, SRS-WEB-NF-001, SRS-WEB-NF-002, SRS-WEB-NF-018, SRS-WEB-NF-024, SRS-WEB-NF-026, SRS-WEB-NF-027, SRS-WEB-NF-037, SRS-WEB-NF-040, SRS-WEB-NF-043, SRS-WEB-NF-044 |
| NEW — ME | 77 | SRS-WEB-SW-003, SRS-WEB-UI-003, SRS-WEB-UI-004, SRS-WEB-UI-007, SRS-WEB-UI-010, SRS-WEB-W1-008, SRS-WEB-W1-010, SRS-WEB-W2-014, SRS-WEB-W3-002, SRS-WEB-W3-012, SRS-WEB-W3-013, SRS-WEB-W4-007, SRS-WEB-W4-019, SRS-WEB-W5-006, SRS-WEB-W6-004, SRS-WEB-W6-012, SRS-WEB-W6-013, SRS-WEB-W6-016, SRS-WEB-W7-010, SRS-WEB-W7-011, SRS-WEB-W8-005, SRS-WEB-W8-006, SRS-WEB-W8-010, SRS-WEB-W8-011, SRS-WEB-W8-012, SRS-WEB-W9-009, SRS-WEB-W9-010, SRS-WEB-W9-011, SRS-WEB-W9-013, SRS-WEB-W9-016, SRS-WEB-W9-018, SRS-WEB-W9-020, SRS-WEB-W10-004, SRS-WEB-W10-005, SRS-WEB-W10-006, SRS-WEB-W10-007, SRS-WEB-W10-013, SRS-WEB-W10-014, SRS-WEB-W11-004, SRS-WEB-W11-005, SRS-WEB-W11-006, SRS-WEB-W11-008, SRS-WEB-W12-006, SRS-WEB-W12-018, SRS-WEB-W13-007, SRS-WEB-W13-010, SRS-WEB-W13-011, SRS-WEB-W14-005, SRS-WEB-W14-017, SRS-WEB-W15-006, SRS-WEB-W15-013, SRS-WEB-W15-014, SRS-WEB-W16-003, SRS-WEB-W16-004, SRS-WEB-W16-005, SRS-WEB-W16-011, SRS-WEB-W17-012, SRS-WEB-W17-013, SRS-WEB-W17-014, SRS-WEB-W17-015, SRS-WEB-W18-002, SRS-WEB-W18-003, SRS-WEB-W18-009, SRS-WEB-NF-003, SRS-WEB-NF-009, SRS-WEB-NF-013, SRS-WEB-NF-014, SRS-WEB-NF-015, SRS-WEB-NF-016, SRS-WEB-NF-019, SRS-WEB-NF-020, SRS-WEB-NF-022, SRS-WEB-NF-030, SRS-WEB-NF-038, SRS-WEB-NF-041, SRS-WEB-NF-051, SRS-WEB-NF-053 |
| WAD/DBD — database design | 39 | SRS-WEB-HW-003, SRS-WEB-UI-006, SRS-WEB-W4-005, SRS-WEB-W4-022, SRS-WEB-W6-001, SRS-WEB-W6-003, SRS-WEB-W6-004, SRS-WEB-W6-006, SRS-WEB-W6-007, SRS-WEB-W6-014, SRS-WEB-W6-018, SRS-WEB-W7-014, SRS-WEB-W7-015, SRS-WEB-W7-020, SRS-WEB-W9-002, SRS-WEB-W9-003, SRS-WEB-W9-004, SRS-WEB-W9-014, SRS-WEB-W9-015, SRS-WEB-W9-017, SRS-WEB-W9-024, SRS-WEB-W10-007, SRS-WEB-W11-001, SRS-WEB-W12-012, SRS-WEB-W12-016, SRS-WEB-W13-001, SRS-WEB-W13-002, SRS-WEB-W13-004, SRS-WEB-W13-006, SRS-WEB-W13-009, SRS-WEB-W13-014, SRS-WEB-W14-001, SRS-WEB-W14-002, SRS-WEB-W14-008, SRS-WEB-W15-005, SRS-WEB-W15-008, SRS-WEB-W16-002, SRS-WEB-W17-002, SRS-WEB-W19-002 |
| PRI — ME-SRS-PRI-001 | 35 | SRS-WEB-SW-001, SRS-WEB-SW-004, SRS-WEB-CM-008, SRS-WEB-CM-011, SRS-WEB-UI-003, SRS-WEB-W2-002, SRS-WEB-W3-001, SRS-WEB-W3-003, SRS-WEB-W3-004, SRS-WEB-W3-005, SRS-WEB-W3-007, SRS-WEB-W3-009, SRS-WEB-W3-011, SRS-WEB-W4-002, SRS-WEB-W4-003, SRS-WEB-W4-006, SRS-WEB-W4-007, SRS-WEB-W4-011, SRS-WEB-W4-024, SRS-WEB-W4-025, SRS-WEB-W5-002, SRS-WEB-W5-009, SRS-WEB-W5-013, SRS-WEB-W5-014, SRS-WEB-W5-016, SRS-WEB-W7-001, SRS-WEB-W7-011, SRS-WEB-W7-012, SRS-WEB-W7-013, SRS-WEB-W13-005, SRS-WEB-W14-009, SRS-WEB-W14-017, SRS-WEB-W16-008, SRS-WEB-W18-001, SRS-WEB-NF-020 |
| Open-issue register | 29 | SRS-WEB-W3-010, SRS-WEB-W4-003, SRS-WEB-W4-006, SRS-WEB-W4-007, SRS-WEB-W4-027, SRS-WEB-W4-028, SRS-WEB-W7-008, SRS-WEB-W7-009, SRS-WEB-W7-018, SRS-WEB-W7-019, SRS-WEB-W7-021, SRS-WEB-W9-020, SRS-WEB-W9-021, SRS-WEB-W12-020, SRS-WEB-W16-006, SRS-WEB-W16-011, SRS-WEB-W17-012, SRS-WEB-W17-015, SRS-WEB-W18-011, SRS-WEB-W19-001, SRS-WEB-W19-003, SRS-WEB-NF-016, SRS-WEB-NF-023, SRS-WEB-NF-031, SRS-WEB-NF-032, SRS-WEB-NF-033, SRS-WEB-NF-034, SRS-WEB-NF-045, SRS-WEB-NF-047 |
| UNSOURCED — open issue | 22 | SRS-WEB-HW-004, SRS-WEB-CM-014, SRS-WEB-UI-012, SRS-WEB-W1-013, SRS-WEB-W2-018, SRS-WEB-W3-014, SRS-WEB-W3-015, SRS-WEB-W4-020, SRS-WEB-W9-021, SRS-WEB-W9-022, SRS-WEB-W9-023, SRS-WEB-W11-012, SRS-WEB-W12-019, SRS-WEB-W13-012, SRS-WEB-W15-011, SRS-WEB-W16-006, SRS-WEB-W17-016, SRS-WEB-NF-007, SRS-WEB-NF-010, SRS-WEB-NF-011, SRS-WEB-NF-021, SRS-WEB-NF-032 |
| WAD/CAP — capacity & performance analysis | 19 | SRS-WEB-CM-007, SRS-WEB-CM-015, SRS-WEB-CM-016, SRS-WEB-W2-008, SRS-WEB-W5-003, SRS-WEB-W8-011, SRS-WEB-W8-012, SRS-WEB-W9-006, SRS-WEB-W9-007, SRS-WEB-W9-008, SRS-WEB-W9-009, SRS-WEB-W9-024, SRS-WEB-NF-003, SRS-WEB-NF-004, SRS-WEB-NF-005, SRS-WEB-NF-006, SRS-WEB-NF-009, SRS-WEB-NF-048, SRS-WEB-NF-052 |
| WAD/ICD — existing hardware interface | 18 | SRS-WEB-SW-005, SRS-WEB-SW-006, SRS-WEB-SW-007, SRS-WEB-W1-003, SRS-WEB-W1-006, SRS-WEB-W1-007, SRS-WEB-W2-003, SRS-WEB-W2-013, SRS-WEB-W2-017, SRS-WEB-W8-002, SRS-WEB-W8-003, SRS-WEB-W8-004, SRS-WEB-W8-015, SRS-WEB-W9-004, SRS-WEB-W17-008, SRS-WEB-W17-009, SRS-WEB-W17-010, SRS-WEB-W17-011 |
| WAD/ADD — architecture design | 16 | SRS-WEB-HW-001, SRS-WEB-SW-009, SRS-WEB-SW-010, SRS-WEB-W2-007, SRS-WEB-W2-008, SRS-WEB-W8-008, SRS-WEB-W8-014, SRS-WEB-W9-005, SRS-WEB-W17-001, SRS-WEB-W17-003, SRS-WEB-W17-006, SRS-WEB-NF-028, SRS-WEB-NF-029, SRS-WEB-NF-035, SRS-WEB-NF-039, SRS-WEB-NF-046 |
| BM — BM_Manual Ch.12 (BTS-600) | 14 | SRS-WEB-W4-002, SRS-WEB-W4-006, SRS-WEB-W4-010, SRS-WEB-W4-011, SRS-WEB-W4-012, SRS-WEB-W4-024, SRS-WEB-W4-025, SRS-WEB-W7-006, SRS-WEB-W7-012, SRS-WEB-W7-013, SRS-WEB-W7-014, SRS-WEB-W7-018, SRS-WEB-W13-005, SRS-WEB-W14-003 |
| SEC — ME-SRS-SEC-001 | 14 | SRS-WEB-W12-003, SRS-WEB-W12-005, SRS-WEB-W12-007, SRS-WEB-W12-015, SRS-WEB-W13-010, SRS-WEB-W14-010, SRS-WEB-W14-011, SRS-WEB-W14-012, SRS-WEB-W14-013, SRS-WEB-W14-014, SRS-WEB-W14-015, SRS-WEB-W18-003, SRS-WEB-W18-006, SRS-WEB-NF-021 |
| Conflict register | 8 | SRS-WEB-CM-015, SRS-WEB-W4-012, SRS-WEB-W4-013, SRS-WEB-W9-006, SRS-WEB-W9-007, SRS-WEB-W14-004, SRS-WEB-W14-006, SRS-WEB-NF-055 |
| Design-constraint register | 8 | SRS-WEB-SW-002, SRS-WEB-SW-009, SRS-WEB-SW-010, SRS-WEB-W14-004, SRS-WEB-NF-030, SRS-WEB-NF-035, SRS-WEB-NF-036, SRS-WEB-NF-037 |
| Deliberate change to the baseline | 6 | SRS-WEB-SW-004, SRS-WEB-W2-002, SRS-WEB-W2-012, SRS-WEB-W4-021, SRS-WEB-W10-001, SRS-WEB-W12-001 |
| WAD/VVP — verification & validation plan | 3 | SRS-WEB-W11-004, SRS-WEB-W15-007, SRS-WEB-NF-051 |
| Assumption register | 2 | SRS-WEB-SW-003, SRS-WEB-W14-018 |
| CODE-P — legacy Primary firmware | 1 | SRS-WEB-W12-001 |
| HW — Digital Controller Specs | 1 | SRS-WEB-W12-018 |
| LSRS — legacy combined SRS | 1 | SRS-WEB-W4-010 |
| Team engineering policy | 1 | SRS-WEB-NF-049 |

### A1.3 Requirements carrying an unsourced value

| ID | Area | Records | Open issue / conflict |
|---|---|---|---|
| SRS-WEB-HW-004 | HW | <TBD-W05> | #11 |
| SRS-WEB-CM-014 | CM | <TBD-W08> | — |
| SRS-WEB-UI-012 | UI | <TBD-W09> | — |
| SRS-WEB-W1-013 | W1 | <TBD-W11> | — |
| SRS-WEB-W2-018 | W2 | <TBD-W01> | — |
| SRS-WEB-W3-014 | W3 | <TBD-W13> | — |
| SRS-WEB-W3-015 | W3 | <TBD-W14> | #14 |
| SRS-WEB-W4-020 | W4 | <TBD-W15> | — |
| SRS-WEB-W9-022 | W9 | <TBD-W22> | #11 |
| SRS-WEB-W9-023 | W9 | <TBD-W23> | — |
| SRS-WEB-W11-012 | W11 | <TBD-W24> | — |
| SRS-WEB-W12-019 | W12 | <TBD-W25> | — |
| SRS-WEB-W13-012 | W13 | <TBD-W27> | C-26 |
| SRS-WEB-W17-016 | W17 | <TBD-W06> | C-22 |
| SRS-WEB-NF-007 | NF | <TBD-W34> | — |
| SRS-WEB-NF-010 | NF | <TBD-W01> | — |
| SRS-WEB-NF-011 | NF | <TBD-W35> | — |
| SRS-WEB-NF-021 | NF | <TBD-W36> | — |

**18 requirements carry an unsourced value.**

### A1.4 TBD tag → requirement

Answering one tag closes every requirement listed against it.

| Tag | Requirements |
|---|---|
| `<TBD-W01>` | SRS-WEB-W2-018, SRS-WEB-NF-003, SRS-WEB-NF-010, SRS-WEB-NF-052 |
| `<TBD-W02>` | SRS-WEB-NF-047 |
| `<TBD-W03>` | SRS-WEB-W9-024 |
| `<TBD-W04>` | SRS-WEB-W14-018 |
| `<TBD-W05>` | SRS-WEB-HW-004 |
| `<TBD-W06>` | SRS-WEB-SW-008, SRS-WEB-W17-016 |
| `<TBD-W07>` | SRS-WEB-CM-007 |
| `<TBD-W08>` | SRS-WEB-CM-014 |
| `<TBD-W09>` | SRS-WEB-UI-012 |
| `<TBD-W10>` | SRS-WEB-W1-009 |
| `<TBD-W11>` | SRS-WEB-W1-013 |
| `<TBD-W12>` | SRS-WEB-W2-017 |
| `<TBD-W13>` | SRS-WEB-W3-014 |
| `<TBD-W14>` | SRS-WEB-W3-015 |
| `<TBD-W15>` | SRS-WEB-W4-020 |
| `<TBD-W16>` | SRS-WEB-W4-027 |
| `<TBD-W17>` | SRS-WEB-W4-028 |
| `<TBD-W18>` | SRS-WEB-W5-012, SRS-WEB-NF-008 |
| `<TBD-W19>` | SRS-WEB-W6-018 |
| `<TBD-W20>` | SRS-WEB-W7-019 |
| `<TBD-W21>` | SRS-WEB-W7-021 |
| `<TBD-W22>` | SRS-WEB-W9-022 |
| `<TBD-W23>` | SRS-WEB-W9-023, SRS-WEB-NF-017 |
| `<TBD-W24>` | SRS-WEB-W11-012 |
| `<TBD-W25>` | SRS-WEB-W12-019 |
| `<TBD-W26>` | SRS-WEB-W12-020 |
| `<TBD-W27>` | SRS-WEB-W13-012 |
| `<TBD-W28>` | SRS-WEB-W15-016 |
| `<TBD-W29>` | SRS-WEB-W15-017 |
| `<TBD-W30>` | SRS-WEB-W16-006, SRS-WEB-W16-012 |
| `<TBD-W31>` | SRS-WEB-W18-011 |
| `<TBD-W32>` | SRS-WEB-W19-002 |
| `<TBD-W33>` | SRS-WEB-W19-003 |
| `<TBD-W34>` | SRS-WEB-NF-007 |
| `<TBD-W35>` | SRS-WEB-NF-011 |
| `<TBD-W36>` | SRS-WEB-NF-021 |
| `<TBD-W37>` | SRS-WEB-NF-033 |
| `<TBD-W38>` | SRS-WEB-NF-034 |
| `<TBD-W39>` | SRS-WEB-NF-048 |

---

## A2. Data-Integrity and Access-Control Verification Sheet

Every requirement below is marked **⚠** in the parent: its failure would silently
lose or corrupt recorded test data, or silently grant access. Per parent
SRS-WEB-NF-049 none may be closed by code inspection — each needs a test that
demonstrates the failure mode is detected or prevented.

`Blocked by` is pre-filled where the requirement carries an unresolved `<TBD-Wnn>`.

**84 of 405 requirements (21%) are marked ⚠.**


### W2 — W2 Session registration & connection management

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W2-011 | retain a board's channel state, session associations and unstored data across a loss of that board'… | — |  |  |
| SRS-WEB-W2-014 | refuse to withdraw a board's authorisation while any of its channels has a running session, unless… | — |  |  |

### W3 — W3 Channel inventory & presence

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W3-005 | not offer to start a session on a channel the board has reported as incompatible | — |  |  |
| SRS-WEB-W3-010 | report, and shall not silently discard, the case where a channel with a running session becomes abs… | — |  |  |

### W4 — W4 Program authoring, storage & validation

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W4-007 | not modify a program version that is referenced by a running session; a change shall create a new v… | — |  |  |
| SRS-WEB-W4-021 | warn, when a PRODUCER step is present, that step-number-based jumps inside the referenced program m… | — |  |  |

### W5 — W5 Program transfer to the Primary Board

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W5-004 | expand every PRODUCER step before transfer, inlining the referenced program's steps and renumbering… | — |  |  |
| SRS-WEB-W5-006 | retain the mapping from every expanded step back to its source program and source step number | — |  |  |
| SRS-WEB-W5-010 | not report a program as transferred until the board has confirmed acceptance | — |  |  |
| SRS-WEB-W5-016 | not transfer a program to a channel whose session is running | — |  |  |

### W6 — W6 Program scheduling

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W6-010 | skip a target channel whose board is not reachable, and shall record the skip with that reason | — |  |  |
| SRS-WEB-W6-011 | skip a target channel that already has a running session, and shall record the skip with that reason | — |  |  |
| SRS-WEB-W6-012 | skip a target channel position that is empty, and shall record the skip with that reason | — |  |  |
| SRS-WEB-W6-013 | skip a target channel for which the requesting schedule's owner lacks start rights, and shall recor… | — |  |  |
| SRS-WEB-W6-016 | execute a schedule whose execution time has passed while it was not running, or shall record that i… | — |  |  |

### W7 — W7 Channel assignment & execution control

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W7-010 | interpret and present the **per-channel** outcome of every control command, and shall not report a… | — |  |  |
| SRS-WEB-W7-011 | not prevent a control command reaching the channels that can accept it because another addressed ch… | — |  |  |
| SRS-WEB-W7-016 | verify, before starting a session, that the channel is present, compatible, fault-free and not alre… | — |  |  |

### W8 — W8 Real-time monitoring

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W8-005 | present the step number in terms the operator authored, resolving expanded PRODUCER steps back to t… | — |  |  |
| SRS-WEB-W8-011 | not allow live-telemetry processing to delay or displace the recording of logged data | — |  |  |
| SRS-WEB-W8-013 | mark a channel's live display as stale when its telemetry stops, and shall not continue to present… | — |  |  |

### W9 — W9 Session data recording

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W9-001 | receive logged measurement records from every board and shall persist them | — |  |  |
| SRS-WEB-W9-006 | apply a bounded queue with a defined overflow policy on every stage of the logged-data path | — |  |  |
| SRS-WEB-W9-007 | process logged data from different channels concurrently, such that one channel's volume does not d… | — |  |  |
| SRS-WEB-W9-009 | raise an operator-visible alarm when a channel's write backlog grows continuously | — |  |  |
| SRS-WEB-W9-010 | acknowledge logged data to the board, so that the board can retain unacknowledged data and retransm… | — |  |  |
| SRS-WEB-W9-011 | be able to determine, for any session, whether its recorded data is complete, and shall be able to… | — |  |  |
| SRS-WEB-W9-012 | request retransmission of logged data it has determined to be missing | — |  |  |
| SRS-WEB-W9-013 | record a durable marker in a session's store for any interval in which data was lost, so that a gap… | — |  |  |
| SRS-WEB-W9-016 | flush all buffered records for a session to durable storage before reporting the session closed | — |  |  |
| SRS-WEB-W9-018 | survive its own restart during a running session without losing records already written, and shall… | — |  |  |
| SRS-WEB-W9-021 | apply a defined policy when storage is exhausted, and shall not silently stop recording | — |  |  |

### W10 — W10 Session self-containment

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W10-001 | write into a session's own store, at session start, the complete step definition of the program as… | — |  |  |
| SRS-WEB-W10-002 | write into a session's own store the complete step definition of every PRODUCER-referenced program | — |  |  |
| SRS-WEB-W10-003 | write into a session's own store the content of every TABLE file the program references | — |  |  |
| SRS-WEB-W10-004 | write into a session's own store the battery parameter record and the limit standard in force | — |  |  |
| SRS-WEB-W10-005 | write into a session's own store the calibration parameters in force on the channel at session start | — |  |  |
| SRS-WEB-W10-013 | verify, at session start, that everything the session store requires for self-containment was writt… | — |  |  |

### W11 — W11 Reporting & export

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W11-004 | include in an export every column present in the session store, and shall not silently omit any | — |  |  |
| SRS-WEB-W11-006 | mark, in an export, any interval in which recorded data was lost | — |  |  |

### W12 — W12 Calibration workflow

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W12-003 | not offer calibration on a channel with a running session | — |  |  |
| SRS-WEB-W12-012 | store every committed calibration in its own persistent store, with the value, the channel, the act… | — |  |  |
| SRS-WEB-W12-013 | retain the previous calibration when a new one is committed, so that a calibration can be reverted… | — |  |  |
| SRS-WEB-W12-016 | permit calibration only to a user holding the calibrate right for that channel | — |  |  |

### W13 — W13 Battery, battery type & limit standard management

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W13-010 | reject a limit standard whose bounds exceed the absolute ratings of the channel it is applied to | — |  |  |
| SRS-WEB-W13-011 | not present a limit standard as enforced by the hardware unless it has been transferred to the board | — |  |  |

### W14 — W14 Configuration & code-message management

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W14-004 | use the same code space as the boards, and shall not re-map a code | — |  |  |
| SRS-WEB-W14-005 | present an unknown code as an unknown code with its numeric value, and shall not present it as no f… | — |  |  |
| SRS-WEB-W14-011 | require an explicit unlocking action before writing a channel's factory configuration or calibration | — |  |  |
| SRS-WEB-W14-012 | not offer to write a channel's factory configuration while that channel has a running session | — |  |  |

### W15 — W15 User, role & access management

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W15-005 | enforce per-user access rights per **channel**, distinguishing at least the rights to start, to sto… | — |  |  |
| SRS-WEB-W15-006 | enforce a channel access right on the server, and shall not rely on the absence of a control in the… | — |  |  |
| SRS-WEB-W15-013 | apply the same role and channel access checks to programmatic access as to interactive access | — |  |  |
| SRS-WEB-W15-014 | be able to revoke an issued token before its expiry | — |  |  |

### W16 — W16 Audit & event logging

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W16-001 | record every operation that changes persistent state in an audit trail | — |  |  |
| SRS-WEB-W16-003 | record an audit entry for a command issued to hardware, not only for a change to its own data | — |  |  |
| SRS-WEB-W16-004 | not permit an audit entry to be modified or deleted through any interface it exposes | — |  |  |

### W17 — W17 System services & external integration

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W17-012 | authenticate every REST, streaming and tool client | — |  |  |
| SRS-WEB-W17-013 | apply the same role and per-channel access checks to a REST, streaming or tool client as to an inte… | — |  |  |
| SRS-WEB-W17-014 | record every command issued by a REST or tool client in its audit trail, identifying the client and… | — |  |  |
| SRS-WEB-W17-015 | permit the tool interface to be disabled entirely by configuration | — |  |  |

### W18 — W18 Firmware image management & distribution

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-W18-003 | verify a firmware image's integrity on receipt, before offering it for distribution | — |  |  |
| SRS-WEB-W18-006 | not offer to update a channel with a running session | — |  |  |
| SRS-WEB-W18-009 | report a target whose version did not change after an update reported success | — |  |  |

### CM — §3.3 Communication interfaces

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-CM-008 | not treat a board's session loss as the end of a session, and shall not close or truncate the sessi… | — |  |  |
| SRS-WEB-CM-011 | determine, on reconnection, what logged data it is missing, and shall request it | — |  |  |
| SRS-WEB-CM-016 | report, rather than silently drop, any received logged-data record it cannot store | — |  |  |

### NF — §5 Non-functional requirements

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-NF-002 | persist a received logged record without loss under burst | — |  |  |
| SRS-WEB-NF-003 | sustain the aggregate logged-data rate of `<TBD-W01>` boards at the registration rate resolved by `… | `<TBD-W01>` |  |  |
| SRS-WEB-NF-004 | process logged data from different channels concurrently | — |  |  |
| SRS-WEB-NF-012 | not silently lose a logged record; every loss shall be recorded and reportable | — |  |  |
| SRS-WEB-NF-013 | leave a session store readable and internally consistent after an abrupt termination of the applica… | — |  |  |
| SRS-WEB-NF-014 | not allow a change to a program, battery, limit standard or calibration record to alter the interpr… | — |  |  |
| SRS-WEB-NF-015 | apply every persistent change atomically, such that a failure leaves no partial change | — |  |  |
| SRS-WEB-NF-016 | provide a means of backing up its main store and its session stores without stopping a running sess… | — |  |  |
| SRS-WEB-NF-019 | resume recording every running session after its own restart, without operator action | — |  |  |
| SRS-WEB-NF-020 | The Web Application's unavailability shall not stop a running test | — |  |  |
| SRS-WEB-NF-024 | authenticate every actor, human or programmatic, before granting access to any function or data | — |  |  |
| SRS-WEB-NF-025 | enforce authorisation on the server for every request | — |  |  |
| SRS-WEB-NF-030 | not log a credential, a token or an encryption key | — |  |  |
| SRS-WEB-NF-031 | be able to revoke a programmatic credential before its expiry | — |  |  |
| SRS-WEB-NF-038 | not require a destructive migration that discards recorded session data | — |  |  |
| SRS-WEB-NF-049 | Every requirement marked **** shall be verified by a test that demonstrates the failure mode is det… | — |  |  |

### UI — §3.4 User interfaces

| ID | Requirement (abridged) | Blocked by | **Test case** | **Result** |
|---|---|---|---|---|
| SRS-WEB-UI-010 | require explicit operator confirmation before any command that stops, interrupts or resets a runnin… | — |  |  |

---

## A3. Baseline Coverage Map

Mechanically derived: every `FR-` and `NFR-` identifier appearing in the developer's
`SRS.md` or `RTM.md`, matched against the `Source` column of this SRS.

Because `WAD/SRS` and `WAD/RTM` use the **same identifiers for different features**
(parent conflict **C-22**), one baseline identifier can map to requirements in
unrelated areas. That is a property of the sources, not an error here.

| Baseline ID | Carried into | Count |
|---|---|---|
| `FR-001.1` | SRS-WEB-W1-001, SRS-WEB-W1-002, SRS-WEB-W1-002 | 3 |
| `FR-001.2` | SRS-WEB-W1-003 | 1 |
| `FR-001.3` | SRS-WEB-W1-006 | 1 |
| `FR-001.4` | SRS-WEB-W1-007 | 1 |
| `FR-001.5` | SRS-WEB-W1-004, SRS-WEB-W1-004 | 2 |
| `FR-002.1` | SRS-WEB-W2-001 | 1 |
| `FR-002.2` | SRS-WEB-W2-002 | 1 |
| `FR-002.3` | SRS-WEB-W2-004, SRS-WEB-W2-005 | 2 |
| `FR-002.4` | SRS-WEB-CM-009, SRS-WEB-CM-009, SRS-WEB-CM-010, SRS-WEB-W2-005 | 4 |
| `FR-002.5` | SRS-WEB-W2-004, SRS-WEB-W2-005 | 2 |
| `FR-002.6` | SRS-WEB-SW-004, SRS-WEB-W2-002 | 2 |
| `FR-002.7` | SRS-WEB-W2-007 | 1 |
| `FR-002.8` | SRS-WEB-CM-006, SRS-WEB-W2-009 | 2 |
| `FR-002.9` | SRS-WEB-W2-010 | 1 |
| `FR-002.10` | SRS-WEB-W2-004, SRS-WEB-W2-012 | 2 |
| `FR-003.1` | SRS-WEB-W8-001 | 1 |
| `FR-003.2` | SRS-WEB-W8-002 | 1 |
| `FR-003.3` | SRS-WEB-W8-001, SRS-WEB-W8-009, SRS-WEB-W12-004 | 3 |
| `FR-003.4` | SRS-WEB-UI-002, SRS-WEB-W8-008 | 2 |
| `FR-003.5` | SRS-WEB-W8-002 | 1 |
| `FR-003.6` | SRS-WEB-W8-009 | 1 |
| `FR-003.7` | SRS-WEB-SW-006, SRS-WEB-W8-015, SRS-WEB-W17-009 | 3 |
| `FR-004.1` | SRS-WEB-W9-001 | 1 |
| `FR-004.2` | SRS-WEB-W9-001, SRS-WEB-W9-002 | 2 |
| `FR-004.3` | SRS-WEB-W9-002, SRS-WEB-W9-003 | 2 |
| `FR-004.4` | SRS-WEB-W9-005 | 1 |
| `FR-004.7` | SRS-WEB-W9-008 | 1 |
| `FR-005.1` | SRS-WEB-W4-001, SRS-WEB-W4-001 | 2 |
| `FR-005.2` | SRS-WEB-W4-004 | 1 |
| `FR-005.3` | SRS-WEB-W4-004, SRS-WEB-W5-001 | 2 |
| `FR-005.4` | SRS-WEB-W4-004 | 1 |
| `FR-005.5` | SRS-WEB-W7-003 | 1 |
| `FR-005.6` | SRS-WEB-W4-015 | 1 |
| `FR-005.7` | SRS-WEB-W4-016, SRS-WEB-W7-003 | 2 |
| `FR-005.8` | SRS-WEB-W4-017, SRS-WEB-W7-004 | 2 |
| `FR-005.9` | SRS-WEB-W4-008, SRS-WEB-W4-018, SRS-WEB-W7-005 | 3 |
| `FR-005.10` | SRS-WEB-W5-004, SRS-WEB-W5-005, SRS-WEB-W7-005 | 3 |
| `FR-005.11` | SRS-WEB-W4-021 | 1 |
| `FR-005.12` | SRS-WEB-W13-008 | 1 |
| `FR-006.1` | SRS-WEB-W12-001 | 1 |
| `FR-006.2` | SRS-WEB-W12-012 | 1 |
| `FR-006.3` | SRS-WEB-W12-013 | 1 |
| `FR-006.4` | SRS-WEB-W12-008 | 1 |
| `FR-006.5` | SRS-WEB-W12-011 | 1 |
| `FR-006.6` | SRS-WEB-W12-012 | 1 |
| `FR-007.1` | SRS-WEB-W13-001, SRS-WEB-W13-003, SRS-WEB-W13-003 | 3 |
| `FR-007.2` | SRS-WEB-W13-003, SRS-WEB-W13-008 | 2 |
| `FR-007.3` | SRS-WEB-W7-015, SRS-WEB-W13-003, SRS-WEB-W13-006 | 3 |
| `FR-007.4` | SRS-WEB-W5-007 | 1 |
| `FR-008.1` | SRS-WEB-W15-001, SRS-WEB-W15-001, SRS-WEB-NF-024 | 3 |
| `FR-008.2` | SRS-WEB-W15-003, SRS-WEB-W15-012 | 2 |
| `FR-008.3` | SRS-WEB-UI-011, SRS-WEB-UI-011, SRS-WEB-W15-004, SRS-WEB-W15-004, SRS-WEB-W15-005 | 5 |
| `FR-008.4` | SRS-WEB-UI-011, SRS-WEB-W1-011, SRS-WEB-W15-015, SRS-WEB-W16-001 | 4 |
| `FR-008.5` | SRS-WEB-W16-001 | 1 |
| `FR-008.6` | SRS-WEB-W15-010 | 1 |
| `FR-008.7` | SRS-WEB-W15-009 | 1 |
| `FR-010` | SRS-WEB-UI-005 | 1 |
| `FR-010.1` | SRS-WEB-UI-006, SRS-WEB-W11-003, SRS-WEB-W14-001, SRS-WEB-W14-003 | 4 |
| `FR-010.2` | SRS-WEB-W11-009, SRS-WEB-W14-008 | 2 |
| `FR-010.3` | SRS-WEB-UI-005, SRS-WEB-W11-001, SRS-WEB-W11-002, SRS-WEB-W17-002 | 4 |
| `FR-010.4` | SRS-WEB-W17-001 | 1 |
| `FR-010.5` | SRS-WEB-W16-010, SRS-WEB-W17-005 | 2 |
| `FR-010.6` | SRS-WEB-UI-005, SRS-WEB-W17-002, SRS-WEB-W17-003 | 3 |
| `FR-010.7` | SRS-WEB-UI-009, SRS-WEB-W17-004 | 2 |
| `FR-010.8` | SRS-WEB-UI-009, SRS-WEB-W17-004 | 2 |
| `FR-010.9` | SRS-WEB-W14-016 | 1 |
| `FR-010.10` | SRS-WEB-W7-007 | 1 |
| `FR-010.11` | SRS-WEB-CM-001, SRS-WEB-W17-006 | 2 |
| `FR-010.12` | SRS-WEB-SW-005, SRS-WEB-W17-008 | 2 |
| `FR-010.13` | SRS-WEB-SW-007, SRS-WEB-W17-011 | 2 |
| `FR-010.14` | SRS-WEB-W17-007 | 1 |
| `FR-011.1` | SRS-WEB-W6-001, SRS-WEB-W6-002, SRS-WEB-W10-001, SRS-WEB-W10-002 | 4 |
| `FR-011.2` | SRS-WEB-W4-022, SRS-WEB-W6-005, SRS-WEB-W10-003 | 3 |
| `FR-011.3` | SRS-WEB-W6-008, SRS-WEB-W10-008, SRS-WEB-W10-009 | 3 |
| `FR-011.4` | SRS-WEB-W6-006, SRS-WEB-W10-008, SRS-WEB-W10-010 | 3 |
| `FR-011.5` | SRS-WEB-W6-009, SRS-WEB-W10-011 | 2 |
| `FR-011.6` | SRS-WEB-W6-010, SRS-WEB-W7-016, SRS-WEB-W10-012 | 3 |
| `FR-011.7` | SRS-WEB-W6-011, SRS-WEB-W7-016 | 2 |
| `FR-011.8` | SRS-WEB-W6-014 | 1 |
| `FR-011.9` | SRS-WEB-W6-015 | 1 |
| `FR-011.10` | SRS-WEB-W6-008 | 1 |
| `NFR-001` | SRS-WEB-NF-002, SRS-WEB-NF-002 | 2 |
| `NFR-002` | SRS-WEB-W8-007, SRS-WEB-W8-007, SRS-WEB-NF-001, SRS-WEB-NF-001 | 4 |
| `NFR-003` | SRS-WEB-W9-018, SRS-WEB-NF-018, SRS-WEB-NF-018 | 3 |
| `NFR-004` | SRS-WEB-W15-002, SRS-WEB-W15-002, SRS-WEB-NF-026 | 3 |
| `NFR-005` | SRS-WEB-W15-012, SRS-WEB-W15-016, SRS-WEB-NF-027 | 3 |
| `NFR-006` | SRS-WEB-W15-011, SRS-WEB-W15-016 | 2 |
| `NFR-007` | SRS-WEB-NF-043, SRS-WEB-NF-043 | 2 |
| `NFR-008` | SRS-WEB-NF-037 | 1 |
| `NFR-009` | SRS-WEB-W16-009, SRS-WEB-W16-009, SRS-WEB-W16-010, SRS-WEB-W16-012, SRS-WEB-NF-040 | 5 |
| `NFR-010` | SRS-WEB-UI-008, SRS-WEB-NF-042, SRS-WEB-NF-044 | 3 |
| `NFR-011` | SRS-WEB-NF-042 | 1 |
| `NFR-012` | SRS-WEB-NF-042 | 1 |
| `NFR-013` | SRS-WEB-W9-019, SRS-WEB-NF-013 | 2 |
| `NFR-014` | SRS-WEB-W15-005 | 1 |
| `NFR-015` | SRS-WEB-W16-001 | 1 |
| `NFR-016` | SRS-WEB-NF-039 | 1 |

### A3.1 Baseline identifiers explicitly withdrawn

Withdrawn in parent §4.19 — DBC CAN management, out of ME scope by the
resolution of Open Issue **#29**.

| Baseline ID |
|---|
| `FR-004.5` |
| `FR-004.6` |
| `FR-005.4` |
| `FR-005.6` |
| `FR-009.1` |
| `FR-009.2` |
| `FR-009.3` |
| `FR-009.4` |
| `FR-009.5` |
| `FR-009.6` |
| `FR-010.15` |

### A3.2 Baseline identifiers neither carried nor withdrawn

**None.** Every `FR-` and `NFR-` identifier in the developer's SRS and RTM is
either carried into a requirement above or explicitly withdrawn in §A3.1.

---

*Generated from `04_SRS_ME_Web_Application_v0.1.md`. 405 requirements, 84 marked ⚠, 97 baseline identifiers mapped.*
