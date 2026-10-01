using BatteryTestingSystem.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class CircuitDto
    {
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
        public string DeviceName { get; set; }
        public string MACID { get; set; }
        public string IPAddress { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsRegistered { get; set; } = false;
        public bool DHCPEnable { get; set; } = false;
        public string? MasterSwVersion { get; set; } = string.Empty;
        public string? ComSwVersion { get; set; } = string.Empty;
        public DateTime? ManufactureDateTime { get; set; }
        public DateTime? CommissioningDateTime { get; set; }
        public DateTime? AssemblyDate { get; set; }
        public string PrimarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string SecondarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string CircuitType { get; set; } = "Single"; // "Single" or "Dual"
        public string? SecondarySwVersion { get; set; } = string.Empty;
        public DateTime? SecondaryAssemblyDate { get; set; }
        public float? ZntMaxVoltage { get; set; }
        public float? LntMaxVoltage { get; set; }
        public float? CircuitMaxVoltage { get; set; }
        public float? CircuitMinVoltage { get; set; }
        public float? CircuitMaxDischargingCurrent { get; set; }
        public float? CircuitMaxChargingCurrent { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; } 
        public DateTime? UpdatedAt { get; set; } 

    }
}
