# Interface Control Document — ME System Interfaces

**Document ID:** ME-ICD-001 · **Version:** 0.1 (draft) · **Date:** 2026-07-28
**Companion documents:** `01_SRS_ME_Primary_Board_v0.1.md` (ME-SRS-PRI-001) ·
`01A_SRS_ME_Primary_Annex_Traceability_and_Core_Allocation_v0.1.md` ·
`02_SRS_ME_Secondary_Board_v0.1.md` (ME-SRS-SEC-001) ·
`02A_SRS_ME_Secondary_Annex_Traceability_v0.1.md` ·
`00_ME_SRS_Source_Coverage_and_Open_Issues_v0.1.md` (gate document)

> **What this document is, and is not.**
>
> It is a **message inventory with semantics**: for every message that crosses an ME
> interface, what it is for, which way it goes, what triggers it, what information it
> carries, how long the sender waits, and what happens when it fails.
>
> It is **not a wire format**. No byte layout, no field offset, no CAN identifier, no
> port number and no serialization scheme is specified. Those are reserved decisions
> **D-03**, **D-04** and **D-05**. The columns that would hold them are present in every
> table and are uniformly marked pending, so that the architect can fill them in place
> without restructuring this document.
>
> Legacy byte layouts *are* quoted in the `Source` column, because they are evidence of
> what information moved and are the natural starting point for D-04 and D-05. They are
> **not** requirements.

---

## 1. Purpose, scope and precedence

### 1.1 Purpose

This document defines the interfaces between the four actors of the ME battery test
system, so that the Web Application team, the Primary Board team and the Secondary Board
team can develop against a single agreed information contract while the encoding
decisions remain open.

### 1.2 Scope

**In scope:** the complete logical message set of IF-A (Web Application ↔ Primary Board),
IF-B (Primary Board ↔ Secondary Board), and IF-D (Primary Board ↔ Modbus client); the
semantics common to all of them; the aggregate timing and rate budget; and the per-message
failure behaviour.

**Out of scope:** encoding, framing, identifier allocation, transport-port assignment and
bit rate (**D-03**, **D-04**, **D-05**); the internal behaviour of any actor (the two SRS
documents); the legacy external CAN and DBC interface, excluded from ME scope by the
resolution of Open Issue **#29**; and the Web Application's REST, SignalR and MCP
interfaces, which are internal to the Web Application and are documented in `WAD/ICD` §7,
§8 and §10.

### 1.3 Precedence

Within this document:

1. **`ME-SRS-PRI-001` and `ME-SRS-SEC-001`** define required *behaviour*. Where a message
   in this document would imply behaviour that contradicts either SRS, the SRS wins and
   the message definition is in error.
2. **HW > BM > LSRS** applies to the information content of a message, as in both SRS
   documents.
3. **`WAD/ICD`** is the strongest evidence for IF-A, because IF-A's peer is an existing
   implemented application. It is evidence, not a contract — see Open Issue **#2**.
4. **`CODE-P` and `CODE-S`** are evidence of what information previously moved. They are
   never a requirement, and for IF-B they describe a topology (1:1 over two UARTs) that
   ME does not have.

### 1.4 How to read the tables

Every message table in §4, §5 and §6 has the same eleven columns.

| Column | Meaning |
|---|---|
| **Msg** | Logical message identifier, local to this document. Stable across revisions. Not a wire identifier. |
| **Name** | Short name for use in code and in conversation. |
| **Purpose** | Why the message exists. |
| **Dir** | Direction. `W→P` Web App to Primary · `P→W` · `P→S` Primary to Secondary · `S→P` · `↔` both ways · `C→P` Modbus client to Primary. |
| **Trigger / Rate** | What causes the message: `cyclic` with an interval, `on event`, `on request`, or `on command`. |
| **Information content** | The information carried, named and never laid out. |
| **Timeout** | How long the sender waits before treating the exchange as failed. |
| **Error behaviour** | What the sender does when the timeout expires or a negative acknowledgement arrives. |
| **Encoding** | **Reserved.** `⟨D-05⟩` for IF-A and IF-D, `⟨D-04⟩` for IF-B. |
| **Frame/ID** | **Reserved.** Same convention. |
| **Source** | Evidence for the message's existence and content. |

The two reserved columns are uniform by design; they are the fill-in-place sheet for
D-04 and D-05. §9.4 gives the same set as a compact worksheet for that purpose.

**`<TBD-Inn>`** marks a value this document cannot supply. The `I` namespace keeps it
distinct from the Primary SRS's `<TBD-nn>` and the Secondary SRS's `<TBD-Snn>`. All are
registered in §9.3.

**`CS-nn`** identifies a common-semantics rule in §3, or a rule stated in §6, §7 or §8
that applies across messages rather than to one.

---

## 2. Interface identification

### 2.1 The four interfaces

| ID | Between | Medium | Multiplicity | Status |
|---|---|---|---|---|
| **IF-A** | Web Application ↔ Primary Board | Ethernet; TCP and UDP, assignment reserved | 1 : 1 | **In scope.** §4 |
| **IF-B** | Primary Board ↔ Secondary Board | Isolated CAN or CAN-FD, single shared bus, 250 kbps default (*"isolated" = no direct electrical connection, so a fault on one board cannot travel down the cable; "shared bus" = one pair of wires carries all nine nodes*) | 1 : *n*, *n* ≤ 8 | **In scope.** §5 |
| **IF-C** | Primary Board ↔ external CAN devices, with DBC decode | — | — | **Excluded from ME scope** by the resolution of Open Issue **#29**. §2.4 |
| **IF-D** | Primary Board ↔ Modbus client | Serial RS-485, or TCP | 1 : *m* | **In scope.** §6 |

**Note on the identifier `IF-D`.** `GATE` §3 proposed `IF-C` for "external CAN / RS485 *if
in scope*". The architect's resolution of **#29** split that proposal in two: external CAN
and DBC are out of scope, while Modbus is in scope and Primary-hosted. `ME-SRS-PRI-001`
§2.2 therefore names the Modbus interface **IF-D** and leaves `IF-C` unused. This document
follows the Primary SRS. `IF-C` is retained as a reserved, unused identifier so that
reopening #29 does not force a renumbering.

### 2.2 IF-A — Web Application ↔ Primary Board

One Primary Board presents exactly one logical interface to the Web Application
(`SRS-PRI-SW-001`). All program, configuration, control, calibration, telemetry,
logged-data and event traffic crosses it. The Primary Board addresses its eight channels
*within* this interface; the Web Application does not open a connection per channel.

**Transport.** TCP and UDP are both used; which carries what is **D-05**. The legacy
system's assignment — TCP 9999 for command and response, UDP 10000 for live telemetry,
UDP 10001 for the session store, UDP 10002/10003 for discovery — is recorded as evidence
in every affected row and is **not** adopted.

**The change from BTS.** In BTS, one device carried one or two circuits and registered
itself per circuit (`WAD/ICD` §3.4 carries both a `DeviceID` and a `CircuitID`). In ME,
one Primary Board carries up to eight, and the enumeration is dynamic (`SRS-PRI-P3-002`).
Every IF-A message that names a channel must therefore be able to name any of eight, and
the Web Application must be able to learn which of the eight exist — which is the reason
for message group **A10**.

### 2.3 IF-B — Primary Board ↔ Secondary Board

One shared CAN bus carries the Primary Board and up to eight Secondary Boards. Each
Secondary Board presents one logical interface (`SRS-SEC-SW-001`) and processes only
messages addressed to its own node identity (`SRS-SEC-CM-005`).

**Three structural changes from BTS**, each of which shapes §5:

| # | BTS | ME | Consequence for this document |
|---|---|---|---|
| 1 | Point-to-point UART, one Secondary | Shared CAN bus, up to eight | Every message needs a node address. Addressing is **D-04**. |
| 2 | **Two** UARTs: `huart4` for commands, `huart5` dedicated to registration data | **One** CAN bus for everything | Registration now contends with commands and with seven peers. This is the whole of §6 and Open Issue **#18**. |
| 3 | The Secondary held the program and ran the step engine | The Primary sequences; the Secondary regulates | The legacy step-transfer messages are **not carried forward** as such; what replaces them is **D-03**. §5.2 states this precisely. |

**Prior art recorded, not adopted.** `PSPE` §4.4 proposes a per-step control frame
carrying a command token, a mode, two setpoints, four safety limits and *K* cut-off
conditions, each with a quantity, a comparison operator, a threshold and a condition
identifier — with the Secondary detecting and the Primary deciding. It is the most
developed proposal for **D-03** in any source and is recorded here as evidence. **It is
not adopted**, and no message in §5 presupposes it.

### 2.4 IF-C — reserved, out of scope

The legacy COM Controller board (`CODE-C`) provided three FDCAN ports with a full DBC
configuration model, and `LSRS` carries approximately 100 requirements predicated on
them. The board does not exist in ME, and the i.MX 8M Plus provides two FlexCAN instances
of which IF-B consumes one (conflict **C-16**).

The architect's resolution of **#29** excludes external CAN and DBC from ME scope. Two
IF-A messages that exist in the legacy protocol are consequently **excluded** rather than
specified — `en_canDbcStepsQuery` (`0x07`) and `en_canDbcDataQuery` (`0x08`) of
`ProgramQueryID_t`, and the DBC record opcodes `0x1F`–`0xFA` of the legacy session store.
They are listed in §4.11 so that the exclusion is explicit and auditable rather than a
silent omission.

### 2.5 IF-D — Primary Board ↔ Modbus client

The Modbus module is in ME scope and is hosted by the Primary Board
(`SRS-PRI-HW-003`, `ME-SRS-PRI-001` §4.25). The Primary Board is a Modbus **slave**. §6
gives its message set, which is the Modbus function-code set rather than an ME-specific
protocol, and the register map it exposes.

### 2.6 What does not exist

Recorded so that absence is not mistaken for omission.

| Non-interface | Why |
|---|---|
| Secondary Board ↔ Web Application | The Secondary has no network interface (`SRS-SEC-SW-003`). All Secondary data reaches the Web Application through the Primary. |
| Secondary Board ↔ Secondary Board | Secondary Boards do not communicate with each other (`SRS-SEC-SW-004`). Any cross-channel coordination is performed by the Primary. |
| Secondary Board ↔ Modbus client | IF-D terminates on the Primary Board. |
| A local operator interface on either board | Neither board has one (`SRS-PRI-UI-001`, `SRS-SEC-UI-001`). |

---

## 3. Common semantics

These rules apply to every message in §4, §5 and §6 unless a row states otherwise. They
are stated once here rather than repeated per message.

### 3.1 Addressing model

| ID | Rule | Source |
|---|---|---|
| CS-01 | Every IF-B message shall carry the node identity of the Secondary Board it concerns. | `SRS-SEC-CM-005`; **D-04** |
| CS-02 | A Secondary Board shall act only on IF-B messages carrying its own node identity, and shall not respond to any other. | `SRS-SEC-CM-005` |
| CS-03 | Every IF-A message that concerns one channel shall carry the channel identity it concerns. | `SRS-PRI-P3-011`; `WAD/ICD` §3.2 (`DeviceID` + `CircuitID`) |
| CS-04 | Channel identity on IF-A shall be independent of node identity on IF-B, so that a Secondary Board can be replaced without changing the channel the Web Application addresses. | NEW — ME |
| CS-05 | An IF-B broadcast addressed to all nodes shall be permitted only for messages listed as such in §5. | **D-04** |
| CS-06 | `<TBD-I01>` — whether IF-B supports a broadcast address at all, and which messages may use it. | **D-04** |

### 3.2 Request, response, acknowledgement

| ID | Rule | Source |
|---|---|---|
| CS-07 | Every message that requires a response shall receive either a positive acknowledgement or a negative acknowledgement carrying a reason. | `SRS-PRI-CM-002`, `SRS-SEC-CM-002` |
| CS-08 | A negative acknowledgement shall distinguish an unrecognised message from a recognised message with invalid parameters, and from a recognised message that is invalid in the recipient's current state. | `SRS-SEC-CM-003`, `-004`; `SRS-SEC-S2-014` |
| CS-09 | A recipient shall not act on a message it negatively acknowledges. | `SRS-SEC-S4-007` |
| CS-10 | A response shall be correlatable to the request that caused it. | NEW — ME |
| CS-11 | `<TBD-I02>` — the correlation mechanism: a sequence number, an echoed request identifier, or strict request-response ordering per node. | **D-04**, **D-05** |

### 3.3 Integrity

| ID | Rule | Source |
|---|---|---|
| CS-12 | Every message on every interface shall carry an integrity check covering its whole content. | `SRS-PRI-CM-001`, `SRS-SEC-CM-001` |
| CS-13 | A recipient shall discard a message failing its integrity check without acting on its content. | `SRS-PRI-CM-001`, `SRS-SEC-CM-001` |
| CS-14 | A recipient shall count discarded messages and shall make the count available as a diagnostic. | `SRS-SEC-S18-*`; NEW — ME |
| CS-15 | An integrity failure shall not by itself be treated as loss of communication; the communication timeout governs that. | derived from CS-13, CS-19 |
| CS-16 | `<TBD-I03>` — the integrity mechanism per interface. The legacy system used CRC-16/Modbus on both interfaces; CAN provides its own frame CRC, so IF-B may need only an application-layer check on multi-frame messages. | **D-04**, **D-05** |

