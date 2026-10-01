/*
 * udp_sock.h - UDP sockets for live data (10000) and session store (10001).
 *
 * In this milestone these are created and held open only. No datagram is sent
 * on them, and per the legacy protocol they are hardware -> server one-way
 * streams, so none is expected inbound either. They are still bound and added
 * to the communication thread's poll() set so the one thread genuinely owns
 * all three sockets.
 *
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_UDP_SOCK_H
#define ME_UDP_SOCK_H

#include <netinet/in.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/*
 * Create a UDP socket bound to INADDR_ANY:port with SO_REUSEADDR.
 * Returns the fd, or -1 on failure.
 */
int me_udp_open(uint16_t port);

void me_udp_close(int fd);

/*
 * Build the destination address for outbound datagrams.
 *
 * ip is a dotted quad parsed with inet_pton - NOT a hostname. The binary is
 * statically linked, and getaddrinfo in a static glibc carries an NSS runtime
 * dependency (ADR-8). Returns false if the string is not a valid IPv4 address.
 *
 * NOTE: the caller passes the same server address used for TCP, so live data
 * goes to the host the board registered with. If the Web Application ever
 * listens for UDP on a different host, that becomes a separate argument.
 */
bool me_udp_dest_init(const char *ip, uint16_t port, struct sockaddr_in *out);

/*
 * Send one datagram. Returns false on failure, having logged errno.
 *
 * A send failure never stops a running test: the caller drops the frame and
 * carries on. A transient ENETUNREACH must not end a 60-second run.
 */
bool me_udp_send_to(int fd, const struct sockaddr_in *dest,
                    const uint8_t *data, size_t len);

#endif /* ME_UDP_SOCK_H */
