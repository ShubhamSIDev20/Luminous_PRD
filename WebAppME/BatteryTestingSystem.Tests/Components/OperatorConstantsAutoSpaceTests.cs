using BatteryTestingSystem.Components.UI;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// AutoSpace runs at confirm-time on the Limit input in the Program Editor (LimitActionPairEditor's
/// ConfirmAddPair/ConfirmLimitEdit) - typing "6 minute" and confirming turns it into "6 min" via
/// FindNearestUnit's "shorten a verbose unit" match. PERCAh needs the opposite direction: "130 per"
/// is a truncated PREFIX of the unit, not a verbose superset of it, so it needs its own fallback -
/// this pins both directions and that ambiguous prefixes are left alone.
/// </summary>
public class OperatorConstantsAutoSpaceTests
{
    [Theory]
    [InlineData("6 minute", "6 min")]      // pre-existing "shorten" direction - regression pin
    [InlineData("> 60 minute", "> 60 min")]
    public void AutoSpace_ShortensAVerboseUnitDownToItsCanonicalForm(string input, string expected)
    {
        Assert.Equal(expected, OperatorConstants.AutoSpace(input));
    }

    [Theory]
    [InlineData("130 per", "130 PERCAh")]
    [InlineData("> 130 per", "> 130 PERCAh")]
    [InlineData("130 PER", "130 PERCAh")]
    public void AutoSpace_CompletesATruncatedUnitPrefix_WhenTheCompletionIsUnambiguous(string input, string expected)
    {
        Assert.Equal(expected, OperatorConstants.AutoSpace(input));
    }
}
