using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class QuantumModel
    {
        [BsonId]
        public long Apex { get; set; }

        public int[] Accounts { get; set; } 

        public int Type { get; set; }

        public byte[] RawQuantum { get; set; }

        public byte[] Effects { get; set; }

        public long TimeStamp { get; set; }
    }
}
