using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Models;

/// <summary>
/// The picker's whole interaction model is one method: toggling a key either adds, removes, or
/// (at the cap) refuses. Testing this independently of any UI is what makes the 6-cap a
/// guaranteed invariant rather than something a checkbox's disabled state merely suggests.
/// </summary>
public class WorkflowNodeConfigTests
{
    [Fact]
    public void Default_MatchesTheCatalogsDefault()
    {
        Assert.Equal(WorkflowNodeProperties.DefaultVisibleProperties, WorkflowNodeConfig.Default.VisibleProperties);
    }

    [Fact]
    public void Default_HasA250MillisecondRefreshRate()
    {
        Assert.Equal(250, WorkflowNodeConfig.Default.RefreshRateMs);
    }

    [Fact]
    public void Toggle_AddsAnAbsentKey()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var result = config.Toggle("Current");

        Assert.Equal(new[] { "Voltage", "Current" }, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_RemovesAPresentKey()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage", "Current" });

        var result = config.Toggle("Voltage");

        Assert.Equal(new[] { "Current" }, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_RefusesToAddPastTheCap()
    {
        var atCap = new WorkflowNodeConfig(
            Enumerable.Range(0, WorkflowNodeProperties.MaxVisible).Select(i => $"k{i}").ToList());

        var result = atCap.Toggle("one-more");

        Assert.Equal(atCap.VisibleProperties, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_StillAllowsRemovingWhenAtTheCap()
    {
        var atCap = new WorkflowNodeConfig(
            Enumerable.Range(0, WorkflowNodeProperties.MaxVisible).Select(i => $"k{i}").ToList());

        var result = atCap.Toggle("k0");

        Assert.Equal(WorkflowNodeProperties.MaxVisible - 1, result.VisibleProperties.Count);
        Assert.DoesNotContain("k0", result.VisibleProperties);
    }

    [Fact]
    public void Toggle_TogglingTheSameKeyTwiceIsANoOp()
    {
        var config = new WorkflowNodeConfig(new[] { "Voltage" });

        var result = config.Toggle("Current").Toggle("Current");

        Assert.Equal(config.VisibleProperties, result.VisibleProperties);
    }

    [Fact]
    public void Toggle_PreservesOrder_NewestLast()
    {
        var config = WorkflowNodeConfig.Default.Toggle("CycleNumber");

        Assert.Equal("CycleNumber", config.VisibleProperties[^1]);
    }
}
