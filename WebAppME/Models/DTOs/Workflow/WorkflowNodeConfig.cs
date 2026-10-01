using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Services.Implementations.Workflow;

namespace BatteryTestingSystem.Models.DTOs.Workflow;

/// <summary>
/// One user's choice of which properties a channel node's face shows (spec D11). Persisted via
/// ServerSessionStorageService under "{UserName}_canvas_node_config" - a separate key from the
/// dashboard's own "{UserName}_card_config", since the two surfaces cap and default differently.
/// </summary>
public record WorkflowNodeConfig(
    IReadOnlyList<string> VisibleProperties,
    int RefreshRateMs = 250,
    IReadOnlyDictionary<string, string>? StatusColors = null)
{
    public static WorkflowNodeConfig Default { get; } =
        new(WorkflowNodeProperties.DefaultVisibleProperties);

    /// <summary>Adds an absent key (unless already at the cap), removes a present one. The single
    /// operation the picker UI drives - keeping it here rather than in the picker component makes
    /// the 6-cap a guaranteed invariant, not something a checkbox's disabled state merely
    /// suggests.</summary>
    /// <summary>Sets one status's colour override. Canvas-only and per-user — the Dashboard's own
    /// status colours are untouched (D13 forbids modifying it at all).</summary>
    public WorkflowNodeConfig WithStatusColor(string statusName, string hslTriplet)
    {
        var next = StatusColors is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(StatusColors);

        next[statusName] = hslTriplet;
        return this with { StatusColors = next };
    }

    public WorkflowNodeConfig Toggle(string key)
    {
        if (VisibleProperties.Contains(key))
            return this with { VisibleProperties = VisibleProperties.Where(k => k != key).ToList() };

        if (VisibleProperties.Count >= WorkflowNodeProperties.MaxVisible)
            return this;

        return this with { VisibleProperties = VisibleProperties.Append(key).ToList() };
    }
}
