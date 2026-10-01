using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// CircuitStatus to CSS token. Never call status.ToString().ToLower() instead of this:
/// CircuitStatus.Countinue is misspelled in the enum while the theme variable is
/// --status-continue, so the naive version silently produces an unstyled node.
/// </summary>
public static class WorkflowStatusCss
{
    public static string Name(CircuitStatus status) => status switch
    {
        CircuitStatus.Idle => "idle",
        CircuitStatus.Charge => "charge",
        CircuitStatus.Discharging => "discharging",
        CircuitStatus.Pause => "pause",
        CircuitStatus.Countinue => "continue",      // enum typo, CSS variable is correct
        CircuitStatus.Interrupt => "interrupt",
        CircuitStatus.Error => "error",
        CircuitStatus.Msg => "msg",
        CircuitStatus.Offline => "offline",
        _ => "idle",
    };

    public static string Var(CircuitStatus status) => $"--status-{Name(status)}";
}
