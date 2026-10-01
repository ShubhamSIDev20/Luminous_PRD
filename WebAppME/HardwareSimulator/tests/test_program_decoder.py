import struct
import sys
import os
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from program_decoder import (
    decode_program_steps, ChunkReassembler,
    OP_STO, OP_CC_CHG, OP_CV_CHG, OP_SET, OP_LOCKAH, ACTION_GOTO,
)


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


def test_decode_lockah_step():
    body = struct.pack(">H", 3) + bytes([OP_LOCKAH]) + struct.pack(">f", 1.0)
    buf = _wrap_step(body, 0xFFFFFFFF)
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["operator"] == OP_LOCKAH
    assert steps[0]["nominal"] == pytest.approx([1.0])


def test_decode_set_with_ah_global_parameter():
    # count=1, code=0x26 (AccumulatedCapacity/Ah), value=0.0, then the 2B reg bitmask (STANDARD)
    body = struct.pack(">H", 1) + bytes([OP_SET])
    body += bytes([1]) + bytes([0x26]) + struct.pack(">f", 0.0)
    body += struct.pack(">H", 0x01FF)
    buf = _wrap_step(body, 0xFFFFFFFF)
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["global_params"] == [{"code": 0x26, "value": pytest.approx(0.0)}]
    assert steps[0]["reg_count"] == 0x01FF


def test_decode_set_with_zero_global_parameters_stays_backward_compatible():
    body = struct.pack(">H", 1) + bytes([OP_SET]) + bytes([0]) + struct.pack(">H", 0x603F)
    buf = _wrap_step(body, 0xFFFFFFFF)
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["global_params"] == []
    assert steps[0]["reg_count"] == 0x603F


def test_decode_default_op_with_one_limit_and_goto_action():
    body = struct.pack(">H", 5) + bytes([OP_CC_CHG])       # StepID=5, CC_CHG
    body += struct.pack(">f", 2.5)                          # nominal current=2.5A
    body += bytes([1])                                      # 1 limit
    body += bytes([0x32, 0x53]) + struct.pack(">f", 4.2)     # Voltage >= 4.2
    body += bytes([ACTION_GOTO]) + struct.pack(">H", 9)      # action: GOTO step 9
    body += bytes([0])                                       # 0 registrations
    buf = b"\xAA\x55" + struct.pack(">I", 0xFFFFFFFF) + body + b"\x55\xAA"
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["nominal"] == pytest.approx([2.5])
    assert steps[0]["limits"][0]["value"] == pytest.approx(4.2)
    assert steps[0]["limits"][0]["action_target"] == 9


def test_decode_q9_live_step_update_frame():
    """Q9's payload (bm_program_v3.2.md) is LEN(2B) + one AA55...55AA step packet - the same
    2-byte-length-prefix-then-frame shape decode_program_steps already strips for the reassembled
    Q4 buffer, so a standalone one-step Q9 payload must decode identically with no simulator
    changes needed beyond calling this function directly on data[4:-2]."""
    step_bytes = struct.pack(">H", 7) + bytes([OP_CC_CHG]) + struct.pack(">f", 1.5) + bytes([0])
    wrapped = _wrap_step(step_bytes, 0xFFFFFFFF)  # single-step payload always uses the terminator
    q9_payload = struct.pack(">H", len(wrapped)) + wrapped

    steps = decode_program_steps(q9_payload)

    assert len(steps) == 1
    assert steps[0]["step_id"] == 7
    assert steps[0]["operator"] == OP_CC_CHG
    assert steps[0]["nominal"] == pytest.approx([1.5])
    assert steps[0]["parse_ok"] is True


def test_decode_default_op_with_zero_limits():
    body = struct.pack(">H", 2) + bytes([OP_CV_CHG])
    body += struct.pack(">f", 3.7)   # nominal voltage=3.7V
    body += bytes([0])                # 0 registrations, NO limit-count byte at all
    buf = b"\xAA\x55" + struct.pack(">I", 0xFFFFFFFF) + body + b"\x55\xAA"
    steps = decode_program_steps(buf)
    assert steps[0]["parse_ok"] is True
    assert steps[0]["limits"] == []


def test_decode_real_captured_transfer():
    """Ground-truth regression test: this exact byte sequence was captured from a
    real end-to-end SendProgram transfer (Program "Test", DeviceID=1) against the
    live app + HardwareSimulator. It exposed 3 decoder bugs fixed in response:
    (1) a 2-byte total-length prefix precedes the 0xAA55 stream, (2) SET has no
    trailing AddRegistrations call, (3) a Time-cutoff limit's value is int32 ms,
    not float32.
    """
    real_bytes = bytes.fromhex(
        "00 31 aa 55 00 00 00 0e 00 01 0a 00 60 3f 55 aa "
        "aa 55 00 00 00 26 00 02 01 40 00 00 00 01 39 56 "
        "00 00 07 d0 00 00 55 aa "
        "aa 55 ff ff ff ff 00 03 0b 55 aa"
    )
    steps = decode_program_steps(real_bytes)
    assert len(steps) == 3
    assert all(s["parse_ok"] for s in steps)

    assert steps[0]["step_id"] == 1
    assert steps[0]["operator"] == 10  # SET
    assert steps[0]["reg_count"] == 0x603F

    assert steps[1]["step_id"] == 2
    assert steps[1]["operator"] == OP_CC_CHG
    assert steps[1]["nominal"] == pytest.approx([2.0])
    assert len(steps[1]["limits"]) == 1
    assert steps[1]["limits"][0]["cutoff"] == 0x39  # CUTOFF_TIME
    assert steps[1]["limits"][0]["value"] == 2000  # int32 ms, not a float

    assert steps[2]["step_id"] == 3
    assert steps[2]["operator"] == OP_STO


def test_decode_multi_step_program():
    step1 = struct.pack(">H", 1) + bytes([OP_CC_CHG]) + struct.pack(">f", 1.0) + bytes([0])
    frame1_len = 2 + 4 + len(step1) + 2  # header(2) + offset(4) + step body + footer(2)
    frame1 = b"\xAA\x55" + struct.pack(">I", frame1_len) + step1 + b"\x55\xAA"
    step2 = struct.pack(">H", 2) + bytes([OP_STO])
    frame2 = b"\xAA\x55" + struct.pack(">I", 0xFFFFFFFF) + step2 + b"\x55\xAA"
    buf = frame1 + frame2
    steps = decode_program_steps(buf)
    assert len(steps) == 2
    assert steps[0]["step_id"] == 1
    assert steps[1]["step_id"] == 2
    assert steps[1]["operator"] == OP_STO
