namespace BatteryTestingSystem.Models.Enums;
public enum StartByte : byte
{
    LiveData = 0xCC,
    Registration = 0xDD,   // Registration / Delete commands
    Configuration = 0xAA,  // Hardware / Configuration commands
    Program = 0xBB,        // Program data commands
    Control = 0xEE,        // Program control commands (Start, Stop, Pause, Continue)
    Calibration = 0xA0     // Calibration commands
}

public enum ConfigurationQuery : byte
{
    HWReady = 0x01,             
    ReadManufacturingConfig = 0x03,  
    ReadFactoryConfig = 0x02,  
    ReadBatteryParams = 0x04,   
    WriteBatteryParams = 0x05,  
    SyncTime = 0x06             // "Sync Time"
}

public enum ProgramDataQuery : byte
{
    HWReadyForProgram = 0x01,   // Q1: HW ready to accept program data
    SendProgramStepsCount = 0x03,
    SendProgram = 0x04,
    SendDbcStepsCount = 0x07,
    SendDbcFile = 0x08,
    LiveStepUpdate = 0x09,      // Q9: amend the currently executing step's parameters in place
    JumpToStep = 0x0A,          // Q10: jump to an arbitrary program step (bm_program_v3.2)

}

// Q9 response REASON byte (payload[5]). 0x00 only ever appears alongside STATUS=Success;
// every other value is a rejection. 0x02/0x03/0x06 can only ever be raised by the Primary -
// the ps_program Q8 ACK/NACK to the Secondary carries no reason field, so every Secondary-side
// rejection (paused/operator-wait/link failure) reaches here as 0x05.
public enum LiveStepUpdateReason : byte
{
    Success = 0x00,
    Idle = 0x01,
    StepNotCurrent = 0x02,
    OperatorChangeNotPermitted = 0x03,
    OperatorNotEligible = 0x04,
    SecondaryRejectedOrLinkDown = 0x05,
    MalformedPacket = 0x06,
    PreviousUpdateInFlight = 0x07
}

/// <summary>
/// Q10 (Jump to Program Step) rejection reasons — bm_program_v3.2.md. 0x00 means the jump
/// succeeded; every other value is why it didn't. Secondary-side rejections (paused, operator-wait,
/// a step request already in flight) all surface as 0x04 — the ps_program Q9 ACK/NACK frame the
/// Secondary sends back carries no reason field, so the Primary cannot forward a more specific cause.
/// </summary>
public enum JumpToStepReason : byte
{
    Success = 0x00,
    NoProgramRunning = 0x01,        // Primary-detected: no program running/paused on this circuit
    StepDoesNotExist = 0x02,        // Primary-detected: target step not in the resident program
    MalformedFrame = 0x03,          // Primary-detected: wrong frame length
    SecondaryRejectedOrLinkDown = 0x04, // Secondary NACK (any cause) or Primary<->Secondary link failure
    JumpAlreadyInFlight = 0x05,     // A previous jump has not been answered yet — not evaluated
}

public enum ProgramControlQuery : byte
{
    Start = 0x01,
    Stop = 0x02,
    Pause = 0x03,
    Continue = 0x04,
    SyncTime = 0x05,
    SystemReset = 0x06
}

public enum RegistrationQuery : byte
{
    Registration = 0x01,   // Registration response with status
    Delete = 0x02          // Delete request
}

public enum CircuitStatus : byte
{
    Idle = 0x00,
    Charge = 0x01,
    Discharging = 0x02,
    Pause = 0x03,
    Countinue = 0x04,
    Interrupt = 0x05,
    Error = 0x06,
    Msg = 0x07,
    Offline = 0x08
}

public enum ProgramRunningStatus : byte
{
    Stop = 0x00,   // 👈 add this
    Running = 0x01,
}

public enum ActionType : byte
{
    Blank = 0x00,
    INT = 0x0E,
    STO = 0x0B,
    ERR = 0x10,
    MSG = 0x11,
    GOTO = 0x09
}

public enum CommandStatus : byte
{
    Failed = 0x00,
    Success = 0x01,
    AlreadyRegistered = 0x02
}

public enum QueryType : byte
{
    IsHWReady = 0x01,
    ReadConfig = 0x02,
    WriteConfig = 0x03,
}

public enum CalibrationQueryId : byte
{
    IsReady = 0x01,
    SendLive = 0x02,
    CurrentChargeLowPoint = 0x03,
    CurrentChargeHighPoint = 0x04,
    CurrentChargeGainOffset = 0x05,
    CurrentDisChargeLowPoint = 0x06,
    CurrentDisChargeHighPoint = 0x07,
    CurrentDisChargeGainOffset = 0x08,
    VoltageChargeLowPoint = 0x09,
    VoltageChargeHighPoint = 0x0A,
    VoltageChargeGainOffset = 0x0B,
    VoltageDisChargeLowPoint = 0x0C,
    VoltageDisChargeHighPoint = 0x0D,
    VoltageDisChargeGainOffset = 0x0E,
    TemperatureLowPoint = 0x0F,
    TemperatureHighPoint = 0x10,
    TemperatureGainOffset = 0x11,
    CancelCalibration = 0x12,
    StopCalibration = 0x13,
    StartChargeVerifyCurrentcalibration = 0x14,
    StartDisChargeVerifyCurrentcalibration = 0x15,
    StopVerifyCurrentcalibration = 0x16,
    PreviousCalibration = 0x17

}

public enum CalibrationRange
{
    Full_Range = 0xFF,
    Range1 = 0x01,
    Range2 = 0x02,
    Range3 = 0x03,
    Range4 = 0x04,
}

public enum CalibrationMode
{
    Charge,
    Discharge
}

public enum CalibrationType
{
    Voltage,
    Current
}

public enum MsgType : int
{
    Default = 0x00,
    Message = 0x01,
    Error = 0x02
}

[Flags]
public enum SystemError
{
    en_ERR_LNT = 1 << 0,
    en_ERR_ZNT = 1 << 1,
    en_ERR_OVER_TEMPERATURE = 1 << 2,
    en_ERR_CURRENT_SETPOINT_UNREACHABLE = 1 << 3,
    en_ERR_VOLTAGE_SETPOINT_UNREACHABLE = 1 << 4,
    en_ERR_OVER_CURRENT = 1 << 5,
    en_ERR_OVER_VOLTAGE = 1 << 6,
    en_ERR_REVERSE_POLARITY_VOLTAGE_SENSE = 1 << 7,
    en_ERR_PRIM_SEC_COM = 1 << 8,
    en_ERR_INVALID_CMD_PRIM_TO_SEC = 1 << 9,
    en_ERR_POWER_FAIL = 1 << 10,
    en_ERR_EEPROM_R_WR = 1 << 11,
    en_ERR_TEMP_FB = 1 << 12,
    en_ERR_NETWORK_CONN_FAIL = 1 << 13,
    en_ERR_POWER_SETPOINT_UNREACHABLE = 1 << 14,
    en_ERR_OVER_POWER = 1 << 15,
    en_ERR_INVALID_PROG_STEP = 1 << 16
}