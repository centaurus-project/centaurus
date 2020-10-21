using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    /// <summary>
    /// Contains last processed payment token
    /// </summary>
    public class ConstellationState
    {
        public long TxCursor { get; set; }
    }
}
