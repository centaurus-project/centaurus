using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Centaurus.Test
{
    public static class BinaryExtensions
    {
        public static byte[] RandomBytes(this int length)
        {
            var buffer = new byte[length];
            new Random().NextBytes(buffer.AsSpan());
            return buffer;
        }
    }
}
