# me-primary

ME Primary Board application for the Toradex Verdin iMX8M Plus (Torizon OS).

**Current milestone:** demonstrate TCP/IP communication with the Web
Application by completing a device registration handshake on TCP port 9999.

The board is the **TCP client**; the Web Application is the **TCP server**.

- Design spec: [`../Docs/specs/2026-08-07-me-primary-registration-design.md`](../Docs/specs/2026-08-07-me-primary-registration-design.md)
- Block diagram: [`../Docs/ME_Primary_Comm_Block_Diagram.md`](../Docs/ME_Primary_Comm_Block_Diagram.md)
- Frame source: [`../Docs/bm_device_registration_v5.0.md`](../Docs/bm_device_registration_v5.0.md)

---

## Quick start

```powershell
# 1. Run the protocol unit tests on this laptop (no board needed)
.\build-native.ps1

# 2. Cross-compile the static aarch64 binary
.\build.ps1

# 3. Deploy to the board and run against the Web Application
.\deploy.ps1 -BoardIP 192.168.0.14 -ServerIP 192.168.0.10
```

Success looks like this on the console:

```
======================================================
  DEVICE REGISTERED
------------------------------------------------------
  Device number   : 1
  Circuit number  : 0x11  (secondary 1, channel 1)
  Device name     : BTS-600
  Server response : 0x01 (Registered)
======================================================
```

---

## What each script does

| Script | Purpose |
|---|---|
| `build-native.ps1` | Builds and runs the **unit tests** for the pure protocol logic using MinGW on Windows. Proves the byte layout and CRC. Proves nothing about aarch64, Torizon, Docker or the network. |
| `build.ps1` | Cross-compiles a **static** aarch64 ELF to `bin\me_primary` and prints the ELF header for verification. |
| `deploy.ps1` | `scp`s the binary to the board and runs it in a container with `--network host`. |

All three are wired into `.vscode/tasks.json`.

---

## deploy.ps1 parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-BoardIP` | *(required)* | Address of the Verdin board |
| `-ServerIP` | *(required)* | Address of the Web Application |
| `-BoardUser` | `torizon` | SSH user on the board |
| `-RemotePath` | `/home/torizon/me_primary` | Where the binary lands |
| `-DockerImage` | `debian:bookworm-slim` | Container image on the board |
| `-Port` | `9999` | TCP command port |
| `-Iface` | `eth0` | Interface to read IP and MAC from |
| `-Device` | `1` | Device number |
| `-Secondary` | `1` | CircuitID upper nibble |
| `-Channel` | `1` | CircuitID lower nibble |
| `-DeviceName` | `BTS-600` | Device name, max 16 chars |
| `-CrcOrder` | `be` | `le` or `be` — see "CRC byte order" below |
| `-Timeout` | `5000` | Response timeout in ms |
| `-DebugLog` | off | Enable debug-level logging |
| `-SkipCopy` | off | Re-run without copying the binary again |

The program itself takes the same options as `--server`, `--port`, `--iface`,
`--device`, `--secondary`, `--channel`, `--name`, `--crc-order`, `--timeout`,
`--verbose`. Run it with `--help` for the full list.

---

## Three things that will bite you

### 1. `--network host` is not optional

The registration frame carries the board's **IP and MAC as payload**. On
Docker's default bridge network the program reads the *container's* virtual
interface (`172.17.x.x`, synthetic MAC) and registers the board at an address
the Web Application can never reach — with a perfectly valid CRC, so nothing
looks wrong.

`deploy.ps1` always passes `--network host`. If you run `docker run` by hand,
pass it yourself. The program warns if it detects a `172.16–31.x.x` address,
but a warning is not a fix.

### 2. CRC goes out high byte first — and the source documents disagree

The CRC trailer is sent **big-endian: high byte first**. A CRC-16/Modbus value
of `0xADBE` appears on the wire as `AD BE`, not `BE AD`:

