# Task T-19: Allow secondary board 0 device registration

> Created: 2026-08-17
> Completed: 2026-08-17
> Session: #11

## Request
Field topology changed from `1-1`..`1-8` to devices now registering as `1-0-1`..`1-0-8` (secondary board `0` instead of `1`). Board `0` registrations were being rejected, which also broke the previously-working `1-1`..`1-8` topology (user reported both no longer connecting). Allow already-registered secondary board `0` to register/connect alongside board `1`.

## Root cause
`Utils/ChannelAddressCodec.Encode(boardNumber, channelNumber)` validated `boardNumber` with `< 1` as the lower bound before packing it into the device wire-protocol address byte. Board `0` threw `ArgumentOutOfRangeException` on every registration/command build, which callers (`ChannelCommandHandler`, `ChannelManager`, `DecoderService`) caught and surfaced as `CommonResponse.Fail(...)` — a silent registration rejection, not a visible crash.

## Changes
- `Utils/ChannelAddressCodec.cs:7-8` — `boardNumber < 1` → `boardNumber < 0` (message: "BoardNumber must be 0-8.")
- `Models/Entities/SecondaryBoard.cs:16` — `[Range(1, 8)]` → `[Range(0, 8)]`

## Not changed (reviewed, judged out of scope)
- `Repositories/Implementations/DeviceChannelRepository.cs` `GetOrCreateBoardAsync`: `IsImplicit = boardNumber == 1` kept as-is — board `1` remains the legacy/implicit sentinel from the pre-multi-board migration; board `0` is a distinct real board, not remapped onto that semantic.

## Verification
- `dotnet build` — 0 errors.
- Not live/simulator tested this session — see follow-up.

## Follow-up
- Live or HardwareSimulator smoke test: register a device as `1-0-1`..`1-0-8` end-to-end and confirm connection/telemetry, alongside re-confirming `1-1`..`1-8` still works.
