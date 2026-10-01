# ME Primary Board — TCP/UDP Communication & Device Registration (Demo)

> Design spec | 2026-08-07 | Status: **Approved pending review**
> Supersedes nothing. First module of the ME Primary Board application.

---

## 1. Purpose and scope

Demonstrate that the ME Primary Board (Toradex Verdin iMX8M Plus, Torizon OS)
can establish TCP/IP communication with the Web Application and complete a
device registration handshake.

**In scope:**

- System initialization creates three sockets: TCP `9999`, UDP `10000`, UDP `10001`
- One communication thread, created at startup, owning all three sockets
- Board acts as **TCP client**; Web Application is the **TCP server**
- Send the 33-byte `0xDD` device registration frame
- Receive and validate the 7-byte registration response
- Print a clear console confirmation when `Value == 0x01`

**Explicitly out of scope** (later milestones): battery testing programs,
calibration, live data streaming, session store traffic, device discovery
(UDP 10002/10003), Modbus (IF-D), CAN to secondary boards (IF-B).

UDP sockets 10000 and 10001 are **created and held open only**. No datagram is
sent on them in this demo. In the legacy protocol these are hardware→server
one-way streams, so no inbound traffic is expected either. They are still
included in the thread's `poll()` set so that all three sockets are owned by the
one communication thread as required; if a datagram ever does arrive it is
logged and discarded rather than parsed.

### 1.1 Success criteria

1. `build.ps1` produces a static aarch64 ELF (`Class: ELF64, Machine: AArch64`).
2. `build-native.ps1` builds and runs the host unit tests; all pass.
3. Deployed to the board, the program connects to the Web Application, sends
   the registration frame, receives `Value == 0x01`, and prints
   `DEVICE REGISTERED`.

Criterion 3 must be verified by the developer against real hardware and a real
Web Application. There is no network path from the build laptop to the board,
and aarch64 binaries cannot be executed on the Windows host at all
(see project `CLAUDE.md`).

---

## 2. Source of truth for the protocol

The byte layout below is **authoritative for this project** and was supplied
directly by the developer from `bm_device_registration_v5.0` (legacy BTS
project document, not present in this workspace).

`ME Workspace/Reference Documents/WebAppDocs/ICD.md` §3.4 documents a similar
frame and agrees on the request layout, but **the ICD is wrong about the
response**: it describes a 5-byte response with no CRC. The real response is
**7 bytes and does carry a CRC**. Any future module built from the ICD should
treat §3.4's response table as suspect.

The ICD is also internally inconsistent about field order: §3.2 shows a generic
header of `Start │ DeviceID │ CircuitID │ QueryID`, while §3.4 shows
`Start │ SubCmd │ Reserved │ DeviceID │ CircuitID`. **§3.4 is correct.** This is
proven by the length byte (§3.1 below), not merely assumed.

### 2.1 Why the length byte settles the field order

Byte 2 is the payload length, counted from DeviceID through the end of the MAC
address. That span is:

```
DeviceID(1) + CircuitID(1) + DeviceName(16) + IPAddress(4) + MACAddress(6) = 28 = 0x1C
```

And the total frame is `3 (header) + 28 (payload) + 2 (CRC) = 33 bytes`, which
matches the known frame size. This is only self-consistent if DeviceID begins
at offset 3. Under §3.2's ordering there would be no length field at all.

**Implementation rule:** the length constant is *computed* from the field sizes,
never hardcoded as `0x1C`, so it cannot drift if a field size changes.

---

## 3. Frame definitions

### 3.1 Registration request — 33 bytes (Board → Web Application)

| Offset | Size | Field | Demo value | Notes |
|---|---|---|---|---|
| 0 | 1 | Start | `0xDD` | Registration command group |
| 1 | 1 | QueryID | `0x01` | Register |
| 2 | 1 | Length | `0x1C` (28) | DeviceID..MAC inclusive; computed |
| 3 | 1 | DeviceID | `0x01` | Device number |
| 4 | 1 | CircuitID | `0x11` | Secondary 1, Channel 1 — see §3.3 |
| 5 | 16 | DeviceName | `"BTS-600"` | ASCII, null-padded to 16 |
| 21 | 4 | IPAddress | `C0 A8 00 0E` | Dotted order, 4 octets |
| 25 | 6 | MACAddress | from `eth0` | 6 octets, wire order |
| 31 | 2 | CRC-16/Modbus | — | **Big Endian (CRC_HI CRC_LO)**, over bytes 0–30 |

