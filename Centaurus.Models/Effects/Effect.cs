using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public abstract class Effect: IXdrSerializableModel
    {
        public abstract EffectTypes EffectType { get; }
        
        //ignore it during the serialization - we need it only to decide which effects to send back to a particular user
        public RawPubKey Pubkey { get; set; }
    }
}
