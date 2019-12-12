using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class QuantumModel
    {

        //it stores ulong
        public long Apex { get; set; }

        public byte[] Account { get; set; }

        public int Type { get; set; }

        public byte[] RawQuantum { get; set; }

        public DateTime TimeStamp { get; set; }
    }
}
