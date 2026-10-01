using BatteryTestingSystem.Components.UI.Calibration;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs
{
    public class CalibrationDto
    {
        // ── Stored calibration data from hardware ──────────────────────────────
        // Current: one entry per range (up to 5 — Full, R1, R2, R3, R4)
        public List<CalibrationDataPointDto> CurrentCharge    { get; set; } = new();
        public List<CalibrationDataPointDto> CurrentDischarge { get; set; } = new();

        // Voltage & Temperature: single-point (no range)
        public CalibrationDataPointDto? VoltageCharge    { get; set; } = null;
        public CalibrationDataPointDto? VoltageDischarge { get; set; } = null;
        public CalibrationDataPointDto? TemperaturePoint { get; set; } = null;

        // ── ADC buffer for averaging ──────────────────────────────────────────
        public List<RealTimeRecordDto> calibrationBuffer { get; set; } = new();

        // ── Process state ─────────────────────────────────────────────────────
        public bool IsCalibrationInPrcoess { get; set; } = false;
        public bool IsVerifying            { get; set; } = false;

        // ── Calibration type (Current / Voltage / Temperature) ────────────────
        public CalibrationTypeMode CalibrationMode { get; set; } = CalibrationTypeMode.Current;

        /// <summary>Convenience: true when calibrating current.</summary>
        public bool IsCurrent     => CalibrationMode == CalibrationTypeMode.Current;

        /// <summary>Convenience: true when calibrating temperature.</summary>
        public bool IsTemperature => CalibrationMode == CalibrationTypeMode.Temperature;

        // ── Charge / Discharge mode (not applicable for Temperature) ──────────
        public bool IsCharge { get; set; } = true;

        // ── Range selection (Current only) ────────────────────────────────────
        public CalibrationRange SelectedRange { get; set; } = CalibrationRange.Range1;

        // ── Verification ──────────────────────────────────────────────────────
        public float? VerifyTestValue { get; set; }

        // ── Calibration inputs ────────────────────────────────────────────────
        public float? UserLowInput    { get; set; }
        public float? UserHighInput   { get; set; }
        public float? SystemLowInput  { get; set; }
        public float? SystemHighInput { get; set; }
        public float? ActualLowInput  { get; set; }
        public float? ActualHighInput { get; set; }

        // ── Computed results ──────────────────────────────────────────────────
        public float? Gain   { get; set; }
        public float? Offset { get; set; }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Upserts (add or replace) a range point in a current charge/discharge list.
        /// </summary>
        public static void UpsertRangePoint(List<CalibrationDataPointDto> list, CalibrationDataPointDto point)
        {
            var existing = list.FirstOrDefault(p => p.Range == point.Range);
            if (existing != null)
            {
                existing.Gain     = point.Gain;
                existing.Offset   = point.Offset;
                existing.DateTime = point.DateTime;
            }
            else
            {
                list.Add(point);
            }
        }

        /// <summary>Gets the stored point for the currently selected range (Current Charge).</summary>
        public CalibrationDataPointDto? CurrentChargeForSelectedRange
            => CurrentCharge.FirstOrDefault(p => p.Range == SelectedRange);

        /// <summary>Gets the stored point for the currently selected range (Current Discharge).</summary>
        public CalibrationDataPointDto? CurrentDischargeForSelectedRange
            => CurrentDischarge.FirstOrDefault(p => p.Range == SelectedRange);
    }
}
