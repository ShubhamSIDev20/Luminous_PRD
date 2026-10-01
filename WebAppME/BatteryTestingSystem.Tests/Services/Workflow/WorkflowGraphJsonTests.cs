using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The layout document is stored as an opaque JSON blob in one column, so serialization IS the
/// schema. These tests are the only thing standing between a renamed property and every saved
/// layout silently losing data.
/// </summary>
public class WorkflowGraphJsonTests
{
    private static WorkflowGraph Sample() => new(
        Version: WorkflowGraph.CurrentVersion,
        Nodes: new List<WorkflowNode>
        {
            new("n1", NodeKind.Device,  10,   0, 0, null),
            new("n2", NodeKind.Board,   20, 240, 0, null),
            new("n3", NodeKind.Channel, 30, 480, 0, new NodeAttachment(7, 8, null)),
            new("n4", NodeKind.Battery, 40, 720, 0, new NodeAttachment(null, null, 9)),
        },
        Edges: new List<WorkflowEdge>
        {
            new("e1", "n1", "n2", EdgeKind.Topology),
            new("e2", "n4", "n3", EdgeKind.Power),
        },
        Viewport: new CanvasViewport(-100, 50, 0.75));

    [Fact]
    public void RoundTrip_PreservesEveryField()
    {
        var original = Sample();

        Assert.True(WorkflowGraphJson.TryDeserialize(
            WorkflowGraphJson.Serialize(original), out var restored, out var error));

        Assert.Null(error);
        Assert.Equal(original.Version, restored!.Version);
        Assert.Equal(original.Viewport, restored.Viewport);
        Assert.Equal(original.Nodes, restored.Nodes);   // records compare structurally
        Assert.Equal(original.Edges, restored.Edges);
    }

    [Fact]
    public void RoundTrip_KeepsANullAttachmentDistinctFromAPartiallyFilledOne()
    {
        // "no configuration at all" and "a DBC but no program" are different states.
        // Conflating them would silently discard a user's DBC-only setup.
        var graph = Sample() with
        {
            Nodes = new List<WorkflowNode>
            {
                new("a", NodeKind.Channel, 1, 0, 0, null),
                new("b", NodeKind.Channel, 2, 0, 0, new NodeAttachment(null, 5, null)),
            }
        };

        Assert.True(WorkflowGraphJson.TryDeserialize(
            WorkflowGraphJson.Serialize(graph), out var restored, out _));

        Assert.Null(restored!.Nodes[0].Attach);
        Assert.Equal(5, restored.Nodes[1].Attach!.DbcFileId);
        Assert.Null(restored.Nodes[1].Attach!.ProgramId);
    }

    [Fact]
    public void Deserialize_RefusesAMismatchedSchemaVersion_WithoutThrowing()
    {
        var json = WorkflowGraphJson.Serialize(Sample() with { Version = 99 });

        Assert.False(WorkflowGraphJson.TryDeserialize(json, out var restored, out var error));

        Assert.Null(restored);
        Assert.Contains("version", error!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"Nodes\": [ }")]
    public void Deserialize_ReturnsFalseOnGarbage_RatherThanThrowing(string junk)
    {
        // A corrupted blob must degrade to "this layout will not open", never to an exception
        // that takes the whole Blazor circuit down.
        Assert.False(WorkflowGraphJson.TryDeserialize(junk, out var restored, out var error));
        Assert.Null(restored);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void EnumsAreWrittenAsNames_SoAReorderedEnumCannotCorruptSavedLayouts()
    {
        var json = WorkflowGraphJson.Serialize(Sample());

        Assert.Contains("\"Battery\"", json);
        Assert.Contains("\"Power\"", json);
    }
}
