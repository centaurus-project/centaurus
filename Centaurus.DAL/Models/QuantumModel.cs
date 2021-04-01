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

        public byte[] Bin { get; set; }
    }
}
