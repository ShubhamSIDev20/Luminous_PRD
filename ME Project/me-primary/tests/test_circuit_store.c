/*
 * test_circuit_store.c - CircuitID mapping and per-circuit storage.
 *
 * The mapping tests matter more than they look. A malformed CircuitID that
 * folds silently into slot 0 would overwrite Secondary 1 Channel 1's program
 * with someone else's - a corruption with no error, no log and no symptom until
 * the wrong test runs on real cells.
 */
#include <string.h>

#include "test_util.h"
#include "../src/store/circuit_store.h"

/* ------------------------------------------------------------- mapping --- */

static void test_valid_circuit_ids_map_to_slots(void)
{
    TEST_CASE("circuit: the four corners of the 8x8 grid map as documented");
    CHECK_EQ_U(0,  me_circuit_slot(0x11)); /* secondary 1, channel 1 */
    CHECK_EQ_U(7,  me_circuit_slot(0x18)); /* secondary 1, channel 8 */
    CHECK_EQ_U(8,  me_circuit_slot(0x21)); /* secondary 2, channel 1 */
    CHECK_EQ_U(63, me_circuit_slot(0x88)); /* secondary 8, channel 8 */
}

static void test_malformed_circuit_ids_are_rejected(void)
{
    TEST_CASE("circuit: nibbles are 1-based, so 0 and >8 are rejected");
    /* Every one of these would land on a real slot if the code simply masked
     * the nibbles. They must not. */
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x00));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x01));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x10));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x09));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x90));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0x99));
    CHECK_EQ_U((unsigned long)ME_SLOT_INVALID, (unsigned long)me_circuit_slot(0xFF));
}

static void test_slot_round_trip(void)
{
    TEST_CASE("circuit: slot -> id -> slot round-trips for all 64 circuits");
    int mismatches = 0;
    for (unsigned sec = 1; sec <= ME_MAX_SECONDARIES; sec++) {
        for (unsigned ch = 1; ch <= ME_MAX_CHANNELS; ch++) {
            const uint8_t id   = ME_CIRCUIT_ID(sec, ch);
            const int     slot = me_circuit_slot(id);
            if (slot == ME_SLOT_INVALID || me_circuit_from_slot(slot) != id) {
                mismatches++;
            }
        }
    }
    CHECK_EQ_U(0, mismatches);
}

/* ------------------------------------------------------------- storage --- */

/* One step packet, 15 bytes: 9 header + 4 payload + 2 end. */
static uint32_t make_step(uint8_t *buf, uint32_t next, uint16_t step_no)
{
    uint32_t i = 0;
    buf[i++] = ME_STEP_START_1;
    buf[i++] = ME_STEP_START_2;
    buf[i++] = (uint8_t)(next >> 24);
    buf[i++] = (uint8_t)(next >> 16);
    buf[i++] = (uint8_t)(next >> 8);
    buf[i++] = (uint8_t)(next);
    buf[i++] = (uint8_t)(step_no >> 8);
    buf[i++] = (uint8_t)(step_no);
    buf[i++] = 0x01; /* operator */
    buf[i++] = 0xDE;
    buf[i++] = 0xAD;
    buf[i++] = 0xBE;
    buf[i++] = 0xEF;
    buf[i++] = ME_STEP_END_1;
    buf[i++] = ME_STEP_END_2;
    return i;
}

static void test_program_append_concatenates(void)
{
    TEST_CASE("store: two appended fragments read back as one buffer");
    me_store_init();

    uint8_t a[15];
    uint8_t b[15];
    const uint32_t la = make_step(a, 15, 1);
    const uint32_t lb = make_step(b, ME_STEP_TERMINATOR, 2);

    CHECK(me_store_program_append(0x11, a, la));
    CHECK_EQ_U(15, me_store_program_len(0x11));
    CHECK(me_store_program_append(0x11, b, lb));
    CHECK_EQ_U(30, me_store_program_len(0x11));

    const uint8_t *p = me_store_program_ptr(0x11);
    CHECK(p != NULL);
    CHECK_MEM(p, a, la);
    CHECK_MEM(p + la, b, lb);
}

