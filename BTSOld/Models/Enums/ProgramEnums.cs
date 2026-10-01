namespace BatteryTestingSystem.Models.Enums;

public enum ParamFormat
{
    None = 0,               // Not applicable
    ConditionValueStr = 1,
    ValueStr = 2,
    Value = 3,
    Str = 4,
    Action = 5,
    Standard = 6,
    Registration = 7,
    Goto = 8,
    INT = 9,
}

public enum ParamType
{
    Action = 0, // Action
    Nominal = 1, // Nominal Value
    Limit = 2, // Limit Value
    Registration = 3, // Registration Value
}

public enum UnitCode : byte
{
    STANDARD = 0xF0, // Standard
    A = 0x21, // Current (Ampere)
    V = 0x22, // Voltage
    W = 0x23, // Power (Watt)
    Ah = 0x24, // Capacity
    Wh = 0x26, // Energy
    C = 0x28, // Temperature
    Time = 0x29,  // Time (s OR Sec/min or m/h or hr)
    BLANK = 0x00,
    INT = 0x0E,
    STO = 0x0B,
    ERR = 0x10,
    MSG = 0x11,
    GOTO = 0x09
}


public enum CutoffCondition : byte
{
    Current = 0x31,
    Voltage = 0x32,
    Power = 0x33,
    ChargeCapacity = 0x34,
    DischargeCapacity = 0x35,
    ChargeEnergy = 0x36,
    DischargeEnergy = 0x37,
    Temperature = 0x38,
    Time = 0x39,
    AccumulatedCapacity = 0x3A,
    StepCapacity = 0x3B,
    AccumulatedEnergy = 0x3C,
    StepEnergy = 0x3D
}

public enum RegistrationType : byte
{
    STANDARD = 0xF0,

    Time = 0x21,
    Current = 0x22,
    Voltage = 0x23,
    Temperature = 0x24,
    Power = 0x25,

    AccumulatedCapacity = 0x26,
    ChargeCapacity = 0x27,
    DischargeCapacity = 0x28,
    StepCapacity = 0x29,

    AccumulatedEnergy = 0x2A,
    ChargeEnergy = 0x2B,
    DischargeEnergy = 0x2C,
    StepEnergy = 0x2D
}


public enum LogicOperator : byte
{
    GreaterThan = 0x51, // >
    LessThan = 0x52, // <
    GreaterThanOrEqual = 0x53, // ≥
    LessThanOrEqual = 0x54, // ≤
    NotEqual = 0x55, // ≠
    Equal = 0x56  // =
}

