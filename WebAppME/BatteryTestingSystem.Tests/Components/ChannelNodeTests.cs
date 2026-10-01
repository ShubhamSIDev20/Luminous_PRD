using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The channel node is a battery cell. Its interior mirrors the dashboard card's RenderNormal
/// arrangement: Primary Data as two UNLABELLED columns (each realtime measurement is identified by
/// its unit token - Ah vs AhCha vs AhDch vs AhStep), Configuration and Program Data as LABELLED
/// rows, because their values carry no unit and mean nothing without a name.
///
/// Every live value is written by workflow-canvas.js into a data-role slot, never by a Blazor
/// re-render, so these tests assert the SLOTS exist and start as placeholders. What they cannot
/// see - the fill geometry, the segment march, whether text stays inside the outline - is covered
/// by the live-verification pass.
/// </summary>
public class ChannelNodeTests : TestContext
{
    private static WorkflowNode Node() => new("chn-1", NodeKind.Channel, 1, 0, 0, null);

    private IRenderedComponent<ChannelNode> Render(params string[] properties) =>
        RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.SelectedProperties, properties.ToList()));

    [Fact]
    public void TheChannelNumberIsStampedOnTheCapInsideTheDragHandle()
    {
        var cut = Render("Voltage");

        // Inside .wf-node__header specifically: that is the selector workflow-canvas.js gates
        // node dragging on.
        var header = cut.Find(".wf-node__header");
        Assert.Contains("1-1-1", header.TextContent);
        Assert.NotNull(header.QuerySelector(".wf-bcap"));
    }

    [Fact]
    public void FixedSlotsArePresentRegardlessOfSelection()
    {
        var cut = Render();

        Assert.NotNull(cut.Find("[data-role='cell']"));
        Assert.NotNull(cut.Find("[data-role='circuit-status']"));
        Assert.NotNull(cut.Find("[data-role='soc']"));
        Assert.NotNull(cut.Find("[data-role='battery-line']"));
        Assert.NotNull(cut.Find("[data-role='program-status']"));
        Assert.NotNull(cut.Find("[data-role='last-update']"));
    }

    [Fact]
    public void TheBatteryLineStripIsReservedEvenWithNoBattery()
    {
        // Height must not depend on whether a battery happens to be attached: the layout computes
        // ChannelNodeHeight from the property list alone, before any telemetry exists. A
        // conditional strip would desynchronise edge attachment on a board where only some
        // channels have batteries.
        var cut = Render();

        Assert.NotNull(cut.Find("[data-role='battery-line']"));
    }

    [Fact]
    public void EverySelectedKeyGetsASlotInItsOwnSection()
    {
        var cut = Render("Voltage", "BatteryID", "CycleNumber");

        Assert.NotNull(cut.Find(".wf-bcell__grid2").QuerySelector("[data-role='prop-Voltage']"));

        var rows = cut.FindAll(".wf-bcell__row");
        Assert.Contains(rows, r => r.QuerySelector("[data-role='prop-BatteryID']") is not null);
        Assert.Contains(rows, r => r.QuerySelector("[data-role='prop-CycleNumber']") is not null);
    }

    [Fact]
    public void PrimaryDataValuesCarryNoVisibleLabelAndAlternateAlignment()
    {
        var cut = Render("Voltage", "Current");

        var cells = cut.FindAll(".wf-bcell__grid2 > *");
        Assert.Equal(2, cells.Count);
        Assert.Contains("wf-bcell__v--left", cells[0].ClassList);
        Assert.Contains("wf-bcell__v--right", cells[1].ClassList);

        // The label is on the title attribute for hover, never as visible text.
        Assert.DoesNotContain("Voltage", cut.Find(".wf-bcell__grid2").TextContent);
    }

    [Fact]
    public void ConfigurationAndProgramRowsShowAVisibleLabelWithoutItsParenthesisedUnit()
    {
        var cut = Render("BatteryID", "StepRunningTime");

        var text = string.Join("|", cut.FindAll(".wf-bcell__row").Select(r => r.TextContent));
        Assert.Contains("Battery ID", text);
        Assert.Contains("Step Time", text);
    }

    [Fact]
    public void SectionHeadingsOnlyAppearForSectionsThatHaveKeys()
    {
        var cut = Render("Voltage");

        var headings = cut.FindAll(".wf-bcell__sec").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Primary Data" }, headings);
    }

    [Fact]
    public void AllThreeHeadingsAppearWhenAllThreeSectionsHaveKeys()
    {
        var cut = Render("Voltage", "BatteryID", "CycleNumber");

        var headings = cut.FindAll(".wf-bcell__sec").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Primary Data", "Configuration", "Program Data" }, headings);
    }

    [Fact]
    public void AnEmptySelectionRendersNoSectionsAndNoPropertySlots()
    {
        var cut = Render();

        Assert.Empty(cut.FindAll(".wf-bcell__sec"));
        Assert.Empty(cut.FindAll("[data-role^='prop-']"));
    }

    [Fact]
    public void PropertySlotsStartAsPlaceholders()
    {
        var cut = Render("Voltage");

        Assert.Equal("--", cut.Find("[data-role='prop-Voltage']").TextContent);
    }

    [Fact]
    public void PrimaryDataRendersTheValueSlotOnlyAndNeverItsOwnUnit()
    {
        // The unit is ALREADY part of the value the bridge writes ("0.164 AhCha"). Rendering
        // UnitFor(key) beside it shipped "0.164 AhChaAhCha" to the canvas - caught live, not by a
        // unit test, which is why this one exists now.
        var cut = Render("ChargeCapacity");

        var cell = cut.Find(".wf-bcell__grid2 > *");
        Assert.Equal("--", cell.TextContent.Trim());
        Assert.DoesNotContain("AhCha", cell.InnerHtml);
        Assert.Equal("prop-ChargeCapacity", cell.GetAttribute("data-role"));
    }

    [Fact]
    public void PrimaryDataCarriesTheFullNameOnTheTitleForHover()
    {
        // With no visible label, the title is the only way to learn what a value is.
        var cut = Render("ChargeCapacity");

        Assert.Equal("Charge Capacity", cut.Find(".wf-bcell__grid2 > *").GetAttribute("title"));
    }

    [Fact]
    public void ProgramAndDbcBadgesRenderOutsideTheCell()
    {
        // Deliberately outside .wf-bcell: they are conditional, and anything conditional inside
        // the cell would make its height depend on state the layout cannot see.
        var cut = RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.ProgramName, "CC-CV 0.5C")
            .Add(x => x.DbcName, "pack.dbc"));

        Assert.Equal(2, cut.FindAll(".wf-badge").Count);
        Assert.Null(cut.Find(".wf-bcell").QuerySelector(".wf-badge"));
    }
}
