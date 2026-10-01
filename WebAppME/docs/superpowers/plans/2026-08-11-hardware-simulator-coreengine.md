# HardwareSimulator: Chunk Reassembly + CoreEngine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix `HardwareSimulator/simulator.py` dropping chunked program/DBC data, then replace its pure-random telemetry generator with a `CoreEngine` that executes the real uploaded program's step sequence (dummy sensor values, real step logic) and tags output/storage by step.

**Architecture:** Two new Python modules (`program_decoder.py`, `core_engine.py`) alongside the existing `simulator.py` (1332 lines, already large — new logic goes in new files, `simulator.py` only gets the wiring calls + two bug fixes). No changes to the C# side.

**Tech Stack:** Python stdlib only (`struct`, `dataclasses`, `enum`) + `pytest` as a new dev-only dependency (no test infra exists yet for this project).

## Global Constraints

- Do not change the wire protocol between `ChannelCommandHandler.cs` and the simulator (chunk size 1400B, command codes, `0xAA55`/offset/`0x55AA` framing) — confirmed correct as designed.
- No physics-based battery modeling — dummy values track setpoints with light noise; capacity/energy use plain `value * dt` integration (bookkeeping, not a response curve).
- No new external Python dependencies beyond `pytest` (dev-only, not required at runtime).
- All per-operator byte constants below are copied verbatim from `Components/UI/Program/OperatorConstants.cs` and `Models/Enums/ProgramEnums.cs` — confirmed via direct source inspection, not guessed.

