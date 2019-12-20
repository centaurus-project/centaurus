using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class AuditorStateRequest: Message
    {
        public override MessageTypes MessageType => MessageTypes.AuditorStateRequest;

        [XdrField(0)]
        public long TargetApex { get; set; }
    }
}
