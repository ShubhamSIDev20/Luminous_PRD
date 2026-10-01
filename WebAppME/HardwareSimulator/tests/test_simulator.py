import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from simulator import HardwareSimulator, DeviceCircuit, CommandStatus
from core_engine import CoreEngine
from program_decoder import OP_CC_CHG


def _running_device() -> DeviceCircuit:
    device = DeviceCircuit(
        device_id=1, circuit_id=1, device_name="SIM_TEST", ip_address="127.0.0.1",
        mac_address="00:00:00:00:00:01", serial_number=1,
    )
    device.program_steps = [
        {"step_id": 1, "operator": OP_CC_CHG, "nominal": [1.0], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
        {"step_id": 2, "operator": OP_CC_CHG, "nominal": [2.0], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    device.core_engine = CoreEngine()
    device.core_engine.load_program(device.program_steps)
    device.core_engine.start()
    return device


def test_handle_jump_to_step_delegates_to_a_running_engine():
    """bm_program_v3.2 Q10, wired through simulator.py rather than testing core_engine
    directly - pins that HardwareSimulator._handle_jump_to_step actually calls the engine
    and returns wire-ready (STATUS, REASON) bytes, not just that the engine logic works."""
    sim = HardwareSimulator()
    device = _running_device()

    status, reason = sim._handle_jump_to_step(device, 2)

    assert status == CommandStatus.SUCCESS
    assert reason == 0x00
    assert device.core_engine.current_step_index == 1  # step_id 2 is index 1


def test_handle_jump_to_step_rejects_when_device_is_paused():
    """device.paused (set by the 0xEE PAUSE command) must reject the jump even though the
    CoreEngine's own state is still RUNNING - simulator.py, not core_engine.py, owns pause."""
    sim = HardwareSimulator()
    device = _running_device()
    device.paused = True

    status, reason = sim._handle_jump_to_step(device, 2)

    assert status == CommandStatus.FAILED
    assert reason == 0x01
    assert device.core_engine.current_step_index == 0  # unchanged


def test_handle_jump_to_step_rejects_a_nonexistent_step():
    sim = HardwareSimulator()
    device = _running_device()

    status, reason = sim._handle_jump_to_step(device, 999)

    assert status == CommandStatus.FAILED
    assert reason == 0x02
