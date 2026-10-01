using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Channels", Schema = "Device")]

    public class Channel : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Required]
        public long SecondaryBoardId { get; set; }

        public SecondaryBoard SecondaryBoard { get; set; } = null!;

        [Range(1, 8)]
        public int ChannelNumber { get; set; }

        public bool IsRegistered { get; set; } = false;

        public string? SecondarySerialNumber { get; set; }      // 4 bytes

        [MaxLength(10)]
        public string ChannelType { get; set; } = "Single"; // "Single" or "Dual"

        [MaxLength(10)]
        public string? SwVersion { get; set; } = string.Empty; // 11 bytes
        public DateTime? AssemblyDate { get; set; }  // 4 bytes

        /// <summary>When manufacturing/factory details were last fetched from the physical device and saved here.</summary>
        public DateTime? LastSyncedAt { get; set; }
        public float? ZntMaxVoltage { get; set; }   // 4 bytes
        public float? LntMaxVoltage { get; set; }  // 4 bytes
        public float? ChannelMaxVoltage { get; set; }  // 4 bytes
        public float? ChannelMinVoltage { get; set; }  // 4 bytes
        public float? ChannelMaxDischargingCurrent { get; set; }  // 4 bytes
        public float? ChannelMaxChargingCurrent { get; set; }  // 4 bytes

    }
}
