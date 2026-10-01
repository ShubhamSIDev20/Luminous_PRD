"""
Hardware Simulator for Battery Testing System
Simulates 100+ devices, handles all commands, generates live UDP data.
"""

import socket
import struct
import time
import uuid
import random
import json
import threading
import logging
import os
import select
from datetime import datetime
from enum import IntEnum
from typing import Dict, List, Optional
from dataclasses import dataclass, field
from collections import defaultdict

from program_decoder import ChunkReassembler, decode_program_steps
from dbc_decoder import decode_dbc_signals
from core_engine import CoreEngine, RUNNING as ENGINE_RUNNING

# =============================================================================
# ENUMS & CONSTANTS (matching BTS DecoderService)
# =============================================================================

class StartByte(IntEnum):
    CONTROL = 0xEE
    REGISTRATION = 0xDD
    PROGRAM = 0xBB
    CONFIGURATION = 0xAA
    REAL_TIME = 0xCC
    CALIBRATION = 0xA0

class CommandStatus(IntEnum):
    SUCCESS = 0x01
    FAILED = 0x00
    ALREADY_REGISTERED = 0x02

class ProgramRunningStatus(IntEnum):
    STOP = 0x00
    RUNNING = 0x01

class CircuitStatus(IntEnum):
    Idle = 0x00
    Charge = 0x01
    Discharging = 0x02
    Pause = 0x03
    Continue = 0x04
    Interrupt = 0x05
    Error = 0x06
    Msg = 0x07
    Offline = 0x08

class OperatorCode(IntEnum):
    """Mirrors Components/UI/Program/OperatorConstants.cs byte values exactly -
    the previous version of this enum used unrelated guessed values."""
    CC_CHG = 1
    CV_CHG = 2
    CP_CHG = 3
    CCCV_CHG = 4
    CC_DCHG = 5
    CP_DCHG = 6
    CCCV_DCHG = 7
    PAU = 8
    GOTO = 9
    SET = 10
    STO = 11
    CYC = 12
    BEG = 13
    INT = 14
    REG = 15
    ERR = 16
    MSG = 17
    TABLE = 18
    CV_DCHG = 19
    PRODUCER = 20
    CC_RECHG = 21
    LOCKAH = 22

CHARGE_OPERATORS = {OperatorCode.CC_CHG, OperatorCode.CV_CHG, OperatorCode.CP_CHG, OperatorCode.CCCV_CHG, OperatorCode.CC_RECHG}
DISCHARGE_OPERATORS = {OperatorCode.CC_DCHG, OperatorCode.CP_DCHG, OperatorCode.CCCV_DCHG, OperatorCode.CV_DCHG}

# =============================================================================
# CONFIGURATION
# =============================================================================

def load_config(path: str = "config.json") -> dict:
    if os.path.exists(path):
        with open(path, "r") as f:
            return json.load(f)
    return {}

# =============================================================================
# CRC16 (matching DecoderService.CalculateCRC16)
# =============================================================================

def calculate_crc16(data: bytes) -> int:
    crc = 0xFFFF
    for byte in data:
        crc ^= byte
        for _ in range(8):
            if crc & 0x0001:
                crc = (crc >> 1) ^ 0xA001
            else:
                crc >>= 1
    return crc

def _registrations_to_bitmask(active_regs) -> int:
    """Falls back to reporting the core telemetry set (opcodes 1-13, matching the
    simulator's pre-existing 0x1FFF default) when no registration has been resolved
    yet, so the store stream is never empty.

    TODO(follow-up): map RStandards bit values -> opcode 1-17 bits properly. Out of
    scope per docs/superpowers/specs/2026-08-11-hardware-simulator-coreengine-design.md's
    deferred DBC-signal-mapping note - this preserves today's behavior exactly.
    """
    if not active_regs:
        return 0x1FFF
    return 0x1FFF


def decode_address_byte(value: int) -> tuple:
    """ChannelAddressCodec.Decode equivalent - splits an address byte back into
    (board_number, channel_number), for logging what a raw wire byte means."""
    return ((value >> 4) & 0xF, value & 0xF)

def bind_crc16(data: bytes) -> bytes:
    crc = calculate_crc16(data)
    return data + struct.pack(">H", crc)

# =============================================================================
# BIG ENDIAN READERS (matching DecoderService)
# =============================================================================

def read_int16_be(data: bytes, index: int) -> int:
    return struct.unpack(">h", data[index:index+2])[0]

def read_int32_be(data: bytes, index: int) -> int:
    return struct.unpack(">i", data[index:index+4])[0]

def read_uint16_be(data: bytes, index: int) -> int:
    return struct.unpack(">H", data[index:index+2])[0]

def read_float_be(data: bytes, index: int) -> float:
    return struct.unpack(">f", data[index:index+4])[0]

def write_float_be(value: float) -> bytes:
    return struct.pack(">f", value)

def write_int16_be(value: int) -> bytes:
    return struct.pack(">h", value)

def write_int32_be(value: int) -> bytes:
    return struct.pack(">i", value)

# =============================================================================
# DEVICE SIMULATION STATE
# =============================================================================

@dataclass
class DeviceCircuit:
    device_id: int
    circuit_id: int
    device_name: str
    ip_address: str
    mac_address: str
    serial_number: int
    # Board/channel addressing (matches Utils.ChannelAddressCodec on the server:
    # high nibble = board 1-8, low nibble = channel 1-8 -> up to 64 channels/device).
    secondary_board_number: int = 1
    channel_number: int = 1
    is_dbc: bool = False
    # Runtime state
    program_status: int = 0
    circuit_status: int = 0
    current_step: int = 0
    step_number: int = 1
    cycle_number: int = 0

    # Live measurement values
    current: float = 0.0
    voltage: float = 12.0
    temperature: float = 25.0
    power: float = 0.0
    step_running_time_ms: int = 0
    running_time_ms: int = 0
    accumulated_capacity: float = 0.0
    charge_capacity: float = 0.0
    discharge_capacity: float = 0.0
    step_capacity: float = 0.0
    accumulated_energy: float = 0.0
    charge_energy: float = 0.0
    discharge_energy: float = 0.0
    step_energy: float = 0.0

    system_error_id: int = 0
    error_id: int = 0
    message_id: int = 0

    operator: int = 0x00
    CycleStatus: int = 0x00
    io_status: bytes = b"\x00\x00\x00"

    program_start_time: float = 0.0
    step_start_time: float = 0.0
    session_id: int = 0
    is_registered: bool = False

    table_step_number: int = 0
    program_steps: List[dict] = field(default_factory=list)
    current_step_index: int = 0
    program_hash: str = ""
    dbc_signals: dict = field(default_factory=dict)
    _program_reassembler: object = field(default_factory=lambda: None)
    _dbc_reassembler: object = field(default_factory=lambda: None)
    core_engine: object = field(default_factory=lambda: None)
    _core_sample: dict = field(default_factory=dict)
    _packet_tick_count: int = 0

    tcp_client: Optional[socket.socket] = None
    send_thread: Optional[threading.Thread] = None
    running: bool = False
    paused: bool = False

    config: dict = field(default_factory=dict)

    @property
    def address_byte(self) -> int:
        """ChannelAddressCodec.Encode(board, channel) equivalent - the byte the
        server decodes back into (SecondaryBoardNumber, ChannelNumber)."""
        return ((self.secondary_board_number & 0xF) << 4) | (self.channel_number & 0xF)

