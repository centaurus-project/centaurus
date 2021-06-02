using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class VaultModel
    {
        [BsonId]
        public int Provider { get; set; }

        public string Vault { get; set; }
    }
}