### 3.4 Timeout, retry and staleness

| ID | Rule | Source |
|---|---|---|
| CS-17 | Every request-response exchange shall have a defined timeout, given per message in §4, §5 and §6. | derived |
| CS-18 | A Secondary Board shall re-request control data that has not arrived within **100 ms**, for **5** attempts, and shall then enter the safe state and report. | `SRS-SEC-S4-024`, `-025` |
| CS-19 | A Secondary Board shall declare loss of Primary communication when no valid message addressed to it has arrived within the configured timeout, default **100 ms**. | `SRS-SEC-CM-007`; HW *Configuration* #9 |
| CS-20 | The Primary Board shall declare a Secondary Board absent on the same criterion and the same default. | `SRS-PRI-CM-006`, `SRS-PRI-P3-008` |
| CS-21 | Every message carrying measurement or event data shall carry a timestamp or an elapsed-time reference, so that a recipient can determine its age. | `SRS-PRI-CM-008`, `SRS-SEC-S12-015` |
| CS-22 | A recipient shall be able to detect that data it has received is stale, and shall not present stale data as current. (*"Stale" means old enough that it may no longer be true — a frozen reading must not look like a live one.*) | NEW — ME |
| CS-23 | A retry shall be idempotent: receiving the same request twice shall have the same effect as receiving it once. (*So if a sender does not hear a reply and asks again, the recipient does not do the job twice.*) | NEW — ME |
| CS-24 | `<TBD-I04>` — the staleness threshold beyond which telemetry must be marked invalid rather than merely old. | Unsourced |

### 3.5 Ordering and segmentation

| ID | Rule | Source |
|---|---|---|
| CS-25 | A message too large for one transport unit shall be segmented, and the recipient shall reassemble it in the correct order. | `SRS-PRI-P4-004`, `SRS-SEC-S16-005` |
| CS-26 | A recipient shall detect a missing, duplicated or out-of-order segment and shall reject the whole message. | `SRS-PRI-P4-005`, `SRS-SEC-S16-006` |
| CS-27 | A recipient shall not act on a partially received message. | `SRS-SEC-S16-012` |
| CS-28 | A segmented transfer shall have a defined overall timeout, independent of the per-segment timeout. | NEW — ME |
| CS-29 | `<TBD-I05>` — the segmentation and flow-control mechanism on IF-B (*segmentation = splitting a message too big for one CAN frame into several; flow control = the receiver telling the sender to slow down*). Relevant because a registration record exceeds one classic CAN frame by roughly eightfold (§7.2). | **D-04** |

### 3.6 Error and event signalling

| ID | Rule | Source |
|---|---|---|
| CS-30 | A fault shall be reported by the detecting party at the first opportunity, not deferred to the next cyclic report. | `SRS-SEC-S13-004` |
| CS-31 | A fault report shall carry the complete active fault set, not only the newest fault. | `SRS-SEC-S13-003` |
| CS-32 | A fault, an informational message and a user error shall be distinguishable in the reporting message. | `SRS-SEC-S13-005`, `-006` |
| CS-33 | The fault code space shall be identical on IF-A and IF-B, so that no translation occurs on the Primary Board. | derived; conflict **C-14** |
| CS-34 | `<TBD-I06>` — the unified fault code space. Four incompatible spaces exist; see conflict **C-14** and `ME-SRS-SEC-001` §8.2 including the new conflicts **C-19** and **C-20**. | **C-14** |

### 3.7 State and lifecycle

| ID | Rule | Source |
|---|---|---|
| CS-35 | A party shall reject a message that is invalid in its current state, with a reason, rather than silently ignoring it. | `SRS-PRI-P2-011`, `SRS-SEC-S2-014` |
| CS-36 | A readiness query shall exist for every multi-message transfer, and the transfer shall not begin until readiness is confirmed. | `CODE-P` `en_isReadyQuery`; `CODE-S` `*_Q_ID_IS_READY` |
| CS-37 | Loss and restoration of an interface shall be observable events on that interface, not inferred from silence alone. | `SRS-SEC-S10-009` |
| CS-38 | Restoration of an interface shall not by itself resume any suspended activity; an explicit command is required. | `SRS-SEC-S10-006`, `SRS-SEC-S19-009` |

### 3.8 Units and representation

| ID | Rule | Source |
|---|---|---|
| CS-39 | Every quantity shall have exactly one unit across every interface: current in amperes, voltage in volts, power in watts, capacity in ampere-hours, energy in watt-hours, temperature in degrees Celsius, time in milliseconds. | `WAD/ICD` §5.2; `CODE-S`; HW *Coding Guidelines* #7 |
| CS-40 | A quantity shall not be scaled differently on IF-A and IF-B. | derived from CS-39 |
| CS-41 | A measured current shall be reported independently of the measurement range in use. | `SRS-SEC-S7-028` |
| CS-42 | A sign convention shall be defined once and applied on both interfaces: `<TBD-I07>` — whether discharge current is negative or whether direction is carried separately. The legacy real-time packet carries an unsigned current with a separate circuit status; the legacy shunt measurement is bipolar. | `WAD/ICD` §5.2; `LSRS` SW_REQ_141 |
| CS-43 | `<TBD-I08>` — whether quantities are exchanged as floating point or as scaled integers. The legacy system used IEEE-754 single precision throughout; a CAN interface may prefer scaled integers. | **D-04**, **D-05** |

---
## 4. IF-A message set — Web Application ↔ Primary Board

Ten message groups, plus a register of messages deliberately excluded. `⟨D-05⟩` marks the
two reserved columns throughout.

### 4.1 A-DSC — Discovery and network configuration

Pre-session: the Web Application finds Primary Boards on the network and configures their
addressing. Legacy used UDP broadcast on 10002 with unicast replies on 10003; the
transport is **D-05**.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-DSC-01 | discover-request | Ask every Primary Board on the network to identify itself | W→P | on request, operator-initiated | none beyond the request itself | `<TBD-I09>` | retry; boards that do not reply are simply not discovered | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §4.2 Q1 |
| A-DSC-02 | discover-reply | Report the board's identity and full network configuration | P→W | on A-DSC-01 | unique board identifier; board IP; subnet mask; gateway; two DNS addresses; configured server address; configured command port; configured live-data port; configured logged-data port | n/a | none; the reply is best-effort | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §4.2 (`BroadcastDeviceInfo`) |
| A-DSC-03 | set-board-network-config | Set the board's own IP configuration | W→P | on command | unique board identifier; board IP; subnet mask; gateway; two DNS addresses | `<TBD-I09>` | negative acknowledgement with reason; configuration unchanged | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §4.2 Q4 (`BroadcastIpConfig`) |
| A-DSC-04 | set-server-config | Tell the board which server to contact and on which ports | W→P | on command | unique board identifier; server address; command port; live-data port; logged-data port | `<TBD-I09>` | negative acknowledgement with reason; configuration unchanged | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §4.2 Q5 (`BroadcastServerConfig`) |
| A-DSC-05 | network-config-applied | Confirm a network configuration change has taken effect, and on which address | P→W | on A-DSC-03 or A-DSC-04 | unique board identifier; the configuration now in force; whether a restart was required | n/a | none | ⟨D-05⟩ | ⟨D-05⟩ | NEW — ME (the legacy protocol has no confirmation, so a failed reconfiguration is silent) |

### 4.2 A-REG — Session registration

The Primary Board announces itself to the Web Application and establishes the command
session. Legacy: TCP 9999, start byte `0xDD`.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-REG-01 | register | Announce the board and open the command session | P→W | on link establishment, and on reconnection | board identifier; board name; board IP; board MAC address; number of channel positions (8); application and bootloader version | `<TBD-I10>` | retry with backoff; the board remains unregistered and accepts no commands | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §3.4; `CODE-P` `DEVICE_REG_DATA_PACKET 0xDD` |
| A-REG-02 | register-response | Accept, reject or note an already-registered board | W→P | on A-REG-01 | echoed board identifier; outcome, distinguishing success, already-registered and failure; assigned session identifier | n/a | on failure the board retries A-REG-01 | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §3.4 (status `0x01`/`0x02`/`0x00`) |
| A-REG-03 | deregister | Close the command session cleanly | ↔ | on orderly shutdown, either side | session identifier; reason | `<TBD-I10>` | the peer falls back to timeout detection | ⟨D-05⟩ | ⟨D-05⟩ | `WAD` device endpoints (delete-device) |
| A-REG-04 | keep-alive | Confirm the command session is live in the absence of other traffic | ↔ | cyclic, `<TBD-I11>` | session identifier | `<TBD-I11>` | the session is declared lost; see A-EVT-04 | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `WEBAPP_RECONNECT_TIMEOUT_MS` 300 000; NEW — ME |

> **Note on `WEBAPP_RECONNECT_TIMEOUT_MS = 300 000`.** The legacy Primary waited **five
> minutes** before giving up on the Web Application. That is a reconnection budget, not a
> liveness interval, and it is recorded here as evidence for `<TBD-I11>` rather than as a
> keep-alive period. A five-minute liveness interval would leave a dead link undetected
> for five minutes while eight channels ran.

