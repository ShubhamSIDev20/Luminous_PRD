/*
 * circuit_store.h - per-circuit persistent storage, owned by the Data Manager.
 *
 * PURE LOGIC: no sockets, no threads, no platform headers. It is built by the
 * host test suite, so the storage rules - bounds, reset-on-resend, completion -
 * are proven on the laptop rather than discovered on hardware.
 *
 * NOT THREAD-SAFE, deliberately. Only the Data Manager thread touches these
 * functions; every other thread reaches them through a message. Adding a mutex
 * would invite a second caller and undo that.
 */
#ifndef ME_CIRCUIT_STORE_H
#define ME_CIRCUIT_STORE_H

#include <stdbool.h>
#include <stdint.h>

#include "../proto/battery_frame.h"
#include "../proto/program_chain.h"
#include "../proto/proto_defs.h"

#define ME_SLOT_INVALID (-1)

/* Largest 0xAA configuration payload retained verbatim. Nothing parses these
 * yet - they are kept whole so the read-back queries can be answered later
 * without a second round trip to the Web Application. */
#define ME_CONFIG_RAW_MAX 256u

/*
 * NAME: me_circuit_config_t, NOT me_config_t. sys_init.h already owns
 * me_config_t for the command-line configuration, and two different structs
 * with that name in one program is a merge conflict waiting to happen.
 */
typedef struct {
    bool     valid;
    uint8_t  query_id;
    uint32_t len;
    uint8_t  raw[ME_CONFIG_RAW_MAX];
} me_circuit_config_t;

/*
 * CircuitID -> storage slot. Both nibbles are 1-BASED, so 0x11 is the first
 * circuit and maps to slot 0.
 *
 * Returns ME_SLOT_INVALID for a malformed ID. It must never fold to slot 0:
 * a bad CircuitID quietly overwriting Secondary 1 Channel 1's program is a
 * corruption with no error, no log and no symptom until the wrong test runs.
 */
int     me_circuit_slot(uint8_t circuit_id);
uint8_t me_circuit_from_slot(int slot);

/* Clear all metadata. Does NOT clear the program buffers themselves - that
 * would touch every page of 128 MB and turn reserved address space into
 * committed memory for no benefit. */
void me_store_init(void);

/* ------------------------------------------------------------- program --- */

/*
 * Append one 0xBB Q4 step-data payload.
 *
 * Appends CONTIGUOUSLY from offset 0, with nothing inserted between packets:
 * the chain's nextIndex values are ABSOLUTE offsets into this buffer, so any
 * inserted framing would invalidate every one of them.
 *
 * If the circuit's program was already complete, this starts a new one - a
 * re-sent program replaces the old rather than being concatenated onto it.
 *
 * Returns false on a malformed CircuitID or if the append would overflow
 * ME_PROGRAM_BUF_SIZE, leaving the stored program untouched in both cases.
 */
bool me_store_program_append(uint8_t circuit_id, const uint8_t *data, uint32_t len);

void           me_store_program_reset(uint8_t circuit_id);
bool           me_store_program_is_complete(uint8_t circuit_id);
uint32_t       me_store_program_len(uint8_t circuit_id);
const uint8_t *me_store_program_ptr(uint8_t circuit_id); /* NULL if invalid */

/* ------------------------------------------------------------- battery --- */

bool me_store_battery_set(uint8_t circuit_id, const me_battery_t *b);
/* Returns false when the CircuitID is bad OR when nothing has been stored. */
bool me_store_battery_get(uint8_t circuit_id, me_battery_t *out);

/* -------------------------------------------------------------- config --- */

bool me_store_config_set(uint8_t circuit_id, uint8_t query_id,
                         const uint8_t *data, uint32_t len);
bool me_store_config_get(uint8_t circuit_id, me_circuit_config_t *out);

#endif /* ME_CIRCUIT_STORE_H */
