using System.Linq;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The picker is a thin UI over WorkflowNodeConfig.Toggle - these tests exercise the wiring
/// (checkbox click -> ConfigChanged fires with the toggled config), not the cap rule itself
/// (already covered by WorkflowNodeConfigTests).
/// </summary>
public class NodePropertyPickerTests : TestContext
{
    [Fact]
    public void RendersOneCheckboxPerAvailableProperty()
    {
        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, WorkflowNodeConfig.Default));

        Assert.Equal(WorkflowNodeProperties.AvailableKeys.Count, cut.FindAll("input[type=checkbox]").Count);
    }

    [Fact]
    public void ChecksExactlyTheConfiguredProperties()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, config));

        var voltageCheckbox = cut.Find($"input[data-key='Voltage']");
        var currentCheckbox = cut.Find($"input[data-key='Current']");

        Assert.True(voltageCheckbox.HasAttribute("checked"));
        Assert.False(currentCheckbox.HasAttribute("checked"));
    }

    [Fact]
    public void CheckingABoxRaisesConfigChangedWithTheToggledConfig()
    {
        WorkflowNodeConfig? captured = null;
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var cut = RenderComponent<NodePropertyPicker>(p => p
            .Add(x => x.Config, config)
            .Add(x => x.ConfigChanged, (WorkflowNodeConfig c) => captured = c));

        cut.Find("input[data-key='Current']").Change(true);

        Assert.Equal(new[] { "Voltage", "Current" }, captured!.VisibleProperties);
    }

    [Fact]
    public void UncheckingABoxRemovesThatPropertyOnly()
    {
        WorkflowNodeConfig? captured = null;
        var config = new WorkflowNodeConfig(new[] { "Voltage", "Current" });

        var cut = RenderComponent<NodePropertyPicker>(p => p
            .Add(x => x.Config, config)
            .Add(x => x.ConfigChanged, (WorkflowNodeConfig c) => captured = c));

        cut.Find("input[data-key='Voltage']").Change(false);

        Assert.Equal(new[] { "Current" }, captured!.VisibleProperties);
    }

    [Fact]
    public void DisablesEveryUncheckedBoxOnceAtTheCap()
    {
        var atCap = new WorkflowNodeConfig(WorkflowNodeProperties.AvailableKeys.Take(WorkflowNodeProperties.MaxVisible).ToList());

        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, atCap));

        var uncheckedBoxes = cut.FindAll("input[type=checkbox]")
            .Where(el => !el.HasAttribute("checked"));

        Assert.All(uncheckedBoxes, el => Assert.True(el.HasAttribute("disabled")));
    }

    [Fact]
    public void ShowsTheSharedCatalogsLabelNotTheRawKey()
    {
        var cut = RenderComponent<NodePropertyPicker>(p => p.Add(x => x.Config, WorkflowNodeConfig.Default));

        Assert.Contains(WorkflowNodeProperties.Label("Voltage"), cut.Markup);
    }
}
