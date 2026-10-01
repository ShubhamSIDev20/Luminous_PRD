/*
 * core_logic.h - the Core Logic thread.
 *
 * Owns test state and program step execution (see ../exec/step_engine.h).
 * It never touches a socket and never owns persistent storage; it asks the
 * Data Manager for what it needs and answers the Communication thread's
 * control frames.
 *
 * Main loop, and the reason it is shaped this way:
 *
 *     if (me_msgq_recv(&g_q_core, &m, 10)) handle(&m);
 *     service_engines();   // ticks every circuit's step_engine, 10ms budget
 *
 * The queue receive already had to be bounded so a stop request is noticed
 * promptly. A fixed 10 ms bound matches step_engine's own timing budget
 * (Docs/specs/2026-08-14-step-execution-design.md §4) - no timer thread,
 * no timerfd.
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_CORE_LOGIC_H
#define ME_CORE_LOGIC_H

#include <stdbool.h>

#include "../sys_init.h"

/* sys must outlive the thread. */
bool me_core_logic_start(const me_system_t *sys);
void me_core_logic_join(void);

#endif /* ME_CORE_LOGIC_H */
