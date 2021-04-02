using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class EffectsContainer
    {
        [XdrField(0)]
        public List<Effect> Effects { get; set; }
    }
}
