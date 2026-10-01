using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The bar itself makes no eligibility decisions - it renders whatever IsActionEnabled reports
/// (WorkflowActionRules, already tested) and reports which button was clicked. These tests only
/// exercise that wiring.
/// </summary>
public class SelectionActionBarTests : TestContext
{
    [Fact]
    public void ShowsTheSelectionCount()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 5)
            .Add(x => x.IsActionEnabled, (string _) => true));

        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public void DisablesAButtonWhenIsActionEnabledReturnsFalse()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string action) => action != "start"));

        Assert.True(cut.Find("button[data-action='start']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[data-action='stop']").HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingAnEnabledButtonRaisesOnActionWithItsName()
    {
        string? captured = null;

        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string _) => true)
            .Add(x => x.OnAction, (string a) => captured = a));

        cut.Find("button[data-action='stop']").Click();

        Assert.Equal("stop", captured);
    }

    [Fact]
    public void RendersAllFiveActions()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string _) => true));

        foreach (var action in new[] { "start", "stop", "pause", "continue", "transfer" })
            Assert.NotNull(cut.Find($"button[data-action='{action}']"));
    }
}
