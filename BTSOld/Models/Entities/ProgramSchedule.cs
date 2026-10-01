using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities;

[Table("ProgramSchedules")]
public class ProgramSchedule : BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // What to transfer
    public long ProgramId { get; set; }
    public long BatteryId { get; set; }
    public long? Port1DbcFileId { get; set; }   // nullable = optional, per-port (mirrors Battery.Port1DbcId/Port2DbcId/Port3DbcId)
    public long? Port2DbcFileId { get; set; }
    public long? Port3DbcFileId { get; set; }

    // When to run — stored as UTC
    public DateTime ScheduledAt { get; set; }

    // Target circuits stored as JSON  e.g. [{"DeviceId":1,"CircuitId":2}]
    public string TargetCircuitsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    // Hangfire job id — so we can delete/reschedule if user edits
    [MaxLength(100)]
    public string? HangfireJobId { get; set; }
}
