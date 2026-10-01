# Annex to ME-SRS-PRI-001 — Traceability Matrix and Core Allocation Sheet

**Document ID:** ME-SRS-PRI-001-A · **Version:** 0.1 (draft) · **Date:** 2026-07-27
**Parent:** `01_SRS_ME_Primary_Board_v0.1.md`

This annex holds §6 (Traceability Matrix) and §7 (Core Allocation — Open) of the
Primary Board SRS. It is **generated from the parent document**, so the two cannot
drift apart; regenerate it after any change to the parent.

Total requirements: **620**

---

## A1. Traceability Matrix

### A1.1 Coverage roll-up by checklist area

| Area | Title | Requirements |
|---|---|---|
| **P1** | P1 Startup / init / self-test / shutdown | 16 |
| **P2** | P2 Operating mode / state machine | 15 |
| **P3** | P3 Secondary discovery, enumeration, presence | 15 |
| **P4** | P4 Program reception | 18 |
| **P5** | P5 Program parsing & data model | 26 |
| **P6** | P6 Program scheduling & assignment | 14 |
| **P7** | P7 Step execution engine | 192 |
| **P8** | P8 Concurrent execution of up to 8 | 11 |
| **P9** | P9 Command dispatch to Secondaries | 18 |
| **P10** | P10 Telemetry acquisition & forwarding | 18 |
| **P11** | P11 Monitoring, limit checking, safety supervision | 14 |
| **P12** | P12 Fault detection & management | 18 |
| **P13** | P13 Emergency stop & safe state | 8 |
| **P14** | P14 Communication-loss handling | 15 |
| **P15** | P15 Data logging & storage | 15 |
| **P16** | P16 Data upload & retrieval | 10 |
| **P17** | P17 Time synchronisation & timestamping | 10 |
| **P18** | P18 Configuration management | 25 |
| **P19** | P19 Operator interaction via Web App | 17 |
| **P20** | P20 Firmware update | 17 |
| **P21** | P21 Calibration management | 21 |
| **P22** | P22 Diagnostics, health, event log, audit | 16 |
| **P23** | P23 Power-fail detection & recovery | 14 |
| **P24** | P24 Security & access control | 10 |
| **P25** | P25 Modbus module | 12 |
| **CM** | §3.3 Communication interfaces | 8 |
| **HW** | §3.1 Hardware interfaces | 8 |
| **NF** | §5 Non-functional requirements | 33 |
| **SW** | §3.2 Software interfaces | 4 |
| **UI** | §3.4 User interfaces | 2 |
| | **Total** | **620** |

### A1.2 Source → requirement

Reverse mapping. A requirement citing several sources appears under each. The
forward mapping (requirement → source) is the `Source` column of the parent.

