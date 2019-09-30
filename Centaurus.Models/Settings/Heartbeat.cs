using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class Heartbeat : Message
    {
        public override MessageTypes MessageType => MessageTypes.Heartbeat;

        public static Heartbeat DecodeXdr(XdrReader stream)
        {
            //TODO: discuss if wee need to sign and verify some data here
            return new Heartbeat();
        }
    }
}
