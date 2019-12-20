using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AlphaState : Message
    {
        public override MessageTypes MessageType => MessageTypes.AlphaState;

        [XdrField(0)]
        public ApplicationState State { get; set; }

        [XdrField(1)]
        public Snapshot LastSnapshot { get; set; }
    }
}