### 4.3 A-CFG — Configuration

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-CFG-01 | config-is-ready | Confirm the board can accept a configuration operation | W→P | before any A-CFG exchange | none | `<TBD-I10>` | the transfer is not started | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_isReadyforConfigQuery 0x01`; CS-36 |
| A-CFG-02 | read-factory-config | Read the board's factory configuration | W→P | on request | channel selector, or board-level | n/a (request) | negative acknowledgement with reason | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_readFactoryDataQuery 0x02` |
| A-CFG-03 | factory-config | Carry the factory configuration | ↔ | on A-CFG-02 or A-CFG-04 | per channel: type number; absolute maximum and minimum voltage; absolute maximum charge and discharge current; maximum power; bank voltage limits; temperature limits; rectifier maximum; the twelve regulation controller parameter sets | `<TBD-I10>` | negative acknowledgement; stored configuration unchanged | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-S` `BTS_FactoryData_t` (109 B); HW *Configuration* |
| A-CFG-04 | write-factory-config | Write the factory configuration | W→P | on command, service-engineer action | as A-CFG-03, plus an explicit unlocking indication | `<TBD-I10>` | negative acknowledgement; stored configuration unchanged | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-NF-032`; `CODE-P` `FACTORY_DATA_PACKET 0x02`. **The legacy `ConfigQueryID_t` has no write-factory query** — this is a gap in the legacy protocol, not in ME |
| A-CFG-05 | read-manufacturing-data | Read provisioning data | W→P | on request | channel selector, or board-level | n/a | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_readManufacturingDataQuery 0x03` |
| A-CFG-06 | manufacturing-data | Carry provisioning data | ↔ | on A-CFG-05 or A-CFG-07 | serial number; type number; hardware version; bootloader version; application version; manufacturing date; the four calibration dates | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | HW *Configuration* #1–4, #44–47 |
| A-CFG-07 | write-manufacturing-data | Write provisioning data | W→P | on command, factory action | as A-CFG-06, plus an explicit unlocking indication | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `MANUFACTURING_DATA_PACKET 0x03` |
| A-CFG-08 | read-battery-params | Read the battery parameter record | W→P | on request | channel selector | n/a | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_readBatteryInfoQuery 0x04` |
| A-CFG-09 | battery-params | Carry the battery parameter record | ↔ | on A-CFG-08 or A-CFG-10 | nominal capacity; number of cells; gassing voltage; maximum voltage; nominal current; cold-cranking current; charge factor; internal resistance; cut-off voltage; nominal voltage; energy density | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.3 (`INTERN[17]`…`INTERN[27]`); `CODE-S` `BTS_BatteryData_t` (43 B) |
| A-CFG-10 | write-battery-params | Write the battery parameter record | W→P | on command | as A-CFG-09; channel selector | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_writeBatteryInfoQuery 0x05` |
| A-CFG-11 | sync-time | Set the board's wall-clock time | W→P | on command, and periodically | absolute time | `<TBD-I10>` | negative acknowledgement; the board keeps its previous time and reports it as unsynchronised | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_syncTimeQuery 0x06`; `LSRS` SW_REQ_21 |
| A-CFG-12 | read-config-item | Read one configuration parameter by identity | W→P | on request | parameter identity; channel selector | n/a | negative acknowledgement for an unknown parameter | ⟨D-05⟩ | ⟨D-05⟩ | HW *Configuration* (parameter column gives per-item identifiers) |
| A-CFG-13 | write-config-item | Write one configuration parameter by identity | W→P | on command | parameter identity; value; channel selector | `<TBD-I10>` | negative acknowledgement with reason, distinguishing unknown parameter from out-of-range value | ⟨D-05⟩ | ⟨D-05⟩ | HW *Configuration*; `SRS-PRI-P18-*` |
| A-CFG-14 | restore-defaults | Restore configuration to defined default values | W→P | on command | scope: board or channel; whether calibration is included, which requires explicit request | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S15-016`, `-017`; Open Issue **#22** |

### 4.4 A-PRG — Program transfer

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-PRG-01 | program-is-ready | Confirm the board can accept a program | W→P | before a program transfer | none | `<TBD-I10>` | the transfer is not started | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_isReadyQuery 0x01`; `SRS-PRI-P4-002` |
| A-PRG-02 | program-metadata | Declare what is about to be transferred | W→P | before program content | program identity; program version; program name; total step count; associated registration format identity; associated battery parameter identity | `<TBD-I10>` | negative acknowledgement; no partial program is retained | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_programMetadataQuery 0x02`; `SRS-PRI-P4-003` |
| A-PRG-03 | program-step-count | Declare the number of steps to follow | W→P | after A-PRG-02 | step count | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_programStepsQuery 0x03`; `WAD/ICD` §3.6 `0x03` |
| A-PRG-04 | program-data | Carry the program content | W→P | segmented, after A-PRG-03 | ordered step records: label, operator, nominal value or values, limit set, action, registration selection; segment index; total segments | `<TBD-I12>` overall | reject the whole transfer; report which segment failed and why | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_programDataQuery 0x04`, 1 MB program buffer; BM §12.4 step record |
| A-PRG-05 | program-transfer-result | Report the outcome of a program transfer | P→W | on completion or rejection of A-PRG-04 | program identity; outcome; on rejection, the reason, distinguishing integrity failure, step-count mismatch, capacity exceeded, decode failure and unknown operator | n/a | none | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P4-008`, `-007`, `-016` |
| A-PRG-06 | read-program-metadata | List the programs resident on the board | W→P | on request | none, or a program identity filter | n/a | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_readProgramMetadataQuery 0x05`; `SRS-PRI-P4-014` |
| A-PRG-07 | read-saved-program | Return the content of a resident program | P→W | on request | program identity; the step records as in A-PRG-04 | `<TBD-I12>` | negative acknowledgement for an unknown program | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_readSavedProgramQuery 0x06`; `SRS-PRI-P4-015` |
| A-PRG-08 | delete-program | Remove a resident program | W→P | on command | program identity | `<TBD-I10>` | negative acknowledgement; refused if the program is executing | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P4-009`; Open Issue **#25** |
| A-PRG-09 | registration-format-definition | Define the set of quantities to record and the rules for recording them | W→P | with or before a program | format identity; selected quantities; recording interval or triggering rule per quantity | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.4.1 and BM §12.4.6; `SRS-PRI-P4-013` |

### 4.5 A-CTL — Control

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-CTL-01 | start | Start a program on one or more channels | W→P | on operator command | channel selector, one or many; program identity; session name; start-from-step, optional; deferred-start time, optional | `<TBD-I10>` | negative acknowledgement per channel with reason; channels that can start are not blocked by one that cannot | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_startProgram 1`; BM §2.4; `SRS-PRI-P19-*` |
| A-CTL-02 | stop | Terminate the program on one or more channels | W→P | on operator command | channel selector | `<TBD-I10>` | negative acknowledgement with reason | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_stopProgram 2`; BM §12.4.5 `STO` |
| A-CTL-03 | pause | Suspend energy transfer while retaining accumulators | W→P | on operator command | channel selector | `<TBD-I10>` | negative acknowledgement with reason | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_pauseProgram 3`; BM §12.4.8.3 `PAU` |
| A-CTL-04 | interrupt | Interrupt the program, holding position | W→P | on operator command | channel selector | `<TBD-I10>` | negative acknowledgement with reason | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.4.5 `INT`; `CODE-S` `csInt` |
| A-CTL-05 | continue | Resume after a pause or an interrupt | W→P | on operator command | channel selector | `<TBD-I10>` | negative acknowledgement with reason | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_continueProgram 4`; BM §2 |
| A-CTL-06 | reset | Return a channel, or the board, to its initial state | W→P | on operator command | scope: board or channel | `<TBD-I10>` | negative acknowledgement; refused while a program runs unless forced | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-P` `en_reset 6`; BM §2.10 `RESET` |
| A-CTL-07 | clear-fault | Clear a latched fault | W→P | on operator command | channel selector; fault identity, or all | `<TBD-I10>` | negative acknowledgement; refused if the underlying condition persists | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S13-011`, `-012`; Open Issue **#17** |
| A-CTL-08 | set-digital-output | Set a channel's spare digital outputs | W→P | on command | channel selector; per-output commanded state | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `CODE-S` `CONTROL_CMD_Q_ID_DO_SELECTION`; Open Issue **#19** |
| A-CTL-09 | control-result | Report the per-channel outcome of a control command | P→W | on every A-CTL message | correlated request identity; per channel, accepted or rejected with reason | n/a | none | ⟨D-05⟩ | ⟨D-05⟩ | CS-07, CS-08; NEW — ME (the legacy protocol acknowledges per device, not per channel) |

### 4.6 A-TLM — Live telemetry

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-TLM-01 | channel-realtime | Stream one channel's live state | P→W | cyclic per channel, `<TBD-I13>`; legacy 500 ms | channel identity; step number; program run state; channel state; error identity; fault bitmask; step elapsed time; total elapsed time; current; voltage; temperature; power; accumulated, charge, discharge and step capacity; accumulated, charge, discharge and step energy; operator; cycle number; table row number; digital I/O state | none, best-effort | loss is detected by staleness, not by timeout; see CS-22 | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §5.2 (79 B, 25 fields); `SRS-PRI-P10-*` |
| A-TLM-02 | board-realtime-rollup | Stream the board's aggregate state across all channel positions | P→W | cyclic, `<TBD-I13>` | board state; per channel position: presence, channel state, active fault summary; aggregate power | none, best-effort | as A-TLM-01 | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P3-011`, `SRS-PRI-P2-012`; **NEW — ME** |
| A-TLM-03 | telemetry-subscribe | Select which channels stream live and at what rate | W→P | on command | channel selector; requested interval; requested quantity subset | `<TBD-I10>` | negative acknowledgement if the requested rate cannot be met | ⟨D-05⟩ | ⟨D-05⟩ | **NEW — ME**. Necessary because eight channels streaming at the legacy rate is eight times the legacy load — Open Issue **#18** |
| A-TLM-04 | calibration-live | Stream live measured and raw values during calibration | P→W | cyclic while calibrating, 500 ms | channel identity; measured current; raw current count; measured voltage; raw voltage count; measured temperature; raw temperature count; calibration error identity | none | as A-TLM-01 | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §5.3 (21 B); `CODE-S` `LIVE_DATA_SEND_TIME 500U` |

### 4.7 A-LOG — Stored registration data

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-LOG-01 | registration-batch | Deliver recorded measurement rows for persistent storage | P→W | cyclic, and on buffer threshold | session identity; channel identity; step number; operator; a batch of records, each an opcode-tagged set of quantities | `<TBD-I14>` | retry; on repeated failure the board buffers and reports the backlog | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/ICD` §6.2, §6.3; BM §12.4.1 |
| A-LOG-02 | registration-batch-ack | Confirm a batch has been durably stored | W→P | on A-LOG-01 | session identity; the highest record accepted | `<TBD-I14>` | the board retains and retransmits from the last acknowledged record | ⟨D-05⟩ | ⟨D-05⟩ | **NEW — ME**. The legacy session-store interface is fire-and-forget UDP with **no acknowledgement**, so a dropped batch is silently lost — see §8.3 |
| A-LOG-03 | request-buffered-data | Ask for data recorded while the link was down | W→P | on reconnection | session identity; the last record the Web Application holds | `<TBD-I14>` | negative acknowledgement if the board no longer holds it | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P16-*`; HW pending item "Data save on Primary in case of PC disconnect"; Open Issue **#12** |
| A-LOG-04 | buffer-status | Report how much recorded data is held and unsent | P→W | cyclic, and on request | per channel: records buffered; oldest record timestamp; buffer occupancy as a fraction; records discarded since the last report | none | none | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S12-022`; **NEW — ME**; Open Issue **#11** |
| A-LOG-05 | session-open | Declare the start of a logging session | P→W | on program start | session identity; channel identity; program identity and version; battery parameter record; registration format; start time | `<TBD-I10>` | the batch that follows cannot be attributed; treated as a transfer failure | ⟨D-05⟩ | ⟨D-05⟩ | `WAD/DBD` session model; `WAD/ICD` §6.1 |
| A-LOG-06 | session-close | Declare the end of a logging session | P→W | on program end | session identity; end time; end reason; final accumulator values; record count | `<TBD-I10>` | the Web Application closes the session on staleness instead | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S12-017`; `WAD/DBD` |

### 4.8 A-CAL — Calibration

Twenty-three commands, enumerated individually because each must receive its own encoding
under **D-05**. All are `W→P`, all carry a channel selector, all are acknowledged, and all
share the timeout `<TBD-I10>` and the error behaviour *negative acknowledgement with a
calibration error identity; stored calibration unchanged*. The `Information content`
column states only what is additional to the channel selector.

| Msg | Name | Purpose | Trigger | Additional information content | Source |
|---|---|---|---|---|---|
| A-CAL-01 | calib-is-ready | Confirm the channel can enter calibration | before entry | none | `CODE-P` `en_isReadyForCalibQuery 0x01` |
| A-CAL-02 | calib-live-data-start | Begin streaming live calibration data (see A-TLM-04) | on command | requested interval | `CODE-P` `en_liveCalibData 0x02` |
| A-CAL-03 | cha-current-low-point | Record the charge-current low reference point | on command | applied reference current; measurement range | `CODE-P` `en_chaCurrentLowPoint 0x03` |
| A-CAL-04 | cha-current-high-point | Record the charge-current high reference point | on command | applied reference current; measurement range | `CODE-P` `en_chaCurrentHighPoint 0x04` |
| A-CAL-05 | cha-current-commit | Compute and store charge-current gain and offset | on command | measurement range; calibration timestamp | `CODE-P` `en_chaCurCalGainOffset 0x05` |
| A-CAL-06 | dch-current-low-point | Record the discharge-current low reference point | on command | applied reference current; measurement range | `CODE-P` `en_disChaCurrentLowPoint 0x06` |
| A-CAL-07 | dch-current-high-point | Record the discharge-current high reference point | on command | applied reference current; measurement range | `CODE-P` `en_disChaCurrentHighPoint 0x07` |
| A-CAL-08 | dch-current-commit | Compute and store discharge-current gain and offset | on command | measurement range; calibration timestamp | `CODE-P` `en_disChaCurCalGainOffset 0x08` |
| A-CAL-09 | cha-voltage-low-point | Record the charge-voltage low reference point | on command | applied reference voltage | `CODE-P` `en_chaVoltageLowPoint 0x09` |
| A-CAL-10 | cha-voltage-high-point | Record the charge-voltage high reference point | on command | applied reference voltage | `CODE-P` `en_chaVoltageHighPoint 0x0A` |
| A-CAL-11 | cha-voltage-commit | Compute and store charge-voltage gain and offset | on command | calibration timestamp | `CODE-P` `en_chaVolCalGainOffset 0x0B` |
| A-CAL-12 | dch-voltage-low-point | Record the discharge-voltage low reference point | on command | applied reference voltage | `CODE-P` `en_disChaVoltageLowPoint 0x0C` |
| A-CAL-13 | dch-voltage-high-point | Record the discharge-voltage high reference point | on command | applied reference voltage | `CODE-P` `en_disChaVoltageHighPoint 0x0D` |
| A-CAL-14 | dch-voltage-commit | Compute and store discharge-voltage gain and offset | on command | calibration timestamp | `CODE-P` `en_disChaVolCalGainOffset 0x0E` |
| A-CAL-15 | temp-low-point | Record the temperature low reference point | on command | applied reference resistance or temperature | `CODE-P` `en_tempCalLowPoint 0x0F`; `CODE-S` `TEMP_LOW_RES_VALUE 18.52` |
| A-CAL-16 | temp-high-point | Record the temperature high reference point | on command | applied reference resistance or temperature | `CODE-P` `en_tempCalHighPoint 0x10`; `CODE-S` `TEMP_HIGH_RES_VALUE 390.48` |
| A-CAL-17 | temp-commit | Compute and store temperature gain and offset | on command | calibration timestamp | `CODE-P` `en_tempCalGainOffset 0x11` |
| A-CAL-18 | calib-cancel | Abandon the procedure, leaving stored calibration unchanged | on command | none | `CODE-P` `en_cancleCalibration 0x12` |
| A-CAL-19 | calib-stop | Leave calibration mode | on command | none | `CODE-P` `en_stopCalibration 0x13` |
| A-CAL-20 | verify-cha-current-start | Drive a charge current so applied calibration can be checked | on command | commanded current | `CODE-P` `en_verifyCurrentCalChargeMode 0x14` |
| A-CAL-21 | verify-dch-current-start | Drive a discharge current so applied calibration can be checked | on command | commanded current | `CODE-P` `en_verifyCurrentCalDischargeMode 0x15` |
| A-CAL-22 | verify-current-stop | End a verification drive | on command | none | `CODE-P` `en_stopVerifyCurrentCal 0x16` |
| A-CAL-23 | read-calibration | Return the stored calibration parameters and their timestamps | on request | which parameter set: full scale, one of four ranges, auto-scale, voltage, temperature, bank voltages, polarity sense | `CODE-P` `en_prevCalibData 0x17`; `CODE-S` `readCalibParameters()` |

