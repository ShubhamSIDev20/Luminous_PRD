# Session 16 — HardwareSimulator README + dead-config/dead-code cleanup

> Date: 2026-08-18T04:00:00Z
> Agent: Claude (Opus 5)
> Status: ✅ Complete — verified (`json.load` OK, `py_compile` OK, pytest 17/17 green)

---

## Goal

User: *"HardwareSimulator has no readme.md or how to operate it. please add there. and remove unwanted lines as well."*

Two deliverables:
1. Write `HardwareSimulator/README.md` — what it is, how to run it, every config key, protocol/threading model, tests, troubleshooting.
2. Remove genuinely dead lines found while documenting.

---

## Files changed

| File | Change |
|---|---|
| `HardwareSimulator/README.md` | **NEW** — 8.4 KB operator guide (see structure below) |
| `HardwareSimulator/config.json` | Removed 4 dead key groups (see below) |
| `HardwareSimulator/simulator.py` | Removed dead `is_dbc` no-op logging block + its `last_sent_time` var; fixed `Â°C` mojibake |

### README.md structure

Requirements → Quick start (`run_sim.py` examples) → CLI options table → How it works
(ASCII TCP/UDP diagram + 4-step connect/register/stream/react lifecycle) → Telemetry
(CoreEngine vs random fallback) → Configuration (one table per `config.json` section) →
Files table + Python↔C# mirror table → `Send-RealtimeUdp.ps1` debug tool → Tests →
Troubleshooting table.

---

## Dead config keys removed from `config.json`

Verified unused by grepping every `.py` in the folder — **no code path reads any of these**:

| Removed | Why it was dead |
|---|---|
| entire `program` block (4 steps: CC/CV/DC/STO) | `config.get("program")` appears nowhere. Programs arrive over the wire (`0xBB`) and are decoded by `program_decoder.py`; this block was a pre-CoreEngine leftover. Its `"DC"` operator isn't even a valid `OperatorConstants` name. |
| `device_defaults.mac_address` | `create_devices` uses `uuid.uuid4().hex[:12].upper()` per channel, ignoring config |
| `device_defaults.serial_number` | `create_devices` computes `10000000 + d*100 + c`, ignoring config |
| `data_ranges.capacity_min` / `capacity_max` / `power_min` / `power_max` | Only `current_*`, `voltage_*`, `temperature_*` are read by `build_realtime_packet`; power/capacity are **derived** (`power = current * voltage`, capacity integrated per tick) |

`data_ranges` still has `current_*`, `voltage_*`, `temperature_*` — all live.

## Dead code removed from `simulator.py`

In `data_sender`, this block ran every tick and **logged a lie** — it says
"sending DBC packet" but sends nothing:

```python
if device.is_dbc and (time.time() - last_sent_time > 1):
    self.logger.debug(f"... DBC mode - sending DBC packet")
    last_sent_time = time.time()
```

Removed it plus the now-orphaned `last_sent_time = time.time()` initialization.
⚠️ **This leaves `is_dbc` write-only** — set at `simulator.py:758`/`:791` when a DBC
transfer arrives, but now read nowhere. Kept the field (it's cheap, it's real device
state, and DBC UDP transmission is plausibly future work) but flagged it to the user.

Also fixed `f"T={device.temperature:.1f}Â°C"` → `C` — a UTF-8-read-as-cp1252 mojibake
(`simulator.py` is the only file in the folder with a BOM, `efbbbf`).

---

## Discoveries / gotchas

- **Server-side UDP/TCP ports are hardcoded**, not config-driven:
  `Services/ChannelManager.cs:42-44` → `commandPort = 9999`, `dataStorePort = 10001`,
  `dataViewPort = 10000`. `HardwareSimulator/config.json`'s `server.*_port` keys only
  configure the *client* side — changing one without the other silently breaks the link.
  Documented as a blockquote warning in the README.
- **`requirements-dev.txt` (just `pytest>=7.4`) was deleted** in the working tree before
  this session. Did **not** re-add it — documented `pip install pytest` inline in the
  README's Tests section instead.
- `config.json.bak` and `simulator.log` are both gitignored (`.gitignore:54` `*.bak`,
  `:63` `*.log`). The `.bak` is a stale pre-`channels_per_device: 8` copy — left in place
  (local artifact, not tracked), mentioned to the user as removable.
