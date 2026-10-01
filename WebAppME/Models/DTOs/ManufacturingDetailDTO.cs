namespace BatteryTestingSystem.Models.DTOs
{
    public class ManufacturingDetailDTO
    {
        public string MasterSWVersion { get; set; }          // 11 bytes
        public string ComSWVersion { get; set; }             // 11 bytes
        public string SecondarySWVersion { get; set; }       // 11 bytes
        public string PrimarySerialNumber { get; set; }        // 4 bytes
        public string SecondarySerialNumber { get; set; }      // 4 bytes
        public DateTime ManufactureDateTime { get; set; }        // 4 bytes (timestamp)
        public DateTime CommissioningDateTime { get; set; }      // 4 bytes
        public DateTime PrimaryPCBAssemblyDateTime { get; set; } // 4 bytes
        public DateTime SecondaryPCBAssemblyDateTime { get; set; } // 4 bytes

        /// <summary>When this data was last fetched from the physical device and saved to the DB. Null if never synced.</summary>
        public DateTime? LastSyncedAt { get; set; }
    }
}
