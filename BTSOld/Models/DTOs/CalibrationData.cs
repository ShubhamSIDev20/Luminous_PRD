using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs
{
    /// <summary>
    /// Full calibration dataset returned by the hardware on query 0x17.
    ///
    /// Current Charge/Discharge — 5 ranges each (Full, R1-100%, R2-50%, R3-25%, R4-12.5%).
    /// Voltage Charge/Discharge — single point (no range selection).
    /// Temperature             — single point.
    ///
    /// Wire order inside 0x17 response (each block = 4B gain + 4B offset + 4B epoch = 12 bytes):
    ///   CurrentCharge[Full, R1, R2, R3, R4]
    ///   CurrentDischarge[Full, R1, R2, R3, R4]
    ///   VoltageCharge
    ///   VoltageDischarge
    ///   Temperature
    /// </summary>
    public class CalibrationData
    {
        // ── Current: per-range lists (5 entries each, index = range order above) ──
        public List<CalibrationDataPointDto> CurrentCharge    { get; set; } = new();
        public List<CalibrationDataPointDto> CurrentDischarge { get; set; } = new();

        // ── Voltage: single point per mode ───────────────────────────────────────
        public CalibrationDataPointDto? VoltageCharge    { get; set; } = new();
        public CalibrationDataPointDto? VoltageDischarge { get; set; } = new();

        // ── Temperature: single point ────────────────────────────────────────────
        public CalibrationDataPointDto? Temperature { get; set; } = new();

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns the CalibrationRange value for position index (0-based) in the wire order.</summary>
        public static CalibrationRange RangeAtIndex(int i) => i switch
        {
            0 => CalibrationRange.Full_Range,
            1 => CalibrationRange.Range1,
            2 => CalibrationRange.Range2,
            3 => CalibrationRange.Range3,
            4 => CalibrationRange.Range4,
            _ => CalibrationRange.Full_Range
        };

        /// <summary>
        /// Returns the stored point for the given range, or null if not present.
        /// </summary>
        public CalibrationDataPointDto? GetCurrentCharge(CalibrationRange range)
            => CurrentCharge.FirstOrDefault(p => p.Range == range);

        public CalibrationDataPointDto? GetCurrentDischarge(CalibrationRange range)
            => CurrentDischarge.FirstOrDefault(p => p.Range == range);
    }
}
