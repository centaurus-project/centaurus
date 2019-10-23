using Centaurus.Domain;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test
{
    public class FakeAuditorWebSocketConnection: AuditorWebSocketConnection
    {

        public FakeAuditorWebSocketConnection()
            :base(new FakeWebSocket())
        {

        }

        public void SetState(ConnectionState state)
        {
            ConnectionState = state;
        }
    }
}