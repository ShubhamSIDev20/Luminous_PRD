using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

/// <summary>
/// Formulas pinned against the manual's own worked examples — see
/// docs/manual-extract/VNC-ACN-battery-parameters.md, "Worked example No. 6" (p.186):
/// a 100 Ah / 6-cell battery, 1.0 ACn5 -> 20 A, 0.05 ACn5 -> 1 A, 2.35 VN -> 14.1 V.
/// </summary>
public class BatteryUnitResolverTests
{
    private static BatteryDTO Battery(float nominalCapacity = 100, int cells = 6) => new()
    {
        Name = "Test Battery",
        Producer = "Test",
        MaximumVoltage = 100,
        BreakVoltage = 1,
        NominalCapacity = nominalCapacity,
        NumberOfCells = cells,
    };

    // ── ACN family: current = value * (NominalCapacity / X hours) ──────

    [Theory]
    [InlineData(1.0f, "ACN5", 20f)]     // manual's own example: 100Ah / 5h = 20A
    [InlineData(0.05f, "ACN5", 1f)]     // manual's own example: 0.05 x 20A = 1A
    [InlineData(1.0f, "ACN", 20f)]      // bare ACN defaults to the same 5h divisor as ACN5
    [InlineData(1.0f, "ACN1", 100f)]    // 100Ah / 1h = 100A
    [InlineData(1.0f, "ACN2", 50f)]     // 100Ah / 2h = 50A
    [InlineData(1.0f, "ACN4", 25f)]     // 100Ah / 4h = 25A
    [InlineData(1.0f, "ACN10", 10f)]    // 100Ah / 10h = 10A
    [InlineData(1.0f, "ACN20", 5f)]     // 100Ah / 20h = 5A
    [InlineData(10f, "ACn5", 200f)]     // manual: "10 ACn5 -> 10*(100Ah/5h) = 200A" (case-insensitive)
    public void Resolve_AcnFamily_ScalesByNominalCapacityOverHours(float input, string unit, float expectedAmps)
    {
        var result = BatteryUnitResolver.Resolve(input, unit, Battery());

        Assert.NotNull(result);
        Assert.Equal(expectedAmps, result!.Value.Value, precision: 3);
        Assert.Equal("A", result.Value.Unit);
    }

    // ── ACN<n> generalized to ANY positive-integer hour divisor, not just the manual's own
    // named C-rates (1/2/4/5/10/20) pinned above.

    [Theory]
    [InlineData(1.0f, "ACN7", 100f / 7f)]
    [InlineData(1.0f, "ACN3", 100f / 3f)]
    [InlineData(2.0f, "ACN123", 2f * (100f / 123f))]
    [InlineData(1.0f, "acn15", 100f / 15f)] // case-insensitive
    public void Resolve_AcnFamily_AcceptsArbitraryHourDivisor(float input, string unit, float expectedAmps)
    {
        var result = BatteryUnitResolver.Resolve(input, unit, Battery());

        Assert.NotNull(result);
        Assert.Equal(expectedAmps, result!.Value.Value, precision: 3);
        Assert.Equal("A", result.Value.Unit);
    }

