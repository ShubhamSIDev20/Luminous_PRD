using System;
using System.Linq;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

/// <summary>
/// BigEndian / LittleEndian are hand-rolled shift-and-mask codecs sitting directly on the
/// hardware wire protocol, so a single transposed shift silently corrupts every reading
/// from a device rather than throwing.
///
/// Round-tripping alone cannot catch that: GetBytes and To* are mirror images of the same
/// shift table, so a swapped pair agrees with itself and the round-trip still passes. Every
/// test here therefore pins the byte order against something independent - an explicit
/// literal layout, or BitConverter - never against the other direction of the same class.
/// </summary>
public class EndianTests
{
    // ---------------------------------------------------------------- byte order

    [Fact]
    public void BigEndian_PutsMostSignificantByteFirst()
    {
        Assert.Equal(new byte[] { 0x12, 0x34 }, BigEndian.GetBytes((short)0x1234));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x56, 0x78 }, BigEndian.GetBytes(0x12345678));
        Assert.Equal(
            new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF },
            BigEndian.GetBytes(0x0123456789ABCDEFL));
    }

    [Fact]
    public void LittleEndian_PutsLeastSignificantByteFirst()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, LittleEndian.GetBytes((short)0x1234));
        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, LittleEndian.GetBytes(0x12345678));
        Assert.Equal(
            new byte[] { 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01 },
            LittleEndian.GetBytes(0x0123456789ABCDEFL));
    }

    [Fact]
    public void TheTwoCodecs_AreExactByteReversalsOfEachOther()
    {
        Assert.Equal(BigEndian.GetBytes(0x12345678).Reverse(), LittleEndian.GetBytes(0x12345678));
        Assert.Equal(BigEndian.GetBytes(0x0123456789ABCDEFL).Reverse(), LittleEndian.GetBytes(0x0123456789ABCDEFL));
    }

    // -------------------------------------------------- independent cross-check

    [Fact]
    public void GetBytes_AgreesWithBitConverter()
    {
        // BitConverter uses the host's byte order. Anchoring to it (rather than to the
        // other direction of our own codec) is what makes this an independent check.
        byte[] Host(byte[] littleEndianBytes) =>
            BitConverter.IsLittleEndian ? littleEndianBytes : littleEndianBytes.Reverse().ToArray();

        Assert.Equal(BitConverter.GetBytes((short)-12345), Host(LittleEndian.GetBytes((short)-12345)));
        Assert.Equal(BitConverter.GetBytes(-123456789), Host(LittleEndian.GetBytes(-123456789)));
        Assert.Equal(BitConverter.GetBytes(-1234567890123L), Host(LittleEndian.GetBytes(-1234567890123L)));
        Assert.Equal(BitConverter.GetBytes(0xDEADBEEFu), Host(LittleEndian.GetBytes(0xDEADBEEFu)));
    }

    [Fact]
    public void To_AgreesWithBitConverter()
    {
        byte[] bytes = { 0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78 };
        byte[] hostOrder = BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();

        Assert.Equal(BitConverter.ToInt64(hostOrder, 0), LittleEndian.ToInt64(bytes));
        Assert.Equal(BitConverter.ToInt32(BitConverter.IsLittleEndian ? bytes : bytes.Take(4).Reverse().ToArray(), 0),
                     LittleEndian.ToInt32(bytes));
    }

    // ---------------------------------------------------------- sign boundaries

    [Theory]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    [InlineData((short)-1)]
    [InlineData((short)0)]
    public void Int16_SurvivesRoundTripAtSignBoundaries(short value)
    {
        Assert.Equal(value, BigEndian.ToInt16(BigEndian.GetBytes(value)));
        Assert.Equal(value, LittleEndian.ToInt16(LittleEndian.GetBytes(value)));
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    public void Int32_SurvivesRoundTripAtSignBoundaries(int value)
    {
        Assert.Equal(value, BigEndian.ToInt32(BigEndian.GetBytes(value)));
        Assert.Equal(value, LittleEndian.ToInt32(LittleEndian.GetBytes(value)));
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    public void Int64_SurvivesRoundTripAtSignBoundaries(long value)
    {
        Assert.Equal(value, BigEndian.ToInt64(BigEndian.GetBytes(value)));
        Assert.Equal(value, LittleEndian.ToInt64(LittleEndian.GetBytes(value)));
    }

    [Fact]
    public void NegativeOne_IsAllOnes_InBothOrders()
    {
        Assert.Equal(new byte[] { 0xFF, 0xFF }, BigEndian.GetBytes((short)-1));
        Assert.Equal(new byte[] { 0xFF, 0xFF }, LittleEndian.GetBytes((short)-1));
        Assert.Equal(-1, BigEndian.ToInt32(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
        Assert.Equal(-1, LittleEndian.ToInt32(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }));
    }

    // ------------------------------------------ signed vs unsigned reinterpretation

    [Fact]
    public void SameBytes_ReadSignedAndUnsigned_DifferAsExpected()
    {
        // The signed and unsigned readers share a shift table but differ in cast; a
        // sign-extension slip shows up here and nowhere else.
        byte[] allOnes = { 0xFF, 0xFF, 0xFF, 0xFF };

        Assert.Equal(-1, BigEndian.ToInt32(allOnes));
        Assert.Equal(uint.MaxValue, BigEndian.ToUInt32(allOnes));
        Assert.Equal(-1, LittleEndian.ToInt32(allOnes));
        Assert.Equal(uint.MaxValue, LittleEndian.ToUInt32(allOnes));

        byte[] highBitSet = { 0x80, 0x00 };
        Assert.Equal(short.MinValue, BigEndian.ToInt16(highBitSet));
        Assert.Equal((ushort)0x8000, BigEndian.ToUInt16(highBitSet));
    }

    [Theory]
    [InlineData(ulong.MaxValue)]
    [InlineData(0UL)]
    [InlineData(0x8000000000000000UL)]
    public void UInt64_SurvivesRoundTrip(ulong value)
    {
        Assert.Equal(value, BigEndian.ToUInt64(BigEndian.GetBytes(value)));
        Assert.Equal(value, LittleEndian.ToUInt64(LittleEndian.GetBytes(value)));
    }

    // -------------------------------------------------------------- offset reads

    [Fact]
    public void Offset_ReadsFromTheMiddleOfAFrame()
    {
        // Real packets are read field-by-field out of one buffer, so a decoder that
        // ignored `offset` would still pass every offset-0 test above.
        byte[] frame = { 0xAA, 0xBB, 0x12, 0x34, 0x56, 0x78, 0xCC };

        Assert.Equal(0x1234, BigEndian.ToUInt16(frame, 2));
        Assert.Equal(0x5678, BigEndian.ToUInt16(frame, 4));
        Assert.Equal(0x12345678, BigEndian.ToInt32(frame, 2));

        Assert.Equal(0x3412, LittleEndian.ToUInt16(frame, 2));
        Assert.Equal(0x78563412, LittleEndian.ToInt32(frame, 2));
    }

    [Fact]
    public void Offset_PastEndOfBuffer_ThrowsIndexOutOfRange()
    {
        // Documents current behaviour: these codecs do no bounds checking of their own
        // and rely on the array indexer to fail.
        byte[] tooShort = { 0x01, 0x02 };

        Assert.Throws<IndexOutOfRangeException>(() => BigEndian.ToInt32(tooShort));
        Assert.Throws<IndexOutOfRangeException>(() => LittleEndian.ToInt32(tooShort));
        Assert.Throws<IndexOutOfRangeException>(() => BigEndian.ToUInt16(tooShort, 1));
    }
}
