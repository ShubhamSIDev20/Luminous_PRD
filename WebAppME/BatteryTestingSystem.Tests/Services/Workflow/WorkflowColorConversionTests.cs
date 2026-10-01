using System;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// The colour input speaks #rrggbb; the whole canvas speaks the raw HSL triplets the theme uses.
/// If this round-trip drifts, a user's chosen colour silently becomes a different one on reload -
/// the kind of bug nobody reports precisely, so it gets pinned here instead.
/// </summary>
public class WorkflowColorConversionTests
{
    [Theory]
    [InlineData("0 0% 0%", "#000000")]
    [InlineData("0 0% 100%", "#ffffff")]
    [InlineData("0 100% 50%", "#ff0000")]
    [InlineData("120 100% 50%", "#00ff00")]
    [InlineData("240 100% 50%", "#0000ff")]
    public void HslToHex_ConvertsKnownColours(string hsl, string expected)
    {
        Assert.Equal(expected, WorkflowColorConversion.HslToHex(hsl));
    }

    [Theory]
    [InlineData("#ff0000", "0 100% 50%")]
    [InlineData("#00ff00", "120 100% 50%")]
    [InlineData("#0000ff", "240 100% 50%")]
    [InlineData("#000000", "0 0% 0%")]
    public void HexToHsl_ConvertsKnownColours(string hex, string expected)
    {
        Assert.Equal(expected, WorkflowColorConversion.HexToHsl(hex));
    }

    [Fact]
    public void RoundTrip_PreservesEveryColourInTheBuiltInPalette()
    {
        // The real risk: a user opens the legend (HSL -> hex), changes nothing, and the picker's
        // change event writes back a subtly different colour (hex -> HSL). Every default must
        // survive the trip unchanged.
        foreach (CircuitStatus status in Enum.GetValues(typeof(CircuitStatus)))
        {
            var original = WorkflowTelemetryBridge.HslFor(status);
            var roundTripped = WorkflowColorConversion.HexToHsl(
                WorkflowColorConversion.HslToHex(original));

            Assert.Equal(original, roundTripped);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a colour")]
    [InlineData("300 80%")]
    public void HslToHex_FallsBackForUnparseableInput(string? bad)
    {
        Assert.Equal("#000000", WorkflowColorConversion.HslToHex(bad));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ff0000")]
    [InlineData("#fff")]
    public void HexToHsl_FallsBackForUnparseableInput(string? bad)
    {
        Assert.Equal("0 0% 62%", WorkflowColorConversion.HexToHsl(bad));
    }
}
