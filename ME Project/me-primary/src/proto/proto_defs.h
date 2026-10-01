/*
 * proto_defs.h - single source of truth for the ME Primary <-> Web Application
 *                wire protocol (IF-A).
 *
 * NO magic numbers for this protocol may appear anywhere else in the codebase.
 *
 * Source: Docs/bm_device_registration_v5.0.md, cross-checked against
 *         ME Workspace/Reference Documents/WebAppDocs/ICD.md section 3.4.
 *
 * Where the two disagree, bm_device_registration_v5.0 wins - see
 * Docs/specs/2026-08-07-me-primary-registration-design.md section 2.
 */
#ifndef ME_PROTO_DEFS_H
#define ME_PROTO_DEFS_H

#include <stdint.h>

/* ---------------------------------------------------------------- ports -- */

#define ME_PORT_TCP_CMD      9999  /* command + response, board is the client */
#define ME_PORT_UDP_LIVE     10000 /* real-time data   (board -> server)      */
#define ME_PORT_UDP_SESSION  10001 /* session store    (board -> server)      */

/* --------------------------------------------------------- command group -- */

#define ME_START_REGISTRATION 0xDDu
#define ME_START_CONFIG       0xAAu
#define ME_START_PROGRAM      0xBBu
#define ME_START_REALTIME     0xCCu
#define ME_START_CONTROL      0xEEu
#define ME_START_CALIBRATION  0xA0u

/* Query IDs within the 0xDD registration group. */
#define ME_QID_REGISTER       0x01u
#define ME_QID_DELETE         0x02u
#define ME_QID_DISCOVER       0x03u
#define ME_QID_IP_CONFIG      0x04u

/*
 * Every 0xAA/0xBB/0xCC/0xEE frame shares the same four-byte header:
 *
 *     Start | DeviceNumber | CircuitNumber | QueryID | ... | CRC[2]
 *
 * (0xDD registration is the exception - its QueryID is byte 1 and it carries a
 *  Length byte. See the registration layout below.)
 */
#define ME_HDR_OFF_START     0u
#define ME_HDR_OFF_DEVICE    1u
#define ME_HDR_OFF_CIRCUIT   2u
#define ME_HDR_OFF_QUERY_ID  3u
#define ME_HDR_LEN           4u

/* Smallest frame that can exist: header + CRC. */
#define ME_FRAME_MIN_LEN (ME_HDR_LEN + ME_REG_CRC_LEN)

/*
 * Query IDs within the 0xAA configuration group.
 * Source: Ref Docs/bm_config_v6.0.md section 2 (transcribed 2026-08-12 from
 * "Config Data Frame Format V6.0.xlsx"). These were previously taken from
 * ConfigQueryID_t in the old BTS_Primary_SOM/src/netDataHandler.h and carried a
 * "NOT specification-backed" warning; the document confirms all six unchanged.
 */
#define ME_QID_CFG_IS_READY           0x01u
#define ME_QID_CFG_READ_FACTORY       0x02u
#define ME_QID_CFG_READ_MANUFACTURING 0x03u
#define ME_QID_CFG_READ_BATTERY       0x04u
#define ME_QID_CFG_WRITE_BATTERY      0x05u
#define ME_QID_CFG_SYNC_TIME          0x06u

/*
 * Q7-Q10 exist and are DELIBERATELY NOT DEFINED here. They are broadcast frames
 * with a different, 2-BYTE header - Start | QueryID, with no DeviceNumber and no
 * CircuitNumber (bm_config_v6.0.md section 9.1). Every 0xAA path in this codebase
 * assumes the 4-byte header, so giving them names would invite exactly the
 * mis-parse the document warns about. Today a broadcast frame reads as
 * CircuitID 0x01, which me_circuit_slot() rejects as malformed, so it is dropped
 * - safe, but by accident rather than by design.
 */

/*
 * 0xAA frame lengths. The protocol encodes no length field anywhere in the
 * group, but the LAYOUT fixes each query's size, which is what lets
 * me_frame_expected_len() give the reassembler a real boundary.
 *
 *   Q1-Q4  header + CRC, no payload            =  6
 *   Q5     header + 40-byte battery + CRC      = 46  (ME_BATTERY_FRAME_LEN)
 *   Q6     header + 4-byte epoch + CRC         = 10
 */
#define ME_CFG_EPOCH_LEN     4u
#define ME_CFG_OFF_EPOCH     ME_HDR_LEN
#define ME_CFG_BARE_LEN      (ME_HDR_LEN + ME_REG_CRC_LEN)
#define ME_CFG_SYNC_LEN      (ME_HDR_LEN + ME_CFG_EPOCH_LEN + ME_REG_CRC_LEN)

