# Hardware Simulator Design

## Project Overview
Build a Python-based hardware simulator for the Battery Testing System (BTS) that can simulate 100+ devices concurrently, handle all BTS commands, and generate real-time UDP data packets.

## Architecture

### Component Design

**1. DeviceCircuit Class**
- Holds all device state: device_id, circuit_id, runtime measurements, program state
- Methods: build_realtime_packet(), build_store_packet(), build_calibration_packet()
- Thread-safe with lock for shared state

**2. HardwareSimulator Class**
- Manages all device circuits
- Creates TCP connections and handles registration
- Spawns listener thread per device for command handling
- Spawns sender thread per device for UDP data transmission

**3. Command Handlers**
- handle_control_command() — 0xEE commands (Start/Stop/Pause/Continue/SyncTime/Reset)
- handle_program_command() — 0xBB commands (Program transfer, DBC file transfer)
- handle_configuration_command() — 0xAA commands (Battery params, factory/mfg config)
- handle_calibration_command() — 0xA0 commands (Calibration data)

### UDP Communication Flow

```
Server (BTS)                    Simulator
     |                            |
     |<---- TCP Connect ---------| (registration port 9999)
     |                            |
     |<---- 0xDD-01 (REG) -------| (registration packet)
     |                            |
     |---- 0xDD-01 (RESP) ------>| (success/already registered)
     |                            |
     |                            | (spawn listener thread)
     |                            |
     |<---- 0xEE-01 (START) -----| (program control)
     |---- 0xEE-01 (RESP) ------>|
     |                            |
     |<---- 0xBB (PROGRAM) ------| (program data transfer)
     |---- 0xBB (RESP) --------->|
     |                            |
     |                            | (spawn sender thread)
     |                            |
     |                            |----> UDP 0xCC (10000) every 10ms
     |                            |
     |                            |----> UDP Store (10001) every 10ms
```

### Device Simulation Strategy

1. **Registration**: Each device registers via TCP with unique device_id/circuit_id, device_name, IP, MAC
2. **Command Handling**: After registration, device listens for commands and responds appropriately
3. **Data Generation**: When program is STARTed, device sends real-time packets at configured interval
4. **State Machine**: Device tracks program_status (Idle/Running/Paused/Completed), circuit_status

### Packet Generation (matching DecoderService)

**Registration Packet (0xDD-01):**
- [0] 0xDD (start)
- [1] 0x01 (query)
- [2] length
- [3-4] device_id, circuit_id
- [5-20] device_name (16 bytes)
- [21-24] IP address
- [25-30] MAC address (6 bytes)
- [31-32] CRC16

**Real-time Packet (0xCC):**
- [0] 0xCC (start)
- [1-2] device_id, circuit_id
- [3] query_id
- [4-5] step_number
- [6] program_status
- [7] circuit_status
- [8] error_id
- [9-12] system_error_id (4 bytes)
- [13-16] step_running_time_ms
- [17-20] running_time_ms
- [21-24] current (float)
- [25-28] voltage (float)
- [29-32] temperature (float)
- [33-36] power (float)
- ... (all real-time fields per DecoderService)
- [last-2] CRC16

**Store Packet (0xCC):**
- Same format but with opcode markers for each field (1=timestamp, 2=current, etc.)
- Matches DecoderService.RealStoreData format

### Load Testing (100+ Devices)

- Each device runs in its own thread
- TCP connections established independently per device
- UDP sender thread per device for data generation
- Configurable packet interval (default 10ms)
- Random data generation with configurable ranges
- Optional error simulation (random system errors)

### Async/Threading Model

- Main thread: Creates all device objects
- Per-device threads (2 per device):
  - Command listener thread: Blocks on TCP socket, processes commands
  - UDP sender thread: Sleeps for packet_interval, generates and sends data
- Shared state protected by threading.Lock
- Non-blocking command processing with immediate responses

### Logging

- Configurable log level (DEBUG/INFO/WARNING/ERROR)
- Console and file output (default: simulator.log)
- Per-device logging with device_id prefix
- Stats tracking: packets_sent, commands_received, registrations, errors

### Configuration

config.json controls:
- server host/ports
- device_count (default: 10)
- circuit_count_per_device (default: 1)
- packet_interval_ms (default: 10ms)
- enable_random_data (bool)
- enable_error_simulation (bool)
- data_ranges for random values
- logging settings

## Implementation Files

```
HardwareSimulator/
├── config.json          # Simulator configuration
├── simulator.py         # Main simulator (this is the working implementation)
├── run_sim.py           # Quick launcher
├── TCP.py               # Original single-device stub (preserved)
└── TestUdp.py           # Original UDP test stub (preserved)
```

## Usage

```bash
# Default: 10 devices, 10ms interval
python simulator.py

# 100 devices
python simulator.py -d 100

# 50 devices, 5ms interval
python simulator.py -d 50 -i 5

# Remote server
python simulator.py -H 192.168.1.100
```

## Command Coverage

| Command Type | Query ID | Method | Implemented |
|--------------|----------|--------|-------------|
| Control (0xEE) | 0x01 | Start | Yes |
| Control (0xEE) | 0x02 | Stop | Yes |
| Control (0xEE) | 0x03 | Pause | Yes |
| Control (0xEE) | 0x04 | Continue | Yes |
| Control (0xEE) | 0x05 | SyncTime | Yes |
| Control (0xEE) | 0x06 | Reset | Yes |
| Program (0xBB) | 0x01 | HWReadyForProgram | Yes |
| Program (0xBB) | 0x03 | SendProgramStepsCount | Yes |
| Program (0xBB) | 0x04 | SendProgram | Yes |
| Program (0xBB) | 0x07 | SendDbcStepsCount | Yes |
| Program (0xBB) | 0x08 | SendDbcFile | Yes |
| Config (0xAA) | 0x01 | WriteBatteryParams | Yes |
| Config (0xAA) | 0x02 | ReadFactoryConfig | Yes |
| Config (0xAA) | 0x03 | ReadManufacturingConfig | Yes |
| Config (0xAA) | 0x05 | SyncTime | Yes |
| Calibration (0xA0) | 0x01 | HWReady | Yes |
| Calibration (0xA0) | 0x02 | SendLive | Yes |
| Calibration (0xA0) | 0x03-0x06 | PointPreset | Yes |
| Calibration (0xA0) | 0x10-0x13 | GainOffset | Yes |
| Calibration (0xA0) | 0x14 | PreviousCalibration | Yes |
| Calibration (0xA0) | 0x20 | Cancel | Yes |
| Calibration (0xA0) | 0x21 | Stop | Yes |
| Registration (0xDD) | 0x02 | Delete | Yes |