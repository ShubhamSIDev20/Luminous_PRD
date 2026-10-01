// Models/Entities/ExportRecord.cs
using BatteryTestingSystem.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.Entities;

/// <summary>
/// Persists metadata about a requested Excel export job.
/// Export files are written to disk and can be re-downloaded without
/// re-running the Hangfire job.
/// </summary>
public class ExportRecord : BaseEntity
{
    [Key]
    public int Id { get; set; }

    /// <summary>Relative path of the source session SQLite DB (matches BatterySession.SessionFilePath).</summary>
    [MaxLength(512)]
    public string SessionFilePath { get; set; } = string.Empty;

    /// <summary>Absolute path of the generated .xlsx file on disk.</summary>
    [MaxLength(512)]
    public string? ExportFilePath { get; set; }

    /// <summary>User who requested the export (matches ApplicationUser.UserName).</summary>
    [MaxLength(100)]
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }

    public ExportStatus Status { get; set; } = ExportStatus.Pending;

    /// <summary>File size in bytes once generated; 0 while pending/failed.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Human-readable display name shown in the UI exports list.</summary>
    [MaxLength(256)]
    public string? DisplayName { get; set; }

    /// <summary>Error details if Status == Failed.</summary>
    [MaxLength(1024)]
    public string? ErrorMessage { get; set; }

    /// <summary>Hangfire job ID so the UI can track/cancel.</summary>
    [MaxLength(64)]
    public string? HangfireJobId { get; set; }
}
