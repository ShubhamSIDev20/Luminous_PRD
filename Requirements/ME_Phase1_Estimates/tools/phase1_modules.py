# -*- coding: utf-8 -*-
"""Phase 1 modules: the grouping and the plain-English descriptions.

This file holds ONLY the two things a machine cannot derive:

    * which work packages belong to which module, and
    * how to describe that module to someone who has not read the SRS.

Everything numeric - requirement counts, effort days, priority, blockers,
how much is already built - is read live from
`../../Code_Tracebility/tools/trace_packages.py` and `trace_status.py`, so
this file can never disagree with the traceability record about a number.

A module is one section of `ME_Primary_SRS_V0.1`. The work packages listed
under it are the ones whose requirements sit in that section; the mapping
was derived from the requirement-to-package trace, not invented.

SCOPE: Primary Board only. The Battery Manager web application is planned
in `Code_Tracebility/ME_Work_Packages_and_Estimates.md` and is deliberately
not represented here.

MODULES entries are:  (id, name, srs_section, work_packages, what_it_covers)
"""

# ------------------------------------------------------ Primary Board ------
# Ordered the way the work should be done, not by SRS number: the modules
# that unblock other modules come first, the independent hardware and
# third-party interfaces come last.
PRIMARY_MODULES = [
    ("M01", "Secondary Board enrolment", "13.0",
     ["WP-P01"],
     "The Primary asks the internal CAN bus which Secondary Boards are "
     "present and how many channels each one has, instead of being told on "
     "the command line. This is the largest gap between the code as built "
     "and the SRS, and several other modules wait behind it."),

    ("M02", "Scale-out to 64 channels", "12.0",
     ["WP-P27"],
     "Run the real target size: 8 Secondary Boards x 8 channels. The "
     "addressing and the 64 storage slots are already written and tested on "
     "the PC. What has never run is more than one Secondary Board or more "
     "than 4 channels on real hardware."),

    ("M03", "Program operators", "10.0",
     ["WP-P02"],
     "The remaining test-program commands: Pause, Discharge, Interrupt, "
     "loops, jumps, register, error, message and table steps. Three of the "
     "thirteen are built, and this is the largest functional module in "
     "Phase 1."),

    ("M04", "Charge / discharge control modes", "10.0, 8.0",
     ["WP-P03"],
     "Constant-voltage mode, the constant-current to constant-voltage "
     "hand-over, non-time step limits, and the clamp that stops a program "
     "asking for more current than the channel is configured to allow. "
     "Today only plain constant-current charging with a time limit works."),

    ("M05", "Cut-off condition engine", "24.0",
     ["WP-P04"],
     "The rules that end a step: 13 condition types, 6 comparators, several "
     "conditions on one step, and debounce so that a single noisy sample "
     "cannot end a test. One condition type and two comparators are built "
     "today."),

    ("M06", "Test records", "16.0",
     ["WP-P05"],
     "Produce the stored test result and send it to the Battery Manager on "
     "UDP 10001. The delivery path is already built end to end and is "
     "carrying nothing; the data it needs is decoded today and then thrown "
     "away."),

    ("M07", "Live data completeness", "16.0, 15.0",
     ["WP-P06"],
     "Fill the fields in the live once-per-second data packet that are "
     "still sent as zero: temperature, power, the capacity and energy "
     "totals, and the cycle counters. The packet layout itself is finished "
     "and verified on hardware."),

    ("M08", "Host link hardening", "15.0",
     ["WP-P23"],
     "Make the replies to the Battery Manager honest: say 'accepted' where "
     "the board today says 'done', refuse a command it does not recognise "
     "instead of acknowledging it, cap the number of sessions, and hold "
     "data while the link is down. Most of this section already works and "
     "is hardware-verified."),

    ("M09", "Test control commands", "17.0",
     ["WP-P22"],
     "Finish Pause, Continue, Reset and the group (all-channel) operations, "
     "and check the preconditions before a Start. All six commands are "
     "accepted and acknowledged today, but only Start and Stop actually do "
     "anything."),

    ("M10", "Fault handling and error codes", "18.0, 21.0",
     ["WP-P10", "WP-P11", "WP-P31"],
     "One error catalogue - code, severity, subsystem, channel, time - and "
     "a fault model that knows whether a fault affects one channel, one "
     "board or the whole system, plus detection of a stalled control "
     "thread. Per-channel isolation already works; the classification "
     "around it does not exist."),

    ("M11", "Internal CAN bus robustness", "14.0",
     ["WP-P25"],
     "Recover from bus-off, notice stale readings, set message priorities, "
     "count errors and stay inside a bus-load budget. The transport itself "
     "works and is partly hardware-verified; this is what keeps it working "
     "on a bad day. It also needs a matching change on the M7 core, which "
     "owns the CAN controller."),

    ("M12", "Analog limits per channel", "8.0",
     ["WP-P19"],
     "Store and validate each channel's maximum battery voltage, its "
     "maximum charge and discharge currents and its sampling rates, then "
     "act on the six named exceptions. Nothing is stored today, which is "
     "why the current clamp in M04 has nothing to clamp against."),

    ("M13", "Performance and timing budgets", "19.0",
     ["WP-P26"],
     "Prove that the control loop holds its interval for 64 channels, that "
     "jitter stays inside its limit with headroom to spare, and that "
     "memory use is within budget. This is mostly measurement on real "
     "hardware, "
     "which is why board time exceeds build time. The control-loop rate is "
     "the single most important open number in the SRS."),

    ("M14", "Real Time Clock", "4.0",
     ["WP-P13"],
     "The I2C clock chip, time stamps in both required formats, time sync "
     "from the host, and pushing time out to the Secondary Boards. A time "
     "correction must not create a jump in a running test's elapsed time. "
     "Hardware-critical: needs the datasheet and a board."),

    ("M15", "Startup, shutdown and persistence", "20.0",
     ["WP-P09", "WP-P07", "WP-P30"],
     "Do not accept commands before the board is ready, bring every channel "
     "to a safe state on shutdown, keep configuration, calibration and "
     "programs across a restart, and report software versions. Everything "
     "is in RAM today, so a restart loses every program and every setting."),

    ("M16", "Power-fail detection and resume", "23.0",
     ["WP-P08"],
     "Detect the supply failing, save enough state to resume, and pick the "
     "test back up on restart. Waiting on a hardware number, not on "
     "software: nobody has yet stated how much warning the board's power "
     "circuit gives."),

    ("M17", "Program management", "11.0",
     ["WP-P21"],
     "Verify that a downloaded program is intact, refuse a download to a "
     "channel that is running, read a program back, and enforce the storage "
     "limit. The step-chain checks are already there; the whole-program "
     "checks are not."),

    ("M18", "Diagnostics and logging", "21.0",
     ["WP-P12"],
     "A log that survives a restart and can be retrieved and filtered by "
     "channel, board, severity and time. Today logging goes to the console "
     "and is gone when the process stops."),

    ("M19", "Calibration", "8.0, 9.0",
     ["WP-P20"],
     "Voltage, current and analog-output calibration per channel, stored "
     "and tied to the identity of the board it was measured on. There is "
     "nothing to carry forward from the old system and no written procedure "
     "yet. Wrong constants after a board swap produce readings that look "
     "completely believable."),

    ("M20", "Digital inputs", "1.0",
     ["WP-P14"],
     "The 4 Primary digital inputs: detect edges, time-stamp them, allow "
     "them to be enabled or disabled, and read the Secondary Boards' inputs "
     "through the Primary. No digital-input code exists at all. "
     "Hardware-critical (GPIO)."),

    ("M21", "Digital outputs", "2.0",
     ["WP-P15"],
     "The 4 Primary digital outputs: command a level, enable or disable an "
     "output, and drive it to a safe state if the link drops or a fault "
     "occurs. Hardware-critical (GPIO), and the safe-state half is "
     "safety-relevant."),

    ("M22", "Charge / discharge change-over", "3.0",
     ["WP-P28"],
     "Record the bank type of each channel and enforce the dead time when "
     "swinging between charge and discharge. It is small in code and "
     "safety-relevant, and its size depends entirely on whether the "
     "Primary or the Secondary sequences the change-over."),

    ("M23", "Network configuration", "6.0",
     ["WP-P24"],
     "Set a static IP or turn DHCP on and off from the Battery Manager, "
     "read the MAC address back on demand, and finish the remaining "
     "registration queries. The MAC is already read from the real interface "
     "and reported at registration."),

    ("M24", "Configurable CAN ports", "7.0",
     ["WP-P18"],
     "User-defined CAN ports towards third-party equipment: configure "
     "ports, messages and signals, subscribe, publish and map to output. "
     "That is 69 requirements in all, the largest single block in the SRS, "
     "and it cannot "
     "reuse the existing internal-bus CAN code."),

    ("M25", "RS-485 and Modbus", "5.0",
     ["WP-P16", "WP-P17"],
     "Port 1 answers a third-party HMI as a Modbus slave; port 2 reads "
     "third-party instruments as a Modbus master and binds what it reads to "
     "a channel so a program can use it. No RS-485 or Modbus code exists, "
     "and the register map for 64 channels has not been agreed."),

    ("M26", "Analog output as engineering values", "9.0",
     ["WP-P29"],
     "Command the analog output in volts and amps rather than raw converter "
     "codes. Already true for current; the voltage half is what is left, "
     "and it folds naturally into M04."),

    ("M27", "Known code items to close out", "-",
     ["WP-P32"],
     "The small defects and audits the team has already written down "
     "against the existing code: the frame-length table audit, a premature "
     "acknowledgement, payload-length guards. This is real work with no "
     "requirement of its own, listed here so that it is not forgotten."),
]

# The Battery Manager web application is NOT covered by this workbook
# (developer, 2026-09-01). Its work packages are owned by 'BM' in
# trace_packages.py and are out of scope by owner, which the build asserts
# explicitly - see OWNER in build_phase1_estimates.py. They remain planned
# and estimated in Code_Tracebility/ME_Work_Packages_and_Estimates.md.

# Primary Board work packages deliberately NOT counted in the totals.
#
# Empty, and that is the correct state today: the only Phase 2 package in
# the register is WP-B16, which is Battery Manager work and is therefore
# already out of scope by owner.
#
# The list is kept because the build treats an unclaimed Primary package as
# a failure. If a Primary package is ever deferred to Phase 2, adding it
# here is the deliberate act that removes its days from the totals - the
# alternative, deleting it from a module, would make the effort vanish
# silently.
#
# Entries are: (work package, title, owner, why it is excluded)
EXCLUDED = []
