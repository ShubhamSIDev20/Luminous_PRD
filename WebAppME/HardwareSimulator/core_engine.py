"""Step-execution state machine. Drives dummy current/voltage/power from the REAL
uploaded program's setpoint operators and Limit-based cutoff conditions, replacing
simulator.py's random telemetry for circuits that have a loaded program.
"""
import random
from dataclasses import dataclass, field
from program_decoder import (
    OP_SET, OP_REG, OP_TABLE, OP_PAU, OP_GOTO, OP_STO, CHARGE_OPS, DISCHARGE_OPS,
    ACTION_GOTO, ACTION_STO, ACTION_INT, ACTION_ERR, ACTION_MSG,
    LIVE_STEP_UPDATE_ALLOWED_OPS,
)

# Models/Enums/ProgramEnums.cs - verified against source
CUTOFF_CURRENT, CUTOFF_VOLTAGE, CUTOFF_POWER = 0x31, 0x32, 0x33
CUTOFF_CHARGE_CAP, CUTOFF_DISCHARGE_CAP = 0x34, 0x35
CUTOFF_CHARGE_ENERGY, CUTOFF_DISCHARGE_ENERGY = 0x36, 0x37
CUTOFF_TEMPERATURE, CUTOFF_TIME = 0x38, 0x39
CUTOFF_ACC_CAP, CUTOFF_STEP_CAP = 0x3A, 0x3B
CUTOFF_ACC_ENERGY, CUTOFF_STEP_ENERGY = 0x3C, 0x3D

LOGIC_GT, LOGIC_LT, LOGIC_GTE, LOGIC_LTE, LOGIC_NEQ, LOGIC_EQ = 0x51, 0x52, 0x53, 0x54, 0x55, 0x56

MAX_STEP_DURATION_MS = 24 * 3600 * 1000  # guard for a step with no Limits at all

IDLE, RUNNING, STOPPED = "IDLE", "RUNNING", "STOPPED"

_FIELD_BY_CUTOFF = {
    CUTOFF_CURRENT: "current", CUTOFF_VOLTAGE: "voltage", CUTOFF_POWER: "power",
    CUTOFF_CHARGE_CAP: "charge_capacity", CUTOFF_DISCHARGE_CAP: "discharge_capacity",
    CUTOFF_CHARGE_ENERGY: "charge_energy", CUTOFF_DISCHARGE_ENERGY: "discharge_energy",
    CUTOFF_TEMPERATURE: "temperature",
    CUTOFF_ACC_CAP: "accumulated_capacity", CUTOFF_STEP_CAP: "step_capacity",
    CUTOFF_ACC_ENERGY: "accumulated_energy", CUTOFF_STEP_ENERGY: "step_energy",
}
_COMPARE = {
    LOGIC_GT: lambda a, b: a > b, LOGIC_LT: lambda a, b: a < b,
    LOGIC_GTE: lambda a, b: a >= b, LOGIC_LTE: lambda a, b: a <= b,
    LOGIC_NEQ: lambda a, b: a != b, LOGIC_EQ: lambda a, b: a == b,
}


