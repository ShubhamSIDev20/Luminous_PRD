/*
 * test_circuit_registry.c - per-circuit registration gate.
 *
 * This is admission control, not storage: a structurally valid CircuitID
 * that nobody ever registered must be refused exactly like a malformed one
 * is - the caller (comm_thread's route_frame) treats both as "ignore this
 * frame" without needing to tell them apart.
 */
#include "test_util.h"
#include "../src/proto/proto_defs.h"
#include "../src/store/circuit_registry.h"

static void test_fresh_circuit_is_not_registered(void)
{
    TEST_CASE("registry: a circuit nobody registered yet reads as not registered");
    me_registry_init();
    CHECK(!me_registry_is_registered(0x11));
}

static void test_mark_then_query_reads_registered(void)
{
    TEST_CASE("registry: marking a circuit registered makes it read back true");
    me_registry_init();
    CHECK(me_registry_mark_registered(0x11));
    CHECK(me_registry_is_registered(0x11));
}

static void test_registering_one_circuit_does_not_affect_others(void)
{
    TEST_CASE("registry: registering 0x11 leaves every other circuit untouched");
    me_registry_init();
    CHECK(me_registry_mark_registered(0x11));
    CHECK(!me_registry_is_registered(0x12));
    CHECK(!me_registry_is_registered(0x21));
    CHECK(!me_registry_is_registered(0x88));
}

static void test_nibble_swap_is_not_aliased(void)
{
    TEST_CASE("registry: 0x18 and 0x81 are distinct circuits, not aliases");
    me_registry_init();
    /* Slots 7 and 56. A swapped secondary/channel mapping is the single most
     * likely arithmetic error and would make these two the same slot. */
    CHECK(me_registry_mark_registered(0x18));
    CHECK(me_registry_is_registered(0x18));
    CHECK(!me_registry_is_registered(0x81));
}

static void test_row_boundary_circuits_are_distinct(void)
{
    TEST_CASE("registry: 0x18 and 0x21 are adjacent slots, not the same one");
    me_registry_init();
    /* Slot 7 (Secondary 1 Channel 8) and slot 8 (Secondary 2 Channel 1) sit
     * either side of a row boundary - an off-by-one in the row arithmetic
     * would merge them. */
    CHECK(me_registry_mark_registered(0x18));
    CHECK(!me_registry_is_registered(0x21));
}

static void test_every_circuit_registers_independently(void)
{
    TEST_CASE("registry: each of the 64 circuits flips exactly one slot");
    int leaks = 0;

    for (unsigned sec = 1; sec <= ME_MAX_SECONDARIES; sec++) {
        for (unsigned ch = 1; ch <= ME_MAX_CHANNELS; ch++) {
            const uint8_t target = ME_CIRCUIT_ID(sec, ch);

            me_registry_init();
            if (!me_registry_mark_registered(target)) {
                leaks++;
                continue;
            }

            /* Exactly one circuit may read as registered: the one marked. */
            for (unsigned s2 = 1; s2 <= ME_MAX_SECONDARIES; s2++) {
                for (unsigned c2 = 1; c2 <= ME_MAX_CHANNELS; c2++) {
                    const uint8_t other = ME_CIRCUIT_ID(s2, c2);
                    const bool    want  = (other == target);
                    if (me_registry_is_registered(other) != want) {
                        leaks++;
                    }
                }
            }
        }
    }
    CHECK_EQ_U(0, leaks);
}

static void test_malformed_circuit_id_cannot_be_registered(void)
{
    TEST_CASE("registry: a malformed CircuitID is refused, never folded to slot 0");
    me_registry_init();
    CHECK(!me_registry_mark_registered(0x00));
    CHECK(!me_registry_mark_registered(0x99));

    /* The single-zero-nibble cases are precisely the ones a buggy 0-based
     * implementation folds onto a real slot. */
    CHECK(!me_registry_mark_registered(0x01));
    CHECK(!me_registry_mark_registered(0x10));
    CHECK(!me_registry_mark_registered(0x09));
    CHECK(!me_registry_mark_registered(0x90));

    /* Confirm none of them silently landed on slot 0 (Secondary 1 / Channel 1). */
    CHECK(!me_registry_is_registered(0x11));
}

static void test_malformed_circuit_id_is_never_registered(void)
{
    TEST_CASE("registry: is_registered is false for a malformed CircuitID "
              "even after other circuits are registered");
    me_registry_init();
    CHECK(me_registry_mark_registered(0x88));
    CHECK(!me_registry_is_registered(0x00));
    CHECK(!me_registry_is_registered(0xFF));
}

static void test_init_clears_previously_registered_circuits(void)
{
    TEST_CASE("registry: init resets state, so a stale registration does "
              "not survive a restart");
    me_registry_init();
    CHECK(me_registry_mark_registered(0x11));
    CHECK(me_registry_is_registered(0x11));

    me_registry_init();
    CHECK(!me_registry_is_registered(0x11));
}

static void test_init_clears_every_slot(void)
{
    TEST_CASE("registry: init clears ALL 64 slots, not just the first");
    me_registry_init();
    for (unsigned sec = 1; sec <= ME_MAX_SECONDARIES; sec++) {
        for (unsigned ch = 1; ch <= ME_MAX_CHANNELS; ch++) {
            CHECK(me_registry_mark_registered(ME_CIRCUIT_ID(sec, ch)));
        }
    }

    me_registry_init();

    int survivors = 0;
    for (unsigned sec = 1; sec <= ME_MAX_SECONDARIES; sec++) {
        for (unsigned ch = 1; ch <= ME_MAX_CHANNELS; ch++) {
            if (me_registry_is_registered(ME_CIRCUIT_ID(sec, ch))) {
                survivors++;
            }
        }
    }
    CHECK_EQ_U(0, survivors);
}

static void test_multiple_circuits_can_be_registered(void)
{
    TEST_CASE("registry: several circuits coexist - the table is not "
              "single-entry");
    me_registry_init();
    /* Today only one circuit is ever marked (one 0xDD per connection), but
     * the CAN-side handshake will mark others. The table must already hold
     * more than one, or that future writer silently evicts this one. */
    CHECK(me_registry_mark_registered(0x11));
    CHECK(me_registry_mark_registered(0x35));
    CHECK(me_registry_mark_registered(0x88));

    CHECK(me_registry_is_registered(0x11));
    CHECK(me_registry_is_registered(0x35));
    CHECK(me_registry_is_registered(0x88));
    CHECK(!me_registry_is_registered(0x34));
}

static void test_marking_twice_is_idempotent(void)
{
    TEST_CASE("registry: registering the same circuit twice (a reconnect) "
              "is harmless");
    me_registry_init();
    CHECK(me_registry_mark_registered(0x21));
    CHECK(me_registry_mark_registered(0x21));
    CHECK(me_registry_is_registered(0x21));
}

void run_circuit_registry_tests(void)
{
    printf("\n-- circuit_registry --\n");
    test_fresh_circuit_is_not_registered();
    test_mark_then_query_reads_registered();
    test_registering_one_circuit_does_not_affect_others();
    test_nibble_swap_is_not_aliased();
    test_row_boundary_circuits_are_distinct();
    test_every_circuit_registers_independently();
    test_malformed_circuit_id_cannot_be_registered();
    test_malformed_circuit_id_is_never_registered();
    test_init_clears_previously_registered_circuits();
    test_init_clears_every_slot();
    test_multiple_circuits_can_be_registered();
    test_marking_twice_is_idempotent();
}
