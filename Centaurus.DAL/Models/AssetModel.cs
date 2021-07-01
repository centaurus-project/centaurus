using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class AssetModel
    {
        public int Id { get; set; }

        public string Code { get; set; }

        public bool IsSuspended { get; set; }
    }
}
