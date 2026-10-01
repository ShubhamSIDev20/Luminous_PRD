# 14. Calibration Flow

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Calibration is its own command family (`0xA0`) with its own live-data path. It is a
guided, multi-step operator procedure — not a single command.

### 14.1 The procedure

```
  OPERATOR: Devices -> channel -> Calibration
        |
        v
  +--------------------------------------------------------------+
  | STEP 1 -- HWReadyToCalibrationAsync()                         |
  |   0xA0 / Query 0x01 (IsReady)                                 |
  |   FAIL -> cannot proceed                                      |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 2 -- SendLiveCurrentandVoltage()                         |
  |   0xA0 / Query 0x02 (SendLive)                                |
  |                                                               |
  |   Tells the channel to START STREAMING calibration telemetry  |
  |   on UDP 10000 with start byte 0xA0. Those packets land in    |
  |   ViewUdpData's Calibration branch and fill a rolling 4-entry |
  |   buffer (calibration.calibrationBuffer) that the UI reads    |
  |   to show the operator the live measured value.               |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 3 -- CalibrationPointPreset(queryId, value, range?)      |
  |                                                               |
  |   Repeated once per point. Operator applies a known reference |
  |   (a calibrated load / meter) and enters the true value.      |
  |                                                               |
  |   0xA0 / Query = the point's ID / optional Range byte         |
  |   Data = GetFloatBytes(value)     (float32 BE)                |
  |                                                               |
  |   Point IDs come in Low/High pairs per quantity:              |
  |     Current  Charge     0x03 Low   0x04 High                  |
  |     Current  Discharge  0x06 Low   0x07 High                  |
  |     Voltage  Charge     0x09 Low   0x0A High                  |
  |     Voltage  Discharge  0x0C Low   0x0D High                  |
  |     Temperature         0x0F Low   0x10 High                  |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 4 -- SetGainOffset(queryId, gain, offset, range?)        |
  |                                                               |
  |   Data = [Gain 4B float BE][Offset 4B float BE][Epoch 4B]     |
  |                                                               |
  |   Gain/Offset IDs:                                            |
  |     CurrentChargeGainOffset      0x05                         |
  |     CurrentDisChargeGainOffset   0x08                         |
  |     VoltageChargeGainOffset      0x0B                         |
  |     VoltageDisChargeGainOffset   0x0E                         |
  |     TemperatureGainOffset        0x11                         |
  |                                                               |
  |   BEFORE sending, the in-memory snapshot is updated:          |
  |                                                               |
  |     Current Charge / Discharge -> PER-RANGE lists             |
  |         CalibrationDto.UpsertRangePoint(                      |
  |             calibration.CurrentCharge,                        |
  |             new CalibrationDataPointDto {                     |
  |                 Range = range ?? CalibrationRange.Full_Range, |
  |                 Gain, Offset, DateTime,                       |
  |                 Mode = Charge|Discharge,                      |
  |                 Type = Current })                             |
  |                                                               |
  |     Voltage Charge / Discharge -> SINGLE point each           |
  |         calibration.VoltageCharge.Gain/Offset/DateTime = ..   |
  |                                                               |
  |     Temperature -> SINGLE point                               |
  |         calibration.TemperaturePoint.Gain/Offset/DateTime = ..|
  |                                                               |
  |   NOTE the asymmetry: current is calibrated per RANGE         |
  |   (Full_Range, Range1..Range4); voltage and temperature are   |
  |   single-point only. The 163-byte read-back record has the    |
  |   same shape -- 4 extra 24-byte range blocks for current.     |
  +--------------------------------+------------------------------+
                                   |
                +------------------+------------------+
                |                  |                  |
                v                  v                  v
  +--------------------+ +------------------+ +---------------------+
  | STEP 5a -- VERIFY  | | STEP 5b -- STOP  | | STEP 5c -- CANCEL   |
  |                    | |                  | |                     |
  | StartChargeVerify  | | StopCalibration  | | CancelCalibration   |
  |   Current   0x14   | |   0xA0 / 0x13    | |   0xA0 / 0x12       |
  | StartDisCharge     | |                  | |                     |
  |   Verify    0x15   | | GUARD:           | | abort, discard      |
  | StopVerify  0x16   | |  IsCalibration   | |                     |
  |   (StopverifyCali- | |  InPrcoess==false| |                     |
  |    bration())      | |  && IsVerifying  | |                     |
  |                    | |  ==false ->      | |                     |
  |                    | |  "No calibration | |                     |
  |                    | |   or verification| |                     |
  |                    | |   in process to  | |                     |
  |                    | |   stop."         | |                     |
  +--------------------+ +------------------+ +---------------------+
                |
                v
  +--------------------------------------------------------------+
  | STEP 6 -- PreviousCalibration()                               |
  |   0xA0 / Query 0x17                                           |
  |                                                               |
  |   Returns the full 163-byte CalibrationData record:           |
  |     bytes   0-4    header + error status                      |
  |     bytes   5-52   RangeFull: current chg/dischg, voltage     |
  |                    chg/dischg -- each Gain/Offset/DateTime    |
  |     bytes  53-76   Range1 (current only)                      |
  |     bytes  77-100  Range2 (current only)                      |
  |     bytes 101-124  Range3 (current only)                      |
  |     bytes 125-148  Range4 (current only)                      |
  |     bytes 149-160  Temperature Gain/Offset/DateTime           |
  |     bytes 161-162  CRC-16                                     |
  |                                                               |
  |   THIS is the 163-byte response that forces the shared read   |
  |   buffer in HandleCommandClientAsync to be 1024 bytes         |
  |   (Chapter 4, Invariant 1).                                   |
  +--------------------------------------------------------------+
```

### 14.2 Calibration's two data paths

```
   COMMANDS                            LIVE VALUES
   (TCP 9999, request/response)        (UDP 10000, one-way stream)
        |                                        |
        v                                        v
  SendAndWaitForResponseAsync            ViewUdpData
        |                                   case StartByte.Calibration:
        v                                        |
  DeviceConnection                               v
        |                                  ParseRealTimeData(payload)
        v                                        |
  0xA0 frames                                    v
                                          calibrationBuffer:
                                            if (Count > 3) RemoveAt(0)
                                            Add(rec.RealTimeRecord)
                                            (rolling window of 4)
                                                 |
                                                 v
                                          RealTime.NotifyDataChanged(...)
                                                 |
                                                 v
                                          operator sees the live reading
                                          while applying the reference
```

**Files involved:** `Services/Implementations/ChannelCommandHandler.cs:871-1108`,
`Services/ChannelManager.cs:437-447`, `Models/DTOs/CalibrationDto`,
`Components/UI/Calibration/`, `docs/PROTOCOL.md` §9

---


---

[⬅ Previous](13-code-call-sequences.md) · [⬅ Index](README.md) · [Next ➡](15-broadcast-discovery.md)
