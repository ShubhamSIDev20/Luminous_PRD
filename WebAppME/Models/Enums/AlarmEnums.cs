namespace BatteryTestingSystem.Models.Enums;

/// <summary>
/// Which subsystem produced an alarm. Used for filtering in the bell and the history page.
/// </summary>
public enum AlarmSource
{
    NONE = 0,
    /// <summary>Device TCP link lost / failed to reconnect.</summary>
    Comms = 1,
    /// <summary>Error or status code reported by the BMS for a channel.</summary>
    ChannelError = 2,
    /// <summary>Failure while persisting recorded data.</summary>
    DataStore = 3
}
