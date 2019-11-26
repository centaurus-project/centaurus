using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class EffectModel
    {
        public ulong Apex { get; set; }

        public byte[] Account { get; set; }

        public int EffectType { get; set; }

        public int Index { get; set; }

        public byte[] RawEffect { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
