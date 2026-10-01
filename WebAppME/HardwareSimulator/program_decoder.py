"""Mirrors Services/ProgramBuilder.cs + Models/Enums/ProgramEnums.cs encoding
exactly. All multi-byte fields are big-endian, matching ExtractFloatAsByteArraySafe's
byte-reversal of BitConverter's native little-endian output.
"""
import struct

# OperatorConstants.cs byte values (verified against source, NOT the unrelated
# legacy OperatorCode enum that pre-existed in simulator.py)
OP_CC_CHG, OP_CV_CHG, OP_CP_CHG, OP_CCCV_CHG = 1, 2, 3, 4
OP_CC_DCHG, OP_CP_DCHG, OP_CCCV_DCHG = 5, 6, 7
OP_PAU, OP_GOTO, OP_SET, OP_STO = 8, 9, 10, 11
OP_CYC, OP_BEG, OP_INT, OP_REG, OP_ERR, OP_MSG, OP_TABLE = 12, 13, 14, 15, 16, 17, 18
OP_CV_DCHG, OP_PRODUCER = 19, 20
OP_CC_RECHG = 21  # mirrors OP_CC_CHG exactly (same NominalConfig shape, generic Default encoding)
OP_LOCKAH = 22  # ProcessLockAhOperator: op + float32 multiplier + end, no cutoffs/registrations

# Models/Enums/ProgramEnums.cs::CutoffCondition.PercAh - a regular float32 cutoff value like
# any other; only CUTOFF_TIME below needs special int32 decoding.
CUTOFF_PERCAH = 0x3E

CHARGE_OPS = {OP_CC_CHG, OP_CV_CHG, OP_CP_CHG, OP_CCCV_CHG, OP_CC_RECHG}
DISCHARGE_OPS = {OP_CC_DCHG, OP_CP_DCHG, OP_CCCV_DCHG, OP_CV_DCHG}

# Q9 "Live Step Update" eligible-operator allow-list (bm_program_v3.2.md). Deliberately NOT
# CHARGE_OPS | DISCHARGE_OPS: those include OP_CC_RECHG, which the protocol doc's allow-list
# omits. Mirrors OperatorConstants.LiveStepUpdateAllowedOperators exactly.
LIVE_STEP_UPDATE_ALLOWED_OPS = {
    OP_CC_CHG, OP_CV_CHG, OP_CP_CHG, OP_CCCV_CHG,
    OP_CC_DCHG, OP_CP_DCHG, OP_CCCV_DCHG, OP_CV_DCHG,
    OP_PAU,
}

# NominalConfig.Configs field counts (Components/UI/Program/OperatorConstants.cs) -
# only operators routed through ProcessDefaultOperator need this; SET/REG/TABLE/
# PAU/GOTO/STO each have their own fixed layout, handled separately below.
DEFAULT_OP_NOMINAL_COUNT = {
    OP_CC_CHG: 1, OP_CV_CHG: 1, OP_CP_CHG: 1, OP_CCCV_CHG: 2,
    OP_CC_DCHG: 1, OP_CP_DCHG: 1, OP_CCCV_DCHG: 2, OP_CV_DCHG: 1,
    OP_CYC: 1, OP_BEG: 1, OP_INT: 0, OP_ERR: 1, OP_MSG: 1, OP_PRODUCER: 1,
    OP_CC_RECHG: 1,
}
DEFAULT_OPS = set(DEFAULT_OP_NOMINAL_COUNT)

# Action opcodes reuse OperatorConstants byte values directly (confirmed from source)
ACTION_GOTO, ACTION_STO, ACTION_INT, ACTION_ERR, ACTION_MSG, ACTION_NONE = 9, 11, 14, 16, 17, 0

# Models/Enums/ProgramEnums.cs::CutoffCondition.Time - needed here (not just in
# core_engine.py) because it changes how a limit's raw 4-byte value must be decoded.
CUTOFF_TIME = 0x39

MAX_LIMIT_CANDIDATES = 16  # bound for the candidate-search below


def _read_i32(buf: bytes, pos: int) -> int:
    return struct.unpack(">i", buf[pos:pos + 4])[0]


def _read_f32(buf: bytes, pos: int) -> float:
    return struct.unpack(">f", buf[pos:pos + 4])[0]


