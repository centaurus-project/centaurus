using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class RequestMessage : Message
    {
        /// <summary>
        /// An account that initiated a quantum request.
        /// </summary>
        [XdrField(0)]
        public RawPubKey Account { get; set; }

        /// <summary>
        /// For request processing purposes. Do not serialize it.
        /// </summary>
        public AccountWrapper AccountWrapper { get; set; }
    }
}