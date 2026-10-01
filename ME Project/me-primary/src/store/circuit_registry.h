/*
 * circuit_registry.h - per-circuit "is this Secondary/Channel registered?" gate.
 *
 * PURE LOGIC: no sockets, no threads, no platform headers - same rule as
 * circuit_store.h, so admission-control behaviour is proven on the host
 * test suite rather than only against hardware.
 *
 * WHY A SEPARATE MODULE FROM circuit_store.h: circuit_store answers "what
 * has this circuit sent" (program bytes, battery config, ...). This module
 * answers a different question asked earlier in the pipeline: "is this
 * circuit allowed to be handled at all". Folding the two together would mix
 * a data question with an access-control question in one file.
 *
 * NOT THREAD-SAFE, deliberately - the same rule circuit_store.h follows.
 * The COMMUNICATION THREAD is the single owner: it is the only thread that
 * calls these functions, which is what makes a plain bool array safe here
 * with no atomics and no lock.
 *
 * CURRENT WRITER: the communication thread, once per TCP connection - after
 * its own 0xDD device registration gets a success reply (Value 0x01 or
 * 0x02), it marks its own configured Secondary/Channel here. Nothing else
 * writes yet.
 *
 * WHY SIZED FOR ALL 64 CIRCUITS NOW: a future CAN-side per-Secondary
 * handshake (once can_mgr.c grows past being a stub) will need to mark
 * other slots. This table exists now so that future write has a home
 * instead of needing a second table invented later.
 *
 * HOW THAT FUTURE WRITE MUST BE DONE: the CAN Data Manager runs on its own
 * thread, so it must NOT call me_registry_mark_registered() directly -
 * that would turn this array into an unsynchronised cross-thread write, and
 * -O2 is entitled to hoist the read in the communication thread's routing
 * loop. Send a message to the communication thread and let it do the mark,
 * exactly as every other thread reaches circuit_store through a message.
 *
 * KNOWN SCOPE LIMIT, deliberate and temporary: the board sends one 0xDD
 * registration per operator-configured channel (--channels) per connection,
 * so only the Secondary/Channels named on the command line can ever be
 * registered - anything else is refused. That is intentional until the
 * CAN-side handshake above lands, which would let a circuit self-announce
 * without being explicitly configured.
 */
#ifndef ME_CIRCUIT_REGISTRY_H
#define ME_CIRCUIT_REGISTRY_H

#include <stdbool.h>
#include <stdint.h>

/* Clear every slot's registration state. Call once at startup. */
void me_registry_init(void);

/*
 * Mark circuit_id as registered.
 *
 * Returns false and marks nothing for a malformed CircuitID (see
 * me_circuit_slot in circuit_store.h) - a bad CircuitID must never be
 * folded onto a real slot such as Secondary 1 Channel 1's.
 */
bool me_registry_mark_registered(uint8_t circuit_id);

/*
 * True only if circuit_id is both a structurally valid Secondary/Channel
 * pair AND has previously been marked registered. A malformed CircuitID is
 * never "registered" - there is no slot to check, so the caller does not
 * need to tell "malformed" and "not yet registered" apart: both mean
 * "ignore this frame".
 */
bool me_registry_is_registered(uint8_t circuit_id);

#endif /* ME_CIRCUIT_REGISTRY_H */
