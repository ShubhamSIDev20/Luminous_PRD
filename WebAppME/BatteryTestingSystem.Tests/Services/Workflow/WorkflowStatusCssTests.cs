using System;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// CircuitStatus.Countinue is misspelled in the enum while the CSS variable is
/// --status-continue. Deriving the name with ToString().ToLower() produces "countinue", which
/// matches nothing and renders an unstyled node with no error in the console or the build.
/// This mapping is the only place that discrepancy is allowed to exist.
/// </summary>
public class WorkflowStatusCssTests
{
    [Theory]
    [InlineData(CircuitStatus.Idle, "idle")]
    [InlineData(CircuitStatus.Charge, "charge")]
    [InlineData(CircuitStatus.Discharging, "discharging")]
    [InlineData(CircuitStatus.Pause, "pause")]
    [InlineData(CircuitStatus.Countinue, "continue")]     // the misspelling, corrected
    [InlineData(CircuitStatus.Interrupt, "interrupt")]
    [InlineData(CircuitStatus.Error, "error")]
    [InlineData(CircuitStatus.Msg, "msg")]
    [InlineData(CircuitStatus.Offline, "offline")]
    public void Name_MapsEveryStatusToItsCssToken(CircuitStatus status, string expected)
    {
        Assert.Equal(expected, WorkflowStatusCss.Name(status));
    }

    [Fact]
    public void Var_WrapsTheTokenInTheThemeVariableName()
    {
        Assert.Equal("--status-continue", WorkflowStatusCss.Var(CircuitStatus.Countinue));
    }

    [Fact]
    public void EveryEnumMemberIsMapped_SoANewStatusCannotSlipThroughUnstyled()
    {
        // If someone adds a status to the enum without adding it here, this fails loudly
        // instead of shipping a node with no colour.
        foreach (CircuitStatus status in Enum.GetValues<CircuitStatus>())
        {
            var name = WorkflowStatusCss.Name(status);
            Assert.False(string.IsNullOrWhiteSpace(name), $"{status} has no CSS token");
        }
    }

    [Fact]
    public void EveryMappedTokenExistsInTheStylesheet()
    {
        // Guards the other half of the pairing: a token with no matching CSS variable is just
        // as invisible as no token at all. Reads app.css because that is where the theme vars
        // are declared (both files carry the same set - ADR-2).
        var cssPath = System.IO.Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot", "css", "app.css");

        if (!System.IO.File.Exists(cssPath)) return;   // skip if the layout differs on CI

        var css = System.IO.File.ReadAllText(cssPath);

        foreach (CircuitStatus status in Enum.GetValues<CircuitStatus>())
        {
            var variable = WorkflowStatusCss.Var(status);
            Assert.True(css.Contains(variable + ":"), $"{variable} is not declared in app.css");
        }
    }
}