**IP byte order:** the four octets are written in dotted-quad order
(`192.168.0.14` → `C0 A8 00 0E`), *not* as a little-endian `uint32`. A wrong
choice here still produces a valid 33-byte frame with a valid CRC and fails
only at the server, so it is called out explicitly.

### 3.2 Registration response — 7 bytes (Web Application → Board)

| Offset | Size | Field | Meaning |
|---|---|---|---|
| 0 | 1 | Start | `0xDD` |
| 1 | 1 | QueryID | `0x01` |
| 2 | 1 | DeviceID | Echo of the value sent |
| 3 | 1 | CircuitID | Echo of the value sent |
| 4 | 1 | **Value** | `0x01` = **Registered**, `0x02` = **Already Registered** — both are success |
| 5 | 2 | CRC-16 | **Big Endian (CRC_HI CRC_LO)**, over bytes 0–4 |

> ⚠️ **AMENDED 2026-08-10 — see
> `Docs/specs/2026-08-10-registration-idle-state-design.md`.** The paragraph
> below was wrong and caused a live defect: the server answered `0x02`
> (Already Registered), the board read that as a rejection, and it
> re-registered every 30 seconds forever. **`0x01` and `0x02` are both
> success.** Only `0x00` and unrecognised values trigger a retry.

**Registration is gated solely on `Value == 0x01`.** Any other value is treated
as not-registered and triggers a retry.

**Echo fields are informational.** The developer's reference response shows
CircuitID `0x01` while this device sends `0x11`. A mismatch between the echoed
DeviceID/CircuitID and what was sent logs a warning but does **not** block
registration.

### 3.3 CircuitID encoding

The legacy per-device circuit number is redefined for ME:

```
 bit 7 6 5 4   3 2 1 0
    ┌───────┬─────────┐
    │  Sec  │ Channel │      0x11 → Secondary 1, Channel 1
    └───────┴─────────┘
    upper nibble = Secondary (DC-DC board) ID
    lower nibble = Channel ID on that Secondary
```

Encoded once, via a macro:

```c
#define ME_CIRCUIT_ID(sec, ch)  ((uint8_t)((((sec) & 0x0Fu) << 4) | ((ch) & 0x0Fu)))
```

Decode helpers `ME_CIRCUIT_SECONDARY(id)` and `ME_CIRCUIT_CHANNEL(id)` accompany it.

### 3.4 CRC-16/Modbus

Polynomial `0xA001` (reflected), init `0xFFFF`, no final XOR. Covers every byte
of the frame except the two CRC bytes themselves. Reference vector:
`"123456789"` → `0x4B37`.

### 3.5 CRC byte order is ambiguous — resolved as a runtime parameter

The two source documents disagree, and this was only discovered after the design
was first drafted:

- `WebAppDocs/ICD.md` §3.2 — "appended **Little Endian**"
- `Docs/bm_device_registration_v5.0.md` line 26 — trailer written as
  **`CRC_HI CRC_LO`**, i.e. big-endian

No arithmetic settles it; the payload-detail sheet lives in a source Excel file
not present in the workspace. Either order produces a well-formed 33-byte frame
with a valid-looking trailer, so a wrong choice fails only inside the server's
checksum test.

**Resolution (amended 2026-08-10):** byte order is a runtime parameter
(`me_crc_order_t`) defaulting to **big-endian — high byte first**. A CRC of
`0xADBE` is transmitted as `AD BE`. This follows
`Docs/bm_device_registration_v5.0.md`, which §2 of this document already
established as authoritative over the ICD, and matches the developer's explicit
direction. The default lives in exactly one place,
`ME_CRC_ORDER_DEFAULT` in `src/proto/crc16.h`; `sys_init.c`, the `--crc-order`
help text and `deploy.ps1` all resolve through it.

Overridable with `--crc-order le`. On a response CRC failure the program prints
the computed CRC alongside the value read both ways and names the order that
would have matched. See **ADR-9**, which supersedes ADR-5.

> The original resolution defaulted to little-endian and was recorded as
> hardware-confirmed on 2026-08-07. That confirmation was only sound for the
> **response** direction; a successful registration says nothing about the
> request direction unless the server actually validates the request CRC.

---

## 4. Architecture

### 4.1 Module structure