/* Query IDs within the 0xBB program group.
 * Source: Ref Docs/bm_program_v3.0.md.
 *
 * Q3 (number of packets) is ACKNOWLEDGED BUT NOT ACTED ON: the Web Application
 * sends program packets directly and completion is detected from the chain
 * terminator, so the count carries no information this board needs. It is still
 * answered, because the Web Application does send it - observed on hardware
 * 2026-08-12. */
#define ME_QID_PRG_IS_READY       0x01u
#define ME_QID_PRG_METADATA       0x02u
#define ME_QID_PRG_PACKET_COUNT   0x03u /* answered, then discarded */
#define ME_QID_PROGRAM_DATA       0x04u
#define ME_QID_PRG_READ_METADATA  0x05u
#define ME_QID_PRG_READ_PROGRAM   0x06u

/* The 0xBB Q4 frame inserts a 16-bit big-endian payload length after the
 * header, before the step bytes. */
#define ME_PRG_OFF_LENGTH  ME_HDR_LEN
#define ME_PRG_LENGTH_LEN  2u
#define ME_PRG_OFF_STEPS   (ME_PRG_OFF_LENGTH + ME_PRG_LENGTH_LEN)

/*
 * Board -> Web Application acknowledgement. ONE shape for the whole protocol:
 *
 *   Start | DeviceNumber | CircuitNumber | QueryID | Value | CRC[2]  (7 bytes)
 *
 * The Start byte and QueryID are echoed from the frame being answered, so this
 * single layout serves every group that needs a simple yes/no reply:
 *   0xAA Q5 battery write
 *   0xBB Q1 is-ready, Q3 packet count, Q4 per-packet ack
 *   0xEE all six control commands
 *
 * Both source documents agree on the shape:
 *   bm_program_v3.0.md - "Response ACK per step: BB 01 01 04 01 -- --"
 *   bm_control_v3.0.md - "Response (OK): EE 01 01 01 01 -- --"
 * There is NO 0xAA document; that shape is inferred from these two.
 */
#define ME_ACK_OFF_VALUE ME_HDR_LEN
#define ME_ACK_LEN       (ME_HDR_LEN + 1u + ME_REG_CRC_LEN)

/*
 * Value byte for every acknowledgement: 0x01 = OK/Success, 0x00 = Fail.
 *
 * Deliberately NOT reusing ME_REG_VALUE_* below. Those belong to the 0xDD
 * group, where 0x02 (Already Registered) also counts as success - no other
 * group carries that meaning, and sharing the names would invite
 * ME_REG_VALUE_IS_SUCCESS() to be applied where it does not belong.
 */
#define ME_ACK_VALUE_FAIL 0x00u
#define ME_ACK_VALUE_OK   0x01u

/*
 * Q1 is-ready carries no payload, so it is header + CRC - the same 6-byte
 * shape as a payload-less 0xEE control frame.
 * Q3 carries a 16-bit big-endian count: the document's example is
 * "BB 01 01 03 00 03 -- --" for three packets.
 */
#define ME_PRG_Q1_LEN        (ME_HDR_LEN + ME_REG_CRC_LEN)
#define ME_PRG_OFF_PKT_COUNT ME_HDR_LEN
#define ME_PRG_PKT_COUNT_LEN 2u
#define ME_PRG_Q3_LEN                                                         \
    (ME_HDR_LEN + ME_PRG_PKT_COUNT_LEN + ME_REG_CRC_LEN)

/* Query IDs within the 0xEE control group.
 * Source: Ref Docs/bm_control_v3.0.md. */
#define ME_CTRL_START      0x01u
#define ME_CTRL_STOP       0x02u
#define ME_CTRL_PAUSE      0x03u
#define ME_CTRL_CONTINUE   0x04u
#define ME_CTRL_SYNC_TIME  0x05u
#define ME_CTRL_RESET      0x06u
#define ME_CTRL_EPOCH_LEN  4u

