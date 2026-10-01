using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.Entities
{
    /// <summary>
    /// One operator-facing alarm. A row is "active" while it is neither acknowledged nor
    /// cleared; repeats of the same AlarmKey collapse into the active row rather than
    /// inserting a new one.
    /// </summary>
    [Table(name: "AlarmLog", Schema = "Audit")]
    public class AlarmLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>Stable identity of the fault, e.g. "12/0/3/comms-loss".</summary>
        [Required]
        [MaxLength(200)]
        public string AlarmKey { get; set; } = string.Empty;

        public SeverityLevel Severity { get; set; } = SeverityLevel.INFO;

        public AlarmSource Source { get; set; } = AlarmSource.NONE;

        [MaxLength(50)]
        public string? DeviceId { get; set; }

        public int? BoardNumber { get; set; }

        public int? ChannelNumber { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        public int OccurrenceCount { get; set; } = 1;

        public DateTime? AcknowledgedAtUtc { get; set; }

        [MaxLength(100)]
        public string? AcknowledgedBy { get; set; }

        public DateTime? ClearedAtUtc { get; set; }
    }
}
