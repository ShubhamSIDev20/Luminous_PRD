/*
 * test_log.c - hex dump formatting tests.
 *
 * Only the pure formatting is tested. The logging output functions themselves
 * are thin stderr wrappers with nothing to assert on.
 */
#include <string.h>

#include "test_util.h"
#include "../src/util/log.h"

/* The first 16 bytes of the demo registration frame. */
static const uint8_t k_frame_head[16] = {
    0xDD, 0x01, 0x1C, 0x01, 0x11, 0x42, 0x54, 0x53,
    0x2D, 0x36, 0x30, 0x30, 0x00, 0x00, 0x00, 0x00
};

static void test_full_line_of_a_registration_frame(void)
{
    TEST_CASE("hex: full 16-byte line renders offset, bytes and ASCII");
    char out[ME_HEX_LINE_SIZE];
    me_hex_line(k_frame_head, 16, 0, out, sizeof(out));

    CHECK_STR("0000  DD 01 1C 01 11 42 54 53  2D 36 30 30 00 00 00 00  "
              "|.....BTS-600....|",
              out);
}

static void test_partial_line_stays_column_aligned(void)
{
    /* The 33-byte frame's last line holds a single byte. Its ASCII column must
     * start at the same position as a full line's, or the dump is hard to read
     * at exactly the moment you need it most. */
    TEST_CASE("hex: short final line stays column-aligned with full lines");
    const uint8_t tail[1] = { 0x4B };
    char full[ME_HEX_LINE_SIZE];
    char partial[ME_HEX_LINE_SIZE];

    me_hex_line(k_frame_head, 16, 0, full, sizeof(full));
    me_hex_line(tail, 1, 32, partial, sizeof(partial));

    const char *full_bar    = strchr(full, '|');
    const char *partial_bar = strchr(partial, '|');
    CHECK(full_bar != NULL);
    CHECK(partial_bar != NULL);
    if (full_bar && partial_bar) {
        CHECK_EQ_U(full_bar - full, partial_bar - partial);
    }

    CHECK(strncmp(partial, "0020  4B ", 9) == 0);
    CHECK_STR("|K|", partial_bar ? partial_bar : "");
}

static void test_offset_is_rendered_in_hex(void)
{
    TEST_CASE("hex: offset column is hexadecimal, not decimal");
    const uint8_t data[1] = { 0x00 };
    char out[ME_HEX_LINE_SIZE];
    me_hex_line(data, 1, 16, out, sizeof(out));
    CHECK(strncmp(out, "0010", 4) == 0);
}

static void test_non_printable_bytes_become_dots(void)
{
    TEST_CASE("hex: non-printable and high bytes render as dots");
    const uint8_t data[4] = { 0x00, 0x1F, 0x7F, 0xFF };
    char out[ME_HEX_LINE_SIZE];
    me_hex_line(data, 4, 0, out, sizeof(out));
    CHECK(strstr(out, "|....|") != NULL);
}

static void test_printable_ascii_is_preserved(void)
{
    TEST_CASE("hex: printable ASCII range renders literally");
    const uint8_t data[4] = { 0x20, 0x41, 0x7A, 0x7E }; /* space A z ~ */
    char out[ME_HEX_LINE_SIZE];
    me_hex_line(data, 4, 0, out, sizeof(out));
    CHECK(strstr(out, "| Az~|") != NULL);
}

void run_log_tests(void)
{
    printf("-- log --\n");
    test_full_line_of_a_registration_frame();
    test_partial_line_stays_column_aligned();
    test_offset_is_rendered_in_hex();
    test_non_printable_bytes_become_dots();
    test_printable_ascii_is_preserved();
}
