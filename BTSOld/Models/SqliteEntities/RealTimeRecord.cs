using BatteryTestingSystem.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.SqliteEntities
{
    public class RealTimeRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime TimeStamp { get; set; }

        public int DeviceID { get; set; }

        public int CircuitID { get; set; }

        public int SessionID { get; set; }

        public int BatteryID { get; set; }

        public int ProgramID { get; set; }

        public int StepNumber { get; set; }

        public TimeSpan StepRunningTime { get; set; }

        public TimeSpan RunningTime { get; set; }

        [Column(TypeName = "float")]
        public float Current { get; set; }

        [Column(TypeName = "float")]
        public float Voltage { get; set; }

        [Column(TypeName = "float")]
        public float Capacity { get; set; }

        [Column(TypeName = "float")]
        public float Temperature { get; set; }

        public ProgramRunningStatus ProgramStatus { get; set; }

        public CircuitStatus CircuitStatus { get; set; }

        public int ErrorID { get; set; }

        public bool RegistrationFlag { get; set; }
    }
}