```
DD 01 1C 01 11 42 54 53 2D 36 30 30 00 00 00 00 00 00 00 00 00
AC 10 12 EE 00 14 2D EF 86 E2 AD BE
                              ^^^^^ CRC 0xADBE, high byte first
```

That frame is pinned byte-for-byte as a golden vector in
`tests/test_reg_frame.c`, so any regression fails on this laptop rather than on
the wire.

The two source documents contradict each other here:

- `bm_device_registration_v5.0` writes the trailer as **`CRC_HI CRC_LO`** (big-endian) — **this is what we send**
- `WebAppDocs/ICD.md` §3.2 says the CRC is appended **little-endian**

Per the project rule, `bm_device_registration_v5.0` wins. Both orders produce a
well-formed frame, so a wrong choice fails only inside the peer's checksum test
— never in parsing. The order remains a runtime switch so a mismatch can be
diagnosed without a rebuild:

```powershell
.\deploy.ps1 -BoardIP <board> -ServerIP <server> -CrcOrder le
```

Note the switch applies to **both directions** — requests sent and responses
validated. If a response fails CRC validation, the program prints the computed
CRC alongside the value read both ways and names the order that *would* have
matched, so one run tells you whether the server disagrees.

> ⚠️ **Deploy the board only after the server-side CRC change is live.**
> Captures on 2026-08-10 showed the Web Application replying **little-endian**
> (`DD 01 01 11 02` + `15 BE`, CRC `0xBE15`). The Web App team is changing it
> to `CRC_HI CRC_LO` to match. Until that lands, this big-endian build will
> reject the server's *response* with `CRC mismatch` — the request is fine.
> `-CrcOrder le` confirms the diagnosis in one run. See ADR-9.

### 3. Response `0x02` means success, not failure

`0x01` (Registered) and `0x02` (Already Registered) are **both** success. The
board registers once per TCP connection and then idles with the connection
held open — it does not re-send the registration frame. On a reconnect it
registers again on the new socket and the server answers `0x02`.

Only `0x00` (Failed) and unrecognised values retry, with the usual 1s→30s
capped backoff.

A healthy run looks like this:

```
state: CONNECTING to 172.16.14.244:9999
state: REGISTERING
...
  DEVICE REGISTERED
  Server response : 0x02 (Already Registered)
...
state: IDLE - registered, holding connection, watching TCP and UDP 10000/10001
```

If you see `registration: rejected by server - value 0x02` followed by
`retrying in 30000 ms`, you are running a binary older than 2026-08-10.

---

## Troubleshooting

| Symptom | What to check |
|---|---|
| `scp` fails | SSH reachability and auth: `ssh torizon@<board-ip> echo ok` |
| `docker: command not found` | `ssh torizon@<board-ip> docker version` |
| Image pull fails | Board needs internet, or pre-pull: `docker images` |
| `connect to <ip>:9999 failed` | Web Application not running, wrong IP, or a firewall. The program retries with backoff — it does not exit. |
| `no response within 5000 ms` | Server accepted the connection but did not reply. Check the server logs; the frame we sent is hex-dumped above this line. |
| `response rejected - CRC mismatch` | See "CRC byte order" above. The log states whether big-endian would have matched. |
| `response rejected - wrong length` | Server replied with something other than 7 bytes. The dump shows exactly what arrived. |
| `rejected by server - value 0x00` | Server parsed the frame but declined. Field ordering or content is wrong — compare the request dump against `Docs/ME_Primary_Comm_Block_Diagram.md` §5.1. |
| `netinfo: interface 'eth0' unavailable` | Torizon may name the NIC `end0`. The program auto-detects and logs what it used; pass `-Iface` to force one. |
| IP shows as `172.17.x.x` | `--network host` is missing. |
| `rpmsg: cannot open /dev/ttyRPMSG30` | M7 not running or remoteproc stopped: `cat /sys/class/remoteproc/remoteproc0/state` should read `running`. The CAN manager degrades rather than exiting — registration and Web Application traffic keep working. |
| CAN manager reports `ack fail` at shutdown | M7 rejected a SET frame (bad payload length or CAN ID). See `Docs/specs/2026-08-18-rpmsg-can-transport-design.md` §8. |

