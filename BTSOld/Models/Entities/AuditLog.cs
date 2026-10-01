using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "AuditLog", Schema = "Audit")]

    public class AuditLog 
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
       public long LogId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string? User { get; set; } = string.Empty;
        public AuditActionType Action { get; set; } = AuditActionType.NONE;
     
        [Required]
        [MaxLength(100)]
        public ModuleName Module { get; set; } = ModuleName.NONE;
        public string? EntityId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public SeverityLevel Status { get; set; } = SeverityLevel.INFO;
        public string? IPAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; } //optional (browser/device info)
        public string? Details { get; set; } = string.Empty;
        public string? Metadata { get; set; } = string.Empty;

    }
}
