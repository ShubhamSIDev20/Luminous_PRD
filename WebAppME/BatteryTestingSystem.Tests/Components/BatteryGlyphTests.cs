using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.Enums;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The glyph's entire API is CSS custom properties, so that the telemetry bridge can update a
/// battery by writing a variable instead of causing a Blazor re-render. These tests pin that
/// contract — particularly that fill is expressed as --wf-soc for a scaleY transform rather than
/// as a height, which would repaint every frame.
/// </summary>
public class BatteryGlyphTests : TestContext
{
    [Fact]
    public void RendersOneCellByDefault()
    {
        var cut = RenderComponent<BatteryGlyph>();

        Assert.Single(cut.FindAll(".wf-cell"));
    }

    [Fact]
    public void RendersAPackOfCellsWhenAsked()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.CellCount, 4));

        Assert.Equal(4, cut.FindAll(".wf-cell").Count);
    }

    [Fact]
    public void ExposesStateOfChargeAsTheWfSocVariable()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 0.42));

        var style = cut.Find(".wf-battery").GetAttribute("style") ?? "";
        Assert.Contains("--wf-soc: 0.42", style);
    }

    [Fact]
    public void ClampsStateOfChargeIntoZeroToOne()
    {
        // Telemetry can legitimately report a slightly out-of-range SoC. A value above 1 would
        // overflow the cell outline; a negative one would flip the transform.
        var high = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 1.8));
        var low = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, -0.5));

        Assert.Contains("--wf-soc: 1", high.Find(".wf-battery").GetAttribute("style"));
        Assert.Contains("--wf-soc: 0", low.Find(".wf-battery").GetAttribute("style"));
    }

    [Fact]
    public void UsesTheCorrectedStatusVariable_NotTheMisspelledEnumName()
    {
        // CircuitStatus.Countinue -> --status-continue. The naive ToString().ToLower() would
        // emit --status-countinue, which is declared nowhere.
        var cut = RenderComponent<BatteryGlyph>(p => p
            .Add(x => x.Status, CircuitStatus.Countinue));

        var style = cut.Find(".wf-battery").GetAttribute("style") ?? "";
        Assert.Contains("--status-continue", style);
        Assert.DoesNotContain("countinue", style);
    }

    [Fact]
    public void MarksChargingSoTheShimmerCanRun()
    {
        var idle = RenderComponent<BatteryGlyph>(p => p.Add(x => x.IsCharging, false));
        var charging = RenderComponent<BatteryGlyph>(p => p.Add(x => x.IsCharging, true));

        Assert.DoesNotContain("wf-battery--charging", idle.Find(".wf-battery").ClassName);
        Assert.Contains("wf-battery--charging", charging.Find(".wf-battery").ClassName);
    }

    [Fact]
    public void ShowsTheFaultTreatmentOnError()
    {
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.Status, CircuitStatus.Error));

        Assert.Contains("wf-battery--fault", cut.Find(".wf-battery").ClassName);
    }

    [Fact]
    public void NeverUsesAnAnimatedHeightOrBoxShadowInline()
    {
        // Guards the performance decision at the point where it is easiest to undo by accident.
        var cut = RenderComponent<BatteryGlyph>(p => p.Add(x => x.StateOfCharge, 0.6));

        var markup = cut.Markup;
        Assert.DoesNotContain("height: 60%", markup);
        Assert.DoesNotContain("box-shadow:", markup);
    }
}
