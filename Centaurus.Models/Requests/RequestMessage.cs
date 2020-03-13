using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class RequestMessage: Message
    {
        public override ulong MessageId => Nonce;
        /// <summary>
        /// An account that initiated a quantum request.
        /// </summary>
        [XdrField(0)]
        public RawPubKey Account { get; set; }

        /// <summary>
        /// Account nonce.
        /// </summary>
        [XdrField(1)]
        public ulong Nonce { get; set; }
    }
}
