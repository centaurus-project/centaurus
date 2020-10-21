using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationInitQuantum : ConstellationSettingsQuantum
    {
        public override MessageTypes MessageType => MessageTypes.ConstellationInitQuantum;
    }
}
