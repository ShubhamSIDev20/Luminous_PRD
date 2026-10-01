namespace BatteryTestingSystem.Models.DTOs
{
    public class FactoryConfigDetailDTO
    {
        public string MacID { get; set; }                 // 6 bytes -> Hex string "C0:1A:2B:3C:64:A8"
        public string DeviceIPAddress { get; set; }       // 4 bytes -> IPv4 string "192.168.100.14"
        public string ClientRemoteIPAddress { get; set; } // 4 bytes -> IPv4 string "192.168.100.14"

        public int TcpClientRemotePort { get; set; }      // 2 bytes -> Int16/Int32 (9999 etc.)
        public int UdpClientRemotePort { get; set; }      // 2 bytes -> Int16/Int32
        public int UdpStoreRemotePort { get; set; }      // 2 bytes -> Int16/Int32

        public bool DhcpEnabled { get; set; }             // 1 byte (0x00 = false, 0x01 = true)

        public string CircuitType { get; set; }           // 1 byte -> string or enum (e.g., "01")

        public float ZntMaxVoltage { get; set; }          // 4 bytes -> IEEE 754 float
        public float LntMaxVoltage { get; set; }          // 4 bytes -> float
        public float CircuitMaxVoltage { get; set; }      // 4 bytes -> float
        public float CircuitMinVoltage { get; set; }      // 4 bytes -> float

        public float CircuitMaxDischargeCurrent { get; set; } // 4 bytes -> float
        public float CircuitMaxChargeCurrent { get; set; }    // 4 bytes -> float
        public int CircuitNumber { get; set; }
    }
}
