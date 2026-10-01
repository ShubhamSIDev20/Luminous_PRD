using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Turns a node's soft EntityId reference into the physical address the operator already reads on
/// the dashboard: "1-8-2" for a channel, "Board 8" for a board.
///
/// The canvas previously rendered EntityId directly, which is the DATABASE PRIMARY KEY — so a
/// channel the rest of the app calls 1-8-2 appeared as "Channel 579". The correct values were
/// always present in the loaded TopologySnapshot and simply never looked up.
///
/// Indexes are built once per snapshot rather than scanned per node: at 640 channels a linear
/// scan per label would be ~450k comparisons on every structural re-render.
///
/// Unresolvable ids yield a visible "?" rather than throwing. Stale nodes are a supported state
/// (their hardware was deleted), and they must still render.
/// </summary>
public sealed class WorkflowNodeLabeller
{
    private readonly Dictionary<int, string> _deviceNames = new();
    private readonly Dictionary<long, int> _boardNumbers = new();
    private readonly Dictionary<long, (int DeviceId, int BoardNumber, int ChannelNumber)> _channelKeys = new();
    private readonly Dictionary<long, List<long>> _channelsByBoardId = new();
    private readonly Dictionary<int, List<long>> _channelsByDeviceId = new();

    public WorkflowNodeLabeller(TopologySnapshot topology)
    {
        foreach (var device in topology.Devices)
        {
            _deviceNames[device.DeviceId] = device.DeviceName;
            var deviceChannels = _channelsByDeviceId[device.DeviceId] = new List<long>();

            foreach (var board in device.Boards)
            {
                _boardNumbers[board.BoardId] = board.BoardNumber;
                var boardChannels = _channelsByBoardId[board.BoardId] = new List<long>();

                foreach (var channel in board.Channels)
                {
                    _channelKeys[channel.ChannelId] =
                        (device.DeviceId, board.BoardNumber, channel.ChannelNumber);
                    boardChannels.Add(channel.ChannelId);
                    deviceChannels.Add(channel.ChannelId);
                }
            }
        }
    }

    public string Label(WorkflowNode node) => node.Kind switch
    {
        NodeKind.Device => node.EntityId is { } d && _deviceNames.TryGetValue((int)d, out var name)
            ? name : "Device ?",

        NodeKind.Board => node.EntityId is { } b && _boardNumbers.TryGetValue(b, out var number)
            ? $"Board {number}" : "Board ?",

        NodeKind.Channel => node.EntityId is { } c && _channelKeys.TryGetValue(c, out var key)
            ? $"{key.DeviceId}-{key.BoardNumber}-{key.ChannelNumber}" : "Channel ?",

        NodeKind.Battery => "Battery",
        _ => node.Id,
    };

    /// <summary>ChannelId to the (DeviceID, SecondaryBoardNumber, ChannelNumber) tuple that
    /// ChannelManager matches circuits by. Database ids never match a circuit.</summary>
    public bool TryGetChannelKey(
        long channelId, out (int DeviceId, int BoardNumber, int ChannelNumber) key) =>
        _channelKeys.TryGetValue(channelId, out key);

    /// <summary>Every channel id beneath a device or board node, for online rollups.</summary>
    public IReadOnlyCollection<long> ChannelIdsForNodeEntity(NodeKind kind, long entityId) => kind switch
    {
        NodeKind.Device => _channelsByDeviceId.TryGetValue((int)entityId, out var d)
            ? d : Array.Empty<long>(),
        NodeKind.Board => _channelsByBoardId.TryGetValue(entityId, out var b)
            ? b : Array.Empty<long>(),
        _ => Array.Empty<long>(),
    };
}
