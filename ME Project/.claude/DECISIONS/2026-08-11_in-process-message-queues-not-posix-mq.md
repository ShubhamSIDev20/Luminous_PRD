# ADR-11: In-process message queues, not POSIX mq_open
> Date: 2026-08-11 | Session: #4 | Status: Accepted
> File: `DECISIONS/2026-08-11_in-process-message-queues-not-posix-mq.md`

---

**Context:** The four threads need to exchange messages including whole program
packets.

**Decision:** A bounded queue of fixed slots guarded by a mutex and two condition
variables (`src/util/msgq.c`), with an optional `eventfd` for the one consumer
that must also wait on sockets.

**Why not POSIX message queues:**
1. All four threads live in **one process** — a kernel round trip buys nothing.
2. `mq_msgsize_max` defaults to **8192 bytes** and cannot be raised from inside
   an unprivileged container. A `0xBB` program packet can exceed that, so every
   bulk message would need chunking around a limit we did not choose.
3. An in-process queue compiles on the Windows host, so the plumbing is unit
   tested without a board — 10 test cases including a 40-message
   producer/consumer across a depth-16 queue.

**Consequences:** `me_msgq_t` is ~1 MB (16 slots × 64 KB). **Never declare one
as a local** — it overflows a default thread stack. All four are file-scope in
`app_queues.c`. Timed waits use `CLOCK_REALTIME` because
`pthread_condattr_setclock` does not exist in winpthreads; acceptable for a
drop timeout, and must not be reused where timing affects the wire.

**Related**
- Supersedes: none
- Relates to: ADR-12
