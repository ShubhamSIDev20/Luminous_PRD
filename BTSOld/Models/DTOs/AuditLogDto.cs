using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.DTOs
{
    public class AuditLogDto
    {
        public long LogId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? User { get; set; } = string.Empty;
        public AuditActionType Action { get; set; } = AuditActionType.NONE;
        public ModuleName Module { get; set; } = ModuleName.NONE;
        public string? EntityId { get; set; } = string.Empty;
        public SeverityLevel Status { get; set; } = SeverityLevel.INFO;
        public string? IPAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; } //optional (browser/device info)
        public string? Details { get; set; } = string.Empty;
        public string? Metadata { get; set; } = string.Empty;
    }
}
