using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class BalanceModel
    {
        public BsonObjectId Id { get; set; }

        public long Amount { get; set; }
    }
}
