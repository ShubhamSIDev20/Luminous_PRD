import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from core_engine import CoreEngine, RUNNING, STOPPED, IDLE, LOGIC_GTE, CUTOFF_VOLTAGE
from program_decoder import (
    OP_CC_CHG, OP_CC_RECHG, OP_CV_CHG, OP_PAU, OP_STO, OP_TABLE,
    ACTION_STO, ACTION_GOTO,
)


def _cc_chg_step(step_id, current, cutoff_value, action="STO", target=None, cutoff=None, logic=None):
    action_op = ACTION_GOTO if action == "GOTO" else ACTION_STO
    return {
        "step_id": step_id, "operator": OP_CC_CHG, "nominal": [current],
        "table_rows": [], "goto_target": None, "reg_count": 0,
        "limits": [{"cutoff": cutoff or CUTOFF_VOLTAGE, "logic": logic or LOGIC_GTE,
                     "value": cutoff_value, "action": action_op, "action_target": target}],
        "parse_ok": True,
    }


def test_cc_chg_step_ends_on_voltage_limit():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=2.0, cutoff_value=4.2)])
    engine.start()
    assert engine.state == RUNNING
    sample = None
    for _ in range(100000):
        sample = engine.tick(100)
        if engine.state == STOPPED:
            break
    assert engine.state == STOPPED
    assert sample["voltage"] >= 4.2


def test_cc_rechg_is_simulated_identically_to_cc_chg():
    """CC_ReChg (0x15) has the same single-Current NominalConfig shape as CC_CHG and is routed
    through the same generic setpoint tick - it must behave identically here too: a charging
    step (accumulates into charge_capacity, not discharge_capacity) with a rising voltage ramp."""
    step = _cc_chg_step(1, current=2.0, cutoff_value=4.2)
    step["operator"] = OP_CC_RECHG

    engine = CoreEngine()
    engine.load_program([step])
    engine.start()
    sample = engine.tick(100)

    assert sample["operator"] == OP_CC_RECHG
    assert sample["charge_capacity"] > 0
    assert sample["discharge_capacity"] == 0


def test_pau_holds_and_advances_after_duration():
    steps = [
        {"step_id": 1, "operator": OP_PAU, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0,
         "limits": [{"cutoff": None, "logic": None, "value": 500, "action": 0, "action_target": None}],
         "parse_ok": True},
        {"step_id": 2, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    for _ in range(4):
        engine.tick(100)
    assert engine.current_step_index == 0  # 400ms elapsed, not yet 500ms
    engine.tick(100)  # 500ms - advances to STO step and stops
    assert engine.state != RUNNING


# ------------------------------------------------------------- apply_live_step_update (Q9)

def test_live_step_update_amends_nominal_and_limits_in_place():
    step = _cc_chg_step(1, current=2.0, cutoff_value=4.2)
    engine = CoreEngine()
    engine.load_program([step])
    engine.start()
    engine.tick(500)  # accrue some elapsed time / capacity before amending

    elapsed_before = engine.elapsed_in_step_ms
    capacity_before = engine.accumulated_capacity

    ok, reason = engine.apply_live_step_update(
        step_id=1, operator=OP_CC_CHG, nominal=[3.0],
        limits=[{"cutoff": CUTOFF_VOLTAGE, "logic": LOGIC_GTE, "value": 4.5,
                 "action": ACTION_STO, "action_target": None}],
    )

    assert ok is True
    assert reason == 0x00
    assert engine.steps[engine.current_step_index]["nominal"] == [3.0]
    assert engine.steps[engine.current_step_index]["limits"][0]["value"] == 4.5
    # Q9 amends in place - unlike a jump, elapsed time and accumulated capacity are preserved.
    assert engine.elapsed_in_step_ms == elapsed_before
    assert engine.accumulated_capacity == capacity_before


def test_live_step_update_never_mutates_the_caller_supplied_resident_program():
    """load_program must copy - an amendment must never leak into the caller's own list, which
    stands in for programDataBuffer/EEPROM (must stay untouched per bm_program_v3.2.md)."""
    resident_steps = [_cc_chg_step(1, current=2.0, cutoff_value=4.2)]
    engine = CoreEngine()
    engine.load_program(resident_steps)
    engine.start()

    engine.apply_live_step_update(step_id=1, operator=OP_CC_CHG, nominal=[9.9], limits=[])

    assert resident_steps[0]["nominal"] == [2.0]  # caller's own dict is untouched


def test_live_step_update_rejects_when_not_running():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=2.0, cutoff_value=4.2)])
    # never started - state is IDLE

    ok, reason = engine.apply_live_step_update(step_id=1, operator=OP_CC_CHG, nominal=[3.0], limits=[])

    assert ok is False
    assert reason == 0x01


