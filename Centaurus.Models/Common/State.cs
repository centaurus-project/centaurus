using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public enum State
    {
        Undefined = 0,
        WaitingForInit = 1,
        Rising = 2,
        /// <summary>
        /// It has started, but not yet ready. If Alpha, then it waits for the majority to connect. If the Auditor, then it waits for a handshake
        /// </summary>
        Running = 3,
        /// <summary>
        /// Auditor is delayed
        /// </summary>
        Chasing = 4,
        /// <summary>
        /// Ready to process quanta
        /// </summary>
        Ready = 5,

        Failed = 10,

        Stopped = 20
    }
}
