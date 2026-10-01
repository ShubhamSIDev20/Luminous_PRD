/*
 * can_mgr.h - the CAN Data Manager thread.
 *
 * Transports CAN-FD frames between Core Logic and the M7 core over RPMsg.
 * Core Logic sends ME_MSG_CAN_TX (CAN ID in `offset`, 64-byte frame in
 * `payload`); this thread wraps it per Ref Docs/RPMSG_PROTOCOL.md and writes
 * it to /dev/ttyRPMSG30. The M7 streams the Secondary's replies back, which
 * this thread turns into ME_MSG_CAN_DATA for Core Logic.
 *
 * The M7 firmware is owned separately (me_battery_m7_core) and does the
 * actual FlexCAN work. Nothing here talks to a CAN controller.
 *
 * If the device cannot be opened the thread still runs, drains the queue and
 * counts drops, so a board with a stopped M7 stays up and diagnosable.
 *
 * Design: Docs/specs/2026-08-18-rpmsg-can-transport-design.md
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_CAN_MGR_H
#define ME_CAN_MGR_H

#include <stdbool.h>

bool me_can_mgr_start(void);
void me_can_mgr_join(void);

#endif /* ME_CAN_MGR_H */
