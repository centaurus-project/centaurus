using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class SequentialRequestMessage : RequestMessage
    {
        /// <summary>
        /// Account nonce.
        /// </summary>
        public ulong Nonce => (ulong)RequestId;
    }
}
