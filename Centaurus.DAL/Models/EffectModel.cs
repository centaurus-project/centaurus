using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class EffectModel
    {
        public BsonObjectId Id { get; set; }

        public int Account { get; set; }

        public int EffectType { get; set; }

        public byte[] RawEffect { get; set; }

        public long Timestamp { get; set; }

        public override string ToString()
        {
            if (Id == null) 
                return null;
            var decodedId = EffectModelIdConverter.DecodeId(Id);
            return $"{{Effect apex: {decodedId.apex}, index: {decodedId.index}, effectType: {EffectType} }}";
        }
    }
}
