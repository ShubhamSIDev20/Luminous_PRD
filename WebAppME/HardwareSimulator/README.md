# Hardware Simulator

Stands in for the physical battery-testing hardware so the `BatteryTestingSystem`
server can be developed and load-tested without a rig on the bench. It speaks the
real wire protocol: it registers over TCP, answers every command the server sends,
and streams live/store telemetry over UDP.

Pure Python standard library — no runtime dependencies.

## Requirements

- Python 3.9+
- The `BatteryTestingSystem` server running (from this project's root, `dotnet run`)
- `pytest` only if you want to run the unit tests (`pip install pytest`)

## Quick start

Start the server first, then from this directory:

```powershell
# Defaults from config.json: 10 devices x 8 channels, 1000 ms packet interval
python run_sim.py

# 100 devices (= 100 TCP connections)
python run_sim.py -d 100

# 16 channels per device -> two secondary boards per device
python run_sim.py -n 16

# Reproduce the field topology that registers as 1-0-1 .. 1-0-8
python run_sim.py --board-base 0

# 50 devices, 5 ms interval (heavy load test)
python run_sim.py -d 50 -i 5

# Point at a server on another machine
python run_sim.py -d 100 -H 192.168.1.50
```

`run_sim.py` is a thin launcher — it prints a banner and shells out to
`simulator.py` with the same arguments. `python simulator.py ...` works
identically.

Stop with `Ctrl+C`; the simulator closes every socket and joins its threads.

### Command-line options

| Flag | Default | Effect |
| --- | --- | --- |
| `-c`, `--config PATH` | `config.json` | Config file to load |
| `-d`, `--devices N` | from config | Override `simulation.device_count` |
| `-n`, `--channels N` | from config | Override `simulation.channels_per_device` (1-64) |
| `-i`, `--interval MS` | from config | Override `simulation.packet_interval_ms` |
| `--board-base {0,1}` | from config | Override `simulation.secondary_board_base` |
| `-H`, `--host HOST` | from config | Override `server.host` |

Flags override the config file.

### `-d` scales devices, `-n` scales boards

These are different axes, and mixing them up is the most common surprise:

- **`-d`** adds whole **devices** — each is its own TCP connection and listener
  thread. Circuit IDs increment the *first* segment: `1-1-1`, then `2-1-1`, `3-1-1`…
- **`-n`** adds **channels within** a device, multiplexed over that device's single
  connection. Channels roll into the next secondary board every 8, so the *middle*
  segment only grows once you pass 8.

So `-d 10` gives you ten devices of one board each — `1-1-1`…`1-1-8`, then
`2-1-1`…`2-1-8`. It will never produce `1-2-1`; that needs `-n 16`:

| Command | Circuit IDs produced |
| --- | --- |
| `-d 10` (8 channels) | `1-1-1`…`1-1-8`, `2-1-1`…`2-1-8`, … `10-1-1`…`10-1-8` |
| `-d 1 -n 16` | `1-1-1`…`1-1-8`, `1-2-1`…`1-2-8` |
| `-d 1 -n 64` | boards 1-8, 8 channels each (the hardware max) |
| `-d 1 -n 8 --board-base 0` | `1-0-1`…`1-0-8` (field topology) |

`channels_per_device` above 64 is clamped to 64 with a warning — the wire address
byte packs board and channel into one nibble each (`board << 4 | channel`), so
8 boards x 8 channels is the ceiling.

### Board numbering

`--board-base` sets the **lowest** secondary-board number. Both values are valid on
the wire — `Utils/ChannelAddressCodec.Encode` accepts boards `0-8`:

- **`1` (default)** — the legacy/implicit topology. Board `1` is also the
  `IsImplicit` sentinel in `DeviceChannelRepository.GetOrCreateBoardAsync`.
- **`0`** — what real field devices actually send (`1-0-1`…`1-0-8`). Use this to
  exercise the server's board-`0` paths, which the simulator could not reach before.

## How it works

```
                 TCP 9999 (commands, one connection per device)
  simulator.py  <------------------------------------------->  ChannelManager
                 UDP 10000  live data (always, while registered)
                 UDP 10001  store data (only while a program runs)
```

1. **Connect** — one TCP socket per `device_id`. Every channel of that device
   shares it, exactly like the server's per-device `DeviceConnection`.
2. **Register** — each channel sends a `0xDD` registration packet and waits for
   the server's status byte. Channels are addressed by a single byte,
   `board << 4 | channel`, so `channels_per_device` is capped at 64
   (8 boards x 8 channels) and is clamped with a warning if you ask for more.
3. **Stream** — once registered, a sender thread per channel emits a `0xCC`
   real-time packet to `data_view_port` every `packet_interval_ms`. A store packet
   goes to `data_store_port` only while that channel's program status is `Running`.
4. **React** — a listener thread per device decodes inbound commands (`0xEE`
   control, `0xBB` program, `0xAA` configuration, `0xA0` calibration, `0xDD`
   registration), mutates the addressed channel's state, and replies with a
   CRC16-bound response.

### Telemetry: program-driven vs. random

Two mutually exclusive sources:

- **`CoreEngine`** — as soon as the server uploads a program, the bytes are
  reassembled (`program_decoder.py`), decoded into real steps, and handed to
  `core_engine.py`. From then on current/voltage/power/temperature and the
  capacity/energy accumulators come from the actual step setpoints and `Limit`
  cutoff conditions. Once the program ends (an `STO` step or an exhausted step
  list) the channel holds idle at zero and reports `ProgramStatus = Stop`.
- **Random fallback** — a channel that has *never* received a program draws values
  uniformly from `data_ranges` when `enable_random_data` is true. A channel that
  has had a program never falls back to random again, matching real hardware.

## Configuration (`config.json`)

Every key below is read by the simulator; nothing is decorative.

### `server`

| Key | Meaning |
| --- | --- |
| `host` | Server IP/hostname to connect and send to |
| `command_port` | TCP port for the command channel |
| `data_view_port` | UDP port for live/real-time packets |
| `data_store_port` | UDP port for store packets |

> The server side of these ports is **hardcoded** in `Services/ChannelManager.cs`
> (9999 / 10000 / 10001). Changing them here alone will not work — change both
> sides or leave them as-is.

### `simulation`

| Key | Meaning |
| --- | --- |
| `device_count` | Number of simulated devices (TCP connections) |
| `channels_per_device` | Channels per device, 1-64 (clamped); rolls into the next board every 8 |
| `secondary_board_base` | Lowest secondary-board number, `0` or `1` (see above) |
| `packet_interval_ms` | Live-packet cadence per channel |
| `enable_random_data` | Random telemetry for channels with no program |
| `enable_error_simulation` | Inject random `SystemErrorId` values |
| `error_rate_percent` | Chance per packet that an error is injected |

Total UDP load is `device_count x channels_per_device x (1000 / packet_interval_ms)`
packets per second. The default 10 x 8 at 1000 ms is 80 pkt/s; dropping to 10 ms
makes that 8,000 pkt/s. Raise the interval first if the server starts dropping
packets.

### `device_defaults`

| Key | Meaning |
| --- | --- |
| `device_name_prefix` | Prefix for generated names, e.g. `SIM_DEVICE_001_1` |
| `ip_address` | IP reported inside registration packets |

MAC addresses are generated per channel from a UUID and serial numbers are derived
from the device/channel index, so neither is configurable.

### `data_ranges`

Bounds for the random fallback: `current_min`/`max`, `voltage_min`/`max`,
`temperature_min`/`max`. A small per-device offset is added so channels are not
identical. Ignored entirely for program-driven channels.

### `logging`

`level` (`DEBUG`/`INFO`/...), `log_file` (default `simulator.log`, always written),
and `console_output`. Set `level` to `DEBUG` to see per-channel address bytes,
decoded programs, and periodic V/I/T lines.

## Files

| File | Role |
| --- | --- |
| `run_sim.py` | Launcher wrapper around `simulator.py` |
| `simulator.py` | Device state, packet builders, TCP command handling, threads |
| `program_decoder.py` | Chunk reassembly + program-byte decode |
| `core_engine.py` | Step-execution state machine driving realistic telemetry |
| `dbc_decoder.py` | DBC payload decode |
| `config.json` | Runtime configuration |
| `Send-RealtimeUdp.ps1` | Debug tool: hand-craft and send a single `0xCC` packet |
| `tests/` | Unit tests for the decoders and the engine |

The decoders and engine are deliberately byte-exact mirrors of the C# side:

| Python | Mirrors |
| --- | --- |
| `program_decoder.py` | `Services/ProgramBuilder.cs`, `Models/Enums/ProgramEnums.cs`, `Components/UI/Program/OperatorConstants.cs` |
| `core_engine.py` | `Models/Enums/ProgramEnums.cs` cutoff / logic codes |
| `dbc_decoder.py` | `Services/DbcParser.cs` (`BuildMultiPortPayload`) |
| `simulator.py` packet builders | `Services/DecoderService.cs`, `Services/CircuitCommandHandler.cs` |

If you change an encoding on the C# side, change it here too or the simulator will
silently feed the server garbage.

## Debugging a single packet

`Send-RealtimeUdp.ps1` sends one hand-built `0xCC` packet with no simulator or
`CoreEngine` involved — useful for exercising a specific decode path (undefined
enum value, boundary CRC, truncated packet).

```powershell
.\Send-RealtimeUdp.ps1                          # localhost:10000, idle defaults
.\Send-RealtimeUdp.ps1 -TargetHost 192.168.1.50
```

Every field is a variable in the `$Fields` block near the top — edit and re-run.

## Tests

```powershell
pip install pytest
python -m pytest tests -q      # run from this directory
```

The tests are self-contained: they build byte streams by hand and assert the
decoders and `CoreEngine` produce the expected steps, signals, and samples. No
server or network needed.

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| No connection / immediate exit | Server not running, or `server.host` / `command_port` wrong |
| Registration fails | Server rejected the channel — check its log; a duplicate board/channel address returns `ALREADY_REGISTERED` |
| Dashboard shows nothing | Live packets go to `data_view_port`; confirm the server is listening on 10000 and no firewall is dropping UDP |
| Channels stuck at zero | Normal after a program completes — the channel holds idle. Send a new program or restart |
| Dropped packets under load | Lower `device_count` / `channels_per_device`, or raise `packet_interval_ms` |
| Nothing in the console | `logging.console_output` is false; `simulator.log` still has everything |
