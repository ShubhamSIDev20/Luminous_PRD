"""Mirrors Services/DbcParser.cs::DbcDatabase.BuildMultiPortPayload exactly (verified
against source, not guessed). All multi-byte fields are big-endian.

Wire layout:
  [36-byte header: port1(12B) + port2(12B) + port3(12B)]
  then, in port order, for each port whose RxMsgCount/TxMsgCount > 0:
    [Rx message blocks, 17B each] [Tx message blocks, 15B each] [Rx signal blocks, 13B each, grouped per Rx message in message order]

Port header (12B): PortEnable(1B) + Baudrate(1B) + RxMsgCount(1B) + TxMsgCount(1B)
                    + RxMsgCfgOffset(4B BE, absolute byte offset from payload start)
                    + TxMsgCfgOffset(4B BE, absolute byte offset from payload start)
Rx message block (17B): IdType(1B) + MsgId(4B BE) + DLC(1B) + Periodicity(2B BE)
                    + MTOEnable(1B) + MTOAction(1B) + MsgTimeout(2B BE)
                    + RxSignalCnt(1B) + RxSignalOffset(4B BE, relative to this port's signal block start)
Tx message block (15B): IdType(1B) + MsgId(4B BE) + DLC(1B) + Periodicity(2B BE)
                    + MTOEnable(1B) + MTOAction(1B) + RxSignalCnt(1B, always 0)
                    + RxSignalOffset(4B BE, always 0)
Signal block (13B): StartBit(1B) + BitLength(1B) + ValueType(1B) + ByteOrder(1B)
                    + SignalId(1B) + Factor(4B BE float) + Offset(4B BE float)

Note: the binary format carries no message/signal NAME strings - only numeric
MsgId and SignalId. Decoded output is keyed by those numeric IDs.
"""
import struct

PORT_HEADER_SIZE = 12
TOTAL_HEADER_SIZE = 3 * PORT_HEADER_SIZE
RX_MSG_BLOCK_BYTES = 17
TX_MSG_BLOCK_BYTES = 15
SIGNAL_BLOCK_BYTES = 13


def _read_u32(buf: bytes, pos: int) -> int:
    return struct.unpack(">I", buf[pos:pos + 4])[0]


def _read_f32(buf: bytes, pos: int) -> float:
    return struct.unpack(">f", buf[pos:pos + 4])[0]


def decode_dbc_signals(buffer: bytes) -> dict:
    """Returns {"port1": {...}, "port2": {...}, "port3": {...}}, each port dict
    shaped {"enabled": bool, "baudrate": int, "rx_messages": [...], "tx_messages": [...]}.
    """
    result = {}
    for i, port_name in enumerate(("port1", "port2", "port3")):
        header_pos = i * PORT_HEADER_SIZE
        result[port_name] = _decode_port(buffer, header_pos)
    return result


def _decode_port(buf: bytes, header_pos: int) -> dict:
    enabled = buf[header_pos] == 1
    baudrate = buf[header_pos + 1]
    rx_count = buf[header_pos + 2]
    tx_count = buf[header_pos + 3]
    rx_cfg_offset = _read_u32(buf, header_pos + 4)
    tx_cfg_offset = _read_u32(buf, header_pos + 8)

    port = {"enabled": enabled, "baudrate": baudrate, "rx_messages": [], "tx_messages": []}
    if not enabled or rx_count == 0 and tx_count == 0:
        return port

    rx_messages_raw = []
    if rx_count > 0:
        pos = rx_cfg_offset
        for _ in range(rx_count):
            rx_messages_raw.append(_decode_rx_message_header(buf, pos))
            pos += RX_MSG_BLOCK_BYTES

    if tx_count > 0:
        pos = tx_cfg_offset
        for _ in range(tx_count):
            port["tx_messages"].append(_decode_tx_message(buf, pos))
            pos += TX_MSG_BLOCK_BYTES

    # Rx signal blocks immediately follow this port's Rx + Tx message blocks;
    # each Rx message's RxSignalOffset is relative to that signal-block region's start.
    signal_block_start = rx_cfg_offset + rx_count * RX_MSG_BLOCK_BYTES + tx_count * TX_MSG_BLOCK_BYTES
    for msg in rx_messages_raw:
        signals = []
        sig_pos = signal_block_start + msg["_signal_offset"]
        for _ in range(msg["_signal_count"]):
            signals.append(_decode_signal(buf, sig_pos))
            sig_pos += SIGNAL_BLOCK_BYTES
        del msg["_signal_offset"], msg["_signal_count"]
        msg["signals"] = signals
        port["rx_messages"].append(msg)

    return port


def _decode_rx_message_header(buf: bytes, pos: int) -> dict:
    id_type = buf[pos]
    msg_id = _read_u32(buf, pos + 1)
    dlc = buf[pos + 5]
    periodicity = struct.unpack(">H", buf[pos + 6:pos + 8])[0]
    mto_enable = buf[pos + 8]
    mto_action = buf[pos + 9]
    msg_timeout = struct.unpack(">H", buf[pos + 10:pos + 12])[0]
    signal_count = buf[pos + 12]
    signal_offset = _read_u32(buf, pos + 13)
    return {
        "is_extended": id_type == 1, "msg_id": msg_id, "dlc": dlc,
        "periodicity_ms": periodicity, "mto_enable": mto_enable,
        "mto_action": mto_action, "msg_timeout_ms": msg_timeout,
        "_signal_count": signal_count, "_signal_offset": signal_offset,
    }


def _decode_tx_message(buf: bytes, pos: int) -> dict:
    id_type = buf[pos]
    msg_id = _read_u32(buf, pos + 1)
    dlc = buf[pos + 5]
    periodicity = struct.unpack(">H", buf[pos + 6:pos + 8])[0]
    mto_enable = buf[pos + 8]
    mto_action = buf[pos + 9]
    return {
        "is_extended": id_type == 1, "msg_id": msg_id, "dlc": dlc,
        "periodicity_ms": periodicity, "mto_enable": mto_enable, "mto_action": mto_action,
    }


def _decode_signal(buf: bytes, pos: int) -> dict:
    return {
        "start_bit": buf[pos], "bit_length": buf[pos + 1],
        "value_type": buf[pos + 2], "byte_order": buf[pos + 3],
        "signal_id": buf[pos + 4],
        "factor": _read_f32(buf, pos + 5), "offset": _read_f32(buf, pos + 9),
    }
