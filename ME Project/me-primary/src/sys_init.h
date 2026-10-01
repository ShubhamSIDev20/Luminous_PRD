/*
 * sys_init.h - system initialization module.
 *
 * Runs once at power-on: reads the board's own network identity and creates
 * all three sockets that the communication thread will own.
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_SYS_INIT_H
#define ME_SYS_INIT_H

#include <stdbool.h>

#include "net/udp_sock.h"
#include "platform/netinfo.h"
#include "proto/reg_frame.h"
#include "util/log.h"

typedef struct {
    char           server_ip[16];
    uint16_t       server_port;
    char           iface[ME_IFNAME_MAX];
    uint8_t        device_id;
    uint8_t        secondary;
    uint8_t        channels[ME_MAX_CHANNELS];
    uint8_t        channel_count;
    char           device_name[ME_REG_NAME_LEN + 1];
    me_crc_order_t crc_order;
    int            connect_timeout_ms;
    int            response_timeout_ms;
    /* CPU index the Core Logic thread is pinned to; -1 disables pinning.
     * See Docs/specs/2026-08-19-core-isolation-cicd-design.md. */
    int            core_affinity;
    bool           verbose;
} me_config_t;

typedef struct {
    me_config_t      cfg;
    me_netinfo_t     net;
    int              udp_live_fd;    /* port 10000 */
    int              udp_session_fd; /* port 10001 */
    /*
     * Where outbound datagrams go. Both sockets existed from the first
     * milestone but had no destination, because nothing was ever sent on them.
     * They are built here from cfg.server_ip.
     */
    struct sockaddr_in udp_live_dest;
    struct sockaddr_in udp_session_dest;
    me_reg_request_t reg_request;    /* built once from cfg + net */
} me_system_t;

/* Fill cfg with the demo defaults. server_ip is left empty - it is required. */
void me_config_defaults(me_config_t *cfg);

/*
 * Read network identity, open UDP 10000 and 10001, and build the registration
 * request. The TCP socket is NOT opened here: it is (re)connected by the
 * communication thread, which must be able to reconnect after a drop.
 */
bool me_sys_init(me_system_t *sys, const me_config_t *cfg);

void me_sys_shutdown(me_system_t *sys);

#endif /* ME_SYS_INIT_H */
