using System;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

public class ChannelAddressCodecTests
{
    [Theory]
    [InlineData(1, 1, 0x11)]
    [InlineData(1, 8, 0x18)]
    [InlineData(8, 1, 0x81)]
    [InlineData(8, 8, 0x88)]
    [InlineData(3, 5, 0x35)]
    public void Encode_PacksBoardAndChannelIntoNibbles(int board, int channel, byte expected)
    {
        Assert.Equal(expected, ChannelAddressCodec.Encode(board, channel));
    }

    // Board and channel are NOT symmetric: board is 0-8, channel is 1-8. Board 0
    // is a legal address (no secondary board), so Encode must accept it. A test
    // row asserting Encode(0, 1) throws was removed - it contradicted the guard
    // in ChannelAddressCodec.Encode, which reads `boardNumber < 0`, not `< 1`.
    [Theory]
    [InlineData(0, 1, 0x01)]
    [InlineData(0, 8, 0x08)]
    public void Encode_AcceptsBoardZero_MeaningNoSecondaryBoard(int board, int channel, byte expected)
    {
        Assert.Equal(expected, ChannelAddressCodec.Encode(board, channel));
    }

    [Theory]
    [InlineData(0x11, 1, 1)]
    [InlineData(0x18, 1, 8)]
    [InlineData(0x81, 8, 1)]
    [InlineData(0x88, 8, 8)]
    public void Decode_UnpacksBoardAndChannelFromNibbles(byte value, int expectedBoard, int expectedChannel)
    {
        var (board, channel) = ChannelAddressCodec.Decode(value);
        Assert.Equal(expectedBoard, board);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(1, 8)]
    [InlineData(8, 1)]
    [InlineData(8, 8)]
    [InlineData(4, 4)]
    public void EncodeThenDecode_RoundTrips(int board, int channel)
    {
        byte encoded = ChannelAddressCodec.Encode(board, channel);
        var (decodedBoard, decodedChannel) = ChannelAddressCodec.Decode(encoded);
        Assert.Equal(board, decodedBoard);
        Assert.Equal(channel, decodedChannel);
    }

    [Theory]
    [InlineData(9, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    [InlineData(-1, 1)]
    public void Encode_OutOfRange_Throws(int board, int channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelAddressCodec.Encode(board, channel));
    }

    [Fact]
    public void EncodeLegacy_DefaultsToBoardOne()
    {
        Assert.Equal(ChannelAddressCodec.Encode(1, 5), ChannelAddressCodec.EncodeLegacy(5));
    }

    [Fact]
    public void Decode_NeverThrows_EvenForNonsenseByte()
    {
        var (board, channel) = ChannelAddressCodec.Decode(0xFF);
        Assert.Equal(15, board);
        Assert.Equal(15, channel);
    }

    // Decode is deliberately permissive where Encode is strict, so the pair is NOT
    // symmetric in the Decode -> Encode direction. These tests pin that asymmetry
    // so it stays a decision rather than becoming a surprise: a byte off the wire
    // can decode fine and then throw when re-encoded.
    [Theory]
    [InlineData(0x00, 0, 0)]  // channel 0 - below Encode's minimum of 1
    [InlineData(0x09, 0, 9)]  // channel 9 - above Encode's maximum of 8
    [InlineData(0x90, 9, 0)]  // board 9   - above Encode's maximum of 8
    public void Decode_AcceptsAddressesThatEncodeWouldReject(byte value, int expectedBoard, int expectedChannel)
    {
        var (board, channel) = ChannelAddressCodec.Decode(value);
        Assert.Equal(expectedBoard, board);
        Assert.Equal(expectedChannel, channel);

        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelAddressCodec.Encode(board, channel));
    }

    [Fact]
    public void DecodeThenEncode_RoundTripsForEveryAddressEncodeAccepts()
    {
        for (int board = 0; board <= 8; board++)
        for (int channel = 1; channel <= 8; channel++)
        {
            byte encoded = ChannelAddressCodec.Encode(board, channel);
            var (b, c) = ChannelAddressCodec.Decode(encoded);
            Assert.Equal((board, channel), (b, c));
        }
    }
}
