/*
 * tcp_client.c - TCP client socket to the Web Application.
 */
#include "tcp_client.h"

#include <arpa/inet.h>
#include <errno.h>
#include <fcntl.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <poll.h>
#include <string.h>
#include <sys/socket.h>
#include <unistd.h>

#include "../util/log.h"

/* How long to wait, after the expected bytes arrive, for any extra bytes that
 * would mean the response is longer than the protocol allows. */
#define ME_TCP_SETTLE_MS 100

static bool set_nonblocking(int fd, bool on)
{
    const int flags = fcntl(fd, F_GETFL, 0);
    if (flags < 0) {
        return false;
    }
    const int want = on ? (flags | O_NONBLOCK) : (flags & ~O_NONBLOCK);
    return fcntl(fd, F_SETFL, want) == 0;
}

int me_tcp_connect(const char *server_ip, uint16_t port, int timeout_ms)
{
    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port   = htons(port);

    if (inet_pton(AF_INET, server_ip, &addr.sin_addr) != 1) {
        ME_LOGE("tcp: '%s' is not a valid IPv4 address", server_ip);
        return -1;
    }

    const int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) {
        ME_LOGE("tcp: socket() failed: %s", strerror(errno));
        return -1;
    }

    /* Registration is a single small frame followed by a wait for the reply.
     * Nagle would delay it for no benefit. */
    const int one = 1;
    (void)setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &one, sizeof(one));

    if (!set_nonblocking(fd, true)) {
        ME_LOGE("tcp: cannot set non-blocking: %s", strerror(errno));
        close(fd);
        return -1;
    }

    int rc = connect(fd, (const struct sockaddr *)&addr, sizeof(addr));
    if (rc < 0 && errno != EINPROGRESS) {
        ME_LOGE("tcp: connect to %s:%u failed: %s", server_ip, port, strerror(errno));
        close(fd);
        return -1;
    }

    if (rc < 0) {
        struct pollfd pfd = { .fd = fd, .events = POLLOUT, .revents = 0 };
        const int pr = poll(&pfd, 1, timeout_ms);
        if (pr <= 0) {
            ME_LOGE("tcp: connect to %s:%u timed out after %d ms",
                    server_ip, port, timeout_ms);
            close(fd);
            return -1;
        }

        int err = 0;
        socklen_t errlen = sizeof(err);
        if (getsockopt(fd, SOL_SOCKET, SO_ERROR, &err, &errlen) < 0 || err != 0) {
            ME_LOGE("tcp: connect to %s:%u failed: %s",
                    server_ip, port, strerror(err != 0 ? err : errno));
            close(fd);
            return -1;
        }
    }

    if (!set_nonblocking(fd, false)) {
        ME_LOGE("tcp: cannot restore blocking mode: %s", strerror(errno));
        close(fd);
        return -1;
    }

    ME_LOGI("tcp: connected to %s:%u", server_ip, port);
    return fd;
}

bool me_tcp_send_all(int fd, const uint8_t *buf, size_t len)
{
    size_t sent = 0;
    while (sent < len) {
        const ssize_t n = send(fd, &buf[sent], len - sent, MSG_NOSIGNAL);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("tcp: send failed after %u/%u bytes: %s",
                    (unsigned)sent, (unsigned)len, strerror(errno));
            return false;
        }
        if (n == 0) {
            ME_LOGE("tcp: peer closed while sending");
            return false;
        }
        sent += (size_t)n;
    }
    return true;
}

int me_tcp_recv_response(int fd, uint8_t *buf, size_t cap, size_t want,
                         int timeout_ms)
{
    size_t got = 0;

    while (got < cap) {
        /* Generous wait until we have what we expect; a short settle after. */
        const int wait_ms = (got >= want) ? ME_TCP_SETTLE_MS : timeout_ms;

        struct pollfd pfd = { .fd = fd, .events = POLLIN, .revents = 0 };
        const int pr = poll(&pfd, 1, wait_ms);

        if (pr < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("tcp: poll failed: %s", strerror(errno));
            return -1;
        }
        if (pr == 0) {
            break; /* quiet - either a timeout, or the response is complete */
        }
        /* POLLHUP frequently arrives ALONGSIDE POLLIN when the server replies
         * and immediately closes. Bailing out on the hangup flag alone would
         * discard a response that is already sitting in the receive buffer, so
         * only treat these as fatal when there is no data to read. */
        if ((pfd.revents & POLLIN) == 0 &&
            (pfd.revents & (POLLERR | POLLHUP | POLLNVAL)) != 0) {
            if (got == 0) {
                ME_LOGE("tcp: peer error or hangup while waiting for response");
                return -1;
            }
            break;
        }

        const ssize_t n = recv(fd, &buf[got], cap - got, 0);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("tcp: recv failed: %s", strerror(errno));
            return -1;
        }
        if (n == 0) {
            if (got == 0) {
                ME_LOGE("tcp: peer closed the connection");
                return -1;
            }
            break; /* peer closed after sending something - report what we got */
        }
        got += (size_t)n;
    }

    return (int)got;
}

void me_tcp_close(int fd)
{
    if (fd >= 0) {
        (void)shutdown(fd, SHUT_RDWR);
        close(fd);
    }
}
