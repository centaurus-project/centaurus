using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class EffectsGroup
    {
        [XdrField(0)]
        public List<Effect> Effects { get; set; }

        [XdrField(1)]
        public ulong AccountSequence { get; set; }

        [XdrField(2, Optional = true)]
        public RawPubKey Account { get; set; }

    }
}