    [Fact]
    public void Resolve_AcnZeroDivisor_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.Resolve(1.0f, "ACN0", Battery()));
    }

    // ── VN: voltage = value * NumberOfCells ─────────────────────────────

    [Theory]
    [InlineData(2.35f, "VN", 14.1f)]    // manual's own example: 2.35 V/cell x 6 cells = 14.1V
    [InlineData(2.48f, "Vn", 14.88f)]   // case-insensitive, "2.48 x 6 = 14.88" (manual p.27 example, 6 cells)
    public void Resolve_Vnc_ScalesByNumberOfCells(float input, string unit, float expectedVolts)
    {
        var result = BatteryUnitResolver.Resolve(input, unit, Battery());

        Assert.NotNull(result);
        Assert.Equal(expectedVolts, result!.Value.Value, precision: 3);
        Assert.Equal("V", result.Value.Unit);
    }

    // ── Pass-through for ordinary units ─────────────────────────────────

    [Theory]
    [InlineData("A")]
    [InlineData("V")]
    [InlineData("W")]
    [InlineData("Ah")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_NonBatteryRelativeUnit_ReturnsNull(string? unit)
    {
        var result = BatteryUnitResolver.Resolve(10f, unit, Battery());
        Assert.Null(result);
    }

    // ── Missing/invalid battery ──────────────────────────────────────────

    [Fact]
    public void Resolve_AcnUnit_NullBattery_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.Resolve(1.0f, "ACN5", null));
    }

    [Fact]
    public void Resolve_VncUnit_NullBattery_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.Resolve(2.35f, "VN", null));
    }

    [Fact]
    public void Resolve_AcnUnit_ZeroNominalCapacity_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.Resolve(1.0f, "ACN5", Battery(nominalCapacity: 0)));
    }

    [Fact]
    public void Resolve_VncUnit_ZeroCells_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.Resolve(2.35f, "VN", Battery(cells: 0)));
    }

    // ── IsValidAcnUnit — shape-valid ("ACN0", bare "ACN") is not the same as usable/explicit ────
    // The editor requires an explicit, non-zero hour divisor to be typed — bare "ACN" is no
    // longer accepted here even though Resolve itself still defaults it to 5h for backward
    // compatibility with already-saved programs (see Resolve_AcnFamily_ScalesByNominalCapacityOverHours).

    [Theory]
    [InlineData("ACN", false)]      // bare — no divisor typed, must be rejected by the editor
    [InlineData("ACN5", true)]
    [InlineData("acn7", true)]      // case-insensitive
    [InlineData("ACN123", true)]
    [InlineData("ACN0", false)]     // shape-valid, but zero divisor is not usable
    [InlineData("ACN00", false)]
    [InlineData("VN", false)]
    [InlineData("A", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidAcnUnit_RequiresExplicitNonZeroDivisor(string? unit, bool expected)
    {
        Assert.Equal(expected, BatteryUnitResolver.IsValidAcnUnit(unit));
    }

    // ── IsBatteryRelativeUnit ─────────────────────────────────────────────

    [Theory]
    [InlineData("ACN", true)]
    [InlineData("ACN5", true)]
    [InlineData("acn5", true)]
    [InlineData("ACN7", true)]
    [InlineData("ACN123", true)]
    [InlineData("VN", true)]
    [InlineData("vn", true)]
    [InlineData("A", false)]
    [InlineData("V", false)]
    [InlineData(null, false)]
    public void IsBatteryRelativeUnit_ClassifiesCorrectly(string? unit, bool expected)
    {
        Assert.Equal(expected, BatteryUnitResolver.IsBatteryRelativeUnit(unit));
    }

    // ── ResolveToken (string form used throughout ProgramBuilder) ────────

    [Fact]
    public void ResolveToken_BatteryRelative_ReturnsResolvedString()
    {
        var result = BatteryUnitResolver.ResolveToken("1.0 ACN5", Battery());
        Assert.Equal("20 A", result);
    }

    [Fact]
    public void ResolveToken_NonBatteryRelative_ReturnsUnchanged()
    {
        var result = BatteryUnitResolver.ResolveToken("20 A", Battery());
        Assert.Equal("20 A", result);
    }

    // ── §12.3 "Using Battery Parameters": bare INTERN[] tokens ──────────

    private static BatteryDTO FullBattery() => new()
    {
        Name = "Full Test Battery",
        Producer = "Test",
        NominalCapacity = 100,
        NumberOfCells = 6,
        GassingVoltage = 14.4f,
        MaximumVoltage = 15.0f,
        NominalVoltage = 12.0f,
        BreakVoltage = 10.5f,
        NominalCurrent = 20f,
        ColdCrankingCurrent = 300f,
        ChargeFactor = 1.05f,
        EnergyDensity = 150f,
        Impedance = 0.02f,
    };

    [Fact]
    public void GetBatteryGlobalVariables_ReturnsAllTenNamesWithCorrectValuesAndUnits()
    {
        var vars = BatteryUnitResolver.GetBatteryGlobalVariables(FullBattery());

        Assert.Equal(10, vars.Count);

        void Assert1(string name, string expectedValue, string expectedUnit)
        {
            var v = Assert.Single(vars, x => x.Name == name);
            Assert.Equal(expectedValue, v.Value);
            Assert.Equal(expectedUnit, v.Unit);
        }

        Assert1("CNom", "100", "Ah");
        Assert1("NoCell", "6", "");
        Assert1("UGas", "14.4", "V");
        Assert1("UMax", "15", "V");
        Assert1("UNom", "12", "V");
        Assert1("CutOff", "10.5", "V");
        Assert1("INom", "20", "A");
        Assert1("ICrank", "300", "A");
        Assert1("ChargeF", "1.05", "");
        Assert1("EDensity", "150", "");
    }

    [Fact]
    public void GetBatteryGlobalVariables_DoesNotIncludeRin()
    {
        // Rin (internal resistance) is deliberately excluded — no CutoffCondition/
        // RegistrationType byte exists for Ohms anywhere in docs/PROTOCOL.md.
        var vars = BatteryUnitResolver.GetBatteryGlobalVariables(FullBattery());
        Assert.DoesNotContain(vars, v => v.Name.Equals("Rin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetPlaceholderGlobalVariables_MatchesGetBatteryGlobalVariablesUnitsExactly()
    {
        // Regression guard: ProgramEditor.razor used to build its own copy of this Name->Unit
        // mapping by hand and set every placeholder's Unit to "" — which made ValidateNominal
        // reject a correctly-used "INom" ("Variable INom must have unit A, don't add unit"),
        // caught only by live browser testing, not by unit tests. Editor placeholders must now
        // come from this single source of truth so the two can never drift apart again.
        var placeholders = BatteryUnitResolver.GetPlaceholderGlobalVariables();
        var real = BatteryUnitResolver.GetBatteryGlobalVariables(FullBattery());

        Assert.Equal(10, placeholders.Count);
        foreach (var p in placeholders)
        {
            var match = Assert.Single(real, r => r.Name == p.Name);
            Assert.Equal(match.Unit, p.Unit);
        }
    }

    [Fact]
    public void InternVariableNames_MatchesGetBatteryGlobalVariablesNames()
    {
        var vars = BatteryUnitResolver.GetBatteryGlobalVariables(FullBattery());
        Assert.Equal(
            BatteryUnitResolver.InternVariableNames.OrderBy(n => n),
            vars.Select(v => v.Name).OrderBy(n => n));
    }

    // ── ResolveCNomCoefficient: coefficient * CNom -> Step Capacity ──────
    // Worked example from program_packet_v0.15.md §14.10: 100 Ah battery, "0.8 CNom" -> 80 Ah,
    // "0.1 CNom" -> 10 Ah — always mapped to "AhStep" (Step Capacity), never plain "Ah"
    // (Accumulated Capacity), which is what the bare-CNom-alone token already uses.

    [Theory]
    [InlineData(0.8f, "CNom", 80f)]
    [InlineData(0.1f, "cnom", 10f)]  // case-insensitive
    [InlineData(1.0f, "CNom", 100f)]
    public void ResolveCNomCoefficient_ScalesByNominalCapacity_MapsToStepCapacity(float coefficient, string token, float expectedAh)
    {
        var result = BatteryUnitResolver.ResolveCNomCoefficient(coefficient, token, Battery());

        Assert.NotNull(result);
        Assert.Equal(expectedAh, result!.Value.Value, precision: 3);
        Assert.Equal("AhStep", result.Value.Unit);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ACN5")]
    [InlineData("VN")]
    [InlineData("INom")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveCNomCoefficient_NonCNomToken_ReturnsNull(string? token)
    {
        var result = BatteryUnitResolver.ResolveCNomCoefficient(0.8f, token, Battery());
        Assert.Null(result);
    }

    [Fact]
    public void ResolveCNomCoefficient_NullBattery_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.ResolveCNomCoefficient(0.8f, "CNom", null));
    }

    [Fact]
    public void ResolveCNomCoefficient_ZeroNominalCapacity_Throws()
    {
        Assert.Throws<BatteryUnitResolutionException>(() => BatteryUnitResolver.ResolveCNomCoefficient(0.8f, "CNom", Battery(nominalCapacity: 0)));
    }

    // ── ProgramUsesBatteryRelativeTokens ─────────────────────────────────

    private static StepModel Step(params (string field, List<string> values)[] fields)
    {
        var step = new StepModel { StepNumber = 1, OperatorCode = 1 };
        foreach (var (field, values) in fields)
        {
            switch (field)
            {
                case "nominal": step.NominalValues = values; break;
                case "limit": step.Limits = values; break;
                case "registration": step.Registrations = values; break;
            }
        }
        return step;
    }

    [Fact]
    public void ProgramUsesBatteryRelativeTokens_PlainProgram_ReturnsFalse()
    {
        var steps = new List<StepModel>
        {
            Step(("nominal", new() { "1 A" }), ("limit", new() { "> 3.2 V" }), ("registration", new() { "100 Ah" })),
        };

        Assert.False(BatteryUnitResolver.ProgramUsesBatteryRelativeTokens(steps));
    }

    [Theory]
    [InlineData("nominal", "1 ACN5")]
    [InlineData("limit", "> 2.48 VN")]
    [InlineData("registration", "0.8 CNom")]
    [InlineData("limit", "CNom")]
    public void ProgramUsesBatteryRelativeTokens_DetectsEachFieldAndToken(string field, string value)
    {
        var steps = new List<StepModel> { Step((field, new() { value })) };
        Assert.True(BatteryUnitResolver.ProgramUsesBatteryRelativeTokens(steps));
    }
}
