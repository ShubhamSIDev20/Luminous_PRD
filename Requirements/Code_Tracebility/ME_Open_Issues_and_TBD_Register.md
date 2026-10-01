# ME - Open Issue and TBD Register, with the code it blocks

> GENERATED FILE - do not hand-edit.
> Regenerate with `python Requirements/Code_Tracebility/tools/build_traceability.py`.
> Register content is read live from `ME_Primary_SRS_V0.1`; the 'blocks' columns are derived from `tools/trace_status.py`.

The SRS records these as open questions. This file adds what each one actually costs: which requirements cannot be implemented until it is answered, and which work package therefore cannot start. **Every hour spent closing a row here buys back multiple engineer-days below it, and several of these rows are a single conversation.**

52 requirements are currently BLOCKED rather than merely unbuilt.

## Highest-leverage rows

Ranked by how much work they unblock, not by their own difficulty.

| Rank | Id | Question | Owner | Days unblocked | WPs |
|---:|---|---|---|---:|---|
| 1 | `OI-20` | What exactly do the DCH, INT, ERR and MSG operators and actions do? | Client | 47 | `WP-B02`, `WP-P02`, `WP-P03` |
| 2 | `TBD-14` | Which CAN port is reserved for the internal Secondary bus. | HW + SW architect | 39 | `WP-B09`, `WP-P18` |
| 3 | `OI-10` | Do user-configurable CAN ports use 11-bit identifiers, 29-bit identifiers, or both? | Client / SW architect | 39 | `WP-B09`, `WP-P18` |
| 4 | `OI-22` | Is 16-way cycle nesting required for Phase 1? | Client / Project lead | 37 | `WP-B02`, `WP-P02` |
| 5 | `OI-17` | What does the SET operator do? | Client | 37 | `WP-B02`, `WP-P02` |
| 6 | `OI-16` | What does the REG operator do, and what are the permitted Registration values? | Client | 37 | `WP-B02`, `WP-P02` |
| 7 | `OI-05` | Is there an emergency stop, and what is the defined safe state of a channel mid-charge and mid-discharge? | Client / Safety | 29 | `WP-P09`, `WP-P10`, `WP-P15`, `WP-P22`, `WP-P31` |
| 8 | `OI-07` | How are 64 channels exposed through a MODBUS register map? | Client / SW architect | 27 | `WP-B09`, `WP-P16` |
| 9 | `OI-11` | What is the catalogue of error codes, and how is each reported? | SW architect | 22 | `WP-B05`, `WP-B14`, `WP-P10`, `WP-P11` |
| 10 | `OI-09` | Which CAN port carries the internal bus, and is it physically distinct from the user-configurable ports? | HW architect | 21 | `WP-P18` |
| 11 | `OI-21` | When an STO operator carries surplus parameters, is an error raised, or are they ignored, or both? | Client | 20 | `WP-B02` |
| 12 | `OI-18` | Should the misspelled error identifiers of BTS V1.7 be corrected in ME? | Requirements owner | 20 | `WP-B02` |

