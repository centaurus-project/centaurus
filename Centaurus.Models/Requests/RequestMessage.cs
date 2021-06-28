using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class RequestMessage : Message
    {
        public override long MessageId => RequestId;

        /// <summary>
        /// An account that initiated a request.
        /// </summary>
        [XdrField(0)]
        public int Account { get; set; }

        /// <summary>
        /// Client request id.
        /// </summary>
        [XdrField(1)]
        public long RequestId { get; set; }
    }
}