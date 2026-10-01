/*
 * data_mgr.h - the Data Manager thread.
 *
 * Owns ALL persistent per-circuit storage. Nothing else touches
 * circuit_store.c; every other thread reaches it through a message.
 *
 * Handles:
 *   STORE_PROGRAM / STORE_BATTERY / STORE_CONFIG   from Communication
 *   REQ_PROGRAM  / REQ_BATTERY                     from Core Logic
 *   SESSION_DATA                                   from Core Logic, ringed
 *                                                  and forwarded to Communication
 *   CAN_DATA                                       from CAN Manager (provision)
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_DATA_MGR_H
#define ME_DATA_MGR_H

#include <stdbool.h>

/*
 * Session records held while waiting for the communication thread. Frames are
 * ~90 bytes, so this is a few kilobytes - not 64 KB per slot.
 */
#define ME_SESSION_RING_DEPTH 64u
#define ME_SESSION_FRAME_MAX  256u

bool me_data_mgr_start(void);
void me_data_mgr_join(void);

#endif /* ME_DATA_MGR_H */
