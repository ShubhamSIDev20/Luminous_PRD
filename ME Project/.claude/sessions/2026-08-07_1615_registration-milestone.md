# Session #2: Registration milestone (2026-08-07 16:15–17:25 IST)
> Date: 2026-08-07T16:15+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: First session recorded in this history (session #1's bring-up work predates the session-file convention — see ADR-1/ADR-2/ADR-3 for its decisions).

> Kept for the reasoning trail. Superseded where it conflicts with session #3 —
> in particular its little-endian CRC conclusion (see ADR-9).

---

## 🎯 Goal That Session
Move from bring-up to the actual ME project: implement the ME Primary Board's
TCP/UDP communication with the Web Application, and demonstrate it by
completing a device registration handshake on TCP 9999.

## ✅ Done That Session
- **T-1 closed** — developer confirmed Hello World ran on the board under Docker
- Renamed `hello-world-bringup/` → **`me-primary/`**
- **Located the frame spec** — `WebAppDocs/ICD.md` §3.4 already documented the
  33-byte `0xDD` frame; developer then supplied `bm_device_registration_v5.0.md`
- Resolved a three-way contradiction between the source documents (ADR-4)
- Wrote the design spec, reviewed and approved:
  `Docs/specs/2026-08-07-me-primary-registration-design.md`
- Implemented **test-first**: CRC-16/Modbus, registration pack/parse, hex dump
  — **46 assertions, all passing**; mutation-tested to confirm they aren't vacuous
- Implemented system init, comm thread (one thread, `poll()` over 3 sockets),
  TCP client, UDP sockets, netinfo, logging
- Cross-build verified: `ELF64 / AArch64 / EXEC`, **no dynamic section** (static)
- Wrote `Docs/ME_Primary_Comm_Block_Diagram.md` — ASCII + Mermaid
- Updated `CLAUDE.md`, `.vscode/tasks.json`, `me-primary/README.md`
- Recorded **ADR-4 … ADR-8**

## 🔬 First hardware run — 2026-08-07 ~18:50 IST
Ran on the board inside the existing `qflex-backend` container
(`NetworkMode=host`, verified). **The board side works end to end; the Web
Application is not listening.**

```
netinfo: interface 'eth0' unavailable, auto-detecting
netinfo: using interface 'ethernet0'
system init: interface ethernet0  ip 172.16.14.209  mac 00:14:2d:ef:86:e2
udp: socket open on port 10000 / 10001
state: CONNECTING to 172.16.10.21:9999
tcp: connect to 172.16.10.21:9999 failed: Connection refused
state: CONNECTING failed, retrying in 1000 ms
```

What this proves:
- **Interface auto-detect earned its keep** — Torizon names the NIC
  **`ethernet0`**, not `eth0` and not `end0`. Without the fallback this would
  have failed at init.
- **The identity is genuine** — MAC `00:14:2d` is **Toradex's OUI**, i.e. the
  Verdin's real hardware address, not a container's (`02:42:...`). Host
  networking is confirmed working and the payload will be correct.
- **`Connection refused` is an RST from the server**, not a board network fault.
  A dead link gives `Network is unreachable` / `No route to host` / a timeout.
  The path works; nothing is bound to 9999 on `172.16.10.21`.
- Backoff retry works as designed — the program stays up and will connect on
  its own once the server starts listening.
- The Docker-bridge warning **is a false positive** on this LAN (board is
  `172.16.14.209`; the check spans `172.16–31`). A narrower MAC-prefix-based
  check was written and then **reverted at the developer's request** — the
  broad warning is intentional. Do not "fix" it again without asking.

## 🏁 T-6 PASSED — registration verified end to end, 2026-08-07 ~19:55 IST

```bash
./me_primary --server 172.16.14.244 --iface ethernet0 --verbose
```

Developer confirmed the device registered against the real Web Application
(`BatteryTestingSystem`, listening `0.0.0.0:9999`). **The milestone is complete.**

Root cause of the earlier failure: **the board was pointed at the wrong host.**
`172.16.10.21` was some other machine on the LAN — it was up, so it answered
with an RST (`Connection refused`), which is why the network path looked broken
when it never was. The Web App runs on the **developer's laptop**, whose Wi-Fi
address is `172.16.14.244` — the same `172.16.0.0/16` as the board's
`172.16.14.209`. Its Ethernet address is `192.168.0.10`, which is *also* the
legacy BTS server address in the discovery example, and was the misleading trail.

### CRC byte order is now CONFIRMED as little-endian (closes T-7)

> ⚠️ **OVERTURNED 2026-08-10 (session #3).** The developer specified high byte
> first, and the code now sends big-endian — see **ADR-9**. The paragraph below
> is session #2's reasoning, kept as the record. Its flaw: the request-direction
> half assumed the server *validates* the request CRC, which a successful
> registration does not establish. Only the response direction was truly proven.

No `--crc-order` was passed, so the LE default was used. Registration could not
have succeeded on a wrong order: the request CRC would have failed the server's
check, and `me_reg_parse_response` **rejects** a response whose CRC does not
validate (ADR-5, strict reject-and-retry). So **both directions are confirmed
little-endian** — `WebAppDocs/ICD.md` §3.2 is right and the `CRC_HI CRC_LO`
notation in `bm_device_registration_v5.0.md` is misleading. See ADR-5.

## 🔄 In Progress
- *(none — T-6 complete)*

## 🚫 Blocked
- *(none)*

## 📁 Files Changed This Session
| File | What Changed |
|------|-------------|
| `hello-world-bringup/` → `me-primary/` | Renamed |
| `me-primary/src/proto/proto_defs.h` | Created — single source of truth: offsets, sizes, ports, IDs, CircuitID macros |
| `me-primary/src/proto/crc16.[ch]` | Created — CRC-16/Modbus, runtime-selectable byte order |
| `me-primary/src/proto/reg_frame.[ch]` | Created — pack request / parse response, pure logic |
| `me-primary/src/net/tcp_client.[ch]` | Created — connect w/ timeout, full-send, timed-recv |
| `me-primary/src/net/udp_sock.[ch]` | Created — UDP 10000/10001 |
| `me-primary/src/platform/netinfo.[ch]` | Created — board's own IP + MAC, with iface auto-detect |
| `me-primary/src/util/log.[ch]` | Created — timestamped log + hex dump |
| `me-primary/src/sys_init.[ch]` | Created — system initialization module |
| `me-primary/src/comm_thread.[ch]` | Created — the one communication thread + state machine |
| `me-primary/src/main.c` | Rewritten — args, signals, start/join/shutdown |
| `me-primary/tests/*` | Created — test_util.h, test_main.c, test_crc16.c, test_reg_frame.c, test_log.c |
| `me-primary/build.ps1` | Rewritten — multi-file, `-std=gnu11`, `-static -pthread -Werror` |
| `me-primary/build-native.ps1` | Rewritten — now builds and RUNS the unit tests |
| `me-primary/deploy.ps1` | Rewritten — `--network host`, `-ServerIP` + app options |
| `me-primary/README.md` | Rewritten |
| `Docs/ME_Primary_Comm_Block_Diagram.md` | Created — functional/workflow diagram |
| `Docs/specs/2026-08-07-...-design.md` | Created — approved design spec |
| `Docs/bm_device_registration_v5.0.md` | Added by the developer |
| `CLAUDE.md` | Updated — new paths, build flags, two-layer verification |
| `.vscode/tasks.json` | Updated — new paths, added `serverIP` prompt |
| `.claude/DECISIONS.md` | Added ADR-4 … ADR-8 |
| `.claude/ARCHITECTURE.md`, `CODEBASE_MAP.md` | Created — session #1 had deferred these until real code existed |
| `me-primary/main.c` | **Deleted** — the old 7-line Hello World; superseded by `src/main.c` |
| `me-primary/bin/hello_world*` | **Deleted** — milestone-1 binaries, orphaned by the rename |
| `hello-world-bringup.tar` | **Deleted** — stale milestone-1 archive |

## 💡 Discoveries / Gotchas
- **The length byte settles the field order.** Byte 2 counts DeviceID→MAC =
  `1+1+16+4+6 = 28 = 0x1C`, and `3+28+2 = 33`. Self-consistent only if DeviceID
  is at offset 3, which confirms ICD §3.4's offsets over §3.2's. Evidence, not
  preference (ADR-4).
- **`WebAppDocs/ICD.md` §3.4 is WRONG about the response** — says 5 bytes, no
  CRC. Real: 7 bytes with CRC. Not corrected (out of scope) — see T-8.
- **CRC byte order is genuinely ambiguous.** ICD says little-endian;
  `bm_device_registration_v5.0` writes `CRC_HI CRC_LO`. Runtime flag, LE default
  (ADR-5). **This is the most likely cause if the first hardware test fails.**
- **`--network host` is protocol-critical, not cosmetic.** The frame carries the
  board's IP/MAC as payload; on the default bridge it would send `172.17.x.x`
  with a valid CRC and register the board unreachably (ADR-6).
- **`-std=c11` hides `struct ifreq` / `IFNAMSIZ` / `IFF_UP`.** Use `-std=gnu11`.
- **ADR-2's NSS warning came due.** Static glibc + `getaddrinfo` is a trap;
  addresses are dotted-quad + `inet_pton` (ADR-8).
- **Torizon may name the NIC `end0`, not `eth0`.** `netinfo` auto-detects the
  first non-loopback IPv4 interface and logs which it used.
- **`POLLHUP` arrives alongside `POLLIN`** when a server replies and immediately
  closes. Treating the hangup flag as fatal on its own discards a response
  already sitting in the receive buffer — found and fixed during review in
  `tcp_client.c`. Worth remembering for every future recv path in this codebase.
- Project is still **not** a git repo — nothing is committed. Developer declined
  `git init` on 2026-08-06; not re-litigated.

## 🔜 Next Agent Should Do
**The registration milestone is DONE and hardware-verified.** Nothing is
outstanding on it. The known-good invocation is:

```bash
./me_primary --server 172.16.14.244 --iface ethernet0 --verbose
```

1. **T-17 — the developer is building a dedicated Docker image** (2026-08-08).
   They asked about exposing ports. **No ports need publishing:** the board is a
   TCP *client* (outbound connections never need `-p`/`EXPOSE`), and `-p` is
   ignored under `--network host` regardless. What the image needs is host
   networking, for the IP/MAC payload reason (ADR-6). Correct this if it recurs.
   A `FROM scratch` image works — the binary is fully static.
2. **T-18 — make `ethernet0` the default `--iface`.** Confirmed on hardware.
   Small, removes two warning lines per run.
3. **T-19 — narrow or drop the Docker-bridge IP warning.** It false-positives on
   this site's `172.16.x.x` LAN. A MAC-prefix check (`02:42`) was written and
   **reverted at the developer's request** — ask before changing it again.
4. **Then the next protocol milestone:** T-10 (remaining `0xDD` queries) or
   T-11 (UDP live data / session store). Both build on proven ground now:
   the frame layout, CRC order and transport are all hardware-verified.
5. Read `.claude/DECISIONS.md` before changing the frame layout, CRC handling,
   linking mode or the `docker run` line — eight ADRs are locked in with reasons.
6. Keep `src/proto/` free of socket and platform headers (ADR-7) — adding one
   silently breaks the host test suite.
