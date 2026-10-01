using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// Pins <see cref="DecoderService.ConvertProgramIntoBytesPackets"/> — the encoder that turns an
/// operator-level program into the byte frames the hardware actually executes.
///
/// Nothing downstream validates these bytes: a misplaced field does not throw, it silently shifts
/// every following field, and the device runs a *different program* than the operator authored.
/// So each test here asserts the payload field-by-field against independent sources of truth —
/// the wire enums (<see cref="CutoffCondition"/>, <see cref="LogicOperator"/>,
/// <see cref="RegistrationType"/>) and BitConverter — never against the encoder's own switch table.
///
/// Tests drive the public entry point and strip the frame with <see cref="Payload"/>, rather than
/// calling ProgramBuilder's helpers directly: the question being answered is "does a step reach the
/// wire correctly", which is a property of the whole pipeline, not of any one helper.
/// </summary>
public class ProgramToBytePacketTests : IDisposable
{
    // ------------------------------------------------------------------ static-state isolation

    // AddRegCount reads RStandards, a process-wide static cache populated only from the DB. Left
    // alone it is whatever an earlier test in the same run happened to leave behind, which would
    // make the SET/REG expectations below order-dependent. Snapshot and restore it per test.
    private readonly List<RegistrationStandardsDTO> _savedStandards;

    public ProgramToBytePacketTests()
    {
        _savedStandards = CurrentStandards();
        SetStandards(new List<RegistrationStandardsDTO>());
    }

    public void Dispose() => SetStandards(_savedStandards);

    private static FieldInfo StandardsField =>
        typeof(RStandards).GetField("_standards", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("RStandards._standards was renamed — update this test.");

    private static List<RegistrationStandardsDTO> CurrentStandards() =>
        (List<RegistrationStandardsDTO>)StandardsField.GetValue(null)!;

    private static void SetStandards(List<RegistrationStandardsDTO> standards) =>
        StandardsField.SetValue(null, standards);

    // ------------------------------------------------------------------------------- helpers

    private static byte[] BeInt(int value)
    {
        var b = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] BeFloat(float value)
    {
        var b = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    /// <summary>Strips the 0xAA55 header, the 4-byte offset field and the 0x55AA footer.</summary>
    private static byte[] Payload(byte[] packet) => packet.Skip(6).Take(packet.Length - 8).ToArray();

    private static byte[] Bytes(params object[] parts)
    {
        var result = new List<byte>();
        foreach (var part in parts)
        {
            switch (part)
            {
                case byte b: result.Add(b); break;
                case int i: result.Add((byte)i); break;
                case byte[] arr: result.AddRange(arr); break;
                default: throw new ArgumentException($"unsupported part {part.GetType()}");
            }
        }
        return result.ToArray();
    }

    private static StepModel Step(int number, byte op) => new() { StepNumber = number, OperatorCode = op };

    // ============================================================================ frame layout

    [Fact]
    public void EveryPacket_IsWrappedInTheSameSentinels()
    {
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.STO),
            Step(2, OperatorConstants.STO),
        });

