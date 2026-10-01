namespace BatteryTestingSystem.Utils
{
    public class ChannelAddressCodec
    {
        public static byte Encode(int boardNumber, int channelNumber)
        {
            if (boardNumber < 0 || boardNumber > 8)
                throw new System.ArgumentOutOfRangeException(nameof(boardNumber), boardNumber, "BoardNumber must be 0-8.");
            if (channelNumber < 1 || channelNumber > 8)
                throw new System.ArgumentOutOfRangeException(nameof(channelNumber), channelNumber, "ChannelNumber must be 1-8.");

            return (byte)(((boardNumber & 0xF) << 4) | (channelNumber & 0xF));
        }

        public static (int BoardNumber, int ChannelNumber) Decode(byte value)
        {
            return ((value >> 4) & 0xF, value & 0xF);
        }

        public static byte EncodeLegacy(int channelNumber) => Encode(1, channelNumber);
    }
}
