# Software Requirements Specification — ME Web Application

**Document ID:** ME-SRS-WEB-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Standard:** ISO/IEC/IEEE 29148:2018
**Target:** ME Web Application — the host application from which ME test programs,
configuration and operator commands originate, and to which telemetry, logged data and
events are delivered
**Companion documents:** `04A_SRS_ME_Web_Application_Annex_Traceability_v0.1.md`
(§6 Traceability) · `01_SRS_ME_Primary_Board_v0.1.md` ·
`02_SRS_ME_Secondary_Board_v0.1.md` · `03_ICD_ME_Interfaces_v0.1.md` ·
`00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` (gate document)

> **Reading note 1 — this document is conditional on Open Issue #2.**
>
> Open Issue **#2** — *which* Web Application is ME's peer — is **not resolved**. Three
> answers are possible: the existing Ador BTS server evolved, Digatron Battery Manager, or
> a new build. This document is written on the working assumption that ME's Web
> Application is **the existing Ador BTS server, evolved** (A-01), because that is the only
> candidate for which a documented baseline exists.
>
> If #2 resolves differently, §4 remains a valid statement of *required behaviour* but the
> `Source` column's evidentiary weight collapses: requirements traced to `WAD` become
> `NEW — ME` and every `<TBD-Wnn>` about carrying an existing mechanism forward becomes
> moot. Nothing in this document is safe to treat as already implemented until #2 is
> answered.
>
> **Reading note 2 — this document supersedes nothing.**
>
> `WAD/SRS` (`BTS-SRS-001` v0.6, status *Approved*) remains the specification of the
> **existing BTS product**. This document specifies the **ME Web Application**, which is a
> different scope: eight channels behind one board instead of one or two circuits per
> device, no external CAN, and a new multi-node management surface. Where the two differ,
> this document states the ME requirement and records the difference; it does not amend
> `WAD/SRS`.
>
> **No §7.** §7 is *Core Allocation* in the Primary SRS, reserved to decision **D-01**. It
> does not apply to a server application. §7 below records the omission rather than
> renumbering, so section numbers align across all three SRS documents.

---

## 1. Introduction

### 1.1 Purpose

This document specifies the software requirements for the **ME Web Application** — the
sole external command and data peer of the ME Primary Board. It is the application through
which operators author and assign test programs, start and stop tests, watch live
measurements, retrieve and export recorded data, calibrate channels, and administer users
and access.

The document is written for the Web Application development team, the software architect,
the test team and the client's review authority. It is a **complete functional
specification of intent**; it deliberately stops short of design.

### 1.2 Scope

**In scope.** All Web-Application-resident software behaviour: Primary Board discovery and
network configuration; session registration and connection management; channel inventory
and presence; program authoring, storage and validation; program transfer; program
scheduling; multi-channel assignment and execution control; real-time monitoring;
session data recording; session self-containment; reporting and export; calibration
workflow; battery, battery-type and standards management; configuration and code-message
management; user, role and access management; audit and event logging; system services and
external integration; and firmware-image distribution.

**Out of scope.**

| Not specified here | Where it belongs | Why |
|---|---|---|
| Primary Board behaviour | `ME-SRS-PRI-001` | Separate item |
| Secondary Board behaviour | `ME-SRS-SEC-001` | Separate item |
| Wire formats, ports, framing, serialization on IF-A | `ME-ICD-001`; **D-05** | Reserved |
| Program *execution* semantics — operators, step sequencing, cycles, limits | `ME-SRS-PRI-001` §4.5, §4.7 | The Primary decodes and executes; the Web Application authors and transfers |
| Regulation, measurement and protection behaviour | `ME-SRS-SEC-001` | Secondary-resident |
| **DBC CAN file management** | — | **Excluded from ME scope** by the resolution of Open Issue **#29**. §4.19 records the exclusion explicitly |
| Modbus (IF-D) client behaviour | `ME-ICD-001` §6 | IF-D terminates on the Primary Board; the Web Application is not a Modbus peer |
| Deployment topology, hosting and CI/CD mechanics | `WAD/ADD` §5; `WAD/CMP` | Not software requirements, but constrained in §2.6 |

### 1.3 Product identity

**"ME" is the name of this new Ador project.** It is not the Digatron "ME" circuit type of
the Battery Manager manual; see `ME-SRS-PRI-001` §1.3. For the Web Application
specifically, two consequences follow:

- `BM` (Battery Manager) is a **functional reference** for operator-facing behaviour —
  what a start dialog offers, what a status view shows, how a dispo list works, what a
  registration format means. It is **not** a compatibility contract and the ME Web
  Application need not interoperate with Battery Manager.
- Named entities adopted from `BM` (registration formats, message numbers, operator
  mnemonics) are adopted for operator familiarity only.

### 1.4 Definitions

The ME document set uses these terms with exactly these meanings. Three of them are
**deliberate corrections** of `WAD/SRS` §1.3, because the same word is used there for a
different thing; each is flagged and carried as a conflict.

| Term | Definition |
|---|---|
| **Web Application** | The application specified by this document. |
| **Primary Board** | The ME system controller (`ME-SRS-PRI-001`). The Web Application's sole hardware peer. |
| **Secondary Board** | The regulating actuator for one channel (`ME-SRS-SEC-001`). The Web Application never addresses one directly. |
| **Board** | One Primary Board together with the Secondary Boards behind it. Replaces `WAD`'s **Device**. |
| **Channel** | One test circuit — one Secondary Board. Up to eight per board. Replaces `WAD`'s **Circuit**. |
| **Channel position** | One of the eight addressable slots on a board, whether or not a Secondary Board is present in it. |
| **Session** | One execution of one program on one channel, with its recorded data. |
| **Program** | An ordered sequence of steps in the BTS-600 program language. |
| **Step** | One program line: Label, Operator, Nominal Value, Limit, Action, Registration. |
| **Schedule** | A named, time-triggered instruction to assign and start a program on target channels. |
| **⚠ Registration (measurement)** | A recorded measurement sample, and the rules governing when samples are recorded. **This is the meaning used throughout the ME document set** — `ME-SRS-PRI-001` §1.4, `ME-SRS-SEC-001` §1.4, `BM` §12.4.1. |
| **⚠ Session registration** | The handshake by which a board announces itself and opens its command session. `WAD/SRS` §1.3 calls this simply "Registration"; ME always qualifies it. **Conflict C-21.** |
| **⚠ Limit standard** | A named set of voltage, current and temperature bounds applied to a session. `WAD/DBD` calls this a `RegistrationStandard`, which in ME's vocabulary reads as a measurement-recording format and is not one. **Conflict C-21.** |
| **Registration format** | The named set of measurement channels written in each registration record. A *measurement* concept, per `BM` §12.4.1. |
| **Board discovery** | Finding boards on the network and configuring their addressing, before any session exists. |
| **Live telemetry** | Measurements streamed for display, not for the permanent record. |
| **Logged data** | Measurements delivered for the permanent record. |
| **Session database** | The per-session store holding one session's logged data. *In plain terms: every test run gets its own separate data file.* |
| **PRODUCER** | An operator that embeds another saved program's steps inline at transfer time (`WAD/SRS` FR-005.6). *In plain terms: a re-usable block of steps that gets copied into the program before it is sent to the hardware, so the hardware never knows it was a separate program.* Contrast `BM` **Procedures**; see Open Issue **#7**. |
| **TABLE file** | An external file supplying the rows of a TABLE-operator step. |

### 1.5 Acronyms

`ACL` Access Control List (*a per-user list of exactly what that person is allowed to do, finer-grained than a role*) · `API` Application Programming Interface · `CAN` Controller Area
Network · `CRUD` Create, Read, Update, Delete · `CSV` Comma-Separated Values ·
`DBC` CAN database file · `EF Core` Entity Framework Core · `HIL` Hardware-In-the-Loop ·
`HTTP` Hypertext Transfer Protocol · `ICD` Interface Control Document · `JWT` JSON Web
Token · `MCP` Model Context Protocol (*a standard interface that lets an AI assistant call software tools directly — here, potentially, to control test channels*) · `NTP` Network Time Protocol · `ORM` Object-Relational
Mapper · `PBKDF2` Password-Based Key Derivation Function 2 (*a deliberately slow way of storing a password so that a stolen database cannot be cracked quickly*) · `RBAC` Role-Based Access
Control · `REST` Representational State Transfer · `RPO` Recovery Point Objective (*the most data you are willing to lose if something fails — measured in time, e.g. "at most 10 seconds of readings"*) ·
`SADP` Simple Automatic Device Protocol · `SSE` Server-Sent Events (*a one-way stream that lets the server keep pushing updates to the browser without the browser asking each time*) · `TCP` Transmission
Control Protocol · `UDP` User Datagram Protocol · `UI` User Interface · `WAL` Write-Ahead
Logging (*the database writes changes to a journal first, so a crash mid-write cannot corrupt the file and readers are never blocked by a writer*)

### 1.6 References

| Tag | Document |
|---|---|
| **WAD** | `Reference Documents/WebAppDocs/` — the existing Ador BTS server documentation set, prepared by the BTS Development Team, APL, May 2026. Sub-cited as below. |
| **WAD/SRS** | `SRS.md` — `BTS-SRS-001` **v0.6**, status *Approved*. 11 functional groups, 10 non-functional requirements. **The developer's SRS; the principal source for this document.** |
| **WAD/RTM** | `RTM.md` — `BTS-RTM-001` **v0.5**. Requirement-to-implementation-to-test traceability. **Contains requirements absent from WAD/SRS** — see conflict **C-22**. |
| **WAD/ADD** | `ADD.md` — `BTS-ADD-001` v0.5. Layered architecture, services, technology stack, deployment, security architecture. |
| **WAD/DDD** | `DDD.md` — detailed design. |
| **WAD/DBD** | `DBD.md` — `BTS-DBD-001` v0.5. Main and per-session database schemas, 12 migrations. |
| **WAD/ICD** | `ICD.md` — the existing BTS hardware interface: TCP 9999, UDP 10000/10001/10002/10003, packet layouts, status and error code spaces. |
| **WAD/CAP** | `ARCHITECTURE_CAPACITY.md` v1.0 — capacity and latency analysis; per-circuit data rates; the single-UDP-processor bottleneck. |
| **WAD/VVP** | `VVP.md` — `BTS-VVP-001` v0.5. Static analysis, unit-test scope, HIL and UI validation test sets. |
| **WAD/CMP**, **WAD/PMP**, **WAD/QAP**, **WAD/RMP** | Configuration management, project management, quality assurance and risk management plans. |
| **WAD/INDEX** | `INDEX.md` — document list and CMMI Level 3 process-area map. |
| **PRI** | `01_SRS_ME_Primary_Board_v0.1.md` — the peer this application talks to. |
| **SEC** | `02_SRS_ME_Secondary_Board_v0.1.md` — for the meaning of quantities reported upward. |
| **ICD** | `03_ICD_ME_Interfaces_v0.1.md` — the IF-A message set this application implements. |
| **BM** | `BM_Manual_eng 2.pdf` — Digatron Battery Manager User Manual 99.3. Functional reference for operator-facing behaviour. |
| **LSRS** | `BTS_Primary_SW_Requirement_Analysis V1.7.xlsx` — legacy combined SRS. Reference only. |
| **GATE** | `00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` — shared source-coverage, open-issue and conflict registers. |
| **NEW — ME** | Requirement arises from the ME architecture with no precedent in any source. |

**Precedence when sources conflict.** For this document the order is
**ICD > PRI > WAD/SRS > WAD/RTM > WAD/ADD, WAD/DBD, WAD/CAP > BM > LSRS**, because:

1. `ICD` defines what actually crosses IF-A, and both this application and the Primary
   Board must implement the same message set.
2. `PRI` defines what the Primary Board will and will not do, which bounds what this
   application can ask of it.
3. `WAD/SRS` is the developer's own approved statement of intent for the baseline product.
4. `WAD/RTM`, `WAD/ADD`, `WAD/DBD` and `WAD/CAP` are *descriptions of an implementation*.
   They are strong evidence of what exists and weak evidence of what is required.

**Where the WebAppDocs set conflicts with itself** — and it does, in five places
(**C-21** … **C-25**) — precedence cannot resolve it, and the item escalates to an Open
Issue rather than being decided here.

### 1.7 Requirement conventions

- **ID:** `SRS-WEB-<AREA>-<NNN>` where `<AREA>` is a coverage area code `W1`…`W19`, or
  `HW`/`SW`/`CM`/`UI` in §3 and `NF` in §5.
- **"shall"** denotes a binding requirement. One requirement per statement.
- **`Source`** cites document + section or requirement ID, or `NEW — ME`.
- **No `Alloc` column.** Not a multicore target; see the reading note.
- **`<TBD-Wnn>`** marks a value or policy that could not be sourced. The `W` namespace
  keeps it distinct from `<TBD-nn>` (Primary), `<TBD-Snn>` (Secondary) and `<TBD-Inn>`
  (ICD). All are registered in §8.3.
- **Open Issue #N** references the shared register in `GATE` §4, reproduced in §8.1.
  Issues raised first by this document are numbered **W-33** onward, continuing the
  sequence the Secondary SRS began at **S-32**.
- **C-nn** references the shared **conflict** register. `GATE` §5 holds C-01…C-18, the
  Secondary SRS added C-19 and C-20, and this document adds **C-21…C-27** (§8.2).
- **Two `W` namespaces, deliberately distinct.** `<TBD-W33>` in angle brackets is an
  unsourced *value* (§8.3). **W-33** in bold is an *open issue* (§8.1). They are unrelated
  and their numbers do not correspond.
- **CN-nn** references a **design constraint** in §2.6.
- **⚠ data-integrity critical.** Requirements whose failure silently loses or corrupts
  recorded test data, or silently grants access, are marked **⚠**. Each requires explicit
  verification evidence; see §5.7. This document has no hardware-critical requirements —
  it commands hardware but does not drive it.

---

## 2. Overall Description

### 2.1 Product perspective

The ME Web Application replaces the BTS Web Application. Four changes drive this
specification.

