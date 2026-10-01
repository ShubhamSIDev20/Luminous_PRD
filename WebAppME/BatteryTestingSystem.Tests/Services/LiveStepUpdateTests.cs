using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// Pins Q9 ("Live Step Update", bm_program_v3.2.md): the single-step wire encoding
/// (<see cref="DecoderService.BuildLiveStepUpdatePacket"/>), the STATUS+REASON response decode
/// (<see cref="DecoderService.TryDecode{T}"/>), and the client-side allow-list/operator/step-number
/// checks (<see cref="LiveStepUpdateValidator"/>) that echo the Primary-only REASON codes before a
/// frame is ever sent.
/// </summary>
public class LiveStepUpdateTests
{
    private static StepModel Step(int number, byte op) => new() { StepNumber = number, OperatorCode = op };

    // ---------------------------------------------------------------- BuildLiveStepUpdatePacket

    [Fact]
    public void BuildLiveStepUpdatePacket_WrapsWithTerminatorSentinel_NotAnOffset()
    {
        // A standalone one-step Q9 payload must carry TERMINATOR_INDEX (0xFFFFFFFF) at [2..5],
        // never a real offset - there is no "next step" to point at.
        var target = Step(7, OperatorConstants.CC_CHG);
        var fullProgram = new List<StepModel> { Step(1, OperatorConstants.SET), target, Step(20, OperatorConstants.STO) };

        byte[] packet = DecoderService.BuildLiveStepUpdatePacket(target, fullProgram);

        Assert.Equal(0xAA, packet[0]);
        Assert.Equal(0x55, packet[1]);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, packet[2..6]);
        Assert.Equal(0x55, packet[^2]);
        Assert.Equal(0xAA, packet[^1]);
    }

    [Fact]
    public void BuildLiveStepUpdatePacket_EncodesTheSameStepBytes_AsTheFullProgramEncoder()
    {
        // The wire format must never drift between Q4 (whole program) and Q9 (one step) - both
        // funnel through the same FormatStepBytes helper internally.
        var target = Step(7, OperatorConstants.CC_CHG);
        var fullProgram = new List<StepModel> { Step(1, OperatorConstants.SET), target, Step(20, OperatorConstants.STO) };

        byte[] q9Packet = DecoderService.BuildLiveStepUpdatePacket(target, fullProgram);
        List<byte[]> q4Packets = DecoderService.ConvertProgramIntoBytesPackets(fullProgram);
        byte[] q4TargetPacket = q4Packets[1]; // target is the 2nd step in fullProgram

        // Strip the 0xAA55 header and 4-byte offset field from both - Q4's isn't the terminator
        // (it has a follow-on step) but the step BODY bytes (StepId+Operator+payload) must match.
        byte[] q9Body = q9Packet[6..^2];
        byte[] q4Body = q4TargetPacket[6..^2];

        Assert.Equal(q4Body, q9Body);
    }

    [Fact]
    public void BuildLiveStepUpdatePacket_ResolvesGlobalVariables_FromTheFullProgram_NotJustTheTargetStep()
    {
        // A CC_CHG step referencing a SET-defined variable earlier in the program must resolve it
        // even though only the CC_CHG step itself is being sent - the SET step is elsewhere in
        // fullProgramSteps, never sent over the wire for Q9.
        var setStep = Step(1, OperatorConstants.SET);
        setStep.NominalValues.Add("(myCurrent=2 A)");
        var target = Step(2, OperatorConstants.CC_CHG);
        target.NominalValues.Add("myCurrent");
        var fullProgram = new List<StepModel> { setStep, target, Step(3, OperatorConstants.STO) };

        byte[] packet = DecoderService.BuildLiveStepUpdatePacket(target, fullProgram);

        // StepId(2B) + Operator(1B) + Current(4B float) = body; resolved to 2.0f, not left as text.
        byte[] body = packet[6..^2];
        byte[] currentBytes = body[3..7];
        if (BitConverter.IsLittleEndian) Array.Reverse(currentBytes);
        Assert.Equal(2.0f, BitConverter.ToSingle(currentBytes, 0));
    }

    // ---------------------------------------------------------------------------- TryDecode (Q9)

    private static byte[] Q9Response(byte status, byte reason) =>
        new byte[] { 0xBB, 0x01, 0x01, 0x09, status, reason, 0x00, 0x00 };

    [Fact]
    public void TryDecode_Q9Success_ReturnsOk()
    {
        var result = DecoderService.TryDecode<bool>(Q9Response(0x01, 0x00));

        Assert.True(result.Success);
        Assert.True(result.Data);
    }

    [Theory]
    [InlineData(0x01, "idle")]
    [InlineData(0x02, "currently executing")]
    [InlineData(0x03, "parameters only")]
    [InlineData(0x04, "not eligible")]
    [InlineData(0x05, "Secondary NACK")]
    [InlineData(0x06, "Malformed")]
    [InlineData(0x07, "still in flight")]
    public void TryDecode_Q9Rejection_ReturnsReasonMessage(byte reason, string expectedSubstring)
    {
        var result = DecoderService.TryDecode<bool>(Q9Response(0x00, reason));

        Assert.False(result.Success);
        Assert.Contains(expectedSubstring, result.Message);
    }

    // ------------------------------------------------------------------- LiveStepUpdateValidator

    [Fact]
    public void Validate_ReturnsNull_WhenOperatorUnchangedAndEligibleAndStepMatches()
    {
        var resident = Step(7, OperatorConstants.CC_CHG);
        var edited = Step(7, OperatorConstants.CC_CHG);

        Assert.Null(LiveStepUpdateValidator.Validate(resident, edited, currentHardwareStepNumber: 7));
    }

    [Fact]
    public void Validate_Rejects_WhenNoResidentStepFound()
    {
        var edited = Step(7, OperatorConstants.CC_CHG);

        var message = LiveStepUpdateValidator.Validate(null, edited, currentHardwareStepNumber: 7);

        Assert.Contains("currently executing", message);
    }

    [Fact]
    public void Validate_Rejects_WhenStepNumberIsNotTheCurrentlyExecutingStep()
    {
        var resident = Step(5, OperatorConstants.CC_CHG);
        var edited = Step(5, OperatorConstants.CC_CHG);

        // Hardware is actually on step 7 right now - resident/edited both describe step 5.
        var message = LiveStepUpdateValidator.Validate(resident, edited, currentHardwareStepNumber: 7);

        Assert.Contains("currently executing", message);
    }

    [Fact]
    public void Validate_Rejects_WhenOperatorChanged()
    {
        var resident = Step(7, OperatorConstants.CC_CHG);
        var edited = Step(7, OperatorConstants.CV_CHG);

        var message = LiveStepUpdateValidator.Validate(resident, edited, currentHardwareStepNumber: 7);

        Assert.Contains("parameters only", message);
    }

    [Theory]
    [InlineData(OperatorConstants.TABLE)]
    [InlineData(OperatorConstants.BEG)]
    [InlineData(OperatorConstants.CYC)]
    [InlineData(OperatorConstants.GOTO)]
    [InlineData(OperatorConstants.CC_RECHG)] // explicitly NOT on the Q9 allow-list, unlike CHARGE_OPS
    public void Validate_Rejects_OperatorsNotOnTheAllowList(byte operatorCode)
    {
        var resident = Step(7, operatorCode);
        var edited = Step(7, operatorCode);

        var message = LiveStepUpdateValidator.Validate(resident, edited, currentHardwareStepNumber: 7);

        Assert.Contains("not eligible", message);
    }

    // ---------------------------------------------------------------- ResolveFullProgramSteps

    [Fact]
    public void ResolveFullProgramSteps_FallsBackToProgramStepModel_WhenExpandedIsEmpty()
    {
        // Regression: ExpandedProgramSteps is only populated when the program has PRODUCER
        // sub-steps (SetProgramAsync leaves it "new()" otherwise) - for every ordinary program,
        // the live-step-update dialog/handler must resolve steps from Program.ProgramStepModel
        // instead, or it always reports "no step is currently executing".
        var expanded = new List<StepModel>();
        var programSteps = new List<StepModel> { Step(1, OperatorConstants.CC_CHG) };

        var resolved = LiveStepUpdateValidator.ResolveFullProgramSteps(expanded, programSteps);

        Assert.Same(programSteps, resolved);
    }

    [Fact]
    public void ResolveFullProgramSteps_PrefersExpanded_WhenProducerStepsWereExpanded()
    {
        var expanded = new List<StepModel> { Step(1, OperatorConstants.CC_CHG), Step(2, OperatorConstants.STO) };
        var programSteps = new List<StepModel> { Step(1, OperatorConstants.PRODUCER) };

        var resolved = LiveStepUpdateValidator.ResolveFullProgramSteps(expanded, programSteps);

        Assert.Same(expanded, resolved);
    }

    [Fact]
    public void ResolveFullProgramSteps_ReturnsEmptyList_WhenBothAreNull()
    {
        var resolved = LiveStepUpdateValidator.ResolveFullProgramSteps(null, null);

        Assert.Empty(resolved);
    }

    // ---------------------------------------------------------------------- ResolveEditSource

    [Fact]
    public void ResolveEditSource_PrefersLastLiveStepUpdate_ForTheSameStillExecutingStep()
    {
        // Regression: re-opening "Edit Current Step" for the same step, after an earlier edit was
        // already submitted this run, must show the last-submitted values (e.g. 11 A), not the
        // original resident values (10 A) - Q9 never writes back to the resident program.
        var resident = Step(7, OperatorConstants.CC_CHG);
        resident.NominalValues.Add("10 A");

        var lastUpdate = Step(7, OperatorConstants.CC_CHG);
        lastUpdate.NominalValues.Add("11 A");

        var source = LiveStepUpdateValidator.ResolveEditSource(resident, lastUpdate, currentHardwareStepNumber: 7);

        Assert.Same(lastUpdate, source);
        Assert.Equal("11 A", source.NominalValues[0]);
    }

    [Fact]
    public void ResolveEditSource_FallsBackToResident_WhenNoAmendmentExistsYet()
    {
        var resident = Step(7, OperatorConstants.CC_CHG);

        var source = LiveStepUpdateValidator.ResolveEditSource(resident, lastLiveStepUpdate: null, currentHardwareStepNumber: 7);

        Assert.Same(resident, source);
    }

    [Fact]
    public void ResolveEditSource_FallsBackToResident_WhenTheCachedAmendmentIsForADifferentStep()
    {
        // The program advanced since the earlier amendment - step 7's cached edit must not leak
        // into step 8 just because it happens to be on the same operator/allow-list.
        var resident = Step(8, OperatorConstants.CC_CHG);
        var staleUpdate = Step(7, OperatorConstants.CC_CHG);
        staleUpdate.NominalValues.Add("11 A");

        var source = LiveStepUpdateValidator.ResolveEditSource(resident, staleUpdate, currentHardwareStepNumber: 8);

        Assert.Same(resident, source);
    }

    [Theory]
    [InlineData(OperatorConstants.CC_CHG)]
    [InlineData(OperatorConstants.CV_CHG)]
    [InlineData(OperatorConstants.CP_CHG)]
    [InlineData(OperatorConstants.CCCV_CHG)]
    [InlineData(OperatorConstants.CC_DCHG)]
    [InlineData(OperatorConstants.CP_DCHG)]
    [InlineData(OperatorConstants.CCCV_DCHG)]
    [InlineData(OperatorConstants.CV_DCHG)]
    [InlineData(OperatorConstants.PAU)]
    public void Validate_Accepts_EveryAllowListedOperator(byte operatorCode)
    {
        var resident = Step(7, operatorCode);
        var edited = Step(7, operatorCode);

        Assert.Null(LiveStepUpdateValidator.Validate(resident, edited, currentHardwareStepNumber: 7));
    }
}
