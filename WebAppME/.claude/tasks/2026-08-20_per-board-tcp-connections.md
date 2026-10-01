# T-45 — Per-(device, board) TCP connections

> Started: 2026-08-20
> Completed: 2026-08-20
> Session: [sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md](../sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md)

## Request
User: hardware-team ADR change — each secondary board may now run as its own TCP client on the same physical LAN device, *or* several boards may share one TCP client. Which one it is "depends on hardware team", so the server must handle **both dynamically**: same socket → boards map onto it together; different sockets → tracked separately. User explicitly flagged the risk: *"when we communicate at the same time with same connection it can misbehave."*

## Why the naive fix was wrong
First instinct was simply re-keying `_deviceConnections` from `deviceId` to `(deviceId, board)`. That is necessary but **not sufficient and actively dangerous**: `DeviceConnection` also owned the `SemaphoreSlim _writeLock` that serializes writes. Two boards sharing one physical socket would then each hold their *own* lock guarding the *same* `NetworkStream` — two simultaneous sends could interleave bytes mid-frame and corrupt both packets. That is exactly the misbehaviour the user predicted.

## Design (approved via brainstorming, bounded path)
Split the one class into two layers:

- **`DeviceLink`** (new, `Services/DeviceConnection.cs`) — one per *physical socket*. Owns the `TcpClient` **and the write lock**. One lock per socket, no matter how many boards ride it.
- **`DeviceConnection`** (existing class, now *logical*) — one per `(device, board)`. Owns that board's `_pendingResponses` correlation, `ChannelSlotKeys`, and points at whichever `DeviceLink` its registrations are arriving on (`AttachLink`, `IsOn`).

Shape is **discovered at runtime**, never assumed: `ChannelManager._deviceLinks` is keyed by the `TcpClient` instance itself, so boards registering on the same socket get the *same* `DeviceLink` (writes serialize), and boards on separate sockets get separate ones (fully independent).

## Implementation notes
- `GetOrCreateDeviceConnection(int deviceId)` → `(int deviceId, int boardNumber)`; added `GetOrCreateDeviceLink(TcpClient?)`.
- `DeviceConnection.TcpClient` kept as a **read-only pass-through** to `Link?.TcpClient`, which is why the change didn't ripple: outside `ChannelManager` only two lines touch it (`ChannelCommandHandler.cs:305` reads it for `IsConnected`, `:320` calls `SendAndWaitAsync`) and both needed no edit.
- Read loop (`HandleCommandClientAsync`) now tracks a `Dictionary<int board, DeviceConnection> boardsOnThisSocket` instead of a single variable, since one socket can carry several boards. Incoming packets route by decoding the board from the address byte's high nibble (`ChannelAddressCodec.Decode(buffer[2])`), falling back to the sole registered board when the decoded board is unknown (`Decode` does not validate — a malformed byte can name a board that never registered).
- Disconnect `finally` tears down **every** board the socket carried, each still individually guarded by `IsOn(client)` so a board already reattached to a newer socket isn't wrongly marked offline (preserves the session-#7 "only 1 of N cards reconnects" fix). Drops the `DeviceLink` from `_deviceLinks` once no board is on it, so a reconnecting socket never inherits a stale write lock and the table can't grow unbounded.
- Comms-loss alarms moved from per-device to per-`(device, board)` (`{device}/{board}/comms-loss`, `BoardNumber` populated, title names the board) — boards can now drop independently, and board 1 coming back must not clear a still-dead board 3's fault. This also matches the canonical alarm-key format already documented in `Models/Entities/AlarmLog.cs` (`"12/0/3/comms-loss"`) better than the old device-only key did.

## Bug found and fixed during live verification
`Add(ChannelDto, TcpClient tcpClient = null)` — the socket is **optional**. Startup rehydration (`LoadDevicesAsync`) and the operator "Allow" gate call it with `null`. `ConcurrentDictionary.GetOrAdd(null, …)` throws `ArgumentNullException`, producing **640 × `Add error: Value cannot be null. (Parameter 'key')`** on boot (previously assigning a null `TcpClient` was harmless). Fixed by making `GetOrCreateDeviceLink` return `null` for a null socket, and `AttachLink(null)` a no-op that *keeps* any existing live link rather than clearing it.