class HardwareSimulator:
    """Simulates multiple hardware devices for load testing."""

    def __init__(self, config_path: str = "config.json"):
        self.config = load_config(config_path)
        self.server_config = self.config.get("server", {})
        self.sim_config = self.config.get("simulation", {})
        self.data_ranges = self.config.get("data_ranges", {})

        self.host = self.server_config.get("host", "localhost")
        self.command_port = self.server_config.get("command_port", 9999)
        self.data_view_port = self.server_config.get("data_view_port", 10000)
        self.data_store_port = self.server_config.get("data_store_port", 10001)

        self.device_count = self.sim_config.get("device_count", 10)
        # Total channels per device (mapped across boards of 8: board=((c-1)//8)+1,
        # channel=((c-1)%8)+1), all sharing ONE TCP connection per device — mirrors
        # the server's per-device DeviceConnection multiplexing.
        self.circuit_count = self.sim_config.get(
            "channels_per_device", self.sim_config.get("circuit_count_per_device", 1)
        )
        # Lowest secondary-board number to hand out. Field devices register as
        # board 0 (1-0-1..1-0-8) while the legacy/implicit topology uses board 1 -
        # ChannelAddressCodec.Encode accepts 0-8, so both are valid on the wire.
        self.board_base = self.sim_config.get("secondary_board_base", 1)
        self.packet_interval_ms = self.sim_config.get("packet_interval_ms", 10)
        self.enable_random = self.sim_config.get("enable_random_data", True)
        self.enable_errors = self.sim_config.get("enable_error_simulation", False)
        self.error_rate = self.sim_config.get("error_rate_percent", 0.1)

        self.devices: Dict[str, DeviceCircuit] = {}
        self.command_socket: Optional[socket.socket] = None
        self.running = False
        self.lock = threading.Lock()

        self.logger = self._setup_logging()
        self.stats = {
            "packets_sent": 0,
            "commands_received": 0,
            "registrations": 0,
            "errors": 0
        }

    def _setup_logging(self) -> logging.Logger:
        log_config = self.config.get("logging", {})
        logger = logging.getLogger("HardwareSimulator")
        logger.setLevel(getattr(logging, log_config.get("level", "INFO")))

        formatter = logging.Formatter(
            "%(asctime)s [%(levelname)s] %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S"
        )

        if log_config.get("console_output", True):
            ch = logging.StreamHandler()
            ch.setFormatter(formatter)
            logger.addHandler(ch)

        log_file = log_config.get("log_file", "simulator.log")
        fh = logging.FileHandler(log_file)
        fh.setFormatter(formatter)
        logger.addHandler(fh)

        return logger

    def get_local_ip(self) -> str:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.connect(("8.8.8.8", 80))
            ip = s.getsockname()[0]
            s.close()
            return ip
        except:
            return "127.0.0.1"

    def get_mac_bytes(self) -> bytes:
        mac_int = uuid.getnode()
        return bytes([(mac_int >> i) & 0xFF for i in range(40, -1, -8)][:6])

    def create_devices(self):
        """Create simulated device circuits, one per channel, grouped by device_id
        so start_all_devices can share a single TCP connection across all of a
        device's channels (up to 8 boards x 8 channels = 64)."""
        if self.circuit_count > 64:
            self.logger.warning(
                f"channels_per_device={self.circuit_count} exceeds the 64-channel "
                f"(8 boards x 8 channels) hardware limit; clamping to 64."
            )
            self.circuit_count = 64

        if self.board_base not in (0, 1):
            self.logger.warning(
                f"secondary_board_base={self.board_base} is outside 0-1; the wire "
                f"address byte only encodes boards 0-8 (ChannelAddressCodec.Encode), "
                f"so a higher base cannot address 64 channels. Falling back to 1."
            )
            self.board_base = 1

        self.logger.info(
            f"Creating {self.device_count} devices with {self.circuit_count} channel(s) each "
            f"(boards numbered from {self.board_base})..."
        )

        device_defaults = self.config.get("device_defaults", {})
        name_prefix = device_defaults.get("device_name_prefix", "SIM_DEVICE_")
        base_ip = device_defaults.get("ip_address", "192.168.1.100")

        for d in range(1, self.device_count + 1):
            for c in range(1, self.circuit_count + 1):
                board_number = ((c - 1) // 8) + self.board_base
                channel_number = ((c - 1) % 8) + 1
                key = f"{d}-{c}"
                device = DeviceCircuit(
                    device_id=d,
                    circuit_id=c,
                    secondary_board_number=board_number,
                    channel_number=channel_number,
                    device_name=f"{name_prefix}{d:03d}_{c}",
                    ip_address=base_ip,
                    mac_address=uuid.uuid4().hex[:12].upper(),
                    serial_number=10000000 + d * 100 + c,
                    config=self.data_ranges
                )
                self.devices[key] = device
                self.logger.debug(
                    f"Created device: {key} - {device.device_name} "
                    f"(board={board_number}, channel={channel_number}, address=0x{device.address_byte:02X})"
                )

        self.logger.info(f"Created {len(self.devices)} device circuits across {self.device_count} device connection(s)")

    # -------------------------------------------------------------------------
    # REGISTRATION
    # -------------------------------------------------------------------------

    def build_registration_packet(self, device: DeviceCircuit) -> bytes:
        """Build registration packet matching DecoderService.ParseRegistrationPacket."""
        start_byte = 0xDD
        query_id = 0x01

        name_bytes = device.device_name.encode("ascii")[:16].ljust(16, b"\x00")

        ip_parts = [int(x) for x in device.ip_address.split(".")]
        mac_bytes = bytes.fromhex(device.mac_address.replace(":", ""))

        # device_id + address_byte (board/channel-encoded, per ChannelAddressCodec)
        # must come from the device — never hardcoded zeros.
        payload = struct.pack(">BBB", start_byte, query_id, 0x00)
        payload += struct.pack(">BB", device.device_id, device.address_byte)
        payload += name_bytes
        payload += bytes(ip_parts)
        payload += mac_bytes

        # 31-byte payload + 2-byte CRC16 = 33 bytes, matching ChannelManager's
        # fixed registration read buffer.
        return bind_crc16(payload)

    def parse_registration_response(self, response: bytes) -> Optional[CommandStatus]:
        """Parse registration response, matching DecoderService.ParseRegistrationResponse."""
        if response[0] != 0xDD or response[1] != 0x01:
            return None
        return CommandStatus(response[4])

    def register_device(self, client: socket.socket, device: DeviceCircuit) -> bool:
        """Perform TCP registration with the server."""
        tag = (
            f"{device.device_id}-{device.circuit_id} "
            f"(board={device.secondary_board_number} channel={device.channel_number} "
            f"addr=0x{device.address_byte:02X})"
        )
        try:
            packet = self.build_registration_packet(device)
            self.logger.info(f"[REGISTER] Device {tag} sending: {packet.hex(' ')}")

            client.sendall(packet)
            response = client.recv(1024)

            self.logger.info(f"[REGISTER] Device {tag} received response: {response.hex(' ')}")

            status = self.parse_registration_response(response)
            if status and status in (CommandStatus.SUCCESS, CommandStatus.ALREADY_REGISTERED):
                device.is_registered = True
                self.stats["registrations"] += 1
                self.logger.info(f"[REGISTER] Device {tag} registered successfully (status={status.name})")
                return True
            else:
                self.logger.warning(f"[REGISTER] Device {tag} registration failed: status={status}")
                return False

        except Exception as e:
            self.logger.error(f"[REGISTER] Error for {tag}: {e}")
            return False

    # -------------------------------------------------------------------------
    # REAL-TIME DATA PACKETS (0xCC - matching DecoderService.ParseRealTimeData)
    # -------------------------------------------------------------------------

    def build_realtime_packet(self, device: DeviceCircuit) -> bytes:
        """Build 0xCC real-time data packet matching DecoderService.ParseRealTimeData."""
        # Generate measurement data - use config ranges or sensible defaults
        ranges = device.config if device.config else {}
        core_running = device.core_engine is not None and device.core_engine.state == ENGINE_RUNNING
        # A circuit that has ever received a program never falls back to random
        # telemetry again, even after CoreEngine finishes (COMPLETED/STOPPED) - a
        # real device holds idle/zero at that point, it doesn't keep transmitting
        # random noise. Only a circuit that never got a program uses the fallback.
        has_program = device.core_engine is not None

        if self.enable_random and not has_program:
            # Add some device-specific offset for uniqueness
            device_id_offset = device.device_id * 0.1

            device.current = random.uniform(
                ranges.get("current_min", 0.5 + device_id_offset),
                ranges.get("current_max", 10.0 + device_id_offset)
            )
            device.voltage = random.uniform(
                ranges.get("voltage_min", 11.0 + device_id_offset),
                ranges.get("voltage_max", 13.5 + device_id_offset)
            )
            device.temperature = random.uniform(
                ranges.get("temperature_min", 22.0),
                ranges.get("temperature_max", 35.0)
            )

        # Ensure minimum values if not using random and not CoreEngine-driven
        if not self.enable_random and not has_program:
            if device.current < 0.1:
                device.current = 1.0 + (device.device_id * 0.5)
            if device.voltage < 10.0:
                device.voltage = 12.0 + (device.device_id * 0.1)

        if has_program and not core_running:
            # Program finished (or hasn't been started yet) - hold idle/zero rather
            # than freezing on the last real reading or drifting via random noise.
            device.current = 0.0
            device.voltage = 0.0

        if core_running:
            # CoreEngine already computed power/capacity/energy for this tick -
            # pull them straight from its sample instead of recomputing here.
            sample = device._core_sample
            device.power = sample["power"]
            device.accumulated_capacity = sample["accumulated_capacity"]
            device.charge_capacity = sample["charge_capacity"]
            device.discharge_capacity = sample["discharge_capacity"]
            device.step_capacity = sample["step_capacity"]
            device.accumulated_energy = sample["accumulated_energy"]
            device.charge_energy = sample["charge_energy"]
            device.discharge_energy = sample["discharge_energy"]
            device.step_energy = sample["step_energy"]
            if device.program_status == ProgramRunningStatus.RUNNING and device.program_start_time > 0:
                device.step_running_time_ms = int((time.time() - device.step_start_time) * 1000)
                device.running_time_ms = int((time.time() - device.program_start_time) * 1000)
        else:
            device.power = device.current * device.voltage
            device.accumulated_capacity += device.current * (self.packet_interval_ms / 3600000.0)

            if device.program_status == ProgramRunningStatus.RUNNING and device.program_start_time > 0:
                device.step_running_time_ms = int((time.time() - device.step_start_time) * 1000)
                device.running_time_ms = int((time.time() - device.program_start_time) * 1000)
                device.charge_capacity = device.accumulated_capacity if device.operator in CHARGE_OPERATORS else 0
                device.discharge_capacity = device.accumulated_capacity if device.operator in DISCHARGE_OPERATORS else 0
                device.step_capacity = device.accumulated_capacity
                device.accumulated_energy += device.power * (self.packet_interval_ms / 3600000.0)
                device.charge_energy = device.accumulated_energy if device.charge_capacity > 0 else 0
                device.discharge_energy = device.accumulated_energy if device.discharge_capacity > 0 else 0
                device.step_energy = device.accumulated_energy

        if self.enable_errors and random.random() * 100 < self.error_rate:
            device.system_error_id = random.choice([1, 2, 4, 8])
        else:
            device.system_error_id = 0

        device.io_status = bytes([random.randint(0, 255) for _ in range(3)])

        payload = struct.pack(
            ">BBBBhBBB",
            StartByte.REAL_TIME,
            device.device_id,
            device.address_byte,
            0x01,
            device.step_number,
            device.program_status,
            device.circuit_status,
            device.error_id
        )

        payload += struct.pack(">i", device.system_error_id)
        payload += struct.pack(">i", device.step_running_time_ms)
        payload += struct.pack(">i", device.running_time_ms)
        payload += struct.pack(">f", device.current)
        payload += struct.pack(">f", device.voltage)
        payload += struct.pack(">f", device.temperature)
        payload += struct.pack(">f", device.power)
        payload += struct.pack(">f", device.accumulated_capacity)
        payload += struct.pack(">f", device.charge_capacity)
        payload += struct.pack(">f", device.discharge_capacity)
        payload += struct.pack(">f", device.step_capacity)
        payload += struct.pack(">f", device.accumulated_energy)
        payload += struct.pack(">f", device.charge_energy)
        payload += struct.pack(">f", device.discharge_energy)
        payload += struct.pack(">f", device.step_energy)
        # Fixed from the previous 5x struct.pack(">HH", ...) block, which wrote 20
        # bytes for what DecoderService.ParseRealTimeData expects as 5 distinct
        # 2-byte fields (10 bytes total), misaligning IOStatus/CRC on every packet.
        core_sample = device._core_sample if core_running else {}
        payload += struct.pack("B", device.operator)
        payload += struct.pack("B", device.CycleStatus)
        payload += struct.pack(">H", core_sample.get("cycle_number", device.cycle_number))
        payload += struct.pack(">H", core_sample.get("cycle_run_iteration", 0))
        payload += struct.pack(">H", core_sample.get("table_step_number", device.table_step_number))
        payload += struct.pack(">H", core_sample.get("table_total_row_number", 0))
        payload += struct.pack(">H", 0)  # reserved
        payload += device.io_status

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    # -------------------------------------------------------------------------
    # STORE DATA PACKETS (matching DecoderService.RealStoreData)
    # -------------------------------------------------------------------------

    def build_store_packet(self, device: DeviceCircuit) -> bytes:
        """Build store data packet matching DecoderService.RealStoreData format."""
        if device.session_id == 0:
            device.session_id = int(time.time())

        payload = struct.pack(">B", StartByte.REAL_TIME)
        payload += struct.pack(">BB", device.device_id, device.address_byte)
        payload += struct.pack(">B", 0x01)
        payload += struct.pack(">i", device.session_id)
        payload += struct.pack(">h", device.step_number)
        payload += struct.pack("B", device.operator)
        payload += struct.pack("B", device.circuit_status)
        active_regs = device._core_sample.get("active_registrations") if device.core_engine else None
        payload += struct.pack(">H", _registrations_to_bitmask(active_regs))

        opcodes = [
            (1, device.step_running_time_ms),
            (2, device.current),
            (3, device.voltage),
            (4, device.temperature),
            (5, device.power),
            (6, device.accumulated_capacity),
            (7, device.charge_capacity),
            (8, device.discharge_capacity),
            (9, device.step_capacity),
            (10, device.accumulated_energy),
            (11, device.charge_energy),
            (12, device.discharge_energy),
            (13, device.step_energy),
        ]

        for opcode, value in opcodes:
            if isinstance(value, int):
                # payload += struct.pack("B", opcode)
                payload += struct.pack(">i", value)
            else:
                # payload += struct.pack("B", opcode)
                payload += struct.pack(">f", value)

        if device.system_error_id > 0:
            payload += struct.pack("B", 14)
            payload += struct.pack(">i", device.system_error_id)

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    # -------------------------------------------------------------------------
    # CALIBRATION PACKETS (0xA0 - matching DecoderService.ParseRealTimeData calibration)
    # -------------------------------------------------------------------------

    def build_calibration_packet(self, device: DeviceCircuit) -> bytes:
        """Build 0xA0 calibration data packet."""
        payload = struct.pack(
            ">BBBBB",
            StartByte.CALIBRATION,
            device.device_id,
            device.address_byte,
            0x14,
            0x00
        )
        payload += struct.pack(">f", device.current)
        payload += struct.pack(">i", int(device.current * 1000))
        payload += struct.pack(">f", device.voltage)
        payload += struct.pack(">i", int(device.voltage * 100))
        payload += struct.pack("B", 0x00)

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    # -------------------------------------------------------------------------
    # SEND UDP DATA
    # -------------------------------------------------------------------------

    def send_udp_data(self, device: DeviceCircuit, store: bool = False):
        """Send UDP data to server."""
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

            packet = self.build_realtime_packet(device)
            sock.sendto(packet, (self.host, self.data_view_port))

            if store:
                store_packet = self.build_store_packet(device)
                sock.sendto(store_packet, (self.host, self.data_store_port))

            sock.close()

        except Exception as e:
            self.stats["errors"] += 1
            self.logger.error(f"[UDP] Send error for {device.device_id}-{device.circuit_id}: {e}")

    # -------------------------------------------------------------------------
    # TCP COMMAND HANDLING (all command types matching CircuitCommandHandler)
    # -------------------------------------------------------------------------

    def handle_control_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xEE Control commands - matching CircuitCommandHandler methods."""
        if len(data) < 4:
            return

        device_id = data[1]
        circuit_id = data[2]  # address byte: board in high nibble, channel in low nibble
        query_id = data[3]
        board_number, channel_number = decode_address_byte(circuit_id)

        self.logger.info(
            f"[CMD-EE] Device {device_id} board={board_number} channel={channel_number} "
            f"(addr=0x{circuit_id:02X}) query={query_id:02X} data={data[4:-2].hex(' ')}"
        )
        self.stats["commands_received"] += 1

        result = CommandStatus.SUCCESS
        response_data = b""

        if query_id == 0x01:  # START
            self.logger.info(f"[CMD] START - Device {device_id}-{circuit_id}")
            device.program_status = ProgramRunningStatus.RUNNING
            device.circuit_status = CircuitStatus.Charge
            device.program_start_time = time.time()
            device.step_start_time = time.time()
            device.running_time_ms = 0
            device.step_running_time_ms = 0
            device.accumulated_capacity = 0.0
            device.accumulated_energy = 0.0
            device.current_step_index = 0
            device.step_number = 1
            device.session_id = int(time.time())

            if len(data) > 4:
                device.session_id = read_int32_be(data[4:8], 0)

            if device.core_engine is None:
                device.core_engine = CoreEngine()
            device.core_engine.load_program(device.program_steps)
            device.core_engine.start()

            self.logger.info(
                f"[START] Device {device_id}-{circuit_id} session={device.session_id}"
            )

        elif query_id == 0x02:  # STOP
            self.logger.info(f"[CMD] STOP - Device {device_id}-{circuit_id}")
            device.program_status = ProgramRunningStatus.STOP
            device.circuit_status = CircuitStatus.Idle
            # Don't set device.running = False - keep registered for continued communication
            device.paused = False
            if device.core_engine is not None:
                device.core_engine.stop()
            self.logger.info(
                f"[STOP] Device {device_id}-{circuit_id} "
                f"stored {device.accumulated_capacity:.2f} Ah"
            )

        elif query_id == 0x03:  # PAUSE
            self.logger.info(f"[CMD] PAUSE - Device {device_id}-{circuit_id}")
            device.paused = True
            device.program_status = ProgramRunningStatus.RUNNING

        elif query_id == 0x04:  # CONTINUE
            self.logger.info(f"[CMD] CONTINUE - Device {device_id}-{circuit_id}")
            device.paused = False
            device.program_status = ProgramRunningStatus.RUNNING

        elif query_id == 0x05:  # SYNC TIME
            self.logger.info(f"[CMD] SYNC TIME - Device {device_id}-{circuit_id}")
            if len(data) >= 8:
                ts = read_int32_be(data, 4)
                dt = datetime.fromtimestamp(ts)
                self.logger.info(f"[SYNC] Timestamp: {ts} = {dt}")

        elif query_id == 0x06:  # SYSTEM RESET
            self.logger.info(f"[CMD] RESET - Device {device_id}-{circuit_id}")
            device.system_error_id = 0
            device.error_id = 0

        # Layout must match DecoderService.BuildCommand / TryDecode: [identity,
        # device_id, addressByte, query_id, result] - addressByte at index 2 is
        # what ChannelManager.HandleCommandClientAsync routes responses on, and
        # query_id at index 3 is what TryDecode reads back out. This previously
        # packed (query_id, device_id, circuit_id) in the wrong order, which put
        # device_id (identical across every channel on the device) at index 2 -
        # every control-command ack on a multi-channel device then routed to
        # whichever pending request happened to be keyed under that device_id,
        # cross-wiring Start/Stop acks between channels and timing out the rest.
        response = struct.pack(">BBBBB", 0xEE, device_id, circuit_id, query_id, result)
        response += b"\x00\x00"
        response = bind_crc16(response)

        client.sendall(response)
        self.logger.debug(f"[RESP-EE] Sent: {response.hex(' ')}")

    def handle_program_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xBB Program commands - matching CircuitCommandHandler methods."""
        if len(data) < 4:
            return

        query_id = data[3]
        device_id = data[1]
        circuit_id = data[2]  # address byte: board in high nibble, channel in low nibble
        board_number, channel_number = decode_address_byte(circuit_id)

        self.logger.info(
            f"[CMD-BB] Device {device_id} board={board_number} channel={channel_number} "
            f"(addr=0x{circuit_id:02X}) query={query_id:02X} len={len(data)}"
        )
        self.stats["commands_received"] += 1

        result = CommandStatus.SUCCESS
        response = struct.pack(">BBBBB", 0xBB, device_id, circuit_id, query_id, result)
        response += b"\x00\x00"
        response = bind_crc16(response)

        if query_id == 0x01:  # HW Ready for Program
            self.logger.info(f"[CMD] HW_READY - Device {device_id}-{circuit_id}")
            self.is_dbc = False
            result = CommandStatus.SUCCESS

        elif query_id == 0x03:  # Program Steps Count
            self.logger.info(f"[CMD] PROGRAM_STEPS_COUNT - Device {device_id}-{circuit_id}")
            if len(data) >= 7:
                count = read_int16_be(data, 4)
                self.logger.info(f"[CMD] Will receive {count} program step chunks")
                device._program_reassembler = device._program_reassembler or ChunkReassembler()
                device._program_reassembler.begin(count)

        elif query_id == 0x04:  # Program Data
            self.logger.debug(f"[CMD] PROGRAM_DATA chunk - Device {device_id}-{circuit_id}")
            if device._program_reassembler is not None:
                full = device._program_reassembler.add_chunk(data[4:-2])  # strip trailing 2-byte CRC
                if full is not None:
                    steps = decode_program_steps(full)
                    if all(s["parse_ok"] for s in steps):
                        device.program_steps = steps
                        self.logger.info(f"[CMD] Decoded {len(steps)} program steps for {device.device_id}-{device.circuit_id}")
                    else:
                        self.logger.error(f"[CMD] Program decode failed for {device.device_id}-{device.circuit_id} - keeping previous program_steps")

        elif query_id == 0x07:  # DBC Steps Count
            self.logger.info(f"[CMD] DBC_STEPS_COUNT - Device {device_id}-{circuit_id}")
            if len(data) >= 7:
                count = read_int16_be(data, 4)
                self.logger.info(f"[CMD] Will receive {count} DBC file chunks")
                device._dbc_reassembler = device._dbc_reassembler or ChunkReassembler()
                device._dbc_reassembler.begin(count)

        elif query_id == 0x08:  # DBC File Data
            self.logger.debug(f"[CMD] DBC_FILE_DATA chunk - Device {device_id}-{circuit_id}")
            self.is_dbc = True
            if device._dbc_reassembler is not None:
                full = device._dbc_reassembler.add_chunk(data[4:-2])  # strip trailing 2-byte CRC
                if full is not None:
                    try:
                        device.dbc_signals = decode_dbc_signals(full)
                        self.logger.info(f"[CMD] Decoded DBC signals for {device.device_id}-{device.circuit_id}")
                    except Exception as e:
                        self.logger.error(f"[CMD] DBC decode failed for {device.device_id}-{device.circuit_id}: {e}")

        elif query_id == 0x09:  # Live Step Update (Q9) - amend the currently-executing step's
            # PARAMETERS ONLY. Unlike every other 0xBB query above, the response carries a second
            # byte (REASON) alongside STATUS, so this builds and sends its own frame and returns
            # early, bypassing the generic single-status-byte footer below.
            self.logger.info(f"[CMD] LIVE_STEP_UPDATE - Device {device_id}-{circuit_id}")
            status, reason = CommandStatus.FAILED, 0x06  # default: malformed, until proven otherwise

            if len(data) >= 8:
                try:
                    steps = decode_program_steps(data[4:-2])  # LEN(2B) + one AA55...55AA step packet
                except Exception:
                    steps = []

                if len(steps) != 1 or not steps[0].get("parse_ok", False):
                    status, reason = CommandStatus.FAILED, 0x06  # malformed step packet
                elif device.core_engine is None:
                    status, reason = CommandStatus.FAILED, 0x01  # no program running or paused
                else:
                    step = steps[0]
                    ok, reason_code = device.core_engine.apply_live_step_update(
                        step["step_id"], step["operator"], step["nominal"], step["limits"]
                    )
                    status = CommandStatus.SUCCESS if ok else CommandStatus.FAILED
                    reason = reason_code

            self.logger.info(
                f"[LIVE_STEP_UPDATE] Device {device_id}-{circuit_id} status={status} reason=0x{reason:02X}"
            )
            response = struct.pack(">BBBBBB", 0xBB, device_id, circuit_id, query_id, status, reason)
            response = bind_crc16(response)
            client.sendall(response)
            self.logger.debug(f"[RESP-BB] Sent: {response.hex(' ')}")
            return

        elif query_id == 0x0A:  # JUMP TO STEP (bm_program_v3.2 Q10)
            # Own response shape: STATUS + REASON, unlike every other 0xBB query here
            # (STATUS-only) - built and sent directly, bypassing the generic footer below.
            if len(data) < 6:
                status, reason = CommandStatus.FAILED, 0x03  # malformed frame (wrong length)
            else:
                step_number = read_uint16_be(data, 4)
                status, reason = self._handle_jump_to_step(device, step_number)
                self.logger.info(
                    f"[CMD] JUMP_TO_STEP({step_number}) - Device {device_id}-{circuit_id} "
                    f"-> status={status} reason=0x{reason:02X}"
                )
            response = struct.pack(">BBBBBB", 0xBB, device_id, circuit_id, query_id, status, reason)
            response += b"\x00"
            response = bind_crc16(response)
            client.sendall(response)
            self.logger.debug(f"[RESP-BB] Sent: {response.hex(' ')}")
            return

        response = struct.pack(">BBBBB", 0xBB, device_id, circuit_id, query_id, result)
        response += b"\x00\x00"
        response = bind_crc16(response)
        client.sendall(response)
        self.logger.debug(f"[RESP-BB] Sent: {response.hex(' ')}")

    def _handle_jump_to_step(self, device: "DeviceCircuit", step_number: int) -> tuple:
        """bm_program_v3.2 Q10. Returns (STATUS, REASON) - REASON mirrors
        Models/Enums/CircuitEnums.cs::JumpToStepReason on the C# side.
        """
        if device.paused or device.core_engine is None or device.core_engine.state != ENGINE_RUNNING:
            # This simulator merges the Primary+Secondary roles and only tracks one
            # "not actively executing" flag (device.paused, set by the 0xEE PAUSE command)
            # plus the engine's own IDLE/STOPPED state - both are exactly what the doc's
            # REASON 0x01 ("no program is running or paused on this circuit") covers, decided
            # locally without ever needing to ask a Secondary. It does not model the real
            # hardware's separate interrupt/error/message-wait states, so those (which reach
            # the Web App as 0x04) have no equivalent to simulate here.
            return CommandStatus.FAILED, 0x01

        ok, reason = device.core_engine.jump_to_step(step_number)
        return (CommandStatus.SUCCESS if ok else CommandStatus.FAILED), reason

    def handle_configuration_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xAA Configuration commands - matching CircuitCommandHandler methods."""
        if len(data) < 4:
            return

        query_id = data[3]
        device_id = data[1]
        circuit_id = data[2]  # address byte: board in high nibble, channel in low nibble
        board_number, channel_number = decode_address_byte(circuit_id)

        self.logger.info(
            f"[CMD-AA] Device {device_id} board={board_number} channel={channel_number} "
            f"(addr=0x{circuit_id:02X}) query={query_id:02X}"
        )
        self.stats["commands_received"] += 1

        if query_id == 0x01:  # Write Battery Params
            self.logger.info(f"[CMD] WRITE_BATTERY_PARAMS - Device {device_id}-{circuit_id}")
            if len(data) >= 50:
                nom_capacity = read_float_be(data, 4)
                num_cells = data[8]
                max_voltage = read_float_be(data, 12)
                self.logger.info(
                    f"[CMD] Battery: cap={nom_capacity:.2f}Ah "
                    f"cells={num_cells} maxV={max_voltage:.2f}V"
                )

        elif query_id == 0x02:  # Read Factory Config
            self.logger.info(f"[CMD] READ_FACTORY_CONFIG - Device {device_id}-{circuit_id}")
            response = self.build_factory_config_response(device)
            client.sendall(response)
            self.logger.debug(f"[RESP-AA-FACTORY] Sent ({len(response)} bytes)")
            return

        elif query_id == 0x03:  # Read Manufacturing Config
            self.logger.info(f"[CMD] READ_MANUFACTURING_CONFIG - Device {device_id}-{circuit_id}")
            response = self.build_manufacturing_config_response(device)
            client.sendall(response)
            self.logger.debug(f"[RESP-AA-MFG] Sent ({len(response)} bytes)")
            return

        elif query_id == 0x05:  # Sync Time
            self.logger.info(f"[CMD] SYNC_TIME - Device {device_id}-{circuit_id}")

        response = struct.pack(">BBBBB", 0xAA, device_id, circuit_id, query_id, 0x01)
        response += b"\x00\x00"
        response = bind_crc16(response)
        client.sendall(response)
        self.logger.debug(f"[RESP-AA] Sent: {response.hex(' ')}")

    def handle_calibration_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xA0 Calibration commands - matching CircuitCommandHandler methods."""
        if len(data) < 4:
            return

        query_id = data[3]
        device_id = data[1]
        circuit_id = data[2]  # address byte: board in high nibble, channel in low nibble
        board_number, channel_number = decode_address_byte(circuit_id)

        self.logger.info(
            f"[CMD-A0] Device {device_id} board={board_number} channel={channel_number} "
            f"(addr=0x{circuit_id:02X}) raw data hex: {' '.join(f'{b:02X}' for b in data)}"
        )
        self.stats["commands_received"] += 1

        if query_id == 0x01:  # HW Ready for Calibration
            self.logger.info(f"[CMD] CAL_HW_READY - Device {device_id}-{circuit_id}")

        elif query_id == 0x02:  # Send Live Current/Voltage
            self.logger.info(f"[CMD] CAL_SEND_LIVE - Device {device_id}-{circuit_id}")
            response = self.build_calibration_response(device)
            client.sendall(response)
            return

        elif query_id in (0x03, 0x04, 0x05, 0x06):  # Calibration Point Preset
            self.logger.info(
                f"[CMD] CAL_POINT_PRESET({query_id:02X}) - "
                f"Device {device_id}-{circuit_id}"
            )
            if len(data) >= 9:
                value = read_float_be(data, 4)
                self.logger.info(f"[CMD] Calibration value: {value}")

        elif query_id == 0x10:  # Set Current Charge Gain/Offset
            self.logger.info(f"[CMD] CAL_SET_CURRENT_CHARGE - Device {device_id}-{circuit_id}")

        elif query_id == 0x11:  # Set Current Discharge Gain/Offset
            self.logger.info(f"[CMD] CAL_SET_CURRENT_DISCHARGE - Device {device_id}-{circuit_id}")

        elif query_id == 0x12:  # Set Voltage Charge Gain/Offset
            self.logger.info(f"[CMD] CAL_SET_VOLTAGE_CHARGE - Device {device_id}-{circuit_id}")

        elif query_id == 0x13:  # Set Voltage Discharge Gain/Offset
            self.logger.info(f"[CMD] CAL_SET_VOLTAGE_DISCHARGE - Device {device_id}-{circuit_id}")

        elif query_id == 0x20:  # Cancel Calibration
            self.logger.info(f"[CMD] CAL_CANCEL - Device {device_id}-{circuit_id}")

        elif query_id == 0x21:  # Stop Calibration
            self.logger.info(f"[CMD] CAL_STOP - Device {device_id}-{circuit_id}")

        elif query_id == 0x14:  # Previous Calibration Data
            self.logger.info(f"[CMD] CAL_READ_PREVIOUS - Device {device_id}-{circuit_id}")
            response = self.build_calibration_previous_response(device)
            client.sendall(response)
            return

        response = struct.pack(">BBBBB", 0xA0, device_id, circuit_id, query_id, 0x01)
        response += b"\x00\x00"
        response = bind_crc16(response)
        client.sendall(response)
        self.logger.debug(f"[RESP-A0] Sent: {response.hex(' ')}")

    def handle_registration_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xDD Registration commands."""
        if len(data) < 4:
            return

        query_id = data[1]
        device_id = data[2]
        circuit_id = data[3]  # address byte: board in high nibble, channel in low nibble
        board_number, channel_number = decode_address_byte(circuit_id)

        self.logger.info(
            f"[CMD-DD] Device {device_id} board={board_number} channel={channel_number} "
            f"(addr=0x{circuit_id:02X}) query={query_id:02X}"
        )
        self.stats["commands_received"] += 1

        if query_id == 0x02:  # Delete/Unregister
            self.logger.info(f"[CMD] UNREGISTER - Device {device_id}-{circuit_id}")
            device.is_registered = False
            device.running = False

        response = struct.pack(">BBBBB", 0xDD, query_id, device_id, circuit_id, 0x01)
        response += b"\x00\x00"
        response = bind_crc16(response)
        client.sendall(response)

    def build_factory_config_response(self, device: DeviceCircuit) -> bytes:
        """Build factory config response (0xAA 0x02) matching DecoderService.FactoryParameters."""
        payload = struct.pack(">BBBB", 0xAA, device.device_id, device.address_byte, 0x02)

        mac_bytes = bytes.fromhex(device.mac_address.replace(":", ""))
        payload += mac_bytes

        ip_parts = [int(x) for x in device.ip_address.split(".")]
        payload += bytes(ip_parts)

        remote_ip = [127, 0, 0, 1]
        payload += bytes(remote_ip)

        payload += struct.pack(">HHH", 9999, 10000, 10001)
        payload += struct.pack("BB", 1, 0)  # dhcp_enabled=1 (True), circuit_type=0 (Single Transistor Bank)

        payload += struct.pack(">f", 14.4)
        payload += struct.pack(">f", 10.8)
        payload += struct.pack(">f", 15.0)
        payload += struct.pack(">f", 10.0)
        payload += struct.pack(">f", 50.0)
        payload += struct.pack(">f", 50.0)
        payload += struct.pack("B", 1)

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    def build_manufacturing_config_response(self, device: DeviceCircuit) -> bytes:
        """Build manufacturing config response (0xAA 0x03) matching DecoderService.ManufacturingParameters."""
        payload = struct.pack(">BBBB", 0xAA, device.device_id, device.address_byte, 0x03)

        payload += b"V4.26.2022\x00"       # 10 chars + 1 null = 11 bytes (matches DecoderService's fixed 11-byte field)
        payload += b"COM_V1.0\x00\x00\x00"  # 8 chars + 3 nulls = 11 bytes
        payload += b"SEC_V2.1\x00\x00\x00"  # 8 chars + 3 nulls = 11 bytes
        payload += struct.pack(">I", device.serial_number)
        payload += struct.pack(">I", device.serial_number + 1)
        payload += struct.pack(">I", int(time.time()) - 86400 * 365)
        payload += struct.pack(">I", int(time.time()) - 86400 * 30)
        payload += struct.pack(">I", int(time.time()) - 86400 * 180)
        payload += struct.pack(">I", int(time.time()) - 86400 * 150)

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    def build_calibration_response(self, device: DeviceCircuit) -> bytes:
        """Build calibration data response matching DecoderService.ParseCalibrationPayload."""
        payload = struct.pack(">BBBB", 0xA0, device.device_id, device.address_byte, 0x14)

        for _ in range(4):
            payload += struct.pack(">f", 1.0)
            payload += struct.pack(">f", 0.0)
            payload += struct.pack(">I", int(time.time()))

        crc = calculate_crc16(payload)
        return payload + struct.pack(">H", crc)

    def build_calibration_previous_response(self, device: DeviceCircuit) -> bytes:
        """Build previous calibration data response."""
        return self.build_calibration_response(device)

    # -------------------------------------------------------------------------
    # COMMAND LISTENER
    # -------------------------------------------------------------------------

    def command_listener(self, device_id: int, client: socket.socket, circuits_by_address: Dict[int, "DeviceCircuit"]):
        """Single read-dispatch loop for one device's shared TCP connection - the
        Python-side mirror of ChannelManager's per-device DeviceConnection
        multiplexing read-loop. Every incoming packet carries an address byte
        (board/channel-encoded) that names which channel-slot it's for; unlike
        the old per-circuit listener, this does NOT skip reading while any one
        channel is paused, since other channels on the same socket still need
        service."""
        self.logger.info(
            f"[LISTENER] Device {device_id} listening on TCP for "
            f"{len(circuits_by_address)} channel(s)..."
        )

        try:
            while self.running:
                try:
                    ready, _, _ = select.select([client], [], [], 0.5)
                    if not ready:
                        continue

                    data = client.recv(1024)
                    if not data:
                        self.logger.info(f"[DISCONNECT] Server closed connection for device {device_id}")
                        break

                    identity = data[0]

                    if identity == 0xDD:
                        # Unregister/registration-echo carries the address byte at
                        # data[3] (DecoderService.BuildRegistration layout), not data[2].
                        if len(data) < 4:
                            continue
                        circuit = circuits_by_address.get(data[3])
                        if circuit is None:
                            self.logger.warning(
                                f"[LISTENER] Device {device_id}: unknown channel address 0x{data[3]:02X}"
                            )
                            continue
                        self.handle_registration_command(data, client, circuit)
                        continue

                    if len(data) < 3:
                        self.logger.warning(f"[LISTENER] Device {device_id}: short packet {data.hex(' ')}")
                        continue

                    circuit = circuits_by_address.get(data[2])
                    if circuit is None:
                        self.logger.warning(
                            f"[LISTENER] Device {device_id}: unknown channel address 0x{data[2]:02X}"
                        )
                        continue

                    if identity == 0xEE:
                        self.handle_control_command(data, client, circuit)
                    elif identity == 0xBB:
                        self.handle_program_command(data, client, circuit)
                    elif identity == 0xAA:
                        self.handle_configuration_command(data, client, circuit)
                    elif identity == 0xA0:
                        self.handle_calibration_command(data, client, circuit)
                    else:
                        self.logger.warning(f"[UNKNOWN] Identity {identity:02X}: {data.hex(' ')}")

                except (ConnectionResetError, BrokenPipeError, OSError) as e:
                    self.logger.info(f"[LISTENER] Connection closed for device {device_id}: {e}")
                    break
                except Exception as e:
                    self.logger.error(f"[LISTENER] Error on device {device_id}: {e}")
                    break
        finally:
            # Shared connection dropped - every channel-slot on this device goes
            # offline together, mirroring ChannelManager's per-device disconnect fan-out.
            for circuit in circuits_by_address.values():
                circuit.running = False
            self.logger.info(f"[LISTENER] Exiting for device {device_id}")

    def data_sender(self, device: DeviceCircuit):
        """Send UDP data packets at configured interval."""
        self.logger.info(
            f"[SENDER] Device {device.device_id}-{device.circuit_id} "
            f"(board={device.secondary_board_number} channel={device.channel_number}) "
            f"starting UDP sender (interval={self.packet_interval_ms}ms) "
            f"-> {self.host}:{self.data_view_port}"
        )

        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

        first_send = True
        while device.running and device.is_registered:
            try:
                if device.paused:
                    time.sleep(0.5)
                    continue

                if device.core_engine is not None and device.core_engine.state == ENGINE_RUNNING:
                    sample = device.core_engine.tick(self.packet_interval_ms)
                    device.current = sample["current"]
                    device.voltage = sample["voltage"]
                    device.power = sample["power"]
                    device.temperature = sample["temperature"]
                    device.step_number = sample["step_number"]
                    device.operator = sample["operator"]
                    device.cycle_number = sample["cycle_number"]
                    device.table_step_number = sample["table_step_number"]
                    device._core_sample = sample  # full field set for build_store_packet's registrations

                    if device.core_engine.state != ENGINE_RUNNING:
                        # CoreEngine reached its program's real end (STO step or an
                        # exhausted step list) this tick. A real device would stop
                        # transmitting store data at this point; without this, the
                        # simulator kept sending Operator=STO store packets forever,
                        # making the server call EndSession repeatedly for an
                        # already-finished session.
                        # NOTE: the app's ProgramRunningStatus enum only defines
                        # Stop=0x00/Running=0x01 (no COMPLETED). Sending COMPLETED
                        # here left ps != Stop forever after natural completion, so
                        # DashboardView.CanContextAction's `ps == Stop` checks never
                        # passed and every action button but Stop stayed disabled.
                        device.program_status = ProgramRunningStatus.STOP
                        device.circuit_status = CircuitStatus.Idle

                # Always send live data when registered (not just when program running)
                packet = self.build_realtime_packet(device)
                try:
                    sock.sendto(packet, (self.host, self.data_view_port))
                    self.stats["packets_sent"] += 1
                    if first_send:
                        self.logger.info(f"[SENDER] First packet sent! {len(packet)} bytes to {self.host}:{self.data_view_port}")
                        first_send = False
                except Exception as e:
                    self.logger.error(f"[SENDER] sendto error: {e}")

                # Only send store data when program is actually running
                if device.program_status == ProgramRunningStatus.RUNNING:
                    store_packet = self.build_store_packet(device)
                    try:
                        sock.sendto(store_packet, (self.host, self.data_store_port))
                    except Exception as e:
                        self.logger.error(f"[SENDER] store sendto error: {e}")


                if device._packet_tick_count % 100 == 0:
                    self.logger.debug(
                        f"[UDP] {device.device_id}-{device.circuit_id} "
                        f"(board={device.secondary_board_number} channel={device.channel_number}) "
                        f"V={device.voltage:.2f}V I={device.current:.2f}A "
                        f"T={device.temperature:.1f}C"
                    )

                device._packet_tick_count += 1

                time.sleep(self.packet_interval_ms / 1000.0)

            except Exception as e:
                if device.running:
                    self.logger.error(f"[SENDER] Error: {e}")
                break

        sock.close()
        self.logger.info(
            f"[SENDER] Stopped for {device.device_id}-{device.circuit_id}"
        )

    # -------------------------------------------------------------------------
    # DEVICE LIFECYCLE
    # -------------------------------------------------------------------------

    def start_device_connection(self, device_id: int, circuits: List["DeviceCircuit"]):
        """Connect ONE TCP socket for this device, register every one of its
        channels over it sequentially, then run a single shared listener +
        per-channel UDP senders. Mirrors the server's DeviceConnection: one
        socket shared by up to 64 channel-slots (8 boards x 8 channels)."""
        retry_delay = 2
        max_retry_delay = 30
        circuits_by_address = {c.address_byte: c for c in circuits}

        while self.running:
            listener = None
            senders: List[threading.Thread] = []
            client = None
            try:
                # Tear down any previous shared connection cleanly BEFORE creating a
                # new socket. CRITICAL: do NOT set running=False before closing the
                # old socket — the old listener thread is alive and using it via
                # select(). Close the old socket first so the listener gets an error
                # and exits, THEN flip running=False so it stops looping.
                old_sock = circuits[0].tcp_client
                if old_sock:
                    for c in circuits:
                        c.tcp_client = None       # detach first so listener sees None
                        c.is_registered = False
                    try:
                        old_sock.shutdown(socket.SHUT_RDWR)
                    except Exception:
                        pass
                    try:
                        old_sock.close()
                    except Exception:
                        pass
                for c in circuits:
                    c.running = False             # safe to flip now — listener already exiting

                client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                client.settimeout(10)
                client.connect((self.host, self.command_port))
                self.logger.info(
                    f"[CONNECT] Device {device_id} connected to {self.host}:{self.command_port} "
                    f"({len(circuits)} channel(s))"
                )

                # Register every channel on this device over the SAME socket,
                # sequentially — one registration round-trip at a time, matching
                # ChannelManager's per-device read loop which handles registration
                # packet-by-packet on a shared connection.
                for circuit in circuits:
                    if not self.register_device(client, circuit):
                        self.logger.warning(
                            f"[DEVICE] {device_id}-{circuit.circuit_id} "
                            f"(board={circuit.secondary_board_number} channel={circuit.channel_number}) "
                            f"registration failed."
                        )

                if not any(c.is_registered for c in circuits):
                    client.close()
                    self.logger.warning(
                        f"[DEVICE] {device_id} - no channels registered, retrying in {retry_delay}s..."
                    )
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)
                    continue

                # At least one channel registered — share this socket across all
                # of the device's channel-slots THEN set running=True so the
                # listener thread sees a valid, fully-shared socket on its first
                # select() call.
                for circuit in circuits:
                    circuit.tcp_client = client
                    circuit.running = circuit.is_registered
                retry_delay = 2   # reset backoff after a successful registration round

                listener = threading.Thread(
                    target=self.command_listener,
                    args=(device_id, client, circuits_by_address),
                    daemon=True,
                    name=f"listener-device-{device_id}"
                )
                listener.start()

                for circuit in circuits:
                    if circuit.is_registered:
                        sender = threading.Thread(
                            target=self.data_sender,
                            args=(circuit,),
                            daemon=True,
                            name=f"sender-{device_id}-{circuit.circuit_id}"
                        )
                        sender.start()
                        senders.append(sender)

                # Wait for the listener to exit — this is our signal that the shared
                # connection dropped, taking every channel on it offline together.
                listener.join()
                # Give senders up to 2 s to notice running went False and exit cleanly.
                for sender in senders:
                    sender.join(timeout=2)

                if self.running:
                    self.logger.info(
                        f"[DEVICE] {device_id} connection lost, reconnecting in {retry_delay}s..."
                    )
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)

            except Exception as e:
                self.logger.error(f"[DEVICE] {device_id} error: {e}")
                if self.running:
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)
            finally:
                if not self.running:
                    for c in circuits:
                        c.running = False
                if client and all(client is not c.tcp_client for c in circuits):
                    try:
                        client.close()
                    except Exception:
                        pass

    def start_all_devices(self):
        """Start all simulated devices in parallel — one TCP connection per
        device, shared across all of that device's channels."""
        self.logger.info("=" * 60)
        self.logger.info("HARDWARE SIMULATOR STARTING")
        self.logger.info(f"  Devices: {self.device_count}")
        self.logger.info(f"  Channels per device: {self.circuit_count}")
        self.logger.info(f"  Total channels: {len(self.devices)}")
        self.logger.info(f"  Packet interval: {self.packet_interval_ms}ms")
        self.logger.info(f"  Server: {self.host}:{self.command_port}")
        self.logger.info("=" * 60)

        circuits_by_device: Dict[int, List[DeviceCircuit]] = defaultdict(list)
        for device in self.devices.values():
            circuits_by_device[device.device_id].append(device)

        threads = []
        for device_id, circuits in circuits_by_device.items():
            circuits.sort(key=lambda c: c.circuit_id)
            t = threading.Thread(
                target=self.start_device_connection,
                args=(device_id, circuits),
                daemon=True,
                name=f"device-{device_id}"
            )
            t.start()
            threads.append(t)
            time.sleep(0.05)

        self.logger.info(
            f"Started {len(threads)} device connection thread(s) for {len(self.devices)} channel(s)"
        )

        try:
            while self.running:
                time.sleep(30)
                self.print_stats()
        except KeyboardInterrupt:
            self.logger.info("Keyboard interrupt received")
        finally:
            self.stop()
            for t in threads:
                t.join(timeout=2)

    def print_stats(self):
        """Print simulator statistics."""
        self.logger.info(
            f"[STATS] Packets: {self.stats['packets_sent']} | "
            f"Commands: {self.stats['commands_received']} | "
            f"Registrations: {self.stats['registrations']} | "
            f"Errors: {self.stats['errors']}"
        )

    def stop(self):
        """Stop all devices and clean up threads."""
        self.logger.info("Stopping simulator...")
        self.running = False

        # Close each device's shared TCP connection once - channels on the same
        # device all point at the same socket object, so dedupe by identity to
        # unblock each per-device listener thread without redundant close() calls.
        closed = set()
        for device in self.devices.values():
            device.running = False
            sock = device.tcp_client
            if sock and id(sock) not in closed:
                closed.add(id(sock))
                try:
                    sock.close()
                except Exception:
                    pass

        # Give threads time to exit cleanly
        time.sleep(0.5)
        self.logger.info("Simulator stopped")

# =============================================================================
# MAIN ENTRY POINT
# =============================================================================

def main():
    import argparse

    parser = argparse.ArgumentParser(description="Battery Testing System Hardware Simulator")
    parser.add_argument(
        "-c", "--config",
        default="config.json",
        help="Path to config.json"
    )
    parser.add_argument(
        "-d", "--devices",
        type=int,
        help="Override device count"
    )
    parser.add_argument(
        "-n", "--channels",
        type=int,
        help="Override channels per device (1-64). Channels roll into the next "
             "secondary board every 8, so -n 16 gives boards X and X+1"
    )
    parser.add_argument(
        "-i", "--interval",
        type=int,
        help="Override packet interval (ms)"
    )
    parser.add_argument(
        "--board-base",
        type=int,
        choices=[0, 1],
        help="Lowest secondary-board number (default 1). Use 0 to reproduce the "
             "field topology that registers as 1-0-1..1-0-8"
    )
    parser.add_argument(
        "-H", "--host",
        help="Override server host"
    )

    args = parser.parse_args()

    sim = HardwareSimulator(args.config)

    if args.devices:
        sim.device_count = args.devices
    if args.channels:
        sim.circuit_count = args.channels
    if args.interval:
        sim.packet_interval_ms = args.interval
    if args.host:
        sim.host = args.host
    # `is not None`, not truthiness - `--board-base 0` is the whole point of the flag
    # and would be silently ignored by an `if args.board_base:` check.
    if args.board_base is not None:
        sim.board_base = args.board_base

    sim.create_devices()
    sim.running = True

    print("\n" + "=" * 60)
    print("  BATTERY TESTING SYSTEM - HARDWARE SIMULATOR")
    print("=" * 60)
    print(f"  Config: {args.config}")
    print(f"  Devices: {sim.device_count}")
    print(f"  Channels per device: {sim.circuit_count}")
    print(f"  Total channels: {len(sim.devices)}")
    _top_board = sim.board_base + ((sim.circuit_count - 1) // 8)
    print(f"  Boards per device: {sim.board_base}"
          f"{'' if _top_board == sim.board_base else f'-{_top_board}'}"
          f"  (e.g. 1-{sim.board_base}-1)")
    print(f"  Packet interval: {sim.packet_interval_ms}ms")
    print(f"  Random data: {sim.enable_random}")
    print(f"  Error simulation: {sim.enable_errors}")
    print(f"  Target server: {sim.host}:{sim.command_port}")
    print("=" * 60)
    print("Press Ctrl+C to stop\n")

    try:
        sim.start_all_devices()
    except KeyboardInterrupt:
        print("\nShutdown requested...")
        sim.stop()

if __name__ == "__main__":
    main()