| Source | Count | Requirements |
|---|---|---|
| BM — BM_Manual Ch.12 (BTS-600) | 261 | SRS-PRI-CM-008, SRS-PRI-UI-001, SRS-PRI-P1-002, SRS-PRI-P2-008, SRS-PRI-P2-009, SRS-PRI-P4-012, SRS-PRI-P4-013, SRS-PRI-P5-002, SRS-PRI-P5-003, SRS-PRI-P5-004, SRS-PRI-P5-005, SRS-PRI-P5-006, SRS-PRI-P5-009, SRS-PRI-P5-010, SRS-PRI-P5-011, SRS-PRI-P5-012, SRS-PRI-P5-013, SRS-PRI-P5-014, SRS-PRI-P5-015, SRS-PRI-P5-016, SRS-PRI-P5-017, SRS-PRI-P5-018, SRS-PRI-P5-019, SRS-PRI-P5-021, SRS-PRI-P5-022, SRS-PRI-P5-023, SRS-PRI-P6-005, SRS-PRI-P6-006, SRS-PRI-P6-009, SRS-PRI-P6-009, SRS-PRI-P6-010, SRS-PRI-P6-011, SRS-PRI-P6-012, SRS-PRI-P7-001, SRS-PRI-P7-004, SRS-PRI-P7-005, SRS-PRI-P7-006, SRS-PRI-P7-007, SRS-PRI-P7-009, SRS-PRI-P7-010, SRS-PRI-P7-011, SRS-PRI-P7-012, SRS-PRI-P7-013, SRS-PRI-P7-014, SRS-PRI-P7-015, SRS-PRI-P7-016, SRS-PRI-P7-017, SRS-PRI-P7-018, SRS-PRI-P7-019, SRS-PRI-P7-020, SRS-PRI-P7-021, SRS-PRI-P7-022, SRS-PRI-P7-023, SRS-PRI-P7-024, SRS-PRI-P7-025, SRS-PRI-P7-026, SRS-PRI-P7-027, SRS-PRI-P7-028, SRS-PRI-P7-029, SRS-PRI-P7-030, SRS-PRI-P7-031, SRS-PRI-P7-032, SRS-PRI-P7-033, SRS-PRI-P7-034, SRS-PRI-P7-036, SRS-PRI-P7-037, SRS-PRI-P7-038, SRS-PRI-P7-039, SRS-PRI-P7-040, SRS-PRI-P7-041, SRS-PRI-P7-042, SRS-PRI-P7-043, SRS-PRI-P7-044, SRS-PRI-P7-045, SRS-PRI-P7-046, SRS-PRI-P7-047, SRS-PRI-P7-048, SRS-PRI-P7-049, SRS-PRI-P7-050, SRS-PRI-P7-051, SRS-PRI-P7-052, SRS-PRI-P7-053, SRS-PRI-P7-054, SRS-PRI-P7-055, SRS-PRI-P7-056, SRS-PRI-P7-057, SRS-PRI-P7-058, SRS-PRI-P7-059, SRS-PRI-P7-060, SRS-PRI-P7-061, SRS-PRI-P7-062, SRS-PRI-P7-063, SRS-PRI-P7-064, SRS-PRI-P7-065, SRS-PRI-P7-066, SRS-PRI-P7-067, SRS-PRI-P7-068, SRS-PRI-P7-069, SRS-PRI-P7-070, SRS-PRI-P7-071, SRS-PRI-P7-072, SRS-PRI-P7-073, SRS-PRI-P7-074, SRS-PRI-P7-075, SRS-PRI-P7-076, SRS-PRI-P7-077, SRS-PRI-P7-078, SRS-PRI-P7-079, SRS-PRI-P7-080, SRS-PRI-P7-081, SRS-PRI-P7-082, SRS-PRI-P7-083, SRS-PRI-P7-085, SRS-PRI-P7-086, SRS-PRI-P7-087, SRS-PRI-P7-088, SRS-PRI-P7-089, SRS-PRI-P7-090, SRS-PRI-P7-091, SRS-PRI-P7-092, SRS-PRI-P7-094, SRS-PRI-P7-095, SRS-PRI-P7-096, SRS-PRI-P7-097, SRS-PRI-P7-098, SRS-PRI-P7-099, SRS-PRI-P7-101, SRS-PRI-P7-102, SRS-PRI-P7-103, SRS-PRI-P7-105, SRS-PRI-P7-106, SRS-PRI-P7-107, SRS-PRI-P7-108, SRS-PRI-P7-109, SRS-PRI-P7-110, SRS-PRI-P7-111, SRS-PRI-P7-112, SRS-PRI-P7-113, SRS-PRI-P7-114, SRS-PRI-P7-115, SRS-PRI-P7-116, SRS-PRI-P7-117, SRS-PRI-P7-118, SRS-PRI-P7-119, SRS-PRI-P7-120, SRS-PRI-P7-121, SRS-PRI-P7-122, SRS-PRI-P7-123, SRS-PRI-P7-124, SRS-PRI-P7-125, SRS-PRI-P7-126, SRS-PRI-P7-127, SRS-PRI-P7-128, SRS-PRI-P7-129, SRS-PRI-P7-130, SRS-PRI-P7-131, SRS-PRI-P7-132, SRS-PRI-P7-133, SRS-PRI-P7-134, SRS-PRI-P7-135, SRS-PRI-P7-136, SRS-PRI-P7-137, SRS-PRI-P7-138, SRS-PRI-P7-139, SRS-PRI-P7-140, SRS-PRI-P7-141, SRS-PRI-P7-142, SRS-PRI-P7-143, SRS-PRI-P7-144, SRS-PRI-P7-145, SRS-PRI-P7-146, SRS-PRI-P7-147, SRS-PRI-P7-148, SRS-PRI-P7-149, SRS-PRI-P7-150, SRS-PRI-P7-151, SRS-PRI-P7-152, SRS-PRI-P7-153, SRS-PRI-P7-154, SRS-PRI-P7-155, SRS-PRI-P7-156, SRS-PRI-P7-157, SRS-PRI-P7-158, SRS-PRI-P7-159, SRS-PRI-P7-160, SRS-PRI-P7-161, SRS-PRI-P7-162, SRS-PRI-P7-163, SRS-PRI-P7-164, SRS-PRI-P7-165, SRS-PRI-P7-166, SRS-PRI-P7-167, SRS-PRI-P7-168, SRS-PRI-P7-169, SRS-PRI-P7-170, SRS-PRI-P7-171, SRS-PRI-P7-172, SRS-PRI-P7-173, SRS-PRI-P7-174, SRS-PRI-P7-175, SRS-PRI-P7-176, SRS-PRI-P7-177, SRS-PRI-P7-178, SRS-PRI-P7-179, SRS-PRI-P7-180, SRS-PRI-P7-181, SRS-PRI-P7-182, SRS-PRI-P7-183, SRS-PRI-P7-184, SRS-PRI-P7-185, SRS-PRI-P7-186, SRS-PRI-P7-187, SRS-PRI-P7-188, SRS-PRI-P7-189, SRS-PRI-P7-190, SRS-PRI-P7-191, SRS-PRI-P7-192, SRS-PRI-P8-002, SRS-PRI-P8-004, SRS-PRI-P8-005, SRS-PRI-P9-011, SRS-PRI-P10-005, SRS-PRI-P10-006, SRS-PRI-P10-007, SRS-PRI-P10-012, SRS-PRI-P10-013, SRS-PRI-P10-014, SRS-PRI-P11-001, SRS-PRI-P11-005, SRS-PRI-P12-004, SRS-PRI-P12-016, SRS-PRI-P13-001, SRS-PRI-P13-005, SRS-PRI-P15-001, SRS-PRI-P15-004, SRS-PRI-P15-005, SRS-PRI-P15-007, SRS-PRI-P15-008, SRS-PRI-P17-004, SRS-PRI-P17-006, SRS-PRI-P19-001, SRS-PRI-P19-002, SRS-PRI-P19-003, SRS-PRI-P19-004, SRS-PRI-P19-005, SRS-PRI-P19-006, SRS-PRI-P19-008, SRS-PRI-P19-010, SRS-PRI-P19-011, SRS-PRI-P19-015, SRS-PRI-P19-016, SRS-PRI-P19-017, SRS-PRI-P22-001, SRS-PRI-P23-010, SRS-PRI-P24-004, SRS-PRI-NF-001, SRS-PRI-NF-002, SRS-PRI-NF-003, SRS-PRI-NF-010, SRS-PRI-NF-011, SRS-PRI-NF-013 |
| Derived from other requirements | 115 | SRS-PRI-P1-006, SRS-PRI-P1-009, SRS-PRI-P1-010, SRS-PRI-P1-012, SRS-PRI-P2-003, SRS-PRI-P2-004, SRS-PRI-P2-006, SRS-PRI-P2-010, SRS-PRI-P2-011, SRS-PRI-P2-011, SRS-PRI-P2-013, SRS-PRI-P2-014, SRS-PRI-P3-010, SRS-PRI-P3-012, SRS-PRI-P4-005, SRS-PRI-P4-007, SRS-PRI-P4-008, SRS-PRI-P4-009, SRS-PRI-P4-016, SRS-PRI-P5-007, SRS-PRI-P5-013, SRS-PRI-P5-018, SRS-PRI-P5-020, SRS-PRI-P5-024, SRS-PRI-P6-003, SRS-PRI-P6-004, SRS-PRI-P6-006, SRS-PRI-P6-013, SRS-PRI-P7-003, SRS-PRI-P7-104, SRS-PRI-P8-003, SRS-PRI-P8-004, SRS-PRI-P8-005, SRS-PRI-P9-017, SRS-PRI-P10-008, SRS-PRI-P10-017, SRS-PRI-P12-008, SRS-PRI-P12-009, SRS-PRI-P12-012, SRS-PRI-P12-014, SRS-PRI-P13-002, SRS-PRI-P13-003, SRS-PRI-P13-004, SRS-PRI-P13-005, SRS-PRI-P13-006, SRS-PRI-P13-007, SRS-PRI-P14-006, SRS-PRI-P14-011, SRS-PRI-P14-012, SRS-PRI-P14-013, SRS-PRI-P14-014, SRS-PRI-P15-010, SRS-PRI-P15-011, SRS-PRI-P15-012, SRS-PRI-P15-014, SRS-PRI-P16-002, SRS-PRI-P16-003, SRS-PRI-P16-004, SRS-PRI-P16-007, SRS-PRI-P16-008, SRS-PRI-P16-009, SRS-PRI-P17-005, SRS-PRI-P17-006, SRS-PRI-P17-007, SRS-PRI-P17-008, SRS-PRI-P17-009, SRS-PRI-P18-005, SRS-PRI-P18-019, SRS-PRI-P18-023, SRS-PRI-P18-024, SRS-PRI-P19-013, SRS-PRI-P20-004, SRS-PRI-P20-005, SRS-PRI-P20-007, SRS-PRI-P20-010, SRS-PRI-P20-011, SRS-PRI-P20-014, SRS-PRI-P20-015, SRS-PRI-P20-016, SRS-PRI-P21-017, SRS-PRI-P21-018, SRS-PRI-P21-019, SRS-PRI-P21-020, SRS-PRI-P22-002, SRS-PRI-P22-003, SRS-PRI-P22-004, SRS-PRI-P22-006, SRS-PRI-P22-007, SRS-PRI-P22-008, SRS-PRI-P22-012, SRS-PRI-P22-014, SRS-PRI-P23-005, SRS-PRI-P23-006, SRS-PRI-P23-007, SRS-PRI-P23-008, SRS-PRI-P23-009, SRS-PRI-P23-011, SRS-PRI-P23-012, SRS-PRI-P24-002, SRS-PRI-P24-003, SRS-PRI-P24-006, SRS-PRI-P24-009, SRS-PRI-P25-005, SRS-PRI-P25-007, SRS-PRI-P25-008, SRS-PRI-P25-009, SRS-PRI-P25-010, SRS-PRI-NF-008, SRS-PRI-NF-012, SRS-PRI-NF-014, SRS-PRI-NF-016, SRS-PRI-NF-017, SRS-PRI-NF-020, SRS-PRI-NF-029, SRS-PRI-NF-033 |
| HW — Digital Controller Specs | 111 | SRS-PRI-HW-001, SRS-PRI-HW-004, SRS-PRI-HW-005, SRS-PRI-HW-006, SRS-PRI-SW-004, SRS-PRI-CM-003, SRS-PRI-CM-005, SRS-PRI-CM-006, SRS-PRI-UI-002, SRS-PRI-P1-003, SRS-PRI-P1-005, SRS-PRI-P1-006, SRS-PRI-P1-008, SRS-PRI-P1-011, SRS-PRI-P1-014, SRS-PRI-P3-004, SRS-PRI-P3-007, SRS-PRI-P3-008, SRS-PRI-P4-010, SRS-PRI-P5-024, SRS-PRI-P7-115, SRS-PRI-P7-116, SRS-PRI-P9-002, SRS-PRI-P9-003, SRS-PRI-P9-004, SRS-PRI-P9-005, SRS-PRI-P9-006, SRS-PRI-P9-007, SRS-PRI-P9-012, SRS-PRI-P9-014, SRS-PRI-P9-015, SRS-PRI-P9-016, SRS-PRI-P10-001, SRS-PRI-P10-002, SRS-PRI-P10-003, SRS-PRI-P10-004, SRS-PRI-P11-002, SRS-PRI-P11-003, SRS-PRI-P11-003, SRS-PRI-P11-004, SRS-PRI-P11-004, SRS-PRI-P11-005, SRS-PRI-P11-006, SRS-PRI-P11-007, SRS-PRI-P11-007, SRS-PRI-P11-008, SRS-PRI-P11-009, SRS-PRI-P11-009, SRS-PRI-P11-010, SRS-PRI-P11-010, SRS-PRI-P11-011, SRS-PRI-P11-011, SRS-PRI-P12-002, SRS-PRI-P12-003, SRS-PRI-P12-005, SRS-PRI-P12-008, SRS-PRI-P14-002, SRS-PRI-P14-004, SRS-PRI-P14-007, SRS-PRI-P14-008, SRS-PRI-P15-002, SRS-PRI-P15-009, SRS-PRI-P16-006, SRS-PRI-P18-001, SRS-PRI-P18-002, SRS-PRI-P18-003, SRS-PRI-P18-004, SRS-PRI-P18-006, SRS-PRI-P18-007, SRS-PRI-P18-008, SRS-PRI-P18-009, SRS-PRI-P18-010, SRS-PRI-P18-011, SRS-PRI-P18-012, SRS-PRI-P18-013, SRS-PRI-P18-014, SRS-PRI-P18-015, SRS-PRI-P18-016, SRS-PRI-P18-017, SRS-PRI-P18-018, SRS-PRI-P18-020, SRS-PRI-P20-012, SRS-PRI-P20-013, SRS-PRI-P21-001, SRS-PRI-P21-002, SRS-PRI-P21-003, SRS-PRI-P21-004, SRS-PRI-P21-005, SRS-PRI-P21-006, SRS-PRI-P21-007, SRS-PRI-P21-008, SRS-PRI-P21-009, SRS-PRI-P21-010, SRS-PRI-P21-016, SRS-PRI-P21-017, SRS-PRI-P22-011, SRS-PRI-P22-012, SRS-PRI-P22-013, SRS-PRI-P22-015, SRS-PRI-P23-001, SRS-PRI-P23-002, SRS-PRI-P23-003, SRS-PRI-P23-004, SRS-PRI-P23-005, SRS-PRI-P23-006, SRS-PRI-P23-010, SRS-PRI-P23-013, SRS-PRI-P25-006, SRS-PRI-NF-005, SRS-PRI-NF-019, SRS-PRI-NF-026 |
| WAD — WebAppDocs | 48 | SRS-PRI-HW-002, SRS-PRI-SW-001, SRS-PRI-CM-001, SRS-PRI-CM-002, SRS-PRI-CM-008, SRS-PRI-UI-001, SRS-PRI-P2-008, SRS-PRI-P2-012, SRS-PRI-P4-001, SRS-PRI-P4-002, SRS-PRI-P4-003, SRS-PRI-P4-008, SRS-PRI-P5-008, SRS-PRI-P7-008, SRS-PRI-P7-035, SRS-PRI-P7-084, SRS-PRI-P10-003, SRS-PRI-P10-005, SRS-PRI-P10-010, SRS-PRI-P10-011, SRS-PRI-P10-015, SRS-PRI-P12-013, SRS-PRI-P12-015, SRS-PRI-P15-003, SRS-PRI-P15-006, SRS-PRI-P16-001, SRS-PRI-P17-002, SRS-PRI-P17-004, SRS-PRI-P18-003, SRS-PRI-P18-022, SRS-PRI-P19-001, SRS-PRI-P19-002, SRS-PRI-P19-003, SRS-PRI-P19-004, SRS-PRI-P19-006, SRS-PRI-P19-007, SRS-PRI-P19-008, SRS-PRI-P19-009, SRS-PRI-P19-014, SRS-PRI-P19-015, SRS-PRI-P19-016, SRS-PRI-P21-011, SRS-PRI-P21-012, SRS-PRI-P21-013, SRS-PRI-P21-014, SRS-PRI-P21-015, SRS-PRI-P22-005, SRS-PRI-P24-004 |
| Open-issue register | 39 | SRS-PRI-HW-003, SRS-PRI-P1-008, SRS-PRI-P1-015, SRS-PRI-P2-002, SRS-PRI-P4-010, SRS-PRI-P4-011, SRS-PRI-P4-016, SRS-PRI-P7-111, SRS-PRI-P7-112, SRS-PRI-P7-113, SRS-PRI-P7-114, SRS-PRI-P7-115, SRS-PRI-P7-116, SRS-PRI-P7-117, SRS-PRI-P7-118, SRS-PRI-P8-009, SRS-PRI-P10-016, SRS-PRI-P11-012, SRS-PRI-P11-013, SRS-PRI-P12-010, SRS-PRI-P12-011, SRS-PRI-P15-010, SRS-PRI-P15-011, SRS-PRI-P15-012, SRS-PRI-P15-013, SRS-PRI-P16-004, SRS-PRI-P16-005, SRS-PRI-P18-021, SRS-PRI-P19-012, SRS-PRI-P20-003, SRS-PRI-P20-006, SRS-PRI-P21-019, SRS-PRI-P22-001, SRS-PRI-P22-005, SRS-PRI-P22-007, SRS-PRI-P22-008, SRS-PRI-P24-001, SRS-PRI-P24-005, SRS-PRI-NF-006 |
| NEW — ME | 27 | SRS-PRI-CM-007, SRS-PRI-P1-013, SRS-PRI-P2-001, SRS-PRI-P2-002, SRS-PRI-P2-005, SRS-PRI-P2-007, SRS-PRI-P3-002, SRS-PRI-P3-003, SRS-PRI-P3-005, SRS-PRI-P3-006, SRS-PRI-P3-009, SRS-PRI-P3-011, SRS-PRI-P4-017, SRS-PRI-P7-104, SRS-PRI-P8-006, SRS-PRI-P8-007, SRS-PRI-P8-008, SRS-PRI-P8-010, SRS-PRI-P10-009, SRS-PRI-P10-016, SRS-PRI-P11-012, SRS-PRI-P11-013, SRS-PRI-P14-009, SRS-PRI-P14-010, SRS-PRI-P18-001, SRS-PRI-P21-001, SRS-PRI-NF-004 |
| UNSOURCED — open issue | 27 | SRS-PRI-HW-008, SRS-PRI-P1-016, SRS-PRI-P2-015, SRS-PRI-P3-013, SRS-PRI-P3-014, SRS-PRI-P3-015, SRS-PRI-P4-018, SRS-PRI-P5-025, SRS-PRI-P6-014, SRS-PRI-P8-011, SRS-PRI-P11-014, SRS-PRI-P12-018, SRS-PRI-P13-008, SRS-PRI-P14-015, SRS-PRI-P15-015, SRS-PRI-P16-010, SRS-PRI-P17-010, SRS-PRI-P18-025, SRS-PRI-P21-021, SRS-PRI-P22-016, SRS-PRI-P23-014, SRS-PRI-P24-010, SRS-PRI-P25-011, SRS-PRI-P25-012, SRS-PRI-NF-007, SRS-PRI-NF-015, SRS-PRI-NF-030 |
| CODE-P — legacy Primary firmware | 25 | SRS-PRI-CM-001, SRS-PRI-CM-002, SRS-PRI-CM-004, SRS-PRI-P1-001, SRS-PRI-P1-003, SRS-PRI-P1-011, SRS-PRI-P4-001, SRS-PRI-P4-002, SRS-PRI-P4-003, SRS-PRI-P4-004, SRS-PRI-P4-006, SRS-PRI-P4-014, SRS-PRI-P4-015, SRS-PRI-P5-008, SRS-PRI-P6-012, SRS-PRI-P12-006, SRS-PRI-P12-007, SRS-PRI-P14-001, SRS-PRI-P14-003, SRS-PRI-P14-005, SRS-PRI-P17-002, SRS-PRI-P18-018, SRS-PRI-P19-010, SRS-PRI-P21-011, SRS-PRI-P23-001 |
| Project brief | 22 | SRS-PRI-HW-001, SRS-PRI-HW-002, SRS-PRI-SW-002, SRS-PRI-P3-001, SRS-PRI-P5-001, SRS-PRI-P6-001, SRS-PRI-P6-002, SRS-PRI-P6-007, SRS-PRI-P6-008, SRS-PRI-P7-002, SRS-PRI-P7-100, SRS-PRI-P8-001, SRS-PRI-P8-002, SRS-PRI-P9-001, SRS-PRI-P9-009, SRS-PRI-P19-007, SRS-PRI-P20-001, SRS-PRI-P20-002, SRS-PRI-P20-006, SRS-PRI-P20-008, SRS-PRI-P20-009, SRS-PRI-NF-022 |
| CODE-S — legacy Secondary firmware | 21 | SRS-PRI-CM-003, SRS-PRI-CM-005, SRS-PRI-P1-002, SRS-PRI-P2-008, SRS-PRI-P9-002, SRS-PRI-P9-003, SRS-PRI-P9-004, SRS-PRI-P9-005, SRS-PRI-P9-006, SRS-PRI-P9-007, SRS-PRI-P9-008, SRS-PRI-P9-012, SRS-PRI-P9-013, SRS-PRI-P9-014, SRS-PRI-P10-004, SRS-PRI-P10-010, SRS-PRI-P12-004, SRS-PRI-P13-001, SRS-PRI-P17-003, SRS-PRI-P23-002, SRS-PRI-P23-003 |
| ICD — ME-ICD-001 (interface message) | 18 | SRS-PRI-P1-013, SRS-PRI-P2-012, SRS-PRI-P3-004, SRS-PRI-P3-005, SRS-PRI-P3-007, SRS-PRI-P3-008, SRS-PRI-P3-009, SRS-PRI-P3-011, SRS-PRI-P3-012, SRS-PRI-P4-002, SRS-PRI-P4-003, SRS-PRI-P4-007, SRS-PRI-P4-008, SRS-PRI-P4-009, SRS-PRI-P4-013, SRS-PRI-P4-014, SRS-PRI-P4-015, SRS-PRI-P4-016 |
| SOC — NXP IMX8MPIEC datasheet | 13 | SRS-PRI-HW-005, SRS-PRI-HW-007, SRS-PRI-P17-001, SRS-PRI-P20-002, SRS-PRI-P20-003, SRS-PRI-P22-009, SRS-PRI-P22-010, SRS-PRI-P24-005, SRS-PRI-P24-006, SRS-PRI-P24-007, SRS-PRI-P24-008, SRS-PRI-NF-019, SRS-PRI-NF-021 |
| LSRS — legacy combined SRS | 9 | SRS-PRI-HW-004, SRS-PRI-HW-007, SRS-PRI-P1-004, SRS-PRI-P1-007, SRS-PRI-P17-001, SRS-PRI-P25-002, SRS-PRI-P25-003, SRS-PRI-P25-004, SRS-PRI-P25-006 |
| Conflict register | 7 | SRS-PRI-P5-026, SRS-PRI-P7-093, SRS-PRI-P10-018, SRS-PRI-P12-001, SRS-PRI-P12-017, SRS-PRI-P20-017, SRS-PRI-NF-009 |
| Reserved decision D-0x | 6 | SRS-PRI-SW-002, SRS-PRI-P3-003, SRS-PRI-P9-001, SRS-PRI-P9-009, SRS-PRI-P9-010, SRS-PRI-P9-018 |
| CODE-C — legacy COM Controller | 3 | SRS-PRI-P25-002, SRS-PRI-P25-003, SRS-PRI-P25-004 |
| Constraint register | 3 | SRS-PRI-NF-020, SRS-PRI-NF-027, SRS-PRI-NF-028 |
| PSPE — prior primary-side design | 2 | SRS-PRI-P9-010, SRS-PRI-P14-001 |
| §4.22 | 2 | SRS-PRI-NF-031, SRS-PRI-NF-032 |
| Architect instruction 2026-07-27 | 1 | SRS-PRI-P25-001 |
| GAP — operator gap analysis | 1 | SRS-PRI-P5-001 |
| ME-ICD-001 | 1 | SRS-PRI-SW-003 |
| Modbus application protocol | 1 | SRS-PRI-P25-007 |
| Team engineering policy | 1 | SRS-PRI-NF-024 |
| architect instruction | 1 | SRS-PRI-NF-022 |
| brief | 1 | SRS-PRI-SW-001 |
| conflict **C-13** | 1 | SRS-PRI-P5-014 |
| conflict C-14 | 1 | SRS-PRI-P12-003 |
| §1.3 | 1 | SRS-PRI-P7-100 |
| §2.4 | 1 | SRS-PRI-UI-001 |
| §4.16 | 1 | SRS-PRI-P14-004 |
| §4.20 | 1 | SRS-PRI-NF-025 |
| §4.23 | 1 | SRS-PRI-NF-018 |
| §4.24 | 1 | SRS-PRI-NF-023 |
| §4.25 | 1 | SRS-PRI-P24-009 |
| §4.3 | 1 | SRS-PRI-P1-013 |
| §4.7.6 | 1 | SRS-PRI-P9-011 |

