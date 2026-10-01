using BatteryTestingSystem.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "CalibrationData", Schema = "Device")]

    public class CalibrationDataPoint : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public int DeviceId { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelId { get; set; }
        public CalibrationMode Mode { get; set; }     // Charge / Discharge
        public CalibrationType Type { get; set; }     // Voltage / Current
        public CalibrationRange Range { get; set; } = CalibrationRange.Full_Range; // Full / R1 / R2 / R3 / R4
        public DateTime DateTime { get; set; }
        public float Gain { get; set; }
        public float Offset { get; set; }
    }
}
