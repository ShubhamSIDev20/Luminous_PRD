using System;
using System.Collections.Generic;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Covers the editor-side acceptance of battery-relative units (ACNx, VN) added to
/// NominalField.AcceptedUnits / OperatorConstants.ValidUnits. Before this, NominalConfig
/// hardcoded exactly one required unit per field (Current required literally "A"), so typing
/// "10 ACn5" into a charge-current field was rejected with "Unit must be A" — this pins the fix.
/// </summary>
public class ValidationHelperAcnVncTests
{
    private static HashSet<string> NoLabels => new();
    private static List<GlobalVariable> NoVars => new();

    [Theory]
    [InlineData("10 ACn5")]
    [InlineData("8 ACN1")]
    [InlineData("8 ACN2")]
    [InlineData("8 ACN4")]
    [InlineData("8 ACN10")]
    [InlineData("8 ACN20")]
    [InlineData("20 A")] // plain current still accepted alongside the ACN family
    public void ValidateNominal_CurrentField_AcceptsAcnFamilyAlongsidePlainAmps(string input)
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.True(isValid, error);
    }

    [Theory]
    [InlineData("10 ACn5")]
    [InlineData("20 A")]
    public void ValidateNominal_CcRechgCurrentField_AcceptsAcnFamilyAlongsidePlainAmps_JustLikeCcChg(string input)
    {
        // CC_ReChg's NominalConfig entry mirrors CC_CHG's exactly (same Current field, same
        // ExtraUnits = BatteryUnitResolver.AcnUnits) — this pins that it wasn't copy-pasted wrong.
        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.CC_RECHG, NoLabels, NoVars);

        Assert.True(isValid, error);
    }

    [Theory]
    [InlineData("2.35 VN")]
    [InlineData("2.35 Vn")]
    [InlineData("14.4 V")] // plain voltage still accepted alongside VN
    public void ValidateNominal_VoltageField_AcceptsVncAlongsidePlainVolts(string input)
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.CV_CHG, NoLabels, NoVars);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateNominal_CurrentField_RejectsVncUnit()
    {
        // VN is a voltage unit — it must not be accepted on a Current field.
        var (isValid, error, _) = ValidationHelper.ValidateNominal("2.35 VN", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("Unit must be one of", error);
    }

    [Fact]
    public void ValidateNominal_VoltageField_RejectsAcnUnit()
    {
        // ACN5 is a current unit — it must not be accepted on a Voltage field.
        var (isValid, error, _) = ValidationHelper.ValidateNominal("1.0 ACN5", index: 0, OperatorConstants.CV_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("Unit must be one of", error);
    }

    [Fact]
    public void ValidateNominal_UnrelatedUnit_StillRejectedOnCurrentField()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("10 W", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData("> 1.0 ACN5")]
    [InlineData("> 2.35 VN")]
    public void ValidateLimit_AcceptsBatteryRelativeUnits(string limit)
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit(limit, OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
    }

    // ── §12.3 "Using Battery Parameters": bare INTERN[] tokens ──────────
    // ProgramEditor.razor seeds `globalVariables` with these as placeholders (Value="0", real
    // Unit) so bare "INom"/"CNom"/etc. validate even though no battery is selected while
    // authoring — real resolution only happens at transfer time via
    // BatteryUnitResolver.GetBatteryGlobalVariables with the actually-selected battery.
    private static List<GlobalVariable> InternPlaceholders => new()
    {
        new() { Name = "CNom", Value = "0", Unit = "Ah" },
        new() { Name = "NoCell", Value = "0", Unit = "" },
        new() { Name = "UGas", Value = "0", Unit = "V" },
        new() { Name = "UMax", Value = "0", Unit = "V" },
        new() { Name = "UNom", Value = "0", Unit = "V" },
        new() { Name = "CutOff", Value = "0", Unit = "V" },
        new() { Name = "INom", Value = "0", Unit = "A" },
        new() { Name = "ICrank", Value = "0", Unit = "A" },
        new() { Name = "ChargeF", Value = "0", Unit = "" },
        new() { Name = "EDensity", Value = "0", Unit = "" },
    };

    [Fact]
    public void ValidateNominal_CurrentField_AcceptsBareInom()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("INom", index: 0, OperatorConstants.CC_CHG, NoLabels, InternPlaceholders);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateNominal_VoltageField_AcceptsBareUGas()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("UGas", index: 0, OperatorConstants.CV_CHG, NoLabels, InternPlaceholders);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateNominal_CurrentField_RejectsBareCNom()
    {
        // CNom is Ah-typed — must not be accepted as a raw Current nominal value.
        var (isValid, error, _) = ValidationHelper.ValidateNominal("CNom", index: 0, OperatorConstants.CC_CHG, NoLabels, InternPlaceholders);

        Assert.False(isValid);
        Assert.Contains("must have unit A", error);
    }

    [Theory]
    [InlineData("> INom")]
    [InlineData("> ICrank")]
    [InlineData("> UGas")]
    [InlineData("> UMax")]
    [InlineData("> UNom")]
    [InlineData("> CutOff")]
    [InlineData("> CNom")]
    public void ValidateLimit_AcceptsBareInternTokens(string limit)
    {
        // Limits aren't tied to a specific field type the way Nominal Value fields are — any
        // known variable name (SET-defined or an implicit battery parameter) is accepted here.
        var (isValid, error, _) = ValidationHelper.ValidateLimit(limit, OperatorConstants.CC_CHG, InternPlaceholders);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateNominal_UnknownBareToken_StillRejected()
    {
        // Sanity check: an arbitrary bare word that isn't SET-defined or an INTERN name still
        // fails — this feature doesn't accidentally accept everything.
        var (isValid, _, _) = ValidationHelper.ValidateNominal("NotARealVariable", index: 0, OperatorConstants.CC_CHG, NoLabels, InternPlaceholders);

        Assert.False(isValid);
    }

    // ── "<coefficient> CNom" (program_packet_v0.15.md §14.10) ────────────
    // Distinct from bare "CNom" above: a numeric coefficient in front means Step Capacity, and
    // is accepted purely syntactically here (no battery needed to author it) — the actual
    // multiplication happens at encode time in ProgramBuilder/BatteryUnitResolver.

    [Theory]
    [InlineData("> 0.8 CNom")]
    [InlineData("0.8 CNom")]
    [InlineData("< 0.1 cnom")] // case-insensitive
    public void ValidateLimit_AcceptsCoefficientCNom(string limit)
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit(limit, OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
    }

    [Fact]
    public void ValidateLimit_RejectsCoefficientCNom_OnPau()
    {
        // PAU only carries a plain duration — Step Capacity has no meaning there.
        var (isValid, error, _) = ValidationHelper.ValidateLimit("0.8 CNom", OperatorConstants.PAU, NoVars);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateRegistration_AcceptsCoefficientCNom()
    {
        var (isValid, error, _) = ValidationHelper.ValidateRegistration("0.1 CNom", OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
    }

    // ── OperatorConstants.AutoSpace — the on-blur formatter run before validation ────────
    // Regression: "CNom" was deliberately kept out of ValidUnits (to avoid leaking into Nominal
    // Value/SET fields), but AutoSpace's fuzzy fallback (FindNearestUnit) searches that same list
    // for the longest registered unit that is a PREFIX of what was typed — and "C" (Temperature)
    // is a prefix of "CNom". So typing "0.8 CNom" and blurring silently rewrote the field to
    // "0.8 C" before ValidateLimit ever ran, which then validated fine as 0.8 degrees, with no
    // error shown anywhere. Fixed by adding "CNom" itself to ValidUnits (as a whole token, exactly
    // like ACN5/VN already are) so AutoSpace's exact patterns match it directly and never reach
    // the fuzzy fallback.

    [Theory]
    [InlineData("0.8 CNom", "0.8 CNom")]
    [InlineData("0.8CNom", "0.8 CNom")]
    [InlineData("> 0.8 CNom", "> 0.8 CNom")]
    [InlineData("0.1 cnom", "0.1 cnom")]  // AutoSpace preserves casing as typed; AutoCorrectUnit normalizes it later
    public void AutoSpace_CoefficientCNom_DoesNotTruncateToTemperatureC(string input, string expected)
    {
        Assert.Equal(expected, OperatorConstants.AutoSpace(input));
    }

    [Fact]
    public void AutoSpace_PlainTemperatureC_StillWorks()
    {
        // Guard against the fix going the other way — a genuine bare "C" (Temperature) input must
        // still be recognized on its own, not just as a prefix of "CNom".
        Assert.Equal("0.8 C", OperatorConstants.AutoSpace("0.8 C"));
    }

    [Theory]
    [InlineData("2.48 VN", "2.48 VN")]
    [InlineData("> 2.48 VN", "> 2.48 VN")]
    public void AutoSpace_Vn_NotAffectedByCNomFix(string input, string expected)
    {
        // VN was already a full ValidUnits entry (unlike CNom), so it was never at risk of this
        // bug — pinned here since the user asked to double-check it too.
        Assert.Equal(expected, OperatorConstants.AutoSpace(input));
    }

    // ── Same bug class as the CNom fix above, but for a numbered ACN suffix. Originally "ACN7"
    // was treated as unsupported (only bare/1/2/4/5/10/20 were real divisors) and the old
    // FindNearestUnit prefix search silently truncated "1 ACN7" to "1 ACN" with no error shown.
    // That was fixed by rejecting it outright instead of silently truncating. BatteryUnitResolver
    // now accepts ANY positive-integer hour divisor (ACN3, ACN7, ACN123, ...), so the digit-guard
    // still matters — a numbered ACN unit must never be silently truncated to a shorter one — but
    // the end state changed: an unrecognized numbered unit is preserved as literally typed and now
    // resolves successfully instead of being flagged "Invalid unit".
    [Theory]
    [InlineData("1 ACN7", "1 ACN7")]
    [InlineData("1 ACN107", "1 ACN107")]
    [InlineData("> 1 ACN7", "> 1 ACN7")]
    public void AutoSpace_NumberedAcnUnit_DoesNotTruncateToShorterUnit(string input, string expected)
    {
        Assert.Equal(expected, OperatorConstants.AutoSpace(input));
    }

    [Fact]
    public void AutoSpace_GenuineTypoGarbage_StillResolvesViaFuzzyMatch()
    {
        // Guard against the fix going too far — garbage typed after a real unit (not more
        // digits) must still resolve via the fuzzy fallback, per FindNearestUnit's own doc
        // example.
        Assert.Equal("2 h", OperatorConstants.AutoSpace("2hbc"));
    }

    // ── ACN<n> generalized to any positive-integer hour divisor (not just 1/2/4/5/10/20) ────
    // Value coefficient was always arbitrary; this generalizes the hour-divisor suffix too, so
    // "1 ACN7", "8 ACN3", etc. are accepted wherever the fixed-set ACN family already was.

    [Theory]
    [InlineData("1 ACN7")]
    [InlineData("8 ACN3")]
    [InlineData("0.5 ACN15")]
    [InlineData("2 acn123")] // case-insensitive
    public void ValidateLimit_AcceptsArbitraryNumberedAcnUnit(string limit)
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit(limit, OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
    }

    [Theory]
    [InlineData("8 ACN7")]
    [InlineData("0.5 ACN15")]
    public void ValidateNominal_CurrentField_AcceptsArbitraryNumberedAcnUnit(string input)
    {
        // Only for fields where Current/Ampere is actually calculated (ExtraUnits includes the
        // ACN family) — CC_CHG's single field is Current.
        var (isValid, error, corrected) = ValidationHelper.ValidateNominal(input, index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.True(isValid, error);
        Assert.Contains("ACN", corrected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateNominal_VoltageField_RejectsArbitraryNumberedAcnUnit()
    {
        // CV_CHG's field is Voltage (ExtraUnits = VncUnits, not the ACN family) — an ACN unit,
        // numbered or not, must never validate there since no current calculation happens on it.
        var (isValid, error, _) = ValidationHelper.ValidateNominal("1.0 ACN7", index: 0, OperatorConstants.CV_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("Unit must be one of", error);
    }

    [Fact]
    public void ValidateNominal_PowerField_RejectsArbitraryNumberedAcnUnit()
    {
        // CP_CHG's field is Power — no ExtraUnits at all — same guard as the voltage field above.
        var (isValid, error, _) = ValidationHelper.ValidateNominal("1.0 ACN7", index: 0, OperatorConstants.CP_CHG, NoLabels, NoVars);

        Assert.False(isValid);
    }

    [Fact]
    public void ValidateRegistration_AcceptsArbitraryNumberedAcnUnit()
    {
        var (isValid, error, corrected) = ValidationHelper.ValidateRegistration("0.5 ACN7", OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
        Assert.Contains("ACN7", corrected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutoCorrectUnit_NormalizesArbitraryAcnUnitCasing()
    {
        Assert.Equal("ACN7", ValidationHelper.AutoCorrectUnit("acn7"));
        Assert.Equal("ACN123", ValidationHelper.AutoCorrectUnit("Acn123"));
    }

    // ── "1 ACN3": neither the coefficient (1) nor the hour divisor (3) may be 0 ──────────────
    // The divisor was previously shape-valid-but-unusable ("ACN0" matched IsAcnUnit and passed
    // editor validation, then only failed at transfer time when BatteryUnitResolver.Resolve
    // actually divided by zero). The coefficient was never checked at all — "0 ACN3" validated
    // fine and would have silently encoded to 0 A. Both are now rejected up front.

    [Fact]
    public void AutoCorrectUnit_RejectsZeroAcnDivisor()
    {
        Assert.Null(ValidationHelper.AutoCorrectUnit("ACN0"));
    }

    [Fact]
    public void ValidateLimit_RejectsZeroAcnDivisor()
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit("> 1 ACN0", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("Invalid unit", error);
    }

    [Fact]
    public void ValidateLimit_RejectsZeroAcnCoefficient()
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit("> 0 ACN3", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("greater than 0", error);
    }

    [Fact]
    public void ValidateNominal_CurrentField_RejectsZeroAcnDivisor()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("1 ACN0", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("Unit must be one of", error);
    }

    [Fact]
    public void ValidateNominal_CurrentField_RejectsZeroAcnCoefficient()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("0 ACN3", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("greater than 0", error);
    }

    [Fact]
    public void ValidateRegistration_RejectsZeroAcnDivisor()
    {
        var (isValid, error, _) = ValidationHelper.ValidateRegistration("1 ACN0", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("Invalid unit", error);
    }

    [Fact]
    public void ValidateRegistration_RejectsZeroAcnCoefficient()
    {
        var (isValid, error, _) = ValidationHelper.ValidateRegistration("0 ACN3", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("greater than 0", error);
    }

    [Fact]
    public void ValidateNominal_CurrentField_StillAcceptsNonZeroCoefficientAndDivisor()
    {
        // Guard against the fix going too far — a genuinely valid "1 ACN3" must still pass.
        var (isValid, error, corrected) = ValidationHelper.ValidateNominal("1 ACN3", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.True(isValid, error);
        Assert.Equal("1 ACN3", corrected);
    }

    // ── Bare "ACN" (no hour divisor typed at all) must now be an error, everywhere ───────────
    // Previously bare "ACN" defaulted to 5h (a documented manual convenience) and validated fine
    // in every field. The editor now requires the user to type an explicit divisor — both the
    // coefficient and the divisor in "<n> ACN<x>" must be real, typed numbers. Note
    // BatteryUnitResolver.Resolve itself is deliberately left defaulting bare "ACN" to 5h, for
    // backward compatibility with programs already saved before this change (see
    // BatteryUnitResolverTests.Resolve_AcnFamily_ScalesByNominalCapacityOverHours) — only the
    // editor's acceptance of newly typed input is tightened.

    [Fact]
    public void AutoCorrectUnit_RejectsBareAcn()
    {
        Assert.Null(ValidationHelper.AutoCorrectUnit("ACN"));
    }

    [Fact]
    public void ValidateNominal_CurrentField_RejectsBareAcn()
    {
        var (isValid, error, _) = ValidationHelper.ValidateNominal("1 ACN", index: 0, OperatorConstants.CC_CHG, NoLabels, NoVars);

        Assert.False(isValid);
        Assert.Contains("Unit must be one of", error);
    }

    [Fact]
    public void ValidateLimit_RejectsBareAcn()
    {
        var (isValid, error, _) = ValidationHelper.ValidateLimit("> 1 ACN", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("Invalid unit", error);
    }

    [Fact]
    public void ValidateRegistration_RejectsBareAcn()
    {
        var (isValid, error, _) = ValidationHelper.ValidateRegistration("1 ACN", OperatorConstants.CC_CHG, NoVars);

        Assert.False(isValid);
        Assert.Contains("Invalid unit", error);
    }

    [Fact]
    public void AutoSpace_BareAcn_PreservedAsTypedNotMangledToPlainAmps()
    {
        // Guard against a regression of the CNom/ACN7-truncation bug class: even though bare
        // "ACN" is now rejected at validation time, AutoSpace's on-blur formatter must still
        // preserve it verbatim (not silently fuzzy-match it down to "1 A") so the user sees a
        // clear "invalid unit" error on the text they actually typed.
        Assert.Equal("1 ACN", OperatorConstants.AutoSpace("1ACN"));
        Assert.Equal("1 ACN", OperatorConstants.AutoSpace("1 ACN"));
    }

    // ── Registration accepts the full ACN logic, same as Nominal/Limit ───────────────────────

    [Theory]
    [InlineData("1 ACN3")]
    [InlineData("8 ACN7")]
    [InlineData("0.5 ACN15")]
    public void ValidateRegistration_AcceptsAcnWithExplicitDivisor(string value)
    {
        var (isValid, error, corrected) = ValidationHelper.ValidateRegistration(value, OperatorConstants.CC_CHG, NoVars);

        Assert.True(isValid, error);
        Assert.Contains("ACN", corrected, StringComparison.OrdinalIgnoreCase);
    }
}
