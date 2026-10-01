# RPMSG Protocol — CAN Frame Exchange (A53 ↔ M7)

**Audience:** A53 (Linux) application developer.
**Purpose:** implement the A53 side of the CAN frame exchange with the M7
core firmware (`me_battery_m7_core`).

| | |
|---|---|
| Document version | 3.0 |
| Date | 2026-08-18 |
| M7 firmware | `me_battery_m7_core` (FreeRTOS + RPMsg-Lite) |
| Reference implementation | `test/rpmsg_can_test.py` (Python, stdlib only) |

---

## 1. Transport overview

- The M7 (FreeRTOS, RPMsg-Lite **remote**) communicates with the A53 (Linux,
  RPMsg **master**) over virtio shared memory.
- On Linux the `imx_rpmsg_tty` driver binds to the M7's name-service
  announcement and creates a character device:

| Parameter | Value |
|---|---|
| Linux device | `/dev/ttyRPMSG30` |
| Name-service channel | `"rpmsg-virtual-tty-channel-1"` |
| M7 endpoint address | `30` (`0x1E`) |
| A53 source address (as seen by M7) | `0x400` |
| RPMsg buffer payload | 496 bytes |
| Direction support | bidirectional |

## 2. Wire frame format

Every message, in both directions, is a packed `rpmsg_frame_t`:

```
+--------+--------+---------------+--------+---------------+-----------------+
| Header | Action |    Command    | Result |    Length     |     Payload     |
| 1 byte | 1 byte | 4 bytes (BE)  | 1 byte | 4 bytes (BE)  | 0..80 bytes     |
+--------+--------+---------------+--------+---------------+-----------------+
  0xAA
```

| Offset | Field | Size | Description |
|---|---|---|---|
| 0 | `header` | 1 | Sync byte, always `0xAA` |
| 1 | `action` | 1 | `0x01` = WRITE_SET, `0x02` = READ |
| 2 | `command` | 4 | Command ID, **big-endian** |
| 6 | `result` | 1 | `0x00` = REQUEST, `0x01` = SUCCESS, `0xFF` = FAILURE |
| 7 | `length` | 4 | Payload length, **big-endian** (0 or 80) |
| 11 | `payload` | 0–80 | Packed `can_frame_msg_t` (§3) |

- Maximum frame size: **11 + 80 = 91 bytes**.
- **Endianness warning:** the header fields are big-endian **on the wire**,
  but the CAN frame payload (§3) is the M7's native **little-endian** struct.
  Convert only the header.

### Field values

```c
#define RPMSG_SYNC_BYTE        (0xAA)
#define RPMSG_ACTION_WRITE_SET (0x01)
#define RPMSG_ACTION_READ      (0x02)
#define RPMSG_RESULT_REQUEST   (0x00)
#define RPMSG_RESULT_SUCCESS   (0x01)
#define RPMSG_RESULT_FAILURE   (0xFF)
```

## 3. CAN frame payload (`can_frame_msg_t`, 80 bytes)

Payload of the SET command and of every streamed CAN frame.
**All multi-byte fields are little-endian** (raw M7 struct).

| Offset | Field | Type | Description |
|---|---|---|---|
| 0 | `timestamp_ms` | `uint32_t` (LE) | M7 sequence counter (not a wall-clock time) |
| 4 | `can_id` | `uint32_t` (LE) | CAN ID (11-bit or 29-bit value, right-aligned) |
| 8 | `is_extended` | `uint8_t` | 1 = extended 29-bit ID, 0 = standard 11-bit ID |
| 9 | `is_fd` | `uint8_t` | 1 = CAN FD, 0 = Classic CAN |
| 10 | `brs` | `uint8_t` | Bit-rate switch flag (meaningful if `is_fd`) |
| 11 | `esi` | `uint8_t` | Error state indicator |
| 12 | `dlc` | `uint8_t` | Raw DLC code (0–15), see table below |
| 13 | `data_len` | `uint8_t` | Real payload byte count |
| 14 | `reserved[2]` | `uint8_t[2]` | Zero |
| 16 | `data[64]` | `uint8_t[64]` | CAN payload; unused bytes are zero |

C definition (packed):

```c
typedef struct __attribute__((packed))
{
    uint32_t timestamp_ms;
    uint32_t can_id;
    uint8_t  is_extended;
    uint8_t  is_fd;
    uint8_t  brs;
    uint8_t  esi;
    uint8_t  dlc;
    uint8_t  data_len;
    uint8_t  reserved[2];
    uint8_t  data[64];
} can_frame_msg_t;   /* exactly 80 bytes */
```

Python equivalent: `struct "<IIBBBBBB2x64s"`.

### DLC reference

`dlc` is the raw CAN FD DLC code; `data_len` is the real byte count.
When building a SET frame, use this table (intermediate lengths are padded
up to the next valid CAN FD length):

| DLC | Bytes | DLC | Bytes |
|---|---|---|---|
| 0–8 | 0–8 (1:1) | 12 | 24 |
| 9 | 12 | 13 | 32 |
| 10 | 16 | 14 | 48 |
| 11 | 20 | 15 | 64 |

Classic CAN frames (`is_fd = 0`) are limited to 8 bytes (DLC 0–8).
CAN bit rates on the M7: 500 kbps nominal, 5 Mbps FD data phase (BRS).

## 4. Command IDs

| Command ID | Name | Direction | Action | Payload |
|---|---|---|---|---|
| `0x00100001` | `RPMSG_CMD_CAN_SET_FRAME` | A53 → M7 | WRITE_SET | `can_frame_msg_t` (80 B) |
| `0x00100002` | `RPMSG_CMD_CAN_GET_FRAME` | both | READ | none / 80 B (stream) |

