# T-24 — Harden TCP 9999 / UDP 10000+10001 listener supervision

> Started: 2026-08-18 | Session #17 | Agent: Claude (Opus 5)
> Status: ✅ Code complete, build-verified — ⚠️ live verification outstanding

## Trigger

User: *"`RunCommandListenerAsync` — what will happen if any error or exception happens there, or
`StartInternal`? Our application depends on TCP 9999 and UDP 10000, 10001 — those must always be
running."* Then: *"you pick which is best"* (retry policy).

## Done

All six items from the analysis (see
`sessions/2026-08-18_0500_listener-supervision-and-port-resilience.md` for the per-defect detail):

1. ✅ `SuperviseAsync` wrapper per listener — retry forever, 1s→30s exponential backoff, `Fatal` +
   event-bus escalation after 5 consecutive failures (ADR-4).
2. ✅ UDP receive loops moved **outside** their `try` + per-iteration catch — one bad datagram no
   longer ends the listener.
3. ✅ `SIO_UDP_CONNRESET(false)` on both `UdpClient`s (Windows) — an offline device's ICMP
   port-unreachable no longer throws on the next `ReceiveAsync`.
4. ✅ TCP accept-failure counter (5) + 500 ms delay, then rethrow for a clean rebind — kills the
   100%-CPU hot-loop path. `HardwareManagerChanged` moved out of `finally` into
   `RaiseHardwareManagerChanged()` on the success path (a throwing subscriber can no longer kill the
   accept loop).
5. ✅ `_udpChannel` recreated per lifecycle and passed by parameter to writer + processor; old
   `_lifecycleCts` disposed on restart; writer uses `TryWrite` instead of a discarded `WriteAsync`.
   `RestartService()` is now safe to call (it previously caused silent total UDP data loss).
6. ✅ `Services/ListenerHealthCheck.cs` wired as `"hardware-listeners"` on the existing
   `AddHealthChecks()` in `Program.cs`.

`dotnet build` → 0 errors.

## Outstanding

- [ ] **Live verification.** Nothing has been exercised against the simulator or real hardware. Worth
      testing specifically: (a) start the app while another process holds 9999 → expect retry logs then
      a clean bind once the port frees; (b) power off a device mid-run → expect the ICMP case to no
      longer kill UDP 10000/10001; (c) hit the health endpoint with a port down → expect `Unhealthy`.
- [ ] **UI banner.** Nothing subscribes to `ChannelManager.ListenerHealthTopic` yet — the escalation
      currently reaches only the log and the health check.
- [ ] Consider supervising `StartUdpProcessorAsync` too. It is deliberately **not** wrapped in
      `SuperviseAsync` (a completed channel reader returns immediately, which would make the supervisor
      spin); it logs `Fatal` on unexpected exit instead.
