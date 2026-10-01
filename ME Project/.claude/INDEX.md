# ME Project — AI Memory Index
> Updated: 2026-08-21T11:15+05:30 | Session #16 | SET_VALUES transmission never coalesced (ADR-39); diagnostic-log tuning

> 📘 **Navigating the code? Read
> [`../Docs/ME_Primary_Implementation_Reference.md`](../Docs/ME_Primary_Implementation_Reference.md) first.**
> It is a file-by-file reference — responsibility, interface, internal logic,
> dependencies and gotchas for every source file — written so you can consult
> one document instead of reading the codebase.

## What Is This?
Embedded application for the ME Primary Board — an NXP MIMX8ML8CVNKZAB
(i.MX8M Plus) on a Toradex Verdin iMX8M Plus module running Torizon OS. The
board is a **TCP client** that registers itself with, and exchanges test data
with, a Web Application.

**Stack:** C (gnu11) | Linux userspace on Cortex-A53 | no RTOS | runs in Docker
on Torizon OS | cross-compiled from Windows

---

## 🗂️ Memory Files

> 🆕 **Restructured 2026-08-19 (session #12).** `SESSION.md`, `TASKS.md` and
> `DECISIONS.md` are now INDEXES ONLY — one row per session/task/decision,
> linking into `sessions/{file}.md` / `tasks/{file}.md` / `DECISIONS/{file}.md`
> for full detail. Read the index first, then open only the linked file(s) you
> actually need — don't read a whole folder up front.

| File | Purpose | Read When |
|------|---------|-----------|
| [AGENT.md](AGENT.md) | Rules + live project state | Every session — mandatory |
| [SESSION.md](SESSION.md) | Session index → `sessions/{file}.md` | Every session — mandatory |
| [TASKS.md](TASKS.md) | Task index → `tasks/{file}.md` (backlog/active/done) | Picking up work |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layers, threading, request flow | Understanding the system |
| [CODEBASE_MAP.md](CODEBASE_MAP.md) | Annotated tree + key functions + gotchas | Navigating code |
| [DECISIONS.md](DECISIONS.md) | ADR index → `DECISIONS/{file}.md` — **39 decisions** | **Before** changing frame layout, CRC, frame-length tables, toolchain, linking, threading, admission control, outbound replies, step execution, CAN-FD, core isolation, or deploy |

Outside `.claude/`:

| Document | Purpose |
|---|---|
| `../Docs/ME_Primary_Implementation_Reference.md` | **File-by-file code reference — read this before opening source** |
| `../Docs/ME_Primary_BTS_Block_Diagram.md` | The 4-thread Battery Testing design |
| `../Docs/ME_Primary_Comm_Block_Diagram.md` | Registration / comms functional diagram |
| `../Docs/bm_device_registration_v5.0.md` | **Authoritative** registration frame source |
| `../Ref Docs/bm_config_v6.0.md` | **`0xAA` configuration / battery spec** — the 40-byte Q5 payload (ADR-20) |
| `../Ref Docs/master_slave_can_v1.0.md` | **CAN-FD spec** — corrected mid-review (ADR-25) |
| `../Ref Docs/program_packet_v0.12.md` | Step operator opcodes, confirmed vs firmware |
| `../Ref Docs/` | `0xAA`-`0xEE`, `0xA0`, CAN-FD frame specs |
| `../Docs/specs/2026-08-07-me-primary-registration-design.md` | Approved registration spec |
| `../Docs/specs/2026-08-14-step-execution-design.md` | Step execution design (ADR-25/26/27/28) |
| `../Docs/plans/2026-08-11-bts-4-thread-base-plan.md` | 4-thread implementation plan |
| `../Docs/plans/2026-08-14-step-execution-plan.md` | Step execution plan — 13 TDD tasks |
| `../me-primary/README.md` | Build, deploy, troubleshooting |
| `../CLAUDE.md` | Project guidance |

---

## 📋 Current Status
## ✅ Registration + CPU3 pin + real CAN-FD traffic (M7 link live) hardware-verified · ⚠️ Full end-to-end battery-test run with a real Secondary still open · ⚠️ Kernel isolcpus provisioning pending

**Active Tasks:** T-24 — full hardware run over the real RPMsg CAN link.
T-60 — apply the isolcpus kernel provisioning runbook on `172.16.18.167`.
T-62 — one acceptance item left (verified Stop-affects-only-its-own-slot not
yet specifically checked).
**Last session:** 2026-08-21 (#16) — the coalescing fix from session #15
(ADR-35) turned out to hide a real problem: a channel's SET could be
coalesced away with no log evidence it ever reached the M7, and the RX
feedback that looked like corroborating proof turned out to be static/canned
test-rig data unrelated to the actual setpoint sent. Fixed structurally
(**ADR-39**): SET_VALUES transmission is now never coalesced — only
READ/poll frames are still rate-limited. Diagnostic logging tuned per
developer iteration: TX log now SET-only, RX value dump switched off
(canned data, no diagnostic value), and a short-lived per-step banner
experiment in `core_logic.c` was added then fully removed at the developer's
request. **Hardware-verified**: every channel's SET now produces its own
logged, byte-verified physical frame. Released as `ghcr.io/quench-ev-charger/
me-primary:0.0.3` via `release.yml` (2026-08-21), which is now also
`:latest` — **T-64 closed**. Developer explicitly chose to keep the
`debian:bookworm-slim` debug base image for this release rather than resolve
T-63 first (standing decision, not an oversight).
**Open Tasks:** 25 backlog | 3 active | 0 blocked | 34 done | 3 won't-do

> 🆕 **CAN-FD is two float endiannesses in one program — do not mix them.**
> `can_frame.c` (CAN-FD) is **little-endian**; `step_decode.c`/`realtime_frame.c`
> (WebApp wire) are **big-endian** (ADR-27). A CAN slot's `0x00` command byte is
> an active `CMD_STO`, not "no change" — safe today only because one channel
> exists on the bus (ADR-28).

> 🔴 **The one thing to grep for in the next hardware log:**
> `RECOVERED, but the length table … needs fixing`. This protocol carries **no
> length field**, so `me_frame_expected_len()` is a table of *documented
> assumptions* — and 2026-08-12 proved one wrong (`0xEE` Q1). The cost was not one
> rejected frame: the 4 orphaned bytes would have desynced **every following
> frame** on the connection. ADR-19 now recovers the boundary from the CRC and logs
> that WARN. **Each occurrence is another wrong table entry naming itself** —
> `0xEE` Q2/Q3/Q4/Q6, `0xBB` Q1/Q3 and `0xAA` Q1–Q4/Q6 are still unconfirmed
> (**T-44**). `0xAA` Q5 = 46 **is** confirmed against a captured frame.
>
> ⚠️ **And the lesson from #9: adding a *correct* table entry broke a *different*
> frame.** A layout that overshoots what has arrived says nothing about where the
> frame ends, so it must not disable the whole-read safety net. After adding any
> entry, check that a shorter frame still resolves. See the ADR-19 amendment.

> 🔴 **Second thing to grep for:** `shorter than the 40 that bm_config_v6.0.md Q5
> defines`. Since #9 a battery payload under 40 bytes is **refused and NACKed**
> rather than half-stored (ADR-21, **T-46**). Also sanity-check the logged
> `impedance` and `energy density`: both are **confirmed `float`** (ADR-20 §9.3,
> **T-47** closed 2026-08-12) — absurd values (≈1.4e-43 or ≈1e9) now point at the
> **Web Application** still sending `uint32`, not at the parser.

> ℹ️ **Admission control is live and the board is functionally single-circuit
> (ADR-17).** A frame is handled only if its CircuitID registered, and the `0xDD`
> frame carries one CircuitID sent once per connection — so 1 of 64 circuits is
> admitted and 63 are refused, while `g_program[64]`/`g_cl_program[64]`/
> `g_demo[64]` are all built for 64. Deliberate and temporary, pending a CAN-side
> handshake.
>
> **This is accepted, not a risk: the Web Application conforms to this board's
> CircuitID** — developer decision, 2026-08-12, T-33 closed. Do **not** re-raise
> it as a blocker on the hardware run, and do not build the `--register-circuits`
> escape hatch (T-35, won't-do). Still worth knowing when reading
> `circuit_registry.c` or when the CAN-side writer lands.

> **Threading (session #4).** Four threads joined by in-process queues, one
> inbox each. `g_q_comm` carries an `eventfd` so the communication thread waits
> on three sockets and its queue in one `poll()`. **Every send has a finite
> timeout and drops on expiry** — deadlock is impossible by construction, and
> congestion shows up in the `queues:` counters at shutdown (ADR-11, ADR-12).
>
> **`src/threads/demo_realtime.c` is DELETED (session #10, ADR-27).**
> `me_execute_program()` in `core_logic.c` now starts a real `step_engine`.

> ✅ **Registration is fully hardware-verified as of 2026-08-10 09:38 UTC.**
> Request `... E2 36 9E`, response `DD 01 01 11 02 BE 15`, banner, then
> `state: IDLE`.
>
> **CRC is BIG-ENDIAN, high byte first, in both directions** (ADR-9,
> supersedes ADR-5). Change it in one place only: `ME_CRC_ORDER_DEFAULT` in
> `me-primary/src/proto/crc16.h`. The Web App team changed the server to match
> on the same day.
>
> **Response `0x01` and `0x02` are both success** (ADR-10). The board then holds
> `ME_COMM_IDLE` and never re-sends registration for a circuit that already
> succeeded. **But** (ADR-34, session #14) a circuit that got `0x00`
> (typically: new circuit, awaiting operator approval in the Web App) IS
> retried from `idle_loop()` every 10s until it succeeds or the process
> stops — gated on `me_registry_is_registered()`, so this never touches an
> already-admitted circuit and cannot regress ADR-10's original bug.
>
> **For T-17: no ports need publishing.** The board is a TCP *client*, and `-p`
> is ignored under `--network host` anyway. Host networking is needed for the
> IP/MAC payload (ADR-6), not for ports.
>
> Full network facts — board/server addresses, the `172.16.10.21` false trail —
> are in `TASKS.md` → "Known-good configuration".

---

## ⚡ Quick Start For New Agents
1. Read this file ✓
2. Read `AGENT.md` → rules + current state (note rules 11–22, they are specific)
3. Read `SESSION.md` → what's in progress and what to ask the developer first
4. Read `TASKS.md` → pick up next task
5. Protocol work? Read `../Docs/ME_Primary_Comm_Block_Diagram.md`
6. Changing the frame, CRC, toolchain or deploy? Read `DECISIONS.md` **first**
7. Start — update `SESSION.md` immediately

## ⚠️ Three things that cost time if you don't know them
- **`--network host` is protocol-critical.** The frame carries the board's
  IP/MAC as payload (ADR-6).
- **`src/proto/` must stay platform-free**, or the host test suite silently
  stops covering it (ADR-7).
- **A passing native build proves protocol logic only** — nothing about
  aarch64, Torizon, Docker or sockets (ADR-3).
