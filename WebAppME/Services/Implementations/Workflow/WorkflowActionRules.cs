using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Canvas-native mirror of DashboardView.CanContextAction, minus "calibration" - the canvas has
/// no calibration entry point (D9 lists only Start/Stop/Pause/Continue/Transfer).
/// </summary>
public static class WorkflowActionRules
{
    public static bool CanPerform(ChannelTelemetry reading, string action)
    {
        var cs = reading.Status;
        var ps = reading.ProgramStatus;
        var tcp = reading.IsConnected;

        if (cs == CircuitStatus.Offline) return false;

        return action switch
        {
            "start" => ps == ProgramRunningStatus.Stop && tcp,
            "stop" => tcp,
            "pause" => ps == ProgramRunningStatus.Running && tcp,
            "continue" => ps == ProgramRunningStatus.Running
                          && (cs == CircuitStatus.Pause
                              || cs == CircuitStatus.Interrupt
                              || cs == CircuitStatus.Countinue
                              || cs == CircuitStatus.Error
                              || cs == CircuitStatus.Msg) && tcp,
            "transfer" => cs == CircuitStatus.Idle && ps == ProgramRunningStatus.Stop && tcp,
            _ => false,
        };
    }
}
