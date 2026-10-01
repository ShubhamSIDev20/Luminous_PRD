using System.Collections.Generic;
using BatteryTestingSystem.Components.UI;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Covers the two new user-facing rules from the LOCKAh/PERCAh feature: LOCKAh's multiplier must
/// be >= 1.0 (stricter than the upstream wire spec's own "> 0"), and PERCAh's limit value must be
/// > 0. Both are WebAppME-only editor rules - nothing on the wire enforces them.
/// </summary>
public class ValidationHelperLockAhPercAhTests
{
    private static HashSet<string> NoLabels => new();
    private static List<GlobalVariable> NoVars => new();

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("2")]
    public void ValidateNominal_LockAh_AcceptsAMultiplierOfOneOrGreater(string input)
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.LOCKAH, NoLabels, NoVars);

        Assert.True(isValid, error);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("0")]
    [InlineData("-1")]
    public void ValidateNominal_LockAh_RejectsAMultiplierBelowOne(string input)
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.LOCKAH, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Equal("Must be 1.0 or greater", error);
    }

    [Fact]
    public void ValidateNominal_LockAh_RejectsANonNumericValue()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("abc", index: 0, OperatorConstants.LOCKAH, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Equal("Invalid number", error);
    }

    [Fact]
    public void ValidateLimit_PercAh_AcceptsAPositiveValueOnARegulatingOperator()
    {
        var (isValid, error, corrected) = ValidationHelper.ValidateLimit("> 130 PERCAh", OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
        Assert.Equal("> 130 PERCAh", corrected);
    }

    [Theory]
    [InlineData("> 0 PERCAh")]
    [InlineData("> -5 PERCAh")]
    public void ValidateLimit_PercAh_RejectsAValueAtOrBelowZero(string input)
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit(input, OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Equal("PERCAh limit value must be greater than 0", error);
    }

    [Fact]
    public void ValidateLimit_PercAh_CompletesFromThePerPrefix_SameMechanismAsExistingUnits()
    {
        // No bespoke autocomplete code for PERCAh - this is AutoCorrectUnit's existing generic
        // prefix-fallback against OperatorConstants.ValidUnits, the same path "V"/"min" use today.
        var (isValid, error, corrected) = ValidationHelper.ValidateLimit("> 130 PER", OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
        Assert.Equal("> 130 PERCAh", corrected);
    }

    [Fact]
    public void ValidateLimit_PercAh_IsUnreachableOnPau_OnlyTimeUnitsAreAccepted()
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit("130 PERCAh", OperatorConstants.PAU, NoVars);

        Assert.False(isValid);
        Assert.Equal("PAU only allows time units (s, sec, min, m, hr, h)", error);
    }

    [Fact]
    public void LockAh_CarriesNoLimitsSection_SoPercAhHasNoFieldToBeEnteredInto()
    {
        Assert.False(OperatorConstants.LimitActionAllowed[OperatorConstants.LOCKAH]);
    }

    // ============================================= SET "Ah"/"Wh" duplicate-name exemption

    // The reference program in the LOCKAh/PERCAh design declares "SET Ah = 0" twice - once before
    // each measurement block. The pre-existing "duplicate variable" check (written for exclusive
    // user-named SET variables, where reuse really is an error) must not flag this as invalid.

    [Theory]
    [InlineData("Ah = 0")]
    [InlineData("Wh = 0")]
    public void ValidateNominal_Set_AllowsAhOrWhToBeDeclaredMoreThanOnce(string input)
    {
        var varName = input.Split('=')[0].Trim();
        var globalVars = new List<GlobalVariable>
        {
            new() { Name = varName, Value = "0", StepId = "step-1" },
            new() { Name = varName, Value = "0", StepId = "step-4" },
        };

        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.SET, NoLabels, globalVars);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateNominal_Set_StillRejectsARegularVariableNameDeclaredTwice()
    {
        // Pins that the Ah/Wh exemption didn't disable duplicate-detection for everything else -
        // two SET steps both defining "myVar" is still genuinely ambiguous for a downstream reference.
        var globalVars = new List<GlobalVariable>
        {
            new() { Name = "myVar", Value = "5", StepId = "step-1" },
            new() { Name = "myVar", Value = "10", StepId = "step-2" },
        };

        var (isValid, error, _) = ValidationHelper.ValidateNominal("myVar = 5 A", index: 0, OperatorConstants.SET, NoLabels, globalVars);

        Assert.False(isValid);
        Assert.Equal("duplicate variable", error);
    }
}
