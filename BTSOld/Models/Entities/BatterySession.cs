using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "BatterySessions", Schema = "Sessions")]

    public class BatterySession : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long SessionID { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public string? SessionName { get; set; }

        [Required]
        public string? ProgramHash { get; set; }

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

        [Required]
        public long? DbcFileRecordID { get; set; }

        public string? DbcName { get; set; }

        public long? Port2DbcFileRecordID { get; set; }
        public string? Port2DbcName { get; set; }
        public long? Port3DbcFileRecordID { get; set; }
        public string? Port3DbcName { get; set; }

        public string SessionFilePath { get; set; }

    }
}
