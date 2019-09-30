using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace Centaurus
{
    public class ConnectionCloseException : Exception
    {
        public WebSocketCloseStatus Status { get; private set; }

        public string Description { get; private set; }

        public ConnectionCloseException (WebSocketCloseStatus status, string description)
        {
            Status = status;
            Description = description;
        }
    }
}
