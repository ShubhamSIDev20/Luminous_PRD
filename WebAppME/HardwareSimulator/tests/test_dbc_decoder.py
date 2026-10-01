import struct
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from dbc_decoder import decode_dbc_signals
from program_decoder import ChunkReassembler


def _port_header(enabled, baudrate, rx_count, tx_count, rx_offset, tx_offset) -> bytes:
    return struct.pack(">BBBBII", 1 if enabled else 0, baudrate, rx_count, tx_count, rx_offset, tx_offset)


def _rx_message(is_extended, msg_id, dlc, periodicity, mto_enable, mto_action,
                 msg_timeout, signal_count, signal_offset) -> bytes:
    return (struct.pack(">B", 1 if is_extended else 0) + struct.pack(">I", msg_id)
            + struct.pack(">B", dlc) + struct.pack(">H", periodicity)
            + struct.pack(">B", mto_enable) + struct.pack(">B", mto_action)
            + struct.pack(">H", msg_timeout) + struct.pack(">B", signal_count)
            + struct.pack(">I", signal_offset))


def _signal(start_bit, bit_length, value_type, byte_order, signal_id, factor, offset) -> bytes:
    return (struct.pack(">BBBBB", start_bit, bit_length, value_type, byte_order, signal_id)
            + struct.pack(">f", factor) + struct.pack(">f", offset))


def test_decode_single_port_one_message_two_signals():
    # Port1: enabled, 1 Rx message with 2 signals, no Tx messages. Ports 2/3 disabled.
    rx_offset = 36  # right after the 3x12-byte header
    port1_header = _port_header(True, 5, rx_count=1, tx_count=0, rx_offset=rx_offset, tx_offset=0)
    port2_header = _port_header(False, 0, 0, 0, 0, 0)
    port3_header = _port_header(False, 0, 0, 0, 0, 0)

    rx_msg = _rx_message(is_extended=False, msg_id=0x100, dlc=8, periodicity=100,
                          mto_enable=1, mto_action=0, msg_timeout=50,
                          signal_count=2, signal_offset=0)

    sig1 = _signal(start_bit=0, bit_length=8, value_type=0, byte_order=1, signal_id=31, factor=1.0, offset=0.0)
    sig2 = _signal(start_bit=8, bit_length=16, value_type=1, byte_order=1, signal_id=32, factor=0.1, offset=-40.0)

    buf = port1_header + port2_header + port3_header + rx_msg + sig1 + sig2

    decoded = decode_dbc_signals(buf)
    assert decoded["port1"]["enabled"] is True
    assert decoded["port1"]["baudrate"] == 5
    assert len(decoded["port1"]["rx_messages"]) == 1
    msg = decoded["port1"]["rx_messages"][0]
    assert msg["msg_id"] == 0x100
    assert msg["dlc"] == 8
    assert len(msg["signals"]) == 2
    assert msg["signals"][0]["signal_id"] == 31
    assert msg["signals"][1]["signal_id"] == 32
    assert msg["signals"][1]["factor"] == pytest_approx(0.1)
    assert decoded["port2"]["enabled"] is False
    assert decoded["port3"]["enabled"] is False


def pytest_approx(value, rel=1e-5):
    import pytest
    return pytest.approx(value, rel=rel)


def test_dbc_uses_program_decoder_chunk_reassembler():
    # DBC transfers reuse the same ChunkReassembler class as program transfers -
    # one separate instance per transfer type per circuit.
    r = ChunkReassembler()
    r.begin(2)
    assert r.add_chunk(b"\x00" * 18) is None
    full = r.add_chunk(b"\x00" * 18)
    assert full == b"\x00" * 36