> **Two notes on A-CAL.** First, the legacy Primary and the legacy Web Application
> disagree about the calibration query numbering: `CODE-P` places cancel at `0x12` and
> read-previous at `0x17`, while `WAD/ICD` §3.8 places cancel at `0x0F` and
> read-previous at `0x14` — a three-command offset corresponding exactly to the
> temperature triplet. The `CODE-P` numbering, which includes temperature, is the later
> and is used above. Recorded as `<TBD-I15>`.
> Second, A-CAL-03 to A-CAL-08 carry a **measurement range** because the Secondary holds
> independent calibration per range (`SRS-SEC-S14-009`). Whether ME retains that scheme is
> Open Issue **#15**; if it does not, the range field disappears from six messages.

### 4.9 A-EVT — Fault and event reporting

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-EVT-01 | channel-fault | Report a change in a channel's active fault set | P→W | on event, immediately | channel identity; complete active fault set; the fault that changed; time of change; whether it latches | `<TBD-I10>` | retry; the fault is also visible in A-TLM-01 | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S13-003`, `-004`; `WAD/ICD` §9.2 |
| A-EVT-02 | channel-message | Report a numbered informational message raised by program execution | P→W | on event | channel identity; message number; step at which it was raised | `<TBD-I10>` | retry | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.4.8.6 `MSG`; BM default messages 1–6 |
| A-EVT-03 | channel-user-error | Report a numbered user error raised by program execution | P→W | on event | channel identity; user error number; step at which it was raised | `<TBD-I10>` | retry | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.4.8.5 `ERR`; `CODE-S` `userExId` |
| A-EVT-04 | board-event | Report a board-level event that is not a channel fault | P→W | on event | event class: restart, self-test outcome, configuration change, calibration change, firmware change, storage threshold, node presence change, link loss or restoration; event detail; time | `<TBD-I10>` | retry; the event is retained for later retrieval if the link is down | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P22-*`; `SRS-PRI-P3-009`; Open Issue **#17** |
| A-EVT-05 | read-event-log | Retrieve retained events | W→P | on request | time range, or from a given event identity | `<TBD-I14>` | negative acknowledgement if the range is no longer held | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P22-*`; Open Issue **#17** |
| A-EVT-06 | message-catalogue | Define or read the operator-editable message texts | ↔ | on command or request | message number; text | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | BM §12.4.8.6 (*Maintenance → BTS-600 Messages*, user-extensible); conflict **C-14** |

### 4.10 A-NOD — Multi-node management (new to ME)

No precedent exists in any source; these messages arise entirely from the change from one
Secondary to eight.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| A-NOD-01 | read-node-inventory | Ask which channel positions are populated and by what | W→P | on request, and after A-REG-02 | none | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P3-011`; **NEW — ME** |
| A-NOD-02 | node-inventory | Report all eight channel positions | P→W | on A-NOD-01, and on any presence change | per position: populated or empty; node identity; type number; hardware, bootloader and application version; compatibility verdict; enumeration state | n/a | none | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P3-004`, `-011`, `-012`; **NEW — ME** |
| A-NOD-03 | node-presence-change | Report that a Secondary Board has appeared or disappeared | P→W | on event | channel position; node identity; new presence state; time; effect on any program running on that channel | `<TBD-I10>` | retry | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P3-009`; **NEW — ME**; Open Issue **#14** |
| A-NOD-04 | node-identity-conflict | Report two Secondary Boards claiming one identity | P→W | on event | the conflicting identity; the channel positions involved | `<TBD-I10>` | retry; the affected channels are unusable until resolved | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P3-005`; **NEW — ME** |
| A-NOD-05 | assign-program-to-channels | Assign a resident program to one or more channel positions | W→P | on command | program identity and version; channel selector; whether execution is independent per channel or synchronised across them; synchronisation group identity, if synchronised | `<TBD-I10>` | negative acknowledgement per channel with reason | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P6-*`; BM §12.4.8.16 (SYNCLine/SYNCProgram); Open Issues **#15**, **#16** |
| A-NOD-06 | read-channel-assignment | Read the current program assignment per channel | W→P | on request | none | `<TBD-I10>` | negative acknowledgement | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P6-*`; **NEW — ME** |
| A-NOD-07 | firmware-image-transfer | Transfer a Secondary Board firmware image to the Primary Board for onward distribution | W→P | segmented, on command | target: channel selector or all; image identity and version; target hardware variant; integrity value; segment index; total segments | `<TBD-I12>` overall | reject the whole transfer and report which segment failed | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P20-*`; `SRS-SEC-S16-001`; conflict **C-06**, Open Issue **#9** |
| A-NOD-08 | firmware-update-command | Instruct the Primary Board to apply a transferred image to its targets | W→P | on command | image identity; channel selector; whether to proceed if a channel is not idle, which shall be refused | `<TBD-I12>` | negative acknowledgement per target | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S16-003`; Open Issue **#9** |
| A-NOD-09 | firmware-update-progress | Report progress and outcome of a firmware update | P→W | cyclic while updating, and on completion | image identity; per target: state, percentage complete, outcome, failure reason | none | none | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-SEC-S16-013`; **NEW — ME** |
| A-NOD-10 | primary-firmware-transfer | Transfer a Primary Board firmware image | W→P | segmented, on command | image identity and version; integrity value; segment index; total segments | `<TBD-I12>` overall | reject the whole transfer | ⟨D-05⟩ | ⟨D-05⟩ | `SRS-PRI-P20-*`; SOC HAB provides the integrity basis |

### 4.11 Messages deliberately excluded from IF-A

Recorded so that the exclusions are auditable. Each exists in the legacy protocol and is
**not** carried into ME.

| Legacy message | Legacy identity | Why excluded |
|---|---|---|
| Send DBC steps count | `ProgramQueryID_t` `en_canDbcStepsQuery 0x07`; `WAD/ICD` §3.6 `0x07` | External CAN and DBC are out of ME scope — Open Issue **#29** resolved; §2.4 |
| Send DBC file | `ProgramQueryID_t` `en_canDbcDataQuery 0x08`; `WAD/ICD` §3.6 `0x08` | As above |
| DBC value records in the logged-data stream | `WAD/ICD` §6.3 opcodes `0x1F`–`0xFA` | As above. Note this frees a large opcode range in the logged-data record space |
| RS-485 configuration packet | `CODE-P` `RS_485_CONFIG_DATA_PACKET 0xFF` | Superseded. Modbus configuration belongs to IF-D; see §6.1 |
| Per-circuit device registration | `WAD/ICD` §3.4 (`DeviceID` + `CircuitID` in the registration packet) | Replaced by board-level registration (A-REG-01) plus dynamic node inventory (A-NOD-02). A BTS device carried 1–2 circuits statically; an ME board carries up to 8 dynamically |

**IF-A total: 90 messages in scope, 5 excluded.**

---
## 5. IF-B message set — Primary Board ↔ Secondary Board

### 5.1 Reading note

Every message below carries a node address (CS-01) and is subject to the loss-of-
communication rules CS-18 to CS-20. `⟨D-04⟩` marks the two reserved columns.

The legacy framing is recorded once here rather than in every row: `CODE-S` used a
five-group scheme distinguished by a leading byte — `0x11` configuration, `0x22` program
step, `0x33` measured data, `0x44` control command, `0x55` calibration — with a circuit
identifier, a query identifier and a CRC-16/Modbus check, over two UARTs at
`BTS_SEC_UART_SEND_TIME_OUT` 999 ms. **All of that is prior art for D-04.** It is a
point-to-point serial framing and does not survive contact with a shared CAN bus.

### 5.2 What is not carried forward, and why

This is the most consequential statement in this document, so it is made explicitly rather
than left to be inferred from absence.

In BTS, the Secondary Board held the program and executed the step engine. Seven legacy IF-B
messages served that role, listed in six rows below because two share a purpose.
**None is carried forward unchanged.**

| Legacy message | Legacy identity | Disposition in ME |
|---|---|---|
| Program step is-ready | `progStepQueryID_t` `PROGRAM_STEP_Q_ID_IS_READY 0x01` | **Retained in substance** as B-DAT-01. A readiness handshake is still needed before control data flows (CS-36). |
| Program step receive | `PROGRAM_STEP_Q_ID_RCV 0x02` | **Replaced** by B-DAT-02. The Secondary no longer receives a *step*; it receives control data whose content is **D-03**. |
| Next step request | `PROGRAM_STEP_Q_ID_NEXT_STEP 0x03` | **Retained in substance** as B-DAT-04, because the Secondary must still be able to say "I have finished; send the next thing" (`SRS-SEC-S4-023`). Whether that remains the right mechanism when the Primary sequences is part of **D-03**. |
| Table row request / next row | `PROGRAM_STEP_Q_ID_REQT_ROW 0x04`, `PROGRAM_STEP_Q_ID_NEXT_ROW 0x05` | **Not carried forward.** TABLE-operator row streaming was a consequence of the Secondary interpreting the TABLE operator. In ME the Primary decodes TABLE and emits ordinary control data per row. |
| Cycle table download | `PROGRAM_STEP_Q_ID_CYCLE_TABLE 0x06` (`cycleTable_t`, 16 cycles, nesting depth 4) | **Not carried forward.** Cycle and nesting control is wholly Primary-side (`ME-SRS-PRI-001` §4.7). |
| Cycle counts backup | `PROGRAM_STEP_Q_ID_CYCLE_COUNTS 0x07` | **Not carried forward** in that form. The Secondary persists only its *own* context across a power fail (B-PFA-02), not loop counters. The Primary persists the sequencing state. |

**The consequence, recorded as Open Issue S-32 in `ME-SRS-SEC-001` §8.1.** The legacy
Secondary detected a cut-off condition locally, confirming it over five consecutive
evaluations at a 1 ms cadence — so a cut-off acted in roughly 5 ms with no bus involved.
If the Primary now decides, the same decision requires a telemetry message to cross the
bus and a command to cross back. §7.4 quantifies that round trip. It is the reason
`<TBD-S23>` and `<TBD-S34>` in the Secondary SRS cannot be answered without first
answering **D-03**.

### 5.3 B-NOD — Node enumeration and presence (new to ME)

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-NOD-01 | enumerate-request | Ask nodes to identify themselves | P→S | after Primary initialization, and on demand | none, or a target node address | `<TBD-I16>` | positions that do not answer are reported empty (A-NOD-02) | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-PRI-P1-013`, `-P3-002`; **NEW — ME** |
| B-NOD-02 | enumerate-reply | Identify the node | S→P | on B-NOD-01 | node identity; type number; hardware version; bootloader version; application version; configured transistor-bank construction; software-selectable option value | n/a | none | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S3-007`; HW *Configuration* #1–4, #15 |
| B-NOD-03 | heartbeat | Confirm the node is alive when no other traffic flows | S→P | cyclic, `<TBD-I17>` | node identity; operating state; active fault summary | `<TBD-I17>` | the Primary declares the node absent per CS-20 | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-PRI-P3-007`; **NEW — ME** |
| B-NOD-04 | node-poll | Solicit a heartbeat from a node that has gone quiet | P→S | on the presence timeout approaching | node address | `<TBD-I16>` | the node is declared absent | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-PRI-P3-008`; **NEW — ME** |
| B-NOD-05 | assign-node-address | Assign a node address, if addressing is Primary-assigned | P→S | during enumeration | proposed address; the node's unique hardware identifier | `<TBD-I16>` | the node remains unaddressed and reports the failure | ⟨D-04⟩ | ⟨D-04⟩ | `<TBD-S15>`; Open Issue **#22**; **D-04**. **Conditional:** this message exists only if the identity is Primary-assigned rather than strapped or provisioned |
| B-NOD-06 | time-sync | Distribute wall-clock time to a node | P→S | on command, and periodically | absolute time | `<TBD-I16>` | the node keeps its previous time and marks its data unsynchronised | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S4-018`; `CODE-S` `BTS_BatteryData_t.epochTime` |

