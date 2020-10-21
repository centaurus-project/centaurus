using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class EffectsNotification : Message, IEffectsContainer
    {
        public override MessageTypes MessageType => MessageTypes.EffectsNotification;

        [XdrField(0)]
        public List<Effect> Effects { get; set; }
    }
}