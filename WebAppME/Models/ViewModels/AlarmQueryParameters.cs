using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.ViewModels
{
    public class AlarmQueryParameters
    {
        public SeverityLevel? Severity { get; set; }
        public AlarmSource? Source { get; set; }
        public string? DeviceId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool OnlyUnacknowledged { get; set; }
    }
}
