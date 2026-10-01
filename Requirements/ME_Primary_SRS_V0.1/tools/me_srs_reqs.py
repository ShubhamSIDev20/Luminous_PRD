# -*- coding: utf-8 -*-
"""The ME Phase 1 requirement rows.  Imported by me_srs_data.py.

Numbering is sequential and stable.  Never renumber an existing ME_SW_REQ_n:
if a requirement dies, mark it withdrawn in Status and leave the number alone.
"""

REQUIREMENTS = []


def S(level, title):
    REQUIREMENTS.append(("S", level, title))


def R(rid, stmt, typ="Requirement", cat="Functional", app="Primary", phase="Phase 1",
      card="Per System", traces="", change="Carried Over", verif="Test", ref="",
      status="Draft", rem=""):
    REQUIREMENTS.append(("R", rid, stmt, typ, cat, app, phase, card, traces, change,
                         verif, ref, status, rem))


# ============================================================ 1  DIGITAL INPUTS
S(1, "1.0   Digital Inputs")
R("ME_SW_REQ_1",
  "The ME Primary Board provides 4 digital inputs. Each ME Secondary Board provides 4 "
  "digital inputs. With the maximum of 8 Secondary Boards the system therefore exposes "
  "4 Primary inputs and 32 Secondary inputs, 36 in total.",
  typ="Information", cat="Interface", app="Primary", card="Per System",
  traces="SW_REQ_1", change="Modified", verif="Inspection", ref="BTS V1.7 S1.0",
  rem="BTS had 4 + 4 = 8 inputs because there was exactly one Secondary. The count now "
      "scales with the number of enrolled Secondaries.")
R("ME_SW_REQ_2",
  "Primary digital inputs shall be referred to as PRI_INPUT_1 to PRI_INPUT_4. Secondary "
  "digital inputs shall be referred to as SEC<s>_INPUT_1 to SEC<s>_INPUT_4, where <s> is "
  "the Secondary ID 1 to 8.",
  typ="Information", cat="Interface", app="BM + Primary", card="Per System",
  traces="SW_REQ_2", change="Modified", verif="Inspection", ref="BTS V1.7 S1.0",
  rem="The flat INPUT_1..INPUT_8 naming of BTS cannot express 36 inputs and gives no way "
      "to say which board an input belongs to.")
R("ME_SW_REQ_3",
  "The Primary software shall detect a voltage level change from Low to High on each of "
  "its 4 digital inputs. Low level is 0 V, High level is 24 V.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_3",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S1.0",
  rem="BTS worded this as 'the BTS SW', covering both boards. Restated as a Primary-only "
      "obligation; edge detection on Secondary inputs is Secondary scope.")
R("ME_SW_REQ_4",
  "The Primary software shall detect a voltage level change from High to Low on each of "
  "its 4 digital inputs. Low level is 0 V, High level is 24 V.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_4",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S1.0",
  rem="See ME_SW_REQ_3.")
R("ME_SW_REQ_5",
  "The Primary software shall time-stamp every detected digital input transition from the "
  "system time reference, with a resolution of at least <TBD-01> ms.",
  cat="Functional", app="Primary", card="Per System", change="New", verif="Test",
  rem="New. With 64 concurrent tests an input event must be placeable in time relative "
      "to events on any channel, otherwise it cannot be correlated with a test record.")
R("ME_SW_REQ_6",
  "It shall be possible to enable or disable any digital input through the Battery "
  "Manager application.",
  cat="Configuration", app="BM", card="Per System", traces="SW_REQ_5",
  change="Carried Over", verif="Test", ref="BTS V1.7 S1.0", status="Pending")
R("ME_SW_REQ_7",
  "The Primary software shall honour the digital input enable/disable configuration and "
  "shall not act on, report or log transitions of a disabled digital input.",
  cat="Functional", app="Primary", card="Per System", traces="SW_REQ_5", change="New",
  verif="Test", ref="BTS V1.7 S1.0",
  rem="The BTS row stated only the BM side. The Primary-side obligation it implies was "
      "never written down.")
R("ME_SW_REQ_8",
  "It shall be possible to read the state of any digital input through the Ethernet port, "
  "if that port is enabled for communication with the host.",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_6",
  change="Carried Over", verif="Test", ref="BTS V1.7 S1.0", status="Pending")
R("ME_SW_REQ_9",
  "The Primary software shall make the state of the digital inputs of every enrolled "
  "Secondary Board readable by the Battery Manager, each addressed by its Secondary ID "
  "and input index.",
  cat="Interface", app="BM + Primary", card="Per Secondary", traces="SW_REQ_6",
  change="Modified", verif="Test", ref="BTS V1.7 S1.0",
  rem="In BTS the Secondary inputs were simply INPUT_5..8 on the one link. They now "
      "arrive from up to 8 boards and must be kept distinct.")
R("ME_SW_REQ_10",
  "It shall be possible to transmit the value read from any digital input on any CAN port "
  "as a 1-bit signal.",
  cat="Interface", app="BM + Primary", phase="Phase 2", card="Per CAN Port",
  traces="SW_REQ_7", change="Carried Over", verif="Test", ref="BTS V1.7 S1.0",
  status="Deferred", rem="Marked NA in BTS V1.7. Deferred to Phase 2 on the same basis.")

# =========================================================== 2  DIGITAL OUTPUTS
S(1, "2.0   Digital Outputs")
R("ME_SW_REQ_11",
  "The ME Primary Board provides 4 digital outputs. Each ME Secondary Board provides 8 "
  "digital outputs. With the maximum of 8 Secondary Boards the system therefore exposes "
  "4 Primary outputs and 64 Secondary outputs, 68 in total.",
  typ="Information", cat="Interface", app="Primary", card="Per System",
  traces="SW_REQ_8", change="Modified", verif="Inspection", ref="BTS V1.7 S2.0")
R("ME_SW_REQ_12",
  "Primary digital outputs shall be referred to as PRI_OUTPUT_1 to PRI_OUTPUT_4. "
  "Secondary digital outputs shall be referred to as SEC<s>_OUTPUT_1 to SEC<s>_OUTPUT_8, "
  "where <s> is the Secondary ID 1 to 8.",
  typ="Information", cat="Interface", app="BM + Primary", card="Per System",
  traces="SW_REQ_9", change="Modified", verif="Inspection", ref="BTS V1.7 S2.0")
R("ME_SW_REQ_13",
  "The Primary software shall set each of its digital outputs to 0 V or 24 V according to "
  "the logical command it receives.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_10",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S2.0",
  rem="Restated as Primary-only. Driving Secondary outputs is covered by ME_SW_REQ_19.")
R("ME_SW_REQ_14",
  "If a Primary digital output is given the logical command LOW, that output shall be 0 V.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_11",
  change="Carried Over", verif="HIL Test", ref="BTS V1.7 S2.0")
R("ME_SW_REQ_15",
  "If a Primary digital output is given the logical command HIGH, that output shall be "
  "24 V.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_12",
  change="Carried Over", verif="HIL Test", ref="BTS V1.7 S2.0")
R("ME_SW_REQ_16",
  "It shall be possible to enable or disable any digital output through the Battery "
  "Manager application.",
  cat="Configuration", app="BM", card="Per System", traces="SW_REQ_13",
  change="Carried Over", verif="Test", ref="BTS V1.7 S2.0", status="Pending")
R("ME_SW_REQ_17",
  "The Primary software shall reject any command addressed to a disabled digital output, "
  "shall leave that output unchanged, and shall report the rejection to the requester.",
  cat="Functional", app="Primary", card="Per System", traces="SW_REQ_13", change="New",
  verif="Test", ref="BTS V1.7 S2.0",
  rem="BTS stated only that disabling was possible, never what happens when a disabled "
      "output is commanded. Silent acceptance would be dangerous.")
R("ME_SW_REQ_18",
  "It shall be possible to control any digital output through the Ethernet port, if that "
  "port is enabled for communication with the host.",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_14",
  change="Carried Over", verif="Test", ref="BTS V1.7 S2.0", status="Pending")
R("ME_SW_REQ_19",
  "The Primary software shall route a digital output command addressed to a Secondary "
  "output to the Secondary Board identified in the command, and shall report to the "
  "requester whether the addressed Secondary accepted it.",
  cat="Interface", app="BM + Primary", card="Per Secondary", traces="SW_REQ_14",
  change="Modified", verif="Test", ref="BTS V1.7 S2.0",
  rem="New routing responsibility. With one Secondary on a point-to-point link there was "
      "nothing to address.")
R("ME_SW_REQ_20",
  "It shall be possible to control any digital output from any 1-bit signal received in "
  "any subscribed message on any CAN port.",
  cat="Interface", app="BM + Primary", phase="Phase 2", card="Per CAN Port",
  traces="SW_REQ_15", change="Carried Over", verif="Test", ref="BTS V1.7 S2.0",
  status="Deferred", rem="Marked NA in BTS V1.7.")
R("ME_SW_REQ_21",
  "On loss of the Battery Manager link, or on detection of a Primary-level fault, the "
  "Primary software shall drive each of its digital outputs to a configured safe state "
  "within <TBD-02> ms.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="HIL Test",
  rem="New. BTS never defined a safe state for the outputs. With up to 64 batteries "
      "connected, leaving outputs in an arbitrary state on fault is not acceptable. "
      "See Open Issue OI-05.")

# ==================================================================== 3  RELAYS
S(1, "3.0   Relays   (context only - relay control is Secondary Board scope)")
R("ME_SW_REQ_22",
  "Each ME Secondary Board carries three relays, referred to as RELAY_1 to RELAY_3. "
  "RELAY_1 is used for change-over between charging and discharging mode on "
  "dual-transistor-bank units. RELAY_2 and RELAY_3 are spare. The driving of these relays "
  "is Secondary Board scope and is not specified in this document.",
  typ="Information", cat="Interface", app="Primary", card="Per Secondary",
  traces="SW_REQ_16, SW_REQ_17, SW_REQ_18, SW_REQ_19", change="Modified",
  verif="Inspection", ref="BTS V1.7 S3.0",
  rem="Retained as context only, because the Primary must know that a change-over takes "
      "physical time. See ME_SW_REQ_23.")
R("ME_SW_REQ_23",
  "The Primary software shall hold, for each channel, the transistor bank type (Single or "
  "Dual) of the unit driving that channel, and shall not command a transition between "
  "charging and discharging on that channel without first commanding the channel setpoint "
  "to zero and then observing the switch-over dead time of <TBD-03> ms.",
  cat="Safety", app="BM + Primary", card="Per Channel",
  traces="SW_REQ_168, SW_REQ_176, SW_REQ_177, SW_REQ_178, SW_REQ_179, SW_REQ_183, "
         "SW_REQ_184, SW_REQ_185",
  change="New", verif="HIL Test", ref="BTS V1.7 S9.2-9.5",
  rem="In BTS the safe switch-over sequence lived entirely on the Secondary, because the "
      "Secondary decided the steps. In ME the Primary decides, so the Primary must "
      "either sequence the change-over itself or guarantee the Secondary does. Which of "
      "the two is Open Issue OI-06; this requirement holds either way.")

# ======================================================================= 4  RTC
S(1, "4.0   Real Time Clock")
R("ME_SW_REQ_24",
  "The RTC module maintains system time, date and time of day, in 24-hour format. It is "
  "used to time-stamp log entries and to provide time-based functionality.",
  typ="Information", cat="Functional", app="Primary", card="Per System",
  traces="SW_REQ_19 (RTC)", change="Carried Over", verif="Inspection",
  ref="BTS V1.7 S4.0")
R("ME_SW_REQ_25",
  "The Primary software shall provide a time-stamp for all system logs, containing both "
  "Epoch time and calendar time in the form YYYY-MM-DD HH:MM:SS.",
  cat="Data", app="Primary", card="Per System", traces="SW_REQ_20",
  change="Carried Over", verif="Test", ref="BTS V1.7 S4.0")
R("ME_SW_REQ_26",
  "The Primary software shall synchronise its RTC with the Battery Manager application "
  "for time correction on request.",
  cat="Functional", app="BM + Primary", card="Per System", traces="SW_REQ_21",
  change="Modified", verif="Test", ref="BTS V1.7 S4.0",
  rem="BTS said 'a BTS web application'. The ME host is a PC application.")
R("ME_SW_REQ_27",
  "The Primary software shall communicate with the RTC device over I2C to set and "
  "retrieve time data.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_22",
  change="Carried Over", verif="Test", ref="BTS V1.7 S4.0")
R("ME_SW_REQ_28",
  "If the RTC fails to provide valid time data during start-up, the Primary software "
  "shall set the time to a default value, shall log an error, and shall refuse to start "
  "any new test until valid time is available.",
  cat="Safety", app="Primary", card="Per System", traces="SW_REQ_23", change="Modified",
  verif="Test", ref="BTS V1.7 S4.0",
  rem="The refusal to start a test is added. A test record with a fabricated timestamp is "
      "worse than no test, because Ah and Wh integration depends on elapsed time.")
R("ME_SW_REQ_29",
  "The Primary shall be the single time reference for the whole ME system. All test "
  "records, live data samples and log entries from all channels shall be expressed on "
  "that one timebase.",
  cat="Data", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="New. With one channel there was nothing to correlate. With 64 channels across 8 "
      "boards, records that cannot be placed on a common timebase cannot be compared.")
R("ME_SW_REQ_30",
  "The Primary software shall distribute system time to every enrolled Secondary Board, "
  "and shall keep the time of every Secondary within <TBD-04> ms of its own.",
  cat="Functional", app="Primary", card="Per Secondary", change="New", verif="Test",
  rem="New. Needed to satisfy ME_SW_REQ_29 wherever a Secondary time-stamps anything "
      "locally.")
R("ME_SW_REQ_31",
  "An RTC correction shall not introduce a discontinuity in the elapsed-time or "
  "integrated-quantity calculation of any test that is running. Every correction shall be "
  "logged with its magnitude, and any test running at the time shall be flagged in its "
  "test record.",
  cat="Data", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. A test may run for days. A wall-clock step of even a few seconds applied "
      "naively to an Ah integrator silently corrupts the result, and the operator has no "
      "way to know it happened.")

