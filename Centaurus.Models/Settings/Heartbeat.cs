using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Heartbeat : Message
    {
        public override MessageTypes MessageType => MessageTypes.Heartbeat;
    }
}
