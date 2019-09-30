using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class RequestMessage: Message
    {
        /// <summary>
        /// An account that initiated a quantum request.
        /// </summary>
        public RawPubKey Account { get; set; }

        /// <summary>
        /// Account nonce.
        /// </summary>
        public ulong Nonce { get; set; }

        public override ulong MessageId => Nonce;
    }
}