# ============================================================== 5  RS-485 PORTS
S(1, "5.0   RS-485 Ports")
S(2, "5.1   RS485_1  -  Modbus RTU slave, towards a third party HMI")
R("ME_SW_REQ_32",
  "The RS485_1 port shall support the MODBUS RTU protocol for interfacing with a third "
  "party HMI.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_24",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.1")
R("ME_SW_REQ_33",
  "The RS485_1 port shall operate as a MODBUS slave node.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_25",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.1")
R("ME_SW_REQ_34",
  "It shall be possible to configure the following for RS485_1:\n"
  "1. ME Device ID\n2. HMI Device ID\n3. Baud Rate\n4. Parity\n5. Stop Bits",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_26",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.1", status="Pending",
  rem="'BTS Device ID' renamed to 'ME Device ID'.")
R("ME_SW_REQ_35",
  "It shall be possible to send the following parameters to the HMI over RS485_1 using "
  "MODBUS, each qualified by the channel it belongs to:\n"
  "1. Battery Voltage\n2. Current\n3. Test Status\n4. Current Operation (Charging / "
  "Discharging)\n5. Any signal received on any CAN port\n6. Any parameter received on "
  "RS485_2\n7. Errors",
  cat="Interface", app="BM + Primary", card="Per Channel", traces="SW_REQ_27",
  change="Modified", verif="Test", ref="BTS V1.7 S5.1",
  rem="In BTS there was one battery, so 'Battery Voltage' was unambiguous. Every "
      "measured parameter now needs a channel address attached to it.")
R("ME_SW_REQ_36",
  "The MODBUS register map of RS485_1 shall be capable of addressing all channels of all "
  "enrolled Secondary Boards. The register layout shall be defined as <TBD-05>.",
  cat="Capacity", app="BM + Primary", card="Per System", traces="SW_REQ_27",
  change="New", verif="Analysis", ref="BTS V1.7 S5.1",
  rem="New, and it needs a decision. 64 channels multiplied by the parameter list of "
      "ME_SW_REQ_35 does not fit a naive flat register map, so either the map is paged, "
      "or the operator selects a channel, or only a subset is exposed. See Open Issue "
      "OI-07.")
R("ME_SW_REQ_37",
  "It shall be possible, through the Battery Manager, to select which channels and which "
  "parameters are published on RS485_1.",
  cat="Configuration", app="BM", card="Per System", change="New", verif="Test",
  rem="New. Follows from ME_SW_REQ_36 - if the full set does not fit, the operator must "
      "be able to choose.")

S(2, "5.2   RS485_2  -  reading from third party devices")
R("ME_SW_REQ_38",
  "The RS485_2 port shall be able to read data from external third party devices.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_28",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2")
R("ME_SW_REQ_39",
  "The RS485_2 port shall be configurable to be used with the MODBUS protocol.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_29",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2")
R("ME_SW_REQ_40",
  "It shall be possible to set the baud rate of the RS485_2 port up to 115200 bps.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_30",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2")
R("ME_SW_REQ_41",
  "The MODBUS protocol on RS485_2 shall use 8 data bits, 1 stop bit, and even, odd or no "
  "parity as configured. The permitted combinations are <TBD-06>.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_31",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2",
  rem="BTS V1.7 wrote 'as per configuration XXX'. The XXX was never filled in.")
R("ME_SW_REQ_42",
  "The MODBUS frame on RS485_2 shall consist of Address Field, Function Code, Data Field "
  "and CRC.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_32",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S5.2")
R("ME_SW_REQ_43",
  "The MODBUS implementation on RS485_2 shall support function codes <TBD-07>.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_33",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2",
  rem="BTS V1.7 wrote 'function codes XXX'. Never filled in.")
R("ME_SW_REQ_44",
  "The MODBUS implementation on RS485_2 shall support RTU mode with a response timeout of "
  "<TBD-08> ms.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_34",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2",
  rem="BTS V1.7 wrote 'a fixed timeout of XXX ms'. Never filled in.")
R("ME_SW_REQ_45",
  "The RS485_2 network shall support up to <TBD-09> devices on the same bus.",
  cat="Capacity", app="Primary", card="Per System", traces="SW_REQ_35",
  change="Carried Over", verif="Analysis", ref="BTS V1.7 S5.2",
  rem="BTS V1.7 wrote 'up to XXX devices'. Never filled in.")
R("ME_SW_REQ_46",
  "The RS485_2 network shall allow configuration of the slave address in the range "
  "<TBD-10> to <TBD-11>.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_36",
  change="Carried Over", verif="Test", ref="BTS V1.7 S5.2", status="Pending",
  rem="BTS V1.7 wrote 'XXX to XXX'. Never filled in.")
R("ME_SW_REQ_47",
  "It shall be possible to bind a value read on RS485_2 to a named quantity of a "
  "specified channel, so that the bound value can be used as a Nominal Value source or a "
  "Limit source by that channel's program.",
  cat="Functional", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and necessary. The CHA and DCH operators accept Temperature as a Limit "
      "(SW_REQ_211, SW_REQ_215). In BTS the one external temperature sensor obviously "
      "belonged to the one battery. With 64 batteries, a temperature reading is useless "
      "unless the system knows whose it is.")

# ============================================================= 6  ETHERNET PORT
S(1, "6.0   Ethernet Port")
R("ME_SW_REQ_48",
  "The ME Primary Board has one Ethernet interface, used to communicate with the host "
  "computer on which the Battery Manager application runs.",
  typ="Information", cat="Interface", app="Primary", card="Per System",
  traces="SW_REQ_37", change="Carried Over", verif="Inspection", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_49",
  "The Ethernet port shall support the TCP/IP protocol.",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_38",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_50",
  "The Primary Ethernet port shall support IPv4.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_39",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_51",
  "The Primary Ethernet port shall support IPv6.",
  cat="Interface", app="Primary", phase="Phase 2", card="Per System",
  traces="SW_REQ_40", change="Carried Over", verif="Test", ref="BTS V1.7 S6.0",
  status="Deferred",
  rem="BTS wrote this as 'if possible' and marked it NA. A requirement cannot say 'if "
      "possible'; it is therefore moved to Phase 2 where it can be committed to or "
      "dropped honestly.")
R("ME_SW_REQ_52",
  "The Primary Ethernet port shall support the DHCP protocol.",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_41",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0", status="Pending")
R("ME_SW_REQ_53",
  "The Primary Ethernet port shall have a unique MAC address.",
  cat="Interface", app="Primary", card="Per System", traces="SW_REQ_42",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_54",
  "It shall be possible to read the MAC address of the Primary Ethernet port through the "
  "Battery Manager application.",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_43",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_55",
  "It shall be possible to set the IP address of the Primary Ethernet port manually "
  "through the Battery Manager application.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_44",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_56",
  "It shall be possible to enable automatic IP address assignment using DHCP.",
  cat="Configuration", app="BM + Primary", card="Per System", traces="SW_REQ_45",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_57",
  "It shall be possible to perform the following over the Primary Ethernet port using "
  "TCP/IP:\n1. Configuration\n2. Calibration\n3. Programming\n4. Reading live parameters",
  cat="Interface", app="BM + Primary", card="Per System", traces="SW_REQ_46",
  change="Carried Over", verif="Test", ref="BTS V1.7 S6.0")
R("ME_SW_REQ_58",
  "The single Ethernet link shall carry the configuration, programming, calibration, live "
  "data and test record traffic of all enrolled Secondary Boards and all their channels "
  "concurrently, without any channel being starved of service.",
  cat="Performance", app="BM + Primary", card="Per System", traces="SW_REQ_46",
  change="New", verif="Test", ref="BTS V1.7 S6.0",
  rem="New. In BTS this link served one channel. It must now serve up to 64. See "
      "section 16 for the numeric budget.")
R("ME_SW_REQ_59",
  "The Primary software shall accept at most <TBD-12> concurrent Battery Manager "
  "sessions, and shall reject further connection attempts with a defined reason code.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="New. Never stated in BTS. With 64 channels and possibly several operators, "
      "whether two people may connect at once is a real question with safety "
      "consequences. See Open Issue OI-08.")
R("ME_SW_REQ_60",
  "On loss of the Battery Manager link, the Primary software shall continue to execute "
  "every running program on every channel without interruption.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and important. A test may run for days; the operator PC will be rebooted, "
      "patched or disconnected during that time. BTS never stated this, so it was left "
      "to the implementer to decide.")
R("ME_SW_REQ_61",
  "While the Battery Manager link is down, the Primary software shall buffer all test "
  "record data it would have sent, for at least <TBD-13> minutes of operation at the "
  "maximum configured registration rate on all channels.",
  cat="Data", app="Primary", card="Per System", change="New", verif="Test",
  rem="New. Follows from ME_SW_REQ_60 - continuing to run is only useful if the results "
      "survive.")
R("ME_SW_REQ_62",
  "On re-establishment of the Battery Manager link, the Primary software shall deliver "
  "the buffered test record data in order, with no gaps and no duplicated records, and "
  "shall inform the Battery Manager if any data was lost because the buffer overflowed.",
  cat="Data", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="New. The known defect in the existing design is that recorded data can be lost "
      "without anyone knowing. An explicit overflow report is what makes the loss "
      "visible rather than silent.")

# ============================================================= 7  CAN INTERFACE
S(1, "7.0   CAN Interface  -  configurable ports towards external devices")
R("ME_SW_REQ_63",
  "The ME Primary Board provides 3 CAN ports for communication with external devices and "
  "with the Secondary Boards.",
  typ="Information", cat="Interface", app="Primary", card="Per System",
  traces="SW_REQ_47", change="Modified", verif="Inspection", ref="BTS V1.7 S7.0",
  rem="In BTS all three ports were available for external devices, because the Secondary "
      "was on a separate serial link. In ME one port carries the Secondary bus.")
R("ME_SW_REQ_64",
  "These ports shall be referred to as CAN_1, CAN_2 and CAN_3.",
  typ="Information", cat="Interface", app="BM + Primary", card="Per System",
  traces="SW_REQ_48", change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_65",
  "One CAN port, <TBD-14>, shall be reserved exclusively for the ME internal bus that "
  "connects the Primary to the Secondary Boards. That port shall not be available for "
  "user configuration, and the requirements of section 12 shall apply to it instead of "
  "the requirements of this section.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", change="New",
  verif="Inspection",
  rem="New, and it is the structural change in this section. Allowing an operator to "
      "reconfigure the baud rate of the bus that carries the control loop for 64 "
      "batteries would be unsafe. See Open Issue OI-09.")
R("ME_SW_REQ_66",
  "All user-configurable CAN ports shall support the following baud rates: 125 kbps, "
  "250 kbps, 500 kbps and 1 Mbps.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_49, SW_REQ_60",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0, S7.1.1.1",
  rem="BTS V1.7 listed 125 kbps in SW_REQ_49 but its baud-rate sub-section omitted it, "
      "and SW_REQ_60 was an unresolved note saying 'Add 125Kbps as well'. Merged here.")
R("ME_SW_REQ_67",
  "All user-configurable CAN ports shall support both standard (11-bit) and extended "
  "(29-bit) CAN identifiers.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_50",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0",
  rem="Note the conflict with ME_SW_REQ_91, which carries BTS SW_REQ_74 requiring an "
      "11-bit identifier. See Open Issue OI-10.")
R("ME_SW_REQ_68",
  "It shall be possible to receive CAN messages on each user-configurable CAN port as per "
  "its configuration.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_51",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_69",
  "It shall be possible to extract the configured signals from messages received on any "
  "user-configurable CAN port.",
  cat="Functional", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_52",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_70",
  "It shall be possible to send received CAN message data to the Battery Manager "
  "application.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_53",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_71",
  "All user-configurable CAN ports shall be able to transmit messages periodically as per "
  "their configuration.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_54",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_72",
  "It shall be possible to send any data held by the Primary on any user-configurable CAN "
  "port.",
  cat="Interface", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_55",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_73",
  "It shall be possible to map a signal received on any user-configurable CAN port onto a "
  "named Primary data item.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_56",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")
R("ME_SW_REQ_74",
  "It shall be possible to map a named Primary data item onto a signal of a message "
  "configured for transmission on any user-configurable CAN port.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_57",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.0")

S(2, "7.1   Configuration of CAN frames")
R("ME_SW_REQ_75",
  "For each user-configurable CAN port the Primary shall receive the following groups of "
  "configuration:\n1. CAN Port Configuration\n2. Message Configuration\n"
  "3. Signal Configuration",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_58",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1")

S(3, "7.1.1   CAN Port Configuration")
R("ME_SW_REQ_76",
  "For each user-configurable CAN port the following port configuration parameters shall "
  "exist:\n1. Baud Rate\n2. Subscribed CAN Messages\n3. Published CAN Messages",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_59",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.1")
R("ME_SW_REQ_77",
  "It shall be possible to set a CAN baud rate of 125 kbps through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_60",
  change="Modified", verif="Test", ref="BTS V1.7 S7.1.1.1",
  rem="BTS SW_REQ_60 was not a requirement but a note to the author reading 'Add 125Kbps "
      "as well'. Written out properly here.")
R("ME_SW_REQ_78",
  "It shall be possible to set a CAN baud rate of 250 kbps through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_61",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.1")
R("ME_SW_REQ_79",
  "It shall be possible to set a CAN baud rate of 500 kbps through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_62",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.1")
R("ME_SW_REQ_80",
  "It shall be possible to set a CAN baud rate of 1 Mbps through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_63",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.1")
R("ME_SW_REQ_81",
  "If the configured baud rate is not one of the supported values, the Primary shall "
  "raise an error notification and shall leave that CAN port disabled.",
  cat="Diagnostic", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_64",
  change="Modified", verif="Test", ref="BTS V1.7 S7.1.1.1",
  rem="'shall leave that CAN port disabled' is added. BTS said only that an error is "
      "raised, leaving it undefined whether the port then runs at some default rate.")
R("ME_SW_REQ_82",
  "Every error notification raised by the Primary shall be reported to the Battery "
  "Manager as a structured error record containing at least: error code, severity, "
  "originating subsystem, affected channel or port, and time of detection.",
  cat="Diagnostic", app="BM + Primary", card="Per System", traces="SW_REQ_65, SW_REQ_96, "
  "SW_REQ_122, SW_REQ_127, SW_REQ_132, SW_REQ_135, SW_REQ_146, SW_REQ_151, SW_REQ_156",
  change="New", verif="Test", ref="BTS V1.7 (nine open questions)",
  rem="New, and it closes nine separate places where BTS V1.7 asked 'How do we indicate "
      "this error?' and never answered. One mechanism answers all nine. The error code "
      "list itself is Open Issue OI-11.")

S(3, "7.1.1.2   Subscribed CAN Messages")
R("ME_SW_REQ_83",
  "A message that a CAN port is expected to read and consume shall be configured as a "
  "Subscribed CAN Message by the configuration tool.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_66",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.1.2",
  rem="Applicability was blank in BTS V1.7; inferred as BM + Primary from context.")
R("ME_SW_REQ_84",
  "The configuration shall state the total number of messages subscribed by each CAN port.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_67",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.1.2",
  rem="Applicability was blank in BTS V1.7; inferred from context.")
R("ME_SW_REQ_85",
  "The Primary shall receive all Subscribed CAN Messages that are configured.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_68",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.2",
  rem="Applicability was blank in BTS V1.7; inferred from context.")
R("ME_SW_REQ_86",
  "Messages not configured as a Subscribed CAN Message shall not be received by the "
  "respective CAN port of the Primary.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_69",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.2",
  rem="Applicability was blank in BTS V1.7; inferred from context.")
R("ME_SW_REQ_87",
  "Each Subscribed CAN Message of a given CAN port shall have a unique message "
  "identifier.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_70",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.1.2",
  rem="Applicability was blank in BTS V1.7; inferred from context.")

S(3, "7.1.1.3   Published CAN Messages")
R("ME_SW_REQ_88",
  "A message that a CAN port is expected to transmit shall be configured as a Published "
  "CAN Message by the configuration tool.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_71",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.1.3",
  rem="Applicability was blank in BTS V1.7; inferred from context.")
R("ME_SW_REQ_89",
  "The configuration shall state the total number of messages published by each CAN port.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_72",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.1.3",
  rem="Applicability was blank in BTS V1.7; inferred from context.")

S(3, "7.1.2   Message Configuration")
R("ME_SW_REQ_90",
  "For each message the following parameters shall be configurable:\n1. Message Id\n"
  "2. Message Name\n3. Message Type\n4. DLC\n5. Periodicity\n6. MTO\n"
  "7. MTO Exception Enable\n8. MTO Exception Action",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_73",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.2")
R("ME_SW_REQ_91",
  "Every CAN message shall have an 11-bit message identifier configured.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_74",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.1",
  rem="Conflicts with ME_SW_REQ_67, which requires extended identifiers to be supported. "
      "Both are carried faithfully from BTS rather than one being quietly dropped. See "
      "Open Issue OI-10.")
R("ME_SW_REQ_92",
  "Configured message identifiers shall be checked for validity. The valid range is "
  "0x000 to 0x7FF.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_75",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.1")
R("ME_SW_REQ_93",
  "Any message with an invalid identifier shall not be considered for transmission or "
  "reception.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_76",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.1")
R("ME_SW_REQ_94",
  "For a given CAN port, all messages shall have a unique CAN message identifier.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_77",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.1")
R("ME_SW_REQ_95",
  "It shall be possible to configure a 29-bit extended message identifier, not using the "
  "J1939 protocol.",
  cat="Configuration", app="BM + Primary", phase="Phase 2", card="Per CAN Port",
  traces="SW_REQ_78", change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.1",
  status="Deferred",
  rem="BTS V1.7 recorded this as a note saying 'to be taken up in the next phase'. "
      "Recorded here as Phase 2 so it is not lost.")
R("ME_SW_REQ_96",
  "Every CAN message configured for a given CAN port shall have a unique message name.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_79",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.2")
R("ME_SW_REQ_97",
  "A CAN message name shall not be more than 30 characters long.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_80",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.2")
R("ME_SW_REQ_98",
  "For each CAN port, every CAN message shall be configured as either a Subscribed CAN "
  "Message or a Published CAN Message.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_81",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.3")
R("ME_SW_REQ_99",
  "CAN messages that are configured as neither Subscribed nor Published shall be ignored "
  "by the Primary.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_82",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.3")
R("ME_SW_REQ_100",
  "For every CAN message it shall be possible to set a DLC of 1 to 8 bytes through "
  "configuration.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_83",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.4")
R("ME_SW_REQ_101",
  "Padding bytes are not required in CAN messages.",
  typ="Information", cat="Configuration", app="BM + Primary", card="Per CAN Port",
  traces="SW_REQ_84", change="Carried Over", verif="Inspection",
  ref="BTS V1.7 S7.1.2.4")
R("ME_SW_REQ_102",
  "The Primary shall receive the configured periodicity for all messages to be "
  "transmitted by each CAN port.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_85",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.5")
R("ME_SW_REQ_103",
  "The Primary shall receive the configured periodicity for all messages to be received "
  "by each CAN port.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_86",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.5")
R("ME_SW_REQ_104",
  "If periodicity is not configured for a message, that message shall be ignored.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_87",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.5")
R("ME_SW_REQ_105",
  "Configured periodicity shall be checked for validity. It shall be between 10 ms and "
  "2000 ms, in multiples of 5 ms.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_88",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.5")
R("ME_SW_REQ_106",
  "A Message Time Out (MTO) shall be configurable for each Subscribed Message of every "
  "CAN port.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_89",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.6", status="Need to check")
R("ME_SW_REQ_107",
  "The minimum MTO value that may be configured shall be at least three times the "
  "periodicity of that message.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_90",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.6", status="Need to check")
R("ME_SW_REQ_108",
  "The maximum MTO that may be configured shall be 10000 ms.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_91",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.6", status="Need to check")
R("ME_SW_REQ_109",
  "If the configured MTO is less than the minimum expected value for that message, or "
  "greater than 10000 ms, it shall be ignored and MTO monitoring shall not be performed "
  "for that message.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_92",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.6", status="Need to check")
R("ME_SW_REQ_110",
  "If MTO is not configured for a message, it shall be ignored and MTO shall not be "
  "monitored for that CAN message.",
  cat="Functional", app="Primary", card="Per CAN Port", traces="SW_REQ_93",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.6", status="Need to check")
R("ME_SW_REQ_111",
  "All Subscribed CAN Messages of all CAN ports shall support the MTO Exception Enable "
  "configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_94",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.7", status="Need to check")
R("ME_SW_REQ_112",
  "If MTO Exception Enable is true for a message, an exception shall be raised on "
  "detection of the MTO for that message.",
  cat="Diagnostic", app="Primary", card="Per CAN Port", traces="SW_REQ_95",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.7", status="Need to check")
R("ME_SW_REQ_113",
  "The MTO Exception Action configuration shall define the action to be taken on "
  "detection of the MTO for a message.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_97",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.2.8", status="Need to check")
R("ME_SW_REQ_114",
  "The following MTO Exception Actions shall be possible:\n1. Stop the tests of the "
  "affected channels\n2. Continue the tests of the affected channels",
  cat="Safety", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_98",
  change="Modified", verif="Test", ref="BTS V1.7 S7.1.2.8", status="Need to check",
  rem="BTS said simply 'Stop the Test' because there was only one test. With up to 64 "
      "tests running, 'the Test' is ambiguous and a naive reading would stop all 64 "
      "because one external CAN message went stale.")
R("ME_SW_REQ_115",
  "The configuration of every Subscribed CAN Message shall state which channels, if any, "
  "that message affects. An MTO Exception Action shall be applied only to the channels so "
  "named, and shall leave all other channels running.",
  cat="Safety", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_98",
  change="New", verif="Test", ref="BTS V1.7 S7.1.2.8",
  rem="New, and it is what makes ME_SW_REQ_114 implementable. Without it the software "
      "cannot know whose test to stop.")

S(3, "7.1.3   Signal Configuration")
R("ME_SW_REQ_116",
  "The signal configuration shall have the following parameters:\n1. Signal Enumeration\n"
  "2. Signal Name\n3. Message ID\n4. Initial Value\n5. Start Bit\n6. Length\n7. Format\n"
  "8. Scale\n9. Offset",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_99",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.3")
R("ME_SW_REQ_117",
  "It shall be possible to configure multiplexed CAN messages.",
  cat="Configuration", app="BM + Primary", phase="Phase 2", card="Per CAN Port",
  traces="SW_REQ_100", change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3",
  status="Deferred",
  rem="BTS V1.7 recorded this as a note 'Add multiplexed CAN Message as well' with no "
      "applicability or status. Recorded here as Phase 2 so it is not lost.")
R("ME_SW_REQ_118",
  "Each signal of all CAN messages of all CAN ports shall have a unique enumeration in "
  "the range 0 to 65535.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_101",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.1")
R("ME_SW_REQ_119",
  "Each signal of all CAN messages of all CAN ports shall have a unique short name of not "
  "more than 30 characters.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_102",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.2")
R("ME_SW_REQ_120",
  "Each CAN signal shall carry the identifier of the CAN message it belongs to.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_103",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.3.3")
R("ME_SW_REQ_121",
  "An initial value shall be configurable for each signal.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_104",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.4")
R("ME_SW_REQ_122",
  "The configured initial value of a signal shall be less than the maximum value that "
  "signal can hold.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_105",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.4")
R("ME_SW_REQ_123",
  "Each CAN signal shall have a start bit configured.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_106",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.5")
R("ME_SW_REQ_124",
  "Valid values for the start bit are 0 to 63 inclusive.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_107",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.5")
R("ME_SW_REQ_125",
  "Each signal shall have a bit length configured.",
  cat="Configuration", app="BM", card="Per CAN Port", traces="SW_REQ_108",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.6")
R("ME_SW_REQ_126",
  "The start bit and length of the signals of one message shall be configured such that "
  "the start bit of the nth signal equals 1 + start bit of the (n-1)th signal + length of "
  "the (n-1)th signal, and the last bit of the last signal of that message is less than "
  "or equal to 64.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_109",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.6")
R("ME_SW_REQ_127",
  "Each CAN signal longer than 8 bits shall have a Format type indicating whether the "
  "signal is stored in Intel or Motorola byte order.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_110",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.7")
R("ME_SW_REQ_128",
  "It shall be possible to set the scaling factor of a signal through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_111",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.8")
R("ME_SW_REQ_129",
  "The data type of the scaling factor shall be float.",
  cat="Data", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_112",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.3.8")
R("ME_SW_REQ_130",
  "It shall be possible to set the offset for each value signal through configuration.",
  cat="Configuration", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_113",
  change="Carried Over", verif="Test", ref="BTS V1.7 S7.1.3.9")
R("ME_SW_REQ_131",
  "The data type of the offset shall be float.",
  cat="Data", app="BM + Primary", card="Per CAN Port", traces="SW_REQ_114",
  change="Carried Over", verif="Inspection", ref="BTS V1.7 S7.1.3.9")

# ============================================================== 8  ANALOG INPUTS
S(1, "8.0   Analog Inputs   (Primary and BM obligations only)")
S(2, "8.1.1   Max Battery Voltage")
R("ME_SW_REQ_132",
  "The parameter Max Battery Voltage shall be configurable for each channel as a factory "
  "setting, before a test is started on that channel.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_119",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.1",
  rem="One value in BTS. Up to 64 values in ME, and channels on the same Secondary Board "
      "may hold different battery types.")
R("ME_SW_REQ_133",
  "Any voltage configured between 2.5 V and 100 V shall be treated as a valid Max Battery "
  "Voltage.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_120",
  change="Carried Over", verif="Test", ref="BTS V1.7 S8.1.1")
R("ME_SW_REQ_134",
  "If the Max Battery Voltage configured for a channel is not valid, the Primary shall "
  "raise the Invalid Max Battery Voltage exception, naming that channel.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_121",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.1",
  rem="'naming that channel' is added. An unattributed exception is useless when 64 "
      "channels are configured.")
R("ME_SW_REQ_135",
  "If the Invalid Max Battery Voltage exception is detected for a channel, testing shall "
  "be disabled for that channel only. All other channels shall be unaffected.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_123",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.1",
  rem="BTS said 'Battery test shall be Disabled' - there was one test. Taken literally "
      "in ME this would stop 64 tests because one channel was misconfigured.")

S(2, "8.1.2   Voltage Sampling Rate")
R("ME_SW_REQ_136",
  "It shall be possible to configure the sampling rate for voltage measurement for each "
  "channel.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_124",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.2", status="Pending")
R("ME_SW_REQ_137",
  "A valid configured voltage sampling rate shall be from 1 ms to 1000 ms.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_125",
  change="Carried Over", verif="Test", ref="BTS V1.7 S8.1.2", status="Pending")
R("ME_SW_REQ_138",
  "If the configured voltage sampling rate is not valid, the Primary shall raise the "
  "Invalid Voltage Sampling Rate exception, naming the affected channel.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_126",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.2", status="Pending")
R("ME_SW_REQ_139",
  "If the Invalid Voltage Sampling Rate exception is detected for a channel, testing shall "
  "be disabled for that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_128",
  change="Modified", verif="Test", ref="BTS V1.7 S8.1.2", status="Pending")
R("ME_SW_REQ_140",
  "The Battery Manager shall reject a combination of per-channel sampling rates whose "
  "total data rate would exceed the internal CAN bus budget or the Primary processing "
  "budget defined in section 16, and shall tell the operator which limit was exceeded.",
  cat="Capacity", app="BM + Primary", card="Per System", traces="SW_REQ_124, SW_REQ_153",
  change="New", verif="Analysis", ref="BTS V1.7 S8.1.2, S8.2.3",
  rem="New, and it is arithmetic rather than opinion. 1 ms sampling was affordable for "
      "one channel. 64 channels at 1 ms is 64000 samples per second crossing a shared "
      "CAN bus. Without this check an operator can configure a system that silently "
      "cannot keep up. See Open Issue OI-12.")

S(2, "8.1.3   Reverse Polarity Detection")
R("ME_SW_REQ_141",
  "If the voltage measured on a channel is negative, that is less than 0 V, continuously "
  "for 2 seconds, the Primary shall raise the Reverse Polarity exception for that channel.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_130",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.1.3",
  rem="The measurement itself is Secondary scope. The exception is raised and owned by "
      "the Primary because the Primary decides whether the test may proceed.")
R("ME_SW_REQ_142",
  "If the Reverse Polarity exception is detected for a channel, testing shall be disabled "
  "for that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_131",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.1.3")

S(2, "8.1.4   Battery Open Detection")
R("ME_SW_REQ_143",
  "Before starting a test on a channel, if the measured battery voltage of that channel is "
  "found to be near 0 V continuously for 2 seconds, the Primary shall raise the Battery "
  "Open exception for that channel. The threshold for 'near 0 V' shall be <TBD-15>.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_133",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.1.4",
  rem="BTS wrote 'near 0V' and never quantified it. A test cannot be written against "
      "'near'.")
R("ME_SW_REQ_144",
  "If the Battery Open exception is detected for a channel, testing shall be disabled for "
  "that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_134",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.1.4")

S(2, "8.1.5   Calibration for Battery Voltage")
R("ME_SW_REQ_145",
  "It shall be possible to calibrate the battery voltage measurement of each channel. The "
  "calibration procedure, the number of calibration points and the acceptance criteria "
  "shall be <TBD-16>.",
  cat="Functional", app="BM + Primary", card="Per Channel",
  traces="SW_REQ_136, SW_REQ_137", change="Modified", verif="HIL Test",
  ref="BTS V1.7 S8.1.5",
  rem="Section 8.1.5 of BTS V1.7 contained two requirement numbers with completely empty "
      "statements. The gap is carried forward openly rather than dropped. See Open "
      "Issue OI-13.")

S(2, "8.2.1   Max Charging Current")
R("ME_SW_REQ_146",
  "The parameter Max Charging Current shall be configurable for each channel as a factory "
  "setting.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_143",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.1")
R("ME_SW_REQ_147",
  "The Max Charging Current configured for a channel shall not be more than 200 A.",
  cat="Constraint", app="BM + Primary", card="Per Channel", traces="SW_REQ_144",
  change="Carried Over", verif="Test", ref="BTS V1.7 S8.2.1")
R("ME_SW_REQ_148",
  "If the Max Charging Current configured for a channel is more than 200 A, the Primary "
  "shall raise the Invalid Max Charging Current exception, naming that channel.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_145",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.1")
R("ME_SW_REQ_149",
  "If the Invalid Max Charging Current exception is detected for a channel, testing shall "
  "be disabled for that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_147",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.1")
R("ME_SW_REQ_150",
  "The Primary shall enforce, for each channel, that no commanded charging current "
  "exceeds the Max Charging Current configured for that channel, irrespective of what the "
  "channel's program requests.",
  cat="Safety", app="Primary", card="Per Channel", traces="SW_REQ_144", change="New",
  verif="HIL Test", ref="BTS V1.7 S8.2.1",
  rem="New. In BTS the Secondary generated the setpoint and could clamp it locally. In ME "
      "the Primary generates the setpoint, so the clamp must exist on the Primary too - "
      "otherwise a badly written program can ask for more than the hardware allows.")

S(2, "8.2.2   Max Discharging Current")
R("ME_SW_REQ_151",
  "The parameter Max Discharging Current shall be configurable for each channel as a "
  "factory setting.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_148",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.2")
R("ME_SW_REQ_152",
  "The Max Discharging Current configured for a channel shall not be more than 200 A.",
  cat="Constraint", app="BM + Primary", card="Per Channel", traces="SW_REQ_149",
  change="Carried Over", verif="Test", ref="BTS V1.7 S8.2.2")
R("ME_SW_REQ_153",
  "If the Max Discharging Current configured for a channel is more than 200 A, the Primary "
  "shall raise the Invalid Max Discharging Current exception, naming that channel.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_150",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.2")
R("ME_SW_REQ_154",
  "If the Invalid Max Discharging Current exception is detected for a channel, testing "
  "shall be disabled for that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_152",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.2")
R("ME_SW_REQ_155",
  "The Primary shall enforce, for each channel, that no commanded discharging current "
  "exceeds the Max Discharging Current configured for that channel, irrespective of what "
  "the channel's program requests.",
  cat="Safety", app="Primary", card="Per Channel", traces="SW_REQ_149", change="New",
  verif="HIL Test", ref="BTS V1.7 S8.2.2", rem="See ME_SW_REQ_150.")

S(2, "8.2.3   Current Sampling Rate")
R("ME_SW_REQ_156",
  "It shall be possible to configure the sampling rate for current measurement for each "
  "channel.",
  cat="Configuration", app="BM", card="Per Channel", traces="SW_REQ_153",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.3", status="Pending")
R("ME_SW_REQ_157",
  "A valid configured current sampling rate shall be from 1 ms to 1000 ms.",
  cat="Configuration", app="BM + Primary", card="Per Channel", traces="SW_REQ_154",
  change="Carried Over", verif="Test", ref="BTS V1.7 S8.2.3", status="Pending")
R("ME_SW_REQ_158",
  "If the configured current sampling rate is not valid, the Primary shall raise the "
  "Invalid Current Sampling Rate exception, naming the affected channel.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_155",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.3", status="Pending")
R("ME_SW_REQ_159",
  "If the Invalid Current Sampling Rate exception is detected for a channel, testing shall "
  "be disabled for that channel only.",
  cat="Safety", app="BM + Primary", card="Per Channel", traces="SW_REQ_157",
  change="Modified", verif="Test", ref="BTS V1.7 S8.2.3", status="Pending")

S(2, "8.2.4   Current Calibration")
R("ME_SW_REQ_160",
  "It shall be possible to calibrate the charging current measurement of each channel.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_164",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.2.5")
R("ME_SW_REQ_161",
  "It shall be possible to calibrate the discharging current measurement of each channel.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_165",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S8.2.6")
R("ME_SW_REQ_162",
  "Calibration data shall be held per channel and shall be bound to the identity of the "
  "Secondary Board that owns that channel, so that replacing a Secondary Board does not "
  "silently apply the previous board's calibration constants to the new hardware.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and it is a real field-service hazard the single-Secondary BTS could not "
      "have. Applying board A's calibration to board B produces measurements that look "
      "plausible and are wrong. See Open Issue OI-14.")
R("ME_SW_REQ_163",
  "Calibration data shall survive a power failure and a Primary software restart.",
  cat="Data", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The ME design records that all state is currently held in RAM and that "
      "power-fail persistence is not yet designed. Calibration is the one item that "
      "cannot be re-sent by the operator from memory. See Open Issue OI-15.")

# ============================================================= 9  ANALOG OUTPUT
S(1, "9.0   Analog Output   (Primary and BM obligations only)")
R("ME_SW_REQ_164",
  "It shall be possible to calibrate the 0 V to 10 V analog output of each channel to "
  "achieve linearity of the output.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_186",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S9.6", status="Pending")
R("ME_SW_REQ_165",
  "It shall be possible to calibrate the -10 V to 0 V analog output of each channel to "
  "achieve linearity of the output.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_187",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S9.6", status="Pending",
  rem="Applies only to channels driven by a dual-transistor-bank unit; a single-bank unit "
      "has no negative output range.")
R("ME_SW_REQ_166",
  "The Primary shall command channel setpoints as engineering values, in amperes or "
  "volts, and shall not command raw analog output codes. Conversion from the engineering "
  "value to the analog output is Secondary Board scope.",
  cat="Interface", app="Primary", card="Per Channel", change="New", verif="Inspection",
  rem="New, and it is a scope-boundary requirement. It keeps the transistor-bank type, "
      "the DAC resolution and the auto-ranging gains out of the Primary, which is what "
      "makes the Secondary internals genuinely separable.")

# =============================================================== 10  PROGRAMMING
S(1, "10.0   Programming")
S(2, "10.1   Introduction")
R("ME_SW_REQ_167",
  "Charging and discharging operations of ME are controlled by a Program developed in the "
  "Battery Manager application. A Program is made of one or more sequential operations "
  "executed by the Primary Board. Each operation is called a Programming Step, or Step. "
  "The operation to be performed in each Step is called the Operator. Each Step is made up "
  "of Step No, Label, Operator, Nominal Value, Limit, Action and Registration.",
  typ="Information", cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_188", change="Modified", verif="Inspection", ref="BTS V1.7 S10.1",
  rem="BTS said the Program is 'executed by BTS hardware i.e. Primary and Secondary'. In "
      "ME the Primary alone executes it; the Secondary regulates to the setpoint it is "
      "given. Everything else about the Step structure is unchanged.")
R("ME_SW_REQ_168",
  "The Primary shall hold and execute an independent Program for each channel. Up to 64 "
  "Programs shall be able to run concurrently, each with its own step pointer, its own "
  "elapsed timers, its own accumulated quantities and its own cycle counters.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and it is the central requirement of this document. Every other programming "
      "requirement below must be read as applying to one channel's Program instance.")
R("ME_SW_REQ_169",
  "The progress of one channel's Program shall not be affected by the state, progress, "
  "error or completion of any other channel's Program.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. This is the requirement that makes a 64-channel machine commercially usable: "
      "an operator must be able to start and stop tests all day without disturbing the "
      "long-running ones.")

S(2, "10.2   Step")
R("ME_SW_REQ_170",
  "Each Programming Step shall have a unique Step number within its Program.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_189", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.2")
R("ME_SW_REQ_171",
  "There shall not be any empty Step, that is a Step without any Operator, in a Program.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_190", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.2")
R("ME_SW_REQ_172",
  "Each Programming Step shall contain the operation to be performed.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_191", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.2")

S(2, "10.3   Operator")
R("ME_SW_REQ_173",
  "The existing BTS supports 28 operators in total, of which only 14 are frequently used.",
  typ="Information", cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_192", change="Carried Over", verif="Inspection", ref="BTS V1.7 S10.3")
R("ME_SW_REQ_174",
  "The following operators shall be supported:\n1. SET\n2. PAU\n3. CHA\n4. DCH\n5. INT\n"
  "6. STO\n7. BEG\n8. CYC\n9. GOTO\n10. REG\n11. ERR\n12. MSG\n13. TABLE",
  cat="Functional", app="BM + Primary", card="Per Program", traces="SW_REQ_193",
  change="Carried Over", verif="Test", ref="BTS V1.7 S10.3",
  rem="BTS V1.7 marked this 'Done (Except REG)'. REG is therefore the one operator in "
      "this list with no working precedent. See Open Issue OI-16.")
R("ME_SW_REQ_175",
  "The operators RCH, LOM, EIS and POL are not supported in Phase 1.",
  cat="Functional", app="BM + Primary", phase="Phase 2", card="Per Program",
  traces="SW_REQ_194", change="Carried Over", verif="Inspection", ref="BTS V1.7 S10.3",
  status="Deferred")
R("ME_SW_REQ_176",
  "Worked examples of every operator and action are given in BTS V1.7 sections 10.3 and "
  "10.4 and remain valid for ME. The only change is that an example program belongs to "
  "one named channel rather than to the system.",
  typ="Information", cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_200, SW_REQ_205, SW_REQ_207, SW_REQ_212, SW_REQ_213, SW_REQ_216, "
         "SW_REQ_219, SW_REQ_232, SW_REQ_243, SW_REQ_244, SW_REQ_271, SW_REQ_273",
  change="Modified", verif="Inspection", ref="BTS V1.7 S10.3, S10.4",
  rem="BTS V1.7 carried twelve rows whose entire content was an embedded example image. "
      "They are referenced here rather than reproduced, so that nothing is lost and "
      "nothing is duplicated.")

S(3, "10.3.1   SET")
R("ME_SW_REQ_177",
  "The Primary shall support the SET operator.",
  cat="Functional", app="BM + Primary", card="Per Program", traces="SW_REQ_195",
  change="Carried Over", verif="Test", ref="BTS V1.7 S10.3.1",
  rem="Applicability was blank in BTS V1.7. The behaviour of SET is not specified in any "
      "source document. See Open Issue OI-17.")

S(3, "10.3.2   PAU")
R("ME_SW_REQ_178",
  "If the Operator of a Step is PAU, the Primary shall command the channel's power "
  "contactors to be dropped and shall hold that channel in the paused condition until the "
  "specified Limit value has elapsed.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_196",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.2",
  rem="Restated as a Primary obligation acting on one channel. The contactor is physically "
      "dropped by the Secondary; the decision to drop it is now the Primary's.")
R("ME_SW_REQ_179",
  "For the PAU operator the Nominal Value field must be blank.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_197", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_180",
  "The Limit value for PAU shall be in the range 10.0 seconds to 99.9 hours.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_198", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_181",
  "If the Primary receives a PAU operator with a Limit outside the range 10.0 seconds to "
  "99.9 hours, it shall raise OPERATOR_PAU_LIMIT_INVALID_VALUE_ERROR for that channel and "
  "the Program of that channel shall not start.",
  cat="Diagnostic", app="Primary", card="Per Channel", traces="SW_REQ_199",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_182",
  "If the Primary receives a PAU operator with a Nominal Value present, it shall raise "
  "OPERATOR_PAU_INVALID_PARAMETER_ERROR for that channel and the Program of that channel "
  "shall not be executed.",
  cat="Diagnostic", app="Primary", card="Per Channel", traces="SW_REQ_201",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_183",
  "With the PAU operator, only the GOTO action shall be allowed.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_202", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_184",
  "If a Step with the PAU operator contains a GOTO action with a valid destination, then "
  "after execution of PAU the Program of that channel shall jump to the Step whose Label "
  "is named in the GOTO action.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_203",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_185",
  "If the Primary receives a Step with the PAU operator and an action other than GOTO, it "
  "shall raise OPERATOR_PAU_INVALID_ACTION_ERROR for that channel and the Program of that "
  "channel shall not start.",
  cat="Diagnostic", app="Primary", card="Per Channel", traces="SW_REQ_204",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.2")
R("ME_SW_REQ_186",
  "If a Step contains the PAU operator with a valid Registration value, registration as "
  "per that value shall be performed for that channel during the execution of PAU.",
  cat="Data", app="Primary", card="Per Channel", traces="SW_REQ_206", change="Modified",
  verif="Test", ref="BTS V1.7 S10.3.2")

S(3, "10.3.3   CHA")
R("ME_SW_REQ_187",
  "When the Operator of a Step is CHA, the Primary shall perform the charging operation on "
  "that channel taking account of the Nominal Value, Limit, Action and Registration "
  "fields.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_208",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_188",
  "For the CHA operator, the Nominal Value and the Limit must both be given.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_209", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_189",
  "If the Nominal Value of a CHA Step is a Current, the Primary shall charge that channel "
  "in constant-current mode until the specified Limit is reached. After the Limit is "
  "crossed it shall perform the Action if one is specified, otherwise it shall advance to "
  "the next Step of that channel's Program.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_210",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_190",
  "For the CHA operator, if the Nominal Value is a Current then the Limit must be a "
  "Voltage, a Time or a Temperature.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_211", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_191",
  "If the Nominal Value of a CHA Step is a Voltage, the Primary shall charge that channel "
  "in constant-voltage mode until the specified Limit is reached. After the Limit is "
  "crossed it shall perform the Action if one is specified, otherwise it shall advance to "
  "the next Step of that channel's Program.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_214",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_192",
  "For the CHA operator, if the Nominal Value is a Voltage then the Limit must be a "
  "Current, an Ah value, a Time or a Temperature.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_215", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_193",
  "If a CHA Step has no Nominal Value, or no Limit, or neither, the Primary shall raise "
  "OPERATOR_CHA_MANDETORY_PARAMETERS_MISSING_ERROR for that channel and the Program of "
  "that channel shall not be executed.",
  cat="Diagnostic", app="Primary", card="Per Channel", traces="SW_REQ_217",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.3",
  rem="Error name carried verbatim from BTS V1.7 including its spelling, so that existing "
      "test material and log parsers still match. See Open Issue OI-18.")
R("ME_SW_REQ_194",
  "If both a Current and a Voltage Nominal Value are given with their Limits in one CHA "
  "Step, the Primary shall charge that channel in constant-current mode until the "
  "specified nominal voltage is reached, then in constant-voltage mode until the specified "
  "current Limit is reached, then perform the specified Action, or advance to the next Step "
  "if no Action is specified.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_218",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_195",
  "A Step with the CHA operator shall work with an Action, or a Registration, or both, if "
  "they are given.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_220",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_196",
  "For the CHA operator, if a Registration value is specified, the standard parameters of "
  "that channel shall be logged with the specified registration settings.",
  cat="Data", app="Primary", card="Per Channel", traces="SW_REQ_221", change="Modified",
  verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_197",
  "For the CHA operator, if the Registration value is blank, the standard parameters of "
  "that channel shall be logged with the default settings.",
  cat="Data", app="Primary", card="Per Channel", traces="SW_REQ_222", change="Modified",
  verif="Test", ref="BTS V1.7 S10.3.3")
R("ME_SW_REQ_198",
  "If a CHA or DCH Step uses Temperature as its Limit, the Primary shall use the "
  "temperature source bound to that channel. If no temperature source is bound to that "
  "channel, the Program shall not start and the reason shall be reported.",
  cat="Safety", app="BM + Primary", card="Per Channel",
  traces="SW_REQ_211, SW_REQ_215", change="New", verif="Test", ref="BTS V1.7 S10.3.3",
  rem="New. With one battery the temperature sensor was unambiguous. With 64 batteries, "
      "using an unbound or wrong sensor as a charge termination limit is a safety "
      "hazard, not a data quality problem. Related to ME_SW_REQ_47.")

S(3, "10.3.4   DCH")
R("ME_SW_REQ_199",
  "The Primary shall support the DCH operator for discharge testing. All rules stated for "
  "the CHA operator in section 10.3.3 shall apply to DCH with the direction of current "
  "reversed, and with the Max Discharging Current of that channel as the enforced limit.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_223",
  change="Modified", verif="HIL Test", ref="BTS V1.7 S10.3.4",
  rem="BTS V1.7 said only 'The BTS shall also support DCH operator for testing the "
      "Discharging' - a single line for an operator as complex as CHA. The rules are made "
      "explicit here by reference rather than left to the implementer to guess. See Open "
      "Issue OI-20.")

S(3, "10.3.5   INT")
R("ME_SW_REQ_200",
  "The Primary shall support the INT operator, which shall place the channel's Program in "
  "the interrupted state.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_224",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.5",
  rem="BTS V1.7 said only 'The BTS shall support the INT operator'. What INT does as an "
      "operator, as distinct from INT as an action, is not specified anywhere. See Open "
      "Issue OI-20.")

S(3, "10.3.6   STO")
R("ME_SW_REQ_201",
  "The STO operator shall terminate the Program of the channel executing it, and shall not "
  "affect any other channel.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_225",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.6",
  rem="'and shall not affect any other channel' is the whole point of the change.")
R("ME_SW_REQ_202",
  "The STO operator shall automatically be set at the end of each Program.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_226", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.6")
R("ME_SW_REQ_203",
  "It shall be possible to use the STO operator in sub-programs.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_227",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.6")
R("ME_SW_REQ_204",
  "STO can be used as an Operator or as an Action.",
  typ="Information", cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_228", change="Carried Over", verif="Inspection",
  ref="BTS V1.7 S10.3.6",
  rem="Applicability was 'NA' in BTS V1.7; retained here as Information because the "
      "distinction is needed to read ME_SW_REQ_205 and ME_SW_REQ_213.")
R("ME_SW_REQ_205",
  "If STO is used as an Operator, that Step must not have a Nominal Value, a Limit or an "
  "Action.",
  cat="Data", app="BM", card="Per Program", traces="SW_REQ_229", change="Carried Over",
  verif="Test", ref="BTS V1.7 S10.3.6")
R("ME_SW_REQ_206",
  "If the Primary receives a Program containing an STO operator that also carries a "
  "Nominal Value, a Limit, an Action or a Registration value, or any combination of these, "
  "it shall raise PROGRAM_STO_EXCEPTION_ERROR for that channel, shall ignore those "
  "surplus values, and shall execute the STO operator.",
  cat="Diagnostic", app="Primary", card="Per Channel",
  traces="SW_REQ_230, SW_REQ_231", change="Modified", verif="Test",
  ref="BTS V1.7 S10.3.6",
  rem="BTS V1.7 SW_REQ_230 and SW_REQ_231 contradicted each other: one raised an error "
      "for exactly the case the other said to ignore and proceed. Merged here into raise "
      "AND proceed, which is the only reading that satisfies both. This resolution needs "
      "confirmation - see Open Issue OI-21.")

S(3, "10.3.7   BEG and CYC")
R("ME_SW_REQ_207",
  "The Primary shall repeat the Steps following a BEG operator until the cycle is "
  "terminated by a CYC operator, independently for each channel.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_233",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_208",
  "The Primary shall execute the Steps of a cycle the number of times assigned to the "
  "cycle's nominal value.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_234",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_209",
  "The Primary shall ensure that all operations within a cycle execute sequentially unless "
  "a Step is interrupted by a GOTO operator or action.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_235",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_210",
  "The Primary shall ensure that all operations within a cycle execute sequentially unless "
  "a Step is interrupted by an INT operator or action.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_236",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_211",
  "The Primary shall ensure that all operations within a cycle execute sequentially unless "
  "a Step is interrupted by an STO operator or action.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_237",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_212",
  "If a GOTO action names a Step that lies outside and before the cycle, the Primary shall "
  "execute that Step and then return to the execution of the cyclic process.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_238",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_213",
  "It shall be possible to add a Label to Steps inside a cycle so that predefined counter "
  "values are not reset.",
  cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_239, SW_REQ_262, SW_REQ_263", change="Carried Over", verif="Test",
  ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_214",
  "The Primary shall execute the Step named in a GOTO action and jump to the end of the "
  "test if the prescribed limits have been reached.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_240",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_215",
  "The Primary shall move to the next Step inside a cycle if the prescribed limits of the "
  "current Step have been reached and no Action has been set.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_241",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_216",
  "The Primary shall support the nesting of up to 16 cycles within one channel's Program, "
  "without conflict between their counters.",
  cat="Capacity", app="Primary", card="Per Channel", traces="SW_REQ_242",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7", status="Pending",
  rem="BTS V1.7 wrote 'interlacing of up to 16 different cycles' with status Pending. The "
      "ME design records that the old buildCycleTable() implementation has not been "
      "ported. This is unbuilt in both systems. See Open Issue OI-22.")
R("ME_SW_REQ_217",
  "For nested cycles the Primary shall maintain a separate iteration counter for each "
  "cycle level, shall complete all iterations of an inner cycle before advancing the outer "
  "cycle, and shall reset the counters of an inner cycle at the start of each iteration of "
  "its enclosing cycle.",
  cat="Functional", app="Primary", card="Per Channel",
  traces="SW_REQ_245, SW_REQ_246, SW_REQ_247, SW_REQ_248, SW_REQ_249, SW_REQ_250, "
         "SW_REQ_251, SW_REQ_252, SW_REQ_253, SW_REQ_254, SW_REQ_255, SW_REQ_256, "
         "SW_REQ_257, SW_REQ_258",
  change="Modified", verif="Test", ref="BTS V1.7 S10.3.7", status="Pending",
  rem="BTS V1.7 spent fourteen rows restating one worked example - an outer cycle over "
      "Steps 11 to 20 run twice, an inner cycle over Steps 14 to 17 run three times, "
      "hence six inner executions. Those fourteen rows state three general rules, which "
      "are given here. The example itself is referenced by ME_SW_REQ_176.")
R("ME_SW_REQ_218",
  "Cycle counters, including the predefined Ah and Wh counters, shall be maintained per "
  "channel and shall be reported per channel.",
  cat="Data", app="Primary", card="Per Channel", traces="SW_REQ_261", change="Modified",
  verif="Test", ref="BTS V1.7 S10.3.7")
R("ME_SW_REQ_219",
  "If an external condition or error is encountered, the Primary shall log the cycle status "
  "of the affected channel and abort execution of that channel's Program as per the "
  "system-defined limits.",
  cat="Diagnostic", app="Primary", card="Per Channel",
  traces="SW_REQ_259, SW_REQ_260", change="Modified", verif="Test",
  ref="BTS V1.7 S10.3.7",
  rem="BTS SW_REQ_260 was an empty requirement row with applicability 'All' and status "
      "'Done'; there was nothing to carry from it beyond this.")

S(2, "10.4   Action")
R("ME_SW_REQ_220",
  "The Action column indicates how a test Step is terminated as soon as the associated "
  "Limit is reached.",
  typ="Information", cat="Functional", app="BM + Primary", card="Per Program",
  traces="SW_REQ_264", change="Carried Over", verif="Inspection", ref="BTS V1.7 S10.4")
R("ME_SW_REQ_221",
  "The Primary shall perform the Action of a Step if the prescribed Limit of that Step has "
  "been reached.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_265",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4")
R("ME_SW_REQ_222",
  "The Primary shall move to the next Step of that channel's Program if the predefined "
  "Limit is reached and the Action is blank.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_266",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4")
R("ME_SW_REQ_223",
  "The following Actions shall be supported:\n1. INT\n2. STO\n3. GOTO\n"
  "4. <Procedure Name>\n5. ERR\n6. MSG",
  cat="Functional", app="BM + Primary", card="Per Program", traces="SW_REQ_267",
  change="Carried Over", verif="Test", ref="BTS V1.7 S10.4",
  rem="BTS V1.7 listed the second action as 'STOP' here and as 'STO' in section 10.4.2. "
      "Written as STO throughout.")
R("ME_SW_REQ_224",
  "When the associated Limit of the Step being executed is reached and the Action is INT, "
  "the Program of that channel shall be interrupted.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_268",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.1")
R("ME_SW_REQ_225",
  "If the Primary receives a Continue command from the Battery Manager for a channel that "
  "is in the interrupted state, that channel's Program shall advance to its next Step. A "
  "Continue command shall affect only the channel it names.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_269",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.1",
  rem="The channel qualification is new. A global Continue would restart every interrupted "
      "channel at once, which an operator would almost never intend.")
R("ME_SW_REQ_226",
  "When the associated Limit of the Step being executed is reached and the Action is STO, "
  "the Primary shall terminate the Step being executed and shall advance to the next Step "
  "of that channel's Program.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_270",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.2")
R("ME_SW_REQ_227",
  "When the associated Limit of the Step being executed is reached and the Action is GOTO, "
  "the Program of that channel shall jump to the Step that carries the named Jump "
  "Destination in its Label column.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_272",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.3")
R("ME_SW_REQ_228",
  "When the associated Limit of the Step being executed is reached and the Action names a "
  "Procedure, the Primary shall execute the named Procedure for that channel.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_274",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.4", status="Pending")
R("ME_SW_REQ_229",
  "After execution of a Procedure the Primary shall execute the Step following the Step "
  "from which the Procedure was called.",
  cat="Functional", app="Primary", card="Per Channel", traces="SW_REQ_275",
  change="Modified", verif="Test", ref="BTS V1.7 S10.4.4", status="Pending")
R("ME_SW_REQ_230",
  "The Primary shall support the ERR Action.",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", traces="SW_REQ_276",
  change="Carried Over", verif="Test", ref="BTS V1.7 S10.4.5",
  rem="Applicability was blank in BTS V1.7. What ERR does is not specified anywhere. See "
      "Open Issue OI-20.")
R("ME_SW_REQ_231",
  "The Primary shall support the MSG Action.",
  cat="Functional", app="BM + Primary", card="Per Channel", traces="SW_REQ_277",
  change="Carried Over", verif="Test", ref="BTS V1.7 S10.4.6",
  rem="Applicability was blank in BTS V1.7. What MSG does is not specified anywhere. See "
      "Open Issue OI-20.")
R("ME_SW_REQ_232",
  "Every message or error produced by an ERR or MSG Action shall identify the channel and "
  "the Step number that produced it.",
  cat="Usability", app="BM + Primary", card="Per Channel",
  traces="SW_REQ_276, SW_REQ_277", change="New", verif="Test", ref="BTS V1.7 S10.4",
  rem="New. An operator message with no channel attached is close to useless when 64 "
      "channels can produce one.")

# =========================================== 11  PROGRAM COMPILATION AND DOWNLOAD
S(1, "11.0   Program Compilation and Download")
R("ME_SW_REQ_233",
  "All Steps of a Program shall be compiled by the programming tool.",
  cat="Functional", app="BM", card="Per Program", traces="SW_REQ_278",
  change="Carried Over", verif="Test", ref="BTS V1.7 S11")
R("ME_SW_REQ_234",
  "After successful compilation, the Program shall be downloaded over Ethernet to the "
  "Primary, addressed to the channel or channels it is intended for.",
  cat="Interface", app="BM", card="Per Channel", traces="SW_REQ_279", change="Modified",
  verif="Test", ref="BTS V1.7 S11",
  rem="BTS downloaded 'in the BTS Primary' - there was one destination. A download now "
      "needs a destination channel.")
R("ME_SW_REQ_235",
  "If any compilation error is found in a Program, the user shall be notified and the "
  "Program shall not be downloaded.",
  cat="Usability", app="BM", card="Per Program", traces="SW_REQ_280",
  change="Carried Over", verif="Test", ref="BTS V1.7 S11")
R("ME_SW_REQ_236",
  "If a Program download fails for any reason, the user shall be notified, and the "
  "notification shall name the channel and the reason.",
  cat="Usability", app="BM + Primary", card="Per Channel", traces="SW_REQ_281",
  change="Modified", verif="Test", ref="BTS V1.7 S11")
R("ME_SW_REQ_237",
  "The Primary shall be able to store one Program per channel, that is up to 64 Programs "
  "concurrently.",
  cat="Capacity", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The maximum Program size per channel and therefore the total storage needed "
      "is <TBD-17>. See Open Issue OI-23.")
R("ME_SW_REQ_238",
  "It shall be possible to assign one authored Program to several channels without "
  "authoring it again.",
  cat="Usability", app="BM", card="Per Channel", change="New", verif="Test",
  rem="New. Testing 64 identical cells with the same profile is the most common expected "
      "use. Requiring 64 separate downloads of the same program would be unusable.")
R("ME_SW_REQ_239",
  "The Primary shall reject a Program download addressed to a channel whose test is "
  "running or interrupted, and shall report the rejection with its reason. It shall not "
  "modify the stored Program of that channel.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. Overwriting the program of a running test would leave the step pointer "
      "indexing into a program that no longer exists.")
R("ME_SW_REQ_240",
  "The Primary shall verify the integrity of a stored Program before starting a test on "
  "that channel, and shall refuse to start if the Program is incomplete or fails its "
  "integrity check.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The ME design detects program completeness from the step-chain terminator, "
      "which means a truncated transfer is detectable. Making the refusal explicit stops "
      "a partial program being executed as though it were whole.")
R("ME_SW_REQ_241",
  "It shall be possible to read back from the Primary the Program currently stored for any "
  "channel, and to compare it with the Program held in the Battery Manager.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. With 64 channels holding 64 programs, an operator has no other way to confirm "
      "that the machine is running what they think it is running.")

# ============================ 12  SYSTEM TOPOLOGY, ADDRESSING AND CAPACITY (NEW)
S(1, "12.0   System Topology, Addressing and Capacity        [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_242",
  "The Primary shall support up to 8 Secondary Boards.",
  cat="Capacity", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S4.1",
  rem="ME_MAX_SECONDARIES = 8 in the current ME design.")
R("ME_SW_REQ_243",
  "The Primary shall support up to 8 channels per Secondary Board.",
  cat="Capacity", app="BM + Primary", card="Per Secondary", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S4.1",
  rem="ME_MAX_CHANNELS = 8 in the current ME design.")
R("ME_SW_REQ_244",
  "The Primary shall support up to 64 channels in total, and shall be able to hold "
  "independent configuration, calibration, Program and test state for every one of them "
  "simultaneously.",
  cat="Capacity", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S4.1",
  rem="ME_MAX_CIRCUITS = 64 in the current ME design.")
R("ME_SW_REQ_245",
  "Every channel shall be identified system-wide by a single-byte Channel Address in which "
  "the upper nibble is the Secondary Board ID and the lower nibble is the channel index "
  "within that board. Both nibbles shall be 1-based, giving valid values 0x11 to 0x88.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S5.2",
  rem="This is the ME redefinition of the legacy BTS flat circuit number. The ME design "
      "already implements it as ME_CIRCUIT_ID(sec, ch).")
R("ME_SW_REQ_246",
  "The Primary shall reject a Channel Address whose Secondary nibble is 0, whose channel "
  "nibble is 0, whose Secondary nibble exceeds the supported maximum, or whose channel "
  "nibble exceeds the supported maximum. A rejected address shall be logged and shall "
  "never be silently mapped onto a valid channel.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S4.1",
  rem="Silently folding a malformed address onto channel 1 would apply a command to the "
      "wrong battery. The ME design already returns ME_SLOT_INVALID for these cases.")
R("ME_SW_REQ_247",
  "The permitted range of the Channel Address shall be consistent between the encoding "
  "definition and the validation logic. The agreed range is <TBD-18>.",
  cat="Data", app="BM + Primary", card="Per System", change="New", verif="Inspection",
  ref="ME_Primary_Comm_Block_Diagram S5.2, ME_Primary_BTS_Block_Diagram S4.1",
  rem="Two ME design documents disagree today: the nibble encoding admits 15 Secondaries "
      "and 15 channels, while storage and validation cap at 8 and 8. Both readings are "
      "defensible, so the range must be stated once and honoured everywhere. See Open "
      "Issue OI-24.")
R("ME_SW_REQ_248",
  "Every command, measurement, test record, log entry and error report that concerns a "
  "channel shall carry that channel's Channel Address.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Inspection",
  rem="The single rule that makes a 64-channel system diagnosable. BTS needed no such "
      "rule because everything implicitly concerned the one battery.")
R("ME_SW_REQ_249",
  "The system shall operate correctly with fewer than the maximum number of Secondary "
  "Boards fitted, and with fewer than the maximum number of channels populated on a fitted "
  "Secondary Board.",
  cat="Functional", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="A partly populated rack is the normal delivered configuration, not an exception.")
R("ME_SW_REQ_250",
  "The Battery Manager shall present the actual discovered configuration - which Secondary "
  "Boards are present and which of their channels are usable - and shall not present "
  "channels that do not exist as though they were merely idle.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Test",
  rem="An absent channel and an idle channel look identical unless the software "
      "distinguishes them, and an operator will eventually try to start a test on one "
      "that is not there.")

# ================== 13  SECONDARY BOARD ENROLMENT AND SUPERVISION (NEW)
S(1, "13.0   Secondary Board Enrolment and Supervision       [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_251",
  "The Primary shall discover and enrol each Secondary Board present on the internal bus, "
  "without manual configuration of the bus population.",
  cat="Functional", app="Primary", card="Per Secondary", change="New", verif="Test",
  rem="New. BTS had a fixed point-to-point link and therefore nothing to discover.")
R("ME_SW_REQ_252",
  "Enrolment of a Secondary Board shall establish and record at least: its Secondary ID, "
  "its hardware identity, its software or firmware version, the number of channels it "
  "provides, and the transistor bank type of each of those channels.",
  cat="Data", app="Primary", card="Per Secondary", change="New", verif="Test",
  rem="The bank type is needed by ME_SW_REQ_23, and the channel count by ME_SW_REQ_250.")
R("ME_SW_REQ_253",
  "The Primary shall not send any channel command to a Secondary Board that is not "
  "enrolled.",
  cat="Safety", app="Primary", card="Per Secondary", change="New", verif="Test",
  rem="Admission control. Commanding a board whose capability is unknown is how a 200 A "
      "setpoint reaches a channel rated for less.")
R("ME_SW_REQ_254",
  "If two Secondary Boards present the same Secondary ID, the Primary shall enrol neither, "
  "shall report the conflict identifying both, and shall continue to serve all correctly "
  "enrolled boards.",
  cat="Safety", app="BM + Primary", card="Per Secondary", change="New", verif="Test",
  rem="A duplicated address on a shared bus is a realistic commissioning error. Enrolling "
      "one of the two arbitrarily would send every command for those 8 channels to an "
      "unpredictable board.")
R("ME_SW_REQ_255",
  "The Primary shall monitor the liveness of every enrolled Secondary Board and shall "
  "declare a board lost if no valid response or periodic message is received from it within "
  "<TBD-19> ms.",
  cat="Safety", app="Primary", card="Per Secondary", change="New", verif="Test")
R("ME_SW_REQ_256",
  "When a Secondary Board is declared lost, the Primary shall place the tests of that "
  "board's channels into a defined fault state, shall report the loss naming the board and "
  "its affected channels, and shall leave the channels of all other Secondary Boards "
  "running undisturbed.",
  cat="Safety", app="BM + Primary", card="Per Secondary", change="New", verif="HIL Test",
  rem="Eight channels stopping is bad. Sixty-four channels stopping because one board's "
      "connector was loose is a different order of problem.")
R("ME_SW_REQ_257",
  "When a previously lost Secondary Board becomes reachable again, the Primary shall "
  "re-enrol it and shall verify that its identity and capability are unchanged before "
  "returning its channels to service.",
  cat="Safety", app="Primary", card="Per Secondary", change="New", verif="Test",
  rem="If the board was swapped rather than reconnected, its calibration constants no "
      "longer apply. See ME_SW_REQ_162.")
R("ME_SW_REQ_258",
  "The Primary shall tolerate a Secondary Board being removed or added while the system is "
  "powered, without disturbing the tests running on other Secondary Boards.",
  cat="Safety", app="Primary", card="Per Secondary", change="New", verif="HIL Test",
  rem="Whether hot-swap is a supported field operation is Open Issue OI-25. The "
      "requirement is stated because the software behaviour must be defined either way.")
R("ME_SW_REQ_259",
  "The Primary shall report the enrolment state of every Secondary Board, and the reason "
  "for any board not being enrolled, to the Battery Manager.",
  cat="Diagnostic", app="BM + Primary", card="Per Secondary", change="New", verif="Test")
R("ME_SW_REQ_260",
  "The enrolment of channels beyond the first shall be implemented before Phase 1 is "
  "considered complete.",
  cat="Functional", app="Primary", card="Per System", change="New", verif="Test",
  status="Pending",
  rem="Recorded because the current ME implementation registers exactly one circuit and "
      "is, in its own words, functionally single-circuit until the bus-side enrolment "
      "handshake exists. This is the largest single gap between the design as built and "
      "this specification. See Open Issue OI-26.")

# ======================================= 14  ME INTERNAL CAN BUS (NEW)
S(1, "14.0   ME Internal CAN Bus, Primary to Secondary Boards   [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_261",
  "The Primary shall communicate with all Secondary Boards over a single shared CAN bus "
  "dedicated to that purpose.",
  cat="Interface", app="Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S8 (IF-B)")
R("ME_SW_REQ_262",
  "The internal CAN bus shall use CAN FD.",
  cat="Interface", app="Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S3",
  rem="The ME design routing table names a CAN-FD frame for the Core-to-CAN path. Classic "
      "CAN would not carry 64 channels of live data at the sampling rates section 8 "
      "permits. The decision needs confirming - see Open Issue OI-27.")
R("ME_SW_REQ_263",
  "The internal CAN bus shall operate at an arbitration bit rate of <TBD-20> and a data "
  "bit rate of <TBD-21>.",
  cat="Interface", app="Primary", card="Per System", change="New", verif="Test",
  rem="No ME or BTS document states either rate. Both follow from the load budget of "
      "ME_SW_REQ_264. See Open Issue OI-27.")
R("ME_SW_REQ_264",
  "The worst-case load of the internal CAN bus, with the maximum number of Secondary "
  "Boards enrolled and all channels running at their maximum configured sampling and "
  "registration rates, shall not exceed <TBD-22> per cent of the bus capacity.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="New, and it is the calculation that decides whether the topology works at all. It "
      "must be done before the bit rate and the sampling limits are fixed. See Open Issue "
      "OI-12.")
R("ME_SW_REQ_265",
  "Frames that carry a setpoint command, a stop command or a fault report shall be "
  "assigned CAN identifiers that win arbitration against frames carrying live measurement "
  "data.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="New. On a shared bus, measurement traffic from 64 channels can delay a stop "
      "command. Priority must be designed in, not discovered on the bench.")
R("ME_SW_REQ_266",
  "The Primary shall deliver a setpoint change to the target Secondary Board within "
  "<TBD-23> ms of deciding it, measured worst case with the bus at its budgeted load.",
  cat="Performance", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. In BTS the Secondary decided its own setpoints, so this delay did not exist. "
      "In ME it is inside the control loop of every channel.")
R("ME_SW_REQ_267",
  "The Primary shall detect that the measurement data of a channel has become stale, "
  "within <TBD-24> ms, and shall treat stale data as a fault of that channel rather than "
  "continuing to act on the last received value.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. Regulating a battery against a measurement that stopped updating is the "
      "failure mode that a shared bus introduces and a point-to-point link did not.")
R("ME_SW_REQ_268",
  "The Primary shall detect a CAN bus-off condition, shall attempt recovery, shall place "
  "all channels into a defined fault state while the bus is unusable, and shall report the "
  "condition.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_269",
  "The Primary shall count and report internal CAN bus error frames, retransmissions and "
  "dropped frames, per Secondary Board.",
  cat="Diagnostic", app="BM + Primary", card="Per Secondary", change="New", verif="Test",
  rem="Per board, because a single failing board or a single bad cable is the most likely "
      "cause and an aggregate count will not find it.")
R("ME_SW_REQ_270",
  "The internal CAN bus configuration shall not be exposed for modification through the "
  "Battery Manager or any external interface.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="Inspection",
  rem="See ME_SW_REQ_65. The user-configurable CAN engine of section 7 must not be able "
      "to reach this bus.")

# ================================== 15  HOST INTERFACE, BM TO PRIMARY (NEW)
S(1, "15.0   Host Interface, Battery Manager to Primary       [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_271",
  "The Primary shall act as the TCP client and the Battery Manager shall act as the TCP "
  "server for the command and response channel. The Primary shall initiate the connection.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S1",
  rem="This is the reverse of the usual arrangement and it is deliberate in the ME design. "
      "It is stated here because a reader who assumes the board is a server will design "
      "the wrong thing.")
R("ME_SW_REQ_272",
  "The command and response channel shall use TCP port 9999.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S1")
R("ME_SW_REQ_273",
  "If the connection to the Battery Manager cannot be established or is lost, the Primary "
  "shall retry with an exponential backoff starting at 1 second and capped at 30 seconds, "
  "shall reset the backoff to its minimum after a successful connection, and shall never "
  "exit.",
  cat="Functional", app="Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S5",
  rem="Already implemented in the ME design. Recorded as a requirement so it cannot be "
      "removed by accident.")
R("ME_SW_REQ_274",
  "Live measurement data shall be sent from the Primary to the Battery Manager on UDP port "
  "10000.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S1")
R("ME_SW_REQ_275",
  "Test record data shall be sent from the Primary to the Battery Manager on UDP port "
  "10001.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Comm_Block_Diagram S1",
  rem="The ME sources call this traffic 'registration data', which has nothing to do with "
      "device registration. See the terminology note on sheet 2 and Open Issue OI-19.")
R("ME_SW_REQ_276",
  "Every frame exchanged between the Primary and the Battery Manager shall be protected by "
  "a CRC-16/MODBUS checksum, using polynomial 0xA001 and initial value 0xFFFF with no final "
  "inversion.",
  cat="Data", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Implementation_Reference S6")
R("ME_SW_REQ_277",
  "The byte order of the CRC on the wire, and of all multi-byte fields, shall be the same "
  "in both directions, and shall be <TBD-25>.",
  cat="Data", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Implementation_Reference ADR-9",
  rem="Two ME documents disagree. Big-endian is recorded as hardware-verified in both "
      "directions, yet a captured server response carried its CRC little-endian. Until "
      "this is settled, an interoperability failure is possible in the field. See Open "
      "Issue OI-28.")
R("ME_SW_REQ_278",
  "The Primary shall reassemble frames correctly from the TCP byte stream, handling both "
  "the case of several frames arriving in one read and the case of one frame split across "
  "several reads.",
  cat="Functional", app="Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_Implementation_Reference S5",
  rem="TCP is a stream, not a message service. This is stated because the legacy code did "
      "not do it and the ME design had to add it.")
R("ME_SW_REQ_279",
  "A frame whose CRC does not verify shall be rejected, shall be logged with both the "
  "computed and the received checksum, and shall not close the connection.",
  cat="Diagnostic", app="Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_280",
  "A frame carrying an unrecognised command group or an unrecognised query identifier "
  "shall be logged and discarded, and shall not close the connection. It shall not be "
  "acknowledged as successful.",
  cat="Diagnostic", app="Primary", card="Per System", change="New", verif="Test",
  rem="Acknowledging a command that was never carried out is worse than not answering, "
      "because the Battery Manager will report success to the operator.")
R("ME_SW_REQ_281",
  "An acknowledgement returned by the Primary shall distinguish 'accepted for execution' "
  "from 'executed', so that the Battery Manager never reports a test as started when only "
  "the command was queued.",
  cat="Interface", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="New. The ME design notes that the current acknowledgement means only 'queued'. With "
      "64 channels an operator cannot verify by looking at the machine.")
R("ME_SW_REQ_282",
  "The Primary shall support device discovery, so that a Battery Manager can find Primary "
  "units on the network without their addresses being known in advance.",
  cat="Interface", app="BM + Primary", phase="Phase 2", card="Per System", change="New",
  verif="Test", status="Deferred",
  ref="ME_Primary_Comm_Block_Diagram S8",
  rem="Defined in the source documents on UDP 10002 and 10003 and explicitly out of scope "
      "of the current ME milestone. Recorded as Phase 2 so it is not lost.")

# ============================= 16  LIVE DATA AND TEST RECORDS (NEW)
S(1, "16.0   Live Data and Test Record Registration           [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_283",
  "The Primary shall report live measurement data for every running channel, each report "
  "carrying at least: step number, program running status, channel status, error "
  "identifier, step running time, program running time, current, voltage, temperature, "
  "power, accumulated and per-step capacity and energy, operator code, cycle and table "
  "position, registration type, and digital input and output state.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S6.6",
  rem="The field list is taken from the live data frame the ME design already implements. "
      "It is recorded here so the requirement does not depend on reading the code.")
R("ME_SW_REQ_284",
  "Live measurement data shall be treated as valuable only while fresh. If the outbound "
  "path is congested, the Primary shall discard the stale report rather than delay a newer "
  "one, and shall count every discard.",
  cat="Performance", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S6.6",
  rem="Correct for a live display, and already the ME design's behaviour. It is "
      "deliberately the opposite of the rule for test records in ME_SW_REQ_285.")
R("ME_SW_REQ_285",
  "Test record data shall never be discarded silently. Every test record produced by the "
  "Primary shall either be delivered to the Battery Manager, or be reported as lost, "
  "identifying the channel and the time span affected.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and it fixes a known defect of the existing design, in which saved "
      "measurements are sent without confirmation so a lost message leaves an invisible "
      "hole in a test record that may have taken days to produce.")
R("ME_SW_REQ_286",
  "The registration type and registration interval shall be configurable per channel, "
  "within the range <TBD-26>.",
  cat="Configuration", app="BM + Primary", card="Per Channel", change="New",
  verif="Test",
  rem="BTS carried a Registration field per Step but never specified its permitted values. "
      "See Open Issue OI-16.")
R("ME_SW_REQ_287",
  "All floating-point values on any external interface shall be encoded as IEEE 754 "
  "single-precision, in the byte order fixed by ME_SW_REQ_277.",
  cat="Data", app="BM + Primary", card="Per System", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram S6.6")
R("ME_SW_REQ_288",
  "Each test record shall carry the identifier of the test session it belongs to, so that "
  "records from a repeated test on the same channel cannot be confused with one another.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_Implementation_Reference S13",
  rem="The ME design already receives a session identifier with the Start command but "
      "currently only logs it. With 64 channels being started and restarted repeatedly, "
      "records with no session identity cannot be reliably grouped.")

# ================================== 17  TEST CONTROL COMMANDS (NEW)
S(1, "17.0   Test Control Commands                            [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_289",
  "The Primary shall accept the commands Start, Stop, Pause, Continue, Sync Time and Reset "
  "from the Battery Manager.",
  cat="Functional", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_Implementation_Reference S13")
R("ME_SW_REQ_290",
  "Start, Stop, Pause and Continue shall each act on exactly the channel named in the "
  "command and on no other.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. In BTS these were necessarily global because there was one channel. Carrying "
      "that behaviour forward unexamined would make every Stop a Stop-all.")
R("ME_SW_REQ_291",
  "The Primary shall state, for each command, whether it is scoped to a channel, to a "
  "Secondary Board or to the whole system. Sync Time and Reset are system-scoped.",
  cat="Interface", app="BM + Primary", card="Per System", change="New",
  verif="Inspection", ref="ME_Primary_Implementation_Reference S13",
  rem="New. The ME design currently gates system-scoped commands as though they were "
      "channel-scoped, which works only by accident. See Open Issue OI-29.")
R("ME_SW_REQ_292",
  "It shall be possible to start, stop, pause and continue a named group of channels, or "
  "all channels, in one operation.",
  cat="Usability", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="New. Starting 64 tests one at a time is not a workable operator experience.")
R("ME_SW_REQ_293",
  "The Primary shall refuse a Start command for a channel unless that channel has a "
  "complete and valid Program, valid configuration, valid calibration data, and an enrolled "
  "Secondary Board, and shall report which of these was missing.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The ME design currently abandons a Start with a log message when the Program "
      "is missing. Reporting the reason back to the operator is what makes that "
      "actionable.")
R("ME_SW_REQ_294",
  "A Stop command shall bring the named channel to its defined safe state before the "
  "channel is reported as stopped.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="HIL Test")
R("ME_SW_REQ_295",
  "The behaviour of a Start command addressed to a channel that has already completed a "
  "test shall be defined as <TBD-27>.",
  cat="Functional", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  ref="ME_Primary_BTS_Block_Diagram",
  rem="The ME design currently re-runs the previous program from the beginning, and its "
      "author flagged this as a deliberate choice open to reversal. With 64 channels an "
      "accidental restart of a completed 12-hour test is a costly mistake. See Open Issue "
      "OI-30.")
R("ME_SW_REQ_296",
  "The system shall provide a means of bringing all channels to their safe state "
  "immediately, independently of the state of the Battery Manager link.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="HIL Test",
  rem="New. Whether this is a physical emergency stop input, a software command, or both, "
      "and what 'safe' means for a battery mid-discharge, is unresolved in every source "
      "document. It affects a large number of safety requirements. See Open Issue OI-05.")

# ========================= 18  FAULT CONTAINMENT AND CHANNEL ISOLATION (NEW)
S(1, "18.0   Fault Containment and Channel Isolation          [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_297",
  "Every fault the Primary can detect shall be classified by scope as a channel fault, a "
  "Secondary Board fault, or a system fault.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="Inspection",
  rem="New, and it is the organising idea of this section. BTS needed no scope because "
      "every fault was a system fault by construction.")
R("ME_SW_REQ_298",
  "A channel fault shall stop or inhibit only the affected channel. All other channels "
  "shall continue unaffected.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test")
R("ME_SW_REQ_299",
  "A Secondary Board fault shall stop or inhibit only the channels of that board. The "
  "channels of all other boards shall continue unaffected.",
  cat="Safety", app="Primary", card="Per Secondary", change="New", verif="Test")
R("ME_SW_REQ_300",
  "A system fault shall bring all channels to their safe state, and shall be reported as "
  "system-scoped so that the operator is not left looking for a faulty channel.",
  cat="Safety", app="BM + Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_301",
  "A fault on one channel shall not prevent a test from being started on any other healthy "
  "channel.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. Stated separately from ME_SW_REQ_298 because inhibiting new starts globally "
      "after any fault is an easy and tempting implementation that would make the machine "
      "unusable.")
R("ME_SW_REQ_302",
  "Every fault report shall carry its scope, the affected channel or board, an error code, "
  "a severity, and the time of detection.",
  cat="Diagnostic", app="BM + Primary", card="Per System", change="New", verif="Test",
  rem="Same structure as ME_SW_REQ_82.")
R("ME_SW_REQ_303",
  "The Primary shall detect the failure or stall of its own control software and shall "
  "bring all channels to their safe state if it occurs.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="Test",
  rem="New. In BTS a stalled Primary left a Secondary that was still executing its own "
      "program. In ME a stalled Primary leaves 64 channels with no decision maker, holding "
      "whatever setpoint they last received.")

# ==================== 19  PERFORMANCE, CAPACITY AND TIMING BUDGETS (NEW)
S(1, "19.0   Performance, Capacity and Timing Budgets         [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_304",
  "The Primary shall sustain the full function of this specification with all supported "
  "channels running concurrently, each with an independent Program, for the maximum "
  "supported test duration.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Test",
  rem="The umbrella performance requirement. Everything below quantifies part of it.")
R("ME_SW_REQ_305",
  "The Primary shall re-evaluate the control decision of every running channel at least "
  "once every <TBD-28> ms.",
  cat="Performance", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and it is the number that decides the whole software architecture. In BTS this "
      "loop lived on the Secondary next to the hardware. In ME it lives on the Primary, "
      "on a Linux application processor, behind a shared bus. No source document states a "
      "rate. See Open Issue OI-31.")
R("ME_SW_REQ_306",
  "The jitter on the control decision interval of any channel shall not exceed <TBD-29> "
  "ms, and shall not accumulate over the duration of a test.",
  cat="Performance", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="Non-accumulating is the important half. An interval that drifts turns an Ah "
      "integration over several days into a wrong number that looks right.")
R("ME_SW_REQ_307",
  "The Primary shall report live data for every running channel at the configured rate, "
  "and the aggregate live data rate for all channels shall not exceed <TBD-30>.",
  cat="Performance", app="BM + Primary", card="Per System", change="New",
  verif="Analysis")
R("ME_SW_REQ_308",
  "The Primary software shall be shown by analysis and by measurement to have at least "
  "<TBD-31> per cent processor headroom under the worst case of ME_SW_REQ_304.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="Stated as a requirement because the ME design records an open item to verify actual "
      "resource use against the container limit once several channels carry real programs.")
R("ME_SW_REQ_309",
  "The memory required by the Primary software, including all per-channel program, "
  "configuration, calibration and buffer storage, shall be bounded, shall be known before "
  "the software starts running, and shall fit within <TBD-32>.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="New. The current ME design reserves a large fixed block for 64 channels' program "
      "buffers, and verifying that against the platform's limit is an open item in that "
      "design. See Open Issue OI-32.")
R("ME_SW_REQ_310",
  "The Primary software shall not allocate memory dynamically after initialisation is "
  "complete.",
  cat="Constraint", app="Primary", card="Per System", change="New", verif="Inspection",
  rem="New. Makes ME_SW_REQ_309 verifiable and removes allocation failure and "
      "fragmentation as causes of a mid-test fault.")
R("ME_SW_REQ_311",
  "The worst-case timing behaviour of the Primary software shall be analysed and "
  "documented, and shall be re-verified on the target hardware whenever the channel count, "
  "the sampling rates or the internal bus configuration changes.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Analysis",
  rem="New. Timing analysis on real hardware is the one thing that cannot be established "
      "by review, and every number in this section depends on it.")

# ============== 20  STARTUP, SHUTDOWN, PERSISTENCE AND RECOVERY (NEW)
S(1, "20.0   Startup, Shutdown, Persistence and Recovery      [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_312",
  "On start-up the Primary shall not command any channel until it has enrolled the "
  "Secondary Boards and validated their configuration.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_313",
  "Channel configuration, calibration data and stored Programs shall survive a power "
  "failure and a Primary software restart.",
  cat="Data", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The current ME design holds all of this in volatile memory and records "
      "power-fail persistence as not yet designed. For a 64-channel machine, losing 64 "
      "programs and 64 calibration sets on a power blip is not acceptable. See Open Issue "
      "OI-15 and section 23.0, which now specifies the power-fail save/restore "
      "behaviour.")
R("ME_SW_REQ_314",
  "After an unexpected restart, the Primary shall not silently resume any test. For every "
  "channel that was running it shall record that the test was interrupted by a restart, and "
  "shall require an explicit operator decision before that channel runs again.",
  cat="Safety", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. Automatically resuming a partly completed charge on a battery whose state is "
      "no longer known is a hazard, and a test record with an unmarked gap is worthless. "
      "This rule now applies only when no valid power-fail snapshot exists - where one "
      "does, ME_SW_REQ_344 in section 23.0 automatically resumes instead. See Open Issue "
      "OI-34.")
R("ME_SW_REQ_315",
  "Test records already produced before an unexpected restart shall remain retrievable, "
  "and the gap caused by the restart shall be explicit in the record.",
  cat="Data", app="BM + Primary", card="Per Channel", change="New", verif="Test")
R("ME_SW_REQ_316",
  "On a commanded shutdown the Primary shall bring every channel to its safe state, shall "
  "flush all pending test records, and shall only then stop.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_317",
  "The Primary shall report its own software version, and the software version of every "
  "enrolled Secondary Board, to the Battery Manager.",
  cat="Diagnostic", app="BM + Primary", card="Per Secondary", change="New", verif="Test")
R("ME_SW_REQ_318",
  "The Primary shall refuse to admit a Secondary Board whose software version is not "
  "compatible with its own, and shall report the incompatibility rather than operating the "
  "board.",
  cat="Safety", app="BM + Primary", card="Per Secondary", change="New", verif="Test",
  rem="New. With 8 field-replaceable boards, a mixed-version rack is inevitable. Silent "
      "operation of an incompatible board is the worst of the available outcomes.")
R("ME_SW_REQ_319",
  "The Primary shall be ready to accept commands within <TBD-33> seconds of power being "
  "applied.",
  cat="Performance", app="Primary", card="Per System", change="New", verif="Test")

# ============================= 21  DIAGNOSTICS AND LOGGING (NEW)
S(1, "21.0   Diagnostics and Logging                          [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_320",
  "A catalogue of every error code the Primary can report shall be maintained, giving for "
  "each code its meaning, its scope, its severity and the recommended operator action.",
  cat="Diagnostic", app="BM + Primary", card="Per System", change="New",
  verif="Inspection",
  rem="New. BTS named individual errors in prose in nine places and asked 'how do we "
      "indicate this error?' without answering. A catalogue is what makes ME_SW_REQ_82 "
      "implementable. See Open Issue OI-11.")
R("ME_SW_REQ_321",
  "The Primary shall maintain a log of events, faults and operator commands, time-stamped "
  "on the system timebase, and shall retain at least <TBD-34> of history.",
  cat="Diagnostic", app="Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_322",
  "The Primary shall maintain a per-channel event history covering at least the start, "
  "stop, step transitions, faults and operator interventions of that channel's test.",
  cat="Diagnostic", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. A single interleaved log of 64 channels' step transitions is unreadable when "
      "investigating one test.")
R("ME_SW_REQ_323",
  "It shall be possible to retrieve the Primary's logs through the Battery Manager, "
  "filtered by channel, by Secondary Board, by severity and by time range.",
  cat="Usability", app="BM + Primary", card="Per System", change="New", verif="Test")
R("ME_SW_REQ_324",
  "Logging shall never delay a control decision, a safety action or the delivery of a test "
  "record. If log storage is unavailable or full, the Primary shall continue to operate and "
  "shall report the logging failure.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="Test",
  rem="New. A blocking write to a full filesystem is a realistic way to stall the control "
      "loop of all 64 channels at once.")
R("ME_SW_REQ_325",
  "Log records shall be retained across a power failure and a software restart.",
  cat="Data", app="Primary", card="Per System", change="New", verif="Test",
  rem="Otherwise the log is unavailable in exactly the situation it is needed for.")

# ================== 22  BATTERY MANAGER APPLICATION (NEW / EXPANDED)
S(1, "22.0   Battery Manager Application                      [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_326",
  "The Battery Manager shall present a single overview of all channels of all enrolled "
  "Secondary Boards, showing for each channel at least: its Channel Address, whether it is "
  "populated, its test state, its current step, and its latest voltage and current.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Demonstration",
  rem="New. BTS had one channel and therefore no overview problem. This screen is the "
      "primary operator surface of the whole product.")
R("ME_SW_REQ_327",
  "The Battery Manager shall provide a detail view for a single channel, showing its full "
  "live parameter set, its Program with the current step marked, and its recent events.",
  cat="Usability", app="BM", card="Per Channel", change="New", verif="Demonstration")
R("ME_SW_REQ_328",
  "The Battery Manager shall provide an editor for authoring Programs, supporting all "
  "operators and actions of section 10, and shall validate a Program against the rules of "
  "section 10 before allowing it to be downloaded.",
  cat="Usability", app="BM", card="Per Program", traces="SW_REQ_278", change="Modified",
  verif="Demonstration", ref="BTS V1.7 S11",
  rem="Validation before download is what stops the operator discovering an invalid "
      "operator combination when a test fails to start on channel 47.")
R("ME_SW_REQ_329",
  "The Battery Manager shall maintain a library of authored Programs, so that a Program can "
  "be reused across channels and across sessions without being re-authored.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Demonstration")
R("ME_SW_REQ_330",
  "The Battery Manager shall show which Program is assigned to each channel, and shall warn "
  "the operator when the Program stored on the Primary for a channel differs from the one "
  "the Battery Manager believes is there.",
  cat="Safety", app="BM", card="Per Channel", change="New", verif="Demonstration",
  rem="Depends on ME_SW_REQ_241. With 64 channels this is the only practical defence "
      "against running the wrong test on a battery.")
R("ME_SW_REQ_331",
  "The Battery Manager shall allow the operator to select a group of channels and apply "
  "start, stop, pause, continue and program assignment to that group in one action, and "
  "shall show the outcome for each channel in the group.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Demonstration",
  rem="'shall show the outcome for each channel' matters - a group action where 3 of 20 "
      "channels silently refused is worse than no group action.")
R("ME_SW_REQ_332",
  "The Battery Manager shall require an explicit confirmation before any action that would "
  "stop or restart a test that is already running.",
  cat="Safety", app="BM", card="Per Channel", change="New", verif="Demonstration",
  rem="New. The cost of a mis-click is now up to 64 multi-day tests.")
R("ME_SW_REQ_333",
  "The Battery Manager shall display live trends of the measured parameters of any selected "
  "channel or set of channels.",
  cat="Usability", app="BM", card="Per Channel", change="New", verif="Demonstration")
R("ME_SW_REQ_334",
  "The Battery Manager shall store the complete test record of every test, identified by "
  "channel, session and time, and shall retain it after the test ends.",
  cat="Data", app="BM", card="Per Channel", change="New", verif="Test")
R("ME_SW_REQ_335",
  "The Battery Manager shall mark any stored test record that contains a reported data loss, "
  "a restart gap or a time correction, so that an incomplete record can never be read as a "
  "complete one.",
  cat="Data", app="BM", card="Per Channel", change="New", verif="Test",
  rem="The consumer-side half of ME_SW_REQ_285, ME_SW_REQ_31 and ME_SW_REQ_315. Reporting "
      "a gap is only useful if the report reaches the person reading the result.")
R("ME_SW_REQ_336",
  "The Battery Manager shall export a test record in a documented, machine-readable format.",
  cat="Data", app="BM", card="Per Channel", change="New", verif="Test")
R("ME_SW_REQ_337",
  "The Battery Manager shall display active alarms and faults with their scope, so that a "
  "channel fault is visibly different from a Secondary Board fault and from a system fault.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Demonstration")
R("ME_SW_REQ_338",
  "The Battery Manager shall provide the configuration and calibration workflows required "
  "by sections 8 and 9, on a per-channel basis.",
  cat="Usability", app="BM", card="Per Channel", change="New", verif="Demonstration")
R("ME_SW_REQ_339",
  "The Battery Manager shall show the state of its connection to the Primary at all times, "
  "and shall make clear when displayed data is stale.",
  cat="Usability", app="BM", card="Per System", change="New", verif="Demonstration",
  rem="A frozen display that looks live is how an operator comes to believe a finished "
      "test is still running.")
R("ME_SW_REQ_340",
  "The Battery Manager shall control access to actions that start, stop or reconfigure a "
  "test, and shall record which user performed each such action.",
  cat="Safety", app="BM", phase="Phase 2", card="Per System", change="New",
  verif="Demonstration", status="Deferred",
  rem="No source document states an access control requirement. Recorded as Phase 2 rather "
      "than omitted, because a 64-channel machine is a shared laboratory resource. See "
      "Open Issue OI-33.")

# ==================================================================== 23  POWER FAIL (NEW)
S(1, "23.0   Power Fail                                        [new section - no BTS "
      "equivalent]")
R("ME_SW_REQ_341",
  "The Primary shall detect an impending loss of input power with sufficient advance "
  "warning to complete the snapshot of ME_SW_REQ_342 for every populated channel before "
  "its own supply collapses. The minimum guaranteed warning time is <TBD-35>.",
  cat="Safety", app="Primary", card="Per System", change="New", verif="HIL Test",
  rem="New. Detection depends on the Primary Board's power-supervisory circuit, which has "
      "not been reviewed here - this requirement needs a hardware reference before it can "
      "be trusted, and needs hardware-in-the-loop testing to verify. See Open Issue "
      "OI-15.")
R("ME_SW_REQ_342",
  "On detecting an impending power loss, the Primary shall save to non-volatile memory a "
  "snapshot of every populated channel's execution state, comprising at least: the "
  "assigned Program identity, the current Step number, the elapsed time within the "
  "current Step referenced to the system timebase, the accumulated per-Step and per-Test "
  "capacity and energy counters, the debounce state of every cut-off condition active on "
  "the current Step (section 24.0), the most recently measured channel parameters, and "
  "the state of any active one-shot Registration override.",
  cat="Data", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New. The non-volatile storage medium, on-media format, and handling of a partial "
      "or corrupted write are not yet decided - see Open Issue OI-15, which this "
      "requirement now also affects. Everything listed is required so a channel can "
      "resume 'from the same point' under ME_SW_REQ_344, not merely from the same Step "
      "number.")
R("ME_SW_REQ_343",
  "On power-up, before resuming any channel, the Primary shall validate the integrity of "
  "that channel's saved snapshot (for example by checksum). A channel whose snapshot "
  "fails validation, or for which no snapshot exists, shall be treated as an unexpected "
  "restart under ME_SW_REQ_314.",
  cat="Safety", app="Primary", card="Per Channel", change="New", verif="Test")
R("ME_SW_REQ_344",
  "For a channel whose snapshot passes validation, the Primary shall automatically "
  "restore its saved state and resume execution of its Program from the saved Step and "
  "elapsed time, without requiring operator confirmation.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  rem="New, and a deliberate, client-directed exception to the no-silent-resume rule of "
      "ME_SW_REQ_314: 314 was written because resuming a test on a battery whose state is "
      "no longer known is a hazard. Here the state IS known, from ME_SW_REQ_342's "
      "snapshot, which is why the exception is scoped to a validated snapshot only. "
      "Client has asked for automatic resume now and flagged that this may be revisited "
      "after discussing the safety trade-off. See Open Issue OI-34.")
R("ME_SW_REQ_345",
  "Whenever a channel resumes automatically under ME_SW_REQ_344, the Primary shall "
  "report the resumption, the outage duration, and the Step it resumed from, to the "
  "Battery Manager and to that channel's event history (ME_SW_REQ_322).",
  cat="Diagnostic", app="BM + Primary", card="Per Channel", change="New", verif="Test",
  rem="New. An automatic action must still be visible to the operator after the fact, "
      "even though it does not wait for the operator beforehand.")

# ================================ 24  CUT-OFF CONDITION HANDLING DURING PROGRAM EXECUTION (NEW)
S(1, "24.0   Cut-off Condition Handling During Program Execution   [new section - no BTS "
      "SRS equivalent; behaviour carried over from the BTS Secondary firmware and "
      "reassigned to the Primary]")
R("ME_SW_REQ_346",
  "For each Step of a channel's Program, the Primary shall support configuring one or "
  "more cut-off conditions of the following types: Current, Voltage, Power, Charge "
  "Capacity, Discharge Capacity, Charge Energy, Discharge Energy, Temperature, Time, "
  "Accumulated Capacity, Step Capacity, Accumulated Energy, and Step Energy.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="BTS_SEC_FW_V201 Core/Inc/stepData.h",
  rem="New. None of this was ever written down in the BTS SRS - BTS V1.7 named the ERR, "
      "MSG and GOTO Step operators but never documented the cut-off condition types, "
      "comparison logics or debounce behaviour that decide when they fire. Carried over "
      "in function from the BTS Secondary firmware (cutoffConditionType_t), and reassigned "
      "here to the Primary, which now holds and executes Program steps (section 12.0) - "
      "in BTS this evaluation and action was entirely internal to the Secondary.")
R("ME_SW_REQ_347",
  "Up to <TBD-36> cut-off conditions may be configured on a single Step.",
  cat="Capacity", app="Primary", card="Per Channel", change="New", verif="Analysis",
  ref="BTS_SEC_FW_V201 Core/Inc/stepData.h",
  rem="New. Was a fixed firmware constant (NUMBER_OF_CUT_OFF_COND) in the BTS Secondary "
      "firmware; not previously an ME-stated limit.")
R("ME_SW_REQ_348",
  "Each cut-off condition shall be evaluated against a configured limit value using one "
  "of the following comparisons: greater-than, less-than, greater-than-or-equal, "
  "less-than-or-equal, not-equal, or equal-to.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="BTS_SEC_FW_V201 Core/Src/btsSecApp.c")
R("ME_SW_REQ_349",
  "A cut-off condition shall be treated as met only after its comparison has been "
  "continuously true for more than <TBD-37> consecutive evaluations, so that a transient "
  "or noise-driven excursion does not trigger the condition.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="BTS_SEC_FW_V201 Core/Src/btsSecApp.c",
  rem="New. Mirrors the limitExCount / LIMIT_EX_COUNTER debounce counter of the BTS "
      "Secondary firmware.")
R("ME_SW_REQ_350",
  "When a cut-off condition is met, the Primary shall carry out exactly one configured "
  "action for that condition: jump execution to a specified Step number (GOTO), end the "
  "Step and raise a fault with a specified error code (ERR), raise an informational "
  "message with a specified message code without ending the Step (MSG), or take no "
  "action if none is configured.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  ref="BTS_SEC_FW_V201 Core/Inc/stepData.h")
R("ME_SW_REQ_351",
  "The Primary shall evaluate every configured cut-off condition of a channel's current "
  "Step using that channel's live measurements as reported by its Secondary Board, and "
  "shall itself carry out the resulting action; the Secondary Board shall not evaluate "
  "cut-off conditions locally.",
  cat="Functional", app="Primary", card="Per Channel", change="Modified", verif="Test",
  ref="BTS_SEC_FW_V201 Core/Src/btsSecApp.c",
  rem="In BTS the Secondary evaluated and acted on cut-off conditions locally, since the "
      "Secondary held the Program. ME's Primary now holds and executes Program steps "
      "(section 12.0), so this responsibility moves with it.")
R("ME_SW_REQ_352",
  "Where more than one cut-off condition is configured on a Step, the Primary shall "
  "evaluate them in configuration order each cycle and act on the first condition found "
  "to be met in that pass.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test",
  status="Need to check",
  rem="Carries forward the fixed evaluation order of the BTS Secondary firmware. Flagged "
      "for confirmation - no client statement exists on whether a different priority "
      "between simultaneously-met conditions is required.")
R("ME_SW_REQ_353",
  "The debounce state of every active cut-off condition on a channel's current Step "
  "shall be included in the power-fail snapshot of ME_SW_REQ_342, so that a channel "
  "resumed under ME_SW_REQ_344 continues debounce counting from where it left off "
  "rather than from zero.",
  cat="Functional", app="Primary", card="Per Channel", change="New", verif="Test")
