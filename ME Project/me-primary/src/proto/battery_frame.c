/*
 * battery_frame.c - 0xAA battery-information payload parsing.
 */
#include "battery_frame.h"

#include <string.h>

#include "realtime_frame.h" /* me_get_f32_be - one big-endian float decoder */

/* Battery ID is the only 16-bit field in the payload. realtime_frame.c has a
 * put_u16_be but keeps it private, and one two-line reader here is cheaper than
 * widening that module's interface for a single caller. */
static uint16_t get_u16_be(const uint8_t *src)
{
    return (uint16_t)(((uint16_t)src[0] << 8) | (uint16_t)src[1]);
}

bool me_battery_parse(const uint8_t *payload, uint32_t len, me_battery_t *out)
{
    memset(out, 0, sizeof(*out));

    if (payload == NULL || len < ME_BATTERY_PAYLOAD_LEN) {
        return false;
    }

    out->nom_capacity          = me_get_f32_be(&payload[ME_BAT_OFF_NOM_CAPACITY]);
    out->no_of_cells           = payload[ME_BAT_OFF_NO_OF_CELLS];
    out->gassing_voltage       = me_get_f32_be(&payload[ME_BAT_OFF_GASSING_VOLTAGE]);
    out->max_voltage           = me_get_f32_be(&payload[ME_BAT_OFF_MAX_VOLTAGE]);
    out->nom_current           = me_get_f32_be(&payload[ME_BAT_OFF_NOM_CURRENT]);
    out->cold_cranking_current = me_get_f32_be(&payload[ME_BAT_OFF_COLD_CRANKING]);
    out->charge_factor         = payload[ME_BAT_OFF_CHARGE_FACTOR];

    /* Offsets 22-39. Discarded entirely until 2026-08-12 - see the header. */
    out->impedance             = me_get_f32_be(&payload[ME_BAT_OFF_IMPEDANCE]);
    out->break_voltage         = me_get_f32_be(&payload[ME_BAT_OFF_BREAK_VOLTAGE]);
    out->nom_voltage           = me_get_f32_be(&payload[ME_BAT_OFF_NOM_VOLTAGE]);
    out->energy_density        = me_get_f32_be(&payload[ME_BAT_OFF_ENERGY_DENSITY]);
    out->battery_id            = get_u16_be(&payload[ME_BAT_OFF_BATTERY_ID]);

    out->valid                 = true;

    return true;
}