class ChunkReassembler:
    """Buffers count-then-N-chunks transfers (program steps or DBC file) until the
    expected chunk count arrives, then hands back the full byte stream. Mirrors the
    ChannelCommandHandler.cs chunking protocol - each SendProgram/SendDbcFile call
    is one TCP round-trip carrying one chunk of the full stream.
    """

    def __init__(self):
        self._buffer = bytearray()
        self._expected_chunks = 0
        self._received_chunks = 0

    def begin(self, expected_chunks: int):
        self._buffer = bytearray()
        self._expected_chunks = expected_chunks
        self._received_chunks = 0

    def add_chunk(self, data: bytes):
        """Returns the full reassembled buffer once the expected count is reached,
        else None. Ignores chunks if begin() was never called (stray/late data).
        """
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


def decode_program_steps(buffer: bytes) -> list:
    """Walks the 0xAA55/offset/step-bytes/0x55AA framed stream ProgramBuilder.BuildPackets
    produces. Returns one dict per step, in encoded order.

    The raw stream ConvertProgramIntoBytesPackets returns is itself prefixed with
    a 2-byte big-endian total-length header (verified against a real captured
    transfer: bytes 0-1 equal len(stream)-2) before the first 0xAA55 frame - not
    part of ChannelCommandHandler's separate chunk-count command. Strip it if present.
    """
    if len(buffer) >= 2 and buffer[0:2] != b"\xAA\x55":
        prefix_len = struct.unpack(">H", buffer[0:2])[0]
        if prefix_len == len(buffer) - 2:
            buffer = buffer[2:]

    steps = []
    pos = 0
    while pos < len(buffer):
        if buffer[pos:pos + 2] != b"\xAA\x55":
            break  # trailing padding or malformed stream - stop, keep what we have
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
        elif op == OP_LOCKAH:
            _decode_lockah(buf, body_start, end, step)
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
    # ProcessSetOperator: 1-byte global-parameter count, then that many 5-byte
    # blocks (1B code - 0x26 AccumulatedCapacity/Ah or 0x2A AccumulatedEnergy/Wh -
    # + 4B float32 value), then AddRegCount(2B). No AddRegistrations call at all
    # (the step's body ends exactly after AddRegCount, no trailing registrations
    # block). REG has no such leading count byte at all (§6.6) - ProcessRegOperator's
    # own-registrations path is just the same 2B AddRegCount; its "inherit from
    # preceding SET" fallback is a decode-time concern handled by CoreEngine, not here.
    step["global_params"] = []
    if step["operator"] == OP_SET:
        if pos >= end:
            step["parse_ok"] = False
            return
        count = buf[pos]
        pos += 1
        for _ in range(count):
            if pos + 5 > end:
                step["parse_ok"] = False
                return
            code = buf[pos]
            value = _read_f32(buf, pos + 1)
            step["global_params"].append({"code": code, "value": value})
            pos += 5
    if pos + 2 != end:
        step["parse_ok"] = False
        return
    step["reg_count"] = struct.unpack(">H", buf[pos:pos + 2])[0]


def _decode_lockah(buf: bytes, pos: int, end: int, step: dict):
    # ProcessLockAhOperator: a single float32 multiplier, then the frame ends -
    # no cutoff-count byte and no registration byte at all.
    if pos + 4 != end:
        step["parse_ok"] = False
        return
    step["nominal"].append(_read_f32(buf, pos))


def _decode_registrations(buf: bytes, pos: int, end: int, step: dict):
    """AddRegistrations: always self-describing - 1B count, then count * 5B entries
    (1B RegistrationType + 4B value). Must consume exactly to `end`.
    """
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
    # PAU limits are always time-only (no comparison operator) per ValidationHelper,
    # so the 4-byte value is always the int32-ms encoding, never a float.
    for n in range(0, MAX_LIMIT_CANDIDATES + 1):
        p = pos
        ok = True
        limits = []
        for _ in range(n):
            if p + 4 > end:
                ok = False
                break
            value = _read_i32(buf, p)
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
                # A Time-cutoff limit's value was always encoded from a time-unit
                # string (ExtractFloatAsByteArraySafe's ms multiplier -> int32),
                # never a bare float - verified against a real captured transfer.
                value = _read_i32(buf, p + 2) if cutoff == CUTOFF_TIME else _read_f32(buf, p + 2)
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
            row["current"] = _read_f32(buf, pos)
            pos += 4
        if opcode_bits & 0x02:
            row["power"] = _read_f32(buf, pos)
            pos += 4
        if opcode_bits & 0x01:
            row["voltage"] = _read_f32(buf, pos)
            pos += 4
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
