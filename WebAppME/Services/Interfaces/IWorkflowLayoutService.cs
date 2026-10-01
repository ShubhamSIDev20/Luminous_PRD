using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Interfaces;

public record LayoutSummary(long Id, string Name, string? Description, DateTime UpdatedAt);

/// <summary>
/// Ok=false means the layout could not be opened at all (missing, foreign, corrupt, or saved by
/// another schema version). Ok=true with a non-empty StaleNodeIds means it opened fine but some
/// nodes point at hardware that no longer exists — those render dimmed, they are not an error.
/// </summary>
public record LayoutLoadResult(
    bool Ok,
    WorkflowGraph? Graph,
    IReadOnlyList<string> StaleNodeIds,
    string? Error)
{
    public static LayoutLoadResult Failed(string error) =>
        new(false, null, Array.Empty<string>(), error);
}

public interface IWorkflowLayoutService
{
    Task<IReadOnlyList<LayoutSummary>> ListAsync(string userId);

    Task<LayoutLoadResult> LoadAsync(long id, string userId, TopologySnapshot topology);

    /// <summary>Pass id = null to create; an existing id to update in place. Returns the id.</summary>
    Task<long> SaveAsync(
        long? id, string name, string? description, WorkflowGraph graph, string userId);

    Task<bool> DeleteAsync(long id, string userId);

    /// <summary>Every channel EntityId already placed in one of this user's OTHER saved layouts,
    /// mapped to that layout's name (for a disabled-palette-item tooltip). Excludes
    /// excludingLayoutId (the layout currently open, if any) so editing a layout does not report
    /// its own channels as claimed by someone else.</summary>
    Task<IReadOnlyDictionary<long, string>> GetClaimedChannelsAsync(
        string userId, long? excludingLayoutId);
}
