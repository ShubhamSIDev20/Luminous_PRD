using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities;

[Table("ScheduleExecutionLogs")]
public class ScheduleExecutionLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public long ScheduleId { get; set; }

    [ForeignKey(nameof(ScheduleId))]
    public ProgramSchedule? Schedule { get; set; }

    public int DeviceId { get; set; }
    public int CircuitId { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.Now;

    // Success | Failed | Skipped_Offline | Skipped_AlreadyRunning
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FailReason { get; set; }
}
