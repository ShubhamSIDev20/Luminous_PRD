using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

public class JumpStepValidatorTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 10)]     // upper bound is inclusive
    [InlineData(5, 10)]
    [InlineData(1, null)]    // total steps unknown - only the lower bound is checked
    [InlineData(65535, null)]
    public void IsValid_AcceptsInBoundsStepNumbers(int stepNumber, int? totalSteps)
    {
        Assert.True(JumpStepValidator.IsValid(stepNumber, totalSteps));
    }

    [Theory]
    [InlineData(null, 10)]   // nothing entered
    [InlineData(0, 10)]      // steps are 1-based
    [InlineData(-1, 10)]
    [InlineData(11, 10)]     // past the loaded program's last step
    [InlineData(65536, null)] // past the wire format's 2-byte ceiling
    public void IsValid_RejectsOutOfBoundsStepNumbers(int? stepNumber, int? totalSteps)
    {
        Assert.False(JumpStepValidator.IsValid(stepNumber, totalSteps));
    }

    [Fact]
    public void IsValid_TreatsAZeroOrNegativeTotalStepsAsUnknown()
    {
        // ProgramDTO.ProgramSteps can be 0/negative for an unloaded program - that must not be
        // read as "the program has 0 steps, reject everything", only the lower bound applies.
        Assert.True(JumpStepValidator.IsValid(1, 0));
        Assert.True(JumpStepValidator.IsValid(100, -1));
    }
}
