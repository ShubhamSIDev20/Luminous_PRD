using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record ChannelActionResult(int DeviceId, int SecondaryBoardNumber, int ChannelNumber, bool Success, string Message);

/// <summary>
/// Extracts DashboardView.DoAction's per-device SemaphoreSlim concurrency pattern into a testable,
/// instantiable class: channels on the same device run sequentially (the device's own TCP link
/// can't handle overlapping commands), channels on different devices run in parallel. One instance
/// per page - the lock dictionary must persist across multiple action invocations, exactly like
/// the dashboard's own _deviceLocks field.
/// </summary>
public class ChannelActionExecutor
{
    private readonly Dictionary<int, SemaphoreSlim> _deviceLocks = new();

    public async Task<IReadOnlyList<ChannelActionResult>> ExecuteAsync(
        string action,
        IReadOnlyList<IChannelCommandHandler> circuits,
        Action? onStepCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = circuits.Select(circuit => RunOneAsync(action, circuit, onStepCompleted, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results;
    }

    private async Task<ChannelActionResult> RunOneAsync(
        string action, IChannelCommandHandler circuit, Action? onStepCompleted, CancellationToken cancellationToken)
    {
        var deviceId = circuit.Channel.DeviceID;
        var gate = GetDeviceLock(deviceId);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var response = await RunAsync(action, circuit);
            return new ChannelActionResult(
                deviceId, circuit.Channel.SecondaryBoardNumber, circuit.Channel.ChannelNumber,
                response.Success, response.Message);
        }
        finally
        {
            gate.Release();
            onStepCompleted?.Invoke();
        }
    }

    private static Task<CommonResponse<bool>> RunAsync(string action, IChannelCommandHandler circuit) => action switch
    {
        "start" => circuit.StartProgram(),
        "stop" => circuit.StopProgram(),
        "pause" => circuit.PauseProgram(),
        "continue" => circuit.ContinueProgram(),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported channel action."),
    };

    private SemaphoreSlim GetDeviceLock(int deviceId)
    {
        lock (_deviceLocks)
        {
            if (!_deviceLocks.TryGetValue(deviceId, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _deviceLocks[deviceId] = gate;
            }
            return gate;
        }
    }
}