static void test_terminator_marks_complete(void)
{
    TEST_CASE("store: the chain terminator is what marks a program complete");
    me_store_init();

    uint8_t a[15];
    uint8_t b[15];
    (void)make_step(a, 15, 1);
    (void)make_step(b, ME_STEP_TERMINATOR, 2);

    CHECK(me_store_program_append(0x11, a, 15));
    /* Step 1 points at offset 15, which does not exist yet - not complete. */
    CHECK(!me_store_program_is_complete(0x11));

    CHECK(me_store_program_append(0x11, b, 15));
    CHECK(me_store_program_is_complete(0x11));
}

static void test_append_after_completion_resets(void)
{
    TEST_CASE("store: a new program replaces the old one instead of appending");
    me_store_init();

    uint8_t a[15];
    (void)make_step(a, ME_STEP_TERMINATOR, 1);
    CHECK(me_store_program_append(0x11, a, 15));
    CHECK(me_store_program_is_complete(0x11));

    /* A re-sent program must not be concatenated onto the finished one - the
     * absolute nextIndex offsets in the new chain would all be wrong. */
    CHECK(me_store_program_append(0x11, a, 15));
    CHECK_EQ_U(15, me_store_program_len(0x11));
}

static void test_append_beyond_capacity_is_refused(void)
{
    TEST_CASE("store: an append past the buffer end is refused, not truncated");
    me_store_init();

    static uint8_t big[ME_PROGRAM_BUF_SIZE];
    memset(big, 0x5A, sizeof(big));

    CHECK(me_store_program_append(0x11, big, ME_PROGRAM_BUF_SIZE));
    CHECK_EQ_U(ME_PROGRAM_BUF_SIZE, me_store_program_len(0x11));

    /* One more byte has nowhere to go. The stored length must not move. */
    const uint8_t extra = 0xFF;
    CHECK(!me_store_program_append(0x11, &extra, 1));
    CHECK_EQ_U(ME_PROGRAM_BUF_SIZE, me_store_program_len(0x11));
}

static void test_storage_rejects_invalid_circuit(void)
{
    TEST_CASE("store: every accessor rejects a malformed CircuitID");
    me_store_init();

    const uint8_t byte = 0x00;
    CHECK(!me_store_program_append(0x00, &byte, 1));
    CHECK(!me_store_program_is_complete(0x00));
    CHECK_EQ_U(0, me_store_program_len(0x00));
    CHECK(me_store_program_ptr(0x00) == NULL);
}

static void test_battery_round_trip(void)
{
    TEST_CASE("store: battery data round-trips per circuit");
    me_store_init();

    me_battery_t in;
    memset(&in, 0, sizeof(in));
    in.valid        = true;
    in.nom_capacity = 100.0f;
    in.no_of_cells  = 12;

    CHECK(me_store_battery_set(0x21, &in));

    me_battery_t out;
    memset(&out, 0, sizeof(out));
    CHECK(me_store_battery_get(0x21, &out));
    CHECK_EQ_U(12, out.no_of_cells);
    CHECK(out.nom_capacity == 100.0f);

    /* A different circuit must not see it. */
    CHECK(!me_store_battery_get(0x11, &out));
}

void run_circuit_store_tests(void)
{
    printf("\n-- circuit_store --\n");
    test_valid_circuit_ids_map_to_slots();
    test_malformed_circuit_ids_are_rejected();
    test_slot_round_trip();
    test_program_append_concatenates();
    test_terminator_marks_complete();
    test_append_after_completion_resets();
    test_append_beyond_capacity_is_refused();
    test_storage_rejects_invalid_circuit();
    test_battery_round_trip();
}
