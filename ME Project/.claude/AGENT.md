# AGENT.md — ME Project
> Updated: 2026-08-21T11:15+05:30 | Session #16 | By: Claude Code (Sonnet 5)
> ⚠️ This file is always live — it reflects the current state of the .claude/ folder
>
> 📘 **Navigating the code? `Docs/ME_Primary_Implementation_Reference.md` is a
> file-by-file reference. Read it before opening source.**

---

## Project Overview
Embedded project targeting an **NXP MIMX8ML8CVNKZAB** (i.MX8M Plus) SoC on a
**Toradex Verdin iMX8M Plus** module running **Torizon OS** with Docker Engine
on-board.

Bring-up is **complete and verified on hardware**. `me-primary/` (renamed from
`hello-world-bringup/`) now holds the ME Primary Board application.

**Current milestone:** the Battery Testing Application base — Communication,
Core Logic, Data Manager and CAN Data Manager threads joined by in-process
message queues, with per-circuit storage, program-chain walking, step-1
extraction and a temporary 60-second demo real-time emitter.
Registration over TCP 9999 is complete; the board is the TCP **client**.

**Admission control is live (session #6, ADR-17).** A frame is handled only if
its CircuitID has registered. Because the `0xDD` frame carries one CircuitID and
is sent once per connection, **1 of 64 circuits is admitted today and the other
63 are refused** — the board is functionally single-circuit until a CAN-side
handshake lands. **Accepted, not a risk:** the Web Application conforms to this
board's CircuitID (developer decision 2026-08-12; T-33 closed, T-35 won't-do).
Do not re-raise it as a blocker.

**The board now REPLIES on TCP (sessions #7–#8, ADR-18).** `0xAA` Q5 battery
write, all six `0xEE` control commands and `0xBB` Q1/Q3/Q4 are acknowledged with
one shared 7-byte frame (`proto/ack_frame.c`) — the first frames this program
writes other than its own registration request. `route_frame()` therefore carries
the socket fd. Replies sit **behind** the admission gate: an unregistered circuit
gets no answer at all. **An ack means QUEUED to the owning thread, never "done".**

**Frame boundaries come from the CRC when the layout table is wrong (session #8,
ADR-19).** This protocol has no length field, so `me_frame_expected_len()` is a
table of *documented assumptions* — and one was wrong on hardware (`0xEE` Q1 Start
carries an undocumented 4-byte Session ID), which desynced every following frame
on the connection rather than just failing its own. `me_frame_resolve_len()` now
falls back to the CRC and **logs a WARN naming both lengths**, so a wrong entry
reports itself instead of corrupting the stream.

⚠️ **Its whole-read step must NOT be gated on `layout == 0` (session #9, ADR-19
amendment).** It was, and adding the *correct* 46-byte `0xAA` Q5 entry then made a
**short** `0xAA` frame truncate: its layout overshot what had arrived, so step 1
couldn't use it and the gate pushed it into the scan. A layout that overshoots says
nothing about where the frame ends. **The general rule: after adding any length-table
entry, check that a frame shorter than it still resolves.**

**`0xAA` is specification-backed since session #9** (`Ref Docs/bm_config_v6.0.md`,
ADR-20) — the first configuration document this project has had. Two consequences
that bite: the battery payload is **40 bytes, not 22** and all 12 fields are stored
per circuit (a payload under 40 is now **refused and NACKed**, ADR-21); and `0xAA`
Q7–Q10 are **broadcast frames with a 2-byte header** that every `0xAA` path here
would mis-parse — they are dropped today only because circuit `0x01` is malformed
(T-48).

**Real program-step execution landed session #10 (ADR-25/26/27/28).**
`me_execute_program()` starts a pure, host-tested `step_engine` per circuit:
`SET`→`CCChg`→`STOP`, one TIME cutoff, CAN-FD `SET_VALUES`/`READ_VALUES` to a
**fabricated** responder in `can_mgr.c` (no physical CAN device yet).
`demo_realtime.c/.h` is **deleted**; its post-registration frame is now
permanent, in `threads/post_reg.[ch]`. **189 host checks. NOT
hardware-verified.**

