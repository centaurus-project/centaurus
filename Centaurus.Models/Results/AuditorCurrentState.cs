using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    /// <summary>
    /// Contains the last snapshot and all quanta that have been handled after the last snapshot
    /// </summary>
    public class AuditorState : Message
    {
        public override MessageTypes MessageType => MessageTypes.AuditorState;

        [XdrField(0)]
        public ApplicationState State { get; set; }

        [XdrField(1, Optional = true)]
        public Snapshot LastSnapshot { get; set; }

        [XdrField(2)]
        public List<MessageEnvelope> PendingQuantums { get; set; } = new List<MessageEnvelope>();
    }
}
