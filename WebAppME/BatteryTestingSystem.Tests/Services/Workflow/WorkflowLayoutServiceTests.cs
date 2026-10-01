using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Spec 4.3 and 8. The stale-node tests here are the highest-value tests in the whole feature:
/// a saved layout WILL eventually reference deleted hardware, and the required behaviour is a
/// dimmed node plus a warning, never a thrown exception that kills the Blazor circuit.
/// </summary>
public class WorkflowLayoutServiceTests
{
    private const string User = "user-a";

    // Device 1 / board 10 / channels 100, 101 all exist. Anything else does not.
    private static TopologySnapshot Topology() => new(new[]
    {
        new TopologyDevice(1, "Dev-1", new[]
        {
            new TopologyBoard(10, 1, new[]
            {
                new TopologyChannel(100, 1),
                new TopologyChannel(101, 2),
            }),
        }),
    });

    private static WorkflowGraph GraphWith(params WorkflowNode[] nodes) =>
        WorkflowGraph.Empty with { Nodes = new List<WorkflowNode>(nodes) };

    private static WorkflowNode Node(string id, NodeKind kind, long? entityId) =>
        new(id, kind, entityId, 0, 0, null);

    private static (WorkflowLayoutService Service, FakeWorkflowLayoutRepository Repo) NewService()
    {
        var repo = new FakeWorkflowLayoutRepository();
        return (new WorkflowLayoutService(repo), repo);
    }

    // ============================================================ FindStaleNodeIds (pure)

    [Fact]
    public void FindStaleNodeIds_ReturnsNothingWhenEveryReferenceResolves()
    {
        var graph = GraphWith(
            Node("d", NodeKind.Device, 1),
            Node("b", NodeKind.Board, 10),
            Node("c", NodeKind.Channel, 100));

        Assert.Empty(WorkflowLayoutService.FindStaleNodeIds(graph, Topology()));
    }

    [Fact]
    public void FindStaleNodeIds_FlagsAChannelThatNoLongerExists()
    {
        var graph = GraphWith(
            Node("c-ok", NodeKind.Channel, 100),
            Node("c-gone", NodeKind.Channel, 999));

        var stale = WorkflowLayoutService.FindStaleNodeIds(graph, Topology());

        Assert.Equal(new[] { "c-gone" }, stale);
    }

    [Fact]
    public void FindStaleNodeIds_FlagsDeletedDevicesAndBoardsToo()
    {
        var graph = GraphWith(
            Node("d-gone", NodeKind.Device, 77),
            Node("b-gone", NodeKind.Board, 88));

        var stale = WorkflowLayoutService.FindStaleNodeIds(graph, Topology());

        Assert.Equal(2, stale.Count);
        Assert.Contains("d-gone", stale);
        Assert.Contains("b-gone", stale);
    }

    [Fact]
    public void FindStaleNodeIds_NeverFlagsABatteryNode()
    {
        // Battery nodes reference a BatteryType, not hardware topology, so the topology snapshot
        // cannot say anything about them. Flagging one would dim every battery on the canvas.
        var graph = GraphWith(Node("bat", NodeKind.Battery, 4242));

        Assert.Empty(WorkflowLayoutService.FindStaleNodeIds(graph, Topology()));
    }

