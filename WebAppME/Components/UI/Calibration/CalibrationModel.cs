using BatteryTestingSystem.Utils;

namespace BatteryTestingSystem.Components.UI.Calibration;

// ─── Calibration Type ────────────────────────────────────────────────────────
/// <summary>
/// Represents the type of calibration being performed.
/// Temperature calibration has no Charge/Discharge mode and no Range selection.
/// </summary>
public enum CalibrationTypeMode
{
    Current,
    Voltage,
    Temperature
}

// ─── Calibration Step ────────────────────────────────────────────────────────
public enum CalibrationStep
{
    Idle,
    LowConfirm,
    LowActualConfirm,
    HighConfirm,
    HighActualConfirm,
    Complete
}

// ─── Message Type ────────────────────────────────────────────────────────────
public enum MessageType
{
    Info,
    Success,
    Warning,
    Error
}

// ─── Calibration Errors ──────────────────────────────────────────────────────
public enum CalibrationError : byte
{
    CALIB_NO_ERROR                      = 0x00,
    CALIB_CC_Charge_LOW_Error           = 0x01,
    CALIB_CC_Charge_HIGH_Error          = 0x02,
    CALIB_CC_Charge_GO_Write_Error      = 0x03,
    CALIB_CC_Discharge_LOW_Error        = 0x04,
    CALIB_CC_Discharge_HIGH_Error       = 0x05,
    CALIB_CC_Discharge_GO_Write_Error   = 0x06,
    CALIB_CV_Charge_LOW_Error           = 0x07,
    CALIB_CV_Charge_HIGH_Error          = 0x08,
    CALIB_CV_Charge_GO_Write_Error      = 0x09,
    CALIB_CV_Discharge_LOW_Error        = 0x0A,
    CALIB_CV_Discharge_HIGH_Error       = 0x0B,
    CALIB_CV_Discharge_GO_Write_Error   = 0x0C,
    CALIB_Cancle_Error                  = 0x0D,
    CALIB_Stop_Error                    = 0x0E,
    CALIB_CC_Charge_Verify_Error        = 0x0F,
    CALIB_CC_Discharge_Verify_Error     = 0x10,
    CALIB_Current_Verify_Stop_Error     = 0x11,
    CALIB_Parameters_Read_Error         = 0x12,
    CALIB_TEMP_LOW_Error                = 0x13,
    CALIB_TEMP_HIGH_Error               = 0x14,
    CALIB_TEMP_GO_Write_Error           = 0x15,
    CALIB_OVER_VOLTAGE_Error            = 0x16,
    CALIB_OVER_CURRENT_Error            = 0x17,
    CALIB_OVER_POWER_Error              = 0x17,
    CALIB_PID_OutOfRange_Error          = 0x19
}

public static class CalibrationErrorExtensions
{
    public static bool TryGetError(byte value, out CalibrationError error)
    {
        if (Enum.IsDefined(typeof(CalibrationError), value))
        {
            error = (CalibrationError)value;
            return true;
        }
        error = CalibrationError.CALIB_NO_ERROR;
        return false;
    }
}

// ─── Calibration Message ─────────────────────────────────────────────────────
public class CalibrationMessage
{
    public DateTime Timestamp { get; set; }
    public string Text { get; set; } = "";
    public MessageType Type { get; set; }
    public string User { get; init; } = CurrentUser.UserName;
}

// ─── Verification Result ─────────────────────────────────────────────────────
public class VerificationResult
{
    public float Expected { get; set; }
    public float Actual { get; set; }
    public float Deviation { get; set; }
    public bool Passed { get; set; }
}
