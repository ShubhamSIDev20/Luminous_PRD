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
    IDLE = 0x00
    RUNNING = 0x01
    PAUSED = 0x02
    COMPLETED = 0x03

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
    SET = 0x01
    CC = 0x02
    CV = 0x03
    CP = 0x04
    CC_CV = 0x05
    PV = 0x06
    PC = 0x07
    PAU = 0x08
    STO = 0x09
    REG = 0x10
    TABLE = 0x11
    PRODUCER = 0x12
    REST = 0xFF

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

    tcp_client: Optional[socket.socket] = None
    send_thread: Optional[threading.Thread] = None
    running: bool = False
    paused: bool = False

    config: dict = field(default_factory=dict)

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
        self.circuit_count = self.sim_config.get("circuit_count_per_device", 1)
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
        """Create simulated device circuits."""
        self.logger.info(f"Creating {self.device_count} devices with {self.circuit_count} circuit(s) each...")

        device_defaults = self.config.get("device_defaults", {})
        name_prefix = device_defaults.get("device_name_prefix", "SIM_DEVICE_")
        base_ip = device_defaults.get("ip_address", "192.168.1.100")

        for d in range(1, self.device_count + 1):
            for c in range(1, self.circuit_count + 1):
                key = f"{d}-{c}"
                device = DeviceCircuit(
                    device_id=d,
                    circuit_id=c,
                    device_name=f"{name_prefix}{d:03d}_{c}",
                    ip_address=base_ip,
                    mac_address=uuid.uuid4().hex[:12].upper(),
                    serial_number=10000000 + d * 100 + c,
                    config=self.data_ranges
                )
                self.devices[key] = device
                self.logger.debug(f"Created device: {key} - {device.device_name}")

        self.logger.info(f"Created {len(self.devices)} device circuits")

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

        # device_id and circuit_id must come from the device — never hardcoded zeros.
        payload = struct.pack(">BBB", start_byte, query_id, 0x00)
        payload += struct.pack(">BB", device.device_id, device.circuit_id)
        payload += name_bytes
        payload += bytes(ip_parts)
        payload += mac_bytes
        payload += struct.pack(">BB", device.device_id, device.circuit_id)

        return payload

    def parse_registration_response(self, response: bytes) -> Optional[CommandStatus]:
        """Parse registration response, matching DecoderService.ParseRegistrationResponse."""
        if response[0] != 0xDD or response[1] != 0x01:
            return None
        return CommandStatus(response[4])

    def register_device(self, client: socket.socket, device: DeviceCircuit) -> bool:
        """Perform TCP registration with the server."""
        try:
            packet = self.build_registration_packet(device)
            self.logger.info(
                f"[REGISTER] Device {device.device_id}-{device.circuit_id} "
                f"sending: {packet.hex(' ')}"
            )

            client.sendall(packet)
            response = client.recv(1024)

            self.logger.info(
                f"[REGISTER] Device {device.device_id}-{device.circuit_id} "
                f"received response: {response.hex(' ')}"
            )

            status = self.parse_registration_response(response)
            if status and status in (CommandStatus.SUCCESS, CommandStatus.ALREADY_REGISTERED):
                device.is_registered = True
                self.stats["registrations"] += 1
                self.logger.info(
                    f"[REGISTER] Device {device.device_id}-{device.circuit_id} "
                    f"registered successfully (status={status.name})"
                )
                return True
            else:
                self.logger.warning(
                    f"[REGISTER] Device {device.device_id}-{device.circuit_id} "
                    f"registration failed: status={status}"
                )
                return False

        except Exception as e:
            self.logger.error(f"[REGISTER] Error: {e}")
            return False

    # -------------------------------------------------------------------------
    # REAL-TIME DATA PACKETS (0xCC - matching DecoderService.ParseRealTimeData)
    # -------------------------------------------------------------------------

    def build_realtime_packet(self, device: DeviceCircuit) -> bytes:
        """Build 0xCC real-time data packet matching DecoderService.ParseRealTimeData."""
        # Generate measurement data - use config ranges or sensible defaults
        ranges = device.config if device.config else {}
        if self.enable_random:
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

        # Ensure minimum values if not using random
        if not self.enable_random:
            if device.current < 0.1:
                device.current = 1.0 + (device.device_id * 0.5)
            if device.voltage < 10.0:
                device.voltage = 12.0 + (device.device_id * 0.1)

        device.power = device.current * device.voltage
        device.accumulated_capacity += device.current * (self.packet_interval_ms / 3600000.0)

        if device.program_status == ProgramRunningStatus.RUNNING and device.program_start_time > 0:
            device.step_running_time_ms = int((time.time() - device.step_start_time) * 1000)
            device.running_time_ms = int((time.time() - device.program_start_time) * 1000)
            device.charge_capacity = device.accumulated_capacity if device.operator in (0x02, 0x03, 0x05) else 0
            device.discharge_capacity = device.accumulated_capacity if device.operator == 0x07 else 0
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
            device.circuit_id,
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
        payload += struct.pack("B", device.operator)
        # Cycle Status
        payload += struct.pack("B", device.CycleStatus)
        # Cycle Number (2B)
        payload += struct.pack(">HH", device.cycle_number, device.table_step_number)
        # Cycle RUN Iteration (2B)
        payload += struct.pack(">HH", device.cycle_number, device.table_step_number)
        # Table Step Number (2B)
        payload += struct.pack(">HH", device.cycle_number, device.table_step_number)
        # Table Total Row Number (2B)
        payload += struct.pack(">HH", device.cycle_number, device.table_step_number)
        # skip 2 bytes
        payload += struct.pack(">HH", device.cycle_number, device.table_step_number)  
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
        payload += struct.pack(">BB", device.device_id, device.circuit_id)
        payload += struct.pack(">B", 0x01)
        payload += struct.pack(">i", device.session_id)
        payload += struct.pack(">h", device.step_number)
        payload += struct.pack("B", device.operator)
        payload += struct.pack("B", device.circuit_status)
        payload += struct.pack(">H", 0x1FFF)
        
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
            device.circuit_id,
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
        circuit_id = data[2]
        query_id = data[3]

        self.logger.info(
            f"[CMD-EE] Device {device_id}-{circuit_id} "
            f"query={query_id:02X} data={data[4:-2].hex(' ')}"
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

            self.logger.info(
                f"[START] Device {device_id}-{circuit_id} session={device.session_id}"
            )

        elif query_id == 0x02:  # STOP
            self.logger.info(f"[CMD] STOP - Device {device_id}-{circuit_id}")
            device.program_status = ProgramRunningStatus.IDLE
            device.circuit_status = CircuitStatus.Idle
            # Don't set device.running = False - keep registered for continued communication
            device.paused = False
            self.logger.info(
                f"[STOP] Device {device_id}-{circuit_id} "
                f"stored {device.accumulated_capacity:.2f} Ah"
            )

        elif query_id == 0x03:  # PAUSE
            self.logger.info(f"[CMD] PAUSE - Device {device_id}-{circuit_id}")
            device.paused = True
            device.program_status = ProgramRunningStatus.PAUSED

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

        response = struct.pack(">BBBBB", 0xEE, query_id, device_id, circuit_id, result)
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
        circuit_id = data[2]

        self.logger.info(
            f"[CMD-BB] Device {device_id}-{circuit_id} "
            f"query={query_id:02X} len={len(data)}"
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
                self.logger.info(f"[CMD] Will receive {count} program steps")

        elif query_id == 0x04:  # Program Data
            self.logger.info(f"[CMD] PROGRAM_DATA - Device {device_id}-{circuit_id}")
            self.logger.debug(f"[CMD] Program data: {data[4:].hex(' ')}")

        elif query_id == 0x07:  # DBC Steps Count
            self.logger.info(f"[CMD] DBC_STEPS_COUNT - Device {device_id}-{circuit_id}")
            if len(data) >= 7:
                count = read_int16_be(data, 4)
                self.logger.info(f"[CMD] Will receive {count} DBC file chunks")

        elif query_id == 0x08:  # DBC File Data
            self.logger.info(f"[CMD] DBC_FILE_DATA - Device {device_id}-{circuit_id}")
            self.is_dbc = True
            self.logger.debug(f"[CMD] DBC data: {data[4:60].hex(' ')}...")
            
        response = struct.pack(">BBBBB", 0xBB, device_id, circuit_id, query_id, result)
        response += b"\x00\x00"
        response = bind_crc16(response)
        client.sendall(response)
        self.logger.debug(f"[RESP-BB] Sent: {response.hex(' ')}")

    def handle_configuration_command(self, data: bytes, client: socket.socket, device: DeviceCircuit):
        """Handle 0xAA Configuration commands - matching CircuitCommandHandler methods."""
        if len(data) < 4:
            return

        query_id = data[3]
        device_id = data[1]
        circuit_id = data[2]

        self.logger.info(
            f"[CMD-AA] Device {device_id}-{circuit_id} "
            f"query={query_id:02X}"
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
        circuit_id = data[2]

        self.logger.info(
            f"[CMD-A0] Device {device_id}-{circuit_id} "
            f"[CMD-A0] Raw data hex: {' '.join(f'{b:02X}' for b in data)}"
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
        circuit_id = data[3]

        self.logger.info(
            f"[CMD-DD] Device {device_id}-{circuit_id} "
            f"query={query_id:02X}"
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
        payload = struct.pack(">BBBB", 0xAA, device.device_id, device.circuit_id, 0x02)

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
        payload = struct.pack(">BBBB", 0xAA, device.device_id, device.circuit_id, 0x03)

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
        payload = struct.pack(">BBBB", 0xA0, device.device_id, device.circuit_id, 0x14)

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

    def command_listener(self, device: DeviceCircuit):
        """Listen for TCP commands from server."""
        self.logger.info(
            f"[LISTENER] Device {device.device_id}-{device.circuit_id} "
            f"listening on TCP..."
        )

        while device.running and device.is_registered:
            try:
                if device.paused:
                    time.sleep(0.2)
                    continue

                sock = device.tcp_client
                if sock is None:
                    # Socket was torn down by start_device — exit cleanly.
                    break

                ready, _, _ = select.select([sock], [], [], 0.5)
                if not ready:
                    continue

                data = sock.recv(1024)
                if not data:
                    self.logger.info(
                        f"[DISCONNECT] Server closed connection for "
                        f"{device.device_id}-{device.circuit_id}"
                    )
                    break

                identity = data[0]

                if identity == 0xEE:
                    self.handle_control_command(data, sock, device)
                elif identity == 0xBB:
                    self.handle_program_command(data, sock, device)
                elif identity == 0xAA:
                    self.handle_configuration_command(data, sock, device)
                elif identity == 0xA0:
                    self.handle_calibration_command(data, sock, device)
                elif identity == 0xDD:
                    self.handle_registration_command(data, sock, device)
                else:
                    self.logger.warning(f"[UNKNOWN] Identity {identity:02X}: {data.hex(' ')}")

            except (ConnectionResetError, BrokenPipeError, OSError) as e:
                if device.running:
                    self.logger.info(f"[LISTENER] Connection closed: {e}")
                break
            except Exception as e:
                if device.running:
                    self.logger.error(f"[LISTENER] Error: {e}")
                break

        self.logger.info(
            f"[LISTENER] Exiting for {device.device_id}-{device.circuit_id}"
        )

    def data_sender(self, device: DeviceCircuit):
        """Send UDP data packets at configured interval."""
        self.logger.info(
            f"[SENDER] Device {device.device_id}-{device.circuit_id} "
            f"starting UDP sender (interval={self.packet_interval_ms}ms) "
            f"-> {self.host}:{self.data_view_port}"
        )

        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        last_sent_time = time.time()

        first_send = True
        while device.running and device.is_registered:
            try:
                if device.paused:
                    time.sleep(0.5)
                    continue

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


                if device.is_dbc and (time.time() - last_sent_time > 1):
                    self.logger.debug(
                        f"[SENDER] {device.device_id}-{device.circuit_id} "
                        f"DBC mode - sending DBC packet"
                    )
                    last_sent_time = time.time()

                if device.current_step_index % 100 == 0:
                    self.logger.debug(
                        f"[UDP] {device.device_id}-{device.circuit_id} "
                        f"V={device.voltage:.2f}V I={device.current:.2f}A "
                        f"T={device.temperature:.1f}Â°C"
                    )

                device.current_step_index += 1

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

    def start_device(self, device: DeviceCircuit):
        """Connect and register a device, then start listening and sending."""
        retry_delay = 2
        max_retry_delay = 30

        while self.running:
            listener = None
            sender = None
            client = None
            try:
                # Tear down any previous connection cleanly BEFORE creating a new socket.
                # CRITICAL: do NOT set device.running = False before closing the old socket.
                # The old listener thread is alive and using device.tcp_client via select().
                # Close the old socket first so the listener gets an error and exits, THEN
                # flip device.running = False so it stops looping.
                if device.tcp_client:
                    old_sock = device.tcp_client
                    device.tcp_client = None      # detach first so listener sees None
                    device.is_registered = False
                    try:
                        old_sock.shutdown(socket.SHUT_RDWR)
                    except Exception:
                        pass
                    try:
                        old_sock.close()
                    except Exception:
                        pass
                device.running = False            # safe to flip now — listener already exiting

                client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                client.settimeout(10)
                client.connect((self.host, self.command_port))
                self.logger.info(
                    f"[CONNECT] Device {device.device_id}-{device.circuit_id} "
                    f"connected to {self.host}:{self.command_port}"
                )

                if not self.register_device(client, device):
                    client.close()
                    # FIX 2b: Exponential backoff on registration failure
                    self.logger.warning(
                        f"[DEVICE] {device.device_id}-{device.circuit_id} "
                        f"registration failed, retrying in {retry_delay}s..."
                    )
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)
                    continue

                # Successful registration — assign socket THEN set running=True
                # so the listener thread sees a valid socket on its very first select() call.
                device.tcp_client = client
                device.running = True
                retry_delay = 2   # reset backoff after a successful registration

                listener = threading.Thread(
                    target=self.command_listener,
                    args=(device,),
                    daemon=True,
                    name=f"listener-{device.device_id}-{device.circuit_id}"
                )
                listener.start()

                sender = threading.Thread(
                    target=self.data_sender,
                    args=(device,),
                    daemon=True,
                    name=f"sender-{device.device_id}-{device.circuit_id}"
                )
                sender.start()

                # Wait for the listener to exit — this is our signal that the connection dropped.
                listener.join()
                # Give sender up to 2 s to notice device.running went False and exit cleanly.
                sender.join(timeout=2)

                if self.running:
                    self.logger.info(
                        f"[DEVICE] {device.device_id}-{device.circuit_id} "
                        f"connection lost, reconnecting in {retry_delay}s..."
                    )
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)

            except Exception as e:
                self.logger.error(
                    f"[DEVICE] {device.device_id}-{device.circuit_id} error: {e}"
                )
                if self.running:
                    time.sleep(retry_delay)
                    retry_delay = min(retry_delay * 2, max_retry_delay)
            finally:
                if not self.running:
                    device.running = False
                if client and client is not device.tcp_client:
                    try:
                        client.close()
                    except Exception:
                        pass

    def start_all_devices(self):
        """Start all simulated devices in parallel."""
        self.logger.info("=" * 60)
        self.logger.info("HARDWARE SIMULATOR STARTING")
        self.logger.info(f"  Devices: {self.device_count}")
        self.logger.info(f"  Circuits per device: {self.circuit_count}")
        self.logger.info(f"  Total circuits: {len(self.devices)}")
        self.logger.info(f"  Packet interval: {self.packet_interval_ms}ms")
        self.logger.info(f"  Server: {self.host}:{self.command_port}")
        self.logger.info("=" * 60)

        threads = []
        for key, device in self.devices.items():
            t = threading.Thread(target=self.start_device, args=(device,), daemon=True)
            t.start()
            threads.append(t)
            time.sleep(0.05)

        self.logger.info(f"Started {len(threads)} device threads")

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

        # Close all TCP client sockets to unblock listener threads
        for device in self.devices.values():
            device.running = False
            if device.tcp_client:
                try:
                    device.tcp_client.close()
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
        "-i", "--interval",
        type=int,
        help="Override packet interval (ms)"
    )
    parser.add_argument(
        "-H", "--host",
        help="Override server host"
    )

    args = parser.parse_args()

    sim = HardwareSimulator(args.config)

    if args.devices:
        sim.device_count = args.devices
    if args.interval:
        sim.packet_interval_ms = args.interval
    if args.host:
        sim.host = args.host

    sim.create_devices()
    sim.running = True

    print("\n" + "=" * 60)
    print("  BATTERY TESTING SYSTEM - HARDWARE SIMULATOR")
    print("=" * 60)
    print(f"  Config: {args.config}")
    print(f"  Devices: {sim.device_count}")
    print(f"  Circuits per device: {sim.circuit_count}")
    print(f"  Total circuits: {len(sim.devices)}")
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

