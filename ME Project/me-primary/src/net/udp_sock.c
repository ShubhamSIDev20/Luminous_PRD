/*
 * udp_sock.c - UDP socket creation.
 */
#include "udp_sock.h"

#include <arpa/inet.h>
#include <errno.h>
#include <netinet/in.h>
#include <string.h>
#include <sys/socket.h>
#include <unistd.h>

#include "../util/log.h"

int me_udp_open(uint16_t port)
{
    const int fd = socket(AF_INET, SOCK_DGRAM, 0);
    if (fd < 0) {
        ME_LOGE("udp: socket() for port %u failed: %s", port, strerror(errno));
        return -1;
    }

    const int one = 1;
    if (setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one)) < 0) {
        ME_LOGW("udp: SO_REUSEADDR on port %u failed: %s", port, strerror(errno));
    }

    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family      = AF_INET;
    addr.sin_addr.s_addr = htonl(INADDR_ANY);
    addr.sin_port        = htons(port);

    if (bind(fd, (const struct sockaddr *)&addr, sizeof(addr)) < 0) {
        ME_LOGE("udp: bind to port %u failed: %s", port, strerror(errno));
        close(fd);
        return -1;
    }

    ME_LOGI("udp: socket open on port %u", port);
    return fd;
}

void me_udp_close(int fd)
{
    if (fd >= 0) {
        close(fd);
    }
}

bool me_udp_dest_init(const char *ip, uint16_t port, struct sockaddr_in *out)
{
    memset(out, 0, sizeof(*out));
    out->sin_family = AF_INET;
    out->sin_port   = htons(port);

    if (inet_pton(AF_INET, ip, &out->sin_addr) != 1) {
        ME_LOGE("udp: '%s' is not a valid IPv4 address for port %u", ip, port);
        return false;
    }
    return true;
}

bool me_udp_send_to(int fd, const struct sockaddr_in *dest,
                    const uint8_t *data, size_t len)
{
    if (fd < 0) {
        return false;
    }

    const ssize_t n = sendto(fd, data, len, MSG_NOSIGNAL,
                             (const struct sockaddr *)dest, sizeof(*dest));
    if (n < 0) {
        ME_LOGW("udp: sendto port %u failed: %s",
                (unsigned)ntohs(dest->sin_port), strerror(errno));
        return false;
    }
    if ((size_t)n != len) {
        /* A short UDP send is not a partial write to retry - the datagram is
         * malformed on the wire and the receiver will reject its CRC. */
        ME_LOGW("udp: short send to port %u: %d of %u bytes",
                (unsigned)ntohs(dest->sin_port), (int)n, (unsigned)len);
        return false;
    }
    return true;
}
