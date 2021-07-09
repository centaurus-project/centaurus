using System;

namespace Centaurus.PersistentStorage
{
    public static class HexConverter
    {

        public static string BufferToHex(byte[] buffer)
        {
            return BitConverter.ToString(buffer).Replace("-", "");
        }

        public static byte[] HexToBuffer(string hex)
        {
            var buffer = new byte[hex.Length >> 1];

            for (var i = 0; i < hex.Length >> 1; ++i)
            {
                buffer[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return buffer;
        }

        static int GetHexVal(int val)
        {
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}
