/*
 * comm_thread.h - the single communication thread.
 *
 * Created once at power-on. Owns the TCP connection to the Web Application and
 * both UDP sockets, multiplexed with poll(). One thread, three sockets.
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_COMM_THREAD_H
#define ME_COMM_THREAD_H

#include <stdbool.h>

#include "../sys_init.h"

/*
 * ME_COMM_REGISTERED and ME_COMM_MONITORING were separate states that nothing
 * ever distinguished. They are one thing: registered, connection held open and
 * polled. IDLE is the name the protocol documentation uses.
 */
typedef enum {
    ME_COMM_CONNECTING = 0,
    ME_COMM_REGISTERING,
    ME_COMM_IDLE,    /* registered; TCP held open and polled */
    ME_COMM_STOPPED
} me_comm_state_t;

const char *me_comm_state_name(me_comm_state_t s);

/* Start the thread. sys must outlive the thread. */
bool me_comm_thread_start(me_system_t *sys);

/*
 * Ask the thread to stop. Async-signal-safe: sets a flag only, so it is safe
 * to call from a SIGINT/SIGTERM handler.
 */
void me_comm_thread_request_stop(void);

bool me_comm_thread_stop_requested(void);

/* Wait for the thread to exit. */
void me_comm_thread_join(void);

/* True once a registration has succeeded at least once. */
bool me_comm_is_registered(void);

#endif /* ME_COMM_THREAD_H */
