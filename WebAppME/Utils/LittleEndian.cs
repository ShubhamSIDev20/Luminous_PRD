namespace BatteryTestingSystem.Utils
{
    public class LittleEndian
    {
        public static short ToInt16(byte[] bytes, int offset = 0)
        {
            return (short)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        public static ushort ToUInt16(byte[] bytes, int offset = 0)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        public static int ToInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset] | (bytes[offset + 1] << 8) |
                   (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
        }

        public static uint ToUInt32(byte[] bytes, int offset = 0)
        {
            return bytes[offset] | ((uint)bytes[offset + 1] << 8) |
                   ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24);
        }

        public static long ToInt64(byte[] bytes, int offset = 0)
        {
            return bytes[offset] | ((long)bytes[offset + 1] << 8) |
                   ((long)bytes[offset + 2] << 16) | ((long)bytes[offset + 3] << 24) |
                   ((long)bytes[offset + 4] << 32) | ((long)bytes[offset + 5] << 40) |
                   ((long)bytes[offset + 6] << 48) | ((long)bytes[offset + 7] << 56);
        }

        public static ulong ToUInt64(byte[] bytes, int offset = 0)
        {
            return bytes[offset] | ((ulong)bytes[offset + 1] << 8) |
                   ((ulong)bytes[offset + 2] << 16) | ((ulong)bytes[offset + 3] << 24) |
                   ((ulong)bytes[offset + 4] << 32) | ((ulong)bytes[offset + 5] << 40) |
                   ((ulong)bytes[offset + 6] << 48) | ((ulong)bytes[offset + 7] << 56);
        }

        public static byte[] GetBytes(short value)
        {
            return new byte[] { (byte)value, (byte)(value >> 8) };
        }

        public static byte[] GetBytes(ushort value)
        {
            return new byte[] { (byte)value, (byte)(value >> 8) };
        }

        public static byte[] GetBytes(int value)
        {
            return new byte[] {
                (byte)value, (byte)(value >> 8),
                (byte)(value >> 16), (byte)(value >> 24)
            };
        }

        public static byte[] GetBytes(uint value)
        {
            return new byte[] {
                (byte)value, (byte)(value >> 8),
                (byte)(value >> 16), (byte)(value >> 24)
            };
        }

        public static byte[] GetBytes(long value)
        {
            return new byte[] {
                (byte)value, (byte)(value >> 8),
                (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40),
                (byte)(value >> 48), (byte)(value >> 56)
            };
        }

        public static byte[] GetBytes(ulong value)
        {
            return new byte[] {
                (byte)value, (byte)(value >> 8),
                (byte)(value >> 16), (byte)(value >> 24),
                (byte)(value >> 32), (byte)(value >> 40),
                (byte)(value >> 48), (byte)(value >> 56)
            };
        }
    }
}
