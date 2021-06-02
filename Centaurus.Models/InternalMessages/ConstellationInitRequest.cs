using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationInitRequest : ConstellationSettingsRequestBase
    {
        public override MessageTypes MessageType => MessageTypes.ConstellationInitRequest;
    }
}
