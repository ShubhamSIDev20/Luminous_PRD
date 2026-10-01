namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>
/// A DB-free picture of real hardware. The builder and validator take this rather than an
/// AppDbContext, which is what keeps them pure and unit-testable — the same reason
/// CircuitSelectionLogic was extracted from DashboardView in T-38.
/// </summary>
public record TopologyChannel(long ChannelId, int ChannelNumber);

public record TopologyBoard(long BoardId, int BoardNumber, IReadOnlyList<TopologyChannel> Channels);

public record TopologyDevice(int DeviceId, string DeviceName, IReadOnlyList<TopologyBoard> Boards);

public record TopologySnapshot(IReadOnlyList<TopologyDevice> Devices)
{
    public static TopologySnapshot Empty => new(Array.Empty<TopologyDevice>());

    public TopologyDevice? FindDeviceOwningBoard(long boardId) =>
        Devices.FirstOrDefault(d => d.Boards.Any(b => b.BoardId == boardId));

    public TopologyDevice? FindDeviceOwningChannel(long channelId) =>
        Devices.FirstOrDefault(d => d.Boards.Any(b => b.Channels.Any(c => c.ChannelId == channelId)));

    public bool HasDevice(int deviceId) => Devices.Any(d => d.DeviceId == deviceId);

    public bool HasBoard(long boardId) => FindDeviceOwningBoard(boardId) is not null;

    public bool HasChannel(long channelId) => FindDeviceOwningChannel(channelId) is not null;

    /// <summary>
    /// Drops devices and boards that carry no channels, for the palette only.
    ///
    /// Deleting circuits on the Circuits page marks the CHANNELS deleted and leaves every Device
    /// row live, so a bench with one real device kept offering ten empty ones to drag on — plus any
    /// junk device a malformed registration packet created. A device with nothing under it cannot
    /// be placed usefully, so it does not belong in the palette.
    ///
    /// Deliberately NOT applied to the snapshot the labeller and validators use: they resolve
    /// existing nodes, and hiding a device there would report already-placed hardware as stale.
    /// </summary>
    public TopologySnapshot WithPlaceableDevicesOnly() =>
        new(Devices
            .Select(d => d with { Boards = d.Boards.Where(b => b.Channels.Count > 0).ToList() })
            .Where(d => d.Boards.Count > 0)
            .ToList());
}
