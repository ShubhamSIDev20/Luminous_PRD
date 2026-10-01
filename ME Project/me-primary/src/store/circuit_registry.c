/*
 * circuit_registry.c - per-circuit registration gate. See circuit_registry.h
 * for why this is a separate table from circuit_store.c.
 */
#include "circuit_registry.h"

#include <string.h>

#include "circuit_store.h"
#include "../proto/proto_defs.h"

static bool g_registered[ME_MAX_CIRCUITS];

void me_registry_init(void)
{
    memset(g_registered, 0, sizeof(g_registered));
}

bool me_registry_mark_registered(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID) {
        return false;
    }
    g_registered[slot] = true;
    return true;
}

bool me_registry_is_registered(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    return (slot != ME_SLOT_INVALID) && g_registered[slot];
}
