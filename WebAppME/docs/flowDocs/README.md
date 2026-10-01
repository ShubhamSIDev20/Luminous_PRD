# BTS System Flow — Block & Sequence Diagram Documentation

> **Project:** WebAppME / BatteryTestingSystem (ASP.NET Core 8 + Blazor Server)
> **Document version:** 2.0 (split into per-flow files)
> **Last updated:** 2026-08-18

Complete operational documentation for the Battery Testing System, from a hardware
channel powering on and requesting registration, through operator approval, data
transfer, program start, live telemetry capture and session-wise storage, to reports
and export.

Every diagram in this set is derived from the actual source code. Each chapter ends
with a **"Files involved"** line naming the source files and line ranges its diagrams
were built from, so any block can be traced back to its implementation.

**Diagram style:** ASCII box diagrams in code fences — they render identically in
VS Code, GitHub, Notepad, printed output, and pasted into Word. No renderer required.

**HTML view:** open [`index.html`](index.html) in any browser for a dark-mode single-page
version of this whole set — sidebar index, chapter filter, and scroll-spy navigation.
It is fully self-contained (no CDN, no network) and works offline straight from disk.
Regenerate it after editing any chapter with:

```
python docs/flowDocs/_build/build.py
```

> ⚠️ **`_build/build.py` is currently missing from the repo** — it was never committed, so the
> command above fails and `index.html` cannot be regenerated from the markdown. Until it is
> restored, any chapter edit must be mirrored into `index.html` by hand (it is plain pre-rendered
> HTML: code fences become `<figure class="panel">…<pre>`, tables become
> `<div class="table-wrap"><table>`). Chapters 04 and A were edited and hand-mirrored this way on
> 2026-08-18, and chapters 03, 04, 11 and 13 again on 2026-08-20 (per-board TCP connections);
> the two files agree as of 2026-08-20. When mirroring, note that quoting inside `<pre>` blocks is
> inconsistent — some blocks escape `"` as `&quot;`, others keep it literal — so match the exact
> existing text rather than assuming one convention.

---

## How to read this set

| If you are… | Start here |
|---|---|
| New to the system | [00 — Master Block Diagram](00-master-block-diagram.md), then [02](02-device-registration.md) → [03](03-user-allow-gate.md) → [05](05-transfer-flow.md) → [06](06-start-program-and-session.md) |
| Debugging a channel that will not connect | [02 — Registration](02-device-registration.md), [03 — Allow gate](03-user-allow-gate.md), [11 — Disconnect/reconnect](11-error-disconnect-reconnect.md) |
| Debugging missing or misfiled test data | [07 — UDP 10001 storage](07-udp-10001-session-storage.md), [06 — Session creation](06-start-program-and-session.md) |
| Tracing an exact call path in code | [13 — Code Call Sequences](13-code-call-sequences.md) |
| Adding a new command or entry point | [04 — Multiplexing](04-connection-multiplexing.md), [09 — Control commands](09-control-commands.md), [12 — Entry points](12-entry-points.md) |
| Looking up a byte value or port | [A — Appendix](A-appendix-reference-tables.md) |

---

## Index

### Part I — Foundations

| # | Chapter | What it covers |
|---|---------|----------------|
| 00 | [Master Block Diagram](00-master-block-diagram.md) | The whole system on one page: hardware ↔ 4 background listeners ↔ service layer ↔ 2 databases ↔ UI |
| 01 | [Background Service Startup](01-background-service-startup.md) | `AddHostedService` → `ExecuteAsync` → `LoadDevicesAsync` + `StartInternal` spawning the 4 listener loops; shutdown and restart |

### Part II — Getting a channel online

| # | Chapter | What it covers |
|---|---------|----------------|
| 02 | [Device / Channel Registration](02-device-registration.md) | The `0xDD 0x01` handshake on TCP 9999 and the full registration decision tree |
| 03 | [The User "Allow" Gate](03-user-allow-gate.md) | Why an unapproved channel gets `Failed`, how the operator approves it, and what `InitializeAsync` rehydrates |
| 04 | [Connection Multiplexing](04-connection-multiplexing.md) | Sockets vs. boards vs. channels: boards may share one TCP socket or each take their own (the server discovers which), plus write serialization and response correlation |

### Part III — Running a test

| # | Chapter | What it covers |
|---|---------|----------------|
| 05 | [Transfer Flow — Battery → DBC → Program](05-transfer-flow.md) | HW-ready check, battery params, optional 3-port DBC, chunked program download |
| 06 | [Start Program & Session Creation](06-start-program-and-session.md) | The 4-guard chain, the packed SessionID, and creation of the self-contained session `.db` |
| 09 | [Control Commands](09-control-commands.md) | Start / Stop / Pause / Continue / TimeSync / Reset / Unregister, and the two ways a session ends |
| 10 | [Read-Back Commands](10-read-back-commands.md) | Manufacturing and factory config read-out, mirrored into the main database |