- **UDP load formula worth knowing:**
  `device_count × channels_per_device × (1000 / packet_interval_ms)` pkt/s.
  The `.bak`'s old `4 channels @ 10 ms` was 4,000 pkt/s; current `8 @ 1000 ms` is 80 pkt/s.
- Gortex quirk hit repeatedly this session: `search_text` rejects a bare-numeric `query`
  (`"9999"` → *"query is required"*), and `cat`/`head` on indexed `.py` files is hook-blocked
  — had to use `mcp__gortex__read_file` with `compress_bodies` + `keep:"main,create_devices"`
  for the 64 KB `simulator.py`. Plain `sed -n 'a,bp'` and `grep -n` still work.
- A `cat > README.md <<'EOF'` heredoc through the Bash tool failed with
  *"unexpected EOF while looking for matching `''`"* on this ~200-line markdown body —
  used `mcp__gortex__write_file` instead. Prefer the file tool for long content here.

---

## Verification

```
python -c "import json; json.load(open('config.json'))"   → config.json OK
python -m py_compile simulator.py core_engine.py program_decoder.py dbc_decoder.py run_sim.py
                                                          → compile OK
python -m pytest tests -q                                 → 17 passed in 0.07s
```

No `dotnet build` needed — `HardwareSimulator/` is not part of the .NET project.

## Part 2 — addressing question → `-n/--channels` + `--board-base` flags

User asked: *"when I said `-d 10` I expected `1-1-1`..`1-1-8` then `1-2-1`.., but it
shows `2-1-1`.. — is this wrong?"*

**Answer: not wrong, config-driven.** `simulator.py`'s `create_devices` derived the
board number **only from the channel index**:

```python
board_number = ((c - 1) // 8) + 1     # c=1..8 -> board 1 ALWAYS
```

With `channels_per_device: 8`, channel 9 (the first that rolls into board 2) is never
created, so device 1 fills board 1 and the loop moves to device 2. `-d` scales
**devices/TCP connections** (first ID segment); boards come from
`channels_per_device` (middle segment). `1-2-1` requires `channels_per_device >= 9`.

### ⚠️ The real bug found underneath

`((c - 1) // 8) + 1` **can never emit board 0**, but session #11 established that real
field devices register as `1-0-1`..`1-0-8` and `Utils/ChannelAddressCodec.cs:7` was
widened to accept boards `0-8` for exactly that reason. So the simulator could not
reproduce the production topology — board-`0` server paths were only ever exercised by
real hardware, never by the simulator.

### Changes (both user-approved via AskUserQuestion)

| Change | Detail |
|---|---|
| `-n` / `--channels N` | Overrides `simulation.channels_per_device`, mirroring the existing `-d`/`-i`/`-H` pattern. The missing flag is what caused the confusion — it previously required a `config.json` edit. |
| `--board-base {0,1}` | New. Sets the **lowest** secondary-board number. Default stays **1**, so existing runs and circuit IDs are unchanged; `--board-base 0` reproduces the field topology. |
| `simulation.secondary_board_base` | New config key (default 1), read in `__init__`; the flag overrides it. |
| `create_devices` | `board_number = ((c - 1) // 8) + self.board_base`; validates base ∈ {0,1} with a warning + fallback to 1. |
| Banner | Now prints `Boards per device: 0-1  (e.g. 1-0-1)` so the topology is visible before packets flow. |
| `run_sim.py` docstring, `README.md` | Documented the `-d`-vs-`-n` axis distinction with a worked ID table, plus a Board-numbering section. |

### 🪤 Trap avoided — do not "simplify" this

```python
if args.board_base is not None:      # NOT `if args.board_base:`
    sim.board_base = args.board_base
```

`--board-base 0` is **falsy**. The neighbouring `if args.devices:` / `if args.interval:`
checks use plain truthiness, so copying that pattern would silently ignore the only
value the flag exists to set. Comment left in the code saying so.

Board base is restricted to `{0,1}` (argparse `choices`) because the address byte packs
board and channel into one nibble each — a base above 1 could not address 64 channels
without overflowing the `0-8` board range.

### Part 2 verification

Drove `create_devices` directly across four flag combinations and checked every
`address_byte` against `ChannelAddressCodec`'s `board << 4 | channel` packing:

| Invocation | Circuit IDs |
|---|---|
| `-d 2` | `1-1-1`..`1-1-8`, `2-1-1`..`2-1-8` |
| `-d 1 -n 16` | `1-1-1`..`1-1-8`, `1-2-1`..`1-2-8` |
| `-d 1 -n 8 --board-base 0` | `1-0-1`..`1-0-8` |
| `-d 1 -n 64` | boards 1-8 (`1-1-1`..`1-8-8`) |

