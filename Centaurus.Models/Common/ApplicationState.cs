using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public enum ApplicationState
    {
        Undefined = 0,
        /// <summary>
        /// First start
        /// </summary>
        WaitingForInit = 1,
        /// <summary>
        /// It has started, but not yet ready. If Alpha, then it waits for the majority to connect. If the Auditor, then it waits for a handshake
        /// </summary>
        Running = 2,
        /// <summary>
        /// Ready to process quanta
        /// </summary>
        Ready = 3,

        Rising = 4,

        Failed = 10,

        Stopped = 20
    }
}