```
me-primary/
├── src/
│   ├── main.c                  entry: init → spawn thread → wait → shutdown
│   ├── sys_init.[ch]           system init: creates all three sockets, reads IP/MAC
│   ├── comm_thread.[ch]        the communication thread: state machine + poll()
│   ├── proto/
│   │   ├── proto_defs.h        ports, start bytes, offsets, sizes, CircuitID macros
│   │   ├── crc16.[ch]          CRC-16/Modbus
│   │   └── reg_frame.[ch]      pack request / parse response — pure, no I/O
│   ├── net/
│   │   ├── tcp_client.[ch]     connect w/ backoff, full-send, timed-recv
│   │   └── udp_sock.[ch]       UDP socket creation
│   ├── platform/
│   │   └── netinfo.[ch]        getifaddrs() → interface IPv4 + MAC
│   └── util/
│       └── log.[ch]            timestamped logging + hex dump
├── tests/
│   ├── test_crc16.c
│   ├── test_reg_frame.c
│   └── test_main.c             minimal assert-based runner
├── build.ps1                   aarch64 static cross-build
├── build-native.ps1            host build + unit tests
├── deploy.ps1                  scp + docker run --network host
└── README.md
```

**Design rationale.** `proto/` is pure logic with no socket dependency, so the
entire byte layout is unit-testable on the Windows host via the existing MinGW
toolchain. This matters because the board round-trip is the slow, manual part of
the loop — offset and CRC errors should be caught in seconds on the laptop, not
minutes later against hardware.

`proto_defs.h` is the single source of truth for every offset, size, port and
constant. No magic numbers appear anywhere else.

All files are far below the 2000-line limit; none is expected to exceed 300.

### 4.2 Threading model

> ⚠️ **State names amended 2026-08-10.** `REGISTERED` and `MONITORING` were
> never used in code and are now a single `ME_COMM_IDLE`. The diagram below
> otherwise still holds. See
> `Docs/specs/2026-08-10-registration-idle-state-design.md` §3.3.

One thread, as required. Three sockets multiplexed with `poll()`.

```
main thread                          communication thread
───────────                          ────────────────────
sys_init()
  ├ read IP + MAC (eth0)
  ├ create TCP socket   :9999
  ├ create UDP socket   :10000
  └ create UDP socket   :10001
pthread_create ────────────────────► CONNECTING
install SIGINT/SIGTERM handler         │  connect() to Web App :9999
pthread_join                           ▼
  │                                  REGISTERING
  │                                    │  send 33-byte 0xDD frame
  │                                    │  recv 7-byte response
  │                                    ▼
  │                                  REGISTERED   ── print DEVICE REGISTERED
  │                                    │
  │                                    ▼
  │                                  MONITORING
  │                                    │  poll() on TCP + UDP×2
  ▼                                    │  until shutdown or disconnect
close all sockets ◄────────────────────┘
                        any failure ──► back to CONNECTING (capped backoff)
```

`poll()` rather than three threads or blocking reads: a single thread was
specified, and `poll()` with a timeout is what lets the shutdown flag be
observed promptly instead of being stuck inside a blocking `recv()`.

### 4.3 Runtime configuration

The Web Application's address is not known at compile time, so it is passed as a
command-line argument. Everything else defaults to the demo values in §3.1 and
is overridable for later use.

| Argument | Default | Meaning |
|---|---|---|
| `--server <ip>` | *(required)* | Web Application IPv4 address |
| `--port <n>` | `9999` | TCP command port |
| `--iface <name>` | `eth0` | Interface to read IP and MAC from |
| `--device <n>` | `1` | DeviceID |
| `--secondary <n>` | `1` | Upper nibble of CircuitID |
| `--channel <n>` | `1` | Lower nibble of CircuitID |
| `--name <str>` | `BTS-600` | DeviceName, truncated to 16 bytes |
| `--crc-order <le\|be>` | `be` | CRC byte order — see §3.5 |
| `--timeout <ms>` | `5000` | Response timeout |
| `--verbose` | off | Debug-level logging |

Invalid or missing arguments print usage and exit non-zero. Secondary and
channel are not range-checked: the developer confirmed neither exceeds 15, and
`ME_CIRCUIT_ID` masks each to 4 bits regardless.

`deploy.ps1` passes these through as `-ServerIP`, `-CrcOrder`, `-Timeout`,
`-DebugLog`.

---

## 5. Error handling

