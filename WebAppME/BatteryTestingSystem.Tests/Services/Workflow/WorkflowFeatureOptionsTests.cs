using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The flags decide whether the experiment is visible at all and whether the old dashboard is
/// reachable from the menu. A missing config section must default to "old dashboard on, canvas
/// off" so that a deployment without the section behaves exactly like main.
/// </summary>
public class WorkflowFeatureOptionsTests
{
    private static WorkflowFeatureOptions Bind(params (string Key, string Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

        var options = new WorkflowFeatureOptions();
        config.GetSection(WorkflowFeatureOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Defaults_HideTheCanvasAndKeepTheLegacyDashboard()
    {
        var options = Bind();

        Assert.False(options.WorkflowCanvas);
        Assert.True(options.LegacyDashboard);
    }

    [Fact]
    public void ReadsBothFlagsFromTheFeaturesSection()
    {
        var options = Bind(
            ("Features:WorkflowCanvas", "true"),
            ("Features:LegacyDashboard", "false"));

        Assert.True(options.WorkflowCanvas);
        Assert.False(options.LegacyDashboard);
    }

    [Fact]
    public void AnAbsentFlagKeepsItsDefault_WhenTheOtherIsSet()
    {
        var options = Bind(("Features:WorkflowCanvas", "true"));

        Assert.True(options.WorkflowCanvas);
        Assert.True(options.LegacyDashboard);
    }
}
