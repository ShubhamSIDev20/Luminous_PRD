/*
 * battery_frame.h - 0xAA Q5 battery-information payload.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers.
 *
 * PROVENANCE:
 * Ref Docs/bm_config_v6.0.md section 5.3, transcribed 2026-08-12 from
 * "Config Data Frame Format V6.0.xlsx" sheet "Battery Data".
 *
 * Section 5.4 of that document confirms this layout against a frame CAPTURED
 * FROM THE ME WEB APPLICATION - all twelve field boundaries land where the
 * specification says, the total is exactly 46 bytes, and the CRC verifies. The
 * offsets below are therefore specification-backed AND wire-confirmed, which is
 * as strong as this project's evidence gets short of a hardware run.
 *
 * HISTORY - why the payload length changed:
 * Until 2026-08-12 this file carried the warning "There is NO
 * battery/configuration specification in Ref Docs" and its offsets were
 * reverse-engineered from storeBatteryData() in the legacy
 * BTS_Primary_SOM/src/networkDataHandler.c. That layout stopped after Charge
 * Factor at 22 bytes. The Web Application sends 40. Because the parser
 * deliberately "tolerated a longer payload", the last 18 bytes - five real
 * fields - were silently discarded on every battery packet. The first seven
 * offsets were correct; they were simply incomplete.
 *
 * Payload layout (offsets are from the first byte AFTER the 4-byte header):
 *
 *    off  size  field                    ID    unit
 *      0     4  nominal capacity       0x65    Ah      float BE
 *      4     1  number of cells        0x66            uint8
 *      5     4  gassing voltage        0x67    V       float BE
 *      9     4  maximum voltage        0x68    V       float BE
 *     13     4  nominal current        0x69    A       float BE
 *     17     4  cold cranking current  0x6A    A       float BE
 *     21     1  charge factor          0x6B    %       uint8
 *     22     4  impedance              0x6C    Ohm     float BE  (see note)
 *     26     4  break voltage          0x6D    V       float BE
 *     30     4  nominal voltage        0x6E    V       float BE
 *     34     4  energy density         0x6F    Wh/Kg   float BE  (see note)
 *     38     2  battery ID             (0x70)          uint16 BE
 *   total  40
 *
 * The per-field ID bytes are NOT transmitted in Q5 - the payload is a
 * fixed-order concatenation of values only (bm_config_v6.0.md section 5.2). The
 * IDs are recorded here for cross-reference with that document. Battery ID's own
 * ID is parenthesised because the spreadsheet gives it as 0x6B, colliding with
 * charge factor; 0x70 is the obvious intent (section 9.4).
 *
 * NOTE on impedance and energy density (bm_config_v6.0.md section 9.3):
 * these are the only two 4-byte battery fields the source spreadsheet does NOT
 * annotate "It will be in float", and its samples decode sensibly only as
 * uint32. Observed traffic settled it the other way - the Web Application sent
 * 3F 80 00 00 (float 1.0) for a field the operator set to 1 - and the developer
 * CONFIRMED float on 2026-08-12, so the spreadsheet samples are simply wrong.
 * Both are float here, pinned by test_impedance_and_energy_density_are_floats().
 * The question mattered because both encodings are 4 bytes wide: the wrong one
 * would have yielded a nonsense value, never a parse error.
 */
#ifndef ME_BATTERY_FRAME_H
#define ME_BATTERY_FRAME_H

#include <stdbool.h>
#include <stddef.h> /* NULL */
#include <stdint.h>

#include "proto_defs.h"

#define ME_BAT_OFF_NOM_CAPACITY    0u
#define ME_BAT_OFF_NO_OF_CELLS     4u
#define ME_BAT_OFF_GASSING_VOLTAGE 5u
#define ME_BAT_OFF_MAX_VOLTAGE     9u
#define ME_BAT_OFF_NOM_CURRENT    13u
#define ME_BAT_OFF_COLD_CRANKING  17u
#define ME_BAT_OFF_CHARGE_FACTOR  21u
#define ME_BAT_OFF_IMPEDANCE      22u
#define ME_BAT_OFF_BREAK_VOLTAGE  26u
#define ME_BAT_OFF_NOM_VOLTAGE    30u
#define ME_BAT_OFF_ENERGY_DENSITY 34u
#define ME_BAT_OFF_BATTERY_ID     38u

#define ME_BATTERY_PAYLOAD_LEN 40u

/* The whole Q5 frame: 4 + 40 + 2. Used by the router's length table, which
 * needs a real boundary for 0xAA because the protocol encodes none. */
#define ME_BATTERY_FRAME_LEN \
    (ME_HDR_LEN + ME_BATTERY_PAYLOAD_LEN + ME_REG_CRC_LEN)

typedef struct {
    bool     valid;
    float    nom_capacity;
    uint8_t  no_of_cells;
    float    gassing_voltage;
    float    max_voltage;
    float    nom_current;
    float    cold_cranking_current;
    uint8_t  charge_factor;
    float    impedance;
    float    break_voltage;
    float    nom_voltage;
    float    energy_density;
    uint16_t battery_id;
} me_battery_t;

/*
 * Parse the payload (NOT the whole frame - the caller has already stripped the
 * header and verified the CRC).
 *
 * Returns false and sets out->valid = false if len is below
 * ME_BATTERY_PAYLOAD_LEN. A longer payload is accepted and the extra bytes are
 * ignored, so a future protocol revision that appends fields still parses.
 *
 * A payload of 22 - the legacy length - is REFUSED rather than half-parsed
 * (developer decision, 2026-08-12). Half a record with out->valid true would let
 * a battery test run against a zeroed nominal voltage; failing loudly names the
 * problem instead.
 */
bool me_battery_parse(const uint8_t *payload, uint32_t len, me_battery_t *out);

#endif /* ME_BATTERY_FRAME_H */