| Condition | Behaviour |
|---|---|
| `connect()` fails | Retry with capped exponential backoff (1s → 30s). Never exit — the Web App may simply not be up yet. |
| Partial `send()` | Loop until all 33 bytes are written or the socket errors. |
| `recv()` timeout | Bounded wait; on expiry, log and return to CONNECTING. |
| Response shorter/longer than 7 bytes | Reject, hex-dump what was received, retry. |
| Start byte ≠ `0xDD` or QueryID ≠ `0x01` | Reject, hex-dump, retry. |
| **Response CRC mismatch** | **Reject and retry.** Log the received bytes plus expected-vs-received CRC so a convention mismatch is immediately visible. |
| DeviceID/CircuitID echo mismatch | Warn only. Does not block registration. |
| `Value` is `0x01` or `0x02` | **Registered.** Print the banner, go to IDLE, hold the connection. *(Amended 2026-08-10 — `0x02` was previously mis-classified as a retry.)* |
| `Value` is `0x00` or unrecognised | Not registered. Log the value, retry. |
| Peer disconnect after registration | Return to CONNECTING and re-register. |
| `SIGINT` / `SIGTERM` | Set shutdown flag, unblock `poll()`, close all sockets, join, exit 0. |

`SIGTERM` handling is not optional: `docker stop` sends it, and an unhandled
`SIGTERM` in PID 1 of a container means an ungraceful kill after the timeout.

### 5.1 Diagnostics

Because verification happens against the real Web Application with no decoder on
the board side, a rejected frame must be diagnosable from the console alone.
Therefore the program hex-dumps **the exact 33 bytes sent** and **every byte
received**, with offsets and field annotations. This is the substitute for a
mock server.

---

## 6. Testing

Host-native unit tests (MinGW, via `build-native.ps1`) covering the pure
protocol logic. These require no board and no network.

**`test_crc16.c`**
- Reference vector `"123456789"` → `0x4B37`
- Empty input → `0xFFFF`
- Single byte, known value
- Byte order: both orders append and verify correctly, and each rejects a frame
  built in the other
- The **default** order is big-endian — asserted directly on
  `ME_CRC_ORDER_DEFAULT`, so the whole program's wire behaviour is pinned by one
  check

**`test_reg_frame.c` — request packing**
- Total length is exactly 33
- Every field offset asserted individually (0,1,2,3,4,5,21,25,31) so a shifted
  field identifies itself by name rather than as one opaque buffer diff
- Length byte equals 28
- `"BTS-600"` occupies bytes 5–11 with bytes 12–20 zero
- Name exactly 16 chars: no truncation, no null terminator written past the field
- Name longer than 16: truncated to 16, no overflow
- IP `192.168.0.14` → `C0 A8 00 0E` in that order
- CircuitID: `ME_CIRCUIT_ID(1,1) == 0x11`; `ME_CIRCUIT_ID(15,15) == 0xFF`;
  nibble decode round-trips
- CRC verifies against an independently computed value over bytes 0–30
- **Golden vector:** the exact 33-byte frame captured from hardware
  (`BTS-600`, device 1 / secondary 1 / channel 1, `172.16.18.238`,
  MAC `00:14:2d:ef:86:e2`) is asserted byte-for-byte and must end `AD BE`.
  This is the regression guard for the CRC order and every other field at once.

**`test_reg_frame.c` — response parsing**
- Valid 7-byte response, `Value == 0x01` → registered
- `Value` of `0x00` / `0x02` / `0xFF` → not registered
- Wrong start byte → rejected
- Wrong QueryID → rejected
- 6-byte and 8-byte inputs → rejected
- Bad CRC → rejected (per §5)
- Echo mismatch → accepted, warning flagged

**Argument validation**
- `--secondary` / `--channel` above 15 or negative → rejected with usage
- Missing `--server` → rejected with usage

Hardware-in-the-loop is required for the socket layer; the unit tests
deliberately do not attempt to cover it.

---

## 7. Build and deployment changes

- `build.ps1` — extended to compile multiple translation units; still resolves
  `aarch64-none-linux-gnu-gcc` from `.embedded-override.json`, still `-static`
  (see project ADR on static linking), still verifies the ELF header.
- `build-native.ps1` — builds the host test binary and runs it; non-zero exit
  on any failing assertion.
- `deploy.ps1` — **adds `--network host`** to the `docker run` invocation, and
  gains a `-ServerIP` parameter for the Web Application address.

### 7.1 Why `--network host` is mandatory, not cosmetic

The registration frame carries the board's IP and MAC **as payload**. On
Docker's default bridge network, `getifaddrs()` inside the container returns the
container's virtual interface — typically `172.17.0.x` with a synthetic MAC —
not the Verdin's real address. The frame would transmit successfully with a
valid CRC and register the board under an address the Web Application can never
route back to.

`--network host` also gives the container the real ports 9999/10000/10001 that
the protocol assumes. This is the first module where the container's network
configuration is part of the protocol's correctness, not merely a runtime detail.

---

## 8. Documentation deliverable

