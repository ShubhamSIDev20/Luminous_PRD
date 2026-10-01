using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Circuits", Schema = "Device")]
    //[Encrypted]

    public class Circuit : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        //[NotEncrypted] 
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [MaxLength(1)]
        public int CircuitID { get; set; }

        public bool IsRegistered { get; set; } = false;

        public string? SecondarySerialNumber { get; set; }      // 4 bytes

        [MaxLength(10)]
        public string CircuitType { get; set; } = "Single"; // "Single" or "Dual"

        [MaxLength(10)]
        public string? SwVersion { get; set; } = string.Empty; // 11 bytes
        public DateTime? AssemblyDate { get; set; }  // 4 bytes
        public float? ZntMaxVoltage { get; set; }   // 4 bytes
        public float? LntMaxVoltage { get; set; }  // 4 bytes
        public float? CircuitMaxVoltage { get; set; }  // 4 bytes
        public float? CircuitMinVoltage { get; set; }  // 4 bytes
        public float? CircuitMaxDischargingCurrent { get; set; }  // 4 bytes
        public float? CircuitMaxChargingCurrent { get; set; }  // 4 bytes

    }
}
