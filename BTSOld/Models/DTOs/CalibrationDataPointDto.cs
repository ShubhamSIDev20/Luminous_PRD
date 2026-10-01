using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs
{
    /// <summary>
    /// A single calibration data point for one specific range.
    /// Used for Voltage and Temperature (which have one range) and as an element
    /// in the per-range list for Current calibration.
    /// </summary>
    public class CalibrationDataPointDto
    {
        public long Id { get; set; }

        public int DeviceId { get; set; }
        public int CircuitId { get; set; }

        public CalibrationMode Mode { get; set; }
        public CalibrationType Type { get; set; }

        // Range this point belongs to. Full_Range (0xFF) = single-range channels (Voltage/Temperature).
        public CalibrationRange Range { get; set; } = CalibrationRange.Full_Range;

        public DateTime DateTime { get; set; }

        public float Gain { get; set; }
        public float Offset { get; set; }
    }
}
