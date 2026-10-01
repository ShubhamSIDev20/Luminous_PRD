/* rpmsg_link.c - see rpmsg_link.h. Linux only. */
#include "rpmsg_link.h"

#include <errno.h>
#include <fcntl.h>
#include <stdlib.h>
#include <string.h>
#include <termios.h>
#include <unistd.h>

#include "../util/log.h"

const char *me_rpmsg_device_path(void)
{
    const char *env = getenv("ME_RPMSG_DEV");

    if (env != NULL && env[0] != '\0') {
        return env;
    }
    return ME_RPMSG_DEV_DEFAULT;
}

int me_rpmsg_open(const char *path)
{
    struct termios tio;
    int fd;

    if (path == NULL) {
        return -1;
    }

    fd = open(path, O_RDWR | O_NOCTTY);
    if (fd < 0) {
        ME_LOGE("rpmsg: cannot open %s: %s", path, strerror(errno));
        return -1;
    }

    /* Raw mode, matching the reference implementation's tty.setraw(). Without
     * it the line discipline would translate CR/LF and interpret control
     * bytes - and a 64-byte CAN payload contains arbitrary bytes, so any
     * translation silently corrupts frames. */
    if (tcgetattr(fd, &tio) != 0) {
        ME_LOGE("rpmsg: tcgetattr on %s failed: %s", path, strerror(errno));
        (void)close(fd);
        return -1;
    }
    cfmakeraw(&tio);
    tio.c_cc[VMIN]  = 0; /* read() returns whatever is available... */
    tio.c_cc[VTIME] = 0; /* ...without blocking on a character timer */
    if (tcsetattr(fd, TCSANOW, &tio) != 0) {
        ME_LOGE("rpmsg: tcsetattr on %s failed: %s", path, strerror(errno));
        (void)close(fd);
        return -1;
    }

    ME_LOGI("rpmsg: opened %s (fd %d)", path, fd);
    return fd;
}

bool me_rpmsg_write_all(int fd, const uint8_t *buf, size_t len)
{
    size_t done = 0u;

    if (fd < 0 || buf == NULL) {
        return false;
    }

    while (done < len) {
        const ssize_t n = write(fd, &buf[done], len - done);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("rpmsg: write failed after %u of %u bytes: %s",
                    (unsigned)done, (unsigned)len, strerror(errno));
            return false;
        }
        done += (size_t)n;
    }
    return true;
}

void me_rpmsg_close(int fd)
{
    if (fd >= 0) {
        (void)close(fd);
    }
}