`Docs/ME_Primary_Comm_Block_Diagram.md` — functional/workflow diagram aimed at
any embedded developer, containing:

1. System context: Board (TCP client) ↔ Web Application (TCP server), all three ports
2. Module block diagram matching §4.1
3. Startup sequence: power-on → sys_init → thread creation → registration
4. Communication thread state machine (§4.2)
5. Annotated byte-layout diagrams of both frames
6. Registration handshake sequence diagram

Rendered as ASCII plus Mermaid, so it is readable in a plain editor without
tooling.

---

## 9. Renaming

`hello-world-bringup/` → `me-primary/`. The bring-up milestone (T-1) is
complete and verified on hardware; this directory becomes the ME Primary Board
application, of which registration is the first module. The stale
`hello-world-bringup.tar` at the project root is a build artifact from that
milestone and is removed.

`CLAUDE.md`, `.vscode/tasks.json` and `README.md` are updated to the new paths.

---

## 10. Open items

| # | Item | Disposition |
|---|---|---|
| 1 | `bm_device_registration_v5.0` is not in the workspace | **Resolved.** The developer added it at `Docs/bm_device_registration_v5.0.md`. It confirms `LEN` semantics, the 7-byte response, and the `0x01` = Success value. |
| 2 | ICD §3.4 documents the response as 5 bytes without CRC | Confirmed incorrect against the source document. **Closed — the developer decided to leave the ICD as it is.** `bm_device_registration_v5.0.md` is authoritative; check it first for any future command group. |
| 3 | Web Application's exact CRC span on the response | Assumed bytes 0–4. If the server disagrees, §5's rejection dump shows it on the first attempt. |
| 4 | **CRC byte order** — ICD says little-endian, the source document writes `CRC_HI CRC_LO` | **RESOLVED 2026-08-10: BIG-endian, high byte first** (`0xADBE` → `AD BE`), per developer directive and per `bm_device_registration_v5.0`, which §2 already made authoritative. Supersedes the 2026-08-07 little-endian reading, whose hardware "confirmation" was only sound for the response direction. Single source of truth: `ME_CRC_ORDER_DEFAULT`. `--crc-order` kept as a diagnostic. See **ADR-9**. ⚠️ Awaiting a hardware re-run. |
| 5 | The registration payload's own field-size detail sheet | Still only in the source Excel. The 16/4/6 sizes come from ICD §3.4 and are corroborated by `LEN = 28`. Tracked as **T-9**. |

---

## 11. Implementation status

Implemented in session #2 on 2026-08-07. Deviations from the design as written:

- **§4.3** gained `--crc-order`, `--timeout` and `--verbose`; the secondary and
  channel range checks were dropped at the developer's direction (the values
  never exceed 15, and `ME_CIRCUIT_ID` masks to 4 bits regardless).
- **§6** gained a third test group, `test_log.c`, covering hex-dump formatting —
  the dump is the sole field-debugging tool, so its correctness matters.
- Total: **46 assertions**, all passing, mutation-tested to confirm a reversed
  IP field is caught.
- Cross-build verified: `ELF64 / AArch64 / EXEC`, no dynamic section.
- `-std=gnu11` was required rather than `-std=c11`: `struct ifreq`, `IFNAMSIZ`
  and `IFF_UP` sit behind `_DEFAULT_SOURCE`, which strict ISO mode disables.

### Verification — all three success criteria met

| §1.1 criterion | Status |
|---|---|
| 1. Static aarch64 ELF | ✅ `ELF64 / AArch64 / EXEC`, no dynamic section |
| 2. Host unit tests pass | ✅ 46 assertions, 0 failures |
| 3. **`DEVICE REGISTERED` on hardware** | ✅ **Verified 2026-08-07 against the real Web Application** |

Known-good invocation:

```bash
./me_primary --server 172.16.14.244 --iface ethernet0 --verbose
```

Notes from the hardware run:

- Torizon names the NIC **`ethernet0`** (not `eth0`, not `end0`). The interface
  auto-detect fallback handled it; passing `--iface ethernet0` skips the warning.
- The board's MAC is `00:14:2d:...` — Toradex's OUI — confirming host
  networking gave the program the Verdin's real identity rather than a
  container's `02:42:...` bridge endpoint. §7.1 held up in practice.
- The one failure encountered was operational, not protocol: the board was
  initially pointed at `172.16.10.21`, a different host on the LAN, which
  answered with an RST. **This design's §5 error handling behaved exactly as
  intended** — it retried with backoff instead of exiting, and the precise
  `Connection refused` errno distinguished "wrong host" from "no network".