    [Fact]
    public void FindStaleNodeIds_HandlesAnEmptyTopology_WithoutThrowing()
    {
        // Happens legitimately: the canvas opens before any device has registered.
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));

        Assert.Equal(new[] { "c" }, WorkflowLayoutService.FindStaleNodeIds(graph, TopologySnapshot.Empty));
    }

    // ============================================================ Save / Load

    [Fact]
    public async Task SaveThenLoad_RoundTripsTheGraph()
    {
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));

        var id = await service.SaveAsync(null, "My layout", "notes", graph, User);
        var loaded = await service.LoadAsync(id, User, Topology());

        Assert.True(loaded.Ok);
        Assert.Null(loaded.Error);
        Assert.Equal(graph.Nodes, loaded.Graph!.Nodes);
        Assert.Empty(loaded.StaleNodeIds);
    }

    [Fact]
    public async Task SaveAsync_WithAnExistingId_UpdatesRatherThanCreating()
    {
        var (service, repo) = NewService();

        var id = await service.SaveAsync(null, "First", null, WorkflowGraph.Empty, User);
        await service.SaveAsync(id, "Renamed", null, WorkflowGraph.Empty, User);

        Assert.Single(repo.Rows);
        Assert.Equal("Renamed", repo.Rows[0].Name);
    }

    [Fact]
    public async Task SaveAsync_StampsTheCurrentSchemaVersion()
    {
        var (service, repo) = NewService();

        await service.SaveAsync(null, "First", null, WorkflowGraph.Empty, User);

        Assert.Equal(WorkflowGraph.CurrentVersion, repo.Rows[0].SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_ReturnsStaleIdsInsteadOfThrowing_WhenHardwareWasDeleted()
    {
        // THE test. A device deleted after the layout was saved must produce a dimmed node.
        var (service, _) = NewService();
        var graph = GraphWith(
            Node("c-ok", NodeKind.Channel, 100),
            Node("c-gone", NodeKind.Channel, 999));

        var id = await service.SaveAsync(null, "Mixed", null, graph, User);
        var loaded = await service.LoadAsync(id, User, Topology());

        Assert.True(loaded.Ok);
        Assert.Equal(2, loaded.Graph!.Nodes.Count);      // nothing is dropped
        Assert.Equal(new[] { "c-gone" }, loaded.StaleNodeIds);
    }

    [Fact]
    public async Task LoadAsync_RefusesALayoutSavedUnderADifferentSchemaVersion()
    {
        var (service, repo) = NewService();
        await repo.UpsertAsync(new WorkflowLayout
        {
            Name = "Ancient",
            OwnerUserId = User,
            SchemaVersion = 99,
            LayoutJson = WorkflowGraphJson.Serialize(WorkflowGraph.Empty with { Version = 99 }),
        });

        var loaded = await service.LoadAsync(repo.Rows[0].Id, User, Topology());

        Assert.False(loaded.Ok);
        Assert.Null(loaded.Graph);
        Assert.Contains("version", loaded.Error!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_ReturnsAnErrorForACorruptedBlob_RatherThanThrowing()
    {
        var (service, repo) = NewService();
        await repo.UpsertAsync(new WorkflowLayout
        {
            Name = "Corrupt",
            OwnerUserId = User,
            SchemaVersion = WorkflowGraph.CurrentVersion,
            LayoutJson = "{ this is not json",
        });

        var loaded = await service.LoadAsync(repo.Rows[0].Id, User, Topology());

        Assert.False(loaded.Ok);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Error));
    }

    [Fact]
    public async Task LoadAsync_ReturnsAnErrorForAMissingOrForeignLayout()
    {
        var (service, _) = NewService();
        var id = await service.SaveAsync(null, "Mine", null, WorkflowGraph.Empty, User);

        var missing = await service.LoadAsync(9999, User, Topology());
        var foreign = await service.LoadAsync(id, "someone-else", Topology());

        Assert.False(missing.Ok);
        Assert.False(foreign.Ok);
    }

    [Fact]
    public async Task ListAsync_ReturnsSummariesForThatUserOnly()
    {
        var (service, _) = NewService();
        await service.SaveAsync(null, "Mine", "d", WorkflowGraph.Empty, User);
        await service.SaveAsync(null, "Theirs", null, WorkflowGraph.Empty, "user-b");

        var list = await service.ListAsync(User);

        Assert.Single(list);
        Assert.Equal("Mine", list[0].Name);
        Assert.Equal("d", list[0].Description);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheLayoutFromTheList()
    {
        var (service, _) = NewService();
        var id = await service.SaveAsync(null, "Doomed", null, WorkflowGraph.Empty, User);

        Assert.True(await service.DeleteAsync(id, User));
        Assert.Empty(await service.ListAsync(User));
    }

    // ============================================================ GetClaimedChannelsAsync

    [Fact]
    public async Task GetClaimedChannelsAsync_MapsAChannelToTheLayoutThatHoldsIt()
    {
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));
        await service.SaveAsync(null, "Alpha", null, graph, User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Equal("Alpha", claimed[100]);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_ExcludesTheCurrentlyOpenLayout()
    {
        // Editing "Alpha" itself must not report its own channels as claimed by someone else.
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));
        var alphaId = await service.SaveAsync(null, "Alpha", null, graph, User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: alphaId);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_CoversEveryOtherSavedLayout()
    {
        var (service, _) = NewService();
        await service.SaveAsync(null, "Alpha", null, GraphWith(Node("c1", NodeKind.Channel, 100)), User);
        await service.SaveAsync(null, "Beta", null, GraphWith(Node("c2", NodeKind.Channel, 200)), User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Equal("Alpha", claimed[100]);
        Assert.Equal("Beta", claimed[200]);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_NeverReturnsAnotherUsersLayouts()
    {
        var (service, _) = NewService();
        await service.SaveAsync(
            null, "Theirs", null, GraphWith(Node("c", NodeKind.Channel, 100)), "user-b");

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_IgnoresNonChannelNodes()
    {
        var (service, _) = NewService();
        await service.SaveAsync(
            null, "Alpha", null,
            GraphWith(Node("d", NodeKind.Device, 1), Node("b", NodeKind.Board, 10)), User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Empty(claimed);
    }
}