### A1.3 Requirements with no external source

These are either derived from other requirements in the same document, or are
placeholders recording an unsourced value. Both are legitimate; the second group
must be resolved before the SRS can be baselined.

| ID | Records | Open issue |
|---|---|---|
| SRS-PRI-HW-008 | <TBD-16> | #16 |
| SRS-PRI-P1-016 | <TBD-17> | #12 |
| SRS-PRI-P2-015 | <TBD-18> | #13 |
| SRS-PRI-P3-013 | <TBD-19> | #22 |
| SRS-PRI-P3-014 | <TBD-20> | #14 |
| SRS-PRI-P3-015 | <TBD-21> | #14 |
| SRS-PRI-P4-018 | <TBD-22> | #25 |
| SRS-PRI-P5-025 | <TBD-23> | #6 |
| SRS-PRI-P6-014 | <TBD-24> | #15 |
| SRS-PRI-P8-011 | <TBD-36> | #16 |
| SRS-PRI-P11-014 | <TBD-37> | #24 |
| SRS-PRI-P12-018 | <TBD-38> | #17 |
| SRS-PRI-P13-008 | <TBD-08> | #8 |
| SRS-PRI-P14-015 | <TBD-39> | — |
| SRS-PRI-P15-015 | <TBD-40> | — |
| SRS-PRI-P16-010 | <TBD-41> | #12 |
| SRS-PRI-P17-010 | <TBD-16> | #16 |
| SRS-PRI-P18-025 | <TBD-20> | #20 |
| SRS-PRI-P21-021 | <TBD-42> | #15 |
| SRS-PRI-P22-016 | <TBD-20> | #20 |
| SRS-PRI-P23-014 | <TBD-23a> | #23 |
| SRS-PRI-P24-010 | <TBD-10> | #10 |
| SRS-PRI-P25-011 | <TBD-43> | — |
| SRS-PRI-P25-012 | <TBD-44> | — |
| SRS-PRI-NF-007 | <TBD-46> | — |
| SRS-PRI-NF-015 | <TBD-47> | — |
| SRS-PRI-NF-030 | <TBD-26a> | #26 |

**27 requirements carry an unsourced value.**

---

## A2. Core Allocation — Open

Every requirement of the parent document is listed below with an **empty**
`Allocation` column, so that decision **D-01** can be resolved in a single pass.

Enter one of `A53`, `M7`, `A53+M7` or `N/A` against each row. The `Current` column
shows the value presently recorded in the parent; `TBD` means pending D-01.

> Filling this sheet also constrains **D-02** (CAN ownership) and **D-06**
> (inter-core mechanism), which should be recorded alongside it.

**Forced allocations (not open):** SRS-PRI-NF-022 — Forced (SOC)


### P1 — P1 Startup / init / self-test / shutdown

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P1-001 | complete initialization and reach a defined operating state after every application of supply… | TBD |  |
| SRS-PRI-P1-002 | place every controlled channel in the safe state before enabling any Secondary Board output | TBD |  |
| SRS-PRI-P1-003 | verify the integrity of its persistent configuration during initialization | TBD |  |
| SRS-PRI-P1-004 | apply factory default configuration values for any configuration item whose stored value fails… | TBD |  |
| SRS-PRI-P1-005 | verify the integrity of its persistent calibration data during initialization | TBD |  |
| SRS-PRI-P1-006 | raise a fault and remain out of the Ready state if calibration data fails its integrity check | TBD |  |
| SRS-PRI-P1-007 | verify that the real-time clock holds a plausible time during initialization, and shall raise… | TBD |  |
| SRS-PRI-P1-008 | execute a power-on self-test before entering the Ready state | TBD |  |
| SRS-PRI-P1-009 | report the outcome of every power-on self-test to the Web Application when the link is availab… | TBD |  |
| SRS-PRI-P1-010 | not enter the Ready state while any power-on self-test is failed | TBD |  |
| SRS-PRI-P1-011 | determine, during initialization, whether the preceding shutdown was orderly or was caused by… | TBD |  |
| SRS-PRI-P1-012 | make the reason for its most recent restart available to the Web Application | TBD |  |
| SRS-PRI-P1-013 | enumerate the connected Secondary Boards during initialization before accepting any program st… | TBD |  |
| SRS-PRI-P1-014 | report its own identity, hardware version, bootloader version and application software version… | TBD |  |
| SRS-PRI-P1-015 | accept a shutdown request and, on receiving one, bring every channel to the safe state, flush… | TBD |  |
| SRS-PRI-P1-016 | record `<TBD-17>` (the enumerated content of the power-on self-test for each subsystem) — see… | TBD |  |