`address_byte` mismatches: **none**. Also drove `main()` with stubbed
`start_all_devices` to confirm all flags plumb through — including `--board-base 0`
landing as `base: 0`, and no-flag defaults unchanged (`10 / 8 / 1 / 1000 / localhost`).
`config.json` valid, all 5 modules compile, pytest **17/17** green.

---

## Part 3 — propagated the board-addressing truth into `docs/`

User: *"update the doc ?"* — `HardwareSimulator/README.md` was already current from part 2, so
swept the project docs for anything the board-`0` finding made wrong. Found four real defects:

| Doc | Was | Now |
|---|---|---|
| `docs/PROTOCOL.md` §2.4 | Titled **"Planned Change … (Not Yet Implemented)"**, *"Designed, not implemented"*, *"do not treat it as current behavior"* | Retitled **"(Implemented)"** — it shipped with multiplexing (`c86426c`..`2fbaf43`) |
| same | Linked `docs/architecture/01-hardware-hierarchy-protocol-design.md` | Link removed — **the whole `docs/architecture/` directory is gone** |
| same | Named the class `CircuitAddressCodec` | Corrected to `ChannelAddressCodec` (real name, `Utils/ChannelAddressCodec.cs`) |
| same | Board **0-15** / circuit **0-15**, and *"existing values reinterpreted as `(board=0, …)`"* | Board **0-8**, channel **1-8** (what `Encode` actually enforces); and the legacy claim is **wrong** — `EncodeLegacy(ch)` is `Encode(1, ch)`, so legacy maps to board **1**, not 0 |
| `docs/flowDocs/04-connection-multiplexing.md:11` | `DEVICE 1 (up to 64 channels, 2 boards)` | `up to 8 boards x 8 channels` — 64 channels needs 8 boards, not 2 |
| `docs/flowDocs/A-appendix-reference-tables.md` §A.5 | One line: *"packs board and channel — see the codec"* | Full nibble diagram, worked `0x01`/`0x11`/`0x88` examples, a can-hold-vs-accepts-vs-validates table, and the board-0-vs-1 warning |

Also added `HardwareSimulator/README.md` to the appendix's A.10 Related-documents table.

### New fact documented: `Decode` does not validate

`ChannelAddressCodec.Encode` throws outside board `0-8` / channel `1-8`, but
`Decode` just masks both nibbles — a malformed packet yields board `9-15` or channel `0` with no
error. Callers resolving a channel by decoded address must handle "no such channel". This was
nowhere in the docs before.

### ⚠️ `docs/flowDocs/index.html` has no generator

`docs/flowDocs/README.md:25` tells you to regenerate the 190 KB HTML view with
`python docs/flowDocs/_build/build.py` — **that file does not exist and was never committed**
(`git log --all -- 'docs/flowDocs/_build/*'` is empty; all of `docs/flowDocs/` is still untracked).
So the HTML cannot be rebuilt from the markdown.

Hand-mirrored both chapter edits into `index.html` instead, matching its own pre-rendered
conventions (code fences → `<figure class="panel">…<pre>` with `&lt;&lt;`-escaped operators,
tables → `<div class="table-wrap"><table>`), verified tag balance and md/html parity, and added a
warning to `flowDocs/README.md` so the next person isn't sent to a command that fails.

**Follow-up:** either write `_build/build.py` or drop the instruction. Every future chapter edit
silently desynchronizes the HTML until then.

## Open follow-up

- `is_dbc` is now write-only in `simulator.py`. Either wire up real DBC UDP
  transmission or drop the field. Raised with the user, not decided.
- **Board-`0` end-to-end run not yet done.** `--board-base 0` is unit-verified only;
  nobody has yet pointed it at the running server to confirm the board-`0`
  registration path works against the session-#11 `ChannelAddressCodec` fix. That was
  the whole point of adding it — worth doing.

## Windows gotcha

`mcp__gortex__edit_file` failed twice mid-session with
*"The process cannot access the file because it is being used by another process"*
on `simulator.py` (atomic temp+rename blocked). **Transient — an immediate retry of the
identical edit succeeded both times.** Two `python.exe` processes were live. Retry
before assuming the edit is impossible; don't fall back to a rewrite.
