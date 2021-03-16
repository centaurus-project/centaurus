using Centaurus.DAL;
using Centaurus.DAL.Models;
using Centaurus.Models;
using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class EffectsContainerExtensions
    {
        public static EffectsContainer ToEffect(this QuantumModel quantumModel)
        {
            return XdrConverter.Deserialize<EffectsContainer>(quantumModel.Effects);
        }
    }
}