**Session #11 (2026-08-18/19, ADR-29/30/31, compact catch-up entry):** the CAN
Manager talks to the M7 over **real RPMsg** now, not a fabricator. The Core
Logic thread is **pinned to CPU3** (`--core`, default 3) with a production
`Dockerfile`/`docker-compose.yml`/GHCR release pipeline replacing the old
`deploy.bat`→`qflex-backend` path (PR #6 merged). **Hardware-verified
2026-08-19:** the pin works even through the legacy path; the board's
`isolcpus` kernel provisioning is **not yet applied** (T-60). `can_mgr.c`
also logs per-Secondary RPMsg round-trip latency in microseconds.

**Session #13 (2026-08-20, ADR-33):** `me-primary/Dockerfile`'s final stage is
now `debian:bookworm-slim`, not `scratch` — restores `docker exec -it` and
`apt-get` (default bookworm repos only) for on-device debugging. This is
**explicitly temporary** (developer: "later we will prepare the production
docker image") — ADR-30's `--core`/`cpuset`/GHCR pipeline is otherwise
unchanged. **T-63** tracks reverting to `scratch` before the next real
release. Not build/deploy tested — no Docker/WSL2 on this laptop (ADR-1).

**Session #14 (2026-08-20, ADR-34):** Deployed v0.0.2 to board
`172.16.18.167` via SSH (`/ssh-terminal` skill, password auth via
`plink`/`pscp` — no key-based auth or `sshpass` on this machine). Found and
fixed a `--channel`/`--channels` flag-name drift bug in **two**
`docker-compose.yml`s (on-device and the repo's own template — the binary
only accepts the plural `--channels <list>` since ADR-32). Developer then
identified the real gap this deploy surfaced: a circuit awaiting web-app
approval (Value `0x00`) was never retried once a sibling channel on the same
connection succeeded (ADR-32 made "one channel up, others still pending" a
reachable, permanent-until-reconnect state). **ADR-34** adds a 10s periodic
retry inside `idle_loop()`, gated per-circuit on
`me_registry_is_registered()` — extends ADR-10, cannot regress it.
**Hardware-verified**: side-loaded binary registering channels 5-8 showed
the retry firing exactly 10s after entering `IDLE`, re-registering only the
circuits still pending. Fix is source-only — not yet in a published GHCR
image (**T-64**).

**Session #15 (2026-08-20, ADR-35/36/37/38):** First hardware run with the
CAN-FD M7 link actually live — `/dev/ttyRPMSG30` had never been mapped into
any container before (`devices:` added to `docker-compose.yml`). This one
run surfaced four real bugs, all fixed:
- **ADR-36**: `step_engine.c`'s `advance_time()` froze `step_run_ms` while a
  CAN response was outstanding — every missed response cost a channel a full
  200ms of *step time*, causing "some channels fast, some slow" on an
  identical program. Contradicted the design doc verbatim. Fixed: accumulate
  unconditionally.
- **ADR-37**: a missed `SET_VALUES` response retried with **another**
  `SET_VALUES` instead of `READ_VALUES` — violates
  `master_slave_can_v1.0.md`'s "Secondary latches the setpoint, do not
  re-issue merely to obtain feedback." Fixed: always retry with READ.
- **ADR-35**: READ/poll frames transmitted a near-empty buffer instead of
  the shared per-block shadow (ADR-32) — reintroducing the zero-fill/CMD_STO
  danger via the READ path. Fixed, plus a 50ms per-block TX rate limit so up
  to 4 channels collapse to one physical CAN-FD frame, not four.
- **ADR-38**: the M7 was handing our own transmitted frames back to us
  indistinguishably from a genuine Secondary reply (`RPMSG_PROTOCOL.md` has
  no origin field). New `s_read_feedback` buffer only updates from frames
  that differ from what we last transmitted (`s_set_shadow`) — Core Logic
  now receives this buffer, never the raw inbound frame.

**Hardware-verified** after all four landed together: 0 missed responses,
0 offline, tx:rx exactly 1:1 (461/461), 0 echoed frames across 461 RX —
cross-checked against raw frame content, not just the counter. One caveat:
no echo occurred in that verification run, so the discard branch itself
wasn't exercised (T-65). None of this is in a published GHCR image yet
(T-64, now also covers this session).

**Session #16 (2026-08-21, ADR-39):** ADR-35's 50ms coalescing turned out to
hide a real problem: a channel's SET could be coalesced away at the moment
of the call, with no log line ever proving its setpoint reached the M7 — and
the RX feedback that looked like corroborating proof was static/canned
test-rig data unrelated to the actual setpoint sent (confirmed with the rig
owner), so it couldn't be used as evidence either. **ADR-39**: SET_VALUES
transmission is now never coalesced — the 50ms rate limit applies only to
READ/poll frames, which never carry new data. Diagnostic logging tuned per
developer iteration: TX log now prints SET-triggered frames only, the RX
value dump (`ME_CAN_RX_FRAME_LOG`) was switched off (canned data, no
diagnostic value), and a short-lived per-step "PROGRAM STEP N EXTRACTED"
banner in `core_logic.c` was added then fully removed at the developer's
request rather than guessing whether "skip step 3" meant one step or all.
**Hardware-verified:** every channel's SET now produces its own logged,
byte-verified physical frame. Released as `0.0.3` via `release.yml`
(2026-08-21) — **T-64 closed**, ADR-34 through ADR-39 now in a published
image. `:latest` moved to `0.0.3`, still the `debian:bookworm-slim` debug
base (T-63 deliberately deferred again by developer request — "always
create debug image, once the project work is done I will tell you to create
scratch first").

**Stack:** C11/gnu11 (GCC 15.2 cross, 16.1 host) | no framework/RTOS — Linux
userspace on Cortex-A53, **4 threads + in-process queues** | no database | runs
in Docker on Torizon OS
**Repo:** Git, adopted session #9/#10 (ADR-24). `main` = stable/merged;
`develop` = ongoing work. Remote: `github.com:Quench-EV-Charger/ME_PRD`.
**Status:** In Development — registration ✅ hardware-verified 2026-08-10;
CPU3 pin + real RPMsg link ✅ hardware-verified 2026-08-19; kernel isolcpus
provisioning and full battery-test execution over RPMsg still pending (T-60, T-24)

---

## How To Run

```powershell
# All scripts live in me-primary/ ; also wired as VS Code tasks

# Run the protocol unit tests on this laptop (no board needed)
.\build-native.ps1

# Build for the board (static aarch64 ELF -> bin\me_primary)
.\build.ps1

# Deploy to the board and run against the Web Application
.\deploy.ps1 -BoardIP <board-ip> -ServerIP <web-app-ip>
```

`deploy.ps1` defaults: `-BoardUser torizon`,
`-RemotePath /home/torizon/me_primary`, `-DockerImage debian:bookworm-slim`,
`-Port 9999`, `-CrcOrder be`. It always passes `--network host` — required, see
ADR-6.

**Test suite:** **189 checks** — the above, plus `step_decode` (SET/CCChg/STOP
byte decode), `can_frame` (64-byte pack/parse, LE floats), and `step_engine`
(cutoff timing, 3-strike offline, full SET->CCChg->STOP sequence) — run by
`build-native.ps1`, which fails the build on any failed assertion. It compiles
with `-DME_PROGRAM_BUF_SIZE=65536` — the production 2 MB × 64 circuits is
128 MB of `.bss`, which Linux reserves but Windows commits.

## Required Environment Variables
None. No `.env`, no `.env.example`. Toolchain paths live in
`.embedded-override.json` (checked in, no secrets). Runtime configuration is
command-line only.

---

## Rules For All Agents

### Mandatory (every session):
1. Read `.claude/INDEX.md` first — get oriented
2. Read `.claude/SESSION.md` before writing any code — it's an index; follow its
   "📍 Current Session" link into `sessions/{file}.md` for what's in progress
3. **Every session gets its own new file** at
   `sessions/{ISO_DATE_TIME}_{kebab-topic}.md` — never append to or reopen a
   previous session's file. Update `SESSION.md`'s pointer + history row when
   starting AND finishing work.
4. **Every task gets its own file** at `tasks/{ISO_DATE}_{kebab-task}.md`. Move
   its row between sections in `TASKS.md` (the index) as status changes; the
   full detail lives in the task's own file, not in the index.
5. **Every decision gets its own file** at `DECISIONS/{ISO_DATE}_{kebab-decision}.md`
   with WHY. Add its row to `DECISIONS.md` (the index), newest first.
6. Update `CODEBASE_MAP.md` when any file is created, deleted or renamed

### Developer standing rules (2026-08-12) — these override the conventions below:
- **The iMX8MP board is the only deliverable** — `.\build.ps1`. A Windows binary is
  never an output of the work.
- **Run the host unit tests when, and only when, files under `me-primary/src/`
  (or `tests/`) were touched** — then run **both** `.\build.ps1` and
  `.\build-native.ps1` and report the check count. Docs, `.claude/`, README or
  build-script edits alone: skip the tests. **If only the cross-build ran, say
  "compiles clean", never "tested"** — `-Werror` is not test coverage.
- **Never invoke the `agent-memory` skill unprompted.** The developer calls
  `/agent-memory` when they want a sync. Ordinary surgical `.claude/` upkeep
  (rules 1–6 below) still applies as part of doing the work.

### Code rules:
7. Never hardcode secrets — this project has none; keep it that way
8. Surgical section edits only — never rewrite a whole `.claude/` file
9. Resolve compiler paths from `.embedded-override.json`, never from bare `PATH`
10. **Test-first.** Write the failing test, watch it fail for the right reason,
    then implement. New protocol fields get a test asserting their offset
    individually.

### Project-specific — do not violate:
11. **Never claim the board target works based on a native build.** It exercises
    zero aarch64 codegen, zero Torizon, zero Docker, zero sockets. Only
    `deploy.ps1` against real hardware is evidence. (ADR-3)
12. **Do not attempt to run the aarch64 binary on this laptop.** ADR-3 — QEMU
    user-mode emulation does not exist on Windows hosts.
13. **Do not assume Docker or WSL2 exist on the laptop.** Neither is installed,
    by deliberate choice (ADR-1). Docker exists only on the board.
14. **Never add socket or platform headers to `src/proto/`.** It silently drops
    out of the host test build and the byte-layout safety net is lost. (ADR-7)
15. **Never put a protocol magic number outside `proto_defs.h`.**
16. **Never remove `--network host` from the deploy path.** The registration
    frame carries the board's IP/MAC as payload; without it the board registers
    at an unreachable address with a valid CRC. (ADR-6)
    **But do not add port publishing** — the board is a TCP *client*, outbound
    connections need no `-p`/`EXPOSE`, and `-p` is ignored under host networking.
17. **Do not introduce hostname resolution.** Static glibc + `getaddrinfo` is a
    trap; addresses are dotted-quad + `inet_pton`. (ADR-8, ADR-2)
18. Read `.claude/DECISIONS.md` before changing the frame layout, CRC handling,
    toolchain, linking mode, threading or the `docker run` line — **nineteen**
    decisions are locked in with stated reasons.
19. **Never declare a `me_msgq_t` as a local** — ~1 MB, overflows a thread
    stack. All four live at file scope in `app_queues.c` (ADR-11).
20. **Never let a malformed CircuitID fold to slot 0.** Nibbles are 1-based;
    `me_circuit_slot()` returns `ME_SLOT_INVALID` and callers must refuse
    (ADR-13).
21. **Never add an unbounded queue send.** Every send takes a finite timeout and
    drops on expiry — that is what makes deadlock impossible (ADR-12).
22. **Never share a float pack/unpack function between `can_frame.c` and
    everything else.** CAN-FD is little-endian; every WebApp-facing frame
    (`step_decode.c`, `realtime_frame.c`) is big-endian (ADR-27).
30. **A CAN-FD slot's `0x00` command byte is an active `CMD_STO`, not "no
    change".** Zero-filling unaddressed slots is accepted only because this
    iteration has one channel on the bus — revisit before channel 2+ (ADR-28).
31. **`step_engine`'s clock is always a parameter (`now_ms`), never read
    internally.** That is what keeps cutoff timing host-testable; don't add
    an internal `clock_gettime()` call to that module (ADR-27).
23. **Admission control belongs in `route_frame()` and nowhere else.** Never add
    a second registration check in a consumer thread, and never route a frame to
    a queue without passing the gate — one chokepoint is what makes it
    unbypassable (ADR-17).
24. **`circuit_registry` is owned by the communication thread alone.** A future
    CAN-side registration must **send a message**, not call
    `me_registry_mark_registered()` directly — a direct call turns a plain bool
    array into an unsynchronised cross-thread write (ADR-17).
25. **Never send a reply ahead of the admission gate.** Answering a query for an
    unregistered circuit is a claim about hardware this board has no record of.
    Replies go after the gate in `route_frame()`, without exception (ADR-18).
26. **An acknowledgement is a claim about the bytes, so verify the CRC first.**
    Every ack path runs through `frame_crc_ok()`. Never ack a frame whose
    checksum was not tested (ADR-18).
27. **Keep reply *layout* in `src/proto/` and reply *timing* in the thread.** The
    packer and the "which queries do we answer" predicate are pure and
    host-tested; only the socket call belongs in `comm_thread.c`. That split is
    what keeps any of this testable on this laptop (ADR-18).
28. **A frame length from `me_frame_expected_len()` is an ASSUMPTION, not a
    parse.** This protocol has no length field. Never add a table entry without
    a captured frame or a document behind it, and never bypass
    `me_frame_resolve_len()` — a wrong length does not fail locally, it desyncs
    every following frame on the connection. That happened on 2026-08-12
    (ADR-19).
29. **When the CRC scan recovers a frame, the WARN is the deliverable.** Silent
    self-healing leaves the layout table wrong forever. If that line appears in a
    hardware log, fix the table entry it names (ADR-19, T-44).

### Never do:
- Skip updating `.claude/` files after making changes
- Leave SESSION.md "In Progress" items stale at session end
- Make architecture changes without a DECISIONS.md entry
- Create a CONTEXT/ file that was not confirmed by the developer

---

## Key Source Files

| File | What It Does |
|------|-------------|
| `Docs/ME_Primary_Implementation_Reference.md` | **File-by-file code reference — start here** |
| `me-primary/src/proto/proto_defs.h` | **Single source of truth** — every offset, size, port, ID, CircuitID macro |
| `me-primary/src/msg.h` | The one message struct crossing every thread boundary |
| `me-primary/src/app_queues.c` | The four queues, the timeouts, the stop flag |
| `me-primary/src/util/msgq.c` | The queue itself — mutex, condvars, optional eventfd |
| `me-primary/src/store/circuit_store.c` | CircuitID→slot mapping and all per-circuit storage |
| `me-primary/src/store/circuit_registry.c` | Which circuits may be handled at all. Comm-thread-owned (ADR-17) |
| `me-primary/src/proto/program_chain.c` | The `AA 55 … 55 AA` step-chain walker |
| `me-primary/src/proto/frame_router.c` | Which thread owns an inbound frame, and where it ends |
| `me-primary/src/proto/ack_frame.c` | The 7-byte ack for every group. Pure (ADR-18) |
| `me-primary/src/threads/core_logic.c` | Test state, program assembly, real step execution (ADR-27) |
| `me-primary/src/threads/data_mgr.c` | The only caller of `circuit_store.c` |
| `me-primary/src/exec/step_engine.c` | Pure per-circuit program-step state machine, clock injected |
| `me-primary/src/proto/step_decode.c` | SET/CCChg/STOP step-body decode, big-endian |
| `me-primary/src/proto/can_frame.c` | CAN-FD 64-byte frame pack/parse, little-endian |
| `me-primary/src/threads/can_mgr.c` | Real RPMsg CAN transport to the M7 (ADR-29); logs per-Secondary RTT in us (ADR-31) |
| `me-primary/src/platform/rpmsg_link.c` | Raw-mode fd I/O on `/dev/ttyRPMSGxx` (ADR-29) |
| `me-primary/src/proto/rpmsg_frame.c` | RPMsg wire codec + stream reassembler (ADR-29) |
| `me-primary/src/proto/reg_frame.c` | Pack the 33-byte request / parse the 7-byte response. Pure logic. |
| `me-primary/src/proto/crc16.c` | CRC-16/Modbus with runtime-selectable byte order |
| `me-primary/src/threads/comm_thread.c` | The one communication thread — state machine + `poll()` over 3 sockets |
| `me-primary/src/sys_init.c` | Power-on init: board identity + both UDP sockets |
| `me-primary/src/platform/netinfo.c` | Board's real IP/MAC; warns on Docker bridge addresses |
| `me-primary/src/util/log.c` | Hex dump — the primary field-debugging tool |
| `me-primary/tests/test_reg_frame.c` | Asserts every field offset individually |
| `.embedded-override.json` | Toolchain path registry — both compilers resolved from here |
| `Docs/bm_device_registration_v5.0.md` | **Authoritative** frame source (ADR-4) |
| `Docs/ME_Primary_Comm_Block_Diagram.md` | Functional/workflow diagram |
| `CLAUDE.md` | Human/agent-facing project guidance |

---

## Toolchains

| Target | Compiler | Path |
|---|---|---|
| Board (aarch64 Linux) | Arm GNU `aarch64-none-linux-gnu` 15.2.rel1 | `C:\ArmGNUToolchain\aarch64-none-linux-gnu-15.2.rel1\bin` |
| Local (Windows x86_64) | MinGW-w64 GCC (WinLibs UCRT) 16.1.0 | `%LOCALAPPDATA%\Microsoft\WinGet\Packages\BrechtSanders.WinLibs.POSIX.UCRT_...\mingw64\bin` |

Both recorded in `.embedded-override.json`. These keys are project-specific — the
global `~/.claude/embedded-toolchain.json` covers only bare-metal MCU toolchains
(Cortex-M GCC, MPLAB, XC8/16/32) and has no slot for either of these.

Build flags that matter: `-std=gnu11` (not `c11` — `struct ifreq` needs
`_DEFAULT_SOURCE`), `-static -pthread`, `-Werror` on both builds.

---

## .claude/ Folder Live State

| File | Last Updated | Summary |
|------|-------------|---------|
| SESSION.md | 2026-08-20T10:07+05:30 | Index. Points to 12 files under `sessions/` (session #2–#13; session #1 predates the convention, its outcomes live in ADR-1/2/3). "📍 Current Session" → `sessions/2026-08-20_1007_debian-bookworm-slim-debug-image.md`. |
| TASKS.md | 2026-08-20T10:07+05:30 | Index. Points to 62 files under `tasks/` (3 active, 23 backlog, 33 done, 3 won't-do, 0 blocked). "📌 Known-good configuration" reference block stays inline (not a task). |
| DECISIONS.md | 2026-08-21T11:15+05:30 | Index. **39 ADRs**, each split into its own file under `DECISIONS/`. ADR-39 (SET_VALUES transmission never coalesced) newest. |
| ARCHITECTURE.md | 2026-08-15T23:55+05:30 | Not yet updated for session #11/#12/#13 — still describes the fabricated CAN responder and no core-affinity/CI/multi-channel/debug-image layer. Due for a pass. |
| CODEBASE_MAP.md | 2026-08-19T15:10+05:30 | Tree: `Dockerfile`/`docker-compose.yml`/`deploy.bat`/`release.yml` added; 189→229 checks (session #12/#13 not yet reflected here either). |
| INDEX.md | 2026-08-20T10:07+05:30 | Status, ADR count (33), session #13 summary |

**CONTEXT/ files:** still none. The developer has not been asked, and the
detection evidence is thin — this is a single C application with no API routes,
no database, no auth, no frontend. `ARCHITECTURE.md` and `CODEBASE_MAP.md` cover
what matters. Revisit only if the codebase grows a genuinely separable subsystem.

**Folder restructuring (session #12, 2026-08-19):** on developer request,
`SESSION.md`/`TASKS.md`/`DECISIONS.md` were split from monolithic files (the
old agent-memory@local skill's flat layout) into index + per-item files
(`sessions/`, `tasks/`, `DECISIONS/`), matching the current agent-memory
skill's canonical layout. All original content was preserved verbatim in the
new per-item files — nothing summarized or dropped. Going forward: every new
session/task/decision gets its own new file; the three index files only ever
gain one new row each.
