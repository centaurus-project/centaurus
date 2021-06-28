using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class PaymentCursorModel
    {
        [BsonId]
        public string Provider { get; set; }

        public string Cursor { get; set; }
    }
}
