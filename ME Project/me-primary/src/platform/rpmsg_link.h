/*
 * rpmsg_link.h - character-device I/O for the A53 <-> M7 RPMsg channel.
 *
 * LINUX ONLY. Not built by build-native.ps1.
 *
 * This module is deliberately thin: open, write, close, and nothing else.
 * Every byte-layout decision lives in proto/rpmsg_frame.c so that it can be
 * unit-tested on the dev laptop without a board. Keep it that way - logic
 * added here becomes logic that only hardware can test.
 *
 * Device and channel parameters: Ref Docs/RPMSG_PROTOCOL.md section 1.
 */
#ifndef ME_RPMSG_LINK_H
#define ME_RPMSG_LINK_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_RPMSG_DEV_DEFAULT "/dev/ttyRPMSG30"

/*
 * The device path: the ME_RPMSG_DEV environment variable when set and
 * non-empty, otherwise ME_RPMSG_DEV_DEFAULT. Returns a pointer that stays
 * valid for the life of the process.
 */
const char *me_rpmsg_device_path(void);

/*
 * Open the device in raw mode. Returns the fd, or -1 (already logged).
 * A -1 return is NOT fatal to the application: the caller degrades to
 * dropping CAN traffic, so a board with a stopped M7 still registers and
 * stays diagnosable.
 */
int me_rpmsg_open(const char *path);

/*
 * Write every byte, retrying on short writes and EINTR. Returns false on a
 * real error (already logged).
 *
 * The protocol wants one frame per write() (RPMSG_PROTOCOL.md section 8
 * step 3). The retry loop exists for correctness under EINTR, not to split
 * frames deliberately.
 */
bool me_rpmsg_write_all(int fd, const uint8_t *buf, size_t len);

/* Safe on -1. */
void me_rpmsg_close(int fd);

#endif /* ME_RPMSG_LINK_H */
