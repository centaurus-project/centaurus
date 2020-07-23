using System;
using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    public abstract class NonceRequestMessage: RequestMessage
    {
        /// <summary>
        /// Account nonce.
        /// </summary>
        public long Nonce { get { return RequestId; } set { RequestId = value; } }
    }
}