### 5.4 B-CFG — Configuration and provisioning

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-CFG-01 | config-is-ready | Confirm the node can accept configuration | P→S | before any B-CFG exchange | none | `<TBD-I16>` | the transfer is not started | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_IS_READY 0x01` |
| B-CFG-02 | read-factory-config | Request the node's factory configuration | P→S | on request | none | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_READ_FACT_DATA 0x02` |
| B-CFG-03 | factory-config | Carry the factory configuration | ↔ | on B-CFG-02 or B-CFG-04 | type number; bank voltage limits; absolute maximum and minimum circuit voltage; maximum charge and discharge current; maximum power; temperature limits; rectifier maximum voltage; twelve regulation controller parameter sets; readiness flag | `<TBD-I16>` | negative acknowledgement; stored configuration unchanged | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `BTS_FactoryData_t` (109 B) |
| B-CFG-04 | write-factory-config | Write the factory configuration | P→S | on command, relayed from A-CFG-04 | as B-CFG-03, plus an unlocking indication | `<TBD-I16>` | negative acknowledgement; stored configuration unchanged | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_WRITE_FACT_DATA 0x03`; `SRS-SEC-NF-032` |
| B-CFG-05 | write-battery-params | Write the battery parameter record | P→S | on command, relayed from A-CFG-10 | nominal capacity; number of cells; gassing voltage; maximum voltage; nominal current; cold-cranking current; charge factor; internal resistance; cut-off voltage; nominal voltage; energy density; absolute time | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_WRITE_BATT_DATA 0x04`, `BTS_BatteryData_t` (43 B) |
| B-CFG-06 | write-manufacturing-data | Write provisioning data | P→S | on command, factory action | serial number; type number; versions; manufacturing date | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_WRITE_MANUFACT_DATA 0x05` |
| B-CFG-07 | read-manufacturing-data | Read provisioning data | P→S | on request | none | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONFIG_DATA_Q_ID_READ_MANUFACT_DATA 0x06` |
| B-CFG-08 | read-config-item | Read one configuration parameter by identity | P→S | on request | parameter identity | `<TBD-I16>` | negative acknowledgement for an unknown parameter | ⟨D-04⟩ | ⟨D-04⟩ | HW *Configuration* (per-item parameter identifiers) |
| B-CFG-09 | write-config-item | Write one configuration parameter by identity | P→S | on command | parameter identity; value | `<TBD-I16>` | negative acknowledgement, distinguishing unknown parameter from out-of-range value | ⟨D-04⟩ | ⟨D-04⟩ | HW *Configuration*; `SRS-SEC-S15-*` |
| B-CFG-10 | restore-defaults | Restore configuration to defaults | P→S | on command | whether calibration is included, which requires explicit request | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S15-016`, `-017` |
| B-CFG-11 | config-state | Report which items are at defaults rather than commissioned values, and the persistent-store write count | S→P | on request, and after any write | per item or per block: default or commissioned; store write count; last integrity check outcome | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S15-013`, `-018`; **NEW — ME** |

### 5.5 B-DAT — Control data (content reserved to D-03)

This is the group that replaces legacy step transfer. **Every row states what must move and
what must happen; none states the content layout.**

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-DAT-01 | control-data-is-ready | Confirm the node can accept control data | P→S | before regulation begins | none | `<TBD-I16>` | regulation is not started; the channel is reported unusable | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `PROGRAM_STEP_Q_ID_IS_READY 0x01`; `SRS-SEC-S4-010` |
| B-DAT-02 | control-data | Command a regulation mode, its setpoint or setpoints, and the safety limits to enforce while applying it | P→S | on each new unit of regulation | regulation mode; setpoint or setpoints; the safety limits the node must enforce locally; a correlation identity so telemetry can be attributed; the registration type to apply; **and, subject to D-03, any threshold condition the node is required to evaluate** | `<TBD-I16>` | negative acknowledgement with reason; the node does not begin regulating and reports the rejection | ⟨D-04⟩ | ⟨D-04⟩ | Brief; `SRS-SEC-S4-001`, `-002`; **D-03**; `PSPE` §4.4 recorded as prior art only |
| B-DAT-03 | control-data-ack | Accept or reject a unit of control data | S→P | on B-DAT-02 | correlation identity; accepted, or rejected with a reason distinguishing unrecognised, out-of-range against configured absolute ratings, and invalid in the current state | n/a | the Primary does not command a start | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S4-006`, `-007`, `-020`; CS-08 |
| B-DAT-04 | control-data-complete | Report that the commanded regulation has finished, and why | S→P | on completion | correlation identity; completion reason, distinguishing commanded stop, a supplied threshold met, a protective limit reached, and a fault; the final measured and derived values | `<TBD-I16>` | the Primary re-requests status; the node holds the safe state meanwhile | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S4-023`, `-S5-037`; `CODE-S` `btsSecReqNextProgramStep()`, `PROG_STOP_ACK 0xFFFF` |
| B-DAT-05 | control-data-request | Ask for the next unit of control data | S→P | on completion of the previous unit; re-requested every **100 ms** for **5** attempts | correlation identity of the completed unit | **100 ms**, 5 attempts | after 5 attempts the node enters the safe state and reports a fault | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `REQEST_STEP_TIME_OUT 100U`, `REQEST_STEP_RESP_COUNT 5U`; `SRS-SEC-S4-024`, `-025` |
| B-DAT-06 | setpoint-update | Change the setpoint within the unit of regulation in force, without restarting it | P→S | on event | correlation identity; the new setpoint; the ramp to apply | `<TBD-I16>` | negative acknowledgement; the previous setpoint remains in force | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S4-021`, `-S7-001`; **NEW — ME**. Needed because the Primary owns ramp trajectories (BM §12.4.3) that the Secondary previously computed itself |
| B-DAT-07 | registration-type-update | Change which quantities the node records, while regulating | P→S | on event | correlation identity; registration type selection | `<TBD-I16>` | negative acknowledgement; the previous selection remains | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S12-018`; BM §12.4.6 |
| B-DAT-08 | range-command | Command a fixed current measurement range, or automatic selection | P→S | on event | range selection, where zero requests automatic | `<TBD-I16>` | negative acknowledgement; refused while regulating a unit of control data | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S7-023`, `-024`, `-025`; BM §12.4.8.13; conflict **C-12** |

### 5.6 B-CTL — Run commands

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-CTL-01 | start | Begin regulating the control data in force | P→S | on command | correlation identity | `<TBD-I16>` | negative acknowledgement with reason; the node stays in the safe state | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_SATRT_PROGRAM 0x01`; `SRS-SEC-S4-011` |
| B-CTL-02 | stop | Stop regulating and enter the safe state | P→S | on command | none | `<TBD-I16>` | **the node enters the safe state regardless**; a lost stop command is covered by the communication timeout (CS-19) | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_STOP_PROGRAM 0x02`; `SRS-SEC-S4-012` |
| B-CTL-03 | interrupt | Stop regulating, hold accumulators, await continue or stop | P→S | on command | none | `<TBD-I16>` | as B-CTL-02 | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_INTERRUPT_PROGRAM 0x03`; `SRS-SEC-S4-013` |
| B-CTL-04 | pause | Suspend energy transfer, hold accumulators | P→S | on command | none, or a duration | `<TBD-I16>` | as B-CTL-02 | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `isProgramPauseFlag`; `SRS-SEC-S4-015`; BM §12.4.8.3 |
| B-CTL-05 | continue | Resume after a pause or an interrupt | P→S | on command | whether the setpoint in force is retained or replaced | `<TBD-I16>` | negative acknowledgement; the node remains in the safe state | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_CONTINUE_PROGRAM 0x04`; `SRS-SEC-S4-014` |
| B-CTL-06 | set-digital-output | Set the node's spare digital outputs | P→S | on command | per-output commanded state | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_DO_SELECTION 0x06`; `SRS-SEC-S17-008` |
| B-CTL-07 | clear-fault | Clear a latched fault on the node | P→S | on command | fault identity, or all | `<TBD-I16>` | negative acknowledgement; refused while the condition persists | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S13-011`, `-012`; Open Issue **#17** |
| B-CTL-08 | control-ack | Accept or reject a run command | S→P | on every B-CTL message | correlated command; accepted or rejected with reason | n/a | the Primary reports the rejection onward (A-CTL-09) | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_RESP_ACK`/`_NACK`; CS-07 |

### 5.7 B-TLM — Telemetry, registration and faults

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-TLM-01 | realtime | Report live measured and derived state | S→P | cyclic, configurable, legacy default **500 ms** | node identity; correlation identity; operating state; regulation direction; active fault set; step and total elapsed time; current; voltage; power; the four capacity and the four energy accumulators; digital input and output state; current range in use; whether regulated current is at zero | none, best-effort | absence for the configured timeout is loss of communication (CS-20) | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `SEND_REAL_TIME_DATA 500U`, `BTS_MeasuredData_t`; `SRS-SEC-S12-001`…`-010` |
| B-TLM-02 | registration | Report a recorded measurement sample | S→P | cyclic, configurable, legacy default **100 ms** | node identity; timestamp; the quantities selected by the registration type in force, from the thirteen available; fault, message and user-error indications where selected | none, best-effort | see §8.3 — an unacknowledged loss here loses recorded data | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `SEND_REGISTRATION_DATA 100U`, `btsRegType_t`, `registration_t`; `SRS-SEC-S12-011`…`-019`; conflict **C-05** |
| B-TLM-03 | registration-on-demand | Report a registration sample immediately | P→S | on request | none | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `MEASURED_DATA_REG_Q_ID_TX 0x02`; `SRS-SEC-S12-016` |
| B-TLM-04 | registration-final | Report the closing sample when regulation stops | S→P | on regulation end | as B-TLM-02, plus the reason regulation ended | `<TBD-I16>` | the Primary requests it explicitly via B-TLM-03 | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `isSendRegAfterStopFlag`, `updateRegistrationParamOnStop()`; `SRS-SEC-S12-017` |
| B-TLM-05 | fault-report | Report a change in the active fault set immediately | S→P | on event, ahead of the next cyclic report | node identity; complete active fault set; the fault that changed; time; whether it latches; the protective action taken | `<TBD-I16>` | retry; the fault is also visible in B-TLM-01 | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S13-003`, `-004`; CS-30, CS-31 |
| B-TLM-06 | registration-loss-report | Report registration samples discarded for want of buffer or bus | S→P | on event, and cyclically while non-zero | node identity; count discarded since the last report; the time range affected | `<TBD-I16>` | retry | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S12-020`, `-022`; **NEW — ME** |
| B-TLM-07 | diagnostics | Report self-monitoring data | S→P | on request, and cyclically at a low rate | raw converter count per channel; commanded reference output in counts and engineering units; health-check channel states; accumulated operating time; contactor operation counts; restart count and last restart reason; worst observed regulation-loop interval; peak stack usage | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S18-*`; HW *Configuration* #23–42; HW remarks #30, #31, #34 |
| B-TLM-08 | telemetry-rate-config | Set the node's cyclic reporting intervals | P→S | on command | real-time interval; registration interval; diagnostics interval | `<TBD-I16>` | negative acknowledgement if the node cannot meet the interval | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S12-008`, `-011`; **NEW — ME**; Open Issue **#18** |

### 5.8 B-CAL — Calibration

The twenty-three calibration commands of §4.8 are relayed to the addressed node. All are
`P→S`, all are acknowledged with a calibration error identity on failure, all share the
timeout `<TBD-I16>`, and none may be issued while the node is regulating
(`SRS-SEC-S14-027`). Each is enumerated so that it can receive its own encoding under
**D-04**.

