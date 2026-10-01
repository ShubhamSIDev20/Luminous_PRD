using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Devices", Schema = "Device")]
    //[Encrypted]
    public class Device : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [MaxLength(3)]
        //[NotEncrypted]
        public int DeviceID { get; set; }

        [Display(Name = "Device Name")]
        [MaxLength(100)]
        public string DeviceName { get; set; }

        [Display(Name = "MAC ID")]
        [MaxLength(100)]
        public string MACID { get; set; }
        public string IPAddress { get; set; }
        public bool DHCPEnable { get; set; } = false;

        public string? ClientRemoteIPAddress { get; set; }
        public int TcpClientRemotePort { get; set; }
        public int UdpClientRemotePort { get; set; }
        public int UdpStoreRemotePort { get; set; }

        public string PrimarySerialNumber { get; set; }        // 4 bytes

        [MaxLength(10)]
        public string? SwVersion { get; set; } = string.Empty; // 11 bytes

        [MaxLength(10)]
        public string? ComSwVersion { get; set; } = string.Empty;

        [MaxLength(10)]
        public DateTime? ManufactureDateTime { get; set; }  // 4 bytes
        public DateTime? CommissioningDateTime { get; set; }  // 4 bytes
        public DateTime? AssemblyDate { get; set; }  // 4 bytes

        /// <summary>When manufacturing details were last fetched from the physical device and saved here.</summary>
        public DateTime? LastSyncedAt { get; set; }
    }
}