> Note how the ranking falls out. `OI-11` (the error-code catalogue) and `OI-05` (what a channel's safe state is) are each one decision that gates several packages, and neither needs any engineering to answer - they need somebody to write the answer down. `OI-31`/`TBD-28` (the control decision interval) is the opposite: it gates the least work by day count but it is the one that can invalidate the architecture, because it decides whether a containerised Linux A53 can host a 64-channel control loop at all. Answer it early even though it unblocks fewer days.

## Full register

| Id | Kind | Area | Question | Affects | Owner | Sev | Blocks these WPs | Days |
|---|---|---|---|---|---|---|---|---:|
| `TBD-01` | TBD | Digital I/O | Required time-stamp resolution for a digital input transition. | ME_SW_REQ_5 | SW architect | Medium | `WP-P14` | 5 |
| `TBD-02` | TBD | Digital I/O | Time within which Primary digital outputs must reach their safe state on link loss or fault. | ME_SW_REQ_21 | Client / Safety | Medium | `WP-P15` | 5 |
| `TBD-03` | TBD | Safety | Switch-over dead time between charging and discharging on one channel. BTS used 500 ms on the Secondary; whether the Primary must add margin for bus latency is unknown. | ME_SW_REQ_23 | HW + SW architect | Medium | `WP-P28` | 4 |
| `TBD-04` | TBD | Timebase | Maximum permitted time offset between the Primary and any Secondary Board. | ME_SW_REQ_30 | SW architect | Medium | `WP-P13` | 7 |
| `TBD-05` | TBD | Modbus | MODBUS register map layout for up to 64 channels on RS485_1. | ME_SW_REQ_36 | Client / SW architect | Medium | `WP-B10`, `WP-P16` | 14 |
| `TBD-06` | TBD | Modbus | Permitted data-bit, stop-bit and parity combinations on RS485_2. BTS wrote 'as per configuration XXX'. | ME_SW_REQ_41 | Client | Medium | `WP-P17` | 9 |
| `TBD-07` | TBD | Modbus | MODBUS function codes to be supported on RS485_2. BTS wrote 'function codes XXX'. | ME_SW_REQ_43 | Client | Medium | `WP-P17` | 9 |
| `TBD-08` | TBD | Modbus | MODBUS RTU response timeout on RS485_2. BTS wrote 'XXX ms'. | ME_SW_REQ_44 | Client | Medium | `WP-P17` | 9 |
| `TBD-09` | TBD | Modbus | Maximum number of devices on the RS485_2 bus. BTS wrote 'up to XXX devices'. | ME_SW_REQ_45 | Client | Medium | `WP-P17` | 9 |
| `TBD-10` | TBD | Modbus | Lowest configurable RS485_2 slave address. BTS wrote 'XXX'. | ME_SW_REQ_46 | Client | Medium | `WP-P17` | 9 |
| `TBD-11` | TBD | Modbus | Highest configurable RS485_2 slave address. BTS wrote 'XXX'. | ME_SW_REQ_46 | Client | Medium | `WP-B10`, `WP-P17` | 14 |
| `TBD-12` | TBD | Host link | Number of concurrent Battery Manager sessions permitted. | ME_SW_REQ_59 | Client / SW architect | Medium | `WP-P23` | 7 |
| `TBD-13` | TBD | Host link | Minimum test record buffering time while the Battery Manager link is down. | ME_SW_REQ_61 | Client | Medium | `WP-P23` | 7 |
| `TBD-14` | TBD | CAN | Which CAN port is reserved for the internal Secondary bus. | ME_SW_REQ_65 | HW + SW architect | Medium | `WP-B09`, `WP-P18` | 39 |
| `TBD-15` | TBD | Safety | Voltage threshold that counts as 'near 0 V' for Battery Open detection. BTS wrote only 'near 0V'. | ME_SW_REQ_143 | Client | Medium | `WP-B07`, `WP-P19` | 18 |
| `TBD-16` | TBD | Calibration | Battery voltage calibration procedure, number of points and acceptance criteria. BTS section 8.1.5 was empty. | ME_SW_REQ_145 | Client | Medium | `WP-B08`, `WP-P20` | 16 |
| `TBD-17` | TBD | Capacity | Maximum Program size per channel, and therefore total Program storage for 64 channels. | ME_SW_REQ_237 | SW architect | Medium | `WP-P21` | 6 |
| `TBD-18` | TBD | Addressing | Agreed valid range of the Channel Address. Two ME documents disagree - the nibble encoding admits 15x15, storage and validation cap at 8x8. | ME_SW_REQ_247 | SW architect | Medium | `WP-P27` | 9 |
| `TBD-19` | TBD | Supervision | Time after which a silent Secondary Board is declared lost. | ME_SW_REQ_255 | SW architect | Medium | `WP-P01` | 9 |
| `TBD-20` | TBD | CAN | Internal CAN bus arbitration bit rate. | ME_SW_REQ_263 | HW + SW architect | High | `WP-P25` | 8 |
| `TBD-21` | TBD | CAN | Internal CAN bus data bit rate. | ME_SW_REQ_263 | HW + SW architect | High | `WP-P25` | 8 |
| `TBD-22` | TBD | CAN | Maximum permitted worst-case internal CAN bus load, as a percentage. | ME_SW_REQ_264 | SW architect | High | `WP-B13`, `WP-P25` | 13 |
| `TBD-23` | TBD | Performance | Worst-case time from a Primary setpoint decision to its delivery at the Secondary. | ME_SW_REQ_266 | SW architect | Medium | - |  |
| `TBD-24` | TBD | Safety | Time after which a channel's measurement data is declared stale. | ME_SW_REQ_267 | SW architect | Medium | `WP-P25` | 8 |
| `TBD-25` | TBD | Host link | Byte order of the CRC and of multi-byte fields on the host link. Two ME documents disagree. | ME_SW_REQ_277 | SW architect + Web App team | High | `WP-B17` | 5 |
| `TBD-26` | TBD | Data | Permitted registration types and the permitted registration interval range. | ME_SW_REQ_286 | Client | Medium | `WP-B06`, `WP-P05` | 16 |
| `TBD-27` | TBD | Control | Behaviour of a Start command sent to a channel that has already completed its test. | ME_SW_REQ_295 | Client | Medium | `WP-P22` | 8 |
| `TBD-28` | TBD | Performance | Control decision interval per channel. This is the single most architecturally significant unknown in this document. | ME_SW_REQ_305 | Client + SW architect | High | `WP-P26` | 9 |
| `TBD-29` | TBD | Performance | Permitted jitter on the control decision interval. | ME_SW_REQ_306 | SW architect | Medium | `WP-P26` | 9 |
| `TBD-30` | TBD | Performance | Maximum aggregate live data rate for all channels. | ME_SW_REQ_307 | SW architect | Medium | `WP-B13`, `WP-P26` | 14 |
| `TBD-31` | TBD | Performance | Minimum processor headroom required under worst case. | ME_SW_REQ_308 | SW architect | Medium | `WP-P26` | 9 |
| `TBD-32` | TBD | Performance | Memory budget available to the Primary software on the target platform. | ME_SW_REQ_309 | SW architect | Medium | `WP-P26` | 9 |
| `TBD-33` | TBD | Startup | Time from power-on to ready-to-accept-commands. | ME_SW_REQ_319 | Client | Medium | `WP-P09` | 5 |
| `TBD-34` | TBD | Diagnostics | Minimum log history to be retained. | ME_SW_REQ_321 | Client | Medium | `WP-B12`, `WP-P12` | 10 |
| `TBD-35` | TBD | Power Fail | Minimum guaranteed warning time between power-fail detection and supply collapse, within which the Primary must complete its snapshot save for every populated channel. Depends on the Primary Board's power-supervisory circuit. | ME_SW_REQ_341 | HW architect | Medium | `WP-P08` | 8 |
| `TBD-36` | TBD | Programming | Maximum number of cut-off conditions configurable on a single Step. Was a fixed compile-time constant in the BTS Secondary firmware. | ME_SW_REQ_347 | SW architect | Medium | `WP-P04` | 7 |
| `TBD-37` | TBD | Programming | Number of consecutive evaluations a cut-off condition must remain true before it is treated as met. | ME_SW_REQ_349 | SW architect | Medium | `WP-P04` | 7 |
| `OI-01` | Open Issue | Scope | Is the host application a PC/desktop application or a browser-based web application? | Section 22, ME_SW_REQ_271 | Client | High | - |  |
| `OI-02` | Open Issue | Scope | Is 'Battery Manager' the agreed product name for the host application in ME? | Whole document | Client / Project lead | High | - |  |
| `OI-03` | Open Issue | Scope | Is the ME Primary a single-core Linux application processor, or does it have a separate real-time core? | ME_SW_REQ_305, ME_SW_REQ_306, ME_SW_REQ_311 | SW architect | High | `WP-P26` | 9 |
| `OI-04` | Open Issue | Requirements | BTS V1.7 uses the requirement number SW_REQ_19 twice - once in section 3.0 Relays and once in section 4.0 RTC. | Sheet 4 | Requirements owner | Low | - |  |
| `OI-05` | Open Issue | Safety | Is there an emergency stop, and what is the defined safe state of a channel mid-charge and mid-discharge? | ME_SW_REQ_21, ME_SW_REQ_294, ME_SW_REQ_296, ME_SW_REQ_300 | Client / Safety | High | `WP-P09`, `WP-P10`, `WP-P15`, `WP-P22`, `WP-P31` | 29 |
| `OI-06` | Open Issue | Safety | Does the Primary sequence the charge/discharge change-over, or does it issue a mode command and rely on the Secondary to sequence it safely? | ME_SW_REQ_23 | HW + SW architect | High | `WP-P28` | 4 |
| `OI-07` | Open Issue | Modbus | How are 64 channels exposed through a MODBUS register map? | ME_SW_REQ_36, ME_SW_REQ_37 | Client / SW architect | Medium | `WP-B09`, `WP-P16` | 27 |
| `OI-08` | Open Issue | Host link | May more than one operator be connected to one Primary at the same time? | ME_SW_REQ_59 | Client | Medium | `WP-P23` | 7 |
| `OI-09` | Open Issue | CAN | Which CAN port carries the internal bus, and is it physically distinct from the user-configurable ports? | ME_SW_REQ_65, ME_SW_REQ_270 | HW architect | High | `WP-P18` | 21 |
| `OI-10` | Open Issue | CAN | Do user-configurable CAN ports use 11-bit identifiers, 29-bit identifiers, or both? | ME_SW_REQ_67, ME_SW_REQ_91, ME_SW_REQ_95 | Client / SW architect | Medium | `WP-B09`, `WP-P18` | 39 |
| `OI-11` | Open Issue | Diagnostics | What is the catalogue of error codes, and how is each reported? | ME_SW_REQ_82, ME_SW_REQ_320 | SW architect | High | `WP-B05`, `WP-B14`, `WP-P10`, `WP-P11` | 22 |
| `OI-12` | Open Issue | Capacity | What sampling and registration rates are simultaneously achievable across 64 channels? | ME_SW_REQ_140, ME_SW_REQ_264, ME_SW_REQ_307 | SW architect | High | `WP-B13` | 5 |
| `OI-13` | Open Issue | Calibration | What is the battery voltage calibration procedure? | ME_SW_REQ_145 | Client | Medium | `WP-B08`, `WP-P20` | 16 |
| `OI-14` | Open Issue | Calibration | How is calibration data bound to a Secondary Board so a board swap cannot apply the wrong constants? | ME_SW_REQ_162 | SW architect | High | `WP-B08`, `WP-P20` | 16 |
| `OI-15` | Open Issue | Persistence | What is persisted to non-volatile storage, in what format, and how is a partial write handled? | ME_SW_REQ_163, ME_SW_REQ_313, ME_SW_REQ_325, ME_SW_REQ_342 | SW architect | High | `WP-P07`, `WP-P08` | 15 |
| `OI-16` | Open Issue | Programming | What does the REG operator do, and what are the permitted Registration values? | ME_SW_REQ_174, ME_SW_REQ_286 | Client | High | `WP-B02`, `WP-P02` | 37 |
| `OI-17` | Open Issue | Programming | What does the SET operator do? | ME_SW_REQ_177 | Client | High | `WP-B02`, `WP-P02` | 37 |
| `OI-18` | Open Issue | Programming | Should the misspelled error identifiers of BTS V1.7 be corrected in ME? | ME_SW_REQ_193 | Requirements owner | Low | `WP-B02` | 20 |
| `OI-19` | Open Issue | Terminology | The word 'registration' is used for three different things across the source documents. | Sheet 2, ME_SW_REQ_275 | Requirements owner | Medium | - |  |
| `OI-20` | Open Issue | Programming | What exactly do the DCH, INT, ERR and MSG operators and actions do? | ME_SW_REQ_199, ME_SW_REQ_200, ME_SW_REQ_230, ME_SW_REQ_231 | Client | High | `WP-B02`, `WP-P02`, `WP-P03` | 47 |
| `OI-21` | Open Issue | Programming | When an STO operator carries surplus parameters, is an error raised, or are they ignored, or both? | ME_SW_REQ_206 | Client | Medium | `WP-B02` | 20 |
| `OI-22` | Open Issue | Programming | Is 16-way cycle nesting required for Phase 1? | ME_SW_REQ_216, ME_SW_REQ_217 | Client / Project lead | Medium | `WP-B02`, `WP-P02` | 37 |
| `OI-23` | Open Issue | Capacity | What is the largest Program that must be supported per channel? | ME_SW_REQ_237, ME_SW_REQ_309 | Client / SW architect | Medium | `WP-P21` | 6 |
| `OI-24` | Open Issue | Addressing | Is the Channel Address range 8x8 or 15x15? | ME_SW_REQ_247 | SW architect | Medium | `WP-P27` | 9 |
| `OI-25` | Open Issue | Supervision | Is hot-swapping a Secondary Board a supported field operation? | ME_SW_REQ_257, ME_SW_REQ_258 | Client | Medium | `WP-P01` | 9 |
| `OI-26` | Open Issue | Supervision | How and when are channels beyond the first enrolled? | ME_SW_REQ_251, ME_SW_REQ_260 | SW architect | High | `WP-B15`, `WP-P01` | 13 |
| `OI-27` | Open Issue | CAN | Is the internal bus Classic CAN or CAN FD, and at what bit rates? | ME_SW_REQ_262, ME_SW_REQ_263, ME_SW_REQ_264 | HW + SW architect | High | `WP-P25` | 8 |
| `OI-28` | Open Issue | Host link | Is the host link CRC big-endian or little-endian on the wire? | ME_SW_REQ_277 | SW architect + Web App team | High | `WP-B17` | 5 |
| `OI-29` | Open Issue | Control | Does every host command carry a meaningful per-channel address, or are some commands device-scoped? | ME_SW_REQ_291 | SW architect + Web App team | Medium | `WP-P22` | 8 |
| `OI-30` | Open Issue | Control | Should a Start command re-run a completed test, or be refused until the Program is re-sent? | ME_SW_REQ_295 | Client | Medium | `WP-P22` | 8 |
| `OI-31` | Open Issue | Performance | How often must the Primary re-evaluate the control decision of each channel? | ME_SW_REQ_305 | Client + SW architect | High | `WP-P26` | 9 |
| `OI-32` | Open Issue | Performance | What is the memory budget on the target platform? | ME_SW_REQ_309 | SW architect | Medium | - |  |
| `OI-33` | Open Issue | Security | Is user authentication and action logging required? | ME_SW_REQ_340 | Client | Medium | `WP-B16` | 6 |
| `OI-34` | Open Issue | Safety | Is fully-automatic resume without operator confirmation after a power fail acceptable from a safety standpoint, given a battery's state may have drifted during the outage? | ME_SW_REQ_314, ME_SW_REQ_344, ME_SW_REQ_345 | Client | High | `WP-P08` | 8 |

## Requirements held up, by register entry

- **`OI-03`** blocks 1 requirement(s): `ME_SW_REQ_311`
- **`OI-05`** blocks 5 requirement(s): `ME_SW_REQ_21`, `ME_SW_REQ_294`, `ME_SW_REQ_296`, `ME_SW_REQ_300`, `ME_SW_REQ_303`
- **`OI-06`** blocks 1 requirement(s): `ME_SW_REQ_23`
- **`OI-07`** blocks 1 requirement(s): `ME_SW_REQ_36`
- **`OI-08`** blocks 1 requirement(s): `ME_SW_REQ_59`
- **`OI-09`** blocks 1 requirement(s): `ME_SW_REQ_65`
- **`OI-11`** blocks 3 requirement(s): `ME_SW_REQ_297`, `ME_SW_REQ_302`, `ME_SW_REQ_320`
- **`OI-12`** blocks 2 requirement(s): `ME_SW_REQ_140`, `ME_SW_REQ_264`
- **`OI-13`** blocks 1 requirement(s): `ME_SW_REQ_145`
- **`OI-14`** blocks 1 requirement(s): `ME_SW_REQ_162`
- **`OI-15`** blocks 4 requirement(s): `ME_SW_REQ_163`, `ME_SW_REQ_313`, `ME_SW_REQ_342`, `ME_SW_REQ_353`
- **`OI-17`** blocks 1 requirement(s): `ME_SW_REQ_177`
- **`OI-22`** blocks 2 requirement(s): `ME_SW_REQ_216`, `ME_SW_REQ_217`
- **`OI-24`** blocks 1 requirement(s): `ME_SW_REQ_247`
- **`OI-25`** blocks 1 requirement(s): `ME_SW_REQ_257`
- **`OI-26`** blocks 2 requirement(s): `ME_SW_REQ_251`, `ME_SW_REQ_260`
- **`OI-28`** blocks 1 requirement(s): `ME_SW_REQ_277`
- **`OI-29`** blocks 1 requirement(s): `ME_SW_REQ_291`
- **`OI-30`** blocks 1 requirement(s): `ME_SW_REQ_295`
- **`OI-31`** blocks 1 requirement(s): `ME_SW_REQ_305`
- **`OI-34`** blocks 1 requirement(s): `ME_SW_REQ_344`
- **`TBD-01`** blocks 1 requirement(s): `ME_SW_REQ_5`
- **`TBD-02`** blocks 1 requirement(s): `ME_SW_REQ_21`
- **`TBD-03`** blocks 1 requirement(s): `ME_SW_REQ_23`
- **`TBD-04`** blocks 1 requirement(s): `ME_SW_REQ_30`
- **`TBD-05`** blocks 1 requirement(s): `ME_SW_REQ_36`
- **`TBD-06`** blocks 1 requirement(s): `ME_SW_REQ_41`
- **`TBD-07`** blocks 1 requirement(s): `ME_SW_REQ_43`
- **`TBD-08`** blocks 1 requirement(s): `ME_SW_REQ_44`
- **`TBD-09`** blocks 1 requirement(s): `ME_SW_REQ_45`
- **`TBD-10`** blocks 1 requirement(s): `ME_SW_REQ_46`
- **`TBD-11`** blocks 1 requirement(s): `ME_SW_REQ_46`
- **`TBD-12`** blocks 1 requirement(s): `ME_SW_REQ_59`
- **`TBD-13`** blocks 1 requirement(s): `ME_SW_REQ_61`
- **`TBD-14`** blocks 1 requirement(s): `ME_SW_REQ_65`
- **`TBD-15`** blocks 1 requirement(s): `ME_SW_REQ_143`
- **`TBD-16`** blocks 1 requirement(s): `ME_SW_REQ_145`
- **`TBD-17`** blocks 1 requirement(s): `ME_SW_REQ_237`
- **`TBD-18`** blocks 1 requirement(s): `ME_SW_REQ_247`
- **`TBD-19`** blocks 1 requirement(s): `ME_SW_REQ_255`
- **`TBD-20`** blocks 1 requirement(s): `ME_SW_REQ_263`
- **`TBD-21`** blocks 1 requirement(s): `ME_SW_REQ_263`
- **`TBD-22`** blocks 2 requirement(s): `ME_SW_REQ_140`, `ME_SW_REQ_264`
- **`TBD-23`** blocks 1 requirement(s): `ME_SW_REQ_266`
- **`TBD-24`** blocks 1 requirement(s): `ME_SW_REQ_267`
- **`TBD-25`** blocks 1 requirement(s): `ME_SW_REQ_277`
- **`TBD-26`** blocks 1 requirement(s): `ME_SW_REQ_286`
- **`TBD-27`** blocks 1 requirement(s): `ME_SW_REQ_295`
- **`TBD-28`** blocks 1 requirement(s): `ME_SW_REQ_305`
- **`TBD-29`** blocks 1 requirement(s): `ME_SW_REQ_306`
- **`TBD-30`** blocks 1 requirement(s): `ME_SW_REQ_307`
- **`TBD-31`** blocks 1 requirement(s): `ME_SW_REQ_308`
- **`TBD-32`** blocks 1 requirement(s): `ME_SW_REQ_309`
- **`TBD-33`** blocks 1 requirement(s): `ME_SW_REQ_319`
- **`TBD-34`** blocks 1 requirement(s): `ME_SW_REQ_321`
- **`TBD-35`** blocks 2 requirement(s): `ME_SW_REQ_341`, `ME_SW_REQ_353`
- **`TBD-36`** blocks 1 requirement(s): `ME_SW_REQ_347`
- **`TBD-37`** blocks 2 requirement(s): `ME_SW_REQ_349`, `ME_SW_REQ_353`