def test_live_step_update_rejects_when_step_is_not_the_currently_executing_one():
    engine = CoreEngine()
    engine.load_program([
        _cc_chg_step(1, current=2.0, cutoff_value=4.2),
        _cc_chg_step(2, current=2.0, cutoff_value=4.2),
    ])
    engine.start()  # current step is 1, not 2

    ok, reason = engine.apply_live_step_update(step_id=2, operator=OP_CC_CHG, nominal=[3.0], limits=[])

    assert ok is False
    assert reason == 0x02


def test_live_step_update_rejects_operator_change():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=2.0, cutoff_value=4.2)])
    engine.start()

    ok, reason = engine.apply_live_step_update(step_id=1, operator=OP_CV_CHG, nominal=[3.0], limits=[])

    assert ok is False
    assert reason == 0x03


def test_live_step_update_rejects_operators_not_on_the_allow_list():
    # A TABLE step is never eligible, even with its own operator unchanged - matches
    # bm_program_v3.2.md's allow-list (TABLE/BEG/CYC/GOTO are structural, not regulating).
    step = {"step_id": 1, "operator": OP_TABLE, "nominal": [], "table_rows": [],
            "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True}
    engine = CoreEngine()
    engine.load_program([step])
    engine.start()

    ok, reason = engine.apply_live_step_update(step_id=1, operator=OP_TABLE, nominal=[], limits=[])

    assert ok is False
    assert reason == 0x04


def test_live_step_update_accepts_a_paused_channel():
    """Unlike jump_to_step, Q9 must accept a paused program - simulated via engine.state staying
    RUNNING while a device-level `paused` flag is set elsewhere (simulator.py), so this is really
    just confirming apply_live_step_update itself never adds a paused-rejecting check of its own."""
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=2.0, cutoff_value=4.2)])
    engine.start()
    assert engine.state == RUNNING  # pause is device-level, not engine-level - see simulator.py

    ok, reason = engine.apply_live_step_update(step_id=1, operator=OP_CC_CHG, nominal=[3.0], limits=[])

    assert ok is True
    assert reason == 0x00