**`ME_RPMSG_DEV`** overrides the RPMsg device path (default `/dev/ttyRPMSG30`)
for the CAN manager thread — set it before running the container if the M7's
name-service channel ever binds to a different `/dev/ttyRPMSGxx` node.

---

## Source layout

```
src/
  main.c              entry: arguments, signals, start/join the four threads
  sys_init.c          system init: netinfo + both UDP sockets + destinations
  app_queues.c        the four inter-thread queues + the stop flag
  msg.h/.c            the one message struct crossing every thread boundary
  threads/            ALL FOUR THREADS live here - nothing that runs as a
                      thread belongs outside this directory
    comm_thread.c     owns all 3 sockets: state machine + poll() + routing
    core_logic.c      test state, program assembly, step-1 extraction
    data_mgr.c        the only caller of the store; chunked serving + ring
    can_mgr.c         RPMsg CAN transport: poll() over g_q_can + /dev/ttyRPMSG30
    demo_realtime.c   TEMPORARY 1 Hz / 60 s emitter - delete with real exec
  store/
    circuit_store.c   CircuitID -> slot + per-circuit program/battery/config
  proto/
    proto_defs.h      SINGLE SOURCE OF TRUTH for offsets, sizes, ports, IDs
    crc16.c           CRC-16/Modbus
    reg_frame.c       pack request / parse response   (pure, no I/O)
    program_chain.c   the AA 55 .. 55 AA step-chain walker
    frame_router.c    inbound frame -> which thread owns it, and where it ends
    control_frame.c   0xEE parse
    battery_frame.c   0xAA battery info   (offsets NOT specification-backed)
    realtime_frame.c  86-byte 0xCC build + big-endian float codec
    rpmsg_frame.c     RPMsg header + can_frame_msg_t codec + stream reassembler
  net/
    tcp_client.c      connect with timeout, full-send, timed-recv
    udp_sock.c        UDP socket creation + destinations + send
  platform/
    netinfo.c         this board's own IP and MAC
    rpmsg_link.c      open/write/close on /dev/ttyRPMSGxx, raw mode
  util/
    log.c             timestamped logging + hex dump
    msgq.c            the queue itself: mutex + condvars + optional eventfd
tests/                111 checks, all host-runnable
  test_crc16.c        CRC vectors and byte order
  test_reg_frame.c    every field offset, name padding, response parsing
  test_log.c          hex dump formatting
  test_msgq.c         FIFO, timeouts, drops, producer/consumer
  test_program_chain.c  fetch by step + every malformed chain case
  test_frame_router.c   every start byte + declared-length overrun
  test_control_frame.c  all six commands + each rejection code
  test_battery_frame.c  every offset, field independence, short/long payloads
  test_realtime_frame.c 95.6f -> 42 BF 33 33, all 80 payload offsets, CRC
  test_circuit_store.c  slot mapping + append/reset/capacity rules
```

`proto/` has no socket or platform dependency. That is deliberate: it is what
makes the byte layout testable on a Windows laptop, where an offset error costs
seconds instead of a board round-trip.

---

## Verification status

| Check | How | Status |
|---|---|---|
| Protocol byte layout and CRC | `build-native.ps1` — 46 assertions | Automated |
| aarch64 static ELF | `build.ps1` — ELF header + no dynamic section | Automated |
| Registration against the Web Application | `deploy.ps1` on real hardware | **Developer must run this** |

There is no network path from the build laptop to the board, and aarch64
binaries cannot execute on Windows at all (QEMU user-mode is unavailable there
— see the project ADRs). Only a real deployment proves the last row.