| # | BTS | ME | Consequence |
|---|---|---|---|
| 1 | One **device** holds 1–2 **circuits**; the server keys everything on `DeviceID`+`CircuitID` and opens **one TCP connection per device-circuit pair** (`WAD/ADD` §2.2, `WAD/RTM` FR-002.6) | One **board** holds up to **8 channels** behind **one logical interface** (`SRS-PRI-SW-001`) | The per-circuit connection model breaks. §4.2 respecifies connection management at board level, and §4.3 adds the channel inventory that BTS never needed. |
| 2 | Hardware announces itself **per circuit**, and a circuit is whitelisted per `DeviceID`+`CircuitID` (`WAD/SRS` FR-002.2, FR-002.3) | A **board** announces itself once; its channel population is discovered afterwards and **can change** | Whitelisting, ACLs and the data model move to *board + channel position*. §4.3, §4.15. |
| 3 | External CAN with DBC decode is a first-class feature — 6 requirements in `WAD/SRS` FR-009, dynamic session-database columns per DBC signal, upload to hardware | **Excluded from ME scope** (Open Issue **#29** resolved) | An entire feature area is withdrawn. §4.19 records exactly what is withdrawn so the exclusion is auditable. |
| 4 | The device sends live and logged data **directly**, at **10 ms** per circuit (`WAD/CAP` §3) | The Primary **aggregates** 8 channels from a shared CAN bus; the achievable rate is 100 ms, or 10 ms only with CAN-FD (`ICD` §7.3) | The ingestion rate assumption changes by an order of magnitude — in the *easier* direction. §5.1 and §4.9 restate the budget. |

### 2.2 System context

```
   ┌──────────────────────────── OPERATORS ────────────────────────────┐
   │  Administrator        Operator            Viewer                  │
   └────────────────────────────┬──────────────────────────────────────┘
                                │  HTTP  (browser; port = D-05)
   ┌────────────────────────────┴──────────────────────────────────────┐
   │                    ME WEB APPLICATION                             │
   │                  (this document)                                  │
   │                                                                   │
   │   programs · schedules · batteries · limit standards              │
   │   users · roles · ACLs · audit · code messages                    │
   │   main store  +  one session store per session                    │
   └────────────────────────────┬──────────────────────────────────────┘
                                │  IF-A  (TCP + UDP; details = D-05)
                                │  ME-ICD-001 §4 — 90 messages
                                │
        ┌───────────────────────┴───────────────────────┐
        │                                               │
   ┌────┴─────────┐                              ┌─────┴────────┐
   │ PRIMARY  #1  │   … up to `<TBD-W01>` boards │ PRIMARY  #m  │
   │ 8 channels   │                              │ 8 channels   │
   └────┬─────────┘                              └──────────────┘
        │  IF-B (CAN) — not visible to this application
   ┌────┴────┬────────┬─── … ───┬────────┐
   │ SEC 1   │ SEC 2  │         │ SEC 8  │
   └─────────┴────────┴─────────┴────────┘

   Also present, and NOT this application's concern:
        IF-D  Modbus client ──► Primary Board   (ME-ICD-001 §6)
```

**The Web Application never addresses a Secondary Board.** Every channel-level operation
is expressed to the Primary Board, which relays it (`ICD` §5.8 shows the calibration relay
explicitly). This is unchanged in *form* from BTS but changed in *fact*: in BTS the server
held a connection per circuit, so "addressing a circuit" meant addressing its own socket.

### 2.3 Technology and deployment constraint envelope

From `WAD/ADD` and `WAD/CAP`. Capability is a **constraint envelope**, not a requirement:
no requirement in this document exists merely because the baseline implementation has a
mechanism. Subject to Open Issue **#2**.

| Aspect | Baseline provision | Bearing on this SRS |
|---|---|---|
| Runtime | .NET 8 | §5.5; `<TBD-W02>` whether ME retains it |
| UI framework | Blazor Server, server-side state, SignalR diff push | §4.8, §5.1. All UI state on the server is why NFR latency is measured server-side |
| Styling / charts | Tailwind CSS 3.x; Highcharts via CDN | **CDN dependency** — a networked dependency in a plant network → **W-33** |
| ORM | EF Core 8, code-first migrations (12 to date) | §4.14, §5.5 |
| Main store | SQLite, single file | **Concurrency ceiling** → `<TBD-W03>`, **W-34** |
| Session store | One SQLite file per session, schema synced at runtime, WAL | §4.9, §4.10 |
| Queueing | `System.Threading.Channels`, **unbounded** (*a waiting line with no maximum length — it never refuses new work, so under overload it grows until memory runs out*) | `WAD/CAP` §6 Risk 2 flags unbounded growth → §4.9, **C-24** |
| Hardware listener | `BackgroundService`; one TCP listener, two UDP listeners | §4.1, §4.2 |
| Ingestion path | **Single** UDP store processor task for all circuits | `WAD/CAP` §6 Risk 1 names this the #1 bottleneck → §5.1, **C-24** |
| Auth | ASP.NET Identity, cookie for UI, JWT for API, PBKDF2 | §4.15, §5.4 |
| Column encryption | Custom attribute + service | §5.4 |
| Scheduling | Hangfire jobs | §4.6 |
| Logging | Serilog rolling file, 7-day retention, in-memory store for UI | §4.16, §5.6 |
| Export | OpenXML | §4.11 |
| External APIs | REST controllers; SSE live endpoint; **MCP tool server** | §4.17, §5.4 |
| Container | Docker, `restart: unless-stopped`, GHCR image | §5.3, §5.6 |
| Browsers | Chrome, Edge, Firefox — latest two versions | §5.6 |
| Capacity | **≤ 10 circuits safe, ≤ 20 practical, 41+ overload** (`WAD/CAP` §4) | **Directly limits how many ME boards one instance can serve** → §5.1, `<TBD-W01>` |

**The capacity figure is the most consequential number in this table.** `WAD/CAP` states a
practical ceiling of about 20 circuits for reliable real-time operation. One ME board is
8 channels. **Three boards is 24 channels — already past that ceiling.** §5.1 carries this
as a requirement and `<TBD-W01>` asks how many boards one instance must serve.

### 2.4 User classes

| Class | Interaction | Source |
|---|---|---|
| **Administrator** | Full access: boards, channels, users, programs, schedules, calibration, settings, code messages | `WAD/SRS` §2.2 |
| **Operator** | Start, stop, pause, continue and interrupt sessions; acknowledge faults; view live and historical data; export | `WAD/SRS` §2.2; BM §2 |
| **Viewer** | Read-only access to data and reports | `WAD/SRS` §2.2 |
| **Test engineer** | Authors programs, defines batteries and limit standards, assigns programs to channels. *Not a role in `WAD`; the work is currently done by an Administrator* → **W-35** | BM §12; NEW — ME |
| **Service / commissioning engineer** | Calibration, factory configuration, per-channel setup, firmware update. *Not a role in `WAD`* → **W-35** | HW *Calibration*; NEW — ME |
| **Primary Board (software actor)** | Peer over IF-A; originates telemetry, logged data and events | `ICD` §4 |
| **External API client (software actor)** | REST and SSE consumer | `WAD/RTM` FR-010.12; `WAD/ICD` §7 |
| **MCP client (software actor)** | AI or LLM tool caller with device-control capability | `WAD/RTM` FR-010.13; `WAD/ICD` §8 |

> **The last actor is a security-relevant novelty.** `WAD` ships an MCP tool server that
> exposes device control to an AI client (`WAD/ADD` §2.1, `MCP/DeviceMcpTools.cs`). Nothing
> in `WAD/SRS` states its authentication, authorisation or audit obligations — the SRS does
> not mention MCP at all. In ME this actor can start and stop tests on eight channels.
> Carried as **W-36** and constrained by SRS-WEB-W17-012 to SRS-WEB-W17-015.

### 2.5 Operating environment

A server on the plant local-area network, reachable by browser from operator
workstations. It reaches one or more ME Primary Boards over the same network. It is
expected to run continuously and unattended, to survive its own restart without losing a
running test's recorded data, and to run for the duration of tests that may last days.
It is not assumed to have internet access — see **W-33**.

### 2.6 Design and implementation constraints

| ID | Constraint | Source |
|---|---|---|
| CN-01 | One board = one Primary Board = up to 8 channels | Brief; `SRS-PRI-P3-001` |
| CN-02 | The Web Application is the **sole** source of programs, configuration and operator commands reaching a board | `SRS-PRI-UI-001`; A-02 |
| CN-03 | The Web Application never communicates with a Secondary Board directly | `SRS-SEC-SW-003` |
| CN-04 | IF-A is the only interface to hardware; transport details reserved | `SRS-PRI-SW-001`; **D-05** |
| CN-05 | The Web Application shall implement the IF-A message set of `ME-ICD-001` §4 without extension | `ICD` §4 |
| CN-06 | The Web Application shall not require the Primary Board to perform any function `ME-SRS-PRI-001` does not require of it | `PRI`; §1.6 precedence |
| CN-07 | External CAN and DBC are out of ME scope | Open Issue **#29** resolved |
| CN-08 | No source file exceeds **2000 lines** | Team engineering policy |
| CN-09 | Code must be modular, decomposed by responsibility | Team engineering policy |
| CN-10 | All schema change managed by versioned migrations | `WAD/SRS` NFR-008; `WAD/RTM` NFR-008 |
| CN-11 | Secrets, credentials and keys shall never be committed to the repository | Team engineering policy |
| CN-12 | Every quantity uses the single unit convention of `ICD` CS-39 | `ICD` CS-39, CS-40 |
| CN-13 | The fault and message code space is shared with the boards and shall not be re-mapped in this application | `ICD` CS-33; conflict **C-14** |
| CN-14 | `<TBD-W02>` — whether ME retains the .NET 8 / Blazor Server / SQLite stack, or is re-platformed | Open Issue **#2** |

### 2.7 Assumptions and dependencies

| ID | Assumption |
|---|---|
| A-01 | ME's Web Application is the existing Ador BTS server **evolved**, not Battery Manager and not a new build. **Subject to Open Issue #2** — see reading note 1. |
| A-02 | The Web Application is the sole originator of programs, configuration and operator commands, and the sole consumer of logged data. |
| A-03 | One channel maps to one Secondary Board and one test circuit. |
| A-04 | `WAD` documents describe an implementation that exists and works at the BTS scale; they are evidence of intent to be confirmed for ME, never requirements. |
| A-05 | `BM` defines required operator-facing behaviour, not compatibility (§1.3). |
| A-06 | Where no source exists, this document records the gap rather than inventing a requirement. |
| A-07 | The plant network is a trusted network **only if Open Issue #10 says so**. Until then no requirement here assumes it. |
| A-08 | Boards are independent; one board's failure does not affect another's session. |
| A-09 | The Web Application's clock is the system time authority distributed to boards (`ICD` A-CFG-11), and is itself synchronised by a means outside this document → `<TBD-W04>`. |

---

## 3. External Interface Requirements

### 3.1 Hardware interfaces

The Web Application has **no direct hardware interface**. It is recorded here because its
absence is a requirement, not an omission.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-HW-001 | The Web Application shall not require any hardware interface other than a general-purpose network interface. | `WAD/ADD` §5; derived |
| SRS-WEB-HW-002 | The Web Application shall not require physical proximity to any board. | `WAD/SRS` §2.1 |
| SRS-WEB-HW-003 | The Web Application shall retain all persistent data on storage that survives its own restart and the restart of its host. | `WAD/DBD` §1, §2 |
| SRS-WEB-HW-004 | The Web Application shall record `<TBD-W05>` (the required storage capacity, derived from the retention policy of Open Issue **#11** and the logged-data rate of §5.1). | Unsourced |

### 3.2 Software interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-SW-001 | The Web Application shall present exactly one logical interface to each Primary Board (IF-A), carrying all program, configuration, control, calibration, telemetry, logged-data and event traffic. | `SRS-PRI-SW-001`; `ICD` §2.2 |
| SRS-WEB-SW-002 | The Web Application shall implement the IF-A message set defined in `ME-ICD-001` §4. | `ICD` §4; CN-05 |
| SRS-WEB-SW-003 | The Web Application shall address each board independently, such that the failure of one board's interface does not impair another's. | NEW — ME; A-08 |
| SRS-WEB-SW-004 | The Web Application shall address each of a board's up to eight channels within that board's single interface, and shall not open a separate connection per channel. | `SRS-PRI-SW-001`; §2.1 change 1; **supersedes** `WAD/RTM` FR-002.6 |
| SRS-WEB-SW-005 | The Web Application shall expose a REST interface for external integration. | `WAD/RTM` FR-010.12; `WAD/ICD` §7 |
| SRS-WEB-SW-006 | The Web Application shall expose a server-sent-event interface for live data. | `WAD/RTM` FR-003.7; `WAD/ICD` §7.5 |
| SRS-WEB-SW-007 | The Web Application shall expose a Model Context Protocol tool interface. | `WAD/RTM` FR-010.13; `WAD/ICD` §8 |
| SRS-WEB-SW-008 | The Web Application shall record `<TBD-W06>` (whether the REST, SSE and MCP interfaces are in ME scope, and for which consumers). None is mentioned in `WAD/SRS`; all three exist in the implementation. See conflict **C-22** and Open Issue **W-36**. | `WAD/RTM` vs `WAD/SRS` |
| SRS-WEB-SW-009 | The Web Application shall be structured in separated presentation, application-service, business-logic and data layers. | `WAD/ADD` §2; CN-09 |
| SRS-WEB-SW-010 | The Web Application shall isolate all IF-A encoding and decoding in one layer, such that no presentation-layer or business-logic component constructs or parses an IF-A message. | `WAD/ADD` §2.2 (`DecoderService`, `PacketAnalyzer`); CN-09 |

### 3.3 Communication interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-CM-001 | The Web Application shall verify the integrity of every message received on IF-A and shall discard a failing message without acting on its content. | `ICD` CS-12, CS-13; `WAD/RTM` FR-010.11 |
| SRS-WEB-CM-002 | The Web Application shall count discarded messages per board and make the count available as a diagnostic. | `ICD` CS-14 |
| SRS-WEB-CM-003 | The Web Application shall acknowledge or negatively acknowledge every IF-A message it receives that requires a response. | `ICD` CS-07 |
| SRS-WEB-CM-004 | The Web Application shall report a rejection with a reason that distinguishes an unrecognised message, a recognised message with invalid parameters, and a message invalid in the current state. | `ICD` CS-08 |
| SRS-WEB-CM-005 | The Web Application shall correlate every response to the request that caused it. | `ICD` CS-10 |
| SRS-WEB-CM-006 | The Web Application shall detect the loss of a board's command session. | `WAD/RTM` FR-002.8; `ICD` §8.1 |
| SRS-WEB-CM-007 | The Web Application shall detect the loss of a board's command session within `<TBD-W07>`, and shall not rely on the absence of live telemetry to infer it. | `ICD` CS-37, `<TBD-I11>`; `WAD/CAP` §7 |
| SRS-WEB-CM-008 | ⚠ The Web Application shall not treat a board's session loss as the end of a session, and shall not close or truncate the session's data on session loss. | `SRS-PRI-P14-*`; `ICD` §8.1 |
| SRS-WEB-CM-009 | The Web Application shall re-establish a board's command session automatically when the board becomes reachable again. | `WAD/SRS` FR-002.4; `WAD/RTM` FR-002.4 |
| SRS-WEB-CM-010 | The Web Application shall reassign an existing board's session state on reconnection rather than creating a duplicate. | `WAD/SRS` FR-002.4 (`AlreadyRegistered 0x02`) |
| SRS-WEB-CM-011 | ⚠ The Web Application shall determine, on reconnection, what logged data it is missing, and shall request it. | `ICD` A-LOG-03; `SRS-PRI-P16-*` |
| SRS-WEB-CM-012 | The Web Application shall timestamp every message it originates that carries a command or a configuration change. | `ICD` CS-21 |
| SRS-WEB-CM-013 | The Web Application shall detect that data it holds for a channel is stale, and shall not present stale data as current. | `ICD` CS-22 |
| SRS-WEB-CM-014 | The Web Application shall record `<TBD-W08>` (the staleness threshold beyond which displayed telemetry is marked invalid rather than merely old) — see `ICD` `<TBD-I04>`. | Unsourced |
| SRS-WEB-CM-015 | The Web Application shall apply a bounded queue with a defined overflow policy to every ingestion path, and shall not use an unbounded queue. | `WAD/CAP` §6 Risk 2; conflict **C-24** |
| SRS-WEB-CM-016 | ⚠ The Web Application shall report, rather than silently drop, any received logged-data record it cannot store. | `WAD/CAP` §9; `ICD` CS-60 |

### 3.4 User interfaces

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-UI-001 | The Web Application shall present its entire operator function set through a web browser, requiring no client installation. | `WAD/SRS` §1.1, §2.1 |
| SRS-WEB-UI-002 | The Web Application shall update live displays without a full page reload. | `WAD/SRS` FR-003.4 |
| SRS-WEB-UI-003 | The Web Application shall present a single view showing all channel positions of a board, including empty positions. | `SRS-PRI-P3-011`; `ICD` A-NOD-02; **NEW — ME** |
| SRS-WEB-UI-004 | The Web Application shall present a single view spanning all boards it serves. | **NEW — ME** |
| SRS-WEB-UI-005 | The Web Application shall preserve per-user view state — pagination, column order, grouping and expansion — across navigation. | `WAD/SRS` FR-010 area; `WAD/RTM` FR-010.3, FR-010.6 |
| SRS-WEB-UI-006 | The Web Application shall present every fault and message using its configured human-readable text, not only its numeric code. | `WAD/RTM` FR-010.1; `WAD/DBD` `CodeMessages` |
| SRS-WEB-UI-007 | The Web Application shall indicate, for every displayed measurement, whether it is live, stale or historical. | `ICD` CS-22; **NEW — ME** |
| SRS-WEB-UI-008 | The Web Application shall support the browsers named in §5.6. | `WAD/SRS` NFR-010 |
| SRS-WEB-UI-009 | The Web Application shall provide a notification mechanism for transient operator feedback and a modal mechanism for operations requiring confirmation. | `WAD/RTM` FR-010.7, FR-010.8 |
| SRS-WEB-UI-010 | ⚠ The Web Application shall require explicit operator confirmation before any command that stops, interrupts or resets a running session, or that changes calibration. | **NEW — ME**; derived from safety |
| SRS-WEB-UI-011 | The Web Application shall not present a control action to a user who lacks the access right to perform it. | `WAD/SRS` FR-008.3; `WAD/RTM` FR-008.3, FR-008.4 |
| SRS-WEB-UI-012 | The Web Application shall record `<TBD-W09>` (whether an operator-facing local language other than English is required). | Unsourced |

---
## 4. Functional Requirements

Coverage key: ● Strong (multiple traceable sources) · ◐ Partial (source exists, gaps
remain) · ○ Absent (no source — Open Issue) · ◆ NEW-ME (no precedent; ME topology change).

### 4.1 W1 — Board discovery and network configuration

**Coverage:** ● Strong. `WAD/SRS` FR-001, `WAD/RTM` FR-001 and `WAD/ICD` §4 all cover it,
and `ICD` §4.1 carries it into ME as message group A-DSC.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W1-001 | The Web Application shall discover boards on the local network without prior knowledge of their addresses. | `WAD/SRS` FR-001.1; `ICD` A-DSC-01 |
| SRS-WEB-W1-002 | The Web Application shall issue the discovery request on every active network interface. | `WAD/SRS` FR-001.1; `WAD/RTM` FR-001.1 |
| SRS-WEB-W1-003 | The Web Application shall receive and interpret a discovery reply carrying the board's unique identifier, its network configuration and its configured server settings. | `WAD/SRS` FR-001.2; `WAD/ICD` §4.2 Q1; `ICD` A-DSC-02 |
| SRS-WEB-W1-004 | The Web Application shall present discovered boards in a live-updating view. | `WAD/SRS` FR-001.5; `WAD/RTM` FR-001.5 |
| SRS-WEB-W1-005 | The Web Application shall distinguish a discovered board that is already known to it from one that is not. | derived; §4.2 |
| SRS-WEB-W1-006 | The Web Application shall permit an Administrator to set a board's IP address, subnet mask, gateway and name-server addresses. | `WAD/SRS` FR-001.3; `WAD/ICD` §4.2 Q4; `ICD` A-DSC-03 |
| SRS-WEB-W1-007 | The Web Application shall permit an Administrator to set the server address and ports a board is to contact. | `WAD/SRS` FR-001.4; `WAD/ICD` §4.2 Q5; `ICD` A-DSC-04 |
| SRS-WEB-W1-008 | The Web Application shall confirm that a network configuration change has taken effect, and shall report the address on which the board is now reachable. | `ICD` A-DSC-05; **NEW — ME**. The baseline protocol has **no confirmation**, so a failed reconfiguration is silent |
| SRS-WEB-W1-009 | The Web Application shall report a network configuration change that it cannot confirm within `<TBD-W10>`. | `ICD` `<TBD-I09>`; derived |
| SRS-WEB-W1-010 | The Web Application shall detect two boards presenting the same unique identifier and shall report the conflict rather than overwriting one with the other. | **NEW — ME** |
| SRS-WEB-W1-011 | The Web Application shall record every network configuration change it issues in its audit trail, including the previous and the new value. | §4.16; `WAD/SRS` FR-008.4 |
| SRS-WEB-W1-012 | The Web Application shall not require discovery to have run in order to communicate with a board whose address is already configured. | derived |
| SRS-WEB-W1-013 | The Web Application shall record `<TBD-W11>` (whether discovery must operate across network segments, which a broadcast-based mechanism cannot do). | Unsourced |

### 4.2 W2 — Session registration and connection management

**Coverage:** ●◆ Strong in mechanism, new in unit. The baseline registers **per circuit**;
ME registers **per board**. This is the single largest structural change in the document.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W2-001 | The Web Application shall accept a session registration initiated by a board. | `WAD/SRS` FR-002.1; `ICD` A-REG-01 |
| SRS-WEB-W2-002 | The Web Application shall accept exactly one session registration per **board**, and shall not require one per channel. | `SRS-PRI-SW-001`; **supersedes** `WAD/SRS` FR-002.2, FR-002.6 |
| SRS-WEB-W2-003 | The Web Application shall interpret a registration carrying the board's identifier, name, network address, hardware address, number of channel positions, and application and bootloader versions. | `WAD/ICD` §3.4; `ICD` A-REG-01 |
| SRS-WEB-W2-004 | The Web Application shall accept a registration only from a board that an Administrator has authorised, and shall reject one from a board that has not been authorised. | `WAD/SRS` FR-002.3, FR-002.5; `WAD/RTM` FR-002.10 |
| SRS-WEB-W2-005 | The Web Application shall respond to a registration distinguishing acceptance, rejection, and re-registration of a board it already holds. | `WAD/SRS` FR-002.3, FR-002.4, FR-002.5 |
| SRS-WEB-W2-006 | The Web Application shall assign a session identifier on acceptance and shall use it to correlate subsequent traffic from that board. | `ICD` A-REG-02; `ICD` CS-10 |
| SRS-WEB-W2-007 | The Web Application shall maintain one command context per registered board, holding that board's connection, its channel inventory, its per-channel state and its data queues. | `WAD/RTM` FR-002.7; `WAD/ADD` §2.2 (`CircuitCommandHandler`); **restated at board level** |
| SRS-WEB-W2-008 | The Web Application shall hold its registry of board contexts in a structure safe for concurrent access. | `WAD/CAP` §6 Risk 3; `WAD/ADD` §2.2 |
| SRS-WEB-W2-009 | The Web Application shall monitor the liveness of every registered board's session independently. | `WAD/RTM` FR-002.8; `ICD` A-REG-04 |
| SRS-WEB-W2-010 | The Web Application shall notify connected operators when a board's session is established or lost. | `WAD/RTM` FR-002.9 |
| SRS-WEB-W2-011 | ⚠ The Web Application shall retain a board's channel state, session associations and unstored data across a loss of that board's session. | `ICD` CS-38; SRS-WEB-CM-008 |
| SRS-WEB-W2-012 | The Web Application shall permit an Administrator to authorise a board and, independently, to authorise each of its channel positions. | `WAD/RTM` FR-002.10; **extended to channel positions for ME** |
| SRS-WEB-W2-013 | The Web Application shall permit an Administrator to withdraw a board's authorisation, and shall refuse subsequent registrations from it. | `WAD/ICD` §7.3 (delete-device); `ICD` A-REG-03 |
| SRS-WEB-W2-014 | ⚠ The Web Application shall refuse to withdraw a board's authorisation while any of its channels has a running session, unless the withdrawal is explicitly forced. | **NEW — ME**; derived |
| SRS-WEB-W2-015 | The Web Application shall close a board's session cleanly on its own orderly shutdown. | `ICD` A-REG-03 |
| SRS-WEB-W2-016 | The Web Application shall record every registration, rejection, re-registration and session loss in its audit trail. | §4.16 |
| SRS-WEB-W2-017 | The Web Application shall record `<TBD-W12>` (how a board is identified for authorisation: unique hardware identifier, network address, or hardware address — the baseline registration carries all three, and network address is not stable). | `WAD/ICD` §3.4 |
| SRS-WEB-W2-018 | The Web Application shall record `<TBD-W01>` (the maximum number of boards one instance must serve concurrently) — see §2.3 and §5.1. | Unsourced |

### 4.3 W3 — Channel inventory and presence

**Coverage:** ◆ New to ME. No precedent: in BTS a device's circuit population was static
and known from the database. In ME it is dynamic and reported by the board.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W3-001 | The Web Application shall obtain from each board the population of its eight channel positions. | `ICD` A-NOD-01, A-NOD-02; `SRS-PRI-P3-011` |
| SRS-WEB-W3-002 | The Web Application shall distinguish a populated channel position from an empty one, and both from a position whose state is unknown. | `ICD` A-NOD-02; **NEW — ME** |
| SRS-WEB-W3-003 | The Web Application shall obtain, for each populated position, the Secondary Board's node identity, type number, hardware version, bootloader version and application version. | `ICD` A-NOD-02; `SRS-PRI-P3-004` |
| SRS-WEB-W3-004 | The Web Application shall obtain the board's verdict on whether each populated position's software version is compatible with the board's own. | `SRS-PRI-P3-012`; `ICD` A-NOD-02 |
| SRS-WEB-W3-005 | ⚠ The Web Application shall not offer to start a session on a channel the board has reported as incompatible. | `SRS-PRI-P3-012`; derived |
| SRS-WEB-W3-006 | The Web Application shall refresh a board's channel inventory on session establishment. | `ICD` A-NOD-01; derived |
| SRS-WEB-W3-007 | The Web Application shall receive and act on an unsolicited report that a channel position's presence has changed. | `ICD` A-NOD-03; `SRS-PRI-P3-009` |
| SRS-WEB-W3-008 | The Web Application shall present a channel presence change to connected operators without requiring a refresh. | derived; SRS-WEB-UI-002 |
| SRS-WEB-W3-009 | The Web Application shall record every channel presence change in its audit trail, with the time and the position affected. | `SRS-PRI-P3-010`; §4.16 |
| SRS-WEB-W3-010 | ⚠ The Web Application shall report, and shall not silently discard, the case where a channel with a running session becomes absent. | `ICD` A-NOD-03; Open Issue **#14** |
| SRS-WEB-W3-011 | The Web Application shall receive and present a report of two Secondary Boards claiming the same node identity. | `ICD` A-NOD-04; `SRS-PRI-P3-005` |
| SRS-WEB-W3-012 | The Web Application shall maintain per-channel-position configuration — limits, calibration association, access control — independently of which Secondary Board currently occupies the position. | `ICD` CS-04; **NEW — ME** |
| SRS-WEB-W3-013 | The Web Application shall detect that the Secondary Board occupying a position has been replaced, and shall report it. | derived from SRS-WEB-W3-003; **NEW — ME** |
| SRS-WEB-W3-014 | The Web Application shall record `<TBD-W13>` (whether per-channel-position history must be retained when a Secondary Board is replaced, for traceability of past sessions). | Unsourced |
| SRS-WEB-W3-015 | The Web Application shall record `<TBD-W14>` (whether an operator may start a session when fewer than the configured number of channels are present) — see Open Issue **#14** and `<TBD-21>` in `ME-SRS-PRI-001`. | Unsourced |

### 4.4 W4 — Program authoring, storage and validation

**Coverage:** ● Strong for the mechanism; ◐ Partial for validation, because the program
language `ME-SRS-PRI-001` §4.7 specifies in full is far richer than the baseline editor
handles.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W4-001 | The Web Application shall permit the creation of a test program consisting of an ordered sequence of steps. | `WAD/SRS` FR-005.1; `WAD/RTM` FR-005.1 |
| SRS-WEB-W4-002 | The Web Application shall represent each step as a label, an operator, a nominal value or values, a limit set, an action and a registration selection. | BM §12.4; `SRS-PRI-P5-002` |
| SRS-WEB-W4-003 | The Web Application shall support every operator that `ME-SRS-PRI-001` §4.7 requires the Primary Board to execute. | `SRS-PRI-P7-*`; Open Issue **#5** resolved — all operators in scope |
| SRS-WEB-W4-004 | The Web Application shall list, read, update and soft-delete programs, and shall permit recovery of a soft-deleted program. (*"Soft-delete" means marking it as deleted and hiding it rather than erasing it, so a past test that used it can still be explained.*) | `WAD/RTM` FR-005.2, FR-005.3, FR-005.4 |
| SRS-WEB-W4-005 | The Web Application shall retain programs in persistent storage. | `WAD/DBD` §1.2 (`BtsPrograms`) |
| SRS-WEB-W4-006 | The Web Application shall hold a program version identity distinct from the program name, and shall increment it on every change to program content. | `SRS-PRI-P4-003`; BM §12.4.8.16 (SYNCProgram is version-sensitive); Open Issue **#25** |
| SRS-WEB-W4-007 | ⚠ The Web Application shall not modify a program version that is referenced by a running session; a change shall create a new version. | Open Issue **#25**; `SRS-PRI-P4-009`; **NEW — ME** |
| SRS-WEB-W4-008 | The Web Application shall validate a program before permitting its transfer. | `WAD/SRS` FR-005.9; derived |
| SRS-WEB-W4-009 | The Web Application shall reject a program containing an operator the target board does not support. | derived from SRS-WEB-W4-003 |
| SRS-WEB-W4-010 | The Web Application shall reject a program whose nominal value, limit or action is invalid for its operator. | `LSRS` SW_REQ_199, 201, 204, 217, 230; BM §12.4 |
| SRS-WEB-W4-011 | The Web Application shall reject a program containing a jump whose destination label does not exist. | BM §12.4.5 `GOTO`; `SRS-PRI-P7-*` |
| SRS-WEB-W4-012 | The Web Application shall reject a program whose cycle structure is unbalanced. | BM §12.5.1; conflict **C-13** |
| SRS-WEB-W4-013 | The Web Application shall reject a program whose cycle nesting exceeds the depth the target board supports. | conflict **C-13**; `<TBD-13>` in `ME-SRS-PRI-001` |
| SRS-WEB-W4-014 | The Web Application shall report every validation failure identifying the step and the reason. | derived |
| SRS-WEB-W4-015 | The Web Application shall support a PRODUCER step that references another saved program by name. | `WAD/SRS` FR-005.6 |
| SRS-WEB-W4-016 | The Web Application shall offer, when editing a PRODUCER step, a selection of all saved programs excluding the program being edited. | `WAD/SRS` FR-005.7 |
| SRS-WEB-W4-017 | The Web Application shall provide a read-only preview of a PRODUCER step's referenced program. | `WAD/SRS` FR-005.8 |
| SRS-WEB-W4-018 | The Web Application shall reject a program whose PRODUCER step references a missing, empty or self-referential program. | `WAD/SRS` FR-005.9; **self-reference added** — the baseline excludes self-reference only from the *dropdown*, not from validation |
| SRS-WEB-W4-019 | The Web Application shall reject a program whose PRODUCER references form a cycle. | **NEW — ME**. Unbounded recursive expansion is otherwise possible |
| SRS-WEB-W4-020 | The Web Application shall record `<TBD-W15>` (the maximum permitted PRODUCER nesting depth). | Unsourced |
| SRS-WEB-W4-021 | ⚠ The Web Application shall warn, when a PRODUCER step is present, that step-number-based jumps inside the referenced program may resolve incorrectly after expansion, and shall identify each such jump. | `WAD/SRS` FR-005.11; **strengthened** — the baseline states the hazard as a preference, not a check |
| SRS-WEB-W4-022 | The Web Application shall support a TABLE step referencing an external file supplying its rows. | `WAD/SRS` FR-011.2; `WAD/DBD` migration *TableOP* |
| SRS-WEB-W4-023 | The Web Application shall reject a program whose TABLE step references a missing or unparseable file. | derived from SRS-WEB-W4-022 |
| SRS-WEB-W4-024 | The Web Application shall associate a battery parameter record with a program. | `SRS-PRI-P4-012`; BM §12.3 |
| SRS-WEB-W4-025 | The Web Application shall associate a registration format with a program. | `SRS-PRI-P4-013`; BM §12.4.1 |
| SRS-WEB-W4-026 | The Web Application shall record every program creation, change and deletion in its audit trail. | §4.16 |
| SRS-WEB-W4-027 | The Web Application shall record `<TBD-W16>` (whether the PRODUCER model or the `BM` Procedure model is adopted; the two are not equivalent) — see Open Issue **#7**. | Open Issue **#7** |
| SRS-WEB-W4-028 | The Web Application shall record `<TBD-W17>` (whether battery-parameter-relative nominal values are resolved by this application at authoring time or by the Primary Board at run time) — see Open Issue **#6**. | Open Issue **#6** |

### 4.5 W5 — Program transfer to the Primary Board

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W5-001 | The Web Application shall confirm a board is ready to receive a program before beginning a transfer. | `WAD/SRS` FR-005.3; `ICD` A-PRG-01; CS-36 |
| SRS-WEB-W5-002 | The Web Application shall transfer program metadata — identity, version, name and total step count — before program content. | `ICD` A-PRG-02, A-PRG-03; `SRS-PRI-P4-003` |
| SRS-WEB-W5-003 | The Web Application shall transfer program content in ordered segments and shall not exceed the transfer unit the interface permits. | `ICD` A-PRG-04; `WAD/CAP` §7 (1400-byte chunk) |
| SRS-WEB-W5-004 | ⚠ The Web Application shall expand every PRODUCER step before transfer, inlining the referenced program's steps and renumbering the result sequentially. | `WAD/SRS` FR-005.10 |
| SRS-WEB-W5-005 | The Web Application shall omit, when expanding a PRODUCER step, the referenced program's leading initialisation step and trailing termination step. | `WAD/SRS` FR-005.10 |
| SRS-WEB-W5-006 | ⚠ The Web Application shall retain the mapping from every expanded step back to its source program and source step number. | **NEW — ME**. Without it, a fault reported against an expanded step number cannot be attributed to a program the operator recognises |
| SRS-WEB-W5-007 | The Web Application shall transfer the battery parameter record associated with the program. | `WAD/RTM` FR-007.4; `ICD` A-CFG-10 |
| SRS-WEB-W5-008 | The Web Application shall transfer the registration format associated with the program. | `ICD` A-PRG-09 |
| SRS-WEB-W5-009 | The Web Application shall interpret the board's report of transfer outcome and shall distinguish each defined rejection reason. | `ICD` A-PRG-05; `SRS-PRI-P4-008` |
| SRS-WEB-W5-010 | ⚠ The Web Application shall not report a program as transferred until the board has confirmed acceptance. | `ICD` A-PRG-05; derived |
| SRS-WEB-W5-011 | The Web Application shall retransmit an entire program when a transfer fails, and shall not attempt to patch a partial transfer. | `ICD` CS-26, CS-27 |
| SRS-WEB-W5-012 | The Web Application shall abandon a transfer that has not completed within `<TBD-W18>` and shall report it. | `ICD` `<TBD-I12>` |
| SRS-WEB-W5-013 | The Web Application shall list the programs currently resident on a board. | `ICD` A-PRG-06; `SRS-PRI-P4-014` |
| SRS-WEB-W5-014 | The Web Application shall retrieve a resident program's content from a board and shall report any difference from its own copy. | `ICD` A-PRG-07; `SRS-PRI-P4-015`; **difference check is NEW — ME** |
| SRS-WEB-W5-015 | The Web Application shall delete a resident program from a board. | `ICD` A-PRG-08 |
| SRS-WEB-W5-016 | ⚠ The Web Application shall not transfer a program to a channel whose session is running. | `SRS-PRI-P4-009`; derived |
| SRS-WEB-W5-017 | The Web Application shall record every program transfer, including its outcome, in its audit trail. | §4.16 |

### 4.6 W6 — Program scheduling

**Coverage:** ○ Absent from `WAD/SRS` entirely; ● Strong in `WAD/RTM` and `WAD/DBD`.

> **This whole feature is missing from the developer's SRS.** `WAD/RTM` FR-011 specifies
> ten scheduler requirements, `WAD/DBD` defines the `ProgramSchedules` and
> `ScheduleExecutionLogs` tables and migration *AddProgramScheduler* (2026-05-05), and
> `WAD/VVP` adds VAL-UI-008 and VAL-UI-009 for it — but `WAD/SRS` v0.6 does not mention
> scheduling, and its FR-011 is a different feature entirely. **Conflict C-22.** The
> requirements below are reconstructed from `WAD/RTM`, `WAD/DBD` and `WAD/VVP`.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W6-001 | The Web Application shall permit the creation of a named schedule. | `WAD/RTM` FR-011.1; `WAD/DBD` `ProgramSchedules` |
| SRS-WEB-W6-002 | The Web Application shall associate a schedule with a program, a battery, an execution time and a set of target channels. | `WAD/RTM` FR-011.1 |
| SRS-WEB-W6-003 | The Web Application shall hold a schedule's execution time in a single unambiguous time reference. | `WAD/DBD` `ProgramSchedules.ScheduledAt` (UTC) |
| SRS-WEB-W6-004 | The Web Application shall permit a schedule to target channels on more than one board. | `WAD/DBD` `TargetCircuitsJson`; **NEW — ME** extension to eight channels per board |
| SRS-WEB-W6-005 | The Web Application shall list schedules with their program, battery and target names resolved to human-readable form. | `WAD/RTM` FR-011.2 |
| SRS-WEB-W6-006 | The Web Application shall permit a schedule to be enabled or disabled without deleting it. | `WAD/RTM` FR-011.4; `WAD/DBD` `IsActive` |
| SRS-WEB-W6-007 | The Web Application shall skip a disabled schedule at its execution time. | `WAD/DBD` `IsActive`; derived |
| SRS-WEB-W6-008 | The Web Application shall permit a schedule to be deleted, and shall cancel its pending execution. | `WAD/RTM` FR-011.3, FR-011.10 |
| SRS-WEB-W6-009 | The Web Application shall, at a schedule's execution time, transfer the program and battery parameters to every target channel and start the session. | `WAD/RTM` FR-011.5 |
| SRS-WEB-W6-010 | ⚠ The Web Application shall skip a target channel whose board is not reachable, and shall record the skip with that reason. | `WAD/RTM` FR-011.6 (`Skipped_Offline`) |
| SRS-WEB-W6-011 | ⚠ The Web Application shall skip a target channel that already has a running session, and shall record the skip with that reason. | `WAD/RTM` FR-011.7 (`Skipped_AlreadyRunning`) |
| SRS-WEB-W6-012 | ⚠ The Web Application shall skip a target channel position that is empty, and shall record the skip with that reason. | **NEW — ME**. A channel position can be empty in ME; a BTS circuit could not |
| SRS-WEB-W6-013 | ⚠ The Web Application shall skip a target channel for which the requesting schedule's owner lacks start rights, and shall record the skip with that reason. | **NEW — ME**; §4.15. `WAD` does not state whose authority a scheduled start carries → **W-37** |
| SRS-WEB-W6-014 | The Web Application shall record the per-channel outcome of every schedule execution, including a failure reason where it failed. | `WAD/RTM` FR-011.8; `WAD/DBD` `ScheduleExecutionLogs` |
| SRS-WEB-W6-015 | The Web Application shall present the execution log filtered by schedule. | `WAD/RTM` FR-011.9 |
| SRS-WEB-W6-016 | ⚠ The Web Application shall execute a schedule whose execution time has passed while it was not running, or shall record that it was missed; it shall not silently ignore it. | **NEW — ME**. `WAD` does not state the behaviour of a schedule missed across a restart → **W-38** |
| SRS-WEB-W6-017 | The Web Application shall reject a schedule whose execution time is in the past at creation. | derived |
| SRS-WEB-W6-018 | The Web Application shall record `<TBD-W19>` (whether recurring schedules are required, or only single-shot; every `WAD` source describes a single execution time). | `WAD/DBD`; `WAD/RTM` |
| SRS-WEB-W6-019 | The Web Application shall record every schedule creation, change, deletion and execution in its audit trail. | §4.16 |

### 4.7 W7 — Channel assignment and execution control

**Coverage:** ●◆ The commands are strongly sourced; multi-channel semantics are new.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W7-001 | The Web Application shall assign a program to one or more channels. | `ICD` A-NOD-05; `SRS-PRI-P6-*` |
| SRS-WEB-W7-002 | The Web Application shall present a channel's current program assignment. | `ICD` A-NOD-06 |
| SRS-WEB-W7-003 | The Web Application shall start a session on one or more channels in one operator action. | `WAD/SRS` FR-005.5; `WAD/RTM` FR-005.7; `ICD` A-CTL-01; **multi-channel is NEW — ME** |
| SRS-WEB-W7-004 | The Web Application shall stop a session on one or more channels. | `WAD/RTM` FR-005.8; `ICD` A-CTL-02 |
| SRS-WEB-W7-005 | The Web Application shall pause and continue a session. | `WAD/RTM` FR-005.9, FR-005.10; `ICD` A-CTL-03, A-CTL-05 |
| SRS-WEB-W7-006 | The Web Application shall interrupt a session, distinguishably from pausing it. | `ICD` A-CTL-04; BM §12.4.5 `INT` |
| SRS-WEB-W7-007 | The Web Application shall reset a channel, and separately a board. | `WAD/RTM` FR-010.10; `ICD` A-CTL-06 |
| SRS-WEB-W7-008 | The Web Application shall clear a latched fault on a channel. | `ICD` A-CTL-07; Open Issue **#17** |
| SRS-WEB-W7-009 | The Web Application shall set a channel's spare digital outputs. | `ICD` A-CTL-08; Open Issue **#19** |
| SRS-WEB-W7-010 | ⚠ The Web Application shall interpret and present the **per-channel** outcome of every control command, and shall not report a command as successful because it succeeded on some channels. | `ICD` A-CTL-09; **NEW — ME**. The baseline acknowledges per device, which in ME would mask seven failures |
| SRS-WEB-W7-011 | ⚠ The Web Application shall not prevent a control command reaching the channels that can accept it because another addressed channel cannot. | `SRS-PRI-CM-007`; **NEW — ME** |
| SRS-WEB-W7-012 | The Web Application shall support a start that is deferred to a stated time, distinctly from a schedule. | BM §2 (deferred start); `SRS-PRI-P19-*` |
| SRS-WEB-W7-013 | The Web Application shall support starting a program from a nominated step rather than its first step. | BM §2 (start-from-step); `SRS-PRI-P19-*` |
| SRS-WEB-W7-014 | The Web Application shall require a session name at start, and shall associate it with the session's recorded data. | BM §2 (session naming); `WAD/DBD` `BatterySessions` |
| SRS-WEB-W7-015 | The Web Application shall associate a battery and a limit standard with each session at start. | `WAD/SRS` FR-007.3; `WAD/DBD` `BatterySessions` |
| SRS-WEB-W7-016 | ⚠ The Web Application shall verify, before starting a session, that the channel is present, compatible, fault-free and not already running. | `WAD/RTM` FR-011.6, FR-011.7; SRS-WEB-W3-005; derived |
| SRS-WEB-W7-017 | The Web Application shall report to the operator why a start was refused. | derived; `ICD` CS-08 |
| SRS-WEB-W7-018 | The Web Application shall support synchronised start across a group of channels, where synchronised execution is required. | BM §12.4.8.16 (SYNCLine/SYNCProgram); Open Issue **#16** |
| SRS-WEB-W7-019 | The Web Application shall record `<TBD-W20>` (whether synchronised multi-channel execution is required, and the permitted start skew across a group) — see Open Issue **#16** and `ICD` `<TBD-I13>`. | Open Issue **#16** |
| SRS-WEB-W7-020 | The Web Application shall record every control command, its issuer and its per-channel outcome in its audit trail. | §4.16; `WAD/DBD` `ProgramAuditExecution` |
| SRS-WEB-W7-021 | The Web Application shall record `<TBD-W21>` (whether one program instance may span several channels as a single logical test, or whether a multi-channel start is always *n* independent sessions) — see Open Issue **#15**. | Open Issue **#15** |

### 4.8 W8 — Real-time monitoring

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W8-001 | The Web Application shall receive live telemetry from every board and route it to the correct channel. | `WAD/SRS` FR-003.1; `WAD/RTM` FR-003.3; `ICD` A-TLM-01 |
| SRS-WEB-W8-002 | The Web Application shall present, per channel, the measured current, voltage, temperature and power; the accumulated, charge, discharge and step capacity; the accumulated, charge, discharge and step energy; the channel state; the active fault set; and the digital input and output state. | `WAD/SRS` FR-003.2; `WAD/RTM` FR-003.5; `WAD/ICD` §5.2 (25 fields) |
| SRS-WEB-W8-003 | The Web Application shall present, per channel, the running step number, the step elapsed time and the total elapsed time. | `WAD/ICD` §5.2; `ICD` A-TLM-01 |
| SRS-WEB-W8-004 | The Web Application shall present the cycle number and, within a TABLE step, the row number. | `WAD/ICD` §5.2 |
| SRS-WEB-W8-005 | ⚠ The Web Application shall present the step number in terms the operator authored, resolving expanded PRODUCER steps back to their source program. | SRS-WEB-W5-006; **NEW — ME** |
| SRS-WEB-W8-006 | The Web Application shall present a board-level aggregate view across all its channel positions. | `ICD` A-TLM-02; **NEW — ME** |
| SRS-WEB-W8-007 | The Web Application shall update a live display within the latency stated in §5.1 of receiving the data. | `WAD/SRS` NFR-002; `WAD/RTM` NFR-002 |
| SRS-WEB-W8-008 | The Web Application shall push live updates to connected browsers without polling. | `WAD/SRS` FR-003.4; `WAD/ADD` §2.1 |
| SRS-WEB-W8-009 | The Web Application shall route live calibration data separately from ordinary live telemetry. | `WAD/SRS` FR-003.3; `WAD/RTM` FR-003.6; `ICD` A-TLM-04 |
| SRS-WEB-W8-010 | The Web Application shall permit selection of which channels stream live, and at what rate. | `ICD` A-TLM-03; **NEW — ME** |
| SRS-WEB-W8-011 | ⚠ The Web Application shall not allow live-telemetry processing to delay or displace the recording of logged data. | `WAD/CAP` §6 Risk 5; **NEW — ME** |
| SRS-WEB-W8-012 | The Web Application shall apply a bound to the rate at which it propagates live updates to a browser, independent of the rate at which it receives them. (*Without this, eight fast channels can flood the browser with more redraws than it can paint.*) | `WAD/CAP` §6 Risk 5 (no backpressure — *no way for a slow consumer to tell a fast producer to wait*); **NEW — ME** |
| SRS-WEB-W8-013 | ⚠ The Web Application shall mark a channel's live display as stale when its telemetry stops, and shall not continue to present the last value as current. | `ICD` CS-22; SRS-WEB-CM-013 |
| SRS-WEB-W8-014 | The Web Application shall present a chart of any selected live quantity over the session's elapsed time. | `WAD/ADD` §2.1 (Highcharts); derived |
| SRS-WEB-W8-015 | The Web Application shall provide live data to external consumers over its server-sent-event interface. | `WAD/RTM` FR-003.7; `WAD/ICD` §7.5 |

### 4.9 W9 — Session data recording

**Coverage:** ● Strong, and the area where `WAD/CAP` identifies the implementation's
principal risk.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W9-001 | ⚠ The Web Application shall receive logged measurement records from every board and shall persist them. | `WAD/SRS` FR-004.1, FR-004.2; `ICD` A-LOG-01 |
| SRS-WEB-W9-002 | The Web Application shall persist each session's records in a store dedicated to that session. | `WAD/SRS` FR-004.2, FR-004.3; `WAD/DBD` §2 |
| SRS-WEB-W9-003 | The Web Application shall name each session store deterministically from the session identity and the channel it belongs to. | `WAD/SRS` FR-004.3; `WAD/DBD` §2 |
| SRS-WEB-W9-004 | The Web Application shall record, per measurement row, the elapsed program time; the current, voltage, temperature and power; the four capacity and the four energy accumulators; the step number; the operator; the fault bitmask; the message identity; the user-error identity; and a timestamp. | `WAD/DBD` §2.1; `WAD/ICD` §6.3 |
| SRS-WEB-W9-005 | The Web Application shall decouple receiving a logged record from writing it, so that a slow write cannot block reception. | `WAD/SRS` FR-004.4; `WAD/ADD` §2.4 |
| SRS-WEB-W9-006 | ⚠ The Web Application shall apply a bounded queue with a defined overflow policy on every stage of the logged-data path. | `WAD/CAP` §6 Risk 2; SRS-WEB-CM-015; conflict **C-24** |
| SRS-WEB-W9-007 | ⚠ The Web Application shall process logged data from different channels concurrently, such that one channel's volume does not delay another's. | `WAD/CAP` §6 Risk 1 — the baseline uses a **single** processor task for all circuits; conflict **C-24** |
| SRS-WEB-W9-008 | The Web Application shall report, per channel, the count of records received, the count written and the count awaiting write. | `WAD/CAP` §9 (`Storerecordcount`, `Unstorerecordcount`); `WAD/RTM` FR-004.7 |
| SRS-WEB-W9-009 | ⚠ The Web Application shall raise an operator-visible alarm when a channel's write backlog grows continuously. | `WAD/CAP` §9 (alert threshold); **NEW — ME** as a requirement |
| SRS-WEB-W9-010 | ⚠ The Web Application shall acknowledge logged data to the board, so that the board can retain unacknowledged data and retransmit it. | `ICD` A-LOG-02, CS-57; **NEW — ME**. The baseline path is unacknowledged — see §8.2 **C-23** |
| SRS-WEB-W9-011 | ⚠ The Web Application shall be able to determine, for any session, whether its recorded data is complete, and shall be able to distinguish a gap from a period in which nothing was recorded. | `ICD` CS-59; **NEW — ME** |
| SRS-WEB-W9-012 | ⚠ The Web Application shall request retransmission of logged data it has determined to be missing. | `ICD` A-LOG-03, CS-58 |
| SRS-WEB-W9-013 | ⚠ The Web Application shall record a durable marker in a session's store for any interval in which data was lost, so that a gap is visible to a later reader of that store alone. | **NEW — ME**; `ICD` CS-59 |
| SRS-WEB-W9-014 | The Web Application shall open a session store at session start, recording the program identity and version, the battery, the limit standard, the channel and the start time. | `ICD` A-LOG-05; `WAD/DBD` `BatterySessions` |
| SRS-WEB-W9-015 | The Web Application shall close a session store at session end, recording the end time, the end reason, the final accumulator values and the record count. | `ICD` A-LOG-06; `WAD/DBD` `BatterySessions` |
| SRS-WEB-W9-016 | ⚠ The Web Application shall flush all buffered records for a session to durable storage before reporting the session closed. | derived; **NEW — ME** |
| SRS-WEB-W9-017 | The Web Application shall record every start, stop, pause and continue action against a session in that session's own store. | `WAD/DBD` §2.2 (`ProgramAuditExecution`) |
| SRS-WEB-W9-018 | ⚠ The Web Application shall survive its own restart during a running session without losing records already written, and shall resume recording to the same session store. | **NEW — ME**; `WAD/SRS` NFR-003 implies restart is expected |
| SRS-WEB-W9-019 | The Web Application shall use a storage mode that keeps a session store readable and consistent while it is being written. | `WAD/RTM` NFR-013 (WAL) |
| SRS-WEB-W9-020 | The Web Application shall report the storage space remaining, and shall warn before it is exhausted. | Open Issue **#11**; **NEW — ME** |
| SRS-WEB-W9-021 | ⚠ The Web Application shall apply a defined policy when storage is exhausted, and shall not silently stop recording. | Open Issue **#11**; `<TBD-W22>` |
| SRS-WEB-W9-022 | The Web Application shall record `<TBD-W22>` (the retention period for session data and the behaviour when storage is exhausted: refuse to start, stop running sessions, overwrite oldest, or alarm only) — see Open Issue **#11**. | Unsourced |
| SRS-WEB-W9-023 | The Web Application shall record `<TBD-W23>` (the recovery point objective for logged data: how much recorded data may be lost in the worst case) — see `ICD` `<TBD-I28>`. | Unsourced |
| SRS-WEB-W9-024 | The Web Application shall record `<TBD-W03>` (whether one store per session, on the baseline engine, remains adequate at the ME channel count) — see §2.3 and Open Issue **W-34**. | `WAD/CAP`; `WAD/DBD` |

### 4.10 W10 — Session self-containment

**Coverage:** ● Strong. Sourced entirely from `WAD/SRS` FR-011, which — note — is a
*different* FR-011 from `WAD/RTM`'s.

> **Why this area matters more in ME than in BTS.** A session store must be readable years
> later, on a machine that has no access to the live program database. In ME a program can
> be assigned to eight channels and expanded from PRODUCER references; without
> self-containment, a stored session cannot be interpreted at all once the referenced
> programs have been edited.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W10-001 | ⚠ The Web Application shall write into a session's own store, at session start, the complete step definition of the program as transferred. | `WAD/SRS` FR-011.1; **extended** — the baseline stores only PRODUCER sub-programs |
| SRS-WEB-W10-002 | ⚠ The Web Application shall write into a session's own store the complete step definition of every PRODUCER-referenced program. | `WAD/SRS` FR-011.1 |
| SRS-WEB-W10-003 | ⚠ The Web Application shall write into a session's own store the content of every TABLE file the program references. | `WAD/SRS` FR-011.2 |
| SRS-WEB-W10-004 | ⚠ The Web Application shall write into a session's own store the battery parameter record and the limit standard in force. | **NEW — ME**. Neither is stored in the baseline session store, so a session's own limits are unrecoverable from it |
| SRS-WEB-W10-005 | ⚠ The Web Application shall write into a session's own store the calibration parameters in force on the channel at session start. | **NEW — ME**. Without them a recorded measurement cannot be re-derived or defended |
| SRS-WEB-W10-006 | The Web Application shall write into a session's own store the identity, versions and node identity of the board and Secondary Board that ran it. | **NEW — ME**; SRS-WEB-W3-003 |
| SRS-WEB-W10-007 | The Web Application shall write into a session's own store the code-message texts for every fault, message and user error the session recorded. | **NEW — ME**; `WAD/DBD` `CodeMessages` is otherwise only in the main store |
| SRS-WEB-W10-008 | The Web Application shall provide a viewer that reads a session store without requiring the main store. | `WAD/SRS` FR-011.3, FR-011.4 |
| SRS-WEB-W10-009 | The viewer shall load PRODUCER sub-program steps from the session store, not from the live program database. | `WAD/SRS` FR-011.3 |
| SRS-WEB-W10-010 | The viewer shall load TABLE file content from the session store, not from the file system. | `WAD/SRS` FR-011.4 |
| SRS-WEB-W10-011 | The viewer shall present a PRODUCER step's referenced steps in a read-only view. | `WAD/SRS` FR-011.5 |
| SRS-WEB-W10-012 | The viewer shall present a TABLE step's file content in a read-only view. | `WAD/SRS` FR-011.6 |
| SRS-WEB-W10-013 | ⚠ The Web Application shall verify, at session start, that everything the session store requires for self-containment was written, and shall not start the session if it was not. | **NEW — ME**; derived |
| SRS-WEB-W10-014 | The Web Application shall record the version of its own software that created a session store, in that store. | **NEW — ME** |

---
### 4.11 W11 — Reporting and export

**Coverage:** ◐ Partial. `WAD/SRS` FR-010 gives three requirements; the ME channel count
makes comparison across channels a new need.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W11-001 | The Web Application shall list completed sessions with their program, battery, channel, start and end time, duration and outcome. | `WAD/SRS` FR-010.3; `WAD/DBD` `BatterySessions` |
| SRS-WEB-W11-002 | The Web Application shall present a session properties view naming the program, the battery, the board and channel, the times, the duration, every embedded PRODUCER sub-program and every captured TABLE file. | `WAD/SRS` FR-010.3 |
| SRS-WEB-W11-003 | The Web Application shall export a session's recorded data to a spreadsheet format. | `WAD/SRS` FR-010.1; `WAD/RTM` VAL-UI-007 |
| SRS-WEB-W11-004 | ⚠ The Web Application shall include in an export every column present in the session store, and shall not silently omit any. | `WAD/VVP` VAL-UI-006 ("all columns"); **NEW — ME** as a requirement |
| SRS-WEB-W11-005 | The Web Application shall state, in an export, the session's identity, the program identity and version, the battery, the channel, the calibration in force and the export time. | SRS-WEB-W10-004, -005; **NEW — ME** |
| SRS-WEB-W11-006 | ⚠ The Web Application shall mark, in an export, any interval in which recorded data was lost. | SRS-WEB-W9-013; **NEW — ME** |
| SRS-WEB-W11-007 | The Web Application shall export a filtered subset of a session's data by time range or by step. | derived |
| SRS-WEB-W11-008 | The Web Application shall present a comparison of the same quantity across several channels or several sessions. | **NEW — ME**. With eight channels per board, cross-channel comparison is the normal case |
| SRS-WEB-W11-009 | The Web Application shall present the audit trail with filters on actor, action, entity and time range. | `WAD/SRS` FR-010.2 |
| SRS-WEB-W11-010 | The Web Application shall export the audit trail. | derived from SRS-WEB-W11-009 |
| SRS-WEB-W11-011 | The Web Application shall not permit an export to be produced by a user who lacks read access to the session's channel. | §4.15; derived |
| SRS-WEB-W11-012 | The Web Application shall record `<TBD-W24>` (whether a formal test report to a named standard is required, and to which standard). No source states one. | Unsourced |

### 4.12 W12 — Calibration workflow

**Coverage:** ● Strong, and the area with the clearest numbering conflict between sources.

> **A numbering conflict to resolve before implementation.** `WAD/SRS` FR-006.1 says
> **20** calibration query types and FR-006.3 puts read-previous at **Q14**. `WAD/ICD` §3.8
> lists cancel at `0x0F` and read-previous at `0x14`. `CODE-P` `CalibrationQueryID_t`
> defines **23** commands, with a temperature triplet at `0x0F`–`0x11` and read-previous at
> `0x17`. `CODE-S` `calibCmdQueryID_t` agrees with `CODE-P`. The three-command offset is
> exactly the temperature triplet, so the Web App documents predate temperature
> calibration. **`ICD` §4.8 adopts the 23-command set**; this document follows it.
> Recorded as `<TBD-I15>` and conflict **C-25**.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W12-001 | The Web Application shall support the complete calibration command set of `ME-ICD-001` §4.8 — twenty-three commands. | `ICD` §4.8; `CODE-P` `CalibrationQueryID_t`; **supersedes** `WAD/SRS` FR-006.1 |
| SRS-WEB-W12-002 | The Web Application shall confirm a channel is ready to enter calibration before beginning. | `ICD` A-CAL-01 |
| SRS-WEB-W12-003 | ⚠ The Web Application shall not offer calibration on a channel with a running session. | `SRS-SEC-S2-012`; derived |
| SRS-WEB-W12-004 | The Web Application shall present live measured and raw converter values while calibrating. | `WAD/SRS` FR-003.3; `ICD` A-CAL-02, A-TLM-04 |
| SRS-WEB-W12-005 | The Web Application shall guide a two-point procedure — low reference, high reference, commit — for charge current, discharge current, charge voltage, discharge voltage and temperature. | `ICD` A-CAL-03 to A-CAL-17; `SRS-SEC-S14-015`, `-016` |
| SRS-WEB-W12-006 | The Web Application shall accept and transmit the applied reference value at each calibration point. | `ICD` A-CAL-03; **NEW — ME** as an explicit requirement |
| SRS-WEB-W12-007 | The Web Application shall accept and transmit the measurement range at each current calibration point. | `ICD` A-CAL-03 note; `SRS-SEC-S14-009` |
| SRS-WEB-W12-008 | The Web Application shall permit a calibration procedure to be cancelled, leaving stored calibration unchanged. | `ICD` A-CAL-18; `WAD/RTM` FR-006.4 |
| SRS-WEB-W12-009 | The Web Application shall permit calibration mode to be left explicitly. | `ICD` A-CAL-19 |
| SRS-WEB-W12-010 | The Web Application shall support a verification drive in charge and in discharge, and shall stop it. | `ICD` A-CAL-20, A-CAL-21, A-CAL-22 |
| SRS-WEB-W12-011 | The Web Application shall read the stored calibration parameters and their timestamps from a channel. | `ICD` A-CAL-23; `WAD/RTM` FR-006.5 |
| SRS-WEB-W12-012 | ⚠ The Web Application shall store every committed calibration in its own persistent store, with the value, the channel, the actor and the time. | `WAD/SRS` FR-006.2; `WAD/DBD` `CalibrationDataPoints`; `WAD/RTM` FR-006.6 |
| SRS-WEB-W12-013 | ⚠ The Web Application shall retain the previous calibration when a new one is committed, so that a calibration can be reverted and a past session's calibration recovered. | `WAD/SRS` FR-006.3; SRS-WEB-W10-005 |
| SRS-WEB-W12-014 | The Web Application shall present a channel's calibration history. | derived from SRS-WEB-W12-013 |
| SRS-WEB-W12-015 | The Web Application shall present and interpret every distinct calibration failure the channel reports. | `SRS-SEC-S14-024` (25 error values); `ICD` §4.8 |
| SRS-WEB-W12-016 | ⚠ The Web Application shall permit calibration only to a user holding the calibrate right for that channel. | `WAD/DBD` `UserCircuitAccess.CanCalibrate`; §4.15 |
| SRS-WEB-W12-017 | The Web Application shall record every calibration action in its audit trail. | §4.16 |
| SRS-WEB-W12-018 | The Web Application shall present the calibration due state of every channel, derived from its stored calibration dates. | HW *Configuration* #44–47; **NEW — ME** |
| SRS-WEB-W12-019 | The Web Application shall record `<TBD-W25>` (whether a calibration interval is mandated, and the required action when it lapses). | Unsourced |
| SRS-WEB-W12-020 | The Web Application shall record `<TBD-W26>` (whether the per-range current calibration scheme is retained) — see Open Issue **#15** and `<TBD-S47>`. | Open Issue **#15** |

### 4.13 W13 — Battery, battery type and limit standard management

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W13-001 | The Web Application shall create, list, update and delete battery type records. | `WAD/DBD` `BatteryTypes`; `WAD/SRS` FR-007.1 |
| SRS-WEB-W13-002 | The Web Application shall hold, per battery type, the chemistry, the nominal voltage and the nominal capacity. | `WAD/DBD` `BatteryTypes` |
| SRS-WEB-W13-003 | The Web Application shall create, list, update and delete battery records. | `WAD/SRS` FR-007.1; `WAD/RTM` FR-007.1, FR-007.2, FR-007.3 |
| SRS-WEB-W13-004 | The Web Application shall hold, per battery, a unique serial number, its type, and its manufacture date. | `WAD/DBD` `Batteries` |
| SRS-WEB-W13-005 | The Web Application shall hold, per battery, the full battery parameter record the boards require: nominal capacity, number of cells, gassing voltage, maximum voltage, nominal current, cold-cranking current, charge factor, internal resistance, cut-off voltage, nominal voltage and energy density. | BM §12.3; `SRS-PRI-P4-012`; **the baseline holds only four of the eleven** → **C-26** |
| SRS-WEB-W13-006 | The Web Application shall link a battery to every session run on it. | `WAD/SRS` FR-007.3; `WAD/DBD` `BatterySessions` |
| SRS-WEB-W13-007 | The Web Application shall present the test history of a battery across sessions and channels. | derived from SRS-WEB-W13-006; **NEW — ME** |
| SRS-WEB-W13-008 | The Web Application shall create, list, update and delete limit standard records. | `WAD/SRS` FR-007.2; `WAD/RTM` FR-005.12 |
| SRS-WEB-W13-009 | The Web Application shall hold, per limit standard, the maximum and minimum voltage, the maximum current, and the minimum and maximum temperature. | `WAD/DBD` `RegistrationStandards` |
| SRS-WEB-W13-010 | ⚠ The Web Application shall reject a limit standard whose bounds exceed the absolute ratings of the channel it is applied to. | `SRS-SEC-S9-016`, `-018`; **NEW — ME** |
| SRS-WEB-W13-011 | ⚠ The Web Application shall not present a limit standard as enforced by the hardware unless it has been transferred to the board. | **NEW — ME**. `WAD` holds limit standards in the server database only; nothing states that they reach the hardware → **W-39** |
| SRS-WEB-W13-012 | The Web Application shall record `<TBD-W27>` (whether a limit standard is enforced by this application, by the Primary Board, by the Secondary Board, or by more than one — and if more than one, which is authoritative) — see **W-39** and conflict **C-26**. | Unsourced |
| SRS-WEB-W13-013 | The Web Application shall record every battery, battery type and limit standard change in its audit trail. | §4.16 |
| SRS-WEB-W13-014 | The Web Application shall refuse to delete a battery, battery type or limit standard referenced by a session, and shall permit it to be deactivated instead. | `WAD/DBD` `IsActive` columns; derived |

### 4.14 W14 — Configuration and code-message management

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W14-001 | The Web Application shall hold the human-readable text of every hardware fault, message and user-error code. | `WAD/RTM` FR-010.1; `WAD/DBD` `CodeMessages` |
| SRS-WEB-W14-002 | The Web Application shall hold, per code, a severity and a category. | `WAD/DBD` `CodeMessages` |
| SRS-WEB-W14-003 | The Web Application shall permit an Administrator to create and edit code message text. | `WAD/RTM` FR-010.1; BM §12.4.8.6 (*Maintenance → BTS-600 Messages*, user-extensible) |
| SRS-WEB-W14-004 | ⚠ The Web Application shall use the same code space as the boards, and shall not re-map a code. | CN-13; `ICD` CS-33; conflict **C-14** |
| SRS-WEB-W14-005 | ⚠ The Web Application shall present an unknown code as an unknown code with its numeric value, and shall not present it as no fault. | **NEW — ME**; derived |
| SRS-WEB-W14-006 | The Web Application shall distinguish a fixed hardware fault code from an operator-editable message number, and shall not merge the two spaces. | conflict **C-14**; `ICD` CS-34 |
| SRS-WEB-W14-007 | The Web Application shall transfer the operator-editable message catalogue to a board where the board requires it. | `ICD` A-EVT-06 |
| SRS-WEB-W14-008 | The Web Application shall hold persistent application settings as versioned key-value entries. | `WAD/RTM` FR-010.2; `WAD/DBD` `ConfigurationEntity` |
| SRS-WEB-W14-009 | The Web Application shall present and permit editing of a board's configuration parameters. | `ICD` A-CFG-12, A-CFG-13; `SRS-PRI-P18-*` |
| SRS-WEB-W14-010 | The Web Application shall present and permit editing of a channel's factory configuration, including its absolute ratings and controller parameters. | `ICD` A-CFG-03, A-CFG-04; `SRS-SEC-S15-006` |
| SRS-WEB-W14-011 | ⚠ The Web Application shall require an explicit unlocking action before writing a channel's factory configuration or calibration. | `SRS-SEC-NF-032`; `ICD` A-CFG-04 |
| SRS-WEB-W14-012 | ⚠ The Web Application shall not offer to write a channel's factory configuration while that channel has a running session. | `SRS-SEC-S5-017`; derived |
| SRS-WEB-W14-013 | The Web Application shall present a board's and a channel's provisioning data — serial number, type number and versions. | `ICD` A-CFG-06; `SRS-SEC-S1-019` |
| SRS-WEB-W14-014 | The Web Application shall present which of a channel's configuration items are at default values rather than commissioned values. | `SRS-SEC-S15-018`; `ICD` B-CFG-11 |
| SRS-WEB-W14-015 | The Web Application shall permit restoring a board's or a channel's configuration to defaults, and shall require calibration to be included only by explicit request. | `ICD` A-CFG-14; `SRS-SEC-S15-016`, `-017` |
| SRS-WEB-W14-016 | The Web Application shall distribute wall-clock time to every board. | `WAD/RTM` FR-010.9; `ICD` A-CFG-11 |
| SRS-WEB-W14-017 | The Web Application shall present, per board, whether its time is synchronised. | `SRS-PRI-P17-*`; **NEW — ME** |
| SRS-WEB-W14-018 | The Web Application shall record `<TBD-W04>` (how the Web Application's own clock is synchronised, given that it is the time authority for every board). | A-09 |
| SRS-WEB-W14-019 | The Web Application shall record every configuration change, with its previous and new value, in its audit trail. | §4.16 |

### 4.15 W15 — User, role and access management

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W15-001 | The Web Application shall authenticate every user before granting any access. | `WAD/SRS` FR-008.1; `WAD/RTM` FR-008.1 |
| SRS-WEB-W15-002 | The Web Application shall store no password in recoverable form. | `WAD/SRS` NFR-004; `WAD/RTM` NFR-004 |
| SRS-WEB-W15-003 | The Web Application shall support the roles Administrator, Operator and Viewer. | `WAD/SRS` FR-008.2 |
| SRS-WEB-W15-004 | The Web Application shall enforce role-based access to every function. | `WAD/SRS` FR-008.3; `WAD/RTM` FR-008.3 |
| SRS-WEB-W15-005 | ⚠ The Web Application shall enforce per-user access rights per **channel**, distinguishing at least the rights to start, to stop and to calibrate. | `WAD/SRS` FR-008.3; `WAD/RTM` NFR-014; `WAD/DBD` `UserCircuitAccess` |
| SRS-WEB-W15-006 | ⚠ The Web Application shall enforce a channel access right on the server, and shall not rely on the absence of a control in the interface. | **NEW — ME**; derived from SRS-WEB-UI-011 |
| SRS-WEB-W15-007 | The Web Application shall present to a user only the channels to which that user has access. | `WAD/VVP` VAL-UI-003 |
| SRS-WEB-W15-008 | The Web Application shall support creating, deactivating and reactivating user accounts without deleting their audit history. | `WAD/DBD` `Users.IsActive`; §4.16 |
| SRS-WEB-W15-009 | The Web Application shall maintain the authenticated user's identity consistently across every part of the interface. | `WAD/RTM` FR-008.7 |
| SRS-WEB-W15-010 | The Web Application shall revalidate an authenticated session periodically, so that a change of rights or a deactivation takes effect without waiting for the session to expire. | `WAD/RTM` FR-008.6 |
| SRS-WEB-W15-011 | The Web Application shall expire an interactive session after a defined period of inactivity. | `WAD/RTM` NFR-006 (1 hour); `<TBD-W28>` |
| SRS-WEB-W15-012 | The Web Application shall issue a bearer token for programmatic access, with a defined lifetime and the holder's roles. (*A "bearer token" is a credential string: whoever holds it is treated as that user, which is why being able to withdraw one matters — see **W-40**.*) | `WAD/RTM` FR-008.2, NFR-005 (24 h) |
| SRS-WEB-W15-013 | ⚠ The Web Application shall apply the same role and channel access checks to programmatic access as to interactive access. | **NEW — ME**; §4.17 |
| SRS-WEB-W15-014 | ⚠ The Web Application shall be able to revoke an issued token before its expiry. | **NEW — ME**. A 24-hour non-revocable token is the only credential lifetime in `WAD` → **W-40** |
| SRS-WEB-W15-015 | The Web Application shall record every authentication, authentication failure, and access-right change in its audit trail. | `WAD/SRS` FR-008.4; §4.16 |
| SRS-WEB-W15-016 | The Web Application shall record `<TBD-W28>` (the required interactive session lifetime, token lifetime, and password policy) — the baseline states an hour and twenty-four hours respectively, and no password policy at all. | `WAD/RTM` NFR-005, NFR-006 |
| SRS-WEB-W15-017 | The Web Application shall record `<TBD-W29>` (whether the two additional user classes of §2.4 — test engineer and service engineer — require distinct roles) — see **W-35**. | §2.4 |

### 4.16 W16 — Audit and event logging

**Coverage:** ● Strong.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W16-001 | ⚠ The Web Application shall record every operation that changes persistent state in an audit trail. | `WAD/SRS` FR-008.4; `WAD/RTM` FR-008.5, NFR-015 |
| SRS-WEB-W16-002 | The Web Application shall record, per audit entry, the action, the entity type and identity, the previous value, the new value, the actor, the time and the origin address. | `WAD/DBD` `AuditLogs` |
| SRS-WEB-W16-003 | ⚠ The Web Application shall record an audit entry for a command issued to hardware, not only for a change to its own data. | **NEW — ME**. `WAD/DBD` `AuditLogs` is entity-oriented; a start command changes no entity |
| SRS-WEB-W16-004 | ⚠ The Web Application shall not permit an audit entry to be modified or deleted through any interface it exposes. | **NEW — ME**; derived |
| SRS-WEB-W16-005 | The Web Application shall record an audit entry for an action taken by a programmatic or tool client, identifying the client. | **NEW — ME**; §4.17, **W-36** |
| SRS-WEB-W16-006 | The Web Application shall retain audit entries for at least `<TBD-W30>`. | `<TBD-W30>`; Open Issue **#11** |
| SRS-WEB-W16-007 | The Web Application shall receive and retain board-level events reported over IF-A. | `ICD` A-EVT-04 |
| SRS-WEB-W16-008 | The Web Application shall retrieve events a board retained while its session was lost. | `ICD` A-EVT-05; `SRS-PRI-P22-*` |
| SRS-WEB-W16-009 | The Web Application shall record its own diagnostic log to durable storage. | `WAD/SRS` NFR-009; `WAD/RTM` NFR-009 |
| SRS-WEB-W16-010 | The Web Application shall present its recent diagnostic log in the interface. | `WAD/SRS` NFR-009; `WAD/RTM` FR-010.5 |
| SRS-WEB-W16-011 | The Web Application shall distinguish, in every log and audit view, an operator action from a scheduled action from a programmatic action. | **NEW — ME**; **W-37** |
| SRS-WEB-W16-012 | The Web Application shall record `<TBD-W30>` (the retention period for the audit trail and for diagnostic logs; the baseline retains diagnostic logs seven days and states nothing for the audit trail). | `WAD/SRS` NFR-009 |

### 4.17 W17 — System services and external integration

**Coverage:** ◐ Partial. Every item is sourced from `WAD/RTM` or `WAD/ADD`; **none is in
`WAD/SRS`** — conflict **C-22**.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W17-001 | The Web Application shall provide an internal publish-subscribe mechanism decoupling hardware-facing components from interface components. | `WAD/RTM` FR-010.4; `WAD/ADD` §3 |
| SRS-WEB-W17-002 | The Web Application shall persist per-user interface state server-side. | `WAD/RTM` FR-010.3, FR-010.6; `WAD/DBD` `ConfigurationEntity` |
| SRS-WEB-W17-003 | The Web Application shall provide a reusable tabular presentation supporting sorting, searching, grouping, column selection and export. | `WAD/RTM` FR-010.6; `WAD/ADD` §3 |
| SRS-WEB-W17-004 | The Web Application shall provide a transient notification mechanism and a modal confirmation mechanism. | `WAD/RTM` FR-010.7, FR-010.8 |
| SRS-WEB-W17-005 | The Web Application shall retain a bounded in-memory diagnostic log for interface presentation. | `WAD/RTM` FR-010.5 |
| SRS-WEB-W17-006 | The Web Application shall encode and decode every IF-A message in one component set, with its integrity value applied centrally. | `WAD/RTM` FR-010.11; `WAD/ADD` §2.2; SRS-WEB-SW-010 |
| SRS-WEB-W17-007 | The Web Application shall build the binary representation of a program for transfer in one component. | `WAD/RTM` FR-010.14 |
| SRS-WEB-W17-008 | The Web Application shall expose a REST interface covering at least board and channel status, program listing and assignment, session listing, and session data retrieval. | `WAD/RTM` FR-010.12; `WAD/ICD` §7.3 |
| SRS-WEB-W17-009 | The Web Application shall expose live channel data over a streaming HTTP interface. | `WAD/RTM` FR-003.7; `WAD/ICD` §7.5 |
| SRS-WEB-W17-010 | The Web Application shall return a consistent response envelope from every REST endpoint, distinguishing success from each defined failure. | `WAD/ICD` §7.4 |
| SRS-WEB-W17-011 | The Web Application shall expose a tool interface permitting an external tool client to query state and issue commands. | `WAD/RTM` FR-010.13; `WAD/ICD` §8 |
| SRS-WEB-W17-012 | ⚠ The Web Application shall authenticate every REST, streaming and tool client. | **NEW — ME**; **W-36** |
| SRS-WEB-W17-013 | ⚠ The Web Application shall apply the same role and per-channel access checks to a REST, streaming or tool client as to an interactive user. | SRS-WEB-W15-013; **NEW — ME** |
| SRS-WEB-W17-014 | ⚠ The Web Application shall record every command issued by a REST or tool client in its audit trail, identifying the client and its credential. | SRS-WEB-W16-005; **NEW — ME** |
| SRS-WEB-W17-015 | ⚠ The Web Application shall permit the tool interface to be disabled entirely by configuration. | **NEW — ME**; **W-36** |
| SRS-WEB-W17-016 | The Web Application shall record `<TBD-W06>` (whether the REST, streaming and tool interfaces are in ME scope, and for which consumers) — see conflict **C-22** and **W-36**. | Unsourced |

### 4.18 W18 — Firmware image management and distribution

**Coverage:** ◆ New to ME, and conditional. `WAD` has no firmware-update feature at all.
Conflict **C-06**, Open Issue **#9**.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W18-001 | The Web Application shall accept a firmware image for a board or for a Secondary Board, with its version and its target hardware variant. | `ICD` A-NOD-07, A-NOD-10; `SRS-PRI-P20-*` |
| SRS-WEB-W18-002 | The Web Application shall retain firmware images with their version, target variant, integrity value, upload actor and upload time. | **NEW — ME** |
| SRS-WEB-W18-003 | ⚠ The Web Application shall verify a firmware image's integrity on receipt, before offering it for distribution. | `SRS-SEC-S16-007`; **NEW — ME** |
| SRS-WEB-W18-004 | The Web Application shall transfer a firmware image to a board for onward distribution to its channels. | `ICD` A-NOD-07 |
| SRS-WEB-W18-005 | The Web Application shall instruct a board to apply a transferred image to nominated targets. | `ICD` A-NOD-08 |
| SRS-WEB-W18-006 | ⚠ The Web Application shall not offer to update a channel with a running session. | `SRS-SEC-S16-003`; `ICD` A-NOD-08 |
| SRS-WEB-W18-007 | The Web Application shall present the progress and per-target outcome of an update. | `ICD` A-NOD-09 |
| SRS-WEB-W18-008 | The Web Application shall present the version currently running on every board and channel, before and after an update. | SRS-WEB-W3-003; `ICD` A-NOD-02 |
| SRS-WEB-W18-009 | ⚠ The Web Application shall report a target whose version did not change after an update reported success. | **NEW — ME** |
| SRS-WEB-W18-010 | The Web Application shall record every firmware upload and every update attempt in its audit trail. | §4.16 |
| SRS-WEB-W18-011 | The Web Application shall record `<TBD-W31>` (whether firmware update through this application is in ME scope at all) — see conflict **C-06**, Open Issue **#9**, and `<TBD-S51>`. | Open Issue **#9** |

### 4.19 W19 — DBC CAN file management — excluded from ME scope

**Coverage:** withdrawn. Recorded so the exclusion is auditable rather than a silent
omission, exactly as `ME-ICD-001` §4.11 does for the corresponding messages.

External CAN and DBC are out of ME scope by the resolution of Open Issue **#29**: the COM
Controller board does not exist in ME, and the i.MX 8M Plus provides two FlexCAN instances
of which IF-B consumes one (conflict **C-16**).

| Withdrawn baseline requirement | Baseline ID | Note |
|---|---|---|
| Upload a DBC CAN file | `WAD/SRS` FR-009.1; `WAD/RTM` FR-009.1 | Withdrawn |
| Parse a DBC file into messages, signals, bit rate and port | `WAD/SRS` FR-009.2; `WAD/RTM` FR-009.2 | Withdrawn |
| Present DBC parse errors | `WAD/RTM` FR-009.3 | Withdrawn |
| Select signals for recording and display | `WAD/SRS` FR-009.3; `WAD/RTM` FR-009.4 | Withdrawn |
| Group and collapse the message and signal view | `WAD/SRS` FR-009.4 | Withdrawn |
| Save an edited DBC database | `WAD/RTM` FR-009.5 | Withdrawn |
| Delete a DBC file record | `WAD/RTM` FR-009.6 | Withdrawn |
| Upload a DBC file to hardware | `WAD/SRS` FR-005.4; `WAD/RTM` FR-005.6 | Withdrawn. The corresponding IF-A messages are excluded in `ICD` §4.11 |
| Decode DBC signals within logged data | `WAD/SRS` FR-004.5; `WAD/RTM` FR-004.6 | Withdrawn. Frees the logged-record opcode range `0x1F`–`0xFA` (`WAD/ICD` §6.3) |
| Extend a session store with one column per DBC signal | `WAD/DBD` §2.1 | Withdrawn. **The dynamic-column mechanism it justified can also be withdrawn** — see `<TBD-W32>` |
| Associate a DBC file with a program or a schedule | `WAD/DBD` `BtsPrograms.DbcFileId`, `ProgramSchedules.DbcFileId` | Withdrawn from use; the columns may remain for migration compatibility |
| Binary file storage service for DBC files | `WAD/RTM` FR-010.15 | Withdrawn. Its only consumer was DBC upload; a general file service may still be needed for TABLE files (SRS-WEB-W4-022) and firmware images (SRS-WEB-W18-002) |
| Verify DBC signal recording | `WAD/VVP` VAL-010; `WAD/RTM` VAL-010 | Withdrawn |

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-W19-001 | The Web Application shall not require any DBC CAN capability. | Open Issue **#29** resolved |
| SRS-WEB-W19-002 | The Web Application shall record `<TBD-W32>` (whether the runtime session-store schema extension mechanism is retained now that its only consumer is withdrawn, or whether a fixed session schema is adopted). | `WAD/DBD` §2.1 |
| SRS-WEB-W19-003 | The Web Application shall record `<TBD-W33>` (whether existing DBC data and its schema must be migrated or may be dropped, if the ME application evolves the baseline database) — see Open Issue **#2**. | Open Issue **#2** |

---

## 5. Non-Functional Requirements

### 5.1 Performance, capacity and latency

**The governing arithmetic.** `WAD/CAP` §4 states a practical ceiling of about **20
circuits** at the baseline rate of **10 ms per circuit** — approximately **2000 logged
records per second** through a single ingestion path. ME changes both sides of that:

| Case | Logged records/s per board (8 channels) | Boards within ~2000 records/s | Channels served |
|---|---|---|---|
| Registration at **100 ms** — feasible on classic CAN at 250 kbps (`ICD` §7.3) | 8 × 10 = **80** | ≈ **25** | ≈ 200 |
| Registration at **10 ms** — requires CAN-FD (`ICD` §7.3) | 8 × 100 = **800** | ≈ **2–3** | ≈ 16–24 |

**So the answer to "how many ME boards can one instance serve" is not a property of this
application alone — it follows from `ICD` `<TBD-I22>`, the registration-rate and CAN-FD
decision.** A tenfold change in that decision changes this application's capacity by
tenfold. This coupling is recorded because neither document could state it alone.

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-001 | The Web Application shall present a received live measurement within **500 ms** of receiving it. | `WAD/SRS` NFR-002; `WAD/RTM` NFR-002 |
| SRS-WEB-NF-002 | ⚠ The Web Application shall persist a received logged record without loss under burst. | `WAD/SRS` NFR-001; `WAD/RTM` NFR-001 |
| SRS-WEB-NF-003 | ⚠ The Web Application shall sustain the aggregate logged-data rate of `<TBD-W01>` boards at the registration rate resolved by `ICD` `<TBD-I22>`, without its write backlog growing without bound. | `WAD/CAP` §4, §9; **NEW — ME** |
| SRS-WEB-NF-004 | ⚠ The Web Application shall process logged data from different channels concurrently. | `WAD/CAP` §6 Risk 1; SRS-WEB-W9-007 |
| SRS-WEB-NF-005 | The Web Application shall bound every internal queue and shall define the behaviour on overflow. | `WAD/CAP` §6 Risk 2; SRS-WEB-CM-015 |
| SRS-WEB-NF-006 | The Web Application shall keep its write backlog per channel observable at run time. | `WAD/CAP` §9; SRS-WEB-W9-008 |
| SRS-WEB-NF-007 | The Web Application shall respond to an interactive request within `<TBD-W34>` at the full configured board count. | Unsourced |
| SRS-WEB-NF-008 | The Web Application shall complete a program transfer to a board within `<TBD-W18>`. | `ICD` `<TBD-I12>` |
| SRS-WEB-NF-009 | The Web Application shall not allow the number of connected browsers to affect the logged-data path. | **NEW — ME**; `WAD/CAP` §6 Risk 5 |
| SRS-WEB-NF-010 | The Web Application shall record `<TBD-W01>` (the maximum number of boards, and hence channels, one instance must serve) — see §2.3 and the table above. | Unsourced |
| SRS-WEB-NF-011 | The Web Application shall record `<TBD-W35>` (the maximum number of concurrent interactive users). | Unsourced |

### 5.2 Data integrity

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-012 | ⚠ The Web Application shall not silently lose a logged record; every loss shall be recorded and reportable. | `ICD` CS-59, CS-60; SRS-WEB-W9-011 |
| SRS-WEB-NF-013 | ⚠ The Web Application shall leave a session store readable and internally consistent after an abrupt termination of the application or its host. | `WAD/RTM` NFR-013; **NEW — ME** |
| SRS-WEB-NF-014 | ⚠ The Web Application shall not allow a change to a program, battery, limit standard or calibration record to alter the interpretation of an already-recorded session. | SRS-WEB-W10-001 to -007; **NEW — ME** |
| SRS-WEB-NF-015 | ⚠ The Web Application shall apply every persistent change atomically, such that a failure leaves no partial change. | **NEW — ME**; derived |
| SRS-WEB-NF-016 | ⚠ The Web Application shall provide a means of backing up its main store and its session stores without stopping a running session. | **NEW — ME**; Open Issue **#11** |
| SRS-WEB-NF-017 | The Web Application shall record `<TBD-W23>` (the recovery point objective for logged data). | `ICD` `<TBD-I28>` |

### 5.3 Availability and reliability

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-018 | The Web Application shall restart automatically after an unexpected termination. | `WAD/SRS` NFR-003; `WAD/RTM` NFR-003 |
| SRS-WEB-NF-019 | ⚠ The Web Application shall resume recording every running session after its own restart, without operator action. | SRS-WEB-W9-018; **NEW — ME** |
| SRS-WEB-NF-020 | ⚠ The Web Application's unavailability shall not stop a running test. | `SRS-PRI-P14-*`; `ICD` §8.1; **NEW — ME** as a requirement on this application's design |
| SRS-WEB-NF-021 | The Web Application shall operate continuously for the duration of a test lasting `<TBD-W36>`. | Unsourced; `SRS-SEC-NF-024` |
| SRS-WEB-NF-022 | The Web Application shall degrade to read-only operation rather than failing entirely when its main store is unavailable. | **NEW — ME** |
| SRS-WEB-NF-023 | The Web Application shall not depend on internet access for any operational function. | **W-33**; §2.3 (CDN dependency) |

### 5.4 Security

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-024 | ⚠ The Web Application shall authenticate every actor, human or programmatic, before granting access to any function or data. | `WAD/SRS` FR-008.1; SRS-WEB-W17-012 |
| SRS-WEB-NF-025 | ⚠ The Web Application shall enforce authorisation on the server for every request. | SRS-WEB-W15-006 |
| SRS-WEB-NF-026 | The Web Application shall store credentials only as a salted one-way derivation. | `WAD/SRS` NFR-004 |
| SRS-WEB-NF-027 | The Web Application shall encrypt at rest those stored fields designated sensitive. | `WAD/SRS` NFR-005 |
| SRS-WEB-NF-028 | The Web Application shall protect state-changing requests against cross-site request forgery. (*An attack where a page on another website silently makes a request using a logged-in operator's browser session — for example, stopping a test.*) | `WAD/ADD` §6 |
| SRS-WEB-NF-029 | The Web Application shall restrict which origins may call its programmatic interfaces. | `WAD/ADD` §6 (CORS) |
| SRS-WEB-NF-030 | ⚠ The Web Application shall not log a credential, a token or an encryption key. | CN-11; **NEW — ME** |
| SRS-WEB-NF-031 | ⚠ The Web Application shall be able to revoke a programmatic credential before its expiry. | SRS-WEB-W15-014; **W-40** |
| SRS-WEB-NF-032 | The Web Application shall serve its interface over an encrypted transport where the network is not trusted. | Open Issue **#10**; `<TBD-W37>` |
| SRS-WEB-NF-033 | The Web Application shall record `<TBD-W37>` (whether the plant network is treated as trusted, whether transport encryption is required for the browser interface and for IF-A, and whether an external standard such as IEC 62443 applies) — see Open Issue **#10**. | Open Issue **#10** |
| SRS-WEB-NF-034 | The Web Application shall record `<TBD-W38>` (whether the tool interface of SRS-WEB-W17-011 is permitted in production, and under what credential) — see **W-36**. | **W-36** |

### 5.5 Maintainability

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-035 | The Web Application shall be structured in separated presentation, application-service, business-logic and data layers. | `WAD/ADD` §2; CN-09 |
| SRS-WEB-NF-036 | No source file of the Web Application shall exceed **2000 lines**. | CN-08 |
| SRS-WEB-NF-037 | The Web Application shall manage every change to its persistent schema through versioned, ordered migrations. | `WAD/SRS` NFR-008; CN-10 |
| SRS-WEB-NF-038 | ⚠ The Web Application shall not require a destructive migration that discards recorded session data. | **NEW — ME** |
| SRS-WEB-NF-039 | The Web Application shall abstract data access behind interfaces, so that no presentation component issues a query directly. | `WAD/RTM` NFR-016; `WAD/ADD` §2.3 |
| SRS-WEB-NF-040 | The Web Application shall emit structured diagnostic logs. | `WAD/SRS` NFR-009 |
| SRS-WEB-NF-041 | The Web Application shall report its own version, build identity and schema version in the interface. | **NEW — ME**; SRS-WEB-W10-014 |
| SRS-WEB-NF-042 | The Web Application shall be built and released through an automated, repeatable pipeline. | `WAD/RTM` NFR-010, NFR-011, NFR-012 |

### 5.6 Portability and environment

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-043 | The Web Application shall run as a containerised deployment. | `WAD/SRS` NFR-007; `WAD/RTM` NFR-007 |
| SRS-WEB-NF-044 | The Web Application shall support the current and immediately preceding major versions of Chrome, Edge and Firefox. | `WAD/SRS` NFR-010 |
| SRS-WEB-NF-045 | The Web Application shall serve every asset its interface requires from its own deployment, without reference to an external network. | **W-33**; SRS-WEB-NF-023 |
| SRS-WEB-NF-046 | The Web Application shall store all mutable data outside its deployment image. | `WAD/ADD` §5 (volume) |
| SRS-WEB-NF-047 | The Web Application shall record `<TBD-W02>` (whether the baseline technology stack is retained for ME) — see Open Issue **#2** and CN-14. | Open Issue **#2** |
| SRS-WEB-NF-048 | The Web Application shall record `<TBD-W39>` (the required host resources at the resolved board count; `WAD/CAP` §8 gives figures only up to 20 circuits). | `WAD/CAP` §8 |

### 5.7 Verification constraints

| ID | Requirement | Source |
|---|---|---|
| SRS-WEB-NF-049 | Every requirement marked **⚠** shall be verified by a test that demonstrates the failure mode is detected or prevented, not by inspection of the code that implements it. | Team engineering policy |
| SRS-WEB-NF-050 | Every requirement concerning logged-data completeness shall be verified by injecting loss and confirming it is detected and reported. | SRS-WEB-W9-011; `ICD` §8.3 |
| SRS-WEB-NF-051 | Every requirement concerning multi-channel behaviour shall be verified with at least eight channels active, not with one. | **NEW — ME**; `WAD/VVP` tests are all single-circuit |
| SRS-WEB-NF-052 | Every requirement concerning capacity shall be verified at the resolved board count of `<TBD-W01>`, and the write backlog shall be recorded during the test. | `WAD/CAP` §9; SRS-WEB-NF-003 |
| SRS-WEB-NF-053 | Every access-control requirement shall be verified by attempting the denied operation through every interface, including the programmatic and tool interfaces. | SRS-WEB-W17-013; **NEW — ME** |
| SRS-WEB-NF-054 | Every requirement carrying a `<TBD-Wnn>` marker shall be treated as unverifiable until the marker is resolved. | derived |
| SRS-WEB-NF-055 | The verification test set shall be renumbered to remove the duplicate and conflicting identifiers of `WAD/VVP` and `WAD/RTM` before it is used — see conflict **C-27**. | conflict **C-27** |

---
## 6. Traceability Matrix

See the companion annex:
`04A_SRS_ME_Web_Application_Annex_Traceability_v0.1.md` §A1.

Every requirement in this document additionally carries its source inline in the `Source`
column of its table, so the annex provides the reverse mapping — from source document and
section to requirement — together with the coverage roll-up per area and the register of
requirements withdrawn from the baseline.

> **Relationship to `WAD/RTM`.** `WAD/RTM` traces the *baseline* requirements to the
> *existing* implementation. It is not superseded and it is not extended. The annex to this
> document traces *ME* requirements to *sources*, which is a different mapping: it answers
> "on what authority does this requirement exist", not "which class implements it". A
> requirement-to-implementation matrix for ME can only be written once the ME
> implementation exists.

---

## 7. Core Allocation — not applicable

This section is retained, empty, so that section numbering aligns with
`01_SRS_ME_Primary_Board_v0.1.md` and `02_SRS_ME_Secondary_Board_v0.1.md`.

The Primary SRS carries a core-allocation section because the i.MX 8M Plus is a
heterogeneous multicore device and the split of function between the Cortex-A53 cluster and
the Cortex-M7 is reserved decision **D-01**. That decision does not reach a server
application. No requirement in this document carries an allocation attribute.

One reserved decision does bear on this document: **D-05**, the IF-A transport, port,
framing and serialization details. It is recorded in §8.4 and its worksheet is
`ME-ICD-001` §9.4.

---

## 8. Open Issues / TBD Register

### 8.1 Open issues

The register is shared with `GATE` §4, `ME-SRS-PRI-001` §8.1 and `ME-SRS-SEC-001` §8.1.
Issues **#1**, **#4**, **#5**, **#27** and **#29** were resolved by the architect on
2026-07-27. The third column states the bearing on **this** document only.

| # | Issue | Bearing on this document |
|---|---|---|
| ~~1~~ | ~~Is ME the Digatron ME circuit type?~~ **CLOSED** — ME is this project's name; BM is a functional reference. | §1.3. Removes any obligation to reproduce Battery Manager's screens, formats or byte layouts. |
| **2** | **Which Web Application is the peer — the existing Ador server evolved, Digatron BM4, or a new build?** | **The highest-leverage issue for this document, and it governs the whole of it.** See reading note 1. Bears on A-01, CN-14, `<TBD-W02>`, `<TBD-W32>`, `<TBD-W33>`, and the evidentiary weight of every `WAD`-sourced requirement. |
| **3** | No ME Primary Board hardware specification exists. | Indirect. Bears on `<TBD-W05>` (storage sizing) only through the logged-data rate. |
| ~~4~~ | ~~Is the COM Controller retained?~~ **CLOSED** — no COM Controller in ME. | §2.1 change 3, §4.19. The reason DBC is withdrawn. |
| ~~5~~ | ~~Which operators are in Phase 1?~~ **CLOSED** — all BTS-600 operators are in scope. | **Direct and large.** SRS-WEB-W4-003: the program editor must handle the complete operator catalog of `ME-SRS-PRI-001` §4.7, which is far richer than the baseline editor. |
| **6** | Are battery-parameter-relative values resolved by the Primary at run time or pre-computed? | **Direct.** `<TBD-W17>`, SRS-WEB-W4-028. If pre-computed, this application must implement the resolution. |
| **7** | Procedures or PRODUCER inline expansion as the reuse model? | **Direct.** `<TBD-W16>`, §4.4, §4.5. PRODUCER is a `WAD` invention with a documented hazard (SRS-WEB-W4-021); `BM` Procedures are the sourced alternative. |
| **8** | Emergency-stop input source and safe state. | Indirect. This application presents the resulting fault; it does not implement the interlock. |
| **9** | Confirm firmware update over CAN is required. | **Direct.** All of §4.18 is conditional. `<TBD-W31>`. |
| **10** | Primary-side security posture; is the plant network trusted? | **Direct.** `<TBD-W37>`, §5.4, A-07. Bears on transport encryption for both the browser interface and IF-A. |
| **11** | Data retention and storage-exhaustion policy. | **Direct and large.** `<TBD-W05>`, `<TBD-W22>`, `<TBD-W30>`; §4.9, §4.16, §5.2. This application owns the storage. |
| **12** | Buffering during link loss; startup self-test; orderly shutdown. | **Direct.** SRS-WEB-CM-011, §4.9. The reciprocal of the Primary's buffering requirement. |
| **13** | Maintenance mode. | Indirect. If it exists, this application must present it and refuse commands invalid in it. |
| **14** | Secondary presence policy: hot-plug, behaviour on loss. | **Direct.** §4.3, `<TBD-W14>`. Determines whether the channel inventory is dynamic during a session. |
| **15** | Program assignment semantics; per-range calibration retention. | **Direct.** `<TBD-W21>`, `<TBD-W26>`; §4.7, §4.12. |
| **16** | Synchronised versus independent multi-channel execution. | **Direct.** `<TBD-W20>`, SRS-WEB-W7-018, -019. Note SYNCProgram is program-**version**-sensitive, which is why SRS-WEB-W4-006 requires a version identity. |
| **17** | Fault latch, acknowledge and clear semantics; event log. | **Direct.** SRS-WEB-W7-008, §4.16. |
| **18** | Telemetry rate budget across 8 nodes. | **Direct and quantified.** §5.1 shows this application's capacity is a function of the answer. Coupled to `ICD` `<TBD-I22>`. |
| **19** | Program-controlled digital output. | SRS-WEB-W7-009. |
| **20** | Health-check channels 1 to 5. | Indirect. This application would present them; their meaning is unknown. |
| **21** | The hardware specification's ten unresolved hardware queries. | **No direct bearing.** |
| **22** | Secondary node identity source; NVM policy; restore to defaults. | SRS-WEB-W14-015; `<TBD-W12>` is the analogous question for *board* identity. |
| **23** | Power-fail: hold-up, persistence, automatic versus confirmed resume. | **Direct.** If resume requires confirmation, this application presents it — no requirement can be written until #23 is answered. |
| **24** | Cross-channel system-level supervision. | Indirect; A-08. |
| **25** | Programs concurrently resident; versioning and rollback. | **Direct.** SRS-WEB-W4-006, -007; SRS-WEB-W5-013. |
| **26** | MISRA-C applicability. | **No bearing.** Not a C codebase. |
| ~~27~~ | ~~Missing operator PDF.~~ **PARTIALLY CLOSED** — all eight operators in scope, semantics unavailable. | **Direct.** The program editor cannot offer `PAUA`, `PAUO`, `URANGE`, `IRANGE`, `OUTA`, `OUTB`, `ISOEXT` or `ISOINT` with correct parameter validation until their semantics are supplied. Bears on SRS-WEB-W4-003, -010. |
| **28** | Does the Primary need analog input of its own? | **No bearing.** |
| ~~29~~ | ~~External CAN / Modbus scope.~~ **CLOSED** — external CAN and DBC excluded; Modbus is Primary-hosted. | **Direct and large.** All of §4.19 — an entire baseline feature area withdrawn. Also confirms this application is not a Modbus peer (§1.2). |
| **30** | TSN or IEEE 1588 on the Primary. | Indirect. Bears on `<TBD-W04>` if this application must be a precise time source. |
| **31** | Must ME reproduce the BM registration formats? | SRS-WEB-W4-025. Weakened by the closure of #1. |
| ~~S-32~~ | Raised by `ME-SRS-SEC-001`: the Secondary's step-engine role is removed and nothing replaces its local behaviour. | Indirect. Bears on what step-level detail this application can display in real time (SRS-WEB-W8-003). |

**Issues raised first by this document.**

| # | Issue | Bearing |
|---|---|---|
| **W-33** | **The interface depends on externally hosted assets.** `WAD/ADD` §4 loads Highcharts from a CDN. A plant server is not assumed to have internet access (§2.5). If the CDN is unreachable, charts fail — and the failure appears as a broken interface, not as a missing dependency. Are there other external asset dependencies? | SRS-WEB-NF-023, NF-045; §2.3 |
| **W-34** | **Main-store engine at ME scale.** `WAD/DBD` §1 puts every entity — users, boards, channels, programs, schedules, batteries, calibration, audit — in one single-file store, and `WAD/CAP` §4 sets a practical ceiling of about 20 circuits. ME's per-instance channel count (§5.1) may reach 200. Does the main store engine and its single-writer model hold at that scale, particularly for the audit trail, which grows fastest? | `<TBD-W03>`; SRS-WEB-W9-024; §2.3 |
| **W-35** | **Two user classes have no role.** §2.4 identifies a test engineer (authors programs, defines batteries and standards) and a service engineer (calibration, factory configuration, firmware). `WAD` has three roles and both these people must be Administrators — which means the calibration and factory-configuration rights cannot be separated from user management. Are distinct roles required? | `<TBD-W29>`; SRS-WEB-W15-017 |
| **W-36** | **The tool interface is an unspecified privileged actor.** `WAD/ADD` §2.1 and `WAD/ICD` §8 ship an MCP tool server exposing device control to an AI or LLM client. `WAD/SRS` does not mention it. In ME it could start or stop tests on eight channels. What authenticates it, what may it do, is it audited, and is it permitted in production at all? | `<TBD-W06>`, `<TBD-W38>`; SRS-WEB-W17-011 to -015; §2.4 |
| **W-37** | **Whose authority does a scheduled start carry?** A schedule created by one user fires later, possibly after that user's rights have been withdrawn or their account deactivated. `WAD` records a creator but states no authorisation check at execution. Is the check made at creation, at execution, or both? | SRS-WEB-W6-013, W16-011 |
| **W-38** | **A schedule missed across a restart has no defined behaviour.** `WAD` describes a job fired at a stated time; nothing states what happens if the application was not running then. Fire late, skip and record, or skip silently? Silently is the current implicit behaviour and is the worst of the three. | SRS-WEB-W6-016 |
| **W-39** | **Limit standards may be enforced nowhere.** `WAD/DBD` holds a limit standard per session in the server database, and `WAD` never transfers it to hardware. `ME-SRS-SEC-001` §4.9 has the Secondary enforcing its own configured absolute ratings, which are a different set of numbers. So a limit standard chosen by an operator may bound nothing at all. Who enforces it, and which set is authoritative? | `<TBD-W27>`; SRS-WEB-W13-011, -012; conflict **C-26** |
| **W-40** | **A programmatic credential cannot be withdrawn.** `WAD/RTM` NFR-005 issues a 24-hour bearer token with roles as claims and no revocation mechanism. Withdrawing a user's access therefore takes up to 24 hours to take effect on the programmatic interface, while taking effect immediately on the interactive one (`WAD/RTM` FR-008.6 revalidates). Is that acceptable, and if not, what revocation is required? | SRS-WEB-W15-014, NF-031 |

### 8.2 Conflict register

Conflicts are carried, not resolved. `GATE` §5 holds C-01…C-18 and
`ME-SRS-SEC-001` §8.2 adds C-19 and C-20. The table below lists those that bear on this
document, then the seven this document adds.

| ID | Conflict | Bearing on this document |
|---|---|---|
| **C-05** | Registration interval: five candidate values, narrowed to two by `ME-ICD-001` §7.3 | **Direct and quantified.** §5.1 — this application's board capacity varies tenfold between the two survivors. |
| **C-06** | Secondary bootloader required / not required / required over CAN | §4.18 is conditional on it. `<TBD-W31>` |
| **C-07**, **C-08** | Battery voltage and current capability versus configured default | SRS-WEB-W13-010 — a limit standard must be validated against *something*, and which is unresolved. |
| **C-13** | Cycle nesting: 16 interlaced cycles versus depth 4 versus a single active loop | SRS-WEB-W4-012, -013 — program validation cannot be written without the answer. |
| **C-14** | Four incompatible fault and message code spaces | **Direct.** CN-13, SRS-WEB-W14-004, -006. This application is where the codes become words an operator reads, so an unresolved mapping surfaces here as wrong text. |
| **C-16** | External CAN port count | Resolved into scope exclusion; §4.19. |
| **C-19** | Open-battery detection has no error code | SRS-WEB-W14-005 — an unknown code must not display as "no fault". |

**Conflicts raised first by this document.**

| ID | Conflict | Sources | Disposition |
|---|---|---|---|
| **C-21** | **"Registration" means three different things across the document set.** (a) *A recorded measurement sample* — `BM` §12.4.1, `ME-SRS-PRI-001` §1.4, `ME-SRS-SEC-001` §1.4, and the whole of registration-format and registration-interval discussion. (b) *The session handshake by which hardware announces itself* — `WAD/SRS` §1.3 defines "Registration" as exactly this, and `WAD/ICD` §3.4 names it the Registration Interface. (c) *A named set of voltage, current and temperature limits* — `WAD/DBD` `RegistrationStandards`, `WAD/RTM` FR-005.12. | `BM`; `PRI`; `SEC`; `WAD/SRS` §1.3; `WAD/ICD` §3.4; `WAD/DBD` | **Not resolvable by precedence — it is a vocabulary collision, and the most dangerous kind, because all three readings are grammatical in the same sentence.** §1.4 of this document adopts (a) as the ME meaning, always qualifies (b) as *session registration*, and renames (c) to *limit standard*. The `WAD` names are recorded so the mapping is traceable. **Requires a naming decision before implementation**, because it will otherwise appear in code, in the database schema and on screen. |
| **C-22** | **`WAD/SRS` and `WAD/RTM` disagree about what the requirements are.** Four same-ID-different-meaning collisions: **FR-010** is *Reporting* in `WAD/SRS` §3.10 and *System Services* in `WAD/RTM` §3; **FR-011** is *Session Self-Containment* in `WAD/SRS` §3.11 and *Program Scheduler* in `WAD/RTM`; **NFR-005** is *column encryption* in `WAD/SRS` and *24-hour token expiry* in `WAD/RTM`; **NFR-006** is *scalability to N devices* in `WAD/SRS` and *1-hour session expiry* in `WAD/RTM`. Beyond the collisions, `WAD/RTM` specifies whole areas `WAD/SRS` omits — the **Program Scheduler** (10 requirements, with database tables and migration *AddProgramScheduler* to match), REST, SSE, MCP, the event bus, and 15 system services — while `WAD/SRS` specifies PRODUCER (FR-005.6–.11) and Session Self-Containment (FR-011) which `WAD/RTM` does not trace at all. | `WAD/SRS` v0.6; `WAD/RTM` v0.5 | **Both documents are marked *Approved*, so there is no basis in status for preferring either.** This document takes the union: every requirement in either source is carried, renumbered into the `W1`…`W19` areas so no collision survives. §4.6 flags the scheduler as reconstructed from `WAD/RTM`, `WAD/DBD` and `WAD/VVP`. **The version numbers suggest the cause** — `WAD/SRS` is v0.6 and `WAD/RTM` v0.5, so the RTM is a revision behind on some content and ahead on others, which means neither is a superset. Requires reconciliation at source. |
| **C-23** | **Logged data is unacknowledged, so its loss is undetectable.** `WAD/ICD` §6 sends logged measurement rows over UDP with no acknowledgement; a dropped datagram silently gaps the permanent record. Raised in `ME-ICD-001` §8.3 as the only failure mode in the system with no detector. | `WAD/ICD` §6; `ICD` §8.3 | Carried into SRS-WEB-W9-010 to -013 and SRS-WEB-NF-012. `<TBD-W23>` asks for the recovery point objective. **This is the highest-consequence unresolved item in this document**, because it concerns the record of tests that may run for days. |
| **C-24** | **"No data loss" is claimed, contradicted, and unsupported at scale.** `WAD/SRS` NFR-001 requires that UDP ingestion handle bursts *"without data loss (`Channel<T>` buffer)"*. `WAD/RTM` NFR-001 restates it as a *"bounded queue"*. `WAD/CAP` §6 Risk 2 states plainly that both the global and the per-circuit channels are **unbounded**, and Risk 1 names the **single** store-processor task as the primary bottleneck, with queues growing without bound past about 40 circuits. So the SRS claims safety, the RTM claims a mechanism that is not the one implemented, and the capacity analysis says the real mechanism trades data loss for unbounded memory growth. | `WAD/SRS` NFR-001; `WAD/RTM` NFR-001; `WAD/CAP` §6 | Carried into SRS-WEB-CM-015, W9-006, W9-007, NF-004, NF-005. An unbounded queue does not prevent loss; it defers it into a memory exhaustion. Requires a decision on the overflow policy, which `<TBD-W22>` and `<TBD-W23>` frame. |
| **C-25** | **Calibration command numbering differs across three sources.** `WAD/SRS` FR-006.1 says **20** query types and FR-006.3 puts read-previous at **Q14**. `WAD/ICD` §3.8 puts cancel at `0x0F` and read-previous at `0x14`. `CODE-P` `CalibrationQueryID_t` and `CODE-S` `calibCmdQueryID_t` both define **23** commands, with a temperature triplet at `0x0F`–`0x11` and read-previous at `0x17`. The offset is exactly three — the temperature triplet — so the Web App documents predate temperature calibration. | `WAD/SRS`; `WAD/ICD` §3.8; `CODE-P`; `CODE-S` | `ME-ICD-001` §4.8 adopts the 23-command set and this document follows it (SRS-WEB-W12-001). Also recorded as `ICD` `<TBD-I15>`. Low risk, but it will silently mis-address three commands if the older numbering is implemented. |
| **C-26** | **The battery and limit data model does not match what the boards require.** `WAD/DBD` holds four battery attributes (`BatteryTypes.NominalVoltage`, `.Capacity`, plus serial and manufacture date); `BM` §12.3 and `SRS-PRI-P4-012` require **eleven** battery parameters to reach the boards. Separately, `WAD/DBD` `RegistrationStandards` holds five limit values in the server database that nothing transfers to hardware, while `ME-SRS-SEC-001` §4.9 has the Secondary enforcing a different set of configured absolute ratings. | `WAD/DBD`; `BM` §12.3; `PRI`; `SEC` | Carried into SRS-WEB-W13-005 (the eleven parameters) and SRS-WEB-W13-010 to -012 (limit authority). Escalated as Open Issue **W-39**, because a limit an operator selects may bound nothing. |
| **C-27** | **Verification test identifiers are duplicated and inconsistent.** `WAD/RTM` §6 assigns VAL-UI-006 to *Group expand/collapse* and VAL-UI-007 to *Excel export*; `WAD/VVP` §4 assigns VAL-UI-006 to *Excel export* and VAL-UI-007 to *Group expand/collapse* — the two are **swapped**. Separately, VAL-UI-008 is *DBC signal checkbox* in `WAD/VVP` §4 and *Scheduler* in both the `WAD/VVP` amendment and `WAD/RTM` — the **same identifier for two different tests**. | `WAD/RTM` §6; `WAD/VVP` §4 and its amendment | Carried into SRS-WEB-NF-055. Not a functional defect, but a traceability one: a test result recorded against VAL-UI-008 cannot be attributed. Requires renumbering before the set is used for ME. |

**Two further document defects, recorded but not raised as conflicts** because they affect
neither behaviour nor traceability:

| Observation | Source |
|---|---|
| `WAD/DBD` contains the section **"## 3. Migration History" twice**, the second listing one migration more than the first (*AddProgramScheduler*). The `ProgramSchedules` and `ScheduleExecutionLogs` table definitions appear between them, orphaned under the first heading. | `WAD/DBD` |
| `WAD/SRS` §1.4 references `PROTOCOL.md` and `README.md`, neither of which is in the supplied `WebAppDocs` set. `WAD/INDEX` lists `RELEASE_NOTES.md`, also not supplied. | `WAD/SRS` §1.4; `WAD/INDEX` |

### 8.3 TBD register

Thirty-nine values or policies could not be sourced. Every requirement carrying one is
unverifiable until it is answered (SRS-WEB-NF-054). Note the namespace warning in §1.7:
`<TBD-W33>` and open issue **W-33** are unrelated.

| Tag | Value required | Raised by | Descends from |
|---|---|---|---|
| `<TBD-W01>` | Maximum number of boards, and hence channels, one instance must serve | §2.3 · §5.1 · SRS-WEB-W2-018 · NF-003 · NF-010 · NF-052 | `WAD/CAP` §4 vs ME channel count; coupled to `ICD` `<TBD-I22>` |
| `<TBD-W02>` | Whether the baseline technology stack is retained for ME | CN-14 · §2.3 · SRS-WEB-NF-047 | Open Issue **#2** |
| `<TBD-W03>` | Whether the single-file main store and one-store-per-session model hold at the ME channel count | §2.3 · SRS-WEB-W9-024 | **W-34** |
| `<TBD-W04>` | How this application's own clock is synchronised, given it is the time authority for every board | A-09 · §2.7 · SRS-WEB-W14-018 | Open Issue **#30** |
| `<TBD-W05>` | Required storage capacity | SRS-WEB-HW-004 | Open Issue **#11** |
| `<TBD-W06>` | Whether the REST, streaming and tool interfaces are in ME scope, and for which consumers | SRS-WEB-SW-008 · W17-016 | **C-22** · **W-36** |
| `<TBD-W07>` | Time within which a board's session loss must be detected | SRS-WEB-CM-007 | `ICD` `<TBD-I11>` |
| `<TBD-W08>` | Staleness threshold beyond which displayed telemetry is marked invalid | SRS-WEB-CM-014 | `ICD` `<TBD-I04>` |
| `<TBD-W09>` | Whether a language other than English is required | SRS-WEB-UI-012 | Unsourced |
| `<TBD-W10>` | Time within which a network configuration change must be confirmed | SRS-WEB-W1-009 | `ICD` `<TBD-I09>` |
| `<TBD-W11>` | Whether discovery must operate across network segments | SRS-WEB-W1-013 | Unsourced |
| `<TBD-W12>` | How a board is identified for authorisation — hardware identifier, network address, or hardware address | SRS-WEB-W2-017 | `WAD/ICD` §3.4 carries all three |
| `<TBD-W13>` | Whether per-channel-position history is retained when a Secondary Board is replaced | SRS-WEB-W3-014 | Unsourced |
| `<TBD-W14>` | Whether a session may start when fewer than the configured channels are present | SRS-WEB-W3-015 | Open Issue **#14** |
| `<TBD-W15>` | Maximum permitted PRODUCER nesting depth | SRS-WEB-W4-020 | Unsourced |
| `<TBD-W16>` | Whether PRODUCER or the `BM` Procedure model is adopted | SRS-WEB-W4-027 | Open Issue **#7** |
| `<TBD-W17>` | Whether battery-parameter-relative values are resolved at authoring time or at run time | SRS-WEB-W4-028 | Open Issue **#6** |
| `<TBD-W18>` | Overall timeout for a segmented transfer | SRS-WEB-W5-012 · NF-008 | `ICD` `<TBD-I12>` |
| `<TBD-W19>` | Whether recurring schedules are required, or only single-shot | SRS-WEB-W6-018 | `WAD/DBD`, `WAD/RTM` describe one execution time |
| `<TBD-W20>` | Whether synchronised multi-channel execution is required, and the permitted start skew | SRS-WEB-W7-019 | Open Issue **#16** |
| `<TBD-W21>` | Whether one program instance may span several channels as one logical test | SRS-WEB-W7-021 | Open Issue **#15** |
| `<TBD-W22>` | Session-data retention period, and behaviour when storage is exhausted | SRS-WEB-W9-021 · W9-022 | Open Issue **#11** · **C-24** |
| `<TBD-W23>` | Recovery point objective for logged data | SRS-WEB-W9-023 · NF-017 | **C-23** · `ICD` `<TBD-I28>` |
| `<TBD-W24>` | Whether a formal test report to a named standard is required | SRS-WEB-W11-012 | Unsourced |
| `<TBD-W25>` | Whether a calibration interval is mandated, and the action when it lapses | SRS-WEB-W12-019 | Unsourced |
| `<TBD-W26>` | Whether the per-range current calibration scheme is retained | SRS-WEB-W12-020 | Open Issue **#15** · `<TBD-S47>` |
| `<TBD-W27>` | Who enforces a limit standard, and which set of bounds is authoritative | SRS-WEB-W13-012 | **W-39** · **C-26** |
| `<TBD-W28>` | Interactive session lifetime, token lifetime, and password policy | SRS-WEB-W15-011 · W15-016 | `WAD/RTM` NFR-005, NFR-006; no password policy stated |
| `<TBD-W29>` | Whether test engineer and service engineer require distinct roles | SRS-WEB-W15-017 | **W-35** |
| `<TBD-W30>` | Retention period for the audit trail and for diagnostic logs | SRS-WEB-W16-006 · W16-012 | `WAD/SRS` NFR-009 states 7 days for logs, nothing for audit |
| `<TBD-W31>` | Whether firmware update through this application is in ME scope | SRS-WEB-W18-011 | **C-06** · Open Issue **#9** |
| `<TBD-W32>` | Whether the runtime session-store schema extension mechanism is retained now its only consumer is withdrawn | §4.19 · SRS-WEB-W19-002 | Open Issue **#29** resolved |
| `<TBD-W33>` | Whether existing DBC data and schema must be migrated or may be dropped | SRS-WEB-W19-003 | Open Issue **#2** |
| `<TBD-W34>` | Interactive response time required at the full board count | SRS-WEB-NF-007 | Unsourced |
| `<TBD-W35>` | Maximum number of concurrent interactive users | SRS-WEB-NF-011 | Unsourced |
| `<TBD-W36>` | Maximum continuous test duration this application must span | SRS-WEB-NF-021 | Unsourced; cf. `<TBD-S64>` |
| `<TBD-W37>` | Whether the plant network is trusted; transport encryption for the browser interface and for IF-A; applicable standard | SRS-WEB-NF-032 · NF-033 | Open Issue **#10** |
| `<TBD-W38>` | Whether the tool interface is permitted in production, and under what credential | SRS-WEB-NF-034 | **W-36** |
| `<TBD-W39>` | Required host resources at the resolved board count | SRS-WEB-NF-048 | `WAD/CAP` §8 stops at 20 circuits |

### 8.4 Reserved architect decisions

| ID | Decision | Status in this document |
|---|---|---|
| **D-01** | Cortex-A53 / Cortex-M7 allocation on the Primary | **Does not bear on this document.** §7. |
| **D-02** | Which Primary core owns the CAN controller | **Does not bear on this document.** |
| **D-03** | Content of the minimum necessary control data | **No direct bearing.** Indirectly bounds the step-level detail this application can display in real time — see Open Issue **S-32** and SRS-WEB-W8-003. |
| **D-04** | CAN layer details, including CAN-FD and bit rate | **Bears indirectly but strongly.** §5.1 shows this application's board capacity follows from the registration rate, which follows from D-04. `ICD` `<TBD-I22>`. |
| **D-05** | IF-A transport assignment, ports, framing, serialization | **Direct.** Every requirement in §3.3, §4.1, §4.2, §4.5, §4.8 and §4.9 is written transport-agnostically because of it. The worksheet is `ME-ICD-001` §9.4. |
| **D-06** | Inter-core communication on the Primary | **Does not bear on this document.** |

---

## 9. Deviations

### 9.1 From the developer's SRS

Recorded so the Web Application team can see exactly what changed and why. **Nothing in
`WAD/SRS` was discarded without a reason stated here or in §4.19.**

| # | Deviation | Reason |
|---|---|---|
| 1 | **`WAD/SRS` is treated as a *source*, not as a document to reformat.** Its eleven functional groups are redistributed across nineteen ME areas, and its requirements are restated in the house convention with a `Source` column. | The request was for this SRS "in the same manner as Primary and Secondary". Those documents are source-traced and carry conflict, TBD and open-issue registers. A reformat could not have produced any of that. |
| 2 | **Requirements from `WAD/RTM`, `WAD/ADD`, `WAD/DBD`, `WAD/CAP` and `WAD/VVP` are carried, not only those from `WAD/SRS`.** | `WAD/RTM` specifies a whole feature — the Program Scheduler — that `WAD/SRS` omits, plus REST, SSE, MCP and 15 system services. Conflict **C-22**. Writing only from `WAD/SRS` would have silently dropped them. |
| 3 | **Requirement identifiers are renumbered** into `SRS-WEB-<area>-<nnn>`; the baseline `FR-` and `NFR-` identifiers appear in the `Source` column. | The baseline identifiers collide between `WAD/SRS` and `WAD/RTM` (**C-22**), so they cannot serve as primary keys. Every one is preserved as a traceable source reference. |
| 4 | **Three terms are renamed.** *Device* → **board**; *Circuit* → **channel**; `RegistrationStandard` → **limit standard**. "Registration" is reserved for its measurement meaning and the handshake is always *session registration*. | Conflict **C-21** — three meanings of "registration" across the document set, and *device*/*circuit* no longer describe the ME topology. §1.4 gives the mapping. |
| 5 | **DBC CAN management is withdrawn entirely** — 12 baseline requirements and a database mechanism. | Open Issue **#29** resolved: external CAN and DBC are out of ME scope. §4.19 lists every withdrawn item so the exclusion is auditable. |
| 6 | **The per-circuit connection and registration model is replaced by a per-board model.** | `SRS-PRI-SW-001`: one Primary Board presents one logical interface for eight channels. `WAD/SRS` FR-002.2, FR-002.6 and `WAD/RTM` FR-002.6 cannot hold in ME. §4.2, §4.3. |
| 7 | **Two areas are added with no baseline precedent:** §4.3 channel inventory and presence, §4.18 firmware distribution. | ME's channel population is dynamic and reported by the board; firmware distribution is required by the brief. Both are `NEW — ME` throughout. |
| 8 | **`WAD/SRS` NFR-001's "no data loss" claim is not carried as stated.** | Conflict **C-24**: the mechanism cited does not provide the property claimed. SRS-WEB-NF-002 keeps the requirement; SRS-WEB-CM-015, W9-006 and W9-007 specify what would actually deliver it. |
| 9 | **Verification requirements are added (§5.7) that the baseline does not have**, including that multi-channel behaviour be verified with eight channels active. | Every test in `WAD/VVP` is single-circuit. A specification for eight channels cannot be validated by single-channel tests. |

### 9.2 From the gate document

| # | Deviation | Reason |
|---|---|---|
| 1 | **`GATE` has no outline for a Web Application SRS.** `GATE` §1, §2 and §3 scope three deliverables — Primary SRS, Secondary SRS, ICD. This document is a fourth, requested afterwards. | Its structure follows `ME-SRS-SEC-001` — the closer precedent, since neither document has a core-allocation section. Coverage ratings use the same key. |
| 2 | **Areas are coded `W1`…`W19`**, not against a `GATE` checklist. | No checklist exists for this item. The areas are derived from `WAD/SRS`'s own functional grouping, extended where ME requires it, so the mapping back to the baseline stays legible. |
| 3 | **The shared registers are extended, not duplicated:** conflicts continue at **C-21**, open issues at **W-33**. | One system-wide conflict register and one open-issue register across four documents is the only way a reviewer can see the whole picture. `ME-SRS-SEC-001` set this precedent with C-19, C-20 and S-32. |
| 4 | **A fourth `<TBD-…>` namespace is introduced.** | `<TBD-nn>`, `<TBD-Snn>` and `<TBD-Inn>` are taken. §1.7 warns about the `<TBD-W33>` / **W-33** visual collision. |
| 5 | **§7 is retained and empty.** | Section numbers align across all three SRS documents, so a reviewer reading them side by side finds the same content under the same number. |

---

*End of ME-SRS-WEB-001 v0.1. Requirement count: 405. §7 is intentionally empty;
§6 is held in the companion annex. 39 unsourced values, 8 new open issues, 7 new conflicts.*