## Verification
- `dotnet build` → 0 errors.
- `dotnet test` → **183/183 passing** (was 180; +3 new).
- New tests in `BatteryTestingSystem.Tests/Services/DeviceConnectionTests.cs`:
  - `TwoBoardsSharingOneLink_DoNotInterleaveBytes` — the user's exact concern: two `DeviceConnection`s on one shared `DeviceLink` sending 500-byte payloads concurrently; each 500-byte block must be uniform, never a mix.
  - `BoardsOnSeparateLinks_DoNotSharePendingResponses` — answering board 2 must not resolve board 1's identically-addressed pending request.
  - `IsOn_TracksTheCurrentlyAttachedSocketOnly` — the guard the disconnect path relies on.
  - Existing 6 tests updated to the new `AttachLink` API (they previously assigned `TcpClient` directly).
- Live (app + `run_sim.py -n 16`, 10 devices × 16 channels = boards 1 and 2 sharing one socket per device, 160 channels):
  - Both board 1 (8/8) and board 2 (8/8) of device 1 fully **Online** simultaneously — 0 ERR, 0 FTL, 0 `Add error`, 0 `second operation`.
  - Real command round-trip on a **board-2** channel (`1-2-1` → Fetch from device) populated all fields and stamped last-synced; log shows address byte `0x21` (board 2, ch 1) and the returned serial `10000109` (simulator's `10000000 + device*100 + channelIndex`, index 9 = board 2 ch 1) — proves the response was correlated to the *correct board* over the shared socket.
  - Hard-killed the simulator: **both** boards of every device went fully Offline, and exactly **20** per-board alarms were raised (10 devices × 2 boards) with correct `BoardNumber` — e.g. `1/1/comms-loss` and `1/2/comms-loss` as separate rows.
  - Restarted the simulator: all 20 alarms cleared (`ClearedAtUtc` set, 0 uncleared), both boards back Online 8/8, zero browser console errors.

## Documentation updated
`docs/flowDocs/` described the old one-socket-per-device model, so it was brought in line:
- **04** (`04-connection-multiplexing.md`) — rewritten. Retitled "Connection Multiplexing (Sockets, Boards and Channels)", now states up front that the socket count is the *hardware's* choice and discovered at runtime, with an ASCII diagram of both shapes (shared socket vs. socket-per-board), separate `DeviceLink`/`DeviceConnection` boxes, a new **INVARIANT 0 — one write lock per SOCKET** panel explaining the interleaving hazard, plus new "Routing a response to the right board" and "Losing a socket" sections.
- **03** (allow gate) — `Add()` call now shows `GetOrCreateDeviceConnection(DeviceID, SecondaryBoardNumber)` + `AttachLink(GetOrCreateDeviceLink(client))`.
- **11** (disconnect/reconnect) — cleanup block rewritten as a per-board loop with the `IsOn` guard, per-board alarm, and link drop; added a paragraph on shared-vs-separate socket blast radius.
- **13** (call sequences) — S1 step 11 and all three S2a `Add()` branches updated; added a callout that `tcpClient` is optional and `AttachLink(null)` is a deliberate no-op.
- **README** — chapter 04's index description updated; hand-mirror note now records the 2026-08-20 pass.
- **`index.html`** — hand-mirrored all of the above (⚠️ still no generator, see below). Validated afterwards: all tags balanced (section/figure/pre/div/h2/h3/h4/p), 19 chapters intact, 0 stale `GetOrCreateDeviceConnection(DeviceID)` / `deviceConnection.TcpClient =` references, and key phrases cross-checked present in both `.md` and `.html`.

🪤 **Mirroring gotcha:** quoting inside `index.html`'s `<pre>` blocks is **inconsistent** — some blocks escape `"` as `&quot;` (chapter 04 did), others keep it literal (chapter 11 did). A find/replace assuming one convention silently matches nothing. Match the exact existing text; the mirror script asserted `count == 1` per edit, which is what caught it.

## Files changed
- `Services/DeviceConnection.cs` (new `DeviceLink` class; `DeviceConnection` now logical per-board)
- `Services/ChannelManager.cs` (per-board registry + per-socket link table, read-loop routing, per-board disconnect/alarms)
- `BatteryTestingSystem.Tests/Services/DeviceConnectionTests.cs` (API update + 3 new tests)
- `docs/flowDocs/{04,03,11,13}*.md`, `docs/flowDocs/README.md`, `docs/flowDocs/index.html`

## Gotcha worth remembering
`kill -9 <pid>` from the Bash tool kills only the Git Bash wrapper, not the Windows process — `ps aux`'s first column is the bash PID, the 4th is the real `WINPID`. A "the server never noticed the disconnect" symptom was actually the simulator still running. Verify with `Get-Process` / `Get-NetTCPConnection` (PowerShell) and stop it with `Stop-Process -Id <winpid> -Force`.
