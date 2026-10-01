using System.Linq;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// Pins <see cref="DecoderService.TryDecode{T}"/>'s Q10 (Jump to Program Step, bm_program_v3.2)
/// case: <c>BB DEV CKT 0A STATUS REASON CRC_LO CRC_HI</c>. Unlike the other StartByte.Program
/// queries, Q10's response carries a second byte (REASON) that must become the response's
/// Message on rejection - not the generic "Ok(false)" every other query falls back to.
/// </summary>
public class DecoderServiceTests
{
    private static byte[] JumpResponse(byte status, byte reason) =>
        new byte[] { (byte)StartByte.Program, 0x01, 0x11, (byte)ProgramDataQuery.JumpToStep, status, reason, 0x00, 0x00 };

    [Fact]
    public void TryDecode_JumpToStep_Success_ReturnsOkTrue()
    {
        var response = DecoderService.TryDecode<bool>(JumpResponse(1, 0));

        Assert.True(response.Success);
        Assert.True(response.Data);
    }

    [Theory]
    [InlineData((byte)JumpToStepReason.NoProgramRunning, "No program is running")]
    [InlineData((byte)JumpToStepReason.StepDoesNotExist, "does not exist")]
    [InlineData((byte)JumpToStepReason.MalformedFrame, "malformed")]
    [InlineData((byte)JumpToStepReason.SecondaryRejectedOrLinkDown, "Rejected by the hardware")]
    [InlineData((byte)JumpToStepReason.JumpAlreadyInFlight, "previous jump is still in progress")]
    public void TryDecode_JumpToStep_Rejection_FailsWithReasonSpecificMessage(byte reason, string expectedSubstring)
    {
        var response = DecoderService.TryDecode<bool>(JumpResponse(0, reason));

        Assert.False(response.Success);
        Assert.Contains(expectedSubstring, response.Message);
    }

    [Fact]
    public void TryDecode_JumpToStep_RejectionReasonsAreDistinctMessages()
    {
        // Guards against all 5 reasons silently collapsing onto one generic string - each one
        // exists so the operator sees a different, actionable message.
        var messages = new[]
        {
            JumpToStepReason.NoProgramRunning,
            JumpToStepReason.StepDoesNotExist,
            JumpToStepReason.MalformedFrame,
            JumpToStepReason.SecondaryRejectedOrLinkDown,
            JumpToStepReason.JumpAlreadyInFlight,
        }.Select(r => DecoderService.TryDecode<bool>(JumpResponse(0, (byte)r)).Message).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
    }
}
