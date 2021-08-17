using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{

    [XdrContract]
    [XdrUnion(0, typeof(EffectsHashInfo))]
    [XdrUnion(1, typeof(EffectsInfo))]
    public abstract class EffectsInfoBase
    {
        [XdrField(0)]
        public byte[] EffectsGroupData { get; set; }
    }

    public class EffectsHashInfo : EffectsInfoBase
    {

    }

    public class EffectsInfo : EffectsInfoBase
    {

    }
}