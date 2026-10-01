# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM debian:bookworm AS builder
RUN apt-get update && \
    apt-get install -y --no-install-recommends crossbuild-essential-arm64 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /build
COPY src ./src

# Mirrors build.ps1's flags and explicit source list exactly - keep both in
# sync by hand; this is deliberately not templated to avoid pulling in test
# sources. See Docs/specs/2026-08-19-core-isolation-cicd-design.md §2 for
# why this is a different cross-compiler than build.ps1's pinned toolchain
# (accepted trade-off, not an oversight).
RUN aarch64-linux-gnu-gcc -std=gnu11 -O2 -Wall -Wextra -Werror \
      -Isrc -static -pthread -o /build/me_primary \
      src/main.c \
      src/sys_init.c \
      src/app_queues.c \
      src/msg.c \
      src/threads/comm_thread.c \
      src/threads/core_logic.c \
      src/threads/data_mgr.c \
      src/threads/can_mgr.c \
      src/threads/post_reg.c \
      src/store/circuit_store.c \
      src/store/circuit_registry.c \
      src/proto/crc16.c \
      src/proto/reg_frame.c \
      src/proto/program_chain.c \
      src/proto/frame_router.c \
      src/proto/ack_frame.c \
      src/proto/program_frame.c \
      src/proto/control_frame.c \
      src/proto/battery_frame.c \
      src/proto/realtime_frame.c \
      src/proto/can_frame.c \
      src/proto/rpmsg_frame.c \
      src/proto/step_decode.c \
      src/exec/step_engine.c \
      src/net/tcp_client.c \
      src/net/udp_sock.c \
      src/platform/netinfo.c \
      src/platform/rpmsg_link.c \
      src/util/log.c \
      src/util/msgq.c \
      src/util/channel_list.c

# NOTE (2026-08-20): temporarily debian:bookworm-slim instead of `scratch` so
# `docker exec -it me-primary bash` and `apt-get` work for on-device debugging
# (see ADR-30/ADR-33). Revisit before the next production release build.
FROM debian:bookworm-slim
COPY --from=builder /build/me_primary /me_primary
ENTRYPOINT ["/me_primary"]
