# ME Project — Peripheral Requirements for the Primary and Secondary Boards

| | |
|---|---|
| **Document ID** | BTS-ME-PER-001 |
| **Version** | 3.0 |
| **Date** | 2026-07-31 |
| **Audience** | Engineering and Management |
| **Status** | For Review — input to hardware design |
| **Prepared by** | Firmware Engineering |

---

## Purpose

This document lists the peripherals each board must provide for the ME platform, with quantities, purposes and specifications.

Digital input, digital output, analog input and analog output counts were read out of the existing firmware, so the numbers reflect real signal usage. Port pins and connector designators are given where they exist.

---

# 1. Requirement Summary

## 1.1 Primary board

| # | Peripheral | Qty | Purpose |
|---|---|---:|---|
| P1 | Ethernet 10/100 | 1 | Communicate with the web application over TCP/IP and UDP |
| P2 | CAN FD — test bus | 1 | Communicate with up to eight Secondary boards |
| P3 | CAN — BMS | 1 | Communicate with the BMS |
| P4 | CAN — external sensor bus | 1 | Read external sensor data |
| P5 | RS-485 | 2 | Modbus communication |
| P6 | Digital inputs | 4 | Interlocks and status contacts |
| P7 | Digital outputs | 3 | Contactor and indicator control |
| P8 | Real-time clock with backup cell | 1 | Keep time across a power loss |
| P9 | Non-volatile memory | 1 | Store eight test sequences and all parameters |
| P10 | Watchdog | 1 | Reset the board if the firmware stops servicing it |

**Communication ports on the Primary: 1 Ethernet, 3 CAN, 2 RS-485.**

## 1.2 Secondary board

| # | Peripheral | Qty | Purpose |
|---|---|---:|---|
| S1 | CAN FD | 1 | Communicate with the Primary |
| S2 | Digital inputs | 4 | Interlocks and status contacts, interrupt-capable |
| S3 | Digital outputs | 8 | Contactor, fan and indicator control |
| S4 | High-resolution ADC | 4 channels | Battery voltage, reverse-polarity voltage, current on two ranges |
| S5 | General-purpose ADC | 4 channels | ZnT, LnT, battery temperature, heatsink temperature |
| S6 | DAC | 1 | Control the transistor bank output |
| S7 | Current-direction select output | 1 | Select the charge or discharge current path |
| S8 | Watchdog | 1 | Reset the board if the firmware stops servicing it |
| S9 | Configuration storage | 1 | Hold calibration constants and settings |

**Analog channels on the Secondary: 8 inputs (4 high-resolution + 4 general-purpose) and 1 output.**

---

# 2. Primary Board

## 2.1 Ethernet — P1 · 1 port

One 10/100 Ethernet port carrying both directions of the PC link.

| Direction | Protocol | Carries |
|---|---|---|
| PC → Primary | TCP/IP | Test sequence download, commands, configuration |
| Primary → PC | UDP | Live measurements and session records |

Requires an external Ethernet PHY and a magnetics-isolated connector.

## 2.2 CAN — P2, P3, P4 · 3 ports

Three independent CAN ports, each serving a separate network.

### P2 · Test bus

| Parameter | Requirement |
|---|---|
| Type | CAN FD |
| Nodes on the bus | Up to 8 Secondaries plus the Primary |
| Arbitration bit rate | 1 Mbit/s |
| Data-phase bit rate | 2 Mbit/s |
| Payload | Up to 64 bytes |
| Target bus utilisation | Below 70 % |

Requires a CAN FD transceiver, bus termination at both physical ends of the cable run, and a connector arrangement allowing eight nodes to be daisy-chained.

### P3 · BMS bus

Connects to the BMS network, interpreted through the CAN database uploaded from the web application. A separate physical port from the test bus.

### P4 · External sensor bus

Reads data from external sensors mounted around the test bench. A separate physical port from the test bus and the BMS bus.

## 2.3 RS-485 — P5 · 2 ports

Two RS-485 ports for Modbus communication. Each port requires its own transceiver, direction control, fail-safe biasing and termination.

## 2.4 Digital inputs — P6 · 4 inputs

