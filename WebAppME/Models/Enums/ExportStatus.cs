// Models/Enums/ExportStatus.cs
namespace BatteryTestingSystem.Models.Enums;

public enum ExportStatus
{
    Pending    = 0,   // Job queued, not started
    Processing = 1,   // Hangfire job running
    Ready      = 2,   // File written, ready to download
    Failed     = 3,   // Job failed — see ExportRecord.ErrorMessage
    Deleted    = 4,   // User deleted the export file
    Superseded = 5    // A newer export was requested; this record is obsolete
}
