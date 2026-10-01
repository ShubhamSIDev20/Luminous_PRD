using System.Collections.Generic;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The dock is the only way to attach a program or DBC, so its gating is a real rule and not
/// cosmetic: those pickers must not appear for a device, board or battery node.
/// </summary>
public class PropertiesDockTests : TestContext
{
    private static PaletteCatalog Catalog() => new(
        new List<PaletteProgram> { new(1, "Cycle-A"), new(2, "Cycle-B") },
        new List<PaletteDbc> { new(5, "pack.dbc") },
        new List<PaletteBatteryType> { new(9, "LFP 48V") });

    private static WorkflowNode Node(NodeKind kind = NodeKind.Channel, NodeAttachment? attach = null) =>
        new("n1", kind, 100, 0, 0, attach);

    [Fact]
    public void ShowsAnEmptyStateWhenNothingIsSelected()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, (WorkflowNode?)null)
            .Add(x => x.Catalog, Catalog()));

        Assert.Contains("Select a node", cut.Markup);
    }

    [Fact]
    public void ShowsProgramAndDbcPickersForAChannel()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog()));

        Assert.NotNull(cut.Find("select[data-role='program']"));
        Assert.NotNull(cut.Find("select[data-role='dbc']"));
    }

    [Theory]
    [InlineData(NodeKind.Device)]
    [InlineData(NodeKind.Board)]
    [InlineData(NodeKind.Battery)]
    public void HidesThePickersForEveryNonChannelNode(NodeKind kind)
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(kind))
            .Add(x => x.Catalog, Catalog()));

        Assert.Empty(cut.FindAll("select[data-role='program']"));
        Assert.Empty(cut.FindAll("select[data-role='dbc']"));
    }

    [Fact]
    public void PreselectsTheCurrentlyAttachedProgram()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(attach: new NodeAttachment(2, null, null)))
            .Add(x => x.Catalog, Catalog()));

        Assert.Equal("2", cut.Find("select[data-role='program']").GetAttribute("value"));
    }

    [Fact]
    public void ChangingTheProgramRaisesTheCallbackWithTheNewAttachment()
    {
        NodeAttachment? captured = null;

        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.OnAttachmentChanged, (NodeAttachment? a) => captured = a));

        cut.Find("select[data-role='program']").Change("1");

        Assert.Equal(1, captured!.ProgramId);
    }

    [Fact]
    public void ClearingTheProgramKeepsTheDbcAttachment()
    {
        // Selecting the blank option must clear one field, not wipe the whole attachment.
        NodeAttachment? captured = null;

        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node(attach: new NodeAttachment(1, 5, null)))
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.OnAttachmentChanged, (NodeAttachment? a) => captured = a));

        cut.Find("select[data-role='program']").Change("");

        Assert.Null(captured!.ProgramId);
        Assert.Equal(5, captured.DbcFileId);
    }

    [Fact]
    public void WarnsWhenTheSelectedNodeIsStale()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog())
            .Add(x => x.IsStale, true));

        Assert.Contains("no longer exists", cut.Markup);
    }

    // ============================================================ Live-data duplication removed
    //
    // The dock used to render a second copy of every measurement ("Live data"), written by
    // workflow-canvas.js the same way it writes the channel node face. Once the node became a
    // battery cell showing all 28 fields itself, that block was pure duplication and was deleted
    // along with its JS writer (applyDockLiveData). This test pins the deletion so it cannot
    // silently come back.

    [Fact]
    public void NeverRendersTheRemovedLiveDataContainer()
    {
        var cut = RenderComponent<PropertiesDock>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Catalog, Catalog()));

        Assert.Empty(cut.FindAll(".wf-dock__live"));
        Assert.Empty(cut.FindAll("[data-role^='dock-']"));
    }
}
