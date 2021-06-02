using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class ConstellationUpgradeQuantum : ConstellationSettingsRequestBase
    {
        public override MessageTypes MessageType => MessageTypes.ConstellationUpgradeRequest;
    }
}
