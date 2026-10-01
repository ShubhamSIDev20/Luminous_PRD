using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// SoC is DERIVED - the hardware sends capacity in Ah, never a percentage. Net coulomb count
/// against the battery's rated capacity, chosen over an AccumulatedCapacity-only variant and
/// voltage interpolation (spec 6.5.1).
///
/// The null cases are the point of the return type: a bench operator must be able to tell
/// "we do not know this cell's charge" from "this cell is flat".
/// </summary>
public class StateOfChargeEstimatorTests
{
    [Fact]
    public void HalfTheRatedCapacityMovedInIsHalfCharged()
    {
        Assert.Equal(0.5, StateOfChargeEstimator.Estimate(25, 0, 50));
    }

    [Fact]
    public void DischargeIsSubtractedFromCharge()
    {
        // 40 Ah in, 15 Ah back out, on a 50 Ah cell = 25/50.
        Assert.Equal(0.5, StateOfChargeEstimator.Estimate(40, 15, 50));
    }

    [Fact]
    public void ClampsAboveFull()
    {
        Assert.Equal(1.0, StateOfChargeEstimator.Estimate(80, 0, 50));
    }

    [Fact]
    public void ClampsBelowEmptyWhenMoreWasDrawnThanPutIn()
    {
        Assert.Equal(0.0, StateOfChargeEstimator.Estimate(10, 30, 50));
    }

    [Fact]
    public void NoBatteryMeansUnknownNotEmpty()
    {
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, null));
    }

    [Fact]
    public void ZeroRatedCapacityMeansUnknownNotEmpty()
    {
        // BatteryDTO.NominalCapacity defaults to 0, so an unconfigured battery record hits this
        // path. Dividing by it would render a confident, wrong 0%.
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, 0f));
    }

    [Fact]
    public void NegativeRatedCapacityMeansUnknown()
    {
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, -5f));
    }
}