@dataclass
class CoreEngine:
    steps: list = field(default_factory=list)
    current_step_index: int = 0
    elapsed_in_step_ms: int = 0
    loop_counters: dict = field(default_factory=dict)   # goto_target -> count so far
    active_registrations: set = field(default_factory=set)
    state: str = IDLE
    accumulated_capacity: float = 0.0
    accumulated_energy: float = 0.0
    temperature: float = 25.0
    _table_row_index: int = 0
    _table_row_elapsed_ms: int = 0
    _ramp_voltage: float = 3.0
    _last_sample: dict = field(default_factory=dict)

    def load_program(self, steps: list):
        # Independent shallow copy of each step dict - a Q9 live-step-update amendment (see
        # apply_live_step_update) replaces a dict's "nominal"/"limits" entries in place, and
        # without this copy that would also mutate the caller's device.program_steps (the
        # EEPROM-equivalent resident program), which the protocol explicitly says stays untouched.
        self.steps = [dict(s) for s in steps]
        if not self.steps:
            self.state = IDLE

    def start(self):
        if not self.steps:
            self.state = IDLE
            return
        self.current_step_index = 0
        self.elapsed_in_step_ms = 0
        self.loop_counters = {}
        self.accumulated_capacity = 0.0
        self.accumulated_energy = 0.0
        self._table_row_index = 0
        self._table_row_elapsed_ms = 0
        self._ramp_voltage = 3.0
        self.state = RUNNING

    def stop(self):
        self.state = STOPPED

    def apply_live_step_update(self, step_id: int, operator: int, nominal: list, limits: list) -> tuple:
        """Q9 "Live Step Update": amend the currently-executing step's PARAMETERS in place.

        Unlike jump_to_step, elapsed time and accumulated capacity/energy are deliberately left
        untouched - Q9 amends a step, it does not end/restart one. Returns (ok, reason) with the
        same REASON byte values as LiveStepUpdateReason in CircuitEnums.cs. Only 0x01/0x02/0x03/0x04
        are reachable here - 0x05 (Secondary NACK/link failure) and 0x07 (already in flight) have no
        simulated equivalent, same as jump_to_step's 0x04/0x05 - only exercised by the C#-side tests.
        """
        if self.state != RUNNING or not self.steps:
            return False, 0x01  # idle, or awaiting operator action - not modeled here

        current = self.steps[self.current_step_index]
        if current["step_id"] != step_id:
            return False, 0x02  # step number is not the currently executing step

        if operator != current["operator"]:
            return False, 0x03  # operator change not permitted - parameters only

        if operator not in LIVE_STEP_UPDATE_ALLOWED_OPS:
            return False, 0x04  # operator not eligible for live update

        current["nominal"] = nominal
        current["limits"] = limits
        return True, 0x00

    def tick(self, dt_ms: int) -> dict:
        if self.state != RUNNING or not self.steps:
            return self._idle_sample()

        self.temperature += random.uniform(-0.05, 0.05)  # slow random walk, no operator sets a temperature setpoint

        step = self.steps[self.current_step_index]
        op = step["operator"]

        if op == OP_SET or op == OP_REG:
            self.active_registrations = self._resolve_registrations(step)
            self._advance_step()
            return self.tick(dt_ms)  # SET/REG are instantaneous - fall through to next step same tick
        if op == OP_GOTO:
            self._do_goto(step["goto_target"])
            if self.state != RUNNING:
                return self._zeroed_sample(step)
            return self.tick(dt_ms)
        if op == OP_STO:
            self.state = STOPPED
            return self._zeroed_sample(step)

        self.elapsed_in_step_ms += dt_ms

        if op == OP_PAU:
            sample = self._hold_last_sample(step)
            duration_ms = int(step["limits"][0]["value"]) if step["limits"] else MAX_STEP_DURATION_MS
            if self.elapsed_in_step_ms >= duration_ms:
                self._advance_step()
                if self.state == RUNNING:
                    return self.tick(0)  # let the next step process immediately, same tick
            return sample
        if op == OP_TABLE:
            return self._tick_table(step, dt_ms)

        # Setpoint operators (CC_CHG/CV_CHG/CP_CHG/CCCV_CHG and _DCHG variants)
        sample = self._tick_setpoint(step, dt_ms)
        self._evaluate_limits(step, sample)
        return sample

    def _idle_sample(self) -> dict:
        return {"current": 0.0, "voltage": 0.0, "power": 0.0, "temperature": self.temperature,
                "step_number": 0, "operator": 0, "cycle_number": 0, "cycle_run_iteration": 0,
                "table_step_number": 0, "table_total_row_number": 0,
                "accumulated_capacity": 0.0, "charge_capacity": 0.0,
                "discharge_capacity": 0.0, "step_capacity": 0.0,
                "accumulated_energy": 0.0, "charge_energy": 0.0,
                "discharge_energy": 0.0, "step_energy": 0.0,
                "active_registrations": self.active_registrations}

    def _zeroed_sample(self, step: dict) -> dict:
        sample = self._idle_sample()
        sample["step_number"] = step["step_id"]
        sample["operator"] = step["operator"]
        return sample

    def _tick_setpoint(self, step: dict, dt_ms: int) -> dict:
        nominal = step["nominal"]
        current = nominal[0] if nominal else 0.0
        voltage = nominal[1] if len(nominal) > 1 else self._dummy_voltage_ramp(step, dt_ms)
        noisy_current = current * random.uniform(0.98, 1.02)
        power = noisy_current * voltage
        self.accumulated_capacity += noisy_current * (dt_ms / 3600000.0)
        self.accumulated_energy += power * (dt_ms / 3600000.0)
        is_charge = step["operator"] in CHARGE_OPS
        sample = {
            "current": noisy_current, "voltage": voltage, "power": power,
            "temperature": self.temperature,
            "step_number": step["step_id"], "operator": step["operator"],
            "cycle_number": 0, "cycle_run_iteration": 0,
            "table_step_number": 0, "table_total_row_number": 0,
            "accumulated_capacity": self.accumulated_capacity,
            "charge_capacity": self.accumulated_capacity if is_charge else 0.0,
            "discharge_capacity": self.accumulated_capacity if not is_charge else 0.0,
            "step_capacity": self.accumulated_capacity,
            "accumulated_energy": self.accumulated_energy,
            "charge_energy": self.accumulated_energy if is_charge else 0.0,
            "discharge_energy": self.accumulated_energy if not is_charge else 0.0,
            "step_energy": self.accumulated_energy,
            "active_registrations": self.active_registrations,
        }
        self._last_sample = sample
        return sample

    def _dummy_voltage_ramp(self, step: dict, dt_ms: int) -> float:
        """Only CC-type steps lack an explicit voltage setpoint. Dummy model: a
        monotonic ramp (rising for charge, falling for discharge) so Voltage-cutoff
        Limits can actually be satisfied - not a real battery response curve.
        """
        direction = 1 if step["operator"] in CHARGE_OPS else -1
        self._ramp_voltage += direction * 0.00002 * dt_ms
        return self._ramp_voltage

    def _hold_last_sample(self, step: dict) -> dict:
        sample = dict(self._last_sample) if self._last_sample else self._zeroed_sample(step)
        sample["step_number"] = step["step_id"]
        sample["operator"] = step["operator"]
        return sample

    def _tick_table(self, step: dict, dt_ms: int) -> dict:
        rows = step["table_rows"]
        if not rows:
            self._advance_step()
            return self.tick(dt_ms)
        row = rows[self._table_row_index]
        self._table_row_elapsed_ms += dt_ms
        current = row["current"] or 0.0
        voltage = row["voltage"] if row["voltage"] is not None else self._dummy_voltage_ramp(step, dt_ms)
        power = row["power"] if row["power"] is not None else current * voltage
        self.accumulated_capacity += current * (dt_ms / 3600000.0)
        self.accumulated_energy += power * (dt_ms / 3600000.0)
        if self._table_row_elapsed_ms >= row["time_ms"]:
            self._table_row_index += 1
            self._table_row_elapsed_ms = 0
            if self._table_row_index >= len(rows):
                self._table_row_index = 0
                self._advance_step()
        sample = {
            "current": current, "voltage": voltage, "power": power,
            "temperature": self.temperature,
            "step_number": step["step_id"], "operator": step["operator"],
            "cycle_number": 0, "cycle_run_iteration": 0,
            "table_step_number": self._table_row_index + 1,
            "table_total_row_number": len(rows),
            "accumulated_capacity": self.accumulated_capacity,
            "charge_capacity": self.accumulated_capacity,
            "discharge_capacity": 0.0, "step_capacity": self.accumulated_capacity,
            "accumulated_energy": self.accumulated_energy,
            "charge_energy": self.accumulated_energy,
            "discharge_energy": 0.0, "step_energy": self.accumulated_energy,
            "active_registrations": self.active_registrations,
        }
        self._last_sample = sample
        return sample

    def _evaluate_limits(self, step: dict, sample: dict):
        hit_any = False
        for limit in step["limits"]:
            if limit["cutoff"] == CUTOFF_TIME:
                actual = self.elapsed_in_step_ms
            else:
                fname = _FIELD_BY_CUTOFF.get(limit["cutoff"])
                if fname is None:
                    continue
                actual = sample[fname]
            if _COMPARE[limit["logic"]](actual, limit["value"]):
                hit_any = True
                self._fire_action(limit["action"], limit["action_target"])
                break
        if not hit_any and not step["limits"] and self.elapsed_in_step_ms >= MAX_STEP_DURATION_MS:
            self._advance_step()  # malformed-program safety net

    def _fire_action(self, action: int, target):
        if action == ACTION_STO:
            self.state = STOPPED
        elif action == ACTION_GOTO:
            self._do_goto(target)
        else:
            self._advance_step()  # INT/ERR/MSG: log-and-continue, not modeled further

    def _do_goto(self, target_step_id):
        target_index = next(
            (i for i, s in enumerate(self.steps) if s["step_id"] == target_step_id), None
        )
        if target_index is None:
            self.state = STOPPED  # unknown target - degrade to stop, don't crash
            return
        count = self.loop_counters.get(target_step_id, 0) + 1
        self.loop_counters[target_step_id] = count
        self.current_step_index = target_index
        self.elapsed_in_step_ms = 0

    def _advance_step(self):
        self.current_step_index += 1
        self.elapsed_in_step_ms = 0
        if self.current_step_index >= len(self.steps):
            self.state = STOPPED

    def jump_to_step(self, target_step_id: int) -> tuple:
        """bm_program_v3.2 Q10 - end the currently executing step and resume from
        target_step_id, without modifying the loaded program. Returns (success, reason_byte);
        reason_byte mirrors Models/Enums/CircuitEnums.cs::JumpToStepReason (0x00 success,
        0x02 step doesn't exist in the resident program). The "no program running/paused"
        rejection (0x01) is decided by the caller (simulator.py's DeviceCircuit.paused flag),
        since this class doesn't own that state.
        """
        if self.state != RUNNING or not self.steps:
            return False, 0x01

        target_index = next(
            (i for i, s in enumerate(self.steps) if s["step_id"] == target_step_id), None
        )
        if target_index is None:
            return False, 0x02

        if target_index == self.current_step_index:
            return True, 0x00  # already there - accepted no-op, not a "restart this step"

        # loop_counters is only ever keyed by a step id a GOTO has landed on (a loop's BEG
        # step) - resetting it here implements "jump onto BEG resets the loop's iteration
        # count to zero". Jumping within a loop or out of one leaves loop_counters alone,
        # matching the doc's other two cases (both no-ops on this dict already).
        if target_step_id in self.loop_counters:
            self.loop_counters[target_step_id] = 0

        self.current_step_index = target_index
        self.elapsed_in_step_ms = 0
        self._table_row_index = 0
        self._table_row_elapsed_ms = 0
        return True, 0x00

    def _resolve_registrations(self, step: dict) -> set:
        # REG without its own registrations inherits the nearest preceding SET's -
        # both already carry the same AddRegCount-derived reg_count from the decoder,
        # so tracking "last non-zero reg_count seen" is sufficient here.
        if step["reg_count"] > 0:
            return {step["reg_count"]}
        return self.active_registrations
