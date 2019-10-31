using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Contains all quanta that were processed after the last snapshot
    /// </summary>
    [XdrContract]
    public class PendingQuanta
    {
        [XdrField(0)]
        public List<MessageEnvelope> Quanta { get; set; } = new List<MessageEnvelope>();
    }
}
