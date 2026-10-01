/*
 * netinfo.c - read the board's own IPv4 and MAC address (Linux).
 *
 * Uses ioctl(SIOCGIFADDR/SIOCGIFHWADDR) rather than getaddrinfo: this binary is
 * linked -static, and getaddrinfo in a static glibc binary carries an NSS
 * runtime dependency. ioctl has none.
 */
#include "netinfo.h"

#include <arpa/inet.h>
#include <errno.h>
#include <ifaddrs.h>
#include <net/if.h>
#include <netinet/in.h>
#include <stdio.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/socket.h>
#include <unistd.h>

#include "../util/log.h"

static bool read_named_iface(int sock, const char *iface, me_netinfo_t *out)
{
    struct ifreq ifr;
    memset(&ifr, 0, sizeof(ifr));
    strncpy(ifr.ifr_name, iface, IFNAMSIZ - 1);

    if (ioctl(sock, SIOCGIFADDR, &ifr) < 0) {
        return false;
    }
    const struct sockaddr_in *sin = (const struct sockaddr_in *)(void *)&ifr.ifr_addr;
    const uint32_t addr = ntohl(sin->sin_addr.s_addr);
    out->ip[0] = (uint8_t)((addr >> 24) & 0xFFu);
    out->ip[1] = (uint8_t)((addr >> 16) & 0xFFu);
    out->ip[2] = (uint8_t)((addr >> 8) & 0xFFu);
    out->ip[3] = (uint8_t)(addr & 0xFFu);

    memset(&ifr, 0, sizeof(ifr));
    strncpy(ifr.ifr_name, iface, IFNAMSIZ - 1);
    if (ioctl(sock, SIOCGIFHWADDR, &ifr) < 0) {
        return false;
    }
    memcpy(out->mac, ifr.ifr_hwaddr.sa_data, 6);

    snprintf(out->iface, sizeof(out->iface), "%s", iface);
    return true;
}

/* First non-loopback interface that is up and carries an IPv4 address. */
static bool find_fallback_iface(char *buf, size_t buf_sz)
{
    struct ifaddrs *list = NULL;
    if (getifaddrs(&list) != 0) {
        return false;
    }

    bool found = false;
    for (struct ifaddrs *ifa = list; ifa != NULL; ifa = ifa->ifa_next) {
        if (ifa->ifa_addr == NULL || ifa->ifa_addr->sa_family != AF_INET) {
            continue;
        }
        if ((ifa->ifa_flags & IFF_UP) == 0 || (ifa->ifa_flags & IFF_LOOPBACK) != 0) {
            continue;
        }
        snprintf(buf, buf_sz, "%s", ifa->ifa_name);
        found = true;
        break;
    }

    freeifaddrs(list);
    return found;
}

bool me_netinfo_read(const char *iface, me_netinfo_t *out)
{
    memset(out, 0, sizeof(*out));

    const int sock = socket(AF_INET, SOCK_DGRAM, 0);
    if (sock < 0) {
        ME_LOGE("netinfo: cannot open probe socket: %s", strerror(errno));
        return false;
    }

    bool ok = false;
    if (iface != NULL && iface[0] != '\0') {
        ok = read_named_iface(sock, iface, out);
        if (!ok) {
            ME_LOGW("netinfo: interface '%s' unavailable, auto-detecting", iface);
        }
    }

    if (!ok) {
        char fallback[ME_IFNAME_MAX];
        if (find_fallback_iface(fallback, sizeof(fallback))) {
            ok = read_named_iface(sock, fallback, out);
            if (ok) {
                ME_LOGW("netinfo: using interface '%s'", fallback);
            }
        }
    }

    close(sock);

    if (!ok) {
        ME_LOGE("netinfo: no usable network interface found");
        return false;
    }

    /* A 172.17.x address is the signature of Docker's default bridge, which
     * means the payload would carry the container's address, not the board's. */
    if (out->ip[0] == 172 && out->ip[1] >= 16 && out->ip[1] <= 31) {
        ME_LOGW("netinfo: %u.%u.%u.%u looks like a Docker bridge address.",
                out->ip[0], out->ip[1], out->ip[2], out->ip[3]);
        ME_LOGW("netinfo: the Web Application will not be able to reach this "
                "board. Run the container with --network host.");
    }

    return true;
}

void me_netinfo_format_ip(const uint8_t ip[4], char *buf, size_t buf_sz)
{
    snprintf(buf, buf_sz, "%u.%u.%u.%u", ip[0], ip[1], ip[2], ip[3]);
}

void me_netinfo_format_mac(const uint8_t mac[6], char *buf, size_t buf_sz)
{
    snprintf(buf, buf_sz, "%02x:%02x:%02x:%02x:%02x:%02x",
             mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
}
