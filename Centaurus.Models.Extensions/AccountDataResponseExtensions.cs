using Centaurus.Xdr;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Models
{
    public static class AccountDataResponseExtensions
    {
        public static byte[] ComputePayloadHash(this AccountDataResponse accountDataResponse)
        {
            if (accountDataResponse == null)
                throw new ArgumentNullException(nameof(accountDataResponse));

            var sequenceBuffer = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(sequenceBuffer, accountDataResponse.Sequence);

            var payloadBytes = accountDataResponse.Balances.SelectMany(b => XdrConverter.Serialize(b))
                .Concat(accountDataResponse.Orders.SelectMany(o => XdrConverter.Serialize(o)))
                .Concat(sequenceBuffer)
                .ToArray();
            return payloadBytes.ComputeHash();
        }
    }
}