### P2 — P2 Operating mode / state machine

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P2-001 | maintain a board-level operating state, distinct from and additional to the per-channel states | TBD |  |
| SRS-PRI-P2-002 | implement the board-level states Initializing, Ready, Running, Degraded, Fault and Maintenance | TBD |  |
| SRS-PRI-P2-003 | enter Ready when initialization completes with no failed self-test and no active board-level f… | TBD |  |
| SRS-PRI-P2-004 | enter Running when at least one channel is executing a program | TBD |  |
| SRS-PRI-P2-005 | enter Degraded when it can continue to control at least one channel but at least one enumerate… | TBD |  |
| SRS-PRI-P2-006 | enter Fault when it can no longer safely control any channel | TBD |  |
| SRS-PRI-P2-007 | maintain an independent operating state for each of the up to 8 channels | TBD |  |
| SRS-PRI-P2-008 | implement the per-channel states Idle, Charge, Discharge, Pause, Continue, Interrupt, Error, M… | TBD |  |
| SRS-PRI-P2-009 | implement the per-channel states Recharge, Deferred Start, Reset and Zero | TBD |  |
| SRS-PRI-P2-010 | permit a channel to change state only through a defined transition | TBD |  |
| SRS-PRI-P2-011 | reject any command that is not valid in the addressed channel's current state, and shall repor… | TBD |  |
| SRS-PRI-P2-012 | report every board-level and per-channel state change to the Web Application | TBD |  |
| SRS-PRI-P2-013 | record every board-level and per-channel state change in its event log | TBD |  |
| SRS-PRI-P2-014 | not allow a channel in the Error state to leave that state until its fault is cleared in accor… | TBD |  |
| SRS-PRI-P2-015 | record `<TBD-18>` (entry conditions, exit conditions and permitted operations for Maintenance… | TBD |  |

### P3 — P3 Secondary discovery, enumeration, presence

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P3-001 | support up to 8 Secondary Boards concurrently | TBD |  |
| SRS-PRI-P3-002 | determine, after initialization, which Secondary Boards are present | TBD |  |
| SRS-PRI-P3-003 | address each Secondary Board individually and unambiguously | TBD |  |
| SRS-PRI-P3-004 | obtain from each present Secondary Board its node identity, hardware version, bootloader versi… | TBD |  |
| SRS-PRI-P3-005 | detect the presence of two Secondary Boards claiming the same node identity, and shall raise a… | TBD |  |
| SRS-PRI-P3-006 | not dispatch any control command to a Secondary Board that it has not successfully enumerated | TBD |  |
| SRS-PRI-P3-007 | monitor the presence of every enumerated Secondary Board continuously while that board is enum… | TBD |  |
| SRS-PRI-P3-008 | declare an enumerated Secondary Board absent when no valid message has been received from it w… | TBD |  |
| SRS-PRI-P3-009 | report every change in a Secondary Board's presence to the Web Application | TBD |  |
| SRS-PRI-P3-010 | record every change in a Secondary Board's presence in its event log | TBD |  |
| SRS-PRI-P3-011 | make the current presence and identity of all 8 channel positions available to the Web Applica… | TBD |  |
| SRS-PRI-P3-012 | verify that a Secondary Board's reported application software version is compatible with its o… | TBD |  |
| SRS-PRI-P3-013 | record `<TBD-19>` (how a Secondary Board acquires its node identity: strap/DIP, Primary assign… | TBD |  |
| SRS-PRI-P3-014 | record `<TBD-20>` (whether Secondary Boards may be added or removed while the Primary is runni… | TBD |  |
| SRS-PRI-P3-015 | record `<TBD-21>` (whether a program may be started when fewer than the configured number of S… | TBD |  |

### P4 — P4 Program reception

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P4-001 | accept a test program transferred from the Web Application | TBD |  |
| SRS-PRI-P4-002 | indicate to the Web Application, on request, whether it is ready to receive a program | TBD |  |
| SRS-PRI-P4-003 | accept program metadata comprising at least program identity, program version, program name an… | TBD |  |
| SRS-PRI-P4-004 | accept a program in multiple transfer units and reassemble it in the correct order | TBD |  |
| SRS-PRI-P4-005 | detect a missing, duplicated or out-of-order transfer unit during program reception and shall… | TBD |  |
| SRS-PRI-P4-006 | verify the integrity of a received program before accepting it | TBD |  |
| SRS-PRI-P4-007 | reject a program whose received step count does not equal the step count declared in its metad… | TBD |  |
| SRS-PRI-P4-008 | report the outcome of every program transfer to the Web Application, distinguishing acceptance… | TBD |  |
| SRS-PRI-P4-009 | not overwrite or modify a program that is currently executing | TBD |  |
| SRS-PRI-P4-010 | store an accepted program in non-volatile storage | TBD |  |
| SRS-PRI-P4-011 | retain stored programs across a restart | TBD |  |
| SRS-PRI-P4-012 | accept battery parameter data associated with a program, comprising nominal capacity, number o… | TBD |  |
| SRS-PRI-P4-013 | accept a registration format definition associated with a program | TBD |  |
| SRS-PRI-P4-014 | make the metadata of every stored program available to the Web Application on request | TBD |  |
| SRS-PRI-P4-015 | return the content of a stored program to the Web Application on request | TBD |  |
| SRS-PRI-P4-016 | reject a program that would exceed its program storage capacity, and shall report the rejection | TBD |  |
| SRS-PRI-P4-017 | accept a program while other channels are executing programs | TBD |  |
| SRS-PRI-P4-018 | record `<TBD-22>` (the number of programs that shall be concurrently resident, and whether pro… | TBD |  |

### P5 — P5 Program parsing & data model

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P5-001 | decode a received program into an internal representation suitable for execution, without assi… | TBD |  |
| SRS-PRI-P5-002 | represent each program step with the fields Label, Operator, Nominal Value, Limit, Action and… | TBD |  |
| SRS-PRI-P5-003 | support more than one Limit, each with its own Action, within a single step | TBD |  |
| SRS-PRI-P5-004 | support more than one Nominal Value within a single step | TBD |  |
| SRS-PRI-P5-005 | support more than one Registration specification within a single step, and shall treat their o… | TBD |  |
| SRS-PRI-P5-006 | interpret a step whose Label field contains an exclamation mark as a comment or disabled step,… | TBD |  |
| SRS-PRI-P5-007 | validate every decoded step against the operator, nominal-value, limit, action and registratio… | TBD |  |
| SRS-PRI-P5-008 | reject a program containing an unrecognised operator, and shall identify the offending step | TBD |  |
| SRS-PRI-P5-009 | reject a program containing a step with an operator/column combination that the operator does… | TBD |  |
| SRS-PRI-P5-010 | reject a program in which a charge, recharge or discharge step specifies a voltage nominal val… | TBD |  |
| SRS-PRI-P5-011 | reject a program containing a `GOTO` whose destination label does not exist at the same progra… | TBD |  |
| SRS-PRI-P5-012 | reject a program that uses any of the reserved label names listed in §4.7.9 | TBD |  |
| SRS-PRI-P5-013 | reject a program containing an unterminated cycle or a `CYC` without a matching `BEG` | TBD |  |
| SRS-PRI-P5-014 | reject a program whose cycle nesting exceeds the supported depth | TBD |  |
| SRS-PRI-P5-015 | reject a program that references a procedure that is not available to it | TBD |  |
| SRS-PRI-P5-016 | reject a program that references a nominal-value table that is not available to it | TBD |  |
| SRS-PRI-P5-017 | reject a program that references a registration format that is not available to it | TBD |  |
| SRS-PRI-P5-018 | report every program validation failure to the Web Application with the offending step number… | TBD |  |
| SRS-PRI-P5-019 | resolve every battery-parameter-relative nominal value and limit to an absolute value using th… | TBD |  |
| SRS-PRI-P5-020 | reject a program that uses a battery-parameter-relative value for which the required battery p… | TBD |  |
| SRS-PRI-P5-021 | support program variables defined by the `SET` operator, and shall substitute a variable's cur… | TBD |  |
| SRS-PRI-P5-022 | apply an assignment to a variable whose name matches a channel unit to that channel's counter | TBD |  |
| SRS-PRI-P5-023 | support the use of a variable as the repetition count of a cycle | TBD |  |
| SRS-PRI-P5-024 | represent every setpoint with sufficient numeric range and resolution to express the full conf… | TBD |  |
| SRS-PRI-P5-025 | record `<TBD-23>` (whether battery-parameter-relative values are resolved by the Primary at ru… | TBD |  |
| SRS-PRI-P5-026 | record `<TBD-13>` (whether the supported cycle structure is 16 distinct cycles, 16 levels of n… | TBD |  |

### P6 — P6 Program scheduling & assignment

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P6-001 | accept an assignment of a stored program to one or more identified channels | TBD |  |
| SRS-PRI-P6-002 | accept assignments for several channels within a single request | TBD |  |
| SRS-PRI-P6-003 | reject an assignment addressed to a channel whose Secondary Board is not present | TBD |  |
| SRS-PRI-P6-004 | reject an assignment addressed to a channel that is currently executing a program | TBD |  |
| SRS-PRI-P6-005 | associate with each assignment the battery parameter data and registration format to be used f… | TBD |  |
| SRS-PRI-P6-006 | maintain, for each channel, the identity and version of the assigned program | TBD |  |
| SRS-PRI-P6-007 | permit the same program to be assigned to more than one channel concurrently | TBD |  |
| SRS-PRI-P6-008 | permit different programs to be assigned to different channels concurrently | TBD |  |
| SRS-PRI-P6-009 | accept a queue of assignments for a channel and execute them in the order given, starting the… | TBD |  |
| SRS-PRI-P6-010 | continue with the next queued assignment for a channel after the current program terminates by… | TBD |  |
| SRS-PRI-P6-011 | accept a deferred start time for an assignment and shall not begin execution before that time | TBD |  |
| SRS-PRI-P6-012 | accept a start-from-step instruction for an assignment and shall begin execution at the identi… | TBD |  |
| SRS-PRI-P6-013 | report the assignment state of every channel to the Web Application | TBD |  |
| SRS-PRI-P6-014 | record `<TBD-24>` (whether a single program instance may span several channels as one coordina… | TBD |  |

### P7 — P7 Step execution engine

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P7-001 | execute the steps of an assigned program in ascending step order except where an Action, `GOTO… | TBD |  |
| SRS-PRI-P7-002 | execute the step sequencing itself and shall not delegate step sequencing to any Secondary Boa… | TBD |  |
| SRS-PRI-P7-003 | The Primary Board shall, on entering a step, establish that step's nominal values, limits, act… | TBD |  |
| SRS-PRI-P7-004 | evaluate every active limit of the current step at a rate of at least once per **100 ms** | TBD |  |
| SRS-PRI-P7-005 | terminate the running step when any active limit of that step is met, except where the associa… | TBD |  |
| SRS-PRI-P7-006 | execute the Action associated with the limit that was met | TBD |  |
| SRS-PRI-P7-007 | proceed to the next step when the Action associated with the met limit is empty | TBD |  |
| SRS-PRI-P7-008 | maintain, for each channel, the current step number, the elapsed step time and the elapsed pro… | TBD |  |
| SRS-PRI-P7-009 | maintain, for each channel, the counters `Ah`, `Wh`, `AhStep`, `WhStep`, `AhPrev`, `WhPrev`, `… | TBD |  |
| SRS-PRI-P7-010 | compute `AhStat` as the accumulated charge ampere-hours divided by the charge factor, less the… | TBD |  |
| SRS-PRI-P7-011 | compute `AhBal` as accumulated charge ampere-hours less accumulated discharge ampere-hours, cl… | TBD |  |
| SRS-PRI-P7-012 | permit `Ah` and `Wh` to take negative values when discharge predominates | TBD |  |
| SRS-PRI-P7-013 | reset the step counters `AhStep` and `WhStep` at the start of each step, and shall carry their… | TBD |  |
| SRS-PRI-P7-014 | support a minimum step duration of **0.1 s** | TBD |  |
| SRS-PRI-P7-015 | implement operator `PAU`, holding the channel with no energy transfer and with power contactor… | TBD |  |
| SRS-PRI-P7-016 | implement operator `CHA`, charging the battery to the step's nominal value until the step's li… | TBD |  |
| SRS-PRI-P7-017 | implement operator `RCH`, behaving as `CHA` and additionally setting the recharge relay and ca… | TBD |  |
| SRS-PRI-P7-018 | implement operator `DCH`, discharging the battery to the step's nominal value until the step's… | TBD |  |
| SRS-PRI-P7-019 | implement operator `INT`, interrupting the program and holding the channel until an explicit c… | TBD |  |
| SRS-PRI-P7-020 | implement operator `STO`, terminating the running program on that channel | TBD |  |
| SRS-PRI-P7-021 | The Primary Board shall, on `STO`, continue with the next queued assignment for that channel a… | TBD |  |
| SRS-PRI-P7-022 | implement operator `GOTO`, transferring execution to the step whose Label matches the given de… | TBD |  |
| SRS-PRI-P7-023 | accept a `GOTO` destination given in the Nominal Value column when `GOTO` is used as an operat… | TBD |  |
| SRS-PRI-P7-024 | permit a `GOTO` to enter or leave a cycle | TBD |  |
| SRS-PRI-P7-025 | not permit a `GOTO` to cross into or out of a procedure | TBD |  |
| SRS-PRI-P7-026 | implement operator `RET`, returning from a procedure to the next higher program level | TBD |  |
| SRS-PRI-P7-027 | implement operator `ALIM`, causing all subsequent current limits expressed in `A` or `mA` to r… | TBD |  |
| SRS-PRI-P7-028 | The Primary Board shall, in the absence of `ALIM`, evaluate current limits expressed in `A` or… | TBD |  |
| SRS-PRI-P7-029 | implement operator `BEG`, marking the beginning of a cycle and binding the cycle name given in… | TBD |  |
| SRS-PRI-P7-030 | implement operator `CYC`, marking the end of a cycle and repeating the enclosed steps the numb… | TBD |  |
| SRS-PRI-P7-031 | maintain an independent repetition counter for each cycle in a program | TBD |  |
| SRS-PRI-P7-032 | support interlaced cycles | TBD |  |
| SRS-PRI-P7-033 | accept a cycle repetition count supplied as a program variable | TBD |  |
| SRS-PRI-P7-034 | accept a preset value for a named cycle's counter established by a `SET` operator, and shall b… | TBD |  |
| SRS-PRI-P7-035 | report the current cycle number of each running channel to the Web Application | TBD |  |
| SRS-PRI-P7-036 | implement procedures as named reusable step sequences invocable by name in the Operator column | TBD |  |
| SRS-PRI-P7-037 | The Primary Board shall, on completing a procedure invoked from a step, continue with the step… | TBD |  |
| SRS-PRI-P7-038 | The Primary Board shall, on completing a procedure invoked as an Action, continue with the ste… | TBD |  |
| SRS-PRI-P7-039 | maintain the program level of execution so that a registration-format change made inside a pro… | TBD |  |
| SRS-PRI-P7-040 | report, for a channel executing a procedure step, both the procedure step being executed and t… | TBD |  |
| SRS-PRI-P7-041 | implement operator `SET` for the assignment of registration formats, counters, variables, time… | TBD |  |
| SRS-PRI-P7-042 | implement operator `REG`, recording one registration in the format named in that step | TBD |  |
| SRS-PRI-P7-043 | The Primary Board shall, for a `REG` step with no registration format named, use the format mo… | TBD |  |
| SRS-PRI-P7-044 | The Primary Board shall, for a `REG` step naming a test section in the Nominal Value column, r… | TBD |  |
| SRS-PRI-P7-045 | implement operator `FILE`, directing subsequent registrations of that channel into the test se… | TBD |  |
| SRS-PRI-P7-046 | reject a program in which operator `FILE` names a test section as `Test` without a distinguish… | TBD |  |
| SRS-PRI-P7-047 | The Primary Board shall, when `FILE` is used inside a cycle with the same nominal value as the… | TBD |  |
| SRS-PRI-P7-048 | implement operator `ADD`, adding the values of the channels named in the Nominal Value column | TBD |  |
| SRS-PRI-P7-049 | implement operator `SUB`, subtracting the value of the second named channel from the first | TBD |  |
| SRS-PRI-P7-050 | implement operator `CLEAR`, deleting all registrations, limits and global setpoints previously… | TBD |  |
| SRS-PRI-P7-051 | implement operator `SAVE`, storing all counters of the channels in the current registration fo… | TBD |  |
| SRS-PRI-P7-052 | implement operator `REST`, restoring counters previously stored by `SAVE` from the test sectio… | TBD |  |
| SRS-PRI-P7-053 | restore under `REST` only those channels present in the registration format declared before th… | TBD |  |
| SRS-PRI-P7-054 | The Primary Board shall, when the data required by a `REST` step is unavailable, either hold t… | TBD |  |
| SRS-PRI-P7-055 | implement operator `SETMUX`, enabling the additional multiplexer channels for serial data acqu… | TBD |  |
| SRS-PRI-P7-056 | implement operator `FILTER`, applying the named digital filter to the channels of the attribut… | TBD |  |
| SRS-PRI-P7-057 | record `<TBD-25>` (the definition of the available digital filters, which BM states may be def… | TBD |  |
| SRS-PRI-P7-058 | implement operator `ERR`, recording the numbered message given in the Nominal Value column, in… | TBD |  |
| SRS-PRI-P7-059 | The Primary Board shall, on receiving a continue command after an `ERR`, resume execution at t… | TBD |  |
| SRS-PRI-P7-060 | implement operator `MSG`, recording the numbered message given in the Nominal Value column wit… | TBD |  |
| SRS-PRI-P7-061 | support `ERR` and `MSG` used as Actions as well as operators | TBD |  |
| SRS-PRI-P7-062 | The Primary Board shall, where a step has two limits and the limit carrying `ERR` or `MSG` is… | TBD |  |
| SRS-PRI-P7-063 | apply the same `ERR` and `MSG` semantics to global limits as to step limits | TBD |  |
| SRS-PRI-P7-064 | hold a message table associating message numbers with message texts | TBD |  |
| SRS-PRI-P7-065 | provide by default the messages numbered 1 "Limit reached!", 2 "Voltage limit reached!", 3 "Te… | TBD |  |
| SRS-PRI-P7-066 | accept additions and modifications to the message table from the Web Application | TBD |  |
| SRS-PRI-P7-067 | implement operator `TASK`, activating a named process that runs in parallel with the program o… | TBD |  |
| SRS-PRI-P7-068 | support at least **12** concurrent parallel processes per program | TBD |  |
| SRS-PRI-P7-069 | implement task `OUW`, enabling the calculation of resistance and power from the measured volta… | TBD |  |
| SRS-PRI-P7-070 | implement tasks `CGRE`, `CLESS` and `TCONTR`, controlling a temperature-dependent relay output | TBD |  |
| SRS-PRI-P7-071 | reject a program in which a safety-related task is invoked by `TASK` before operator `PARALLEL… | TBD |  |
| SRS-PRI-P7-072 | implement operator `PROT`, starting an external reporting process and passing it the circuit i… | TBD |  |
| SRS-PRI-P7-073 | report, during execution of a `PROT` step and in the registration data, that the `PROT` comman… | TBD |  |
| SRS-PRI-P7-074 | implement operator `BATT`, presenting a constant voltage with an upper and a lower current lim… | TBD |  |
| SRS-PRI-P7-075 | implement operator `TABLE`, executing the sequence of setpoints held in the named nominal-valu… | TBD |  |
| SRS-PRI-P7-076 | interpret each nominal-value table line as an ordered set of duration, current, power and volt… | TBD |  |
| SRS-PRI-P7-077 | interpret a nominal-value table duration without an explicit unit as seconds, and shall accept… | TBD |  |
| SRS-PRI-P7-078 | treat a nominal-value table line specifying only duration and voltage as a charge step irrespe… | TBD |  |
| SRS-PRI-P7-079 | treat negative current and power values in a nominal-value table as discharge | TBD |  |
| SRS-PRI-P7-080 | apply the scaling factors `Factor_I`, `Factor_P` and `Factor_U` to the corresponding values of… | TBD |  |
| SRS-PRI-P7-081 | apply the table clamps `Ap`, `An`, `Vp`, `Vn`, `Wp` and `Wn` when present, limiting table-deri… | TBD |  |
| SRS-PRI-P7-082 | The Primary Board shall, where a table clamp is absent, limit table-derived setpoints to the v… | TBD |  |
| SRS-PRI-P7-083 | restart a nominal-value table from its beginning when the table is exhausted and no limit of t… | TBD |  |
| SRS-PRI-P7-084 | report the current table step number of a running channel to the Web Application | TBD |  |
| SRS-PRI-P7-085 | implement operator `RANGE`, selecting the measurement and control range of a channel | TBD |  |
| SRS-PRI-P7-086 | enable automatic range selection at every program start | TBD |  |
| SRS-PRI-P7-087 | The Primary Board shall, under automatic range selection, choose for each new step the range w… | TBD |  |
| SRS-PRI-P7-088 | The Primary Board shall, under automatic range selection and in the absence of a current setpo… | TBD |  |
| SRS-PRI-P7-089 | The Primary Board shall, on a `RANGE` step naming a range number, select that range manually a… | TBD |  |
| SRS-PRI-P7-090 | not change range within a step | TBD |  |
| SRS-PRI-P7-091 | not apply automatic range selection to steps whose nominal value is a ramp, a power value, or… | TBD |  |
| SRS-PRI-P7-092 | effect a range change at the beginning of the next step, having first commanded the channel cu… | TBD |  |
| SRS-PRI-P7-093 | record `<TBD-12>` (the number of ranges, their boundaries as a proportion of maximum current,… | TBD |  |
| SRS-PRI-P7-094 | implement operator `PARALLEL`, reserving the channels named in the Nominal Value column for pa… | TBD |  |
| SRS-PRI-P7-095 | reject a `PARALLEL` operation in which any named channel is not in the stopped state, and shal… | TBD |  |
| SRS-PRI-P7-096 | divide the nominal value of a step among all channels participating in a parallel group | TBD |  |
| SRS-PRI-P7-097 | not permit a channel reserved for parallel operation to be started by another assignment while… | TBD |  |
| SRS-PRI-P7-098 | retain a parallel reservation after the master program terminates normally, and shall release… | TBD |  |
| SRS-PRI-P7-099 | report a distinct reason for each defined parallel-operation failure, namely current below set… | TBD |  |
| SRS-PRI-P7-100 | support a parallel group of up to 8 channels | TBD |  |
| SRS-PRI-P7-101 | implement operator `SYNCLine`, holding a channel at that step until every channel of its synch… | TBD |  |
| SRS-PRI-P7-102 | implement operator `SYNCProgram`, behaving as `SYNCLine` but synchronising only those channels… | TBD |  |
| SRS-PRI-P7-103 | maintain a synchronisation group membership for each channel | TBD |  |
| SRS-PRI-P7-104 | release a synchronisation barrier and report the condition when a member channel of the group… | TBD |  |
| SRS-PRI-P7-105 | implement operator `EIS`, executing an impedance-spectroscopy step with the parameters `ADC`,… | TBD |  |
| SRS-PRI-P7-106 | interpret a single frequency value in an `EIS` step as a measurement at that frequency, and tw… | TBD |  |
| SRS-PRI-P7-107 | apply the `EIS` parameter defaults `AAcMax` 2 A, `VAcMax` 20 V, `VAcMin` −2 V, `mVideal` 10 mV… | TBD |  |
| SRS-PRI-P7-108 | treat an `EIS` step with `ADC` absent or zero as a pause step, a positive `ADC` as charge and… | TBD |  |
| SRS-PRI-P7-109 | reject an `EIS` step defining `VDC` without `ADC` | TBD |  |
| SRS-PRI-P7-110 | reject an `EIS` step whose time limit is not greater than the duration of the specified measur… | TBD |  |
| SRS-PRI-P7-111 | implement operator `PAUA`, whose detailed behaviour is `<TBD-26>` | TBD |  |
| SRS-PRI-P7-112 | implement operator `PAUO`, whose detailed behaviour is `<TBD-27>` | TBD |  |
| SRS-PRI-P7-113 | implement operator `IRANGE`, selecting the current measurement and control range, whose detail… | TBD |  |
| SRS-PRI-P7-114 | implement operator `URANGE`, selecting the voltage measurement and control range, whose detail… | TBD |  |
| SRS-PRI-P7-115 | implement operator `OUTA`, controlling a digital output from the program, whose detailed behav… | TBD |  |
| SRS-PRI-P7-116 | implement operator `OUTB`, controlling a digital output from the program, whose detailed behav… | TBD |  |
| SRS-PRI-P7-117 | implement operator `ISOEXT`, whose detailed behaviour is `<TBD-32>` | TBD |  |
| SRS-PRI-P7-118 | implement operator `ISOINT`, whose detailed behaviour is `<TBD-33>` | TBD |  |
| SRS-PRI-P7-119 | The Primary Board shall, when the range selected by `IRANGE` or `URANGE` differs from the acti… | TBD |  |
| SRS-PRI-P7-120 | support constant-current nominal values expressed in `A` and `mA` | TBD |  |
| SRS-PRI-P7-121 | support constant-voltage nominal values expressed in `V` | TBD |  |
| SRS-PRI-P7-122 | support constant-power nominal values expressed in `Watt`, and shall not accept `W` as the pow… | TBD |  |
| SRS-PRI-P7-123 | support constant-resistance nominal values expressed in `Ohm` | TBD |  |
| SRS-PRI-P7-124 | support the battery-relative current nominal values `ACn1`, `ACn2`, `ACn4`, `ACn5`, `ACn10` an… | TBD |  |
| SRS-PRI-P7-125 | support the battery-relative voltage nominal value `VnC`, resolving it as the given volts per… | TBD |  |
| SRS-PRI-P7-126 | not accept a battery-relative nominal value as a ramp endpoint | TBD |  |
| SRS-PRI-P7-127 | support ramp nominal values defined by a start value, an end value and a ramp duration, for cu… | TBD |  |
| SRS-PRI-P7-128 | The Primary Board shall, where a ramp completes before the step's limit is met, hold the ramp… | TBD |  |
| SRS-PRI-P7-129 | The Primary Board shall, where the step's limit is met before the ramp completes, terminate th… | TBD |  |
| SRS-PRI-P7-130 | support two simultaneous nominal values in one step and shall command the channel so that neit… | TBD |  |
| SRS-PRI-P7-131 | The Primary Board shall, for a charge step with current and voltage nominal values, regulate c… | TBD |  |
| SRS-PRI-P7-132 | The Primary Board shall, for a discharge step with power and voltage nominal values, regulate… | TBD |  |
| SRS-PRI-P7-133 | support the timers `TIMER1`, `TIMER2` and `TIMER3`, set and started by a `SET` operator and ru… | TBD |  |
| SRS-PRI-P7-134 | support the global limits `MaxCHAI`, `MaxDCHI`, `MaxChaU`, `MaxDchU`, `MaxChaW` and `MaxDchW`,… | TBD |  |
| SRS-PRI-P7-135 | regulate the channel to a global nominal value when a step's own setpoint would otherwise exce… | TBD |  |
| SRS-PRI-P7-136 | terminate the program when a global limit with no associated Action is reached | TBD |  |
| SRS-PRI-P7-137 | support an elapsed-step-time limit expressed in seconds, minutes or hours, or as hours, minute… | TBD |  |
| SRS-PRI-P7-138 | interpret a time limit as elapsed time within the program step and not as time of day | TBD |  |
| SRS-PRI-P7-139 | support a lower limit on any registration channel, met when that channel's value falls below t… | TBD |  |
| SRS-PRI-P7-140 | support an upper limit on any registration channel, met when that channel's value rises above… | TBD |  |
| SRS-PRI-P7-141 | support a delta limit, met when the change in a channel's value since the start of the step ex… | TBD |  |
| SRS-PRI-P7-142 | support the conjunction of two or more limits, met only when every conjoined condition is sati… | TBD |  |
| SRS-PRI-P7-143 | associate the Action of a conjoined limit chain with the last condition of that chain | TBD |  |
| SRS-PRI-P7-144 | treat limit channels grouped by a common unit as conjoined when used within a conjoined limit… | TBD |  |
| SRS-PRI-P7-145 | support the gradient limits `GradT` and `GradTm`, met when temperature rises by more than the… | TBD |  |
| SRS-PRI-P7-146 | support the gradient limits `GradU`, `GradUm` and `GradI`, met when the corresponding quantity… | TBD |  |
| SRS-PRI-P7-147 | re-establish the gradient reference value once per hour for hour-based gradient limits and onc… | TBD |  |
| SRS-PRI-P7-148 | evaluate gradient limits against every new measurement sample | TBD |  |
| SRS-PRI-P7-149 | support the limit `deltaV`, evaluating the rate of change of the voltage channel or of a desig… | TBD |  |
| SRS-PRI-P7-150 | support a configurable time base for `deltaV`, expressed in milliseconds as the interval betwe… | TBD |  |
| SRS-PRI-P7-151 | support `deltaV` evaluation across a designated contiguous set of general real channels, ident… | TBD |  |
| SRS-PRI-P7-152 | support the timer limits `Timer1`, `Timer2` and `Timer3`, met when the corresponding timer exp… | TBD |  |
| SRS-PRI-P7-153 | skip a step whose only limit is a timer that has already expired, and shall continue with the… | TBD |  |
| SRS-PRI-P7-154 | support the limit `AhDef`, recording the present ampere-hour value as the reference capacity a… | TBD |  |
| SRS-PRI-P7-155 | record the `AhDef` reference capacity as an unsigned value | TBD |  |
| SRS-PRI-P7-156 | support the limit `PercAh`, met when the ampere-hour counter reaches the stated percentage of… | TBD |  |
| SRS-PRI-P7-157 | support the limit `PERCCN_P`, met when the capacity accumulated at the end of the previous ste… | TBD |  |
| SRS-PRI-P7-158 | support the limit `PERCCN_C`, met when the capacity accumulated in the present step reaches th… | TBD |  |
| SRS-PRI-P7-159 | evaluate `PERCCN_P` and `PERCCN_C` without regard to the sign of the ampere-hour value | TBD |  |
| SRS-PRI-P7-160 | reject a program using the limits `AhDef`, `PercAh`, `PERCCN_P` or `PERCCN_C` with any operato… | TBD |  |
| SRS-PRI-P7-161 | support the limits `A_notAbs` and `mA_notAbs`, evaluating current limits with regard to the si… | TBD |  |
| SRS-PRI-P7-162 | support the derived limits `ACN5`, `VNC`, `OHM` and `WATT`, each evaluated from the present me… | TBD |  |
| SRS-PRI-P7-163 | support the limits `ABATT` and `VBATT` | TBD |  |
| SRS-PRI-P7-164 | record `<TBD-34>` (the definition of limits `ABATT` and `VBATT`, described in BM only as custo… | TBD |  |
| SRS-PRI-P7-165 | permit any of the battery parameters of §4.4 to be used as a limit value | TBD |  |
| SRS-PRI-P7-166 | The Primary Board shall, for a met limit with an empty Action, execute the next step | TBD |  |
| SRS-PRI-P7-167 | The Primary Board shall, for a met limit with Action `INT`, interrupt the program and hold the… | TBD |  |
| SRS-PRI-P7-168 | The Primary Board shall, for a met limit with Action `STO`, stop the program on that channel | TBD |  |
| SRS-PRI-P7-169 | The Primary Board shall, for a met limit with Action `GOTO`, transfer execution to the step be… | TBD |  |
| SRS-PRI-P7-170 | The Primary Board shall, for a met limit whose Action names a procedure, execute that procedur… | TBD |  |
| SRS-PRI-P7-171 | support conditional branching by permitting each limit of a step to carry a distinct `GOTO` Ac… | TBD |  |
| SRS-PRI-P7-172 | record a registration at the beginning and at the end of every step for which no registration… | TBD |  |
| SRS-PRI-P7-173 | support a delta registration trigger, recording whenever a named channel changes by the stated… | TBD |  |
| SRS-PRI-P7-174 | support a periodic registration trigger, recording at the stated time interval | TBD |  |
| SRS-PRI-P7-175 | support a threshold registration trigger, recording when a named channel crosses the stated va… | TBD |  |
| SRS-PRI-P7-176 | support a gated delta registration trigger, applying a delta or periodic trigger only after a… | TBD |  |
| SRS-PRI-P7-177 | support a bounded-count registration trigger, recording exactly the stated number of registrat… | TBD |  |
| SRS-PRI-P7-178 | support a registration resolution of **0.1 s** | TBD |  |
| SRS-PRI-P7-179 | combine multiple registration triggers within one step, recording whenever any of them is sati… | TBD |  |
| SRS-PRI-P7-180 | apply the registration format named in a step's Registration column to that step only, and sha… | TBD |  |
| SRS-PRI-P7-181 | support the registration control variable `RLevel` with the values 0, 1, 2, 3 and 4 | TBD |  |
| SRS-PRI-P7-182 | The Primary Board shall, for `RLevel` 0, suppress all registrations | TBD |  |
| SRS-PRI-P7-183 | The Primary Board shall, for `RLevel` 1, record step-change registrations only | TBD |  |
| SRS-PRI-P7-184 | The Primary Board shall, for `RLevel` 2, record both data and step-change registrations | TBD |  |
| SRS-PRI-P7-185 | The Primary Board shall, for `RLevel` 3, suppress step-change registrations and record data re… | TBD |  |
| SRS-PRI-P7-186 | The Primary Board shall, for `RLevel` 4, suppress step-change registrations and record no data… | TBD |  |
| SRS-PRI-P7-187 | accept a change of `RLevel` at any point in a program and apply it from that point | TBD |  |
| SRS-PRI-P7-188 | support the special label `ONERROR`, transferring execution to the step bearing the named labe… | TBD |  |
| SRS-PRI-P7-189 | execute all steps from an `ONERROR` destination up to the next `STO` | TBD |  |
| SRS-PRI-P7-190 | support the special label `ONEXIT`, executing the steps under the named label after the progra… | TBD |  |
| SRS-PRI-P7-191 | reject a program whose `ONERROR` handler does not begin with a `CLEAR` step | TBD |  |
| SRS-PRI-P7-192 | reject a program that uses any of the following as a label: `V, A, mA, Ah, mAh, AhCha, AhLad,… | TBD |  |

### P8 — P8 Concurrent execution of up to 8

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P8-001 | execute programs on up to 8 channels concurrently | TBD |  |
| SRS-PRI-P8-002 | execute each channel's program independently of the others except where `PARALLEL`, `SYNCLine`… | TBD |  |
| SRS-PRI-P8-003 | maintain a separate execution context per channel, comprising at least the current step, step… | TBD |  |
| SRS-PRI-P8-004 | meet the 100 ms limit-evaluation rate of SRS-PRI-P7-004 for every running channel simultaneous… | TBD |  |
| SRS-PRI-P8-005 | meet the 0.1 s registration resolution of SRS-PRI-P7-178 for every running channel simultaneou… | TBD |  |
| SRS-PRI-P8-006 | not allow the termination of a program on one channel to affect the execution of programs on o… | TBD |  |
| SRS-PRI-P8-007 | not allow a fault on one channel to affect the execution of programs on other channels, except… | TBD |  |
| SRS-PRI-P8-008 | not allow the loss of one Secondary Board to affect the execution of programs on other channel… | TBD |  |
| SRS-PRI-P8-009 | start programs on channels named in a single multi-channel start request such that the differe… | TBD |  |
| SRS-PRI-P8-010 | report the execution state of all 8 channel positions to the Web Application as a single aggre… | TBD |  |
| SRS-PRI-P8-011 | record `<TBD-36>` (whether a multi-channel assignment runs as independent parallel instances o… | TBD |  |

### P9 — P9 Command dispatch to Secondaries

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P9-001 | dispatch to each Secondary Board only the control data necessary for that board to charge or d… | TBD |  |
| SRS-PRI-P9-002 | command a Secondary Board to enter charge mode | TBD |  |
| SRS-PRI-P9-003 | command a Secondary Board to enter discharge mode | TBD |  |
| SRS-PRI-P9-004 | command a Secondary Board to enter pause, with no energy transfer | TBD |  |
| SRS-PRI-P9-005 | command a Secondary Board to stop | TBD |  |
| SRS-PRI-P9-006 | command a Secondary Board to interrupt | TBD |  |
| SRS-PRI-P9-007 | command a Secondary Board to continue from interrupt or pause | TBD |  |
| SRS-PRI-P9-008 | command a Secondary Board to resume after a power-fail event | TBD |  |
| SRS-PRI-P9-009 | command the setpoint to be regulated by a Secondary Board, together with the regulation mode t… | TBD |  |
| SRS-PRI-P9-010 | command the safety limits to be enforced locally by a Secondary Board | TBD |  |
| SRS-PRI-P9-011 | command the measurement and control range to be applied by a Secondary Board | TBD |  |
| SRS-PRI-P9-012 | command the state of a Secondary Board's digital outputs | TBD |  |
| SRS-PRI-P9-013 | confirm that each dispatched command has been accepted by the addressed Secondary Board before… | TBD |  |
| SRS-PRI-P9-014 | detect the rejection of a dispatched command and shall raise a fault identifying the command a… | TBD |  |
| SRS-PRI-P9-015 | detect the absence of a response to a dispatched command within a configurable timeout and sha… | TBD |  |
| SRS-PRI-P9-016 | not dispatch a setpoint that exceeds the addressed channel's configured maximum current, maxim… | TBD |  |
| SRS-PRI-P9-017 | command every channel to its safe state on entering the board-level Fault state | TBD |  |
| SRS-PRI-P9-018 | record `<TBD-03>` (the exact content of the minimum necessary control data) — see **D-03** | TBD |  |

### P10 — P10 Telemetry acquisition & forwarding

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P10-001 | acquire measured values from every present Secondary Board | TBD |  |
| SRS-PRI-P10-002 | acquire from each Secondary Board at least the measured current, measured battery voltage, mea… | TBD |  |
| SRS-PRI-P10-003 | acquire from each Secondary Board its operating state, its active fault set and its latest err… | TBD |  |
| SRS-PRI-P10-004 | acquire measured values from each Secondary Board at a configurable interval | TBD |  |
| SRS-PRI-P10-005 | derive, for each channel, the instantaneous power from the measured current and voltage | TBD |  |
| SRS-PRI-P10-006 | derive, for each channel, the instantaneous resistance from the measured current and voltage | TBD |  |
| SRS-PRI-P10-007 | integrate, for each channel, the measured current and power into the counters of SRS-PRI-P7-009 | TBD |  |
| SRS-PRI-P10-008 | detect a measured value that is stale by more than a configurable age and shall not use it for… | TBD |  |
| SRS-PRI-P10-009 | aggregate the telemetry of all channels into a single view for delivery to the Web Application | TBD |  |
| SRS-PRI-P10-010 | transmit per-channel live telemetry to the Web Application at a configurable interval | TBD |  |
| SRS-PRI-P10-011 | include in each live telemetry record the channel identity, the current step number, the progr… | TBD |  |
| SRS-PRI-P10-012 | record registrations in accordance with the active registration format and the active registra… | TBD |  |
| SRS-PRI-P10-013 | support the registration formats `SIMPLE`, `STANDARD`, `CHANREG`, `CHAREG`, `DCHREG`, `CYCLE`,… | TBD |  |
| SRS-PRI-P10-014 | accept, store and apply user-defined registration formats | TBD |  |
| SRS-PRI-P10-015 | forward recorded registrations to the Web Application for persistent storage | TBD |  |
| SRS-PRI-P10-016 | sustain the configured telemetry and registration rates for all 8 channels simultaneously with… | TBD |  |
| SRS-PRI-P10-017 | detect the loss of a telemetry or registration record and shall report the loss | TBD |  |
| SRS-PRI-P10-018 | record `<TBD-05>` (the required per-channel registration interval and the acceptable end-to-en… | TBD |  |

### P11 — P11 Monitoring, limit checking, safety supervision

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P11-001 | evaluate every configured global limit against every running channel continuously | TBD |  |
| SRS-PRI-P11-002 | evaluate the configured maximum current, maximum voltage and minimum voltage of each channel a… | TBD |  |
| SRS-PRI-P11-003 | command a channel to its safe state when that channel's measured current exceeds its configure… | TBD |  |
| SRS-PRI-P11-004 | command a channel to its safe state when that channel's measured voltage exceeds its configure… | TBD |  |
| SRS-PRI-P11-005 | command a channel to its safe state when that channel's measured battery temperature exceeds i… | TBD |  |
| SRS-PRI-P11-006 | command a channel to its safe state when that channel's heatsink temperature exceeds its confi… | TBD |  |
| SRS-PRI-P11-007 | detect a channel reporting that its commanded current setpoint cannot be reached, and shall tr… | TBD |  |
| SRS-PRI-P11-008 | detect a channel reporting that its commanded voltage setpoint cannot be reached, and shall tr… | TBD |  |
| SRS-PRI-P11-009 | detect a channel reporting reversed battery power-connection polarity, and shall not permit th… | TBD |  |
| SRS-PRI-P11-010 | detect a channel reporting reversed battery sense-connection polarity, and shall not permit th… | TBD |  |
| SRS-PRI-P11-011 | detect a channel reporting a difference between its power-connection voltage and its sense-con… | TBD |  |
| SRS-PRI-P11-012 | supervise the aggregate of all channels against configured system-level limits | TBD |  |
| SRS-PRI-P11-013 | escalate a channel-level condition to a system-level condition when a configured escalation cr… | TBD |  |
| SRS-PRI-P11-014 | record `<TBD-37>` (the cross-channel quantities to be supervised at system level and the escal… | TBD |  |

### P12 — P12 Fault detection & management

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P12-001 | maintain a single fault-code space for the whole ME system | TBD |  |
| SRS-PRI-P12-002 | support at least 100 distinct fault codes | TBD |  |
| SRS-PRI-P12-003 | represent each fault reported by a Secondary Board using the fault-code space of SRS-PRI-P12-0… | TBD |  |
| SRS-PRI-P12-004 | distinguish system faults, which arise from equipment malfunction, from user messages, which a… | TBD |  |
| SRS-PRI-P12-005 | detect and record each of the fault conditions enumerated in the hardware specification's erro… | TBD |  |
| SRS-PRI-P12-006 | detect and record the loss of the Web Application link as a fault | TBD |  |
| SRS-PRI-P12-007 | detect and record an invalid program step as a fault | TBD |  |
| SRS-PRI-P12-008 | classify every fault by severity, distinguishing at least a warning, which does not stop a pro… | TBD |  |
| SRS-PRI-P12-009 | associate every fault with the channel to which it applies, or with the board where it is not… | TBD |  |
| SRS-PRI-P12-010 | latch a fault classified as latching, retaining it after its cause has cleared | TBD |  |
| SRS-PRI-P12-011 | clear a latched fault only on an explicit acknowledgement | TBD |  |
| SRS-PRI-P12-012 | reject an acknowledgement of a latched fault whose cause is still present, and shall report th… | TBD |  |
| SRS-PRI-P12-013 | report every fault to the Web Application on detection, and shall report its clearance | TBD |  |
| SRS-PRI-P12-014 | record every fault detection, escalation, acknowledgement and clearance in its event log | TBD |  |
| SRS-PRI-P12-015 | include the active fault set of each channel in that channel's telemetry | TBD |  |
| SRS-PRI-P12-016 | transfer execution to a program's `ONERROR` handler, where one is defined, when a fault occurs… | TBD |  |
| SRS-PRI-P12-017 | record `<TBD-14>` (the unified fault-code space and the mapping from the hardware specificatio… | TBD |  |
| SRS-PRI-P12-018 | record `<TBD-38>` (which faults latch, who may acknowledge them, and whether acknowledgement r… | TBD |  |

### P13 — P13 Emergency stop & safe state

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P13-001 | define, for every channel, a safe state in which no energy is transferred to or from the batte… | TBD |  |
| SRS-PRI-P13-002 | command a channel to its safe state on any fault classified as a trip for that channel | TBD |  |
| SRS-PRI-P13-003 | command every channel to its safe state on entering the board-level Fault state | TBD |  |
| SRS-PRI-P13-004 | command every channel to its safe state before an orderly shutdown completes | TBD |  |
| SRS-PRI-P13-005 | accept a stop-all command that places every channel in its safe state, and shall act on it irr… | TBD |  |
| SRS-PRI-P13-006 | not require the Web Application link to be available in order to place a channel in its safe s… | TBD |  |
| SRS-PRI-P13-007 | report entry into the safe state, and the reason for it, to the Web Application and to its eve… | TBD |  |
| SRS-PRI-P13-008 | record `<TBD-08>` (the emergency-stop input source, its electrical behaviour, and the required… | TBD |  |

### P14 — P14 Communication-loss handling

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P14-001 | continue to execute running programs when the Web Application link is lost | TBD |  |
| SRS-PRI-P14-002 | buffer telemetry and registration data that cannot be delivered while the Web Application link… | TBD |  |
| SRS-PRI-P14-003 | attempt to re-establish the Web Application link continuously while it is lost | TBD |  |
| SRS-PRI-P14-004 | deliver buffered data to the Web Application after the link is re-established | TBD |  |
| SRS-PRI-P14-005 | take the configured action for prolonged loss of the Web Application link when that loss excee… | TBD |  |
| SRS-PRI-P14-006 | report the duration of the most recent Web Application link outage after the link is re-establ… | TBD |  |
| SRS-PRI-P14-007 | detect the loss of each Secondary Board independently, within the configured CAN timeout | TBD |  |
| SRS-PRI-P14-008 | raise a fault against the affected channel when a Secondary Board is lost | TBD |  |
| SRS-PRI-P14-009 | suspend the execution of the program on a channel whose Secondary Board is lost | TBD |  |
| SRS-PRI-P14-010 | continue to execute programs on all channels whose Secondary Boards remain present when one Se… | TBD |  |
| SRS-PRI-P14-011 | attempt to re-establish communication with a lost Secondary Board continuously while that boar… | TBD |  |
| SRS-PRI-P14-012 | verify a recovered Secondary Board's identity, software version and configuration before resum… | TBD |  |
| SRS-PRI-P14-013 | report the loss and the recovery of every Secondary Board to the Web Application and to its ev… | TBD |  |
| SRS-PRI-P14-014 | release any synchronisation barrier that a lost Secondary Board's channel cannot reach, and sh… | TBD |  |
| SRS-PRI-P14-015 | record `<TBD-39>` (whether a suspended program resumes automatically on recovery of its Second… | TBD |  |

### P15 — P15 Data logging & storage

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P15-001 | record registration data for every running channel in accordance with the active registration… | TBD |  |
| SRS-PRI-P15-002 | store registration data in non-volatile storage | TBD |  |
| SRS-PRI-P15-003 | associate every registration record with its session identity, channel identity, step number,… | TBD |  |
| SRS-PRI-P15-004 | record step-change registrations marking the beginning and the end of every step, subject to t… | TBD |  |
| SRS-PRI-P15-005 | record every user message raised by an `ERR` or `MSG` operator into the registration data of t… | TBD |  |
| SRS-PRI-P15-006 | record every fault into the registration data of the affected channel | TBD |  |
| SRS-PRI-P15-007 | record registration data into the test section currently selected for that channel | TBD |  |
| SRS-PRI-P15-008 | support multiple test sections within a single session | TBD |  |
| SRS-PRI-P15-009 | retain stored registration data across a restart | TBD |  |
| SRS-PRI-P15-010 | monitor the free capacity of its registration data storage continuously | TBD |  |
| SRS-PRI-P15-011 | report a warning to the Web Application when free storage capacity falls below a configurable… | TBD |  |
| SRS-PRI-P15-012 | refuse to start a program when free storage capacity is insufficient for that program, and sha… | TBD |  |
| SRS-PRI-P15-013 | take the configured action when registration data storage is exhausted while a program is runn… | TBD |  |
| SRS-PRI-P15-014 | not corrupt previously stored registration data when storage is exhausted | TBD |  |
| SRS-PRI-P15-015 | record `<TBD-40>` (storage medium, capacity, retention period, and the required behaviour on e… | TBD |  |

### P16 — P16 Data upload & retrieval

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P16-001 | deliver stored registration data to the Web Application on request | TBD |  |
| SRS-PRI-P16-002 | permit the Web Application to request registration data by session, by channel and by time ran… | TBD |  |
| SRS-PRI-P16-003 | deliver registration data in the order in which it was recorded | TBD |  |
| SRS-PRI-P16-004 | indicate to the Web Application which stored registration data has not yet been acknowledged a… | TBD |  |
| SRS-PRI-P16-005 | retain registration data until the Web Application has acknowledged its receipt or the configu… | TBD |  |
| SRS-PRI-P16-006 | deliver buffered registration data accumulated during a Web Application link outage without du… | TBD |  |
| SRS-PRI-P16-007 | deliver buffered data without interrupting the delivery of live telemetry | TBD |  |
| SRS-PRI-P16-008 | report the volume of buffered data awaiting delivery to the Web Application | TBD |  |
| SRS-PRI-P16-009 | make its event log available to the Web Application on request | TBD |  |
| SRS-PRI-P16-010 | record `<TBD-41>` (buffering duration and depth to be sustained during a Web Application link… | TBD |  |

### P17 — P17 Time synchronisation & timestamping

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P17-001 | maintain a wall-clock time | TBD |  |
| SRS-PRI-P17-002 | accept a time synchronisation from the Web Application and shall set its wall-clock time from… | TBD |  |
| SRS-PRI-P17-003 | distribute time synchronisation to every present Secondary Board | TBD |  |
| SRS-PRI-P17-004 | timestamp every registration record with its wall-clock time | TBD |  |
| SRS-PRI-P17-005 | timestamp every event-log entry with its wall-clock time | TBD |  |
| SRS-PRI-P17-006 | maintain a monotonic time base for step timing, limit evaluation and registration triggering t… | TBD |  |
| SRS-PRI-P17-007 | not alter the elapsed step time or elapsed program time of a running program when its wall-clo… | TBD |  |
| SRS-PRI-P17-008 | record a time discontinuity in its event log when its wall-clock time is set by more than a co… | TBD |  |
| SRS-PRI-P17-009 | report that its wall-clock time is unsynchronised when no time synchronisation has been receiv… | TBD |  |
| SRS-PRI-P17-010 | record `<TBD-16>` (whether a battery-backed real-time clock is fitted and whether network time… | TBD |  |

### P18 — P18 Configuration management

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P18-001 | hold a system configuration set and a separate configuration set for each of the 8 channels | TBD |  |
| SRS-PRI-P18-002 | support at least 200 configuration parameters | TBD |  |
| SRS-PRI-P18-003 | accept configuration parameter values from the Web Application | TBD |  |
| SRS-PRI-P18-004 | relay configuration parameters destined for a Secondary Board to that board | TBD |  |
| SRS-PRI-P18-005 | validate every configuration value against its permitted range before applying it, and shall r… | TBD |  |
| SRS-PRI-P18-006 | hold, for each channel, the type number, bootloader software version, application software ver… | TBD |  |
| SRS-PRI-P18-007 | hold, for each channel, the maximum charge current, maximum discharge current, maximum voltage… | TBD |  |
| SRS-PRI-P18-008 | hold, for each channel, the communication timeout, default **100 ms** | TBD |  |
| SRS-PRI-P18-009 | hold, for each channel, the current setpoint tolerance band for error declaration, default **0… | TBD |  |
| SRS-PRI-P18-010 | hold, for each channel, the current settling timeout, default **1000 ms** | TBD |  |
| SRS-PRI-P18-011 | hold, for each channel, the current ramp time, default **10 ms**, and the voltage ramp time, d… | TBD |  |
| SRS-PRI-P18-012 | hold, for each channel, the charge relay on time, off time and wait time, defaults **100 ms**,… | TBD |  |
| SRS-PRI-P18-013 | hold, for each channel, the discharge relay on time, off time and wait time, defaults **100 ms… | TBD |  |
| SRS-PRI-P18-014 | hold, for each channel, the lower limit, upper limit, warning limit and hysteresis of each of… | TBD |  |
| SRS-PRI-P18-015 | hold, for each channel, the calibration dates of charge current, charge voltage, discharge cur… | TBD |  |
| SRS-PRI-P18-016 | hold, for each channel, the software configuration selector permitting up to 255 selectable op… | TBD |  |
| SRS-PRI-P18-017 | hold, for each channel, the transistor-bank type selection | TBD |  |
| SRS-PRI-P18-018 | store all configuration in non-volatile storage and shall retain it across a restart | TBD |  |
| SRS-PRI-P18-019 | protect stored configuration with an integrity check | TBD |  |
| SRS-PRI-P18-020 | hold a factory default value for every configuration parameter | TBD |  |
| SRS-PRI-P18-021 | restore all configuration parameters, or an identified subset, to their factory default values… | TBD |  |
| SRS-PRI-P18-022 | deliver the current value of any configuration parameter to the Web Application on request | TBD |  |
| SRS-PRI-P18-023 | record every configuration change in its event log, together with the previous value and the n… | TBD |  |
| SRS-PRI-P18-024 | reject a configuration change to a channel that is currently executing a program where that ch… | TBD |  |
| SRS-PRI-P18-025 | record `<TBD-20>` (the health-check channel definitions of Open Issue #20 and the wear and int… | TBD |  |

### P19 — P19 Operator interaction via Web App

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P19-001 | accept a start command for an identified channel | TBD |  |
| SRS-PRI-P19-002 | accept a stop command for an identified channel | TBD |  |
| SRS-PRI-P19-003 | accept a pause command for an identified channel | TBD |  |
| SRS-PRI-P19-004 | accept a continue command for an identified channel | TBD |  |
| SRS-PRI-P19-005 | accept an interrupt command for an identified channel | TBD |  |
| SRS-PRI-P19-006 | accept a reset command | TBD |  |
| SRS-PRI-P19-007 | accept any of the commands of SRS-PRI-P19-001 to SRS-PRI-P19-005 addressed to several channels… | TBD |  |
| SRS-PRI-P19-008 | accept a session identity and a session name with a start command, and shall associate them wi… | TBD |  |
| SRS-PRI-P19-009 | accept a battery identity with a start command and shall associate it with the resulting sessi… | TBD |  |
| SRS-PRI-P19-010 | accept a start-from-step instruction with a start command | TBD |  |
| SRS-PRI-P19-011 | accept a deferred start time with a start command | TBD |  |
| SRS-PRI-P19-012 | accept a fault acknowledgement for an identified channel or for the board | TBD |  |
| SRS-PRI-P19-013 | reject a command addressed to a channel that is not present, and shall report the rejection | TBD |  |
| SRS-PRI-P19-014 | report the outcome of every command per addressed channel, so that a partial success within a… | TBD |  |
| SRS-PRI-P19-015 | deliver a live view of all channels to the Web Application comprising the data of SRS-PRI-P10-… | TBD |  |
| SRS-PRI-P19-016 | deliver, for a channel executing a program, the identity and version of the running program an… | TBD |  |
| SRS-PRI-P19-017 | accept from the Web Application the message table additions and modifications of SRS-PRI-P7-066 | TBD |  |

### P20 — P20 Firmware update

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P20-001 | accept a firmware image for itself from the Web Application | TBD |  |
| SRS-PRI-P20-002 | verify the integrity of a received firmware image before installing it | TBD |  |
| SRS-PRI-P20-003 | verify the authenticity of a received firmware image before installing it | TBD |  |
| SRS-PRI-P20-004 | reject a firmware image that fails its integrity or authenticity check, and shall report the r… | TBD |  |
| SRS-PRI-P20-005 | refuse to install its own firmware while any channel is executing a program | TBD |  |
| SRS-PRI-P20-006 | retain the ability to boot a working firmware image if an update fails | TBD |  |
| SRS-PRI-P20-007 | report the outcome of every firmware update attempt | TBD |  |
| SRS-PRI-P20-008 | accept a firmware image destined for an identified Secondary Board | TBD |  |
| SRS-PRI-P20-009 | transfer a Secondary Board firmware image to the addressed Secondary Board over the CAN interf… | TBD |  |
| SRS-PRI-P20-010 | refuse to update a Secondary Board that is executing a program | TBD |  |
| SRS-PRI-P20-011 | place a channel in its safe state before updating that channel's Secondary Board | TBD |  |
| SRS-PRI-P20-012 | verify that a Secondary Board firmware update completed successfully, and shall report the out… | TBD |  |
| SRS-PRI-P20-013 | detect and report a Secondary Board firmware update failure, distinguishing at least the condi… | TBD |  |
| SRS-PRI-P20-014 | re-verify a Secondary Board's identity and software version after an update of that board | TBD |  |
| SRS-PRI-P20-015 | update Secondary Boards one at a time unless a source establishes otherwise | TBD |  |
| SRS-PRI-P20-016 | record every firmware update attempt and its outcome in its event log | TBD |  |
| SRS-PRI-P20-017 | record `<TBD-09>` (whether Secondary firmware update over CAN is required, whether the Seconda… | TBD |  |

### P21 — P21 Calibration management

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P21-001 | hold calibration data for each of the 8 channels | TBD |  |
| SRS-PRI-P21-002 | accept calibration data from the Web Application and relay it to the addressed Secondary Board | TBD |  |
| SRS-PRI-P21-003 | hold, per channel, the charge current calibration gain and offset and the discharge current ca… | TBD |  |
| SRS-PRI-P21-004 | hold, per channel, the charge voltage calibration gain and offset and the discharge voltage ca… | TBD |  |
| SRS-PRI-P21-005 | hold, per channel, the current regulator proportional, integral and derivative parameters | TBD |  |
| SRS-PRI-P21-006 | hold, per channel, the voltage regulator proportional, integral and derivative parameters | TBD |  |
| SRS-PRI-P21-007 | hold, per channel, the external temperature sensor calibration gain and offset | TBD |  |
| SRS-PRI-P21-008 | hold, per channel, the internal heatsink temperature calibration gain and offset | TBD |  |
| SRS-PRI-P21-009 | hold, per channel, the charge-mode transistor-bank voltage feedback calibration gain and offset | TBD |  |
| SRS-PRI-P21-010 | hold, per channel, the discharge-mode transistor-bank voltage feedback calibration gain and of… | TBD |  |
| SRS-PRI-P21-011 | support a guided calibration procedure in which the Web Application commands a channel to a ca… | TBD |  |
| SRS-PRI-P21-012 | deliver live calibration data comprising the measured current, the raw current converter count… | TBD |  |
| SRS-PRI-P21-013 | support the cancellation of a calibration procedure, restoring the previously stored calibrati… | TBD |  |
| SRS-PRI-P21-014 | support a verification pass that reports the deviation of measured values from applied referen… | TBD |  |
| SRS-PRI-P21-015 | deliver the previously stored calibration data of a channel on request | TBD |  |
| SRS-PRI-P21-016 | record the date of each calibration operation against the affected quantity | TBD |  |
| SRS-PRI-P21-017 | store calibration data in non-volatile storage, protected by an integrity check, and shall ret… | TBD |  |
| SRS-PRI-P21-018 | refuse to start a calibration procedure on a channel that is executing a program | TBD |  |
| SRS-PRI-P21-019 | place a channel in a defined calibration state for the duration of a calibration procedure on… | TBD |  |
| SRS-PRI-P21-020 | record every calibration change in its event log | TBD |  |
| SRS-PRI-P21-021 | record `<TBD-42>` (whether per-range calibration comprising a full range and four sub-ranges i… | TBD |  |

### P22 — P22 Diagnostics, health, event log, audit

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P22-001 | maintain an event log | TBD |  |
| SRS-PRI-P22-002 | record in the event log every state change, fault detection and clearance, command received, c… | TBD |  |
| SRS-PRI-P22-003 | record each event-log entry with a timestamp, a severity, an originating subsystem and, where… | TBD |  |
| SRS-PRI-P22-004 | store the event log in non-volatile storage and shall retain it across a restart | TBD |  |
| SRS-PRI-P22-005 | record in the event log the identity of the originator of every command that changes system st… | TBD |  |
| SRS-PRI-P22-006 | not permit the deletion or modification of an existing event-log entry | TBD |  |
| SRS-PRI-P22-007 | continue to record events when the event log is full, discarding the oldest entries, and shall… | TBD |  |
| SRS-PRI-P22-008 | monitor its own processor load, memory utilisation and storage utilisation continuously | TBD |  |
| SRS-PRI-P22-009 | monitor its own die temperature and shall raise a fault when it exceeds a configured trip point | TBD |  |
| SRS-PRI-P22-010 | detect and record a memory error reported by an error-correcting memory subsystem | TBD |  |
| SRS-PRI-P22-011 | evaluate each of the five health-check channels of each Secondary Board against its configured… | TBD |  |
| SRS-PRI-P22-012 | raise a warning when a health-check channel crosses its warning limit, and a trip when it cros… | TBD |  |
| SRS-PRI-P22-013 | apply the configured hysteresis when determining that a health-check channel has returned with… | TBD |  |
| SRS-PRI-P22-014 | deliver a health summary of itself and of every channel to the Web Application on request | TBD |  |
| SRS-PRI-P22-015 | provide a diagnostic mode in which raw converter counts of a channel can be read without alter… | TBD |  |
| SRS-PRI-P22-016 | record `<TBD-20>` (the physical quantity assigned to each of health-check channels 1 to 5 and… | TBD |  |

### P23 — P23 Power-fail detection & recovery

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P23-001 | detect an impending loss of supply | TBD |  |
| SRS-PRI-P23-002 | command every channel to its safe state on detecting an impending loss of supply | TBD |  |
| SRS-PRI-P23-003 | persist, on detecting an impending loss of supply, sufficient state to resume each running pro… | TBD |  |
| SRS-PRI-P23-004 | persist, on detecting an impending loss of supply, all registration data not yet written to no… | TBD |  |
| SRS-PRI-P23-005 | complete the persistence actions of SRS-PRI-P23-003 and SRS-PRI-P23-004 within the available s… | TBD |  |
| SRS-PRI-P23-006 | record a power-fail event in its event log | TBD |  |
| SRS-PRI-P23-007 | detect, after a restart, that the preceding shutdown was caused by loss of supply | TBD |  |
| SRS-PRI-P23-008 | verify the integrity of persisted program state before using it to resume a program | TBD |  |
| SRS-PRI-P23-009 | verify that each channel's Secondary Board is present, identified and consistent with the pers… | TBD |  |
| SRS-PRI-P23-010 | restore the persisted counters, cycle counters, timers and variables of a resumed program | TBD |  |
| SRS-PRI-P23-011 | report to the Web Application, after a restart following loss of supply, which programs are re… | TBD |  |
| SRS-PRI-P23-012 | record the resumption or the abandonment of every program after a restart in its event log | TBD |  |
| SRS-PRI-P23-013 | detect unstable input supply and shall raise a fault | TBD |  |
| SRS-PRI-P23-014 | record `<TBD-23a>` (the supply hold-up time available for persistence, and whether a program r… | TBD |  |

### P24 — P24 Security & access control

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P24-001 | accept commands that change system state only from an authenticated peer | TBD |  |
| SRS-PRI-P24-002 | record the authenticated identity of the originator of every state-changing command in its eve… | TBD |  |
| SRS-PRI-P24-003 | reject a state-changing command from an unauthenticated peer, and shall record the rejection | TBD |  |
| SRS-PRI-P24-004 | distinguish the privilege required for operational commands, for configuration changes, for ca… | TBD |  |
| SRS-PRI-P24-005 | protect the confidentiality and integrity of data in transit on the Web Application interface | TBD |  |
| SRS-PRI-P24-006 | verify the authenticity of its own firmware at every boot | TBD |  |
| SRS-PRI-P24-007 | protect stored credentials and cryptographic keys against retrieval over any external interface | TBD |  |
| SRS-PRI-P24-008 | not expose a debug interface that permits the alteration of firmware or configuration in a pro… | TBD |  |
| SRS-PRI-P24-009 | isolate the Modbus interface from the Web Application interface such that traffic on one canno… | TBD |  |
| SRS-PRI-P24-010 | record `<TBD-10>` (whether the Primary authenticates the Web Application and enforces roles it… | TBD |  |

### P25 — P25 Modbus module

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-P25-001 | host the Modbus module | TBD |  |
| SRS-PRI-P25-002 | implement the Modbus protocol over a serial physical layer | TBD |  |
| SRS-PRI-P25-003 | expose, through the Modbus module, the measured values of every channel | TBD |  |
| SRS-PRI-P25-004 | expose, through the Modbus module, the operating state and active fault set of every channel | TBD |  |
| SRS-PRI-P25-005 | expose, through the Modbus module, the board-level operating state | TBD |  |
| SRS-PRI-P25-006 | hold a Modbus address, a baud rate, a parity setting and a stop-bit setting as configuration p… | TBD |  |
| SRS-PRI-P25-007 | validate every Modbus request and shall return the defined Modbus exception response for an un… | TBD |  |
| SRS-PRI-P25-008 | not allow a Modbus request to place any channel in an unsafe condition | TBD |  |
| SRS-PRI-P25-009 | not allow a failure or a timeout on the Modbus interface to affect program execution on any ch… | TBD |  |
| SRS-PRI-P25-010 | record Modbus communication faults in its event log | TBD |  |
| SRS-PRI-P25-011 | record `<TBD-43>` (whether the Primary acts as Modbus server, Modbus client or both; the physi… | TBD |  |
| SRS-PRI-P25-012 | record `<TBD-44>` (whether write access through the Modbus module is permitted, and if so to w… | TBD |  |

### CM — §3.3 Communication interfaces

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-CM-001 | detect corrupted messages on every external interface and discard them without acting on their… | TBD |  |
| SRS-PRI-CM-002 | acknowledge or negatively acknowledge every command received on IF-A that requires a response | TBD |  |
| SRS-PRI-CM-003 | reject a command whose parameters are outside their permitted range, and report the rejection… | TBD |  |
| SRS-PRI-CM-004 | detect the loss of the Web Application link | TBD |  |
| SRS-PRI-CM-005 | detect the loss of each Secondary Board link independently | TBD |  |
| SRS-PRI-CM-006 | treat a Secondary Board as unresponsive when no valid message has been received from it within… | TBD |  |
| SRS-PRI-CM-007 | not allow the failure of one Secondary Board link to prevent communication with the remaining… | TBD |  |
| SRS-PRI-CM-008 | timestamp every message it originates on IF-A that carries measurement or event data | TBD |  |

### HW — §3.1 Hardware interfaces

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-HW-001 | communicate with up to 8 Secondary Boards over a CAN interface | TBD |  |
| SRS-PRI-HW-002 | communicate with the Web Application over an Ethernet interface | TBD |  |
| SRS-PRI-HW-003 | provide a Modbus interface | TBD |  |
| SRS-PRI-HW-004 | retain all persistent data in non-volatile storage that survives loss of supply | TBD |  |
| SRS-PRI-HW-005 | service a hardware watchdog such that an unserviced watchdog causes a board reset | TBD |  |
| SRS-PRI-HW-006 | detect an impending loss of supply and signal it to software before supply voltage falls below… | TBD |  |
| SRS-PRI-HW-007 | obtain wall-clock time from a real-time clock | TBD |  |
| SRS-PRI-HW-008 | record `<TBD-16>` (whether the RTC is battery-backed and retains time across supply loss) — se… | TBD |  |

### NF — §5 Non-functional requirements

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-NF-001 | evaluate the active limits of every running channel at least once per **100 ms** | TBD |  |
| SRS-PRI-NF-002 | support a registration resolution of **0.1 s** on every running channel | TBD |  |
| SRS-PRI-NF-003 | support a minimum program step duration of **0.1 s** | TBD |  |
| SRS-PRI-NF-004 | meet the timing of SRS-PRI-NF-001 to SRS-PRI-NF-003 with all 8 channels running concurrently | TBD |  |
| SRS-PRI-NF-005 | detect the loss of a Secondary Board within the configured communication timeout, default **10… | TBD |  |
| SRS-PRI-NF-006 | command a channel to its safe state within `<TBD-45>` of detecting a trip condition on that ch… | TBD |  |
| SRS-PRI-NF-007 | complete initialization and reach the Ready state within `<TBD-46>` of the application of supp… | TBD |  |
| SRS-PRI-NF-008 | not lose any registration record while sustaining the configured registration rate on all 8 ch… | TBD |  |
| SRS-PRI-NF-009 | record `<TBD-05>` (the required per-channel registration interval and the acceptable end-to-en… | TBD |  |
| SRS-PRI-NF-010 | treat the safe state of a channel as its default, entering it whenever the correct condition o… | TBD |  |
| SRS-PRI-NF-011 | not rely on program limits as the sole protection of a battery or of the equipment | TBD |  |
| SRS-PRI-NF-012 | retain the ability to place a channel in its safe state when the Web Application link is unava… | TBD |  |
| SRS-PRI-NF-013 | report, whenever a program stops rather than terminating the queue, that subsequent queued ass… | TBD |  |
| SRS-PRI-NF-014 | not resume energy transfer on a channel after a fault without an explicit command | TBD |  |
| SRS-PRI-NF-015 | record `<TBD-47>` (the safety integrity requirement applicable to the ME system, if any, and t… | TBD |  |
| SRS-PRI-NF-016 | continue to control the remaining channels when any single Secondary Board fails | TBD |  |
| SRS-PRI-NF-017 | recover to a defined state after an unexpected restart without operator intervention | TBD |  |
| SRS-PRI-NF-018 | not lose persisted configuration, calibration, program or registration data as a result of an… | TBD |  |
| SRS-PRI-NF-019 | detect its own failure to make progress and shall cause a reset | TBD |  |
| SRS-PRI-NF-020 | bound the memory used by every buffer and queue, and shall not fail as a result of a sustained… | TBD |  |
| SRS-PRI-NF-021 | operate correctly across the industrial junction-temperature range of the specified processor,… | TBD |  |
| SRS-PRI-NF-022 | operate on the NXP `MIMX8ML8CVNKZAB` with Linux on the Cortex-A53 cluster and an RTOS on the C… | Forced (SOC) |  |
| SRS-PRI-NF-023 | fail closed, rejecting a state-changing command, when it cannot establish the authorisation of… | TBD |  |
| SRS-PRI-NF-024 | not log credentials, keys or other secrets | TBD |  |
| SRS-PRI-NF-025 | not weaken the security state of the system as a result of a firmware update | TBD |  |
| SRS-PRI-NF-026 | be organised in layers separating Application, Interface, Basic Software and Communication con… | TBD |  |
| SRS-PRI-NF-027 | No source file of the Primary Board software shall exceed 2000 lines | TBD |  |
| SRS-PRI-NF-028 | not allocate memory dynamically after initialization | TBD |  |
| SRS-PRI-NF-029 | report its own software version, and the version of every interface it implements, on request | TBD |  |
| SRS-PRI-NF-030 | record `<TBD-26a>` (whether MISRA-C compliance is required, and for which modules) — see Open… | TBD |  |
| SRS-PRI-NF-031 | provide a diagnostic log obtainable without interrupting program execution | TBD |  |
| SRS-PRI-NF-032 | provide sufficient information in its event log to reconstruct the sequence of events precedin… | TBD |  |
| SRS-PRI-NF-033 | support the reading of raw converter counts for diagnostic purposes | TBD |  |

### SW — §3.2 Software interfaces

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-SW-001 | present exactly one logical interface to the Web Application (IF-A) carrying all program, conf… | TBD |  |
| SRS-PRI-SW-002 | present one logical interface per Secondary Board (IF-B), addressed individually | TBD |  |
| SRS-PRI-SW-003 | implement the message set defined in `ME-ICD-001` for IF-A and IF-B | TBD |  |
| SRS-PRI-SW-004 | be implemented in a layered architecture separating Application, Interface, Basic Software and… | TBD |  |

### UI — §3.4 User interfaces

| ID | Requirement (abridged) | Current | **Allocation** |
|---|---|---|---|
| SRS-PRI-UI-001 | expose all operator functions through the Web Application; it shall not require a local human-… | TBD |  |
| SRS-PRI-UI-002 | provide a diagnostic access path usable by service personnel independently of the Web Applicat… | TBD |  |

---

*Generated from `01_SRS_ME_Primary_Board_v0.1.md`. 620 requirements.*