Four digital inputs, all **active low**.

| Input | Port pin | Connector |
|---|---|---|
| Digital input 1 | PC2 | J9 |
| Digital input 2 | PC3 | J10 |
| Digital input 3 | PC6 | J8 |
| Digital input 4 | PC7 | J11 |

The four states are packed into the lower half of one status byte and reported to the web application together with the Secondary's input states.

## 2.5 Digital outputs — P7 · 3 outputs

Three digital outputs, each driven from one bit of a control byte set by the web application.

| Output | Port pin | Connector |
|---|---|---|
| Digital output 1 | PC17 | J10 |
| Digital output 2 | PC31 | J11 |
| Digital output 3 | PC18 | J12 |

## 2.6 Real-time clock — P8 · 1

A real-time clock with a battery or supercapacitor backup, so the board keeps correct time across a power loss without depending on the PC.

## 2.7 Non-volatile memory — P9 · 1

Non-volatile memory holding:

| Contents |
|---|
| Eight test sequences, in indexed slots selectable from the web application |
| Battery and channel configuration, per channel |
| Calibration constants |
| Manufacturing and commissioning data |
| Power-fail recovery data — session state, cycle counts, step position |

## 2.8 Watchdog — P10 · 1

An independent watchdog that resets the board if the firmware stops servicing it.

---

# 3. Secondary Board

## 3.1 CAN FD — S1 · 1 port

The Secondary's link to the Primary.

| Parameter | Requirement |
|---|---|
| Type | CAN FD |
| Arbitration bit rate | 1 Mbit/s |
| Data-phase bit rate | 2 Mbit/s |
| Payload | Up to 64 bytes |
| Node address | Unique per board, 1 to 8 |

Requires a CAN FD transceiver, termination on the two boards at the physical ends of the cable run, and a means of setting the node address.

## 3.2 Digital inputs — S2 · 4 inputs

Four digital inputs, all interrupt-capable.

## 3.3 Digital outputs — S3 · 8 outputs

Eight digital outputs for contactor, fan and indicator control. All eight default to the off state at reset and at power-up.

## 3.4 High-resolution ADC — S4 · 4 channels

| Channel | Measures |
|---|---|
| 1 | Battery voltage |
| 2 | Reverse-polarity voltage |
| 3 | Battery current — main range |
| 4 | Battery current — second, scaled range |

| Parameter | Requirement |
|---|---|
| Resolution | 24-bit |
| Sample rate | Up to 30 kSPS |
| Interface | Dedicated SPI |

Channels 3 and 4 are two different current ranges. The board switches range to hold resolution across a wide current span.

Each channel is filtered by a moving average in firmware and corrected by two-point calibration constants.

## 3.5 General-purpose ADC — S5 · 4 channels

| Channel | Port pin | Measures |
|---|---|---|
| 1 | PC4 | ZnT signal voltage |
| 2 | PA0 | Battery temperature — PT100 RTD |
| 3 | PA6 | LnT signal voltage |
| 4 | to be assigned | Heatsink temperature |

| Parameter | Requirement |
|---|---|
| Resolution | 16-bit |
| Reading method | Continuous, DMA-driven |

Sensor-presence detection on the temperature channels: readings outside a valid window are reported as "sensor not connected".

## 3.6 DAC — S6 · 1

Sets the transistor bank drive level. The 1 ms control loop writes to it on every cycle.

| Parameter | Requirement |
|---|---|
| Resolution | 20-bit |
| Interface | Dedicated SPI |
| Control lines | Separate chip-select and load-DAC lines |
| Zero code | Corresponds to zero drive |

## 3.7 Current-direction select — S7 · 1 output

A dedicated output selecting the charge or discharge current path. Defaults to a safe state at reset.

## 3.8 Watchdog — S8 · 1

An independent watchdog that resets the board if the firmware stops servicing it. Active in every operating mode, including calibration.

## 3.9 Configuration storage — S9 · 1

Non-volatile storage for calibration constants and board settings. Capacity is small — the test sequence is held on the Primary. Storage within the processor's own flash is sufficient.

---

*End of document.*
