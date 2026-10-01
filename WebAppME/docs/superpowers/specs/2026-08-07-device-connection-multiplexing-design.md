# Device-level TCP connection multiplexing (one connection per device, up to 64 channels)

Status: **Designed, approved, not yet implemented.**

## Context

The Device/SecondaryBoard/Channel hierarchy (see
`docs/architecture/01-hardware-hierarchy-protocol-design.md` and
`docs/superpowers/specs/2026-08-06-device-secondary-channel-hierarchy-design.md`)
established the data model: 1 Device → up to 8 SecondaryBoards → up to 8
Channels each (64 channels max per device), with a 1-8/1-8 wire address byte
and a 3-part in-memory handler key (`deviceId-boardNumber-channelId`).

That work left the **connection topology** unchanged: every channel still
gets its own TCP connection and its own `ChannelCommandHandler` instance,
each owning its own `TcpClient`. In practice, one physical device only has
one network link — today's design would require the hardware to open up to
64 separate TCP connections per device, one per channel, which doesn't match
how the hardware actually talks to the server.

This sub-project changes the connection topology: **one TCP connection per
device**, shared across all of that device's channels (up to 64). The
server continues to address individual channels via the existing board+channel
byte (unchanged wire format) — only the *transport* multiplexing changes,
not the protocol.

Confirmed decisions (this conversation):

1. **New device-level connection owner.** A new internal `DeviceConnection`
   class (one per device) owns the single shared `TcpClient`, a write lock,
   and a response-correlation table. It is NOT the public handler type —
   it's a transport-layer object hidden inside `ChannelManager`.
2. **Registration wire format is unchanged.** Each channel still sends its
   own registration packet (same `0xDD 0x01` header, same payload shape as
   today), just now arriving on the shared per-device connection instead of
   its own connection. No new bulk/multi-channel registration packet format.
3. **Public API surface is unchanged.** `ChannelManager._devices` stays a
   flat `ConcurrentDictionary<string, IChannelCommandHandler>` keyed by
   `"deviceId-boardNumber-channelId"`, exactly as today. Every existing call
   site (`DashboardView.razor`, `DeviceController`, `DeviceMcpTools`,
   `SchedulerService`) keeps working unchanged — they still call
   `handler.StartProgram()`, read `handler.Session`, etc. on a per-channel
   object.
4. **`ChannelCommandHandler` becomes a "channel-slot".** It keeps its
   existing fields (`Channel`, `Session`, `RealTime`, `Program`, `Battery`,
   `calibration`, `dbcData`) and existing public method signatures
   unchanged. Internally, instead of owning its own `_tcpClient`, it holds a
   reference to its parent `DeviceConnection` and routes every write through
   it.
5. **Continuous read-loop per device connection.** Today's
   `HandleCommandClientAsync` reads exactly one packet then stops reading
   (the connection is kept open only for outbound writes). It becomes a
   persistent loop that demuxes every incoming packet — new channel
   registrations and command responses alike — by the packet's address byte.
6. **Whole-device disconnect.** If the shared connection drops, every
   channel-slot belonging to that device is marked offline together
   (`IsConnected = false`) — matches the physical reality of one cable per
   device.
7. **State survives reconnect.** On a new connection for a device that
   already has channel-slots (from a prior connection), the existing
   `DeviceConnection`/channel-slot objects are reattached to the new
   `TcpClient` rather than recreated — in-flight Session/Program/calibration
   state is not lost to a transient network drop.
8. **Response correlation by address byte.** Since multiple channels share
   one socket, `SendAndWaitForResponseAsync`-style calls register a
   `TaskCompletionSource<byte[]>` keyed by their address byte *before*
   writing; the shared read-loop resolves the correct one when a matching
   response arrives, instead of each channel doing its own blocking read on
   a private socket.

## Design

### 1. New internal class: `DeviceConnection`

New file `Services/DeviceConnection.cs` (internal to `Services` — not part of
the public handler interface), one instance per physical device connection:

- `TcpClient TcpClient { get; set; }` — the shared socket; replaceable on
  reconnect
- `SemaphoreSlim WriteLock` — serializes outbound writes so two channels'
  commands can never interleave mid-byte-stream on the shared stream
- `ConcurrentDictionary<byte, TaskCompletionSource<byte[]>> PendingResponses`
  — keyed by the response's address byte (`ChannelAddressCodec`-encoded
  board+channel)
- `HashSet<string> ChannelSlotKeys` — tracks which
  `"deviceId-boardNumber-channelId"` keys belong to this device, so a socket
  drop can mark all of them offline in one pass
- `CancellationTokenSource ReadLoopCts` and the read-loop `Task` itself
- `Task SendAsync(byte addressByte, byte[] payload)` — acquires `WriteLock`,
  writes, releases (fire-and-forget style commands)
- `Task<byte[]> SendAndWaitAsync(byte addressByte, byte[] payload, TimeSpan timeout)`
  — registers a `TaskCompletionSource` under `addressByte` in
  `PendingResponses`, calls `SendAsync`, awaits the TCS with the existing
  timeout, removes the entry whether it completed or timed out

### 2. `ChannelManager.HandleCommandClientAsync` — becomes a read-loop

Current behavior: accept connection, read exactly one packet, if it's a
registration packet process it and send one response, then the method
returns (the `TcpClient` is stored on the handler for later outbound writes,
but nothing ever reads from it again).

New behavior:
1. Accept the connection.
2. Read the first packet (still expected to be a registration packet — same
   parsing as today via `DecoderService.ParseRegistrationPacket`).
