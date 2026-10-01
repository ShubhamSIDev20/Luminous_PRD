/*
 * msg.h - the one message type that crosses every thread boundary.
 *
 * PURE: no sockets, no platform headers, no threads. Both the queue and every
 * thread include this, and the host test build compiles it unchanged.
 *
 * One struct, discriminated by `type`, rather than a union per direction. A
 * single shape means a queue slot is a fixed size, which is what lets the whole
 * system run with no dynamic allocation: every queue is a static array.
 *
 * See Docs/ME_Primary_BTS_Block_Diagram.md section 3 for the routing table.
 */
#ifndef ME_MSG_H
#define ME_MSG_H

#include <stdint.h>

/*
 * Largest payload a single message can carry. Sized so that no inbound TCP
 * frame and no outbound program chunk needs splitting at this layer:
 * a 0xBB Q4 program packet carries a 16-bit length field, so 64 KB covers the
 * largest frame the Web Application can describe.
 *
 * A slot is therefore ~64 KB and a depth-16 queue ~1 MB. That is .bss, which
 * Linux reserves without committing, and send/recv copy only `len` bytes - so
 * a 6-byte control frame does not cost a 64 KB memcpy.
 */
#ifndef ME_MSG_PAYLOAD_MAX
#define ME_MSG_PAYLOAD_MAX (64u * 1024u)
#endif

typedef enum {
    ME_MSG_NONE = 0,

    /* Communication -> Data Manager */
    ME_MSG_STORE_PROGRAM,     /* one 0xBB Q4 step-data payload               */
    ME_MSG_STORE_BATTERY,     /* 0xAA battery-info payload                   */
    ME_MSG_STORE_CONFIG,      /* 0xAA configuration payload                  */

    /* Communication -> Core Logic */
    ME_MSG_CONTROL,           /* whole 0xEE frame                            */

    /* Core Logic -> Data Manager */
    ME_MSG_REQ_PROGRAM,
    ME_MSG_REQ_BATTERY,

    /* Data Manager -> Core Logic */
    ME_MSG_RSP_PROGRAM_CHUNK, /* offset + len, ME_MSG_FLAG_LAST on the last  */
    ME_MSG_RSP_BATTERY,
    ME_MSG_RSP_NOT_FOUND,     /* status says which request failed, and why   */

    /* Core Logic -> Communication */
    ME_MSG_REALTIME_DATA,     /* 0xCC frame -> UDP 10000                     */

    /* Core Logic -> Data Manager -> Communication */
    ME_MSG_SESSION_DATA,      /* 0xCC frame -> UDP 10001                     */

    /* Core Logic <-> CAN Data Manager - provision, no hardware yet */
    ME_MSG_CAN_TX,
    ME_MSG_CAN_DATA,

    ME_MSG_TYPE_COUNT
} me_msg_type_t;

typedef enum {
    ME_STATUS_OK = 0,
    ME_STATUS_NO_PROGRAM,         /* nothing stored for that circuit         */
    ME_STATUS_PROGRAM_INCOMPLETE, /* stored, but no terminator seen yet      */
    ME_STATUS_PROGRAM_INVALID,    /* chain walk failed on it                 */
    ME_STATUS_NO_BATTERY,         /* no battery data stored                  */
    ME_STATUS_BAD_CIRCUIT,        /* CircuitID did not map to a slot         */
    ME_STATUS_COUNT
} me_msg_status_t;

/* Final chunk of a multi-part transfer. */
#define ME_MSG_FLAG_LAST 0x01u

typedef struct {
    me_msg_type_t type;
    uint8_t       circuit_id; /* raw byte: 0x11 = Secondary 1, Channel 1     */
    uint8_t       flags;      /* ME_MSG_FLAG_*                               */
    uint16_t      status;     /* me_msg_status_t; 0 = OK                     */
    uint32_t      offset;     /* byte offset, for chunked transfers          */
    uint32_t      len;        /* valid bytes in payload[]                    */
    uint8_t       payload[ME_MSG_PAYLOAD_MAX];
} me_msg_t;

const char *me_msg_type_name(me_msg_type_t t);
const char *me_msg_status_name(me_msg_status_t s);

#endif /* ME_MSG_H */
