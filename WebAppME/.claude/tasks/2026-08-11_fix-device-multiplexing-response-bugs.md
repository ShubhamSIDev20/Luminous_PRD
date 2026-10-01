# T-5: Fix device-multiplexing response buffer truncation + session board-number identity bug
> Created: 2026-08-11T00:00:00Z | Status: Done
> File: `tasks/2026-08-11_fix-device-multiplexing-response-bugs.md`

---

## Description
Two correctness bugs surfaced during end-to-end verification of the device-connection-multiplexing feature (ADR-1):
1. `ChannelManager.HandleCommandClientAsync`'s shared per-device TCP read loop used a fixed 33-byte buffer for every response type, but non-registration responses (factory config, manufacturing config, calibration) run up to 163 bytes — causing truncation and byte-stream desync that corrupted the address-byte routing used by `DeviceConnection.HandleIncomingPacket`.
2. `ChannelCommandHandler.StartProgram` never assigned `Session.SecondaryBoardNumber` and omitted the board number from `Session.SessionFilePath`, so two channels sharing a channel number on different boards of the same device could collide on session identity/file.

Also updated `HardwareSimulator/` (Python hardware simulator) to model one TCP connection per device shared across multiple board/channel-addressed circuits, matching the server-side `DeviceConnection` multiplexing, so end-to-end testing (plan Task 5 Step 4) can actually exercise the multi-channel-per-socket scenario.

## Why / Context
User reported: "HandleCommandClientAsync, deviceConnection.HandleIncomingPacket in this class DeviceConnection has some issue... responses currently getting wrong... session name check... not only deviceId-channelId now there boardid also added so check that also." Two parallel Explore agents traced this to the exact root causes above (see `sessions/2026-08-11_0000_multiplexing-fixes-and-simulator-update.md` for the investigation detail).

## Acceptance Criteria
- [x] Buffer sized to comfortably fit the largest known response (163 bytes) with headroom
- [x] `Session.SecondaryBoardNumber` set in `StartProgram`, matching `Channel.SecondaryBoardNumber`
- [x] `Session.SessionFilePath` format matches `ChannelManager.StoreUdpData`'s `{SessionID}_{DeviceId}_{SecondaryBoardNumber}_{ChannelId}.db` pattern
- [x] `dotnet build BatteryTestingSystem.sln` — 0 errors
- [x] `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj` — 29/29 pass
- [x] `HardwareSimulator/simulator.py` updated to one-TCP-connection-per-device, board/channel address byte encoding on every wire-protocol write site
- [x] Committed and pushed to `origin/main` (commit `3339f86`)

## Progress Log
<!-- Newest entry at the TOP. -->
- **2026-08-11T00:00:00Z** (Claude Sonnet 5): Verified both fixes via `get_symbol_source` against the real current code (not just the agent reports), applied via `mcp__gortex__edit_file`, rebuilt (0 errors) and re-ran the full test suite (29/29 pass). Committed with the rest of the session's pending changes and pushed to `origin/main` after flagging (and getting explicit user confirmation on) a plaintext password already present in the user's own pending `appsettings.json` change. A background security review independently flagged the same hardcoded-credential finding post-push — acknowledged, already reviewed with the user before the push.
- **2026-08-11T00:00:00Z** (Claude Sonnet 5): Ran two parallel Explore agents (read-only) to investigate: one on `DeviceConnection`/`ChannelManager` packet framing and `DecoderService.TryDecode`, one hunting for any deviceId+channelId-only (missing board) lookup/correlation keys. First found the buffer-size/framing bug; second found the `Session.SecondaryBoardNumber`/`SessionFilePath` bug in `ChannelCommandHandler.StartProgram`. Confirmed `CommandTracker`, `ChannelManager.MakeKey`/`Add`/`Get`/`Remove`, `SendAndWaitForResponseAsync`'s address-byte encoding, `GetLastSessionAsync`, and `GetChannelAsync` already correctly include board number — no fix needed there.
- **2026-08-11T00:00:00Z** (Claude Sonnet 5): Rewrote `HardwareSimulator/simulator.py` for device-level TCP multiplexing (one socket per device shared across circuits, `address_byte` property, `decode_address_byte()` helper, per-device `command_listener`/`start_device_connection` replacing the old per-circuit `start_device`), updated `config.json` (`channels_per_device`), added board/channel to log/debug output on request.

## Files Touched
| File | What Changed |
|------|-------------|
| `Services/ChannelManager.cs` | `HandleCommandClientAsync` read buffer `byte[33]` → `byte[1024]` |
| `Services/Implementations/ChannelCommandHandler.cs` | `StartProgram`: added `Session.SecondaryBoardNumber = Channel.SecondaryBoardNumber;`; `SessionFilePath` now includes board segment |
| `HardwareSimulator/simulator.py` | Rewritten for one-TCP-connection-per-device multiplexing, board/channel addressing (created — new to this repo) |
| `HardwareSimulator/config.json` | `circuit_count_per_device` → `channels_per_device` (created) |
| `HardwareSimulator/run_sim.py` | Launcher, unchanged from initial add (created) |
| `.gitignore` | Added `__pycache__/`, `*.pyc`, `*.log`, `*.tmp-*` |

## Related
- Depends on: none
- Relates to: ADR-1 (`DECISIONS/2026-08-07_device-connection-multiplexing.md`), plan `docs/superpowers/plans/2026-08-07-device-connection-multiplexing.md` (Task 5 Step 4 — hardware-in-the-loop verification, still pending an actual run against a live server)
