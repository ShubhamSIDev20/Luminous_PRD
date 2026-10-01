namespace BatteryTestingSystem.Services.BROADCAST;

public sealed class BroadcastDeviceInfo
{
    public byte[] UniqueId { get; set; } = Array.Empty<byte>();
    public string DeviceIp { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string Dns1 { get; set; } = string.Empty;
    public string Dns2 { get; set; } = string.Empty;
    public string RemoteIp { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public int UdpLivePort { get; set; }
    public int UdpRegPort { get; set; }

    /// <summary>Stable deduplication key derived from the 4-byte unique ID.</summary>
    public string UniqueKey => BitConverter.ToString(UniqueId);
}

/// <summary>
/// Payload for a Q4 (set device IP) broadcast command.
/// </summary>
public sealed class BroadcastIpConfig
{
    public byte[] UniqueId { get; set; } = Array.Empty<byte>();
    public string DeviceIp { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string Dns1 { get; set; } = string.Empty;
    public string Dns2 { get; set; } = string.Empty;
}

/// <summary>
/// Payload for a Q5 (set server config) broadcast command.
/// </summary>
public sealed class BroadcastServerConfig
{
    public byte[] UniqueId { get; set; } = Array.Empty<byte>();
    public string RemoteIp { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public int UdpLivePort { get; set; }
    public int UdpRegPort { get; set; }
}