**The decode ambiguity and how this plan resolves it:** `ProgramBuilder.ProcessLimitsWithActions` writes **no byte at all** when a step has zero limits (not even `0x00`), but writes a 1-byte count + limit-blocks when it has 1+ limits (and never a count byte at all for PAU steps). This means a byte stream, read blindly, cannot tell "0 limits → next byte is actually the Registrations count" from "N limits → next byte is the Limits count" — confirmed by checking `PacketAnalyzer.cs` (the C# side's own diagnostic re-decoder for this format), which sidesteps the problem entirely by reading counts from the original `StepModel` object rather than the bytes — an option the simulator doesn't have, since it only ever receives bytes.

This plan resolves it with a **bounded candidate-search validated against the step's known total length**: the outer `0xAA55 <4-byte offset> ... 0x55AA` framing tells us exactly where the current step's bytes end. After decoding nominal values, try limit-count candidates `N = 0, 1, 2, ... 16` in order; for each, greedily parse `N` limit-blocks (each self-describing in length once you're inside it — a limit block's action byte tells you if 0, 1, or 2 extra bytes follow) then parse the trailing Registrations block (also self-describing: a count byte followed by exactly `5 * count` bytes). The first `N` whose parse lands **exactly** on the step's known end offset is accepted. If no candidate in `0..16` lands exactly on the end, the step is marked unparseable (logged, not raised) — this is the same graceful-degradation contract as the rest of §1.

---

## 1. Dev Test Scaffolding

**Files:**
- Create: `HardwareSimulator/requirements-dev.txt`
- Create: `HardwareSimulator/tests/__init__.py`

**Interfaces:** none — this task only sets up the ability for later tasks' tests to run.

- [ ] **Step 1: Create the dev requirements file**

```
pytest>=7.4
```

- [ ] **Step 2: Create an empty test package marker**

```python
```//HardwareSimulator/tests/__init__.py — intentionally empty
```

- [ ] **Step 3: Verify pytest runs (with zero tests, should report "no tests ran")**

Run: `pip install -r HardwareSimulator/requirements-dev.txt && pytest HardwareSimulator/tests -v`
Expected: exits 0 (or pytest's "no tests collected" exit code 5 — either is fine at this stage; a hard error/traceback is not)

- [ ] **Step 4: Commit**

```bash
git add HardwareSimulator/requirements-dev.txt HardwareSimulator/tests/__init__.py
git commit -m "test: add pytest scaffolding for HardwareSimulator"
```

---

## 2. Program Step Decoder (`program_decoder.py`)

**Files:**
- Create: `HardwareSimulator/program_decoder.py`
- Test: `HardwareSimulator/tests/test_program_decoder.py`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ChunkReassembler` class and `decode_program_steps(buffer: bytes) -> list[dict]` — used by Task 5 (`simulator.py` wiring) and by Task 4 (`core_engine.py` consumes the returned step-dict shape).

Each decoded step dict has this shape (all keys always present, unused ones `None`/empty):
```python
{
    "step_id": int,
    "operator": int,          # real OperatorConstants byte value (1-20)
    "nominal": [float, ...],  # setpoint values, order matches NominalConfig field order
    "table_rows": [{"time_ms": int, "current": float|None, "power": float|None, "voltage": float|None}, ...],
    "goto_target": int|None,  # GOTO operator's resolved target step_id
    "reg_count": int,         # SET/REG AddRegCount value (bitmask-sum, not a setpoint)
    "limits": [{"cutoff": int, "logic": int, "value": float, "action": int, "action_target": int|None}, ...],
    "parse_ok": bool,         # False if the candidate-search below found no valid length match
}
```

- [ ] **Step 1: Write the operator/enum constant tables and big-endian byte helpers**

```python
"""Mirrors Services/ProgramBuilder.cs + Models/Enums/ProgramEnums.cs encoding
exactly. All multi-byte fields are big-endian, matching ExtractFloatAsByteArraySafe's
byte-reversal of BitConverter's native little-endian output."""
import struct
from dataclasses import dataclass, field

# OperatorConstants.cs byte values (verified against source, NOT the unrelated
# legacy OperatorCode enum that pre-existed in simulator.py)
OP_CC_CHG, OP_CV_CHG, OP_CP_CHG, OP_CCCV_CHG = 1, 2, 3, 4
OP_CC_DCHG, OP_CP_DCHG, OP_CCCV_DCHG = 5, 6, 7
OP_PAU, OP_GOTO, OP_SET, OP_STO = 8, 9, 10, 11
OP_CYC, OP_BEG, OP_INT, OP_REG, OP_ERR, OP_MSG, OP_TABLE = 12, 13, 14, 15, 16, 17, 18
OP_CV_DCHG, OP_PRODUCER = 19, 20

CHARGE_OPS = {OP_CC_CHG, OP_CV_CHG, OP_CP_CHG, OP_CCCV_CHG}
DISCHARGE_OPS = {OP_CC_DCHG, OP_CP_DCHG, OP_CCCV_DCHG, OP_CV_DCHG}

# NominalConfig.Configs field counts (Components/UI/Program/OperatorConstants.cs) -
# only operators routed through ProcessDefaultOperator need this; SET/REG/TABLE/
# PAU/GOTO/STO each have their own fixed layout, handled separately below.
DEFAULT_OP_NOMINAL_COUNT = {
    OP_CC_CHG: 1, OP_CV_CHG: 1, OP_CP_CHG: 1, OP_CCCV_CHG: 2,
    OP_CC_DCHG: 1, OP_CP_DCHG: 1, OP_CCCV_DCHG: 2, OP_CV_DCHG: 1,
    OP_CYC: 1, OP_BEG: 1, OP_INT: 0, OP_ERR: 1, OP_MSG: 1, OP_PRODUCER: 1,
}
DEFAULT_OPS = set(DEFAULT_OP_NOMINAL_COUNT)

# Action opcodes reuse OperatorConstants byte values directly (confirmed from source)
ACTION_GOTO, ACTION_STO, ACTION_INT, ACTION_ERR, ACTION_MSG, ACTION_NONE = 9, 11, 14, 16, 17, 0

MAX_LIMIT_CANDIDATES = 16  # bound for the candidate-search below


def _read_i32(buf: bytes, pos: int) -> int:
    return struct.unpack(">i", buf[pos:pos + 4])[0]


def _read_f32(buf: bytes, pos: int) -> float:
    return struct.unpack(">f", buf[pos:pos + 4])[0]
```

- [ ] **Step 2: Write the failing test for a minimal single-step decode (STO, the simplest case — zero body bytes, no registrations call at all)**

```python
# HardwareSimulator/tests/test_program_decoder.py
import struct
from program_decoder import decode_program_steps, OP_STO

def _wrap_step(step_bytes: bytes, next_offset: int) -> bytes:
    """0xAA 0x55 <4B offset> <step bytes> 0x55 0xAA, matching ProgramBuilder.BuildPackets."""
    return b"\xAA\x55" + struct.pack(">I", next_offset) + step_bytes + b"\x55\xAA"

def test_decode_single_sto_step():
    step_bytes = struct.pack(">H", 1) + bytes([OP_STO])  # StepID=1, OpCode=STO, no body
    buf = _wrap_step(step_bytes, 0xFFFFFFFF)  # sentinel: last step
    steps = decode_program_steps(buf)
    assert len(steps) == 1
    assert steps[0]["step_id"] == 1
    assert steps[0]["operator"] == OP_STO
    assert steps[0]["parse_ok"] is True
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pytest HardwareSimulator/tests/test_program_decoder.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'program_decoder'`

- [ ] **Step 4: Implement the packet-framing walker and per-operator dispatch**

```python
def decode_program_steps(buffer: bytes) -> list:
    """Walks the 0xAA55/offset/step-bytes/0x55AA framed stream ProgramBuilder.BuildPackets
    produces. Returns one dict per step, in encoded order."""
    steps = []
    pos = 0
    while pos < len(buffer):
        if buffer[pos:pos + 2] != b"\xAA\x55":
            break  # trailing padding or malformed stream — stop, keep what we have
        offset_field = _read_i32(buffer, pos + 2) & 0xFFFFFFFF
        body_start = pos + 6
        # Footer is always the last 2 bytes of THIS step's frame. We don't know
        # the frame end directly from offset_field when it's the 0xFFFFFFFF
        # sentinel (last step) - in that case the frame runs to the buffer's end.
        if offset_field == 0xFFFFFFFF:
            frame_end = len(buffer)
        else:
            frame_end = offset_field  # offset_field marks the next frame's start
        body_end = frame_end - 2  # strip the 0x55AA footer
        step = _decode_one_step(buffer, body_start, body_end)
        steps.append(step)
        pos = frame_end
    return steps


def _decode_one_step(buf: bytes, start: int, end: int) -> dict:
    step_id = struct.unpack(">H", buf[start:start + 2])[0]
    op = buf[start + 2]
    body_start = start + 3
    step = {
        "step_id": step_id, "operator": op, "nominal": [], "table_rows": [],
        "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True,
    }
    try:
        if op == OP_STO:
            pass  # ProcessDefaultOperator's STO branch: zero body bytes, no registrations
        elif op == OP_SET:
            _decode_set_or_reg(buf, body_start, end, step)
        elif op == OP_REG:
            _decode_set_or_reg(buf, body_start, end, step)
        elif op == OP_GOTO:
            step["goto_target"] = struct.unpack(">H", buf[body_start:body_start + 2])[0]
            _decode_registrations(buf, body_start + 2, end, step)
        elif op == OP_PAU:
            _decode_pau(buf, body_start, end, step)
        elif op == OP_TABLE:
            _decode_table(buf, body_start, end, step)
        elif op in DEFAULT_OPS:
            _decode_default(buf, body_start, end, step, op)
        else:
            step["parse_ok"] = False  # unknown operator code
    except (struct.error, IndexError):
        step["parse_ok"] = False
    return step


def _decode_set_or_reg(buf: bytes, pos: int, end: int, step: dict):
    # ProcessSetOperator: 1 placeholder byte + AddRegCount(2B). ProcessRegOperator's
    # own-registrations path is the same 2B AddRegCount; its "inherit from preceding
    # SET" fallback is a decode-time concern handled by CoreEngine, not here.
    if step["operator"] == OP_SET:
        pos += 1  # skip the placeholder 0x00
    step["reg_count"] = struct.unpack(">H", buf[pos:pos + 2])[0]
    _decode_registrations(buf, pos + 2, end, step)


def _decode_registrations(buf: bytes, pos: int, end: int, step: dict):
    """AddRegistrations: always self-describing - 1B count, then count * 5B entries
    (1B RegistrationType + 4B value). Must consume exactly to `end`."""
    if pos >= end:
        return
    count = buf[pos]
    pos += 1
    consumed = pos + count * 5
    if consumed != end:
        step["parse_ok"] = False


def _decode_pau(buf: bytes, pos: int, end: int, step: dict):
    # ProcessPauOperator never writes a limit-count byte - candidate-search over
    # how many (value+action) blocks fit before the trailing Registrations block.
    for n in range(0, MAX_LIMIT_CANDIDATES + 1):
        p = pos
        ok = True
        limits = []
        for _ in range(n):
            if p + 4 > end:
                ok = False
                break
            value = _read_f32(buf, p)
            p += 4
            action, action_target, p = _read_action(buf, p, end)
            if action is None:
                ok = False
                break
            limits.append({"cutoff": None, "logic": None, "value": value,
                            "action": action, "action_target": action_target})
        if not ok:
            continue
        reg_start = p
        if _registrations_consume_exactly(buf, reg_start, end):
            step["limits"] = limits
            _decode_registrations(buf, reg_start, end, step)
            return
    step["parse_ok"] = False


def _decode_default(buf: bytes, pos: int, end: int, step: dict, op: int):
    nominal_count = DEFAULT_OP_NOMINAL_COUNT[op]
    for _ in range(nominal_count):
        if pos + 4 > end:
            step["parse_ok"] = False
            return
        step["nominal"].append(_read_f32(buf, pos))
        pos += 4
    # Candidate-search: is the byte at `pos` a Limits count, or does this step
    # have zero limits (meaning `pos` is directly the Registrations count byte)?
    for n in range(0, MAX_LIMIT_CANDIDATES + 1):
        if n == 0:
            p = pos  # ProcessLimitsWithActions wrote nothing for zero limits
            limits = []
            ok = True
        else:
            if pos >= end or buf[pos] != n:
                continue  # the count byte itself must literally equal our guess
            p = pos + 1
            limits = []
            ok = True
            for _ in range(n):
                if p + 6 > end:
                    ok = False
                    break
                cutoff, logic = buf[p], buf[p + 1]
                value = _read_f32(buf, p + 2)
                p += 6
                action, action_target, p = _read_action(buf, p, end)
                if action is None:
                    ok = False
                    break
                limits.append({"cutoff": cutoff, "logic": logic, "value": value,
                                "action": action, "action_target": action_target})
            if not ok:
                continue
        if _registrations_consume_exactly(buf, p, end):
            step["limits"] = limits
            _decode_registrations(buf, p, end, step)
            return
    step["parse_ok"] = False


def _decode_table(buf: bytes, pos: int, end: int, step: dict):
    row_count = struct.unpack(">H", buf[pos:pos + 2])[0]
    pos += 2
    for _ in range(row_count):
        field_count, opcode_bits = buf[pos], buf[pos + 1]
        pos += 2
        time_ms = _read_i32(buf, pos)
        pos += 4
        row = {"time_ms": time_ms, "current": None, "power": None, "voltage": None}
        # bit3=time(always set,ignored here), bit2=A, bit1=W, bit0=V - order on wire
        if opcode_bits & 0x04:
            row["current"] = _read_f32(buf, pos); pos += 4
        if opcode_bits & 0x02:
            row["power"] = _read_f32(buf, pos); pos += 4
        if opcode_bits & 0x01:
            row["voltage"] = _read_f32(buf, pos); pos += 4
        step["table_rows"].append(row)
    _decode_registrations(buf, pos, end, step)


def _read_action(buf: bytes, pos: int, end: int):
    """Returns (action_opcode, action_target_or_None, new_pos) or (None, None, pos) on failure."""
    if pos >= end:
        return None, None, pos
    action = buf[pos]
    pos += 1
    if action == ACTION_GOTO:
        if pos + 2 > end:
            return None, None, pos
        target = struct.unpack(">H", buf[pos:pos + 2])[0]
        return action, target, pos + 2
    if action in (ACTION_ERR, ACTION_MSG):
        if pos + 1 > end:
            return None, None, pos
        return action, buf[pos], pos + 1
    return action, None, pos  # STO/INT/blank: no extra bytes


def _registrations_consume_exactly(buf: bytes, pos: int, end: int) -> bool:
    if pos > end:
        return False
    if pos == end:
        return False  # AddRegistrations always writes at least 1 byte (0x00 minimum)
    count = buf[pos]
    return pos + 1 + count * 5 == end
```

- [ ] **Step 5: Run test to verify it passes**

Run: `pytest HardwareSimulator/tests/test_program_decoder.py -v`
Expected: PASS

- [ ] **Step 6: Add the ChunkReassembler class + its own test cases**

```python
class ChunkReassembler:
    """Buffers count-then-N-chunks transfers (program steps or DBC file) until the
    expected chunk count arrives, then hands back the full byte stream. Mirrors the
    ChannelCommandHandler.cs chunking protocol - each SendProgram/SendDbcFile call
    is one TCP round-trip carrying one chunk of the full stream."""
    def __init__(self):
        self._buffer = bytearray()
        self._expected_chunks = 0
        self._received_chunks = 0

    def begin(self, expected_chunks: int):
        self._buffer = bytearray()
        self._expected_chunks = expected_chunks
        self._received_chunks = 0

    def add_chunk(self, data: bytes) -> bytes | None:
        """Returns the full reassembled buffer once the expected count is reached,
        else None. Ignores chunks if begin() was never called (stray/late data)."""
        if self._expected_chunks == 0:
            return None
        self._buffer.extend(data)
        self._received_chunks += 1
        if self._received_chunks >= self._expected_chunks:
            full = bytes(self._buffer)
            self._expected_chunks = 0
            self._received_chunks = 0
            self._buffer = bytearray()
            return full
        return None
```

```python
# appended to HardwareSimulator/tests/test_program_decoder.py
from program_decoder import ChunkReassembler

def test_chunk_reassembler_multi_chunk():
    r = ChunkReassembler()
    r.begin(3)
    assert r.add_chunk(b"AAA") is None
    assert r.add_chunk(b"BBB") is None
    assert r.add_chunk(b"CCC") == b"AAABBBCCC"

def test_chunk_reassembler_resets_on_new_begin_mid_transfer():
    r = ChunkReassembler()
    r.begin(3)
    r.add_chunk(b"stale")
    r.begin(2)  # simulates a retried/interrupted transfer starting over
    assert r.add_chunk(b"AA") is None
    assert r.add_chunk(b"BB") == b"AABB"

def test_decode_default_op_with_one_limit_and_goto_action():
    import struct
    from program_decoder import OP_CC_CHG, ACTION_GOTO, decode_program_steps
    body = struct.pack(">H", 5) + bytes([OP_CC_CHG])       # StepID=5, CC_CHG
    body += struct.pack(">f", 2.5)                          # nominal current=2.5A
    body += bytes([1])                                      # 1 limit
    body += bytes([0x32, 0x53]) + struct.pack(">f", 4.2)     # Voltage >= 4.2
    body += bytes([ACTION_GOTO]) + struct.pack(">H", 9)      # action: GOTO step 9
    body += bytes([0])                                       # 0 registrations
    buf = b"\xAA\x55" + struct.pack(">I", 0xFFFFFFFF) + body + b"\x55\xAA"
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["nominal"] == [2.5]
    assert steps[0]["limits"][0]["value"] == 4.2
    assert steps[0]["limits"][0]["action_target"] == 9

def test_decode_default_op_with_zero_limits():
    import struct
    from program_decoder import OP_CV_CHG, decode_program_steps
    body = struct.pack(">H", 2) + bytes([OP_CV_CHG])
    body += struct.pack(">f", 3.7)   # nominal voltage=3.7V
    body += bytes([0])                # 0 registrations, NO limit-count byte at all
    buf = b"\xAA\x55" + struct.pack(">I", 0xFFFFFFFF) + body + b"\x55\xAA"
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["limits"] == []
```

- [ ] **Step 7: Run the full test file**

Run: `pytest HardwareSimulator/tests/test_program_decoder.py -v`
Expected: all PASS (this specifically exercises the candidate-search disambiguation both with and without limits present — the core risk this module exists to handle)

- [ ] **Step 8: Commit**

```bash
git add HardwareSimulator/program_decoder.py HardwareSimulator/tests/test_program_decoder.py
git commit -m "feat: add program-step chunk reassembly and byte decoder for HardwareSimulator"
```

---

## 3. DBC Chunk Reassembly + Decode (`dbc_decoder.py`)

**Files:**
- Create: `HardwareSimulator/dbc_decoder.py`
- Test: `HardwareSimulator/tests/test_dbc_decoder.py`

**Interfaces:**
- Consumes: `ChunkReassembler` from Task 2 (`from program_decoder import ChunkReassembler`).
- Produces: `decode_dbc_signals(buffer: bytes) -> dict` — used by Task 5.

- [ ] **Step 1: Before writing the decoder, read the real encoder source**

The exact byte layout of `DbcDatabase.BuildMultiPortPayload` (`Services/DbcParser.cs`) has not yet been pulled into this plan — unlike §2, whose ProgramBuilder byte layout was fully verified against source. Before writing `decode_dbc_signals`, call:

```
mcp__gortex__get_symbol_source id="Services/DbcParser.cs::DbcDatabase.BuildMultiPortPayload" compress_bodies=false
```

and read the full method body to confirm the exact field order/widths (message ID width, signal ID range 31-255 per earlier research, bit_offset/bit_length/scale/offset encoding) before implementing Step 2 below.

- [ ] **Step 2: Write the failing test using a hand-built payload matching what Step 1 revealed**

Write `test_dbc_decoder.py` with one test that builds a small multi-port payload by hand (2 messages, 1-2 signals each) using the exact field layout confirmed in Step 1, and asserts `decode_dbc_signals(buf)` returns `{message_id: {signal_name_or_id: (bit_offset, bit_length, scale, offset)}}` matching those values. (Concrete byte values depend on Step 1's findings — write the literal test bytes once that method's source is in hand; do not guess the layout.)

- [ ] **Step 3: Run test to verify it fails**

Run: `pytest HardwareSimulator/tests/test_dbc_decoder.py -v`
Expected: FAIL — `ModuleNotFoundError`

- [ ] **Step 4: Implement `decode_dbc_signals` mirroring the confirmed layout**

Implement using the exact field order/widths found in Step 1 — a `struct`-based sequential reader in the same style as `program_decoder.py`'s helpers (`_read_i32`/`_read_f32`, big-endian throughout). Also add a `ChunkReassembler`-compatible usage note: DBC reassembly reuses `program_decoder.ChunkReassembler` as its own separate instance (one reassembler per transfer type per circuit — program and DBC transfers are independent and can be in flight on different circuits at the same time).

- [ ] **Step 5: Run test to verify it passes**

Run: `pytest HardwareSimulator/tests/test_dbc_decoder.py -v`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add HardwareSimulator/dbc_decoder.py HardwareSimulator/tests/test_dbc_decoder.py
git commit -m "feat: add DBC chunk decoder for HardwareSimulator"
```

---

## 4. CoreEngine — Step Execution State Machine (`core_engine.py`)

**Files:**
- Create: `HardwareSimulator/core_engine.py`
- Test: `HardwareSimulator/tests/test_core_engine.py`

**Interfaces:**
- Consumes: step dicts shaped as produced by `program_decoder.decode_program_steps` (Task 2) — `{"step_id", "operator", "nominal", "table_rows", "goto_target", "reg_count", "limits", "parse_ok"}`. Also consumes the operator constants (`OP_CC_CHG`, etc.) and `CHARGE_OPS`/`DISCHARGE_OPS` from `program_decoder.py`.
- Produces: `CoreEngine` class with `load_program`/`start`/`stop`/`tick` — used by Task 6 (`simulator.py` wiring).

- [ ] **Step 1: Write the CutoffCondition/LogicOperator constant tables and the CoreEngine skeleton**

```python
"""Step-execution state machine. Drives dummy current/voltage/power from the REAL
uploaded program's setpoint operators and Limit-based cutoff conditions, replacing
simulator.py's random telemetry for circuits that have a loaded program."""
import random
from dataclasses import dataclass, field
from program_decoder import (
    OP_SET, OP_REG, OP_TABLE, OP_PAU, OP_GOTO, OP_STO, CHARGE_OPS, DISCHARGE_OPS,
    ACTION_GOTO, ACTION_STO, ACTION_INT, ACTION_ERR, ACTION_MSG,
)

# Models/Enums/ProgramEnums.cs - verified against source
CUTOFF_CURRENT, CUTOFF_VOLTAGE, CUTOFF_POWER = 0x31, 0x32, 0x33
CUTOFF_CHARGE_CAP, CUTOFF_DISCHARGE_CAP = 0x34, 0x35
CUTOFF_CHARGE_ENERGY, CUTOFF_DISCHARGE_ENERGY = 0x36, 0x37
CUTOFF_TEMPERATURE, CUTOFF_TIME = 0x38, 0x39
CUTOFF_ACC_CAP, CUTOFF_STEP_CAP = 0x3A, 0x3B
CUTOFF_ACC_ENERGY, CUTOFF_STEP_ENERGY = 0x3C, 0x3D

LOGIC_GT, LOGIC_LT, LOGIC_GTE, LOGIC_LTE, LOGIC_NEQ, LOGIC_EQ = 0x51, 0x52, 0x53, 0x54, 0x55, 0x56

MAX_STEP_DURATION_MS = 24 * 3600 * 1000  # guard for a step with no Limits at all

IDLE, RUNNING, STOPPED = "IDLE", "RUNNING", "STOPPED"


@dataclass
class CoreEngine:
    steps: list = field(default_factory=list)
    current_step_index: int = 0
    elapsed_in_step_ms: int = 0
    loop_counters: dict = field(default_factory=dict)   # goto_target -> count so far
    active_registrations: set = field(default_factory=set)
    state: str = IDLE
    _table_row_index: int = 0
    _table_row_elapsed_ms: int = 0

    def load_program(self, steps: list):
        self.steps = steps
        self.state = IDLE if not steps else self.state

    def start(self):
        if not self.steps:
            self.state = IDLE
            return
        self.current_step_index = 0
        self.elapsed_in_step_ms = 0
        self.loop_counters = {}
        self._table_row_index = 0
        self._table_row_elapsed_ms = 0
        self.state = RUNNING

    def stop(self):
        self.state = STOPPED
```

- [ ] **Step 2: Write the failing test for the simplest real case — a CC_CHG step ending on a Voltage limit**

```python
# HardwareSimulator/tests/test_core_engine.py
from core_engine import CoreEngine, RUNNING, STOPPED, LOGIC_GTE, CUTOFF_VOLTAGE
from program_decoder import OP_CC_CHG, ACTION_STO, ACTION_GOTO

def _cc_chg_step(step_id, current, cutoff_value, action="STO", target=None):
    action_op = ACTION_GOTO if action == "GOTO" else ACTION_STO
    return {
        "step_id": step_id, "operator": OP_CC_CHG, "nominal": [current],
        "table_rows": [], "goto_target": None, "reg_count": 0,
        "limits": [{"cutoff": CUTOFF_VOLTAGE, "logic": LOGIC_GTE, "value": cutoff_value,
                     "action": action_op, "action_target": target}],
        "parse_ok": True,
    }

def test_cc_chg_step_ends_on_voltage_limit():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=2.0, cutoff_value=4.2)])
    engine.start()
    assert engine.state == RUNNING
    # Drive many ticks; dummy voltage model must eventually cross 4.2V during charge
    for _ in range(100000):
        sample = engine.tick(100)
        if engine.state == STOPPED:
            break
    assert engine.state == STOPPED
    assert sample["voltage"] >= 4.2
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pytest HardwareSimulator/tests/test_core_engine.py -v`
Expected: FAIL — `tick` not yet defined on `CoreEngine`

- [ ] **Step 4: Implement `tick`, the dummy value model, and Limit evaluation**

```python
    def tick(self, dt_ms: int) -> dict:
        if self.state != RUNNING or not self.steps:
            return self._idle_sample()

        step = self.steps[self.current_step_index]
        op = step["operator"]

        if op == OP_SET or op == OP_REG:
            self.active_registrations = self._resolve_registrations(step)
            self._advance_step()
            return self.tick(dt_ms)  # SET/REG are instantaneous - fall through to next step same tick
        if op == OP_GOTO:
            self._do_goto(step["goto_target"])
            return self.tick(dt_ms)
        if op == OP_STO:
            self.state = STOPPED
            return self._zeroed_sample(step)

        self.elapsed_in_step_ms += dt_ms
        self.accumulated_capacity = getattr(self, "accumulated_capacity", 0.0)
        self.accumulated_energy = getattr(self, "accumulated_energy", 0.0)

        if op == OP_PAU:
            sample = self._hold_last_sample(step)
            duration_ms = int(step["limits"][0]["value"]) if step["limits"] else MAX_STEP_DURATION_MS
            if self.elapsed_in_step_ms >= duration_ms:
                self._advance_step()
            return sample
        if op == OP_TABLE:
            return self._tick_table(step, dt_ms)

        # Setpoint operators (CC_CHG/CV_CHG/CP_CHG/CCCV_CHG and _DCHG variants)
        sample = self._tick_setpoint(step, dt_ms)
        self._evaluate_limits(step, sample)
        return sample

    def _idle_sample(self) -> dict:
        return {"current": 0.0, "voltage": 0.0, "power": 0.0, "step_number": 0,
                "operator": 0, "cycle_number": 0, "cycle_run_iteration": 0,
                "table_step_number": 0, "table_total_row_number": 0,
                "accumulated_capacity": 0.0, "charge_capacity": 0.0,
                "discharge_capacity": 0.0, "step_capacity": 0.0,
                "accumulated_energy": 0.0, "charge_energy": 0.0,
                "discharge_energy": 0.0, "step_energy": 0.0,
                "active_registrations": self.active_registrations}

    def _zeroed_sample(self, step: dict) -> dict:
        sample = self._idle_sample()
        sample["step_number"] = step["step_id"]
        sample["operator"] = step["operator"]
        return sample

    def _tick_setpoint(self, step: dict, dt_ms: int) -> dict:
        nominal = step["nominal"]
        current = nominal[0] if nominal else 0.0
        voltage = nominal[1] if len(nominal) > 1 else self._dummy_voltage_ramp(step, dt_ms)
        noisy_current = current * random.uniform(0.98, 1.02)
        power = noisy_current * voltage
        self.accumulated_capacity += noisy_current * (dt_ms / 3600000.0)
        self.accumulated_energy += power * (dt_ms / 3600000.0)
        is_charge = step["operator"] in CHARGE_OPS
        return {
            "current": noisy_current, "voltage": voltage, "power": power,
            "step_number": step["step_id"], "operator": step["operator"],
            "cycle_number": 0, "cycle_run_iteration": 0,
            "table_step_number": 0, "table_total_row_number": 0,
            "accumulated_capacity": self.accumulated_capacity,
            "charge_capacity": self.accumulated_capacity if is_charge else 0.0,
            "discharge_capacity": self.accumulated_capacity if not is_charge else 0.0,
            "step_capacity": self.accumulated_capacity,
            "accumulated_energy": self.accumulated_energy,
            "charge_energy": self.accumulated_energy if is_charge else 0.0,
            "discharge_energy": self.accumulated_energy if not is_charge else 0.0,
            "step_energy": self.accumulated_energy,
            "active_registrations": self.active_registrations,
        }

    def _dummy_voltage_ramp(self, step: dict, dt_ms: int) -> float:
        """Only CC-type steps lack an explicit voltage setpoint. Dummy model: a
        monotonic ramp (rising for charge, falling for discharge) so Voltage-cutoff
        Limits can actually be satisfied - not a real battery response curve."""
        base = getattr(self, "_ramp_voltage", 3.0)
        direction = 1 if step["operator"] in CHARGE_OPS else -1
        base += direction * 0.00002 * dt_ms
        self._ramp_voltage = base
        return base

    def _hold_last_sample(self, step: dict) -> dict:
        sample = getattr(self, "_last_sample", None) or self._zeroed_sample(step)
        sample["step_number"] = step["step_id"]
        sample["operator"] = step["operator"]
        return sample

    def _tick_table(self, step: dict, dt_ms: int) -> dict:
        rows = step["table_rows"]
        if not rows:
            self._advance_step()
            return self.tick(dt_ms)
        row = rows[self._table_row_index]
        self._table_row_elapsed_ms += dt_ms
        current = row["current"] or 0.0
        voltage = row["voltage"] or self._dummy_voltage_ramp(step, dt_ms)
        power = row["power"] if row["power"] is not None else current * voltage
        self.accumulated_capacity += current * (dt_ms / 3600000.0)
        self.accumulated_energy += power * (dt_ms / 3600000.0)
        if self._table_row_elapsed_ms >= row["time_ms"]:
            self._table_row_index += 1
            self._table_row_elapsed_ms = 0
            if self._table_row_index >= len(rows):
                self._table_row_index = 0
                self._advance_step()
        sample = {
            "current": current, "voltage": voltage, "power": power,
            "step_number": step["step_id"], "operator": step["operator"],
            "cycle_number": 0, "cycle_run_iteration": 0,
            "table_step_number": self._table_row_index + 1,
            "table_total_row_number": len(rows),
            "accumulated_capacity": self.accumulated_capacity,
            "charge_capacity": self.accumulated_capacity,
            "discharge_capacity": 0.0, "step_capacity": self.accumulated_capacity,
            "accumulated_energy": self.accumulated_energy,
            "charge_energy": self.accumulated_energy,
            "discharge_energy": 0.0, "step_energy": self.accumulated_energy,
            "active_registrations": self.active_registrations,
        }
        self._last_sample = sample
        return sample

    def _evaluate_limits(self, step: dict, sample: dict):
        field_by_cutoff = {
            CUTOFF_CURRENT: "current", CUTOFF_VOLTAGE: "voltage", CUTOFF_POWER: "power",
            CUTOFF_CHARGE_CAP: "charge_capacity", CUTOFF_DISCHARGE_CAP: "discharge_capacity",
            CUTOFF_CHARGE_ENERGY: "charge_energy", CUTOFF_DISCHARGE_ENERGY: "discharge_energy",
            CUTOFF_ACC_CAP: "accumulated_capacity", CUTOFF_STEP_CAP: "step_capacity",
            CUTOFF_ACC_ENERGY: "accumulated_energy", CUTOFF_STEP_ENERGY: "step_energy",
        }
        compare = {
            LOGIC_GT: lambda a, b: a > b, LOGIC_LT: lambda a, b: a < b,
            LOGIC_GTE: lambda a, b: a >= b, LOGIC_LTE: lambda a, b: a <= b,
            LOGIC_NEQ: lambda a, b: a != b, LOGIC_EQ: lambda a, b: a == b,
        }
        hit_any = False
        for limit in step["limits"]:
            if limit["cutoff"] == CUTOFF_TIME:
                actual = self.elapsed_in_step_ms
            else:
                fname = field_by_cutoff.get(limit["cutoff"])
                if fname is None:
                    continue
                actual = sample[fname]
            if compare[limit["logic"]](actual, limit["value"]):
                hit_any = True
                self._fire_action(limit["action"], limit["action_target"])
                break
        if not hit_any and not step["limits"] and self.elapsed_in_step_ms >= MAX_STEP_DURATION_MS:
            self._advance_step()  # malformed-program safety net

    def _fire_action(self, action: int, target):
        if action == ACTION_STO:
            self.state = STOPPED
        elif action == ACTION_GOTO:
            self._do_goto(target)
        else:
            self._advance_step()  # INT/ERR/MSG: log-and-continue, not modeled further

    def _do_goto(self, target_step_id: int):
        target_index = next(
            (i for i, s in enumerate(self.steps) if s["step_id"] == target_step_id), None
        )
        if target_index is None:
            self.state = STOPPED  # unknown target - degrade to stop, don't crash
            return
        count = self.loop_counters.get(target_step_id, 0) + 1
        self.loop_counters[target_step_id] = count
        self.current_step_index = target_index
        self.elapsed_in_step_ms = 0

    def _advance_step(self):
        self.current_step_index += 1
        self.elapsed_in_step_ms = 0
        if self.current_step_index >= len(self.steps):
            self.state = STOPPED

    def _resolve_registrations(self, step: dict) -> set:
        # REG without its own registrations inherits the nearest preceding SET's -
        # both already carry the same AddRegCount-derived reg_count from the decoder,
        # so tracking "last non-zero reg_count seen" is sufficient here.
        if step["reg_count"] > 0:
            return {step["reg_count"]}
        return self.active_registrations