| Msg | Relays | Legacy identity | Additional information content |
|---|---|---|---|
| B-CAL-01 | A-CAL-01 | `CALIB_CMD_Q_ID_START 0x01` | none |
| B-CAL-02 | A-CAL-02 | `CALIB_CMD_Q_ID_LIVE_DATA 0x02` | requested interval |
| B-CAL-03 | A-CAL-03 | `CALIB_CMD_Q_ID_CHA_CURRENT_LP 0x03` | applied reference current; measurement range |
| B-CAL-04 | A-CAL-04 | `CALIB_CMD_Q_ID_CHA_CURRENT_HP 0x04` | applied reference current; measurement range |
| B-CAL-05 | A-CAL-05 | `CALIB_CMD_Q_ID_CHA_CURRENT_GO 0x05` | measurement range; calibration timestamp |
| B-CAL-06 | A-CAL-06 | `CALIB_CMD_Q_ID_DCH_CURRENT_LP 0x06` | applied reference current; measurement range |
| B-CAL-07 | A-CAL-07 | `CALIB_CMD_Q_ID_DCH_CURRENT_HP 0x07` | applied reference current; measurement range |
| B-CAL-08 | A-CAL-08 | `CALIB_CMD_Q_ID_DCH_CURRENT_GO 0x08` | measurement range; calibration timestamp |
| B-CAL-09 | A-CAL-09 | `CALIB_CMD_Q_ID_CHA_VOLT_LP 0x09` | applied reference voltage |
| B-CAL-10 | A-CAL-10 | `CALIB_CMD_Q_ID_CHA_VOLT_HP 0x0A` | applied reference voltage |
| B-CAL-11 | A-CAL-11 | `CALIB_CMD_Q_ID_CHA_VOLT_GO 0x0B` | calibration timestamp |
| B-CAL-12 | A-CAL-12 | `CALIB_CMD_Q_ID_DCH_VOLT_LP 0x0C` | applied reference voltage |
| B-CAL-13 | A-CAL-13 | `CALIB_CMD_Q_ID_DCH_VOLT_HP 0x0D` | applied reference voltage |
| B-CAL-14 | A-CAL-14 | `CALIB_CMD_Q_ID_DCH_VOLT_GO 0x0E` | calibration timestamp |
| B-CAL-15 | A-CAL-15 | `CALIB_CMD_Q_ID_TEMP_LP 0x0F` | applied reference resistance or temperature |
| B-CAL-16 | A-CAL-16 | `CALIB_CMD_Q_ID_TEMP_HP 0x10` | applied reference resistance or temperature |
| B-CAL-17 | A-CAL-17 | `CALIB_CMD_Q_ID_TEMP_GO 0x11` | calibration timestamp |
| B-CAL-18 | A-CAL-18 | `CALIB_CMD_Q_ID_CANCEL 0x12` | none |
| B-CAL-19 | A-CAL-19 | `CALIB_CMD_Q_ID_STOP 0x13` | none |
| B-CAL-20 | A-CAL-20 | `CALIB_CMD_Q_ID_START_VERIFY_CHA_CURENT 0x14` | commanded current |
| B-CAL-21 | A-CAL-21 | `CALIB_CMD_Q_ID_START_VERIFY_DCH_CURENT 0x15` | commanded current |
| B-CAL-22 | A-CAL-22 | `CALIB_CMD_Q_ID_STOP_VERIFY_CURENT 0x16` | none |
| B-CAL-23 | A-CAL-23 | `CALIB_CMD_Q_ID_READ_PARAM 0x17` | which parameter set to return |

Two further messages complete the group; they have no A-CAL counterpart because they carry
data *from* the node.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-CAL-24 | calibration-parameters | Return the stored calibration set | S→P | on B-CAL-23 | gain, offset and timestamp for: charge and discharge current at full scale and at each of four ranges; the auto-scale path; charge and discharge voltage; reverse-polarity sense; battery temperature; heatsink temperature; bank charge-mode and discharge-mode voltage; and the persistent-store write count | `<TBD-I16>` | negative acknowledgement | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `btsFact_n_calib_t`, `readCalibParameters()`; HW *Calibration* (25 parameters) |
| B-CAL-25 | calibration-live | Stream live measured and raw values during calibration | S→P | cyclic while calibrating, **500 ms** | measured current and its raw count; measured voltage and its raw count; measured temperature and its raw count; calibration state; calibration error identity | none | as B-TLM-01 | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `sendLiveCurrentVolt()`, `LIVE_DATA_SEND_TIME 500U`; `SRS-SEC-S14-017`, `-018` |

### 5.9 B-FWU — Firmware update over CAN

Conditional on the resolution of conflict **C-06** and Open Issue **#9**. HW §8 specifies
host-PC flashing; the client remark says update is not required; the ME brief requires it
over CAN. Specified here as the brief requires.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-FWU-01 | update-prepare | Ask the node to enter firmware-update mode | P→S | on command | image identity and version; target hardware variant; total image size; integrity value | `<TBD-I18>` | negative acknowledgement; **refused while regulating** | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S16-003`, `-008`; `CODE-S` error `0x11` |
| B-FWU-02 | update-segment | Carry one segment of the image | P→S | segmented, as fast as the bus allows | segment index; total segments; segment content | `<TBD-I18>` per segment; `<TBD-I12>` overall | the node rejects the whole update and remains on its current image | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S16-005`, `-006` |
| B-FWU-03 | update-segment-ack | Confirm a segment, or request retransmission | S→P | on each segment, or per window | segment index; accepted, or the index required | `<TBD-I18>` | the Primary retransmits or abandons | ⟨D-04⟩ | ⟨D-04⟩ | CS-26; **NEW — ME** |
| B-FWU-04 | update-activate | Verify and activate the received image | P→S | after the last segment | image identity; integrity value | `<TBD-I18>` | the node reports verification failure and **does not activate** | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S16-007`, `-012`; `CODE-S` errors `0x14`, `0x15` |
| B-FWU-05 | update-result | Report the outcome | S→P | on completion or failure | image identity; outcome; failure reason, distinguishing erase failure, verification failure, programming not possible, and variant mismatch; the version now running | `<TBD-I18>` | the Primary re-queries via B-NOD-01 | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S16-013`; `CODE-S` errors `0x11`, `0x14`, `0x15` |
| B-FWU-06 | update-abort | Abandon an update in progress | P→S | on command | image identity | `<TBD-I18>` | the node remains able to accept a further update (`SRS-SEC-S16-011`) | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S16-011`; **NEW — ME** |

### 5.10 B-PFA — Power-fail context

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| B-PFA-01 | power-fail-notify | Report an impending loss of supply | S→P | on event, once, best-effort | node identity; the state at the moment of detection | none — the node cannot wait | the Primary infers the loss from the communication timeout | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S19-001`, `-006`; HW error `0x12`; `CODE-P` power-fail notification ×3 |
| B-PFA-02 | persisted-context | Report the context persisted across the supply loss | S→P | after the next initialization, on request | the four capacity and four energy accumulators; elapsed regulation time; elapsed time of the interrupted unit of control data; operating state at interruption; active fault set; registration type in force; correlation identity of the interrupted unit; whether the context passed its integrity check | `<TBD-I16>` | the node reports the context unusable rather than returning a corrupt one | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `progBackUp_t`, `powerFailBackup()`; `SRS-SEC-S19-004`, `-010`, `-011` |
| B-PFA-03 | power-resume | Restore the persisted context and prepare to continue | P→S | on command | the context to restore, or an instruction to use the node's own persisted copy | `<TBD-I16>` | negative acknowledgement; the node stays in the safe state | ⟨D-04⟩ | ⟨D-04⟩ | `CODE-S` `CONTROL_CMD_Q_ID_POWER_RESUME 0x05`, `powerFailResume()`; `SRS-SEC-S19-008` |
| B-PFA-04 | restart-report | Report the reason for the most recent restart | S→P | after initialization, unsolicited | node identity; restart reason, distinguishing orderly, supply loss, watchdog expiry and external reset; self-test outcome; restart count | `<TBD-I16>` | the Primary requests it explicitly | ⟨D-04⟩ | ⟨D-04⟩ | `SRS-SEC-S1-017`, `-018`; `SRS-SEC-S11-005` |

**IF-B total: 76 messages in scope. Of the seven legacy step-transfer messages,
two are retained in substance, one is replaced, and four are not carried forward (§5.2).**

---
## 6. IF-D message set — Primary Board ↔ Modbus client

### 6.1 Scope and posture

Open Issue **#29** is resolved: the Modbus module is in ME scope and is hosted by the
Primary Board. The legacy provision — two dedicated RS-485 ports on the COM Controller
(`CODE-C` `ModSlave.c`, `RS485_Slave.c`, `RS485_Master.c`) — has no host in ME, and the
i.MX 8M Plus offers four UARTs that need external transceivers (conflict **C-17**).

The Primary Board is a Modbus **slave** (`LSRS` SW_REQ_25). IF-D therefore has no
ME-specific message set: its messages are Modbus function codes, and what ME must actually
define is **the register map** those function codes address. That map is `<TBD-I19>`.

| Msg | Name | Purpose | Dir | Trigger / Rate | Information content | Timeout | Error behaviour | Encoding | Frame/ID | Source |
|---|---|---|---|---|---|---|---|---|---|---|
| D-MB-01 | read-holding-registers | Read configuration and measurement values | C→P | on client request | starting register; register count | `<TBD-I20>` | Modbus exception response | Modbus RTU or TCP — ⟨D-05⟩ | function code `0x03` | `LSRS` SW_REQ_24, 27, 33 |
| D-MB-02 | read-input-registers | Read measurement values only | C→P | on client request | starting register; register count | `<TBD-I20>` | Modbus exception response | ⟨D-05⟩ | function code `0x04` | `LSRS` SW_REQ_33 |
| D-MB-03 | write-single-register | Write one writable item | C→P | on client request | register address; value | `<TBD-I20>` | Modbus exception response; the write is not applied | ⟨D-05⟩ | function code `0x06` | `LSRS` SW_REQ_33; Open Issue **#29** |
| D-MB-04 | write-multiple-registers | Write a contiguous block of writable items | C→P | on client request | starting register; count; values | `<TBD-I20>` | Modbus exception response; no partial write is applied | ⟨D-05⟩ | function code `0x10` | `LSRS` SW_REQ_33 |
| D-MB-05 | read-coils / read-discrete-inputs | Read digital states | C→P | on client request | starting address; count | `<TBD-I20>` | Modbus exception response | ⟨D-05⟩ | function codes `0x01`, `0x02` | `LSRS` SW_REQ_27 (digital input reporting) |
| D-MB-06 | write-single-coil | Set one digital output | C→P | on client request | address; state | `<TBD-I20>` | Modbus exception response | ⟨D-05⟩ | function code `0x05` | `LSRS` SW_REQ_15; Open Issue **#19** |
| D-MB-07 | diagnostic / report-slave-id | Identify the board to the client | C→P | on client request | none | `<TBD-I20>` | Modbus exception response | ⟨D-05⟩ | function codes `0x08`, `0x11` | `LSRS` SW_REQ_33 |

### 6.2 Register map — the actual work item

`LSRS` SW_REQ_27 names what a Modbus client must be able to read: battery voltage,
current, test status, whether the test is charging or discharging, signals received on the
external CAN ports, parameters received on the second RS-485 port, and errors. Two of
those seven no longer exist — the external CAN ports and the second RS-485 port are out of
ME scope (§2.4) — and the remaining five must be multiplied by eight channels.

| Item | Per | Access | Note |
|---|---|---|---|
| Battery voltage | channel × 8 | read | `LSRS` SW_REQ_27.1 |
| Battery current | channel × 8 | read | `LSRS` SW_REQ_27.2 |
| Test status | channel × 8 | read | `LSRS` SW_REQ_27.3; the channel state of `SRS-PRI-P2-008` |
| Charge or discharge indication | channel × 8 | read | `LSRS` SW_REQ_27.4 |
| Active fault set | channel × 8 | read | `LSRS` SW_REQ_27.7; conflict **C-14** applies |
| Power, capacity and energy accumulators | channel × 8 | read | Not in `LSRS`; a Modbus client that can see voltage and current will expect these — **NEW — ME** |
| Channel presence | channel × 8 | read | **NEW — ME**; a Modbus client must be able to tell an idle channel from an absent one |
| Board state | board | read | `SRS-PRI-P2-002` |
| Start, stop, pause, continue | channel × 8 | **write** | **`<TBD-I21>` — whether write access is permitted at all.** `ME-SRS-PRI-001` carries this as `<TBD-44>` and Open Issue **#29**. A Modbus client that can start a test is a safety-relevant actor |
| ~~External CAN signals~~ | — | — | **Excluded**; §2.4 |
| ~~Second RS-485 parameters~~ | — | — | **Excluded**; §2.4 |

| ID | Requirement on the map | Source |
|---|---|---|
| CS-44 | The register map shall be documented, versioned, and stable across firmware releases within a major version. | NEW — ME |
| CS-45 | Every register shall carry the unit convention of CS-39. | CS-39 |
| CS-46 | A register whose channel is absent shall read as an explicitly invalid value, not as zero. | NEW — ME; CS-22 |
| CS-47 | `<TBD-I19>` — the complete register map: addresses, widths, scaling and access. | Unsourced |
| CS-48 | `<TBD-I20>` — Modbus transport (serial RS-485 or TCP), slave address range, bit rate, parity, stop bits, and response timeout. `LSRS` SW_REQ_26, 30, 31 and 34 leave every one of these as `XXX`. | `LSRS` (values absent) |
| CS-49 | `<TBD-I21>` — whether write access is permitted, and to which items. | Open Issue **#29** |

**IF-D total: 7 messages, plus a register map that is the substance of the interface.**

---

## 7. Timing and rate budget

This section exists because ME multiplies the legacy node count by eight while removing the
dedicated registration channel. It is the quantitative half of Open Issue **#18**.

### 7.1 Stated assumptions

Every number below follows from these. Change an assumption and the conclusions change.

