using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus
{
    /// <summary>
    /// This class contains all necessary info about request.
    /// </summary>
    public class EffectsPagingToken
    {
        /// <summary>
        /// Storage effect id. First 8 bytes is Apex, and next 4 bytes is effect index.
        /// </summary>
        public byte[] Id { get; set; } = new byte[12];

        /// <summary>
        /// The sorting field. If the value is true, then we need to do a reverse sorting.
        /// </summary>
        public bool IsDesc { get; set; }

        /// <summary>
        /// If the value is false we will take n (limit) items after the specified id, otherwise previus n items.
        /// </summary>
        public bool IsPrev { get; set; }

        /// <summary>
        /// Limit per page
        /// </summary>
        public short Limit { get; set; } = 50;

        public byte[] ToByteArray()
        {
            return Id
                .Concat(BitConverter.GetBytes(IsDesc))
                .Concat(BitConverter.GetBytes(IsPrev))
                .Concat(BitConverter.GetBytes(Limit))
                .ToArray();
        }

        public string ToBase64()
        {
            return Convert.ToBase64String(ToByteArray());
        }

        public static EffectsPagingToken FromByteArray(byte[] rawPagingToken)
        {
            if (rawPagingToken == null)
                return new EffectsPagingToken();

            if (rawPagingToken.Length != 16)
                throw new ArgumentException("Must be a valid 16 byte array.", nameof(rawPagingToken));

            return new EffectsPagingToken
            {
                Id = rawPagingToken.Take(12).ToArray(),
                IsDesc = BitConverter.ToBoolean(rawPagingToken, 12),
                IsPrev = BitConverter.ToBoolean(rawPagingToken, 13),
                Limit = BitConverter.ToInt16(rawPagingToken, 14)
            };
        }

        public static EffectsPagingToken FromBase64(string pagingToken)
        {
            if (string.IsNullOrEmpty(pagingToken))
                throw new ArgumentNullException(nameof(pagingToken));
            return FromByteArray(Convert.FromBase64String(pagingToken));
        }
    }
}
