/*
 * netinfo.h - read the board's own IPv4 address and MAC address.
 *
 * These values are not diagnostics: they are carried INSIDE the registration
 * payload, so the Web Application uses them to reach back to this board. Wrong
 * values produce a frame that transmits perfectly and registers the device at
 * an unreachable address.
 *
 * Consequence: the container must run with --network host. On Docker's default
 * bridge this reads the container's virtual interface (172.17.0.x with a
 * synthetic MAC) instead of the Verdin's real one.
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_NETINFO_H
#define ME_NETINFO_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_IFNAME_MAX 32

typedef struct {
    char    iface[ME_IFNAME_MAX];
    uint8_t ip[4];
    uint8_t mac[6];
} me_netinfo_t;

/*
 * Populate out for the named interface.
 *
 * If iface is NULL or cannot be read, falls back to the first non-loopback
 * interface that is up and has an IPv4 address - Torizon may present the NIC
 * as eth0 or end0 depending on kernel naming. out->iface reports what was
 * actually used.
 *
 * Returns true on success.
 */
bool me_netinfo_read(const char *iface, me_netinfo_t *out);

/* "192.168.0.14" into buf. */
void me_netinfo_format_ip(const uint8_t ip[4], char *buf, size_t buf_sz);

/* "00:14:2d:ab:cd:ef" into buf. */
void me_netinfo_format_mac(const uint8_t mac[6], char *buf, size_t buf_sz);

#endif /* ME_NETINFO_H */
