using System.Collections.Generic;
using Centaurus.Xdr;

namespace Centaurus.Models
{
    /// <summary>
    /// Message used as a message response.
    /// </summary>
    public abstract class ResultMessageBase : Message
    {
        [XdrField(0)]
        public ResultStatusCode Status { get; set; }

        [XdrField(1, Optional = true)]
        public string ErrorMessage { get; set; }

        [XdrField(2)]
        public long OriginalMessageId { get; set; }

        public override long MessageId => OriginalMessageId;
    }
}