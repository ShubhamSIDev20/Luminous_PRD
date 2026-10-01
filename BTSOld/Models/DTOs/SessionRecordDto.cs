using BatteryTestingSystem.Services;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class SessionRecordDto
    {
        [Required]
        public long SessionID { get; set; }

        public string? SessionName { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Required]
        public long DeviceID { get; set; }
        [Required]
        public long CircuitID { get; set; }
        [Required]
        public long BatteryID { get; set; }
        public string? BatteryName { get; set; }

        [Required]
        public long ProgramID { get; set; }
        public string? ProgramName { get; set; }

        public long? DbcFileRecordID { get; set; }
        public string? DbcName { get; set; }

        public long? Port2DbcFileRecordID { get; set; }
        public string? Port2DbcName { get; set; }
        public long? Port3DbcFileRecordID { get; set; }
        public string? Port3DbcName { get; set; }

        public string? SessionFilePath { get; set; }
        public string? ProgramHash { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int Unstorerecordcount { get; set; } = 0;
        public int Storerecordcount { get; set; } = 0;

        public string Duration
        {
            get
            {
                if (EndTime == null)
                    return ""; // or string.Empty / "-"

                var duration = EndTime.Value - StartTime;

                if (duration.TotalSeconds < 0)
                    return "";

                return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        }
        public bool IsDeleted { get; set; } = false;
        public virtual ProgramDTO programs { get; set; } = new();
        public virtual BatteryDTO battery { get; set; } = new();
        public virtual DbcDatabase dbcDatabse { get; set; } = new();

    }
}