        Assert.All(packets, p =>
        {
            Assert.Equal(0xAA, p[0]);
            Assert.Equal(0x55, p[1]);
            Assert.Equal(0x55, p[^2]);
            Assert.Equal(0xAA, p[^1]);
        });
    }

    [Fact]
    public void TheOffsetField_PointsAtWhereTheNextPacketStartsInTheConcatenatedStream()
    {
        // The offset is cumulative, not per-step: a decoder walking the stream adds nothing of its
        // own, it jumps to the absolute position this field names. An encoder that wrote each
        // step's own length here would look right for step 1 and desync from step 2 onwards, which
        // is why this asserts against a running total rather than against packet[i].Length.
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.STO),
            Step(2, OperatorConstants.STO),
            Step(3, OperatorConstants.STO),
        });

        int runningTotal = 0;
        for (int i = 0; i < packets.Count - 1; i++)
        {
            runningTotal += packets[i].Length;
            Assert.Equal(BeInt(runningTotal), packets[i].Skip(2).Take(4));
        }
    }

    [Fact]
    public void TheLastPacket_CarriesTheTerminatorInsteadOfAnOffset()
    {
        // 0xFFFFFFFF is what tells the device "no step follows". Emitting a real offset here would
        // send it reading past the end of the program.
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.STO),
            Step(2, OperatorConstants.STO),
        });

        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, packets[^1].Skip(2).Take(4));
    }

    [Fact]
    public void EmptyOrNullProgram_ProducesNoPackets()
    {
        Assert.Empty(DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>()));
        Assert.Empty(DecoderService.ConvertProgramIntoBytesPackets(null!));
    }

    // ====================================================================== step id and opcode

    [Fact]
    public void EveryStep_LeadsWithABigEndianStepIdThenItsOpcode()
    {
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(300, OperatorConstants.STO),
        });

        // 300 = 0x012C. A little-endian slip would emit 2C 01 and the device would run step 11009.
        Assert.Equal(Bytes(0x01, 0x2C, OperatorConstants.STO), Payload(packets[0]));
    }

    [Fact]
    public void StoStep_CarriesNothingBeyondItsIdAndOpcode()
    {
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(7, OperatorConstants.STO),
        });

        Assert.Equal(Bytes(0x00, 0x07, OperatorConstants.STO), Payload(packets[0]));
    }

    // ================================================================== SET / REG register mask

    // The register-type field is a bit mask, not a count: each logged unit contributes its own bit
    // value from RStandards.unitValueMap. h,min,sec (1), ERR_E (8192) and MSG_E (16384) are added
    // unconditionally, so an empty standard set is 24577 — not 0.
    private const int AlwaysOnMask = 1 + 8192 + 16384;

    [Fact]
    public void SetStep_EmitsAZeroByteThenTheRegisterMask()
    {
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.SET),
        });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.SET, 0x00, BeShort(AlwaysOnMask)),
            Payload(packets[0]));
    }

    [Fact]
    public void TheRegisterMask_AddsABitPerUnitOfTheNamedStandard()
    {
        // Proves the mask is really derived from the standard rather than being a constant: A adds
        // 2 and V adds 4 on top of the always-on bits.
        SetStandards(new List<RegistrationStandardsDTO>
        {
            new() { StandardName = "MYSTD", UnitList = new List<string> { "A", "V" } },
        });

        var set = Step(1, OperatorConstants.SET);
        set.Registrations = new List<string> { "mystd" };   // matched case-insensitively

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.SET, 0x00, BeShort(AlwaysOnMask + 2 + 4)),
            Payload(packets[0]));
    }

    [Fact]
    public void RegStep_WithoutItsOwnRegistrations_InheritsTheNearestPrecedingSet()
    {
        // "Nearest preceding", not "first in the program": a two-block program has one SET per
        // block, and binding block 2's REG to block 1's SET would log the wrong channels.
        SetStandards(new List<RegistrationStandardsDTO>
        {
            new() { StandardName = "BLOCK1", UnitList = new List<string> { "A" } },          // 2
            new() { StandardName = "BLOCK2", UnitList = new List<string> { "V", "W" } },     // 4 + 16
        });

        var setA = Step(1, OperatorConstants.SET);
        setA.Registrations = new List<string> { "BLOCK1" };
        var setB = Step(3, OperatorConstants.SET);
        setB.Registrations = new List<string> { "BLOCK2" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            setA,
            Step(2, OperatorConstants.REG),
            setB,
            Step(4, OperatorConstants.REG),
        });

        Assert.Equal(
            Bytes(0x00, 0x02, OperatorConstants.REG, BeShort(AlwaysOnMask + 2)),
            Payload(packets[1]));
        Assert.Equal(
            Bytes(0x00, 0x04, OperatorConstants.REG, BeShort(AlwaysOnMask + 4 + 16)),
            Payload(packets[3]));
    }

    [Fact]
    public void RegStep_WithItsOwnRegistrations_UsesThemInsteadOfThePrecedingSet()
    {
        SetStandards(new List<RegistrationStandardsDTO>
        {
            new() { StandardName = "SETSTD", UnitList = new List<string> { "A" } },   // 2
            new() { StandardName = "REGSTD", UnitList = new List<string> { "C" } },   // 8
        });

        var set = Step(1, OperatorConstants.SET);
        set.Registrations = new List<string> { "SETSTD" };
        var reg = Step(2, OperatorConstants.REG);
        reg.Registrations = new List<string> { "REGSTD" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set, reg });

        Assert.Equal(
            Bytes(0x00, 0x02, OperatorConstants.REG, BeShort(AlwaysOnMask + 8)),
            Payload(packets[1]));
    }

    [Fact]
    public void RegStep_WithNoSetAnywhereBeforeIt_StillEmitsTheTwoByteField()
    {
        // The field is fixed-width on the wire. Skipping it when there is nothing to say would
        // shift every subsequent step's parse by two bytes instead of just logging nothing.
        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.REG),
        });

        Assert.Equal(Bytes(0x00, 0x01, OperatorConstants.REG, 0x00, 0x00), Payload(packets[0]));
    }

    // ============================================================================ GOTO targets

    [Fact]
    public void GotoStep_ResolvesItsTargetByLabel()
    {
        var target = Step(2, OperatorConstants.STO);
        target.Label = "LoopTop";

        var jump = Step(5, OperatorConstants.GOTO);
        jump.NominalValues = new List<string> { "looptop" };   // case-insensitive

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { target, jump });

        // id, opcode, then the *target's* step id, then an empty registration list
        Assert.Equal(
            Bytes(0x00, 0x05, OperatorConstants.GOTO, 0x00, 0x02, 0x00),
            Payload(packets[1]));
    }

    [Fact]
    public void GotoStep_FallsBackToAStepNumberWhenNoLabelMatches()
    {
        var jump = Step(5, OperatorConstants.GOTO);
        jump.NominalValues = new List<string> { "2" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(2, OperatorConstants.STO),
            jump,
        });

        Assert.Equal(
            Bytes(0x00, 0x05, OperatorConstants.GOTO, 0x00, 0x02, 0x00),
            Payload(packets[1]));
    }

    [Fact]
    public void GotoStep_WithAnUnresolvableTarget_FallsThroughToTheNextStep()
    {
        // Documents current behaviour: an unresolvable jump becomes StepNumber+1, i.e. a no-op
        // that continues the program rather than an error or a jump to step 0.
        var jump = Step(5, OperatorConstants.GOTO);
        jump.NominalValues = new List<string> { "NoSuchLabel" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { jump });

        Assert.Equal(
            Bytes(0x00, 0x05, OperatorConstants.GOTO, 0x00, 0x06, 0x00),
            Payload(packets[0]));
    }

    // ================================================================ full charge/discharge step

    [Fact]
    public void ChargeStep_EmitsNominalThenLimitThenActionThenRegistration_InThatOrder()
    {
        // The one test that exercises a real operator end to end. Field order is the whole
        // contract here: the device reads positionally, so a swapped unit/operator pair turns
        // "stop above 60 minutes" into something else entirely without any error.
        var step = Step(3, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "20 A" };
        step.Limits = new List<string> { "> 60 min" };
        step.Actions = new List<string> { "STO" };
        step.Registrations = new List<string> { "10 min" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(
                0x00, 0x03,                                  // step id
                OperatorConstants.CC_CHG,                    // opcode
                BeFloat(20f),                                // nominal: 20 A stays a float
                0x01,                                        // limit count
                (byte)CutoffCondition.Time,                  // limit unit
                (byte)LogicOperator.GreaterThan,             // limit comparison
                BeInt(60 * 60_000),                          // limit value: time becomes int ms
                0x0B,                                        // action: STO
                0x01,                                        // registration count
                (byte)RegistrationType.Time,                 // registration type
                BeInt(10 * 60_000)),                         // registration interval in ms
            Payload(packets[0]));
    }

    [Fact]
    public void TimeValues_BecomeIntegerMilliseconds_WhileEverythingElseStaysFloat()
    {
        // Two encodings share one 4-byte slot and nothing on the wire says which is which — the
        // unit alone decides. Encoding 60 min as a float would be read as ~1.4e-42 by the device.
        var step = Step(1, OperatorConstants.CC_DCHG);
        step.NominalValues = new List<string> { "12.5 V", "90 s" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.CC_DCHG, BeFloat(12.5f), BeInt(90 * 1000), 0x00),
            Payload(packets[0]));
    }

    [Fact]
    public void ALimitWithNoAction_StillEmitsItsActionByte()
    {
        // Limits and actions are paired positionally; a missing action must be an explicit 0x00 or
        // the next limit is read as this one's action.
        var step = Step(1, OperatorConstants.CC_CHG);
        step.Limits = new List<string> { "> 4.2 V" };
        step.Actions = new List<string>();          // deliberately short — EnsureActionsCount pads it

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(
                0x00, 0x01,
                OperatorConstants.CC_CHG,
                0x01,
                (byte)CutoffCondition.Voltage,
                (byte)LogicOperator.GreaterThan,
                BeFloat(4.2f),
                0x00,                                // padded action
                0x00),                               // no registrations
            Payload(packets[0]));
    }

    [Fact]
    public void AStepWithNoRegistrations_EmitsAZeroCountRatherThanNothing()
    {
        var step = Step(1, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "1 A" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.CC_CHG, BeFloat(1f), 0x00),
            Payload(packets[0]));
    }

    [Fact]
    public void CcRechg_IsOpcode0x15_AndEncodesByteIdenticallyToCcChgApartFromTheOpcodeByte()
    {
        // CC_ReChg mirrors CC_CHG's NominalConfig shape exactly (single Current field, ACN-family
        // battery-relative units accepted) and isn't special-cased in DecoderService's dispatch
        // switch, so it falls through to the same ProcessDefaultOperator path as CC_CHG. The wire
        // bytes must therefore be identical apart from the opcode byte itself.
        Assert.Equal((byte)0x15, OperatorConstants.CC_RECHG);

        var rechg = Step(1, OperatorConstants.CC_RECHG);
        rechg.NominalValues = new List<string> { "20 A" };
        rechg.Limits = new List<string> { "> 60 min" };

        var chg = Step(1, OperatorConstants.CC_CHG);
        chg.NominalValues = new List<string> { "20 A" };
        chg.Limits = new List<string> { "> 60 min" };

        var rechgPayload = Payload(DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { rechg })[0]);
        var chgPayload = Payload(DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { chg })[0]);

        Assert.Equal(OperatorConstants.CC_RECHG, rechgPayload[2]);
        Assert.Equal(OperatorConstants.CC_CHG, chgPayload[2]);
        Assert.Equal(chgPayload.Skip(3), rechgPayload.Skip(3));
    }

    [Fact]
    public void CcRechg_NominalValue_Acn5_ResolvesAgainstTheBatteryJustLikeCcChg()
    {
        var withAcn5 = Step(1, OperatorConstants.CC_RECHG);
        withAcn5.NominalValues = new List<string> { "1.0 ACn5" };

        var withPlainA = Step(1, OperatorConstants.CC_RECHG);
        withPlainA.NominalValues = new List<string> { "20 A" };

        var withAcn5Packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn5 }, FullBattery());
        var withPlainAPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(withPlainAPackets[0]), Payload(withAcn5Packets[0]));
    }

    // ============================================================================= PAU is special

    [Fact]
    public void PauStep_OmitsBothTheLimitCountAndTheUnitByte()
    {
        // PAU is the one operator whose limit is bare value-only. Emitting the count/unit bytes
        // that every other operator carries would push its duration four bytes out of place.
        var step = Step(2, OperatorConstants.PAU);
        step.Limits = new List<string> { "30 min" };
        step.Registrations = new List<string> { "1 h" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(
                0x00, 0x02,
                OperatorConstants.PAU,
                BeInt(30 * 60_000),                  // duration, straight in — no count, no unit
                0x00,                                // action slot
                0x01,                                // registration count
                (byte)RegistrationType.Time,
                BeInt(1 * 3_600_000)),
            Payload(packets[0]));
    }

    // ================================================================ SET variables substitution

    [Fact]
    public void AVariableDefinedBySet_IsSubstitutedIntoLaterSteps()
    {
        // The point of SET: downstream steps name the variable, and the encoder must put the
        // *value* on the wire — the device has never heard of the name.
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "chgCurrent = 15 A" };

        var charge = Step(2, OperatorConstants.CC_CHG);
        charge.NominalValues = new List<string> { "chgCurrent" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set, charge });

        Assert.Equal(
            Bytes(0x00, 0x02, OperatorConstants.CC_CHG, BeFloat(15f), 0x00),
            Payload(packets[1]));
    }

    // ===================================================================== PRODUCER inlining

    [Fact]
    public void ProducerStep_IsReplacedByTheReferencedProgramsBody()
    {
        // The device has no concept of a sub-program: a PRODUCER must be gone by the time bytes
        // are built, with the referenced program's SET and STO stripped and its body inlined.
        var inner = new List<StepModel>
        {
            Step(1, OperatorConstants.SET),
            Step(2, OperatorConstants.CC_CHG),
            Step(3, OperatorConstants.STO),
        };
        inner[1].NominalValues = new List<string> { "5 A" };

        var producer = Step(1, OperatorConstants.PRODUCER);
        producer.NominalValues = new List<string> { "SubProg" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(
            new List<StepModel> { producer },
            new Dictionary<string, List<StepModel>> { ["SubProg"] = inner });

        // One packet, and it is the inner charge step renumbered to 1 — not a PRODUCER opcode.
        Assert.Single(packets);
        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.CC_CHG, BeFloat(5f), 0x00),
            Payload(packets[0]));
    }

    [Fact]
    public void ProducerExpansion_DoesNotMutateTheReferencedProgram()
    {
        // Steps are renumbered during inlining. Renumbering the caller's own objects would corrupt
        // the saved sub-program for every later use in the same session.
        var inner = new List<StepModel>
        {
            Step(1, OperatorConstants.SET),
            Step(2, OperatorConstants.CC_CHG),
            Step(3, OperatorConstants.STO),
        };

        var producer = Step(9, OperatorConstants.PRODUCER);
        producer.NominalValues = new List<string> { "SubProg" };

        DecoderService.ConvertProgramIntoBytesPackets(
            new List<StepModel> { producer },
            new Dictionary<string, List<StepModel>> { ["SubProg"] = inner });

        Assert.Equal(new[] { 1, 2, 3 }, inner.Select(s => s.StepNumber));
    }

    [Fact]
    public void ProducerExpansion_RENUMBERS_THE_CALLERS_OWN_STEPS_IN_PLACE()
    {
        // ⚠ Documents a real side effect, not a desirable one. Inlining deep-clones the *referenced*
        // program's steps but appends the outer program's own steps BY REFERENCE, then renumbers
        // everything from 1. So encoding a program that contains a PRODUCER silently rewrites the
        // caller's StepNumbers — the editor's in-memory model included. Here step 10 becomes step 2
        // purely as a side effect of building bytes.
        var charge = Step(10, OperatorConstants.CC_CHG);
        var producer = Step(1, OperatorConstants.PRODUCER);
        producer.NominalValues = new List<string> { "SubProg" };

        var outer = new List<StepModel> { producer, charge };

        DecoderService.ConvertProgramIntoBytesPackets(outer, new Dictionary<string, List<StepModel>>
        {
            ["SubProg"] = new()
            {
                Step(1, OperatorConstants.SET),
                Step(2, OperatorConstants.CC_CHG),
                Step(3, OperatorConstants.STO),
            },
        });

        Assert.Equal(2, charge.StepNumber);   // was 10 before the call
    }

    [Fact]
    public void WithoutResolvedPrograms_TheTwoOverloadsAgree()
    {
        var steps = new List<StepModel> { Step(1, OperatorConstants.STO) };

        var withNull = DecoderService.ConvertProgramIntoBytesPackets(steps, null);
        var direct = DecoderService.ConvertProgramIntoBytesPackets(steps);

        Assert.Equal(direct.Count, withNull.Count);
        Assert.Equal(direct[0], withNull[0]);
    }

    // ======================================================================= whole-program shape

    [Fact]
    public void AWholeProgram_ProducesOnePacketPerStepInProgramOrder()
    {
        var charge = Step(2, OperatorConstants.CC_CHG);
        charge.NominalValues = new List<string> { "2 A" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            Step(1, OperatorConstants.SET),
            charge,
            Step(3, OperatorConstants.PAU),
            Step(4, OperatorConstants.STO),
        });

        Assert.Equal(4, packets.Count);
        Assert.Equal(new byte[] { OperatorConstants.SET, OperatorConstants.CC_CHG, OperatorConstants.PAU, OperatorConstants.STO },
                     packets.Select(p => Payload(p)[2]));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, packets.Select(p => Payload(p)[1]));
    }

    private static byte[] BeShort(int value)
    {
        var b = BitConverter.GetBytes((short)value);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    // ============================================================ ACNx / VN battery-relative units

    // Manual's own worked example (docs/manual-extract/VNC-ACN-battery-parameters.md, p.186):
    // 100 Ah / 6-cell battery, 1.0 ACn5 -> 20 A, 2.35 VN -> 14.1 V.
    private static BatteryDTO Battery(float nominalCapacity = 100, int cells = 6) => new()
    {
        Name = "Test Battery",
        Producer = "Test",
        MaximumVoltage = 100,
        BreakVoltage = 1,
        NominalCapacity = nominalCapacity,
        NumberOfCells = cells,
    };

    [Fact]
    public void NominalValue_Acn5_EncodesIdenticallyToTheEquivalentPlainAmps()
    {
        var withAcn5 = Step(1, OperatorConstants.CC_CHG);
        withAcn5.NominalValues = new List<string> { "1.0 ACn5" };

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "20 A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn5 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void NominalValue_Vnc_EncodesIdenticallyToTheEquivalentPlainVolts()
    {
        // 2.35f * 6 in float arithmetic is 14.099999... not the decimal-exact 14.1 — compute the
        // expected value the same way the resolver does rather than hand-typing a float literal.
        float expectedVolts = 2.35f * 6;

        var withVnc = Step(1, OperatorConstants.CV_CHG);
        withVnc.NominalValues = new List<string> { "2.35 VN" };

        var withPlainV = Step(1, OperatorConstants.CV_CHG);
        withPlainV.NominalValues = new List<string> { $"{expectedVolts.ToString(System.Globalization.CultureInfo.InvariantCulture)} V" };

        var vncPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withVnc }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainV });

        Assert.Equal(Payload(plainPackets[0]), Payload(vncPackets[0]));
    }

    [Fact]
    public void NominalValue_AcnFamily_WithoutABattery_Throws()
    {
        var step = Step(1, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "1.0 ACn5" };

        Assert.Throws<BatteryUnitResolutionException>(() =>
            DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step }));
    }

    [Fact]
    public void Limit_Acn5_ResolvesCutoffConditionAndValueTogether()
    {
        // A limit written with ACN5 must both (a) tag the cutoff-condition byte as Current — the
        // same byte "A" would produce — and (b) carry the battery-scaled value, not the raw one.
        var withAcn5 = Step(1, OperatorConstants.CC_CHG);
        withAcn5.NominalValues = new List<string> { "1 A" };
        withAcn5.Limits = new List<string> { "> 0.5 ACn5" };   // -> 0.5 * (100Ah/5h) = 10 A

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "1 A" };
        withPlainA.Limits = new List<string> { "> 10 A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn5 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void Limit_Vnc_ResolvesCutoffConditionAndValueTogether()
    {
        float expectedVolts = 2.48f * 6; // compute in float, same as the resolver — avoid a literal-rounding mismatch

        var withVnc = Step(1, OperatorConstants.CC_CHG);
        withVnc.NominalValues = new List<string> { "1 A" };
        withVnc.Limits = new List<string> { "> 2.48 VN" };    // -> 2.48 * 6 cells

        var withPlainV = Step(1, OperatorConstants.CC_CHG);
        withPlainV.NominalValues = new List<string> { "1 A" };
        withPlainV.Limits = new List<string> { $"> {expectedVolts.ToString(System.Globalization.CultureInfo.InvariantCulture)} V" };

        var vncPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withVnc }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainV });

        Assert.Equal(Payload(plainPackets[0]), Payload(vncPackets[0]));
    }

    [Fact]
    public void Registration_Acn5_ResolvesRegistrationTypeAndValueTogether()
    {
        // Regression guard: TryParseRegistration used to strip every digit from the whole input
        // (not just the leading number), which mangled "ACN5" into "ACN" and "ACN10" into "ACN1".
        var withAcn5 = Step(1, OperatorConstants.CC_CHG);
        withAcn5.NominalValues = new List<string> { "1 A" };
        withAcn5.Registrations = new List<string> { "0.5 ACn5" };  // -> 10 A

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "1 A" };
        withPlainA.Registrations = new List<string> { "10 A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn5 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void NominalValue_ArbitraryAcnDivisor_EncodesIdenticallyToTheEquivalentPlainAmps()
    {
        // ACN7 isn't one of the manual's own named C-rates (1/2/4/5/10/20) — this pins that the
        // resolver, and the CutoffCondition/RegistrationType byte mapping downstream, both accept
        // any positive-integer hour divisor.
        float expectedAmps = 1.0f * (100f / 7f);

        var withAcn7 = Step(1, OperatorConstants.CC_CHG);
        withAcn7.NominalValues = new List<string> { "1.0 ACN7" };

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { $"{expectedAmps.ToString(System.Globalization.CultureInfo.InvariantCulture)} A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn7 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void Limit_ArbitraryAcnDivisor_ResolvesCutoffConditionAndValueTogether()
    {
        // Same as Limit_Acn5_ResolvesCutoffConditionAndValueTogether above, but for a divisor not
        // in the old fixed set — pins that TryParseUnit still tags the cutoff-condition byte as
        // Current for it.
        var withAcn7 = Step(1, OperatorConstants.CC_CHG);
        withAcn7.NominalValues = new List<string> { "1 A" };
        withAcn7.Limits = new List<string> { "> 0.5 ACN7" };   // -> 0.5 * (100Ah/7h) A

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "1 A" };
        float expectedAmps = 0.5f * (100f / 7f);
        withPlainA.Limits = new List<string> { $"> {expectedAmps.ToString(System.Globalization.CultureInfo.InvariantCulture)} A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn7 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void Registration_ArbitraryAcnDivisor_ResolvesRegistrationTypeAndValueTogether()
    {
        // Same as Registration_Acn5_ResolvesRegistrationTypeAndValueTogether above, but for a
        // divisor not in the old fixed set — pins that TryParseRegistration still tags the
        // registration-type byte as Current for it.
        var withAcn7 = Step(1, OperatorConstants.CC_CHG);
        withAcn7.NominalValues = new List<string> { "1 A" };
        withAcn7.Registrations = new List<string> { "0.5 ACN7" };

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "1 A" };
        float expectedAmps = 0.5f * (100f / 7f);
        withPlainA.Registrations = new List<string> { $"{expectedAmps.ToString(System.Globalization.CultureInfo.InvariantCulture)} A" };

        var acnPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn7 }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(acnPackets[0]));
    }

    [Fact]
    public void Registration_Acn10_IsNotMangledIntoAcn1ByDigitStripping()
    {
        var withAcn10 = Step(1, OperatorConstants.CC_CHG);
        withAcn10.NominalValues = new List<string> { "1 A" };
        withAcn10.Registrations = new List<string> { "1.0 ACn10" };  // -> 100Ah/10h = 10 A

        var withAcn1 = Step(1, OperatorConstants.CC_CHG);
        withAcn1.NominalValues = new List<string> { "1 A" };
        withAcn1.Registrations = new List<string> { "1.0 ACn1" };    // -> 100Ah/1h = 100 A (must differ)

        var packets10 = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn10 }, Battery());
        var packets1 = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withAcn1 }, Battery());

        Assert.NotEqual(Payload(packets1[0]), Payload(packets10[0]));
    }

    // ================================================== §12.3 battery-parameter bare tokens

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
    public void NominalValue_BareInom_EncodesIdenticallyToTheEquivalentPlainAmps()
    {
        var withInom = Step(1, OperatorConstants.CC_CHG);
        withInom.NominalValues = new List<string> { "INom" };

        var withPlainA = Step(1, OperatorConstants.CC_CHG);
        withPlainA.NominalValues = new List<string> { "20 A" };

        var inomPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withInom }, FullBattery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainA });

        Assert.Equal(Payload(plainPackets[0]), Payload(inomPackets[0]));
    }

    [Fact]
    public void Limit_BareUGas_ResolvesCutoffConditionAndValueTogether()
    {
        var withUGas = Step(1, OperatorConstants.CC_CHG);
        withUGas.NominalValues = new List<string> { "1 A" };
        withUGas.Limits = new List<string> { "> UGas" };   // -> 14.4 V, cutoff = Voltage

        var withPlainV = Step(1, OperatorConstants.CC_CHG);
        withPlainV.NominalValues = new List<string> { "1 A" };
        withPlainV.Limits = new List<string> { "> 14.4 V" };

        var uGasPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withUGas }, FullBattery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainV });

        Assert.Equal(Payload(plainPackets[0]), Payload(uGasPackets[0]));
    }

    [Fact]
    public void Registration_BareCNom_ResolvesRegistrationTypeAndValueTogether()
    {
        var withCNom = Step(1, OperatorConstants.CC_CHG);
        withCNom.NominalValues = new List<string> { "1 A" };
        withCNom.Registrations = new List<string> { "CNom" };   // -> 100 Ah, RegistrationType = AccumulatedCapacity

        var withPlainAh = Step(1, OperatorConstants.CC_CHG);
        withPlainAh.NominalValues = new List<string> { "1 A" };
        withPlainAh.Registrations = new List<string> { "100 Ah" };

        var cNomPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withCNom }, FullBattery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainAh });

        Assert.Equal(Payload(plainPackets[0]), Payload(cNomPackets[0]));
    }

    [Fact]
    public void NominalValue_SetDefinedVariable_WinsOverImplicitBatteryValueOnNameCollision()
    {
        // User intent (an explicit SET) must always beat the implicit battery-derived value.
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "INom = 5 A" };

        var charge = Step(2, OperatorConstants.CC_CHG);
        charge.NominalValues = new List<string> { "INom" };

        var withOverride = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set, charge }, FullBattery());

        var plainCharge = Step(2, OperatorConstants.CC_CHG);
        plainCharge.NominalValues = new List<string> { "5 A" };
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { Step(1, OperatorConstants.SET), plainCharge });

        // Compare only the CC_CHG step (index 1) — the SET step's own encoding is unaffected.
        Assert.Equal(Payload(plainPackets[1]), Payload(withOverride[1]));
    }

    [Fact]
    public void NominalValue_BareInom_WithoutABattery_LeavesValueUnresolved()
    {
        // Unlike ACNx/VN, a bare battery-parameter name with no matching battery/SET variable
        // is just an unmatched identifier — ExtractFloatAsByteArraySafe can't parse "INom" as a
        // number, so no bytes are emitted for it (same as any other unresolvable bare word today
        // — this is pre-existing ProcessNominalValues behavior, not a new failure mode).
        var step = Step(1, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "INom" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(Bytes(0x00, 0x01, OperatorConstants.CC_CHG, 0x00), Payload(packets[0]));
    }

    // ==================================================================== LOCKAh (opcode 0x16)

    [Fact]
    public void LockAhStep_EncodesAsOpcodePlusFloatMultiplier_WithNoCutoffOrRegistrationBytes()
    {
        // Same minimal shape as GOTO: operator + one fixed payload + end. Getting this wrong (e.g.
        // falling through to ProcessDefaultOperator's registration tail) would append a stray byte
        // that shifts every step after this one in the program.
        Assert.Equal((byte)0x16, OperatorConstants.LOCKAH);

        var step = Step(3, OperatorConstants.LOCKAH);
        step.NominalValues = new List<string> { "1.0" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(0x00, 0x03, OperatorConstants.LOCKAH, BeFloat(1.0f)),
            Payload(packets[0]));
    }

    [Fact]
    public void LockAhStep_EncodesAnyMultiplierGreaterThanOne()
    {
        var step = Step(1, OperatorConstants.LOCKAH);
        step.NominalValues = new List<string> { "1.1" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.LOCKAH, BeFloat(1.1f)),
            Payload(packets[0]));
    }

    // ============================================================== PERCAh cutoff (0x3E)

    [Fact]
    public void PercAhLimit_EncodesCutoffCondition0x3E()
    {
        var step = Step(6, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "1.0 A" };
        step.Limits = new List<string> { "> 130 PERCAh" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step });

        Assert.Equal(
            Bytes(
                0x00, 0x06,
                OperatorConstants.CC_CHG,
                BeFloat(1.0f),
                0x01,
                (byte)CutoffCondition.PercAh,
                (byte)LogicOperator.GreaterThan,
                BeFloat(130f),
                0x00,
                0x00),
            Payload(packets[0]));
    }

    // ======================================================= SET global parameters (Ah / Wh)

    [Fact]
    public void SetStep_WithAhVariable_EmitsAGlobalParameterBlockBeforeTheRegisterMask()
    {
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "Ah = 0" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(
            Bytes(
                0x00, 0x01, OperatorConstants.SET,
                0x01,                                          // 1 global parameter
                (byte)RegistrationType.AccumulatedCapacity,     // 0x26
                BeFloat(0f),
                BeShort(AlwaysOnMask)),
            Payload(packets[0]));
    }

    [Fact]
    public void SetStep_WithWhVariable_UsesTheAccumulatedEnergyCode()
    {
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "Wh = -640.5" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(
            Bytes(
                0x00, 0x01, OperatorConstants.SET,
                0x01,
                (byte)RegistrationType.AccumulatedEnergy,       // 0x2A
                BeFloat(-640.5f),
                BeShort(AlwaysOnMask)),
            Payload(packets[0]));
    }

    [Fact]
    public void SetStep_WithBothAhAndWh_EmitsTwoBlocksInDeclaredOrder()
    {
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "Ah = 0", "Wh = 0" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(
            Bytes(
                0x00, 0x01, OperatorConstants.SET,
                0x02,
                (byte)RegistrationType.AccumulatedCapacity, BeFloat(0f),
                (byte)RegistrationType.AccumulatedEnergy, BeFloat(0f),
                BeShort(AlwaysOnMask)),
            Payload(packets[0]));
    }

    [Fact]
    public void SetStep_WithAnUnrelatedNamedVariable_StaysByteIdenticalToBeforeThisFeature()
    {
        // "Ah"/"Wh" are the only names that become wire-level global parameters. Every other SET
        // variable name is purely an in-app reference substitution, exactly as it was before -
        // this pins the N=0 backward-compatibility guarantee.
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "chgCurrent = 15 A" };

        var packets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(
            Bytes(0x00, 0x01, OperatorConstants.SET, 0x00, BeShort(AlwaysOnMask)),
            Payload(packets[0]));
    }

    [Fact]
    public void SetStep_WithAhVariable_DefaultsRegistrationToStandardWhenNoneWasChosen()
    {
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "Ah = 0" };
        Assert.Empty(set.Registrations);

        DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(new List<string> { "STANDARD" }, set.Registrations);
    }

    [Fact]
    public void SetStep_WithAhVariable_DoesNotOverrideAnExplicitlyChosenRegistration()
    {
        var set = Step(1, OperatorConstants.SET);
        set.NominalValues = new List<string> { "Ah = 0" };
        set.Registrations = new List<string> { "CUSTOM" };

        DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { set });

        Assert.Equal(new List<string> { "CUSTOM" }, set.Registrations);
    }

    [Fact]
    public void SetStep_WithAhVariable_InheritsTheNearestPrecedingSetsRegistrationRatherThanStandard()
    {
        // The reference program's second "SET Ah = 0" (opening the charge block) should carry
        // forward whatever registration the first block's SET actually used - not be forced back
        // to a hardcoded STANDARD every time, the same way REG already inherits from its preceding
        // SET (RegStep_WithoutItsOwnRegistrations_InheritsTheNearestPrecedingSet, above).
        var firstSet = Step(1, OperatorConstants.SET);
        firstSet.NominalValues = new List<string> { "Ah = 0" };
        firstSet.Registrations = new List<string> { "MYSTD" };

        var secondSet = Step(4, OperatorConstants.SET);
        secondSet.NominalValues = new List<string> { "Ah = 0" };
        Assert.Empty(secondSet.Registrations);

        DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel>
        {
            firstSet,
            Step(2, OperatorConstants.CC_DCHG),
            Step(3, OperatorConstants.LOCKAH),
            secondSet,
        });

        Assert.Equal(new List<string> { "MYSTD" }, secondSet.Registrations);
    }

    [Fact]
    public void SetStep_WithAhVariable_FallsBackToStandard_WhenThePrecedingSetHasNoRegistrationEither()
    {
        var firstSet = Step(1, OperatorConstants.SET);   // no Ah/Wh, no registration - a plain block opener
        var secondSet = Step(2, OperatorConstants.SET);
        secondSet.NominalValues = new List<string> { "Ah = 0" };

        DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { firstSet, secondSet });

        Assert.Equal(new List<string> { "STANDARD" }, secondSet.Registrations);
    }

    // ============================================================ Coefficient'd CNom ("0.8 CNom")
    // program_packet_v0.15.md §14.10: a coefficient in front of the bare CNom token ("0.8 CNom",
    // "0.1 CNom") is distinct from CNom used alone (see Registration_BareCNom_... above, which is
    // Accumulated Capacity with no multiplier) — it must resolve to an absolute Step Capacity
    // value, because it counts the Ah delivered during THIS step, not across the whole program.

    [Fact]
    public void Limit_CoefficientCNom_ResolvesToStepCapacity()
    {
        var withCNom = Step(1, OperatorConstants.CC_CHG);
        withCNom.NominalValues = new List<string> { "1 A" };
        withCNom.Limits = new List<string> { "= 0.8 CNom" };   // -> 100Ah battery: 0.8 * 100 = 80 AhStep

        var withPlainAhStep = Step(1, OperatorConstants.CC_CHG);
        withPlainAhStep.NominalValues = new List<string> { "1 A" };
        withPlainAhStep.Limits = new List<string> { "= 80 AhStep" };

        var cNomPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withCNom }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainAhStep });

        Assert.Equal(Payload(plainPackets[0]), Payload(cNomPackets[0]));
    }

    [Fact]
    public void Limit_CoefficientCNom_DiffersFromAccumulatedCapacity()
    {
        // Guards against silently mapping CNom to plain "Ah" (Accumulated Capacity, 0x3A) instead
        // of "AhStep" (Step Capacity, 0x3B) — they are NOT byte-identical.
        var withCNom = Step(1, OperatorConstants.CC_CHG);
        withCNom.NominalValues = new List<string> { "1 A" };
        withCNom.Limits = new List<string> { "= 0.8 CNom" };

        var withPlainAh = Step(1, OperatorConstants.CC_CHG);
        withPlainAh.NominalValues = new List<string> { "1 A" };
        withPlainAh.Limits = new List<string> { "= 80 Ah" };

        var cNomPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withCNom }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainAh });

        Assert.NotEqual(Payload(plainPackets[0]), Payload(cNomPackets[0]));
    }

    [Fact]
    public void Registration_CoefficientCNom_ResolvesToStepCapacity()
    {
        var withCNom = Step(1, OperatorConstants.CC_CHG);
        withCNom.NominalValues = new List<string> { "1 A" };
        withCNom.Registrations = new List<string> { "0.1 CNom" };   // -> 100Ah battery: 0.1 * 100 = 10 AhStep

        var withPlainAhStep = Step(1, OperatorConstants.CC_CHG);
        withPlainAhStep.NominalValues = new List<string> { "1 A" };
        withPlainAhStep.Registrations = new List<string> { "10 AhStep" };

        var cNomPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withCNom }, Battery());
        var plainPackets = DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { withPlainAhStep });

        Assert.Equal(Payload(plainPackets[0]), Payload(cNomPackets[0]));
    }

    [Fact]
    public void Limit_CoefficientCNom_WithoutABattery_Throws()
    {
        var step = Step(1, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "1 A" };
        step.Limits = new List<string> { "= 0.8 CNom" };

        Assert.Throws<BatteryUnitResolutionException>(() =>
            DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step }));
    }

    [Fact]
    public void Registration_CoefficientCNom_WithoutABattery_Throws()
    {
        var step = Step(1, OperatorConstants.CC_CHG);
        step.NominalValues = new List<string> { "1 A" };
        step.Registrations = new List<string> { "0.1 CNom" };

        Assert.Throws<BatteryUnitResolutionException>(() =>
            DecoderService.ConvertProgramIntoBytesPackets(new List<StepModel> { step }));
    }
}