| ID | Assumption | Basis |
|---|---|---|
| T-01 | IF-B is classic CAN 2.0B at **250 kbps** unless stated otherwise. | HW *Configuration* #14 |
| T-02 | A classic CAN extended frame with 8 data bytes occupies **150 bits** including inter-frame space and typical bit stuffing (*extra bits CAN inserts to keep the receivers' clocks locked to the sender; they add to the real length of every message*). Arithmetic: 1 SOF + 32 arbitration + 6 control + 64 data + 16 CRC + 2 ACK + 7 EOF + 3 IFS = 131 bits, plus stuffing on the 118 stuffable bits. | CAN 2.0B specification |
| T-03 | Usable frame rate at 250 kbps is therefore **1667 frames/s**. | 250 000 ÷ 150 |
| T-04 | A registration record carrying all thirteen quantities plus the fault, message and user-error indications and addressing occupies about **64 bytes**, hence **8 classic frames**. | `CODE-S` `registration_t` (52 B) + `SystemError` + message + user error + addressing |
| T-05 | A real-time record occupies about **72 bytes**, hence **9 classic frames**. | `CODE-S` `BTS_MeasuredData_t` plus the eight accumulators; compare `WAD/ICD` §5.2 at 79 bytes |
| T-06 | Eight Secondary Boards report concurrently. | Project brief |
| T-07 | A CAN-FD frame carrying a 64-byte payload at 500 kbps arbitration and 2 Mbps data phase occupies about **340 µs**. | CAN-FD specification |
| T-08 | A bus utilisation above **50 %** (*how much of the cable's total capacity is in use*) is treated as the point beyond which worst-case latency ceases to be predictable for classic CAN. | Engineering practice; stated so it can be challenged |

### 7.2 Cyclic load at the legacy rates

| Traffic | Per node | 8 nodes | Frames/s | Bus load at 250 kbps |
|---|---|---|---|---|
| Registration at 100 ms (T-04) | 10 records/s × 8 frames = 80 frames/s | ×8 | **640** | **38.4 %** |
| Real-time at 500 ms (T-05) | 2 records/s × 9 frames = 18 frames/s | ×8 | **144** | **8.6 %** |
| Heartbeat at `<TBD-I17>`, assumed 100 ms, 1 frame | 10 frames/s | ×8 | **80** | 4.8 % |
| **Cyclic subtotal** | | | **864** | **51.8 %** |
| Control data, commands, acknowledgements | `<TBD-I23>` — depends on the average duration of a unit of control data | | not budgeted | not budgeted |

**The cyclic traffic alone, at the legacy rates, consumes about 52 % of a 250 kbps bus** —
already at the T-08 ceiling, before a single command, acknowledgement or unit of control
data is sent. This is the finding that Open Issue **#18** was raised to expose, now
quantified.

Note where the pressure comes from: registration is **74 % of the cyclic load**, and it is
the one rate that conflict **C-05** leaves undecided across five candidate values.

### 7.3 Conflict C-05 resolved by arithmetic

The five candidate registration intervals of **C-05**, each evaluated against the bus.

| Candidate | Source | Records/s across 8 nodes | Classic frames/s | Required bit rate | Verdict |
|---|---|---|---|---|---|
| **≤ 500 µs** | HW §6 | 16 000 | 128 000 | **≈ 19.2 Mbit/s** | **Impossible.** Exceeds classic CAN by ~19× and the CAN-FD data phase by ~2.4×, and CAN-FD arbitration is still capped at 1 Mbit/s |
| **1 ms** | Client remark, "need to approve up to 1 ms" | 8 000 | 64 000 | **≈ 9.6 Mbit/s** | **Impossible** on any CAN variant |
| **10 ms** | Client remark | 800 | 6 400 | **≈ 960 kbit/s** | **Not viable on classic CAN** — ~96 % of a 1 Mbit/s bus. **Viable on CAN-FD:** 800 FD frames/s × 340 µs (T-07) ≈ **27 %** |
| **100 ms** | Implemented in `CODE-S` | 80 | 640 | ≈ 96 kbit/s | **Viable.** 38 % of the default 250 kbps bus |
| **0.1 s** | BM §12.4.6, the manual's own maximum registration resolution | 80 | 640 | ≈ 96 kbit/s | **Viable.** Identical to 100 ms |

**Conclusion offered to the architect.** Three of the five candidates are excluded by
physics rather than by preference, and the exclusion does not depend on the disputed
assumptions: even at T-02 = 131 bits with no stuffing and a hypothetical zero inter-frame
space, 500 µs and 1 ms remain impossible by an order of magnitude. That reduces **C-05**
to a single real decision:

- **100 ms**, and IF-B stays classic CAN at 250 kbps; or
- **10 ms**, and IF-B must be **CAN-FD** with 64-byte payloads and a raised bit rate —
  which is a **D-04** decision with a hardware consequence, since every node needs a
  CAN-FD transceiver.

HW §7 already says *"Select Hardware suitable for CANFD"*, so the second option is
anticipated by the hardware specification. Recorded as `<TBD-I22>`.

**This does not close C-05.** The client may need 10 ms for a real measurement reason, and
the arithmetic says what that costs rather than deciding it. What the arithmetic does close
is the ≤500 µs figure in HW §6, which cannot be met by any CAN system and should be struck
from the specification.

### 7.4 Round-trip latency, and what it means for D-03

If the Primary decides when a unit of regulation ends, that decision crosses the bus twice.

| Stage | Time | Basis |
|---|---|---|
| Secondary acquires the measurement | ≤ 1 ms | `SRS-SEC-NF-002` |
| Secondary confirms a threshold, where it evaluates one | 5 evaluations | `SRS-SEC-S5-036`, `LIMIT_EX_COUNTER 5` |
| Registration or real-time record reaches the Primary | 8–9 frames × 0.6 ms = **4.8–5.4 ms** transmission, **plus arbitration delay behind other traffic at 52 % load** (*on CAN every node competes for the cable and the higher-priority message wins; a lower-priority one waits*) | T-02, T-03, §7.2 |
| Primary evaluates and decides | its own cycle, `<TBD-I24>` | `ME-SRS-PRI-001` §5.1 |
| Stop or next-unit command reaches the Secondary | 1 frame × 0.6 ms = **0.6 ms**, plus arbitration | T-02 |
| Secondary brings the reference output to zero | ≤ 10 ms | `SRS-SEC-NF-008`; HW §6 |

**One-way is roughly 5–10 ms; the round trip is roughly 12–25 ms** before any Primary
processing, on a bus already at half capacity.

Two consequences, both recorded rather than resolved:

1. **The legacy local path was about 5 ms** — five 1 ms evaluations, no bus. Moving the
   decision to the Primary multiplies the cut-off reaction time by roughly three to five.
2. **HW §6 requires a charge↔discharge transition within 10 ms at maximum current.** If a
   transition is triggered by a Primary-side decision, the round trip alone exceeds that
   budget. The 10 ms figure is therefore achievable only for transitions the Secondary
   initiates locally.

This is the quantitative reason that **D-03** cannot be settled on architectural taste
alone, and the reason `ME-SRS-SEC-001` §4.5.4 requires the Secondary to enforce supplied
limits locally whatever D-03 decides. It is also the substance of Open Issue **S-32**.

### 7.5 Rate budget requirements

| ID | Requirement | Source |
|---|---|---|
| CS-50 | The aggregate IF-B load shall not exceed `<TBD-I24>` under any combination of configured reporting rates and command traffic. | T-08; NEW — ME |
| CS-51 | The Primary Board shall reject a requested reporting rate that would exceed the configured bus-load ceiling, rather than accepting it and dropping traffic. | `SRS-SEC-S12-*`; `SRS-PRI-P10-*`; NEW — ME |
| CS-52 | Registration data shall be prioritised over real-time telemetry when the bus is saturated, because registration is a record and telemetry is a view. | NEW — ME; `<TBD-I25>` |
| CS-53 | A fault report shall be prioritised over all cyclic traffic. | CS-30 |
| CS-54 | `<TBD-I26>` — the priority ordering of IF-B message classes, which on CAN is an identifier-allocation decision and therefore part of **D-04**. | **D-04** |
| CS-55 | `<TBD-I23>` — the assumed average and minimum duration of a unit of control data, without which control-data traffic cannot be budgeted. BM's minimum step duration of **0.1 s** is the worst case: eight channels each completing a unit every 0.1 s would add 80 request-response exchanges per second. | BM §12.4.8.9 |
| CS-56 | The IF-A live telemetry rate shall be settable independently of the IF-B rate, so that eight channels streaming to the Web Application does not force eight channels streaming on the CAN bus. | A-TLM-03; NEW — ME |

---

## 8. Failure matrix

One row per failure mode: how it is detected, what each party does, and how the system
recovers.

### 8.1 IF-A failures

| Failure | Detected by | Detector's action | Peer's action | Recovery | Source |
|---|---|---|---|---|---|
| Web Application link lost | Primary, on keep-alive or command timeout | Continue running programs; buffer registration data; report on reconnection | Mark the board and all its channels stale; do not present stale data as current | Primary re-registers (A-REG-01); Web Application requests the backlog (A-LOG-03) | `SRS-PRI-P14-*`; `CODE-P` `ERR_NETWORK_CONN_FAIL`; CS-22 |
| Web Application link lost **while a program runs** | as above | **Programs continue.** The Primary does not stop a test because a viewer disappeared | as above | as above | `SRS-PRI-P14-*`; `CODE-P` `clearPauseCmdSentOnNetFail()` |
| Message integrity failure | Recipient | Discard, count, do not act | none | Sender retries on timeout | CS-12, CS-13, CS-14 |
| Command rejected | Web Application, on negative acknowledgement | Present the reason to the operator | Recipient's state unchanged | Operator corrects and retries | CS-07, CS-08 |
| Program transfer fails mid-way | Primary | Retain no partial program; report which segment failed | Retransmit the whole program | — | `SRS-PRI-P4-005`; CS-26, CS-27 |
| Logged-data batch lost | **Nobody, in the legacy design** | — | — | **See §8.3** | `WAD/ICD` §6 |
| Board storage approaching exhaustion | Primary | Report via A-LOG-04 | Retrieve and acknowledge the backlog | Policy is `<TBD>` — Open Issue **#11** | `SRS-PRI-P15-*` |
| Time never synchronised | Primary | Mark its data unsynchronised; do not silently timestamp with a default | Synchronise (A-CFG-11) | — | `LSRS` SW_REQ_23; A-CFG-11 |

### 8.2 IF-B failures

| Failure | Detected by | Detector's action | Peer's action | Recovery | Source |
|---|---|---|---|---|---|
| Node silent beyond the timeout | Primary, at 100 ms default | Declare the node absent; report A-NOD-03; apply the policy of `<TBD-S37>` to that channel; **do not disturb the other seven** | — | Node reappears; the Primary re-enumerates (B-NOD-01) | `SRS-PRI-CM-006`, `-007`, `-P3-008`; CS-20 |
| Primary silent beyond the timeout | Secondary, at 100 ms default | Enter the safe state per `<TBD-S37>`; retain accumulators and the reason; keep enforcing every protective limit | — | **No automatic resumption**; an explicit command is required | `SRS-SEC-S10-001`…`-008`; CS-19, CS-38 |
| Message integrity failure | Recipient | Discard, count; **do not treat as loss of communication** | none | Sender retries | CS-13, CS-15 |
| Control data does not arrive | Secondary | Re-request every 100 ms, 5 times; then enter the safe state and report a fault | Primary supplies it or observes the fault | Explicit restart | `SRS-SEC-S4-024`, `-025`; CS-18 |
| Control data rejected | Primary, on B-DAT-03 negative acknowledgement | Do not command a start; report onward | Secondary stays in the safe state | Primary corrects the setpoint or limit | `SRS-SEC-S4-006`, `-020` |
| CAN controller bus-off | Node | Recover without a board reset; report each occurrence | Sees the node as silent meanwhile | Automatic | `SRS-SEC-CM-011` |
| Two nodes claim one identity | Primary | Report A-NOD-04; refuse to command either | — | Manual; the identity source is `<TBD-S15>` | `SRS-PRI-P3-005` |
| Node registration buffer overruns | Node | Report B-TLM-06 with the count and time range discarded; **never discard silently** | Primary reports onward via A-LOG-04 | Reduce the rate (B-TLM-08) or raise the bus capacity | `SRS-SEC-S12-020`, `-022` |
| Bus saturated | Both | Apply the priority ordering of `<TBD-I26>`; shed real-time before registration | — | Rate reconfiguration | CS-52, CS-53, §7.5 |
| Node fault while regulating | Node, locally | Enter the safe state **without waiting for the Primary**, even with the link down; then report | Primary reports onward; applies escalation policy | Fault clear (B-CTL-07) once the condition has cleared | `SRS-SEC-S9-028`, `-029`; CS-30 |
| Node loses supply | Node | Reference output to zero; persist context; report best-effort (B-PFA-01) | Primary sees the node go silent | B-PFA-02 then B-PFA-03, on explicit command | `SRS-SEC-S19-*`; CS-38 |
| Firmware update interrupted | Node | Do not activate a partial image; remain updatable | Primary retries or abandons | Repeat the update | `SRS-SEC-S16-011`, `-012` |
| Node's software version incompatible | Primary, at enumeration | Refuse to run a program on that channel; raise a fault | — | Firmware update | `SRS-PRI-P3-012` |

### 8.3 The one failure mode with no detector — logged data

Called out separately because it is the only place in this document where a failure is
currently **undetectable**, and because it concerns the system's permanent record.

The legacy logged-data path is **UDP on port 10001, fire-and-forget, with no
acknowledgement** (`WAD/ICD` §6). A dropped datagram loses recorded measurement rows and
nobody finds out: the hardware believes it sent them, the server never received them, and
the gap in the session database is indistinguishable from a period in which nothing was
recorded.

That is tolerable for a live view. It is not tolerable for the record of a test that may
have run for days, and it becomes eight times more likely when eight channels share the
path.

| ID | Requirement | Source |
|---|---|---|
| CS-57 | Logged registration data shall be acknowledged, and the Primary Board shall retain unacknowledged data until it is acknowledged. | A-LOG-01, A-LOG-02; **NEW — ME** |
| CS-58 | The Primary Board shall be able to retransmit logged data from the last acknowledged record. | A-LOG-03 |
| CS-59 | A gap in the logged record shall be detectable by the Web Application, and shall be distinguishable from a period in which nothing was recorded. | **NEW — ME** |
| CS-60 | The Primary Board shall report the count of logged records it has discarded, if it discards any. | A-LOG-04; `SRS-SEC-S12-022` |
| CS-61 | `<TBD-I27>` — whether the acknowledged path of CS-57 is adopted, or whether the fire-and-forget path is accepted with its data-loss consequence stated. | Open Issue **#11** |
| CS-62 | `<TBD-I28>` — the recovery point objective for logged data: how much recorded data may be lost in the worst case. | Unsourced |

### 8.4 IF-D failures

| Failure | Detected by | Detector's action | Peer's action | Recovery | Source |
|---|---|---|---|---|---|
| Client request malformed or out of range | Primary | Modbus exception response; no state change | Client's concern | Client corrects | Modbus specification |
| Client requests a register for an absent channel | Primary | Return an explicitly invalid value, not zero | Client must recognise it | — | CS-46 |
| Client attempts a write where write access is not permitted | Primary | Modbus exception response | — | — | `<TBD-I21>` |
| Client link lost | Primary | No action; Modbus is stateless and the client is not a controlling actor | — | Client reconnects | Modbus specification |
| Client writes a control action | Primary | **`<TBD-I21>`** — if permitted, the write is a safety-relevant actor and must be subject to the same state validation as A-CTL | — | — | Open Issue **#29** |

---

## 9. Open issues and pending decisions

### 9.1 Reserved decisions

| ID | Decision | What this document does about it |
|---|---|---|
| **D-03** | Content of the minimum necessary control data | §5.5 specifies what must move and what must happen, never a layout. §5.2 states what is not carried forward. §7.4 quantifies the latency cost of each possible split. `PSPE` §4.4 is recorded as prior art and not adopted. |
| **D-04** | CAN layer details: 2.0B or FD, bit rate, addressing, higher-layer protocol, framing | Every IF-B row's two reserved columns. §7.3 shows that the registration-rate decision and the CAN-FD decision are the same decision. §9.4 is the worksheet. |
| **D-05** | Web App interface details: transport assignment, ports, framing, serialization | Every IF-A and IF-D row's two reserved columns. Legacy port assignments are recorded as evidence in §2.2 and are not adopted. |
| **D-01, D-02, D-06** | Primary-internal core allocation, CAN ownership, inter-core mechanism | **No bearing on this document.** An external interface is unaffected by which core services it. D-06 may constrain `<TBD-I24>`. |

### 9.2 Conflicts bearing on the interfaces

| ID | Conflict | Bearing here |
|---|---|---|
| **C-05** | Registration interval: five candidate values | **§7.3 narrows it to two by arithmetic** and recommends striking the ≤500 µs figure. `<TBD-I22>` |
| **C-06** | Secondary bootloader required, not required, or required over CAN | §5.9 exists conditionally. `<TBD-S51>` in the Secondary SRS |
| **C-10** | CAN bit rate 250 kbps (IF-B) versus 125 k–1 M (legacy external CAN) | **Different buses**; only the 250 kbps figure applies here. The external-CAN figures are out of scope (§2.4) |
| **C-14** | Four incompatible fault code spaces | CS-33, CS-34, `<TBD-I06>`. The code space crosses both interfaces, so it cannot be resolved on one board alone. Widened by **C-19** and **C-20** in `ME-SRS-SEC-001` §8.2 |
| **C-16** | External CAN port count | Resolved into scope exclusion; §2.4 |
| **C-17** | RS-485 hosting | §6.1 — Modbus is Primary-hosted and needs external transceivers |
| **C-19** | Open-battery detection has no error code | Bears on CS-34 and the fault sets of A-EVT-01 and B-TLM-05 |

### 9.3 TBD register

| Tag | Value required | Raised by | Descends from |
|---|---|---|---|
| `<TBD-I01>` | Whether IF-B supports a broadcast address, and which messages may use it | CS-05, CS-06 | **D-04** |
| `<TBD-I02>` | Request-response correlation mechanism | CS-10, CS-11 | **D-04**, **D-05** |
| `<TBD-I03>` | Integrity mechanism per interface | CS-16 | **D-04**, **D-05** |
| `<TBD-I04>` | Staleness threshold beyond which telemetry is invalid rather than old | CS-24 | Unsourced |
| `<TBD-I05>` | IF-B segmentation and flow-control mechanism | CS-29 | **D-04** |
| `<TBD-I06>` | The unified fault code space | CS-34 | **C-14**, **C-19**, **C-20** |
| `<TBD-I07>` | Sign convention for directional quantities | CS-42 | `WAD/ICD` §5.2 vs `LSRS` SW_REQ_141 |
| `<TBD-I08>` | Floating point or scaled integers | CS-43 | **D-04**, **D-05** |
| `<TBD-I09>` | Discovery request timeout | A-DSC-01, -03, -04 | Unsourced |
| `<TBD-I10>` | IF-A command-response timeout | most IF-A rows | Unsourced |
| `<TBD-I11>` | IF-A keep-alive interval and session-loss timeout | A-REG-04 | `CODE-P` 300 000 ms is a reconnection budget, not this |
| `<TBD-I12>` | Overall timeout for a segmented transfer | A-PRG-04, A-PRG-07, A-NOD-07, A-NOD-10, B-FWU-02 | CS-28 |
| `<TBD-I13>` | IF-A live telemetry interval, per channel and aggregate | A-TLM-01, -02, -03 | Open Issue **#18** |
| `<TBD-I14>` | Bulk data-transfer timeout on IF-A | A-LOG-01, -02, -03, A-EVT-05 | Unsourced |
| `<TBD-I15>` | Which calibration query numbering is correct — `CODE-P` includes temperature at `0x0F`–`0x11`, `WAD/ICD` does not | §4.8 note | `CODE-P` vs `WAD/ICD` §3.8 |
| `<TBD-I16>` | IF-B command-response timeout | most IF-B rows | Unsourced |
| `<TBD-I17>` | IF-B heartbeat interval and presence timeout | B-NOD-03, -04; §7.2 | HW *Configuration* #9 gives 100 ms for silence, not for heartbeat |
| `<TBD-I18>` | Firmware-update per-segment timeout | all B-FWU rows | Open Issue **#9** |
| `<TBD-I19>` | The complete Modbus register map | CS-47; §6.2 | Unsourced |
| `<TBD-I20>` | Modbus transport, slave address range, bit rate, parity, stop bits, response timeout | CS-48; all D-MB rows | `LSRS` SW_REQ_26, 30, 31, 34 (all `XXX`) |
| `<TBD-I21>` | Whether Modbus write access is permitted, and to which items | CS-49; §6.2; §8.4 | Open Issue **#29**; `<TBD-44>` in `ME-SRS-PRI-001` |
| `<TBD-I22>` | Registration interval **and** the classic-CAN-versus-CAN-FD decision, which §7.3 shows are one decision | §7.3 | **C-05** + **D-04** |
| `<TBD-I23>` | Assumed average and minimum duration of a unit of control data | CS-55; §7.2 | BM §12.4.8.9 gives 0.1 s as the minimum step |
| `<TBD-I24>` | Maximum permitted IF-B bus utilisation, and the Primary's decision cycle | CS-50; §7.4 | T-08 is an engineering default, not a requirement |
| `<TBD-I25>` | Whether registration may be dropped under load or must throttle | CS-52 | Unsourced |
| `<TBD-I26>` | Priority ordering of IF-B message classes | CS-54 | **D-04** |
| `<TBD-I27>` | Whether logged data is acknowledged, or fire-and-forget is accepted | CS-61; §8.3 | Open Issue **#11** |
| `<TBD-I28>` | Recovery point objective for logged data | CS-62; §8.3 | Unsourced |

### 9.4 D-04 and D-05 worksheet

The two reserved columns of every table in §4, §5 and §6, gathered so they can be filled
in one pass. Fill this, and the encoding columns follow mechanically.

**D-05 — IF-A and IF-D**

| Question | Answer |
|---|---|
| Which transport carries command and response? | |
| Which transport carries live telemetry? | |
| Which transport carries logged data? | |
| Which transport carries discovery? | |
| Port assignment for each | |
| Framing: length-prefixed, delimited, or transport-framed | |
| Serialization: binary layout, or a self-describing format | |
| Integrity mechanism (`<TBD-I03>`) | |
| Correlation mechanism (`<TBD-I02>`) | |
| Numeric representation (`<TBD-I08>`) | |
| Is logged data acknowledged? (`<TBD-I27>`) | |
| Modbus transport and parameters (`<TBD-I20>`) | |

**D-04 — IF-B**

| Question | Answer |
|---|---|
| CAN 2.0B or CAN-FD? (`<TBD-I22>`, and see §7.3) | |
| Bit rate, and data-phase bit rate if CAN-FD | |
| Identifier length: 11-bit or 29-bit | |
| Node addressing: where in the identifier, or in the payload | |
| Node identity source (`<TBD-S15>`) | |
| Identifier allocation, which fixes message priority (`<TBD-I26>`) | |
| Is there a broadcast address? (`<TBD-I01>`) | |
| Higher-layer protocol: bespoke, CAN-TP, CANopen, J1939 | |
| Segmentation and flow control (`<TBD-I05>`) | |
| Application-layer integrity beyond the CAN frame CRC (`<TBD-I03>`) | |
| Control-data content (**D-03**, `<TBD-S18>`) | |
| Does the Secondary evaluate cut-off conditions? (**D-03**, and see §7.4) | |
| Registration interval (`<TBD-I22>`) | |
| Bus utilisation ceiling (`<TBD-I24>`) | |

---

## 10. Deviations from the gate document

Recorded so the reviewer can see where this document departs from the outline in
`GATE` §3.

| # | Deviation | Reason |
|---|---|---|
| 1 | **The Modbus interface is `IF-D`, not `IF-C`.** `IF-C` is reserved and unused. | `GATE` §3 proposed `IF-C` for "external CAN / RS485 *if in scope*". The resolution of **#29** split that: external CAN out, Modbus in. `ME-SRS-PRI-001` §2.2 already names Modbus `IF-D`; this document follows the SRS rather than the superseded outline. §2.1 |
| 2 | **§6 is the IF-D message set, so the timing budget is §7, the failure matrix §8, and the registers §9.** `GATE` §3 numbered timing 6, failure 7, registers 8. | IF-D needed a message set of its own once #29 was resolved in its favour. Section content is unchanged; only the numbers shift by one. |
| 3 | **Message count exceeds the `GATE` §3 estimate** of ~45 IF-A, ~35 IF-B, ~10 new. | Chiefly because the 23 calibration commands are enumerated individually on both interfaces rather than grouped. Each needs its own row to receive its own encoding under D-04 and D-05, which is the document's purpose. |
| 4 | **§7 goes beyond a rate budget and evaluates conflict C-05 arithmetically.** | The budget could not be written without computing what each candidate rate costs, and the computation excludes three of the five candidates outright. Leaving that unsaid would have been withholding the most useful thing in the section. The conclusion is offered, not imposed: §7.3 states the assumptions and says explicitly that it does not close C-05. |
| 5 | **§8.3 raises a failure mode not in the gate outline** — logged data is unacknowledged and its loss is undetectable. | Found while mapping `WAD/ICD` §6 against the eight-channel topology. It concerns the permanent record of a multi-day test, so it is called out rather than left in a table row. |
| 6 | **§5.2 is a table of what is *not* carried forward.** | The step-transfer messages are the largest single change in IF-B. Recording their disposition explicitly is more auditable than their silent absence. |
| 7 | **`<TBD-Inn>` is a third TBD namespace**, alongside the Primary's `<TBD-nn>` and the Secondary's `<TBD-Snn>`. | The three registers ask different questions. Cross-references between them are explicit in §9.3. |

---

*End of ME-ICD-001 v0.1. IF-A: 90 messages · IF-B: 76 messages ·
IF-D: 7 messages · total 173. Common semantics: 62 rules.
Reserved: D-03, D-04, D-05.*
