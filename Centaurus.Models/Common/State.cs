using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public enum State
    {
        Undefined = 0,
        /// <summary>
        /// It has started, but not yet ready. If Alpha, then it waits for the majority to connect. If the Auditor, then it waits for a handshake
        /// </summary>
        Running = 1,
        /// <summary>
        /// Ready to process quanta
        /// </summary>
        Ready = 2,

        Rising = 3,

        Failed = 10,

        Stopped = 20
    }
}