3. Resolve or create the `DeviceConnection` for `newChannel.DeviceID`:
   - If one already exists (reconnect case), replace its `TcpClient` with the
     new connection and restart its read-loop.
   - If not, create a new `DeviceConnection`, wire up its `TcpClient`.
4. Create/register the channel-slot (`ChannelCommandHandler`) for this
   board+channel exactly as `Add()` does today, except the channel-slot's
   transport dependency is the `DeviceConnection`, not a private `TcpClient`.
5. Enter the read-loop: continuously read packets from the socket. For each
   packet:
   - Decode the address byte.
   - If `DeviceConnection.PendingResponses` has a waiting TCS for that byte,
     complete it with the packet payload — this resolves whichever
     channel-slot's `SendAndWaitForResponseAsync` is waiting.
   - Otherwise, treat it as a (re-)registration packet for that channel and
     process it the same way step 4 did.
6. On socket exception/close: cancel the read-loop, fail any still-pending
   `TaskCompletionSource`s (so waiting calls don't hang), mark every
   channel-slot in `ChannelSlotKeys` as `IsConnected = false`. Leave the
   `DeviceConnection` object and its channel-slots in `_devices` — do not
   remove them — so reconnect can reattach and restore state.

### 3. `ChannelCommandHandler` — delegate writes to `DeviceConnection`

- Remove `_tcpClient` as an owned field; replace with a reference to the
  owning `DeviceConnection`.
- Every one of the ~22 `CommandRequest`-building call sites
  (`StartProgram`, `StopProgram`, calibration commands, DBC transfer, etc.)
  keeps building the command byte array exactly as today via
  `DecoderService.BuildCommand`, but sends it via
  `_deviceConnection.SendAsync(addressByte, payload)` instead of writing to
  `_tcpClient` directly.
- `SendAndWaitForResponseAsync<T>` becomes a thin wrapper around
  `_deviceConnection.SendAndWaitAsync(addressByte, payload, timeout)`,
  parsing the returned bytes into `T` exactly as today.
- `IsConnected` becomes a simple flag set by the owning `DeviceConnection`
  (true while its `TcpClient` is connected, false on disconnect) rather than
  each handler checking its own private socket state.

### 4. `Add()` / registration bookkeeping

- `ChannelManager.Add(ChannelDto channel, TcpClient tcpClient)` changes to
  resolve (find-or-create) the `DeviceConnection` for `channel.DeviceID`
  first, then create/reuse the channel-slot and register it in `_devices`
  under the existing 3-part key — the public method signature and its
  callers (`HandleCommandClientAsync`) barely change in shape, just what
  they wire the channel-slot up to.
- `Get()`/`Remove()` — unchanged; they operate on the flat `_devices`
  dictionary exactly as today.
- Unregistering a single channel (`UnregisterAsync`) removes that one
  channel-slot from `_devices` and from the owning `DeviceConnection`'s
  `ChannelSlotKeys`, but does **not** close the shared connection — other
  channels on the same device keep using it. The connection only closes when
  the physical socket drops or every channel on the device is gone.

## Risks / open questions carried into implementation

1. **Read-loop packet framing** — today's registration/command packets are
   fixed-size reads (e.g. 33-byte buffer). The continuous read-loop needs to
   correctly frame multiple packet types (registration vs. calibration
   response vs. live-data ack, if any arrive on this TCP channel at all —
   confirm during implementation whether live/calibration data flows over
   UDP only, as today's `ChannelManager` UDP listeners suggest, or whether
   any of it also arrives on the TCP command connection).
2. **Existing 22 `CommandRequest` call sites in `ChannelCommandHandler`**
   need their outbound write call swapped from direct `_tcpClient` usage to
   `_deviceConnection.SendAsync`/`SendAndWaitAsync` — mechanical but
   touches every one of them.
3. **Timeout/cleanup ordering** — when a `DeviceConnection`'s socket drops
   while a `SendAndWaitAsync` is in flight, the pending `TaskCompletionSource`
   must be failed (not left to time out slowly) so callers get a fast,
   accurate "disconnected" error rather than waiting out the full timeout.
4. **`TrySendFailureNotification`'s 2-byte framing** (already flagged as an
   open question in the earlier hierarchy design doc) — needs to be
   reconciled with the new read-loop once we know for certain what produces
   that packet and over which transport (TCP vs UDP).

## Critical files
- new `Services/DeviceConnection.cs`
- `Services/ChannelManager.cs` (`HandleCommandClientAsync`, `Add`, `Get`,
  `Remove`)
- `Services/Implementations/ChannelCommandHandler.cs` (all ~22 command-build
  sites + `SendAndWaitForResponseAsync` + `IsConnected`)
- `Services/Interfaces/IChannelCommandHandler.cs` (if `IsConnected`'s
  backing changes shape)

## Verification

1. Unit test: two concurrent `SendAndWaitForResponseAsync` calls on two
   different channel-slots of the same `DeviceConnection` resolve
   independently (no cross-delivery) when responses arrive out of order.
2. Unit test: two concurrent `SendAsync` calls serialize through `WriteLock`
   with no interleaved bytes on the underlying stream.
3. Integration/manual: simulate one TCP connection registering 2+ channels
   for the same device; confirm both appear as independent dashboard cards
   with independent state.
4. Integration/manual: drop the simulated connection; confirm all channels
   for that device go offline together (`IsConnected = false`).
5. Integration/manual: reconnect; confirm channel-slot state (Session,
   Program) is preserved rather than reset, and channels come back online
   without needing to re-run the whole registration-then-approve workflow.
6. Regression: existing single-channel-per-device flows (today's only
   real-world case) continue to work identically — build, existing
   `ChannelAddressCodec` tests still pass, dashboard still shows correct
   card state.
