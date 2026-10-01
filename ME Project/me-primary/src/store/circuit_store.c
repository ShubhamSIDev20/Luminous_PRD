/*
 * circuit_store.c - per-circuit storage.
 *
 * FOOTPRINT: g_program is ME_MAX_CIRCUITS x ME_PROGRAM_BUF_SIZE - 128 MB at
 * the production 2 MB setting. That is .bss, which Linux reserves as
 * demand-zero pages: address space, not physical memory, until a page is
 * written. The binary does not grow. build-native.ps1 compiles with
 * -DME_PROGRAM_BUF_SIZE=65536 because Windows commits .bss where Linux does
 * not.
 */
#include "circuit_store.h"

#include <string.h>

#include "../util/log.h"

static uint8_t  g_program[ME_MAX_CIRCUITS][ME_PROGRAM_BUF_SIZE];
static uint32_t g_program_len[ME_MAX_CIRCUITS];
static bool     g_program_complete[ME_MAX_CIRCUITS];

static me_battery_t        g_battery[ME_MAX_CIRCUITS];
static me_circuit_config_t g_config[ME_MAX_CIRCUITS];

int me_circuit_slot(uint8_t circuit_id)
{
    const uint8_t sec = ME_CIRCUIT_SECONDARY(circuit_id);
    const uint8_t ch  = ME_CIRCUIT_CHANNEL(circuit_id);

    /* 1-based on both nibbles. 0 is not "the first one", it is malformed. */
    if (sec < 1u || sec > ME_MAX_SECONDARIES || ch < 1u || ch > ME_MAX_CHANNELS) {
        return ME_SLOT_INVALID;
    }
    return (int)(((uint32_t)(sec - 1u) * ME_MAX_CHANNELS) + (ch - 1u));
}

uint8_t me_circuit_from_slot(int slot)
{
    if (slot < 0 || slot >= (int)ME_MAX_CIRCUITS) {
        return 0;
    }
    const uint8_t sec = (uint8_t)((uint32_t)slot / ME_MAX_CHANNELS + 1u);
    const uint8_t ch  = (uint8_t)((uint32_t)slot % ME_MAX_CHANNELS + 1u);
    return ME_CIRCUIT_ID(sec, ch);
}

void me_store_init(void)
{
    memset(g_program_len,      0, sizeof(g_program_len));
    memset(g_program_complete, 0, sizeof(g_program_complete));
    memset(g_battery,          0, sizeof(g_battery));
    memset(g_config,           0, sizeof(g_config));
    /* g_program itself is left alone on purpose - see the file header. */
}

/* Log once and return the slot, or -1. */
static int slot_or_warn(uint8_t circuit_id, const char *what)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID) {
        ME_LOGW("store: %s rejected - CircuitID 0x%02X is not a valid "
                "secondary(1-%u)/channel(1-%u) pair",
                what, circuit_id,
                (unsigned)ME_MAX_SECONDARIES, (unsigned)ME_MAX_CHANNELS);
    }
    return slot;
}

bool me_store_program_append(uint8_t circuit_id, const uint8_t *data,
                             uint32_t len)
{
    const int slot = slot_or_warn(circuit_id, "program append");
    if (slot == ME_SLOT_INVALID || data == NULL || len == 0u) {
        return false;
    }

    /* A packet arriving for a finished program starts a new one. Concatenating
     * would leave the new chain's absolute offsets pointing into the old. */
    if (g_program_complete[slot]) {
        ME_LOGI("store: circuit 0x%02X - new program replacing the previous "
                "%u-byte one", circuit_id, (unsigned)g_program_len[slot]);
        g_program_len[slot]      = 0;
        g_program_complete[slot] = false;
    }

    if (len > ME_PROGRAM_BUF_SIZE
        || g_program_len[slot] > (ME_PROGRAM_BUF_SIZE - len)) {
        ME_LOGE("store: circuit 0x%02X - program append of %u bytes would "
                "exceed the %u-byte buffer (%u already stored); packet dropped",
                circuit_id, (unsigned)len, (unsigned)ME_PROGRAM_BUF_SIZE,
                (unsigned)g_program_len[slot]);
        return false;
    }

    memcpy(&g_program[slot][g_program_len[slot]], data, len);
    g_program_len[slot] += len;

    if (me_chain_is_complete(g_program[slot], g_program_len[slot])) {
        g_program_complete[slot] = true;
        ME_LOGI("store: circuit 0x%02X - program COMPLETE: %u step(s), "
                "%u bytes",
                circuit_id,
                (unsigned)me_chain_count_steps(g_program[slot],
                                               g_program_len[slot]),
                (unsigned)g_program_len[slot]);
    }
    return true;
}

void me_store_program_reset(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID) {
        return;
    }
    g_program_len[slot]      = 0;
    g_program_complete[slot] = false;
}

bool me_store_program_is_complete(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    return (slot != ME_SLOT_INVALID) && g_program_complete[slot];
}

uint32_t me_store_program_len(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    return (slot == ME_SLOT_INVALID) ? 0u : g_program_len[slot];
}

const uint8_t *me_store_program_ptr(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    return (slot == ME_SLOT_INVALID) ? NULL : g_program[slot];
}

bool me_store_battery_set(uint8_t circuit_id, const me_battery_t *b)
{
    const int slot = slot_or_warn(circuit_id, "battery store");
    if (slot == ME_SLOT_INVALID || b == NULL) {
        return false;
    }
    g_battery[slot]       = *b;
    g_battery[slot].valid = true;
    return true;
}

bool me_store_battery_get(uint8_t circuit_id, me_battery_t *out)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID || !g_battery[slot].valid) {
        return false;
    }
    *out = g_battery[slot];
    return true;
}

bool me_store_config_set(uint8_t circuit_id, uint8_t query_id,
                         const uint8_t *data, uint32_t len)
{
    const int slot = slot_or_warn(circuit_id, "config store");
    if (slot == ME_SLOT_INVALID || data == NULL) {
        return false;
    }
    if (len > ME_CONFIG_RAW_MAX) {
        ME_LOGW("store: circuit 0x%02X - config query 0x%02X payload is %u "
                "bytes, keeping the first %u",
                circuit_id, query_id, (unsigned)len,
                (unsigned)ME_CONFIG_RAW_MAX);
        len = ME_CONFIG_RAW_MAX;
    }
    g_config[slot].valid    = true;
    g_config[slot].query_id = query_id;
    g_config[slot].len      = len;
    memcpy(g_config[slot].raw, data, len);
    return true;
}

bool me_store_config_get(uint8_t circuit_id, me_circuit_config_t *out)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID || !g_config[slot].valid) {
        return false;
    }
    *out = g_config[slot];
    return true;
}
