namespace BatteryTestingSystem.Utils
{
    public class BigEndian
    {
        public static short ToInt16(byte[] bytes, int offset = 0)
        {
            return (short)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        public static ushort ToUInt16(byte[] bytes, int offset = 0)
        {
            return (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
        }

        public static int ToInt32(byte[] bytes, int offset = 0)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        public static uint ToUInt32(byte[] bytes, int offset = 0)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        public static long ToInt64(byte[] bytes, int offset = 0)
        {
            return ((long)bytes[offset] << 56) | ((long)bytes[offset + 1] << 48) |
                   ((long)bytes[offset + 2] << 40) | ((long)bytes[offset + 3] << 32) |
                   ((long)bytes[offset + 4] << 24) | ((long)bytes[offset + 5] << 16) |
                   ((long)bytes[offset + 6] << 8) | bytes[offset + 7];
        }

        public static ulong ToUInt64(byte[] bytes, int offset = 0)
        {
            return ((ulong)bytes[offset] << 56) | ((ulong)bytes[offset + 1] << 48) |
                   ((ulong)bytes[offset + 2] << 40) | ((ulong)bytes[offset + 3] << 32) |
                   ((ulong)bytes[offset + 4] << 24) | ((ulong)bytes[offset + 5] << 16) |
                   ((ulong)bytes[offset + 6] << 8) | bytes[offset + 7];
        }

        public static byte[] GetBytes(short value)
        {
            return new byte[] { (byte)(value >> 8), (byte)value };
        }

        public static byte[] GetBytes(ushort value)
        {
            return new byte[] { (byte)(value >> 8), (byte)value };
        }

        public static byte[] GetBytes(int value)
        {
            return new byte[] {
                (byte)(value >> 24), (byte)(value >> 16),
                (byte)(value >> 8), (byte)value
            };
        }

        public static byte[] GetBytes(uint value)
        {
            return new byte[] {
            (byte)(value >> 24), (byte)(value >> 16),
            (byte)(value >> 8), (byte)value
        };
        }

        public static byte[] GetBytes(long value)
        {
            return new byte[] {
                (byte)(value >> 56), (byte)(value >> 48),
                (byte)(value >> 40), (byte)(value >> 32),
                (byte)(value >> 24), (byte)(value >> 16),
                (byte)(value >> 8), (byte)value
            };
        }

        public static byte[] GetBytes(ulong value)
        {
            return new byte[] {
                (byte)(value >> 56), (byte)(value >> 48),
                (byte)(value >> 40), (byte)(value >> 32),
                (byte)(value >> 24), (byte)(value >> 16),
                (byte)(value >> 8), (byte)value
            };
        }
    }
}