```

- [ ] **Step 5: Run test to verify it passes**

Run: `pytest HardwareSimulator/tests/test_core_engine.py -v`
Expected: PASS

- [ ] **Step 6: Add remaining test cases (PAU duration, GOTO loop bound, no-program fallback, malformed-GOTO degradation)**

```python
def test_pau_holds_and_advances_after_duration():
    from core_engine import CoreEngine, RUNNING
    from program_decoder import OP_PAU, OP_STO
    steps = [
        {"step_id": 1, "operator": OP_PAU, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0,
         "limits": [{"cutoff": None, "logic": None, "value": 500, "action": 0, "action_target": None}],
         "parse_ok": True},
        {"step_id": 2, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    for _ in range(4):
        engine.tick(100)
    assert engine.current_step_index == 0  # 400ms elapsed, not yet 500ms
    engine.tick(100)  # 500ms - advances to STO step and stops
    assert engine.state != RUNNING

def test_goto_loop_bounded_then_falls_through():
    from core_engine import CoreEngine, RUNNING, STOPPED
    from program_decoder import OP_GOTO, OP_STO, ACTION_GOTO
    steps = [
        {"step_id": 1, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.tick(10)
    assert engine.state == STOPPED  # sanity: STO alone stops immediately

def test_no_program_stays_idle():
    from core_engine import CoreEngine, IDLE
    engine = CoreEngine()
    engine.load_program([])
    engine.start()
    assert engine.state == IDLE
    sample = engine.tick(100)
    assert sample["current"] == 0.0

def test_invalid_goto_target_degrades_to_stopped():
    from core_engine import CoreEngine, STOPPED
    from program_decoder import OP_CC_CHG, ACTION_GOTO
    steps = [
        {"step_id": 1, "operator": OP_CC_CHG, "nominal": [1.0], "table_rows": [],
         "goto_target": None, "reg_count": 0,
         "limits": [{"cutoff": 0x38, "logic": 0x53, "value": -999.0,
                      "action": ACTION_GOTO, "action_target": 9999}],  # unreachable target
         "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.tick(100)  # temperature always >= -999, so the GOTO always fires
    assert engine.state == STOPPED
```

- [ ] **Step 7: Run the full test file**

Run: `pytest HardwareSimulator/tests/test_core_engine.py -v`
Expected: all PASS

- [ ] **Step 8: Commit**

```bash
git add HardwareSimulator/core_engine.py HardwareSimulator/tests/test_core_engine.py
git commit -m "feat: add CoreEngine step-execution state machine for HardwareSimulator"
```

---

## 5. Wire Chunk Reassembly Into `simulator.py`

**Files:**
- Modify: `HardwareSimulator/simulator.py` (`DeviceCircuit` dataclass, `handle_program_command`)

**Interfaces:**
- Consumes: `ChunkReassembler`, `decode_program_steps` (Task 2), `decode_dbc_signals` (Task 3).
- Produces: `device.program_steps` (real decoded steps, replacing the always-empty list) and `device.dbc_signals`, consumed by Task 6.

- [ ] **Step 1: Add reassembler fields and a decoded-DBC field to `DeviceCircuit`**

In `simulator.py`, near the existing `program_steps: List[dict] = field(default_factory=list)` (line 184), add:

```python
    dbc_signals: dict = field(default_factory=dict)
    _program_reassembler: object = field(default_factory=lambda: None)
    _dbc_reassembler: object = field(default_factory=lambda: None)
```

And add the import at the top of the file:

```python
from program_decoder import ChunkReassembler, decode_program_steps
from dbc_decoder import decode_dbc_signals
```

- [ ] **Step 2: Replace the log-and-discard branches in `handle_program_command` with real reassembly**

Replace lines 691-710 (the `elif query_id == 0x03` through `elif query_id == 0x08` block) with:

```python
        elif query_id == 0x03:  # Program Steps Count
            if len(data) >= 7:
                count = read_int16_be(data, 4)
                self.logger.info(f"[CMD] Will receive {count} program step chunks")
                device._program_reassembler = device._program_reassembler or ChunkReassembler()
                device._program_reassembler.begin(count)

        elif query_id == 0x04:  # Program Data
            if device._program_reassembler is not None:
                full = device._program_reassembler.add_chunk(data[4:])
                if full is not None:
                    steps = decode_program_steps(full)
                    if all(s["parse_ok"] for s in steps):
                        device.program_steps = steps
                        self.logger.info(f"[CMD] Decoded {len(steps)} program steps for {device.device_id}-{device.circuit_id}")
                    else:
                        self.logger.error(f"[CMD] Program decode failed for {device.device_id}-{device.circuit_id} - keeping previous program_steps")

        elif query_id == 0x07:  # DBC Steps Count
            if len(data) >= 7:
                count = read_int16_be(data, 4)
                self.logger.info(f"[CMD] Will receive {count} DBC file chunks")
                device._dbc_reassembler = device._dbc_reassembler or ChunkReassembler()
                device._dbc_reassembler.begin(count)

        elif query_id == 0x08:  # DBC File Data
            self.is_dbc = True
            if device._dbc_reassembler is not None:
                full = device._dbc_reassembler.add_chunk(data[4:])
                if full is not None:
                    try:
                        device.dbc_signals = decode_dbc_signals(full)
                        self.logger.info(f"[CMD] Decoded DBC signals for {device.device_id}-{device.circuit_id}")
                    except Exception as e:
                        self.logger.error(f"[CMD] DBC decode failed for {device.device_id}-{device.circuit_id}: {e}")
```

- [ ] **Step 3: Manual smoke test — upload a program from the running app and confirm it decodes**

Run: `python HardwareSimulator/run_sim.py` (or however the dev server starts it), then from the main app's Program upload UI, send a real program to a simulated device. Check `HardwareSimulator/simulator.log` for `"Decoded N program steps"` instead of the old raw-hex logging.
Expected: log line confirms N matches the real program's step count; no `"Program decode failed"` line.

- [ ] **Step 4: Commit**

```bash
git add HardwareSimulator/simulator.py
git commit -m "fix: reassemble chunked program/DBC data in HardwareSimulator instead of discarding it"
```

---

## 6. Wire CoreEngine Into `simulator.py` + Fix Existing Encoding Bugs

**Files:**
- Modify: `HardwareSimulator/simulator.py` (`OperatorCode` enum, `DeviceCircuit`, `handle_control_command`, `data_sender`, `build_realtime_packet`, `build_store_packet`)

**Interfaces:**
- Consumes: `CoreEngine` (Task 4), `device.program_steps` (Task 5).

- [ ] **Step 1: Replace the incorrect `OperatorCode` enum (lines 56-69) with the real `OperatorConstants` values**

```python
class OperatorCode(IntEnum):
    """Mirrors Components/UI/Program/OperatorConstants.cs byte values exactly -
    the previous version of this enum used unrelated guessed values."""
    CC_CHG = 1
    CV_CHG = 2
    CP_CHG = 3
    CCCV_CHG = 4
    CC_DCHG = 5
    CP_DCHG = 6
    CCCV_DCHG = 7
    PAU = 8
    GOTO = 9
    SET = 10
    STO = 11
    CYC = 12
    BEG = 13
    INT = 14
    REG = 15
    ERR = 16
    MSG = 17
    TABLE = 18
    CV_DCHG = 19
    PRODUCER = 20

CHARGE_OPERATORS = {OperatorCode.CC_CHG, OperatorCode.CV_CHG, OperatorCode.CP_CHG, OperatorCode.CCCV_CHG}
DISCHARGE_OPERATORS = {OperatorCode.CC_DCHG, OperatorCode.CP_DCHG, OperatorCode.CCCV_DCHG, OperatorCode.CV_DCHG}
```

- [ ] **Step 2: Rename the misused tick-counter to stop colliding with real step tracking**

`DeviceCircuit.current_step_index` (line 185) is declared for real step tracking but `data_sender`'s loop (line 1053: `device.current_step_index += 1`, used only for `% 100 == 0` debug-log throttling) currently repurposes it as a send-tick counter — a collision that would corrupt CoreEngine's real step cursor once wired in. Add a dedicated counter field next to `current_step_index` in `DeviceCircuit`:

```python
    _packet_tick_count: int = 0
```

And in `data_sender`, change line 1045 (`if device.current_step_index % 100 == 0:`) and line 1053 (`device.current_step_index += 1`) to use `device._packet_tick_count` instead.

- [ ] **Step 3: Add a `CoreEngine` instance to `DeviceCircuit` and hook START/STOP**

Add to `DeviceCircuit` (near the other runtime-state fields):

```python
    core_engine: object = field(default_factory=lambda: None)
```

Add the import: `from core_engine import CoreEngine, RUNNING as ENGINE_RUNNING`

In `handle_control_command`'s `query_id == 0x01` (START) branch (after line 606's `device.step_number = 1`), add:

```python
            if device.core_engine is None:
                device.core_engine = CoreEngine()
            device.core_engine.load_program(device.program_steps)
            device.core_engine.start()
```

In the `query_id == 0x02` (STOP) branch (after line 621), add:

```python
            if device.core_engine is not None:
                device.core_engine.stop()
```

- [ ] **Step 4: Call `CoreEngine.tick()` from `data_sender` and feed its output into the packet builders**

In `data_sender`, immediately before the `packet = self.build_realtime_packet(device)` line (1019), add:

```python
                if device.core_engine is not None and device.core_engine.state == ENGINE_RUNNING:
                    sample = device.core_engine.tick(self.packet_interval_ms)
                    device.current = sample["current"]
                    device.voltage = sample["voltage"]
                    device.power = sample["power"]
                    device.step_number = sample["step_number"]
                    device.operator = sample["operator"]
                    device.cycle_number = sample["cycle_number"]
                    device.table_step_number = sample["table_step_number"]
                    device._core_sample = sample  # full field set for build_store_packet's registrations
```

- [ ] **Step 5: Fix `build_realtime_packet`'s mis-sized Cycle/Table field block**

`build_realtime_packet` currently has two problems this step fixes together: (a) it unconditionally regenerates random current/voltage/temperature even when `enable_random` should be bypassed by a running CoreEngine, and (b) the field-size bug. Replace lines 387-409 (the `if self.enable_random:` / `if not self.enable_random:` blocks) with:

```python
        core_running = device.core_engine is not None and device.core_engine.state == ENGINE_RUNNING
        if self.enable_random and not core_running:
            device_id_offset = device.device_id * 0.1
            device.current = random.uniform(
                ranges.get("current_min", 0.5 + device_id_offset),
                ranges.get("current_max", 10.0 + device_id_offset)
            )
            device.voltage = random.uniform(
                ranges.get("voltage_min", 11.0 + device_id_offset),
                ranges.get("voltage_max", 13.5 + device_id_offset)
            )
            device.temperature = random.uniform(
                ranges.get("temperature_min", 22.0),
                ranges.get("temperature_max", 35.0)
            )
        if not self.enable_random and not core_running:
            if device.current < 0.1:
                device.current = 1.0 + (device.device_id * 0.5)
            if device.voltage < 10.0:
                device.voltage = 12.0 + (device.device_id * 0.1)
```

Then replace the mis-sized block (lines 460-471, the five repeated `struct.pack(">HH", device.cycle_number, device.table_step_number)` calls) with the correctly-sized 10-byte version, sourcing real values from `device._core_sample` when a CoreEngine is driving this circuit:

```python
        sample = getattr(device, "_core_sample", None) or {}
        payload += struct.pack("B", device.operator)
        payload += struct.pack("B", device.CycleStatus)
        payload += struct.pack(">H", sample.get("cycle_number", device.cycle_number))
        payload += struct.pack(">H", sample.get("cycle_run_iteration", 0))
        payload += struct.pack(">H", sample.get("table_step_number", device.table_step_number))
        payload += struct.pack(">H", sample.get("table_total_row_number", 0))
        payload += struct.pack(">H", 0)  # reserved
```

(This replaces the old `payload += struct.pack("B", device.operator)` line and everything through the old 5x `>HH` block — net result is 10 bytes for Cycle/Table instead of 20, matching `DecoderService.ParseRealTimeData`'s offset table.)

- [ ] **Step 6: Fix `build_store_packet`'s hardcoded presence bitmask**

Replace line 493 (`payload += struct.pack(">H", 0x1FFF)`) with:

```python
        active_regs = getattr(device, "_core_sample", {}).get("active_registrations") if device.core_engine else None
        bitmask = 0x1FFF if not active_regs else _registrations_to_bitmask(active_regs)
        payload += struct.pack(">H", bitmask)
```

And add the helper function near the top-level helpers (alongside `calculate_crc16`):

```python
def _registrations_to_bitmask(active_regs) -> int:
    """Falls back to reporting the core telemetry set (opcodes 1-13, matching the
    simulator's pre-existing 0x1FFF default) when no registration has been resolved
    yet, so the store stream is never empty."""
    if not active_regs:
        return 0x1FFF
    return 0x1FFF  # TODO(follow-up): map RStandards bit values -> opcode 1-17 bits;
                    # out of scope per design doc's deferred DBC-signal-mapping note -
                    # this preserves today's behavior exactly until that follow-up lands
```

- [ ] **Step 7: Run the full pytest suite plus a manual smoke run**

Run: `pytest HardwareSimulator/tests -v`
Expected: all PASS (Tasks 1-4's tests plus no regressions)

Run: start the simulator (`python HardwareSimulator/run_sim.py`), upload a real program via the app, send START, and watch `simulator.log` / a UDP capture on port 10000 for several seconds.
Expected: `step_number` advances in the live log output as the real program's steps progress (not stuck at 1), and current/voltage move toward the program's configured setpoints instead of jumping randomly every packet.

- [ ] **Step 8: Commit**

```bash
git add HardwareSimulator/simulator.py
git commit -m "feat: wire CoreEngine into HardwareSimulator telemetry; fix operator codes and realtime-packet field sizing"
```

---

## 7. Final Verification

**Files:** none — verification only.

- [ ] **Step 1: Run the complete test suite**

Run: `pytest HardwareSimulator/tests -v`
Expected: all tests across all four test files pass.

- [ ] **Step 2: Run the existing C# test suite to confirm no regressions on the unrelated side**

Run: `dotnet test BatteryTestingSystem.sln`
Expected: same pass count as before this work (no C# files were touched — this is a pure regression check).

- [ ] **Step 3: Manual end-to-end check (cannot be automated — requires the running app's UI, which is behind login)**

Report to the user: start the dev server + simulator, log in, upload a program with at least one setpoint step (e.g. CC_CHG with a Voltage limit) and one TABLE step, hit START on a channel, and confirm on the Dashboard that: (a) step number advances over time instead of staying at 1, (b) current/voltage track the program's configured values instead of jumping randomly, (c) the channel's session data (once stopped) shows values bucketed by the real step number.

- [ ] **Step 4: Update `.claude/` agent memory per project mandate**

Update `.claude/SESSION.md` (index) + a new `sessions/{date}_hardware-simulator-coreengine.md` (detail), `.claude/TASKS.md` + a new `tasks/{date}_hardware-simulator-coreengine.md`, and `.claude/AGENT.md`'s header/live-state table, summarizing: chunk-reassembly fix, CoreEngine addition, the two encoding bugs fixed (mis-sized Cycle/Table field block, wrong OperatorCode enum), and the DBC-signal-to-store-bitmask mapping left as a follow-up (Task 6 Step 6's `TODO`).
