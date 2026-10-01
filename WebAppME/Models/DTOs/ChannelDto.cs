using BatteryTestingSystem.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class ChannelDto
    {
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; } = 1;
        public int ChannelNumber { get; set; }
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
        public string ChannelType { get; set; } = "Single"; // "Single" or "Dual"
        public string? SecondarySwVersion { get; set; } = string.Empty;
        public DateTime? SecondaryAssemblyDate { get; set; }
        public float? ZntMaxVoltage { get; set; }
        public float? LntMaxVoltage { get; set; }
        public float? ChannelMaxVoltage { get; set; }
        public float? ChannelMinVoltage { get; set; }
        public float? ChannelMaxDischargingCurrent { get; set; }
        public float? ChannelMaxChargingCurrent { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

    }
}
