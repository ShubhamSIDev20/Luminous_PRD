/*
 * sys_init.c - system initialization module.
 */
#include "sys_init.h"

#include <stdio.h>
#include <string.h>

#include "net/udp_sock.h"

void me_config_defaults(me_config_t *cfg)
{
    memset(cfg, 0, sizeof(*cfg));
    cfg->server_port         = ME_PORT_TCP_CMD;
    cfg->device_id           = 0x01;
    cfg->secondary           = 1;
    cfg->channels[0]         = 1;
    cfg->channel_count       = 1;
    cfg->crc_order           = ME_CRC_ORDER_DEFAULT;
    cfg->connect_timeout_ms  = 5000;
    cfg->response_timeout_ms = 5000;
    cfg->core_affinity       = 3;     /* dedicate CPU3 to Core Logic by default */
    cfg->verbose             = false;
    snprintf(cfg->iface, sizeof(cfg->iface), "eth0");
    snprintf(cfg->device_name, sizeof(cfg->device_name), "BTS-600");
}

bool me_sys_init(me_system_t *sys, const me_config_t *cfg)
{
    memset(sys, 0, sizeof(*sys));
    sys->cfg            = *cfg;
    sys->udp_live_fd    = -1;
    sys->udp_session_fd = -1;

    ME_LOGI("system init: starting");

    if (!me_netinfo_read(cfg->iface, &sys->net)) {
        return false;
    }

    char ip_str[16];
    char mac_str[18];
    me_netinfo_format_ip(sys->net.ip, ip_str, sizeof(ip_str));
    me_netinfo_format_mac(sys->net.mac, mac_str, sizeof(mac_str));
    ME_LOGI("system init: interface %s  ip %s  mac %s",
            sys->net.iface, ip_str, mac_str);

    sys->udp_live_fd = me_udp_open(ME_PORT_UDP_LIVE);
    if (sys->udp_live_fd < 0) {
        return false;
    }

    sys->udp_session_fd = me_udp_open(ME_PORT_UDP_SESSION);
    if (sys->udp_session_fd < 0) {
        me_udp_close(sys->udp_live_fd);
        sys->udp_live_fd = -1;
        return false;
    }

    /* Live data and session data go to the same host the board registers with,
     * on their own ports. Without these, both sockets are send-capable but have
     * nowhere to send - which is exactly the state they were in until now. */
    if (!me_udp_dest_init(cfg->server_ip, ME_PORT_UDP_LIVE, &sys->udp_live_dest)
        || !me_udp_dest_init(cfg->server_ip, ME_PORT_UDP_SESSION,
                             &sys->udp_session_dest)) {
        me_udp_close(sys->udp_session_fd);
        me_udp_close(sys->udp_live_fd);
        sys->udp_live_fd    = -1;
        sys->udp_session_fd = -1;
        return false;
    }

    /*
     * Template only: device_id/name/ip/mac are shared by every channel this
     * process registers (one device_id per board, not per channel).
     * circuit_id is intentionally left at 0 here - do_registration() in
     * comm_thread.c builds a per-attempt copy with circuit_id set to
     * ME_CIRCUIT_ID(cfg.secondary, cfg.channels[i]) for each configured
     * channel. See Docs/specs/2026-08-19-multi-channel-secondary1-design.md.
     */
    memset(&sys->reg_request, 0, sizeof(sys->reg_request));
    sys->reg_request.device_id  = cfg->device_id;
    snprintf(sys->reg_request.device_name, sizeof(sys->reg_request.device_name),
             "%s", cfg->device_name);
    memcpy(sys->reg_request.ip, sys->net.ip, ME_REG_IP_LEN);
    memcpy(sys->reg_request.mac, sys->net.mac, ME_REG_MAC_LEN);

    char chlist[64] = { 0 };
    size_t chlist_len = 0;
    for (uint8_t i = 0; i < cfg->channel_count; i++) {
        chlist_len += (size_t)snprintf(chlist + chlist_len, sizeof(chlist) - chlist_len,
                                       "%s%u", (i == 0) ? "" : ",",
                                       (unsigned)cfg->channels[i]);
    }

    ME_LOGI("system init: device %u, secondary %u, channels [%s], name \"%s\"",
            sys->reg_request.device_id, (unsigned)cfg->secondary, chlist,
            sys->reg_request.device_name);
    ME_LOGI("system init: complete - TCP %s:%u, UDP %u and %u open",
            cfg->server_ip, cfg->server_port,
            ME_PORT_UDP_LIVE, ME_PORT_UDP_SESSION);

    return true;
}

void me_sys_shutdown(me_system_t *sys)
{
    if (sys->udp_live_fd >= 0) {
        me_udp_close(sys->udp_live_fd);
        sys->udp_live_fd = -1;
    }
    if (sys->udp_session_fd >= 0) {
        me_udp_close(sys->udp_session_fd);
        sys->udp_session_fd = -1;
    }
    ME_LOGI("system shutdown: sockets closed");
}
