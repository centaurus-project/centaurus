using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectSerializer : IXdrSerializer<Effect>
    {
        private Effect ReadEffect(XdrReader reader)
        {
            var type = reader.ReadEnum<EffectTypes>();
            switch (type)
            {
                case EffectTypes.OrderPlaced: return reader.Read<OrderPlacedEffect>();
                case EffectTypes.OrderRemoved: return reader.Read<OrderRemovedEffect>();
                case EffectTypes.Trade: return reader.Read<TradeEffect>();
                case EffectTypes.TransactionSigned: return reader.Read<TransactionSignedEffect>();
            }
            throw new NotImplementedException("Unsupported effect type " + type.ToString());
        }

        public void Deserialize(ref Effect value, XdrReader reader)
        {
            if (value == null)
            {
                value = ReadEffect(reader);
            }
        }

        public void Serialize(Effect value, XdrWriter writer)
        {
            writer.Write(value.EffectType);
        }
    }
}
