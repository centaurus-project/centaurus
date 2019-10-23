using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class FakeAlphaWebSocketConnection : AlphaWebSocketConnection
    {
        public FakeAlphaWebSocketConnection()
            :base(new FakeWebSocket())
        {

        }

        public void SetState(ConnectionState state)
        {
            ConnectionState = state;
        }

        public void SetClientPubKey(RawPubKey pubKey)
        {
            ClientPubKey = pubKey;
        }
    }
}
