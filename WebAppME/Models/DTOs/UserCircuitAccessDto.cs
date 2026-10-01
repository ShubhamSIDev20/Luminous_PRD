namespace BatteryTestingSystem.Models.DTOs
{
    public class UserCircuitAccessDto
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }
        public string? DeviceName { get; set; }  // for display
    }
}
