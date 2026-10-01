using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public class WorkflowLayoutService : IWorkflowLayoutService
{
    private readonly IWorkflowLayoutRepository _repository;

    public WorkflowLayoutService(IWorkflowLayoutRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<LayoutSummary>> ListAsync(string userId)
    {
        var rows = await _repository.ListForUserAsync(userId);
        return rows
            .Select(r => new LayoutSummary(r.Id, r.Name, r.Description, r.UpdatedAt))
            .ToList();
    }

    public async Task<LayoutLoadResult> LoadAsync(
        long id, string userId, TopologySnapshot topology)
    {
        var row = await _repository.GetForUserAsync(id, userId);

        if (row is null)
            return LayoutLoadResult.Failed("That layout no longer exists.");

        if (!WorkflowGraphJson.TryDeserialize(row.LayoutJson, out var graph, out var error))
            return LayoutLoadResult.Failed(error!);

        return new LayoutLoadResult(true, graph, FindStaleNodeIds(graph!, topology), null);
    }

    public async Task<long> SaveAsync(
        long? id, string name, string? description, WorkflowGraph graph, string userId)
    {
        var row = id is { } existingId
            ? await _repository.GetForUserAsync(existingId, userId)
            : null;

        row ??= new WorkflowLayout { OwnerUserId = userId };

        row.Name = name;
        row.Description = description;
        row.LayoutJson = WorkflowGraphJson.Serialize(graph);
        row.SchemaVersion = WorkflowGraph.CurrentVersion;

        var saved = await _repository.UpsertAsync(row);
        return saved.Id;
    }

    public Task<bool> DeleteAsync(long id, string userId) =>
        _repository.SoftDeleteAsync(id, userId);

    public async Task<IReadOnlyDictionary<long, string>> GetClaimedChannelsAsync(
        string userId, long? excludingLayoutId)
    {
        var rows = await _repository.ListForUserAsync(userId);
        var claimed = new Dictionary<long, string>();

        foreach (var row in rows)
        {
            if (row.Id == excludingLayoutId) continue;
            if (!WorkflowGraphJson.TryDeserialize(row.LayoutJson, out var graph, out _)) continue;

            foreach (var node in graph!.Nodes)
            {
                if (node.Kind == NodeKind.Channel && node.EntityId is { } channelId)
                    claimed[channelId] = row.Name;
            }
        }

        return claimed;
    }

    /// <summary>
    /// Node ids whose soft reference no longer resolves against live hardware. These render
    /// dimmed with a warning badge and are excluded from the telemetry subscription — they are
    /// never dropped from the graph, because silently deleting a user's work is worse than
    /// showing them a grey node. Battery nodes are exempt: they reference a BatteryType, which
    /// the topology snapshot knows nothing about.
    /// </summary>
    public static IReadOnlyList<string> FindStaleNodeIds(
        WorkflowGraph graph, TopologySnapshot topology) =>
        graph.Nodes
            .Where(n => !Resolves(n, topology))
            .Select(n => n.Id)
            .ToList();

    private static bool Resolves(WorkflowNode node, TopologySnapshot topology)
    {
        if (node.Kind == NodeKind.Battery) return true;
        if (node.EntityId is not { } id) return false;

        return node.Kind switch
        {
            NodeKind.Device => topology.HasDevice((int)id),
            NodeKind.Board => topology.HasBoard(id),
            NodeKind.Channel => topology.HasChannel(id),
            _ => true,
        };
    }
}
