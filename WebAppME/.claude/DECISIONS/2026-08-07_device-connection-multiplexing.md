# ADR-1: One TCP Connection Per Device, Multiplexed Across Channels
> Date: 2026-08-07T00:00:00Z | Session: #1 | Status: Accepted
> File: `DECISIONS/2026-08-07_device-connection-multiplexing.md`

---

**Context:** The prior transport model opened one TCP connection per channel. A device can have up to 64 channels across multiple secondary boards, so this meant up to 64 sockets per physical device — wasteful and harder to manage reconnect/failure semantics consistently.

**Decision:** Replace per-channel sockets with a single `DeviceConnection` per physical device that owns the shared `TcpClient`, serializes writes with a `SemaphoreSlim`, and correlates responses to the right channel via a `ConcurrentDictionary<byte, TaskCompletionSource<byte[]>>` keyed by channel address byte. `ChannelCommandHandler` becomes a channel-slot that delegates I/O to `DeviceConnection` instead of owning its own socket. `ChannelManager` runs a persistent per-device read loop (`RunCommandListenerAsync`) that demultiplexes incoming packets.

**Reason:** Reduces socket count from up to 64-per-device to 1-per-device, centralizes write serialization and response correlation, and simplifies disconnect/reconnect handling (one connection state instead of N). Rejected keeping per-channel sockets because it doesn't scale cleanly with board/channel count and complicates the design further as more devices are added.

**Impact:**
- ✅ One socket per device regardless of channel count — up to 64 channels share it
- ✅ Existing wire format, public `ChannelManager._devices` keying (`"deviceId-boardNumber-channelId"`), and caller-facing methods (`handler.StartProgram()`, `handler.Session`) unchanged — no breaking changes for `DeviceController` or `DeviceMcpTools`
- ✅ Channel state (session, program) survives reconnects
- ⚠️ Disconnecting the shared connection now marks **all** channel slots for that device offline simultaneously — a single-channel failure previously didn't affect siblings
- ⚠️ All writes to a device are now serialized through one semaphore — higher channel-count devices could see more write contention under heavy concurrent load

**Related**
- Supersedes: none
- Relates to: `docs/superpowers/specs/2026-08-07-device-connection-multiplexing-design.md` (full design spec)