/*
 * 0xEE payloads. Most commands carry none; two carry four bytes.
 *
 *   Q1 Start     : EE | dev | ckt | 01 | SessionID[4] | CRC[2]   10 bytes
 *   Q5 Sync Time : EE | dev | ckt | 05 | Epoch[4]     | CRC[2]   10 bytes
 *   Q2/Q3/Q4/Q6  : EE | dev | ckt | qq |              | CRC[2]    6 bytes
 *
 * ⚠️ THE SESSION ID WAS NOT IN bm_control_v3.0.md's original table. It was found
 * on hardware 2026-08-12: a 10-byte Start frame was split at 6 bytes, its CRC
 * computed over the wrong span, and rejected as BAD_CRC while being perfectly
 * valid. The document is now annotated, but the wire is the authority here.
 *
 * Both payload-carrying commands happen to be 10 bytes, so the two constants
 * below are equal today. They are kept separate anyway: they describe different
 * fields, and collapsing them would hide it if one ever changed.
 */
#define ME_CTRL_SESSION_ID_LEN 4u
#define ME_CTRL_OFF_SESSION_ID ME_HDR_LEN
#define ME_CTRL_OFF_EPOCH      ME_HDR_LEN

#define ME_CTRL_BARE_LEN  (ME_HDR_LEN + ME_REG_CRC_LEN)
#define ME_CTRL_START_LEN (ME_HDR_LEN + ME_CTRL_SESSION_ID_LEN + ME_REG_CRC_LEN)
#define ME_CTRL_SYNC_LEN  (ME_HDR_LEN + ME_CTRL_EPOCH_LEN + ME_REG_CRC_LEN)

/* Query IDs within the 0xCC measured-parameter group.
 * Source: Docs/Ref Docs/bm_measured_param_v5.2.md. */
#define ME_QID_REALTIME  0x01u /* UDP 10000 live data, and UDP 10001 session */
#define ME_QID_CAN_DATA  0x02u /* UDP 10000 CAN data                          */

/* ------------------------------------------------- program step chain --- */
/*
 * A battery-testing program is a singly-linked list embedded in a byte array.
 * Each step names the ABSOLUTE offset of the next one; 0xFFFFFFFF ends it.
 *
 *  off  0     1     2  3  4  5     6  7     8      ...     n-2  n-1
 *      +-----+-----+-----------+---------+-----+-----------+----+----+
 *      | AA  | 55  | nextIndex | stepNo  | op  | step data | 55 | AA |
 *      +-----+-----+-----------+---------+-----+-----------+----+----+
 *
 * WARNING: ME_STEP_START_1 is 0xAA, the same value as ME_START_CONFIG. They are
 * separate namespaces - a step packet never appears on the wire on its own, and
 * a config frame never appears inside a program buffer - but the names are kept
 * distinct so the collision stays harmless.
 */
#define ME_STEP_START_1     0xAAu
#define ME_STEP_START_2     0x55u
#define ME_STEP_END_1       0x55u
#define ME_STEP_END_2       0xAAu
#define ME_STEP_TERMINATOR  0xFFFFFFFFu

#define ME_STEP_OFF_START    0u
#define ME_STEP_OFF_NEXT     2u
#define ME_STEP_OFF_STEP_NO  6u
#define ME_STEP_OFF_OPERATOR 8u

/* Bytes needed before the operator field can be read. */
#define ME_STEP_HEADER_LEN 9u
/* Smallest packet that can exist: header plus the two end-sequence bytes. */
#define ME_STEP_MIN_LEN (ME_STEP_HEADER_LEN + 2u)

/* ------------------------------------------------------ capacity ------- */
/*
 * CircuitID upper nibble = Secondary, lower nibble = Channel, both 1-based.
 * 8 x 8 = 64 circuits.
 */
#define ME_MAX_SECONDARIES 8u
#define ME_MAX_CHANNELS    8u
#define ME_MAX_CIRCUITS    (ME_MAX_SECONDARIES * ME_MAX_CHANNELS)

/*
 * Bytes reserved per circuit for a battery-testing program.
 *
 * 64 x 2 MB = 128 MB of .bss in the Data Manager, mirrored in Core Logic.
 * On Linux that is demand-zero: address space, not physical memory, until a
 * page is written. build-native.ps1 overrides this to 64 KB, because Windows
 * commits .bss where Linux only reserves it.
 */
#ifndef ME_PROGRAM_BUF_SIZE
#define ME_PROGRAM_BUF_SIZE (2u * 1024u * 1024u)
#endif

/* Response Value byte (0xDD Q1). */
#define ME_REG_VALUE_FAILED             0x00u
#define ME_REG_VALUE_REGISTERED         0x01u
#define ME_REG_VALUE_ALREADY_REGISTERED 0x02u

/*
 * Both 0x01 (Registered) and 0x02 (Already Registered) mean the device IS
 * registered - 0x02 only adds that the Web Application already held a record
 * for it. Only 0x00 (Failed) and unrecognised values are rejections.
 *
 * This is not a hypothetical distinction: treating 0x02 as a rejection made
 * the board close the connection and re-register every 30 seconds, observed
 * on hardware 2026-08-10.
 */
