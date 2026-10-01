using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class BatteryDTO
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Battery name is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 100 characters.")]
        public string Name { get; set; }

        [Required]
        public int BatteryTypeId { get; set; } = 1;

        [StringLength(500)]
        public string? Comments { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        [Required]
        public int Quantity { get; set; } = 1;

        [StringLength(100)]
        [Required]
        public string Producer { get; set; } = string.Empty;

        [Range(0, 1000, ErrorMessage = "Nominal Voltage must be between 0 and 1000.")]
        public float NominalVoltage { get; set; } = 0;

        [Range(0, 10000)]
        public float NominalCurrent { get; set; } = 0;

        [Range(0, 100000)]
        public float NominalCapacity { get; set; } = 0;

        [Range(0, 10)]
        public float ChargeFactor { get; set; } = 0;

        [Range(0, 1000)]
        public float Impedance { get; set; } = 0;

        [Range(0, 500)]
        public float EnergyDensity { get; set; } = 0;

        [Range(0, 5000)]
        public float ColdCrankingCurrent { get; set; } = 0;

        [Range(1, 100)]
        public int NumberOfCells { get; set; } = 1;

        [Range(0.000001, 1000, ErrorMessage = "MaximumVoltage must be vaild")]
        [Required]
        public float MaximumVoltage { get; set; } 

        [Range(0, 1000)]
        public float GassingVoltage { get; set; }

        [Range(0.000001, 1000, ErrorMessage = "BreakVoltage must be vaild")]
        [Required]
        public float BreakVoltage { get; set; } 
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;

        public long? Port1DbcId { get; set; }
        public long? Port2DbcId { get; set; }
        public long? Port3DbcId { get; set; }
    }
}
