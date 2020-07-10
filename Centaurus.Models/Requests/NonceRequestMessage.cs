using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class NonceRequestMessage: RequestMessage
    {
        public override ulong MessageId => Nonce;

        /// <summary>
        /// Account nonce.
        /// </summary>
        [XdrField(0)]
        public ulong Nonce { get; set; }
    }
}