#define ME_REG_VALUE_IS_SUCCESS(v)                                            \
    ((uint8_t)(v) == ME_REG_VALUE_REGISTERED ||                               \
     (uint8_t)(v) == ME_REG_VALUE_ALREADY_REGISTERED)

/* ------------------------------------------- registration request layout -- */
/*
 *  off  size  field
 *    0     1  Start        0xDD
 *    1     1  QueryID      0x01
 *    2     1  Length       payload bytes, DeviceID..MAC inclusive (28)
 *    3     1  DeviceID
 *    4     1  CircuitID    upper nibble = Secondary, lower nibble = Channel
 *    5    16  DeviceName   ASCII, null-padded
 *   21     4  IPAddress    dotted-quad order, NOT a little-endian uint32
 *   25     6  MACAddress   wire order
 *   31     2  CRC-16/Modbus over bytes 0..30
 *  total 33
 */
#define ME_REG_OFF_START      0u
#define ME_REG_OFF_QUERY_ID   1u
#define ME_REG_OFF_LENGTH     2u
#define ME_REG_OFF_DEVICE_ID  3u
#define ME_REG_OFF_CIRCUIT_ID 4u
#define ME_REG_OFF_NAME       5u
#define ME_REG_OFF_IP         21u
#define ME_REG_OFF_MAC        25u
#define ME_REG_OFF_CRC        31u

#define ME_REG_NAME_LEN 16u
#define ME_REG_IP_LEN   4u
#define ME_REG_MAC_LEN  6u
#define ME_REG_CRC_LEN  2u

#define ME_REG_HEADER_LEN 3u /* Start + QueryID + Length */

/*
 * The Length byte is COMPUTED from the field sizes, never hardcoded as 0x1C.
 * If a field size ever changes, the length byte follows automatically instead
 * of silently disagreeing with the frame it describes.
 */
#define ME_REG_PAYLOAD_LEN                                                    \
    (1u /* DeviceID */ + 1u /* CircuitID */                                   \
     + ME_REG_NAME_LEN + ME_REG_IP_LEN + ME_REG_MAC_LEN)

#define ME_REG_REQUEST_LEN                                                    \
    (ME_REG_HEADER_LEN + ME_REG_PAYLOAD_LEN + ME_REG_CRC_LEN)

/* ------------------------------------------ registration response layout -- */
/*
 *  off  size  field
 *    0     1  Start        0xDD
 *    1     1  QueryID      0x01
 *    2     1  DeviceID     echo
 *    3     1  CircuitID    echo
 *    4     1  Value        0x01 = registered
 *    5     2  CRC-16 over bytes 0..4
 *  total 7
 *
 * NOTE: ICD.md section 3.4 documents this as 5 bytes with no CRC. That is
 * incorrect; bm_device_registration_v5.0 shows "DD 01 01 01 01 -- --".
 */
#define ME_RSP_OFF_START      0u
#define ME_RSP_OFF_QUERY_ID   1u
#define ME_RSP_OFF_DEVICE_ID  2u
#define ME_RSP_OFF_CIRCUIT_ID 3u
#define ME_RSP_OFF_VALUE      4u
#define ME_RSP_OFF_CRC        5u

#define ME_REG_RESPONSE_BODY_LEN 5u
#define ME_REG_RESPONSE_LEN      (ME_REG_RESPONSE_BODY_LEN + ME_REG_CRC_LEN)

/* --------------------------------------------------- CircuitID encoding -- */
/*
 *   bit 7 6 5 4   3 2 1 0
 *      +-------+---------+
 *      |  Sec  | Channel |     0x11 -> Secondary 1, Channel 1
 *      +-------+---------+
 *
 * ME redefinition of the legacy per-device circuit number: the upper nibble
 * identifies the Secondary (DC-DC) board, the lower nibble the channel on it.
 */
#define ME_CIRCUIT_ID(sec, ch)                                                \
    ((uint8_t)((((uint8_t)(sec) & 0x0Fu) << 4) | ((uint8_t)(ch) & 0x0Fu)))

#define ME_CIRCUIT_SECONDARY(id) ((uint8_t)(((uint8_t)(id) >> 4) & 0x0Fu))
#define ME_CIRCUIT_CHANNEL(id)   ((uint8_t)((uint8_t)(id) & 0x0Fu))

#endif /* ME_PROTO_DEFS_H */
