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

        public ApplicationState State { get; set; }

        public Snapshot LastSnapshot { get; set; }

        public List<MessageEnvelope> PendingQuantums { get; set; } = new List<MessageEnvelope>();
    }
}
