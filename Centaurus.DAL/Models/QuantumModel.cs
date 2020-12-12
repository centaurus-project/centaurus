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

        public byte[] Account { get; set; }

        public int Type { get; set; }

        public byte[] RawQuantum { get; set; }

        public long TimeStamp { get; set; }
    }
}
