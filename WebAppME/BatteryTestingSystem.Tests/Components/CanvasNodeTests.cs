using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// At the smallest level-of-detail tier (spec section 5) a node's header/body text is hidden by
/// CSS and it renders as a plain colour tile — the title attribute is what lets an operator still
/// identify one on hover, since the CSS work itself is not something a unit test can see.
/// </summary>
public class CanvasNodeTests : TestContext
{
    [Fact]
    public void RootElementCarriesATitleAttributeForLodTileHoverIdentification()
    {
        var node = new WorkflowNode("chn-1", NodeKind.Channel, 1, 0, 0, null);

        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, node)
            .Add(x => x.Title, "1-8-2"));

        Assert.Equal("1-8-2", cut.Find(".wf-node").GetAttribute("title"));
    }

    // ============================================================ HeaderContent
    //
    // The channel battery cell stamps its channel number on a cap that replaces the default title
    // row. It goes through this slot rather than replacing the header ELEMENT, because
    // workflow-canvas.js:373 starts a node drag only on e.target.closest(".wf-node__header") - a
    // cap rendered outside that element makes the node undraggable with no error anywhere, and no
    // unit test can see it.

    [Fact]
    public void WithoutACustomHeaderTheTitleStillRenders()
    {
        var node = new WorkflowNode("chn-1", NodeKind.Channel, 1, 0, 0, null);

        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, node)
            .Add(x => x.Title, "1-1-1"));

        Assert.Contains("1-1-1", cut.Find(".wf-node__header").TextContent);
    }

    [Fact]
    public void ACustomHeaderReplacesTheTitleRowInsideTheDragHandle()
    {
        var node = new WorkflowNode("chn-1", NodeKind.Channel, 1, 0, 0, null);

        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, node)
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.HeaderContent, b => b.AddMarkupContent(0, "<span class=\"cap\">CAP</span>")));

        var header = cut.Find(".wf-node__header");
        Assert.NotNull(header.QuerySelector(".cap"));
        Assert.DoesNotContain("1-1-1", header.TextContent);
    }

    [Fact]
    public void ACustomHeaderStillSuppressesNothingElseOnTheNode()
    {
        // Ports and the selection ring are shared chrome; a custom header must not disturb them.
        var node = new WorkflowNode("chn-1", NodeKind.Channel, 1, 0, 0, null);

        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, node)
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.IsSelected, true)
            .Add(x => x.HeaderContent, b => b.AddMarkupContent(0, "<span>CAP</span>")));

        Assert.NotNull(cut.Find(".wf-port--in"));
        Assert.NotNull(cut.Find(".wf-port--out"));
        Assert.Contains("wf-node--selected", cut.Find(".wf-node").ClassList);
    }
}
