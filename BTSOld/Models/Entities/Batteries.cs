using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Batteries", Schema = "Battery")]

    public class Batteries : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public string Name { get; set; }

        public int BatteryTypeId { get; set; }

        public string? Comments { get; set; }

        public int Quantity { get; set; } = 1;

        // Extended Section
        public string Producer { get; set; }

        public float NominalVoltage { get; set; }

        public float NominalCurrent { get; set; }

        public float NominalCapacity { get; set; }

        public float ChargeFactor { get; set; }

        public float Impedance { get; set; }

        public float EnergyDensity { get; set; }

        public float ColdCrankingCurrent { get; set; }

        public int NumberOfCells { get; set; }

        public float MaximumVoltage { get; set; }

        public float GassingVoltage { get; set; }

        public float BreakVoltage { get; set; }

        public long? Port1DbcId { get; set; }
        public long? Port2DbcId { get; set; }
        public long? Port3DbcId { get; set; }
    }
}