## 5. Receiver requirements (A53 side)

`/dev/ttyRPMSG30` delivers a **byte stream**: a single `read()` may contain
a partial frame, one frame, or several concatenated frames. The receiver
must:

1. Append each `read()` to a buffer.
2. Scan for the `0xAA` sync byte (discard anything before it).
3. Wait until 11 header bytes are available; read `length` (big-endian).
4. Reject implausible lengths (`> 80`) by skipping one byte and rescanning.
5. Wait until `11 + length` bytes are available, then extract one frame.
6. Repeat — a buffer may contain several frames.

Distinguishing frame types after parsing:

| `command` | `length` | Meaning |
|---|---|---|
| `0x00100001` (SET) | 0 | ACK for your SET command |
| `0x00100002` (GET) | 0 | ACK for your GET command |
| `0x00100002` (GET) | 80 | Streamed CAN1 RX frame |

## 6. Minimal reference code (Python 3, stdlib)

Build and send a SET frame; parse the stream:

```python
import os, struct, tty

SYNC = 0xAA
SET_FRAME, GET_FRAME = 0x00100001, 0x00100002
WRITE_SET, READ, REQUEST = 0x01, 0x02, 0x00

def wrap(action, command, result, payload):
    return struct.pack(">BBIBI", SYNC, action, command, result,
                       len(payload)) + payload

def can_msg(can_id, ext, fd_frame, brs, dlc, data):
    return struct.pack("<IIBBBBBB", 0, can_id, int(ext), int(fd_frame),
                       int(brs), 0, dlc, len(data)) \
           + b"\x00\x00" + data.ljust(64, b"\x00")

fd = os.open("/dev/ttyRPMSG30", os.O_RDWR | os.O_NOCTTY)
tty.setraw(fd)

# Send a 64-byte CAN FD frame, ID 0x300, BRS set:
data = bytes([0x00] + [(0xA0 + i) & 0xFF for i in range(1, 64)])
os.write(fd, wrap(WRITE_SET, SET_FRAME, REQUEST,
                  can_msg(0x300, False, True, True, 15, data)))

# Read loop: buffer bytes, split into frames by scanning for 0xAA and
# using the big-endian length field (see §5).
```

A complete, tested implementation (sender + stream parser + monitor) is
provided in the firmware repository at **`test/rpmsg_can_test.py`**.

## 7. Quick verification

On the A53, with the M7 running:

```bash
ls -l /dev/ttyRPMSG*                                   # device exists
cat /sys/class/remoteproc/remoteproc0/state            # "running"
python3 rpmsg_can_test.py                              # 64 B dummy frame every 1 s
```

Expected: `ACK SET_FRAME SUCCESS` printed every second, and on the M7 UART
`RPMSG SET received: ID=0x300 STD FD+BRS LEN=64 data= ...`.

## 8. Worked example: sending a CAN frame (byte level)

Goal: transmit a **Classic CAN** frame, standard ID `0x18F`, 8 data bytes
`11 22 33 44 55 66 77 88` on CAN1.

### Step 1 — build the payload (`can_frame_msg_t`, 80 bytes)

Remember: payload fields are **little-endian**.

```
Offset  Bytes                  Field
0       00 00 00 00            timestamp_ms = 0 (unused for SET)
4       8F 01 00 00            can_id = 0x18F (little-endian!)
8       00                     is_extended = 0 (standard 11-bit ID)
9       00                     is_fd = 0 (Classic CAN)
10      00                     brs = 0
11      00                     esi = 0
12      08                     dlc = 8
13      08                     data_len = 8
14      00 00                  reserved
16      11 22 33 44 55 66 77 88  data[0..7]
24      00 00 ... (56 bytes)   data[8..63] = 0
```

### Step 2 — wrap it (11-byte header, big-endian)

```
AA                                        header (sync)
01                                        action = WRITE_SET
00 10 00 01                               command = 0x00100001 (CAN_SET_FRAME)
00                                        result = REQUEST
00 00 00 50                               length = 80 (0x50, big-endian)
```

### Step 3 — the exact bytes to write to `/dev/ttyRPMSG30`

One single `write()` of 91 bytes (11-byte header + 80-byte payload):

```
AA 01 00 10 00 01 00 00 00 00 50                          <- 11-byte header
00 00 00 00 8F 01 00 00 00 00 00 00 08 08 00 00           <- timestamp, can_id (LE), flags, dlc, data_len, reserved
11 22 33 44 55 66 77 88                                   <- data[0..7]
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00           <- data[8..63] = 56 zero bytes
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 00
```

### Step 4 — read the ACK (11 bytes)

The M7 answers within a few milliseconds:

```
AA 01 00 10 00 01 01 00 00 00 00
```

- action `01` = WRITE_SET (echoed), command `0x00100001` (echoed)
- result `01` = SUCCESS: the M7 accepted the command and queued it for CAN1
  transmission. (If no CAN device is connected, the CAN transmit itself
  fails — that is only visible on the M7 debug UART, not in the ACK.)
- result `FF` = FAILURE: the frame was rejected (e.g. wrong payload length).
- length = 0: no payload.

### Same example with the module helpers

```python
os.write(fd, wrap(WRITE_SET, SET_FRAME, REQUEST,
                  can_msg(0x18F, False, False, False, 8,
                          bytes([0x11,0x22,0x33,0x44,0x55,0x66,0x77,0x88]))))
```