### Part IV — Data flow

| # | Chapter | What it covers |
|---|---------|----------------|
| 07 | [UDP 10001 → Session-Wise Storage](07-udp-10001-session-storage.md) | The 5-stage store pipeline, on-disk layout, session-file tables, and why the design is shaped this way |
| 08 | [UDP 10000 — Live View Path](08-udp-10000-live-view.md) | Display-only telemetry, and the one bridge from live view into stored data |
| 17 | [Report & Export Read-Back](17-report-and-export.md) | Interactive paging/charting from session files, plus the two-phase async Excel export |

### Part V — Operations

| # | Chapter | What it covers |
|---|---------|----------------|
| 11 | [Error, Disconnect & Reconnect Paths](11-error-disconnect-reconnect.md) | Socket death, the stale-socket guard, reconnect behaviour, and a 12-row failure catalogue |
| 12 | [Entry Points — Four Doors, One Core](12-entry-points.md) | Blazor UI, REST API, MCP tools and Scheduler converging on one handler |
| 14 | [Calibration Flow](14-calibration.md) | The guided `0xA0` procedure, per-range vs single-point calibration, and the 163-byte read-back |
| 15 | [Broadcast — Device Discovery](15-broadcast-discovery.md) | Finding and configuring a device that does not yet know the server |
| 16 | [Scheduled Program Execution](16-scheduled-execution.md) | Hangfire-backed unattended transfer-then-start, and its execution log |

### Part VI — Reference

| # | Chapter | What it covers |
|---|---------|----------------|
| 13 | [Code Call Sequences](13-code-call-sequences.md) | Nine sequence diagrams (S1–S9) with real method signatures, plus a sequence → `file:line` cross-reference |
| A | [Appendix — Reference Tables](A-appendix-reference-tables.md) | Ports, start bytes, query IDs, status codes, SessionID bit layout, Storage V2 header, file naming, calibration IDs, and the divergence register |

### Part VII — Authoring content

| # | Chapter | What it covers |
|---|---------|----------------|
| 18 | [Program Editor: Authoring Guide](18-program-editor-guide.md) | Every operator's mandatory/optional rules, the Limit/Action/Label/Registration grammar, and worked examples from a minimal program to loops, global variables and `PRODUCER` composition |

---

## The short version

If you read nothing else, this is the lifecycle:

```
  1. Channel powers on, dials TCP 9999, sends [0xDD 0x01]        --> Ch 02
  2. Server answers "Failed" and writes the row to the database  --> Ch 02
  3. Operator clicks Allow; IsRegistered = 1                     --> Ch 03
  4. Hardware's next retry now succeeds; handler is created      --> Ch 03
  5. Operator transfers Battery -> DBC (optional) -> Program     --> Ch 05
  6. Operator hits Start; session .db is created, 0xEE/0x01 sent --> Ch 06
  7. Hardware streams data on UDP 10001 stamped with the SessionID
  8. Server files it under sessions/<dd-MM-yyyy>/<SID>_<D>_<B>_<C>.db  --> Ch 07
  9. Test ends by Stop command, or by an STO record in the stream --> Ch 09
 10. Operator views or exports the session file                   --> Ch 17
```

---

## ⚠ Known divergences

Four places where two parts of the system — or the code and `docs/PROTOCOL.md` —
do not agree. All were found while writing this documentation. **None has been
changed**; three touch the wire protocol and need firmware confirmation.

| # | Divergence | Chapter |
|---|---|---|
| 1 | UI and REST/MCP transfer paths use different orders and different DBC port counts | [13 · S4](13-code-call-sequences.md#s4-transfer-from-rest--mcp-devicecontrollercoresendprogram) |
| 2 | `TimeSyn` and `ResetSystem` emit the same wire frame (`0xEE` + `0x06`) | [09](09-control-commands.md) |
| 3 | Broadcast ports in code (`10002`/`10003`) differ from the spec (`10003`/`10004`) | [15](15-broadcast-discovery.md) |
| 4 | Scheduler resolves target channels without comparing the board number | [16](16-scheduled-execution.md) |

Full detail in the [divergence register](A-appendix-reference-tables.md#a9-divergence-register).

---

## Related documents

| Document | Contents |
|---|---|
| `docs/PROTOCOL.md` | Byte-level protocol specification (framing, CRC-16, every packet layout) |
| `.claude/ARCHITECTURE.md` | System design overview, known gaps, deployment topology |
| `.claude/CODEBASE_MAP.md` | File-by-file map of the repository |
| `.claude/DECISIONS.md` | Architecture decision records |
| `docs/deployment/` | Customer-site installation guide |