def test_sto_step_stops_immediately():
    steps = [
        {"step_id": 1, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.tick(10)
    assert engine.state == STOPPED


def test_no_program_stays_idle():
    engine = CoreEngine()
    engine.load_program([])
    engine.start()
    assert engine.state == IDLE
    sample = engine.tick(100)
    assert sample["current"] == 0.0


def test_invalid_goto_target_degrades_to_stopped():
    steps = [
        {"step_id": 1, "operator": OP_CC_CHG, "nominal": [1.0], "table_rows": [],
         "goto_target": None, "reg_count": 0,
         "limits": [{"cutoff": 0x38, "logic": 0x53, "value": -999.0,
                      "action": ACTION_GOTO, "action_target": 9999}],  # unreachable target
         "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.tick(100)  # temperature always >= -999, so the GOTO always fires
    assert engine.state == STOPPED


def test_goto_step_jumps_to_target():
    from program_decoder import OP_GOTO
    steps = [
        {"step_id": 1, "operator": OP_GOTO, "nominal": [], "table_rows": [],
         "goto_target": 3, "reg_count": 0, "limits": [], "parse_ok": True},
        {"step_id": 2, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
        {"step_id": 3, "operator": OP_CC_CHG, "nominal": [1.5], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    sample = engine.tick(10)
    assert sample["step_number"] == 3  # jumped past step 2's STO entirely
    assert engine.state == RUNNING


def test_capacity_and_energy_accumulate_only_while_running():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=1.0, cutoff_value=999.0)])  # limit never trips
    engine.start()
    for _ in range(10):
        sample = engine.tick(1000)  # 1s ticks
    assert sample["accumulated_capacity"] > 0.0
    assert sample["accumulated_energy"] > 0.0
    engine.stop()
    idle_sample = engine.tick(1000)
    assert idle_sample["accumulated_capacity"] == 0.0  # idle sample reports zeroed, not accumulating


def test_jump_to_step_moves_to_target_and_resets_step_elapsed_time():
    steps = [
        _cc_chg_step(1, current=1.0, cutoff_value=999.0),  # limit never trips
        _cc_chg_step(2, current=2.0, cutoff_value=999.0),
        _cc_chg_step(3, current=3.0, cutoff_value=999.0),
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    for _ in range(5):
        engine.tick(100)  # 500ms elapsed into step 1

    ok, reason = engine.jump_to_step(3)

    assert ok is True
    assert reason == 0x00
    assert engine.current_step_index == 2  # step_id 3 is index 2
    assert engine.elapsed_in_step_ms == 0  # bm_program_v3.2: step elapsed time resets
    sample = engine.tick(10)
    assert sample["step_number"] == 3


def test_jump_to_nonexistent_step_is_rejected_and_does_not_move():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=1.0, cutoff_value=999.0)])
    engine.start()

    ok, reason = engine.jump_to_step(500)

    assert ok is False
    assert reason == 0x02  # step does not exist
    assert engine.current_step_index == 0  # unchanged


def test_jump_when_not_running_is_rejected():
    engine = CoreEngine()
    engine.load_program([_cc_chg_step(1, current=1.0, cutoff_value=999.0)])
    # never started - engine.state is IDLE

    ok, reason = engine.jump_to_step(1)

    assert ok is False
    assert reason == 0x01  # no program running


def test_jump_to_the_currently_running_step_is_an_accepted_no_op():
    steps = [_cc_chg_step(1, current=1.0, cutoff_value=999.0), _cc_chg_step(2, current=2.0, cutoff_value=999.0)]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.tick(300)

    ok, reason = engine.jump_to_step(1)

    assert ok is True
    assert reason == 0x00
    assert engine.elapsed_in_step_ms == 300  # untouched - jumping to yourself does nothing


def test_jump_onto_a_loop_beg_step_resets_its_iteration_counter():
    from program_decoder import OP_GOTO

    steps = [
        {"step_id": 1, "operator": OP_CC_CHG, "nominal": [1.0], "table_rows": [],  # BEG-equivalent
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
        {"step_id": 2, "operator": OP_GOTO, "nominal": [], "table_rows": [],
         "goto_target": 1, "reg_count": 0, "limits": [], "parse_ok": True},
        {"step_id": 3, "operator": OP_STO, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    engine.current_step_index = 1  # sitting on the GOTO step
    engine.tick(10)  # GOTO fires -> loop_counters[1] becomes 1, lands back on step 1
    assert engine.loop_counters[1] == 1

    # Move on from step 1 without going through the loop again (simulates time passing),
    # so the jump below is onto BEG from elsewhere - not the "already there" no-op case,
    # which the doc says does nothing at all, counter included.
    engine.current_step_index = 2
    ok, reason = engine.jump_to_step(1)

    assert ok is True
    assert engine.loop_counters[1] == 0  # bm_program_v3.2: landing on BEG restarts the loop


def test_set_step_configures_registrations_and_falls_through():
    from program_decoder import OP_SET
    steps = [
        {"step_id": 1, "operator": OP_SET, "nominal": [], "table_rows": [],
         "goto_target": None, "reg_count": 42, "limits": [], "parse_ok": True},
        {"step_id": 2, "operator": OP_CC_CHG, "nominal": [1.0], "table_rows": [],
         "goto_target": None, "reg_count": 0, "limits": [], "parse_ok": True},
    ]
    engine = CoreEngine()
    engine.load_program(steps)
    engine.start()
    sample = engine.tick(10)
    assert sample["step_number"] == 2  # SET was instantaneous, fell through to CC_CHG
    assert engine.active_registrations == {42}
