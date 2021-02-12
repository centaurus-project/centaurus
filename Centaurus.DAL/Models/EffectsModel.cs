using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class EffectsModel
    {
        public BsonObjectId Id { get; set; }

        public long Apex { get; set; }

        public int Account { get; set; }

        public List<SingleEffectModel> Effects { get; set; }

        public override string ToString()
        {
            return $"{{Effect apex: {Apex}, account: {(Account == default ? "system" : Account.ToString())} }}";
        }
    }

    public class SingleEffectModel
    {
        public int ApexIndex { get; set; }

        public byte[] RawEffect { get; set; }
    }
}
