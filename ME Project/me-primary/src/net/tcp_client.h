/*
 * tcp_client.h - TCP client socket to the Web Application.
 *
 * The board is the CLIENT; the Web Application is the server on port 9999.
 * Linux-only. Not built by build-native.ps1.
 */
#ifndef ME_TCP_CLIENT_H
#define ME_TCP_CLIENT_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/*
 * Connect to server_ip:port. server_ip must be a dotted-quad literal - this
 * binary is linked -static and deliberately avoids getaddrinfo/NSS.
 *
 * Returns a socket fd, or -1 on failure.
 */
int me_tcp_connect(const char *server_ip, uint16_t port, int timeout_ms);

/* Send the whole buffer, looping over partial writes. True on success. */
bool me_tcp_send_all(int fd, const uint8_t *buf, size_t len);

/*
 * Read a response into buf (capacity cap).
 *
 * Waits up to timeout_ms for the first byte, then keeps reading until
 * want bytes have arrived or the peer goes quiet. Once want bytes are in
 * hand it waits one further short interval to see whether MORE follows -
 * that is how an over-long response is detected rather than silently
 * truncated to the expected size.
 *
 * Returns bytes read, 0 on timeout with nothing received, or -1 on error
 * or peer disconnect.
 */
int me_tcp_recv_response(int fd, uint8_t *buf, size_t cap, size_t want,
                         int timeout_ms);

void me_tcp_close(int fd);

#endif /* ME_TCP_CLIENT_H */
